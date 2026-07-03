using System;
using Gree.UnityWebView;
using UnityEngine;

namespace MemoAnchor.UI
{
    public sealed class KakaoPostcodeWebView
    {
        private const string WEB_VIEW_OBJECT_NAME = "KakaoPostcodeWebView";
        private const string BASE_URL = "https://postcode.map.kakao.com/";
        private const string UNITY_SCHEME_PREFIX = "unity:";
        private const string CLOSE_MESSAGE = "__memoanchor_close__";
        private const int WEB_VIEW_TEXT_ZOOM_PERCENT = 100;
        private const int TOP_SAFE_OFFSET_PIXELS = 28;
        private const int TOP_BAR_HEIGHT_PIXELS = 52;

        private static readonly string HTML = @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no'>
    <style>
        html, body { margin: 0; padding: 0; width: 100%; min-height: 100%; overflow-x: hidden; overflow-y: auto; background: #fff; }
        body { box-sizing: border-box; padding-top: " + (TOP_SAFE_OFFSET_PIXELS + TOP_BAR_HEIGHT_PIXELS) + @"px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; }
        #topbar { position: fixed; left: 0; top: " + TOP_SAFE_OFFSET_PIXELS + @"px; right: 0; height: " + TOP_BAR_HEIGHT_PIXELS + @"px; display: flex; align-items: center; justify-content: flex-end; background: #fff; z-index: 10; border-bottom: 1px solid #eee; }
        #close { appearance: none; border: 0; background: transparent; padding: 0 20px; height: " + TOP_BAR_HEIGHT_PIXELS + @"px; color: #222; font-size: 16px; }
        #wrap { width: 100%; min-height: calc(100vh - " + (TOP_SAFE_OFFSET_PIXELS + TOP_BAR_HEIGHT_PIXELS) + @"px); overflow: visible; }
    </style>
    <script src='https://t1.kakaocdn.net/mapjsapi/bundle/postcode/prod/postcode.v2.js'></script>
</head>
<body>
    <div id='topbar'><button id='close' type='button'>닫기</button></div>
    <div id='wrap'></div>
    <script>
        var wrap = document.getElementById('wrap');
        function unityCall(message) {
            if (window.Unity && window.Unity.call) {
                window.Unity.call(message);
                return;
            }

            window.location = 'unity:' + encodeURIComponent(message);
        }

        function resizeWrap(size) {
            wrap.style.height = size.height + 'px';
            document.body.style.height = (" + (TOP_SAFE_OFFSET_PIXELS + TOP_BAR_HEIGHT_PIXELS) + @" + size.height) + 'px';
        }

        function endsWithAny(value, suffixes) {
            for (var i = 0; i < suffixes.length; i++) {
                if (value.endsWith(suffixes[i])) {
                    return true;
                }
            }

            return false;
        }

        function buildAddress(data) {
            var addr = data.userSelectedType === 'R' ? data.roadAddress : data.jibunAddress;
            var extra = '';
            if (data.userSelectedType === 'R') {
                if (data.bname !== '' && endsWithAny(data.bname, ['동', '로', '가'])) {
                    extra += data.bname;
                }

                if (data.buildingName !== '' && data.apartment === 'Y') {
                    extra += (extra !== '' ? ', ' : '') + data.buildingName;
                }

                if (extra !== '') {
                    addr += ' (' + extra + ')';
                }
            }

            return {
                zonecode: data.zonecode,
                address: addr,
                roadAddress: data.roadAddress,
                jibunAddress: data.jibunAddress,
                buildingName: data.buildingName,
                bname: data.bname
            };
        }

        document.getElementById('close').addEventListener('click', function() {
            unityCall('" + CLOSE_MESSAGE + @"');
        });

        function getPostcodeConstructor() {
            if (window.kakao && window.kakao.Postcode) {
                return window.kakao.Postcode;
            }

            if (window.daum && window.daum.Postcode) {
                return window.daum.Postcode;
            }

            return null;
        }

        function showLoadError() {
            wrap.innerHTML = '<div style=""box-sizing:border-box;padding:24px;color:#333;font-size:15px;line-height:1.5;"">주소 검색 화면을 불러오지 못했습니다.<br>네트워크 상태를 확인해주세요.</div>';
        }

        function openPostcode(retryCount) {
            var Postcode = getPostcodeConstructor();
            if (!Postcode) {
                if (retryCount < 20) {
                    window.setTimeout(function() {
                        openPostcode(retryCount + 1);
                    }, 100);
                    return;
                }

                showLoadError();
                return;
            }

            new Postcode({
                oncomplete: function(data) {
                    unityCall(JSON.stringify(buildAddress(data)));
                },
                onresize: function(size) {
                    resizeWrap(size);
                },
                width: '100%',
                height: '100%'
            }).embed(wrap);
        }

        window.onload = function() {
            openPostcode(0);
        };
    </script>
</body>
</html>";

        private readonly Action<string> _onAddressSelected;
        private WebViewObject _webViewObject;

        public KakaoPostcodeWebView(Action<string> onAddressSelected)
        {
            _onAddressSelected = onAddressSelected;
        }

        public void Open()
        {
            Close();
            _ = OpenAsync();
        }

        public void Close()
        {
            if (_webViewObject == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(_webViewObject.gameObject);
            _webViewObject = null;
        }

        private async Awaitable OpenAsync()
        {
            WebViewObject webViewObject = new GameObject(WEB_VIEW_OBJECT_NAME).AddComponent<WebViewObject>();
            _webViewObject = webViewObject;
            webViewObject.Init(
                cb: OnWebViewMessage,
                err: message => Debug.LogWarning(message),
                httpErr: message => Debug.LogWarning(message),
                transparent: false,
                zoom: false,
                enableWKWebView: true);

            while (_webViewObject == webViewObject && !webViewObject.IsInitialized())
            {
                await Awaitable.NextFrameAsync();
            }

            if (_webViewObject != webViewObject)
            {
                return;
            }

            webViewObject.SetMargins(0, 0, 0, 0);
            webViewObject.SetTextZoom(WEB_VIEW_TEXT_ZOOM_PERCENT);
            webViewObject.SetScrollbarsVisibility(true);
            webViewObject.SetVisibility(true);
#if UNITY_WEBGL
            webViewObject.LoadURL("data:text/html;charset=utf-8," + Uri.EscapeDataString(HTML));
#else
            webViewObject.LoadHTML(HTML, BASE_URL);
#endif
        }

        private void OnWebViewMessage(string message)
        {
            string normalizedMessage = NormalizeWebViewMessage(message);
            if (normalizedMessage == CLOSE_MESSAGE)
            {
                Close();
                return;
            }

            Close();
            _onAddressSelected(normalizedMessage);
        }

        private static string NormalizeWebViewMessage(string message)
        {
            string normalizedMessage = message.StartsWith(UNITY_SCHEME_PREFIX)
                ? message[UNITY_SCHEME_PREFIX.Length..]
                : message;

            return Uri.UnescapeDataString(normalizedMessage);
        }
    }
}
