$ErrorActionPreference = "Stop"

$sdkDir = "D:\Android\Sdk"
$buildTools = "$sdkDir\build-tools\35.0.0"
$platform = "$sdkDir\platforms\android-35\android.jar"
$java = "C:\Users\acer\jdk-17\bin"
$projDir = "D:\POSFASHION\android\GarmentScanner"
$outDir = "D:\POSFASHION\publish\android"
$binDir = "$projDir\bin"
$objDir = "$projDir\obj"

Write-Host "===> Initializing build directories..." -ForegroundColor Cyan
if (Test-Path $binDir) { Remove-Item -Recurse -Force $binDir }
if (Test-Path $objDir) { Remove-Item -Recurse -Force $objDir }
New-Item -ItemType Directory -Path $binDir, $objDir, $outDir -Force | Out-Null

Write-Host "===> [Step 1/6] Compiling Android Resources with AAPT2..." -ForegroundColor Cyan
& "$buildTools\aapt2.exe" compile --dir "$projDir\res" -o "$binDir\res.zip"

Write-Host "===> [Step 2/6] Linking Package and Generating R.java..." -ForegroundColor Cyan
& "$buildTools\aapt2.exe" link -I "$platform" --manifest "$projDir\AndroidManifest.xml" -o "$binDir\app.unsigned.apk" --java "$projDir\src" "$binDir\res.zip" --auto-add-overlay

Write-Host "===> [Step 3/6] Compiling Java Sources with JDK 17..." -ForegroundColor Cyan
$javaFiles = Get-ChildItem -Path "$projDir\src" -Recurse -Filter "*.java" | ForEach-Object { $_.FullName }
& "$java\javac.exe" -source 17 -target 17 -cp "$platform" -d "$objDir" $javaFiles

Write-Host "===> [Step 4/6] Converting Java Bytecode to Dalvik DEX with D8..." -ForegroundColor Cyan
$classFiles = Get-ChildItem -Path "$objDir" -Recurse -Filter "*.class" | ForEach-Object { $_.FullName }
& "$buildTools\d8.bat" --output "$binDir" $classFiles

Write-Host "===> [Step 5/6] Injecting classes.dex into APK..." -ForegroundColor Cyan
& "$java\jar.exe" uf "$binDir\app.unsigned.apk" -C "$binDir" classes.dex

Write-Host "===> [Step 6/6] Zipaligning and Signing Release APK..." -ForegroundColor Cyan
& "$buildTools\zipalign.exe" -f -p 4 "$binDir\app.unsigned.apk" "$binDir\app.aligned.apk"

$keystore = "$projDir\debug.keystore"
if (-not (Test-Path $keystore)) {
    Write-Host "Creating signing keystore..."
    & "$java\keytool.exe" -genkey -v -keystore $keystore -storepass android -alias androiddebugkey -keypass android -keyalg RSA -keysize 2048 -validity 10000 -dname "CN=GarmentPOS, OU=Retail, O=GarmentERP, C=IN"
}

$finalApk = "$outDir\GarmentPOS-Scanner.apk"
& "$buildTools\apksigner.bat" sign --ks $keystore --ks-pass pass:android --ks-key-alias androiddebugkey --out $finalApk "$binDir\app.aligned.apk"

if (Test-Path $finalApk) {
    $apkItem = Get-Item $finalApk
    $sizeKb = [math]::Round($apkItem.Length / 1024, 2)
    Write-Host "`nSUCCESS! Android APK Generated: $finalApk ($sizeKb KB)" -ForegroundColor Green
} else {
    Write-Error "APK generation failed!"
}
