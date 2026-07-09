package com.memoanchor.systemui;

import android.annotation.SuppressLint;
import android.app.Activity;
import android.graphics.Color;
import android.view.Gravity;
import android.view.ViewGroup;
import android.webkit.JavascriptInterface;
import android.webkit.WebChromeClient;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.FrameLayout;
import android.widget.TextView;

public final class KakaoPostcodeOverlay {
    private static final int OVERLAY_TAG = 5108001;
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

    private static Overlay activeOverlay;

    private KakaoPostcodeOverlay() {
    }

    public static void show(Activity activity, String unityGameObjectName) {
        activity.runOnUiThread(() -> {
            if (activeOverlay != null) {
                activeOverlay.dismiss();
            }

            activeOverlay = new Overlay(activity, unityGameObjectName);
            activeOverlay.show();
        });
    }

    private static final class Overlay {
        private static final int TOP_BAR_HEIGHT_DP = 52;
        private static final int CLOSE_BUTTON_WIDTH_DP = 72;

        private final Activity activity;
        private final String unityGameObjectName;
        private FrameLayout container;
        private WebView webView;

        private Overlay(Activity activity, String unityGameObjectName) {
            this.activity = activity;
            this.unityGameObjectName = unityGameObjectName;
        }

        @SuppressLint("SetJavaScriptEnabled")
        private void show() {
            ViewGroup decorView = (ViewGroup) activity.getWindow().getDecorView();
            ViewGroup existingOverlay = decorView.findViewWithTag(OVERLAY_TAG);
            if (existingOverlay != null) {
                decorView.removeView(existingOverlay);
            }

            container = new FrameLayout(activity);
            container.setTag(OVERLAY_TAG);
            container.setClickable(true);
            container.setBackgroundColor(Color.WHITE);

            webView = new WebView(activity);
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

            FrameLayout.LayoutParams webViewLayoutParams = new FrameLayout.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT,
                    ViewGroup.LayoutParams.MATCH_PARENT);
            webViewLayoutParams.topMargin = dp(TOP_BAR_HEIGHT_DP);
            container.addView(webView, webViewLayoutParams);

            TextView closeButton = new TextView(activity);
            closeButton.setText("닫기");
            closeButton.setTextColor(Color.rgb(40, 40, 40));
            closeButton.setTextSize(16);
            closeButton.setGravity(Gravity.CENTER);
            closeButton.setOnClickListener(_view -> dismiss());

            FrameLayout.LayoutParams closeButtonLayoutParams = new FrameLayout.LayoutParams(
                    dp(CLOSE_BUTTON_WIDTH_DP),
                    dp(TOP_BAR_HEIGHT_DP),
                    Gravity.TOP | Gravity.END);
            container.addView(closeButton, closeButtonLayoutParams);

            activity.addContentView(container, new ViewGroup.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT,
                    ViewGroup.LayoutParams.MATCH_PARENT));
            webView.loadDataWithBaseURL(BASE_URL, HTML, "text/html", "utf-8", null);
        }

        private void dismiss() {
            if (webView != null) {
                webView.removeJavascriptInterface(JS_BRIDGE_NAME);
                webView.destroy();
                webView = null;
            }

            if (container != null) {
                ViewGroup parent = (ViewGroup) container.getParent();
                if (parent != null) {
                    parent.removeView(container);
                }
                container = null;
            }

            if (activeOverlay == this) {
                activeOverlay = null;
            }
        }

        private int dp(int value) {
            return Math.round(value * activity.getResources().getDisplayMetrics().density);
        }

        private final class PostcodeJavascriptBridge {
            @JavascriptInterface
            public void onAddressSelected(String payloadJson) {
                activity.runOnUiThread(() -> {
                    sendUnityMessage(unityGameObjectName, UNITY_CALLBACK_METHOD, payloadJson);
                    dismiss();
                });
            }
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
