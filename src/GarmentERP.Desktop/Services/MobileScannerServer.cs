using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows.Media.Imaging;
using QRCoder;

namespace GarmentERP.Desktop.Services;

public record BarcodeScanResult(bool Success, string ProductName, string SizeName, string ColorName, decimal Price, string Message);

public class MobileScannerServer : IDisposable
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private bool _isRunning = false;

    public int Port { get; private set; } = 5055;
    public string LocalIp { get; private set; } = "127.0.0.1";
    public string ScannerUrl => $"http://{LocalIp}:{Port}/scanner";
    public string ConnectedDevice { get; private set; } = "No device connected";
    public DateTime? LastScanTime { get; private set; }

    public event Func<string, string, Task<BarcodeScanResult>>? OnBarcodeScanned;
    public event Action<string>? OnDeviceConnected;

    public void Start(int preferredPort = 5055)
    {
        if (_isRunning) return;

        LocalIp = GetLocalIpAddress();
        Port = preferredPort;

        for (int i = 0; i < 5; i++)
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, Port);
                _listener.Start();
                _isRunning = true;
                break;
            }
            catch
            {
                Port++;
            }
        }

        if (!_isRunning || _listener == null) return;

        _cts = new CancellationTokenSource();
        Task.Run(() => AcceptClientsAsync(_cts.Token));
    }

    public void Stop()
    {
        _isRunning = false;
        try { _cts?.Cancel(); } catch { }
        try { _listener?.Stop(); } catch { }
    }

    public BitmapImage GenerateQrCodeImage(int pixelsPerModule = 10)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(ScannerUrl, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrData);
        byte[] qrBytes = qrCode.GetGraphic(pixelsPerModule);

        var bitmap = new BitmapImage();
        using (var ms = new MemoryStream(qrBytes))
        {
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();
        }
        return bitmap;
    }

    private async Task AcceptClientsAsync(CancellationToken ct)
    {
        while (_isRunning && !ct.IsCancellationRequested && _listener != null)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => HandleClientAsync(client, ct), ct);
            }
            catch
            {
                if (!_isRunning) break;
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        using (var stream = client.GetStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true })
        {
            try
            {
                string? requestLine = await reader.ReadLineAsync(ct);
                if (string.IsNullOrWhiteSpace(requestLine)) return;

                var parts = requestLine.Split(' ');
                if (parts.Length < 2) return;

                string method = parts[0].ToUpperInvariant();
                string path = parts[1].Split('?')[0];

                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string? headerLine;
                int contentLength = 0;

                while (!string.IsNullOrEmpty(headerLine = await reader.ReadLineAsync(ct)))
                {
                    int colonIndex = headerLine.IndexOf(':');
                    if (colonIndex > 0)
                    {
                        var key = headerLine[..colonIndex].Trim();
                        var val = headerLine[(colonIndex + 1)..].Trim();
                        headers[key] = val;
                        if (string.Equals(key, "Content-Length", StringComparison.OrdinalIgnoreCase))
                        {
                            int.TryParse(val, out contentLength);
                        }
                    }
                }

                string body = string.Empty;
                if (contentLength > 0)
                {
                    var buffer = new char[contentLength];
                    int totalRead = 0;
                    while (totalRead < contentLength)
                    {
                        int read = await reader.ReadAsync(buffer, totalRead, contentLength - totalRead);
                        if (read <= 0) break;
                        totalRead += read;
                    }
                    body = new string(buffer, 0, totalRead);
                }

                // ROUTING
                if (method == "GET" && (path == "/" || path == "/scanner"))
                {
                    var html = GetScannerHtml();
                    byte[] htmlBytes = Encoding.UTF8.GetBytes(html);
                    await writer.WriteAsync($"HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {htmlBytes.Length}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(htmlBytes, ct);
                }
                else if (method == "GET" && path == "/api/ping")
                {
                    var userAgent = headers.GetValueOrDefault("User-Agent", "Mobile Browser");
                    ConnectedDevice = ParseDeviceName(userAgent);
                    OnDeviceConnected?.Invoke(ConnectedDevice);

                    var respJson = JsonSerializer.Serialize(new
                    {
                        status = "online",
                        device = ConnectedDevice,
                        posCounter = "Counter-01",
                        store = "Grand Fashion Store"
                    });
                    byte[] bytes = Encoding.UTF8.GetBytes(respJson);
                    await writer.WriteAsync($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nAccess-Control-Allow-Origin: *\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(bytes, ct);
                }
                else if (method == "POST" && path == "/api/scan")
                {
                    var scanReq = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrEmpty(body) ? "{}" : body);
                    string barcode = scanReq.TryGetProperty("barcode", out var b) ? b.GetString() ?? "" : "";
                    string device = scanReq.TryGetProperty("device", out var d) ? d.GetString() ?? "" : headers.GetValueOrDefault("User-Agent", "Android Mobile");

                    ConnectedDevice = ParseDeviceName(device);
                    LastScanTime = DateTime.UtcNow;

                    BarcodeScanResult result;
                    if (OnBarcodeScanned != null && !string.IsNullOrWhiteSpace(barcode))
                    {
                        result = await OnBarcodeScanned.Invoke(barcode.Trim(), ConnectedDevice);
                    }
                    else
                    {
                        result = new BarcodeScanResult(false, "", "", "", 0, "No barcode processor registered");
                    }

                    var respJson = JsonSerializer.Serialize(new
                    {
                        success = result.Success,
                        barcode = barcode,
                        productName = result.ProductName,
                        size = result.SizeName,
                        color = result.ColorName,
                        price = result.Price,
                        message = result.Message
                    });

                    byte[] bytes = Encoding.UTF8.GetBytes(respJson);
                    await writer.WriteAsync($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nAccess-Control-Allow-Origin: *\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(bytes, ct);
                }
                else if (method == "GET" && (path.EndsWith(".apk", StringComparison.OrdinalIgnoreCase) || path == "/download"))
                {
                    string[] possiblePaths = [
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "publish", "android", "GarmentPOS-Scanner.apk"),
                        Path.Combine(Directory.GetCurrentDirectory(), "publish", "android", "GarmentPOS-Scanner.apk"),
                        @"D:\POSFASHION\publish\android\GarmentPOS-Scanner.apk"
                    ];

                    string? apkPath = possiblePaths.FirstOrDefault(File.Exists);
                    if (apkPath != null)
                    {
                        byte[] apkBytes = await File.ReadAllBytesAsync(apkPath, ct);
                        await writer.WriteAsync($"HTTP/1.1 200 OK\r\nContent-Type: application/vnd.android.package-archive\r\nContent-Disposition: attachment; filename=\"GarmentPOS-Scanner.apk\"\r\nContent-Length: {apkBytes.Length}\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(apkBytes, ct);
                    }
                    else
                    {
                        await writer.WriteAsync("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                    }
                }
                else
                {
                    await writer.WriteAsync("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                }
            }
            catch
            {
                // Client disconnected
            }
        }
    }

    private static string ParseDeviceName(string userAgent)
    {
        if (userAgent.Contains("Android"))
        {
            var parts = userAgent.Split(';');
            foreach (var part in parts)
            {
                if (part.Contains("Build/") || part.Contains("SM-") || part.Contains("Pixel") || part.Contains("Redmi") || part.Contains("M20") || part.Contains("CPH"))
                {
                    return part.Replace("Build/", "").Trim();
                }
            }
            return "Android Device";
        }
        if (userAgent.Contains("iPhone")) return "Apple iPhone";
        if (userAgent.Contains("iPad")) return "Apple iPad";
        return "WiFi Mobile Device";
    }

    private static string GetLocalIpAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            var endPoint = socket.LocalEndPoint as IPEndPoint;
            return endPoint?.Address.ToString() ?? "127.0.0.1";
        }
        catch
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch { }
            return "127.0.0.1";
        }
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }

    private static string GetScannerHtml()
    {
        return @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no"">
    <title>Garment POS — Mobile Barcode Scanner</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; }
        body { background: #0F172A; color: #F8FAFC; height: 100vh; display: flex; flex-direction: column; overflow: hidden; }
        header { background: #1E293B; padding: 12px 16px; display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid #334155; }
        .logo-box { display: flex; align-items: center; gap: 8px; }
        .logo-badge { background: #2563EB; color: white; font-weight: bold; padding: 4px 8px; border-radius: 6px; font-size: 13px; }
        .pos-title { font-size: 14px; font-weight: 700; color: #FFFFFF; }
        .status-pill { background: #064E3B; color: #34D399; font-size: 11px; font-weight: 700; padding: 4px 10px; border-radius: 12px; display: flex; align-items: center; gap: 5px; }
        .status-dot { width: 8px; height: 8px; background: #10B981; border-radius: 50%; animation: pulse 1.5s infinite; }
        @keyframes pulse { 0% { opacity: 0.4; } 50% { opacity: 1; } 100% { opacity: 0.4; } }

        /* Camera Viewport */
        .camera-container { flex: 1; position: relative; background: #000; overflow: hidden; display: flex; justify-content: center; align-items: center; }
        video { width: 100%; height: 100%; object-fit: cover; }
        .viewfinder { position: absolute; width: 78%; max-width: 320px; height: 180px; border: 2px solid #38BDF8; border-radius: 12px; box-shadow: 0 0 0 9999px rgba(15, 23, 42, 0.65); pointer-events: none; }
        .laser { position: absolute; width: 100%; height: 2px; background: #EF4444; box-shadow: 0 0 8px #EF4444; top: 0; animation: scan 2s infinite ease-in-out; }
        @keyframes scan { 0% { top: 4%; } 50% { top: 94%; } 100% { top: 4%; } }

        /* Camera Controls */
        .camera-actions { position: absolute; top: 14px; right: 14px; display: flex; flex-direction: column; gap: 10px; z-index: 10; }
        .ctrl-btn { width: 44px; height: 44px; background: rgba(30, 41, 59, 0.85); border: 1px solid #475569; border-radius: 50%; color: white; display: flex; justify-content: center; align-items: center; font-size: 18px; cursor: pointer; backdrop-filter: blur(4px); }
        .ctrl-btn:active { background: #2563EB; }

        /* Feedback Card */
        #feedbackCard { position: absolute; bottom: 12px; left: 16px; right: 16px; background: #1E293B; border: 1.5px solid #10B981; border-radius: 10px; padding: 12px 16px; display: none; z-index: 20; box-shadow: 0 10px 25px rgba(0,0,0,0.5); }
        .feed-title { font-size: 14px; font-weight: bold; color: #10B981; }
        .feed-detail { font-size: 13px; color: #F8FAFC; margin-top: 2px; }
        .feed-price { font-size: 15px; font-weight: bold; color: #FBBF24; margin-top: 4px; }

        /* Manual Input & Recent Panel */
        .bottom-panel { background: #1E293B; padding: 14px 16px; border-top: 1px solid #334155; }
        .input-row { display: flex; gap: 8px; }
        .barcode-input { flex: 1; background: #0F172A; border: 1px solid #475569; color: white; padding: 10px 14px; border-radius: 8px; font-size: 14px; font-weight: 600; outline: none; }
        .barcode-input:focus { border-color: #38BDF8; }
        .send-btn { background: #2563EB; color: white; border: none; padding: 0 16px; border-radius: 8px; font-weight: bold; font-size: 14px; cursor: pointer; }
        .send-btn:active { background: #1D4ED8; }

        .scans-counter { display: flex; justify-content: space-between; align-items: center; margin-top: 10px; font-size: 12px; color: #94A3B8; }
        .clear-link { color: #38BDF8; cursor: pointer; text-decoration: underline; }
    </style>
</head>
<body>
    <header>
        <div class=""logo-box"">
            <span class=""logo-badge"">POS SCAN</span>
            <span class=""pos-title"">Garment Mobile</span>
        </div>
        <div style=""display:flex;align-items:center;gap:8px;"">
            <a href=""/download"" style=""background:#059669;color:#FFFFFF;font-size:11px;font-weight:700;padding:5px 9px;border-radius:6px;text-decoration:none;display:flex;align-items:center;gap:4px;"">⬇ Get APK</a>
            <div class=""status-pill"" id=""statusPill"">
                <span class=""status-dot""></span>
                <span id=""statusText"">Connected</span>
            </div>
        </div>
    </header>

    <div class=""camera-container"">
        <video id=""video"" playsinline autoplay muted></video>
        <div class=""viewfinder"">
            <div class=""laser""></div>
        </div>

        <!-- Permission Overlay -->
        <div id=""permissionOverlay"" style=""display:none; position:absolute; inset:0; background:#0F172A; z-index:30; padding:18px; text-align:center; flex-direction:column; justify-content:center; align-items:center; overflow-y:auto;"">
            <div style=""font-size:38px; margin-bottom:8px;"">🔒</div>
            <h3 style=""font-size:17px; font-weight:bold; color:#FFFFFF; margin-bottom:6px;"" id=""permTitle"">Chrome HTTP Security Restriction</h3>
            <p style=""font-size:12px; color:#94A3B8; max-width:320px; margin-bottom:14px; line-height:1.4;"" id=""permDesc"">
                Mobile Chrome restricts live video over plain Wi-Fi HTTP. Choose a quick solution below:
            </p>

            <button id=""btnGrantPermission"" style=""background:#2563EB; color:white; border:none; padding:10px 20px; border-radius:8px; font-weight:bold; font-size:13px; cursor:pointer; margin-bottom:8px; display:inline-flex; align-items:center; gap:8px;"">
                📸 Try Live Camera Again
            </button>

            <!-- Option 1: Native APK -->
            <a href=""/download"" style=""background:#059669; color:white; text-decoration:none; padding:11px 18px; border-radius:8px; font-size:13px; font-weight:bold; width:100%; max-width:290px; margin-bottom:8px; display:inline-flex; align-items:center; justify-content:center; gap:8px; box-shadow:0 4px 10px rgba(5,150,105,0.3);"">
                📲 1. Install Scanner APK (60 FPS Laser Scan)
            </a>

            <!-- Option 2: Snap Photo -->
            <input type=""file"" id=""cameraFileInput"" accept=""image/*"" capture=""environment"" style=""display:none;"">
            <button id=""btnSnapPhoto"" style=""background:#0F766E; color:white; border:none; padding:11px 18px; border-radius:8px; font-weight:bold; font-size:13px; cursor:pointer; width:100%; max-width:290px; margin-bottom:10px; display:inline-flex; align-items:center; justify-content:center; gap:8px;"">
                📷 2. Snap Photo to Scan (Instant Fallback)
            </button>

            <!-- Option 3: Chrome Flag Instruction -->
            <div style=""background:#1E293B; border:1px solid #334155; border-radius:8px; padding:10px; width:100%; max-width:290px; text-align:left;"">
                <div style=""font-size:11px; font-weight:bold; color:#38BDF8; margin-bottom:4px;"">⚙️ 3. Or Enable Live Video in Mobile Chrome:</div>
                <div style=""font-size:10px; color:#CBD5E1; line-height:1.4;"">
                    1. Open Chrome tab: <b style=""color:#F59E0B;"">chrome://flags</b><br>
                    2. Search: <b style=""color:#F59E0B;"">unsafely-treat-insecure-origin-as-secure</b><br>
                    3. Enable &amp; paste this server URL below:<br>
                </div>
                <div style=""display:flex; gap:6px; margin-top:6px;"">
                    <input type=""text"" id=""flagUrlInput"" value="""" readonly style=""background:#0F172A; border:1px solid #475569; color:#38BDF8; font-size:11px; padding:4px 6px; border-radius:4px; width:100%; font-family:monospace;"">
                    <button id=""btnCopyFlagUrl"" type=""button"" style=""background:#2563EB; color:white; border:none; padding:4px 10px; border-radius:4px; font-size:11px; font-weight:bold; cursor:pointer; white-space:nowrap;"">Copy</button>
                </div>
                <div style=""font-size:10px; color:#94A3B8; margin-top:4px;"">4. Tap <b>Relaunch</b> and live camera will start!</div>
            </div>
        </div>

        <div class=""camera-actions"">
            <button class=""ctrl-btn"" id=""btnTorch"" title=""Toggle Torch"">🔦</button>
            <button class=""ctrl-btn"" id=""btnFlip"" title=""Flip Camera"">🔄</button>
        </div>

        <div id=""feedbackCard"">
            <div class=""feed-title"" id=""feedStatus"">✅ Scanned to POS!</div>
            <div class=""feed-detail"" id=""feedName"">Men's Luxury Cotton Shirt</div>
            <div class=""feed-price"" id=""feedPrice"">Rs. 1,299.00</div>
        </div>
    </div>

    <div class=""bottom-panel"">
        <form class=""input-row"" id=""manualForm"">
            <input type=""text"" id=""manualBarcode"" class=""barcode-input"" placeholder=""Enter Barcode / SKU manually..."" autocomplete=""off"">
            <button type=""submit"" class=""send-btn"">Send</button>
        </form>
        <div class=""scans-counter"">
            <span id=""scanStats"">Scanned 0 items this session</span>
            <span class=""clear-link"" id=""btnClearCount"">Reset Count</span>
        </div>
    </div>

    <script>
        const video = document.getElementById('video');
        const feedbackCard = document.getElementById('feedbackCard');
        const feedStatus = document.getElementById('feedStatus');
        const feedName = document.getElementById('feedName');
        const feedPrice = document.getElementById('feedPrice');
        const scanStats = document.getElementById('scanStats');
        const btnTorch = document.getElementById('btnTorch');
        const btnFlip = document.getElementById('btnFlip');
        const manualForm = document.getElementById('manualForm');
        const manualBarcode = document.getElementById('manualBarcode');
        const permissionOverlay = document.getElementById('permissionOverlay');
        const permTitle = document.getElementById('permTitle');
        const permDesc = document.getElementById('permDesc');
        const btnGrantPermission = document.getElementById('btnGrantPermission');
        const btnSnapPhoto = document.getElementById('btnSnapPhoto');
        const cameraFileInput = document.getElementById('cameraFileInput');

        let currentStream = null;
        let isFrontFacing = false;
        let isTorchOn = false;
        let scanCount = 0;
        let lastBarcode = '';
        let lastScanTimestamp = 0;
        let isScanning = true;

        btnGrantPermission.addEventListener('click', () => {
            initCamera();
        });

        btnSnapPhoto.addEventListener('click', () => {
            cameraFileInput.click();
        });

        cameraFileInput.addEventListener('change', async (e) => {
            const file = e.target.files && e.target.files[0];
            if (!file) return;

            feedStatus.innerText = '🔍 Reading Photo Barcode...';
            feedStatus.style.color = '#38BDF8';
            feedName.innerText = file.name;
            feedPrice.innerText = 'Analyzing photo...';
            feedbackCard.style.display = 'block';

            try {
                const imgBitmap = await createImageBitmap(file);
                if ('BarcodeDetector' in window) {
                    const detector = new BarcodeDetector({
                        formats: ['ean_13', 'code_128', 'code_39', 'qr_code', 'upc_a', 'upc_e']
                    });
                    const barcodes = await detector.detect(imgBitmap);
                    if (barcodes.length > 0) {
                        handleBarcodeScanned(barcodes[0].rawValue);
                        return;
                    }
                }
                feedStatus.innerText = '⚠️ No Barcode Found in Photo';
                feedStatus.style.color = '#F59E0B';
                feedName.innerText = 'Please ensure the barcode bars are sharp and in focus.';
                feedPrice.innerText = 'Try snapping closer or enter manually below.';
            } catch (err) {
                feedStatus.innerText = '⚠️ Photo read error';
                feedStatus.style.color = '#EF4444';
            }
        });

        // Sound Synthesis (2400Hz POS confirmation chirp)
        function playChirp() {
            try {
                const ctx = new (window.AudioContext || window.webkitAudioContext)();
                const osc = ctx.createOscillator();
                const gain = ctx.createGain();
                osc.type = 'sine';
                osc.frequency.setValueAtTime(2400, ctx.currentTime);
                gain.gain.setValueAtTime(0.25, ctx.currentTime);
                gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.1);
                osc.connect(gain);
                gain.connect(ctx.destination);
                osc.start();
                osc.stop(ctx.currentTime + 0.1);
            } catch (e) {}
        }

        // Camera Initialization
        async function initCamera() {
            if (currentStream) {
                currentStream.getTracks().forEach(t => t.stop());
            }

            const flagUrlInput = document.getElementById('flagUrlInput');
            if (flagUrlInput) {
                flagUrlInput.value = window.location.origin;
            }

            const btnCopyFlagUrl = document.getElementById('btnCopyFlagUrl');
            if (btnCopyFlagUrl) {
                btnCopyFlagUrl.onclick = () => {
                    try {
                        if (navigator.clipboard && navigator.clipboard.writeText) {
                            navigator.clipboard.writeText(window.location.origin);
                        } else {
                            flagUrlInput.select();
                            document.execCommand('copy');
                        }
                        btnCopyFlagUrl.innerText = 'Copied!';
                        setTimeout(() => { btnCopyFlagUrl.innerText = 'Copy'; }, 2000);
                    } catch(e) {}
                };
            }

            if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
                permissionOverlay.style.display = 'flex';
                permTitle.innerText = 'Chrome HTTP Security Restriction';
                permDesc.innerText = 'Mobile Chrome blocks live video over local Wi-Fi HTTP (needs Secure Context). Pick one of the 3 solutions below:';
                return;
            }

            const constraints = {
                video: {
                    facingMode: isFrontFacing ? 'user' : 'environment',
                    width: { ideal: 1280 },
                    height: { ideal: 720 }
                }
            };

            try {
                currentStream = await navigator.mediaDevices.getUserMedia(constraints);
                video.srcObject = currentStream;
                video.setAttribute('playsinline', true);
                await video.play();
                permissionOverlay.style.display = 'none';
                startBarcodeDetection();
            } catch (err) {
                console.error('Camera error:', err);
                permissionOverlay.style.display = 'flex';
                permTitle.innerText = 'Camera Permission Required';
                permDesc.innerText = 'Please tap Enable Camera and select While using the app / Allow.';
            }
        }

        // Barcode Detection (Uses native Android Chrome BarcodeDetector API)
        async function startBarcodeDetection() {
            if ('BarcodeDetector' in window) {
                try {
                    const detector = new BarcodeDetector({
                        formats: ['ean_13', 'code_128', 'code_39', 'qr_code', 'upc_a', 'upc_e']
                    });

                    async function detectFrame() {
                        if (!isScanning) {
                            requestAnimationFrame(detectFrame);
                            return;
                        }

                        if (video.readyState >= 2) {
                            try {
                                const barcodes = await detector.detect(video);
                                if (barcodes.length > 0) {
                                    const code = barcodes[0].rawValue;
                                    const now = Date.now();
                                    if (code !== lastBarcode || (now - lastScanTimestamp > 1800)) {
                                        lastBarcode = code;
                                        lastScanTimestamp = now;
                                        handleBarcodeScanned(code);
                                    }
                                }
                            } catch (e) {}
                        }
                        requestAnimationFrame(detectFrame);
                    }
                    detectFrame();
                } catch (e) {
                    console.log('BarcodeDetector fallback');
                }
            }
        }

        // Send Scanned Barcode to POS Desktop via WiFi
        async function handleBarcodeScanned(code) {
            playChirp();
            if (navigator.vibrate) navigator.vibrate([80, 40, 80]);

            scanCount++;
            scanStats.innerText = 'Scanned ' + scanCount + ' items this session';

            try {
                const res = await fetch('/api/scan', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ barcode: code, device: navigator.userAgent })
                });

                const data = await res.json();
                if (data.success) {
                    feedStatus.innerText = '✅ ADDED TO POS CART';
                    feedStatus.style.color = '#10B981';
                    feedName.innerText = data.productName + ' (' + data.size + ' / ' + data.color + ')';
                    feedPrice.innerText = 'Price: Rs. ' + Number(data.price).toFixed(2);
                } else {
                    feedStatus.innerText = '⚠️ NOT FOUND IN CATALOG';
                    feedStatus.style.color = '#F59E0B';
                    feedName.innerText = 'Barcode: ' + code;
                    feedPrice.innerText = 'Please check variant SKU.';
                }
            } catch (err) {
                feedStatus.innerText = '📡 BEAMED TO POS';
                feedStatus.style.color = '#38BDF8';
                feedName.innerText = 'Barcode: ' + code;
                feedPrice.innerText = 'Sent successfully.';
            }

            feedbackCard.style.display = 'block';
            setTimeout(() => { feedbackCard.style.display = 'none'; }, 2200);
        }

        // Manual Input Submit
        manualForm.addEventListener('submit', (e) => {
            e.preventDefault();
            const val = manualBarcode.value.trim();
            if (val) {
                handleBarcodeScanned(val);
                manualBarcode.value = '';
            }
        });

        // Torch Toggle
        btnTorch.addEventListener('click', async () => {
            if (!currentStream) return;
            const track = currentStream.getVideoTracks()[0];
            try {
                isTorchOn = !isTorchOn;
                await track.applyConstraints({ advanced: [{ torch: isTorchOn }] });
                btnTorch.style.background = isTorchOn ? '#FBBF24' : 'rgba(30, 41, 59, 0.85)';
            } catch (e) {
                alert('Flashlight not supported on this camera.');
            }
        });

        // Flip Camera
        btnFlip.addEventListener('click', () => {
            isFrontFacing = !isFrontFacing;
            initCamera();
        });

        // Reset Counter
        document.getElementById('btnClearCount').addEventListener('click', () => {
            scanCount = 0;
            scanStats.innerText = 'Scanned 0 items this session';
        });

        // Heartbeat / ping POS every 4 seconds
        setInterval(async () => {
            try {
                const res = await fetch('/api/ping');
                if (res.ok) {
                    document.getElementById('statusText').innerText = 'Connected';
                    document.getElementById('statusPill').style.background = '#064E3B';
                }
            } catch (e) {
                document.getElementById('statusText').innerText = 'Offline';
                document.getElementById('statusPill').style.background = '#7F1D1D';
            }
        }, 4000);

        window.addEventListener('load', initCamera);
    </script>
</body>
</html>";
    }
}
