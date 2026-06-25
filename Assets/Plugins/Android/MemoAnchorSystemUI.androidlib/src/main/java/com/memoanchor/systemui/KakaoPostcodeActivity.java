package com.memoanchor.systemui;

import android.annotation.SuppressLint;
import android.app.Activity;
import android.graphics.Color;
import android.os.Bundle;
import android.view.ViewGroup;
import android.webkit.JavascriptInterface;
import android.webkit.WebChromeClient;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;

public class KakaoPostcodeActivity extends Activity {
    public static final String EXTRA_UNITY_GAME_OBJECT = "unityGameObjectName";

    private static final String UNITY_CALLBACK_METHOD = "OnAddressSearchResult";
    private static final String JS_BRIDGE_NAME = "MemoAnchorAddressBridge";
    private static final String BASE_URL = "https://postcode.map.kakao.com/";
    private static final String HTML = "<!DOCTYPE html><html><head><meta charset='utf-8'>"
            + "<meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no'>"
            + "<style>html,body{margin:0;padding:0;width:100%;min-height:100%;overflow-x:hidden;overflow-y:auto;}"
            + "body{box-sizing:border-box;padding-top:50px;}"
            + "#wrap{width:100%;height:100%;}"
            + "</style>"
            + "<script src='https://t1.kakaocdn.net/mapjsapi/bundle/postcode/prod/postcode.v2.js'></script>"
            + "</head><body><div id='wrap'></div><script>"
            + "var wrap=document.getElementById('wrap');"
            + "function resizeWrap(size){wrap.style.height=size.height+'px';}"
            + "function buildAddress(data){var addr=data.userSelectedType==='R'?data.roadAddress:data.jibunAddress;"
            + "var extra='';"
            + "if(data.userSelectedType==='R'){"
            + "if(data.bname!==''&&/[동로가]$/.test(data.bname)){extra+=data.bname;}"
            + "if(data.buildingName!==''&&data.apartment==='Y'){extra+=(extra!==''?', ':'')+data.buildingName;}"
            + "if(extra!==''){addr+=' ('+extra+')';}}"
            + "return {zonecode:data.zonecode,address:addr,roadAddress:data.roadAddress,jibunAddress:data.jibunAddress,buildingName:data.buildingName,bname:data.bname};}"
            + "window.onload=function(){new kakao.Postcode({oncomplete:function(data){"
            + "window." + JS_BRIDGE_NAME + ".onAddressSelected(JSON.stringify(buildAddress(data)));"
            + "},onresize:function(size){resizeWrap(size);},width:'100%',height:'100%'}).embed(wrap);};"
            + "</script></body></html>";

    private String unityGameObjectName;
    private WebView webView;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        unityGameObjectName = getIntent().getStringExtra(EXTRA_UNITY_GAME_OBJECT);
        SystemBarsHelper.apply(getWindow());
        getWindow().setStatusBarColor(Color.TRANSPARENT);
        getWindow().setNavigationBarColor(Color.WHITE);
        getWindow().getDecorView().setBackgroundColor(Color.WHITE);
        setupWebView();
    }

    @SuppressLint("SetJavaScriptEnabled")
    private void setupWebView() {
        webView = new WebView(this);
        webView.setLayoutParams(new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT,
                ViewGroup.LayoutParams.MATCH_PARENT));

        WebSettings settings = webView.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setJavaScriptCanOpenWindowsAutomatically(true);
        settings.setSupportMultipleWindows(false);
        settings.setUseWideViewPort(true);

        webView.setWebChromeClient(new WebChromeClient());
        webView.setWebViewClient(new WebViewClient());
        webView.addJavascriptInterface(new PostcodeJavascriptBridge(), JS_BRIDGE_NAME);
        webView.setVerticalScrollBarEnabled(true);
        setContentView(webView);
        webView.loadDataWithBaseURL(BASE_URL, HTML, "text/html", "utf-8", null);
    }

    @Override
    protected void onDestroy() {
        if (webView != null) {
            webView.removeJavascriptInterface(JS_BRIDGE_NAME);
            webView.destroy();
            webView = null;
        }
        super.onDestroy();
    }

    private final class PostcodeJavascriptBridge {
        @JavascriptInterface
        public void onAddressSelected(String payloadJson) {
            runOnUiThread(() -> {
                sendUnityMessage(unityGameObjectName, UNITY_CALLBACK_METHOD, payloadJson);
                finish();
            });
        }
    }

    private static void sendUnityMessage(String gameObjectName, String methodName, String message) {
        try {
            Class<?> unityPlayerClass = Class.forName("com.unity3d.player.UnityPlayer");
            unityPlayerClass
                    .getMethod("UnitySendMessage", String.class, String.class, String.class)
                    .invoke(null, gameObjectName, methodName, message);
        } catch (Exception exception) {
            exception.printStackTrace();
        }
    }
}
