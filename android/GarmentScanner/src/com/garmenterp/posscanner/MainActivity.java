package com.garmenterp.posscanner;

import android.Manifest;
import android.app.Activity;
import android.content.Context;
import android.content.SharedPreferences;
import android.content.pm.PackageManager;
import android.os.Build;
import android.os.Bundle;
import android.view.View;
import android.view.inputmethod.InputMethodManager;
import android.webkit.PermissionRequest;
import android.webkit.WebChromeClient;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.Button;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.Toast;

public class MainActivity extends Activity {

    private static final int PERMISSION_REQ_CODE = 101;
    private static final String PREF_NAME = "PosScannerPrefs";
    private static final String KEY_SERVER_URL = "ServerUrl";

    private WebView webView;
    private EditText etServerUrl;
    private Button btnConnect;
    private LinearLayout loadingLayout;
    private SharedPreferences prefs;
    private PermissionRequest pendingWebPermission;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        prefs = getSharedPreferences(PREF_NAME, Context.MODE_PRIVATE);

        webView = (WebView) findViewById(R.id.webView);
        etServerUrl = (EditText) findViewById(R.id.etServerUrl);
        btnConnect = (Button) findViewById(R.id.btnConnect);
        loadingLayout = (LinearLayout) findViewById(R.id.loadingLayout);

        // Pre-fill last URL or default to current PC IP
        String savedUrl = prefs.getString(KEY_SERVER_URL, "http://10.240.75.9:5055/scanner");
        etServerUrl.setText(savedUrl);

        setupWebView();
        checkPermissions();

        btnConnect.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                connectToServer();
            }
        });

        // Auto-connect on start if URL saved
        if (savedUrl != null && !savedUrl.isEmpty()) {
            webView.loadUrl(savedUrl);
        }
    }

    private void setupWebView() {
        WebSettings settings = webView.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setDatabaseEnabled(true);
        settings.setMediaPlaybackRequiresUserGesture(false);
        settings.setAllowFileAccess(true);
        settings.setAllowContentAccess(true);
        settings.setLoadWithOverviewMode(true);
        settings.setUseWideViewPort(true);

        webView.setWebViewClient(new WebViewClient() {
            @Override
            public void onPageFinished(WebView view, String url) {
                super.onPageFinished(view, url);
                loadingLayout.setVisibility(View.GONE);
            }

            @Override
            public void onReceivedError(WebView view, int errorCode, String description, String failingUrl) {
                loadingLayout.setVisibility(View.GONE);
                Toast.makeText(MainActivity.this, "Cannot reach POS Server: " + description, Toast.LENGTH_LONG).show();
            }
        });

        webView.setWebChromeClient(new WebChromeClient() {
            @Override
            public void onPermissionRequest(final PermissionRequest request) {
                runOnUiThread(new Runnable() {
                    @Override
                    public void run() {
                        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
                            if (checkSelfPermission(Manifest.permission.CAMERA) == PackageManager.PERMISSION_GRANTED) {
                                request.grant(request.getResources());
                            } else {
                                pendingWebPermission = request;
                                requestPermissions(new String[]{Manifest.permission.CAMERA}, PERMISSION_REQ_CODE);
                            }
                        } else {
                            request.grant(request.getResources());
                        }
                    }
                });
            }
        });
    }

    private void connectToServer() {
        String url = etServerUrl.getText().toString().trim();
        if (url.isEmpty()) {
            Toast.makeText(this, "Please enter POS Server URL", Toast.LENGTH_SHORT).show();
            return;
        }

        if (!url.startsWith("http://") && !url.startsWith("https://")) {
            url = "http://" + url;
        }
        if (!url.endsWith("/scanner") && !url.contains(":")) {
            url = url + ":5055/scanner";
        }

        etServerUrl.setText(url);
        prefs.edit().putString(KEY_SERVER_URL, url).apply();

        hideKeyboard();
        loadingLayout.setVisibility(View.VISIBLE);
        webView.loadUrl(url);
        Toast.makeText(this, "Connecting to POS Scanner...", Toast.LENGTH_SHORT).show();
    }

    private void hideKeyboard() {
        InputMethodManager imm = (InputMethodManager) getSystemService(Context.INPUT_METHOD_SERVICE);
        if (imm != null && getCurrentFocus() != null) {
            imm.hideSoftInputFromWindow(getCurrentFocus().getWindowToken(), 0);
        }
    }

    private void checkPermissions() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
            if (checkSelfPermission(Manifest.permission.CAMERA) != PackageManager.PERMISSION_GRANTED) {
                requestPermissions(new String[]{
                        Manifest.permission.CAMERA,
                        Manifest.permission.INTERNET,
                        Manifest.permission.VIBRATE
                }, PERMISSION_REQ_CODE);
            }
        }
    }

    @Override
    public void onRequestPermissionsResult(int requestCode, String[] permissions, int[] grantResults) {
        super.onRequestPermissionsResult(requestCode, permissions, grantResults);
        if (requestCode == PERMISSION_REQ_CODE) {
            if (grantResults.length > 0 && grantResults[0] == PackageManager.PERMISSION_GRANTED) {
                Toast.makeText(this, "Camera access granted!", Toast.LENGTH_SHORT).show();
                if (pendingWebPermission != null) {
                    pendingWebPermission.grant(pendingWebPermission.getResources());
                    pendingWebPermission = null;
                }
                webView.reload();
            } else {
                if (pendingWebPermission != null) {
                    pendingWebPermission.deny();
                    pendingWebPermission = null;
                }
                Toast.makeText(this, "Camera permission needed for barcode scanning.", Toast.LENGTH_LONG).show();
            }
        }
    }

    @Override
    public void onBackPressed() {
        if (webView.canGoBack()) {
            webView.goBack();
        } else {
            super.onBackPressed();
        }
    }
}
