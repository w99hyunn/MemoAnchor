using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MemoAnchor.UI
{
    [RequireComponent(typeof(Tab_ScanView))]
    public class Tab_ScanController : MonoBehaviour
    {
        private const string AndroidBridgeClass = "com.memoanchor.systemui.AddressSearchBridge";

        private Tab_ScanView _view;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void MemoAnchor_OpenKakaoPostcodeSearch(string unityGameObjectName);
#endif

        [Serializable]
        private class AddressSearchResult
        {
            public string zonecode;
            public string address;
            public string roadAddress;
            public string jibunAddress;
            public string buildingName;
            public string bname;
        }

        private void Awake()
        {
            TryGetComponent<Tab_ScanView>(out _view);
        }

        private void Start()
        {
            _view.AddressButton.clicked += OnClickAddressButton;
        }

        private void OnDisable()
        {
            _view.AddressButton.clicked -= OnClickAddressButton;
        }

        private void OnClickAddressButton()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using var bridge = new AndroidJavaClass(AndroidBridgeClass);
            bridge.CallStatic("open", activity, gameObject.name);
#elif UNITY_IOS && !UNITY_EDITOR
            MemoAnchor_OpenKakaoPostcodeSearch(gameObject.name);
#endif
        }

        public void OnAddressSearchResult(string payloadJson)
        {
            if (payloadJson.Length == 0)
            {
                return;
            }

            AddressSearchResult result = JsonUtility.FromJson<AddressSearchResult>(payloadJson);
            if (result == null || result.address.Length == 0)
            {
                return;
            }

            _view.SetSelectedAddress(result.address);
        }
    }
}
