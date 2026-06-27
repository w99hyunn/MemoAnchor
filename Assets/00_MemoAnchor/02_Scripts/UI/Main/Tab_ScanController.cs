using System.Runtime.InteropServices;
using UnityEngine;

namespace MemoAnchor.UI
{
    [RequireComponent(typeof(Tab_ScanView))]
    public class Tab_ScanController : MonoBehaviour
    {
        private const string ANDROID_BRIDGE_CLASS = "com.memoanchor.systemui.AddressSearchBridge";

        private Tab_ScanView _view;
        private readonly ScanAddressService _scanAddressService = new();

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void MemoAnchor_OpenKakaoPostcodeSearch(string unityGameObjectName);
#endif

        private void Awake()
        {
            TryGetComponent<Tab_ScanView>(out _view);
        }

        private void Start()
        {
            _view.AddressButton.clicked += OnClickAddressButton;
            _view.AddressAddButton.clicked += OnClickAddressAddButton;
            _ = LoadAddressesAsync();
        }

        private void OnDisable()
        {
            _view.AddressButton.clicked -= OnClickAddressButton;
            _view.AddressAddButton.clicked -= OnClickAddressAddButton;
        }

        private void OnClickAddressButton()
        {
            _ = LoadAddressesAsync();
            _view.ShowAddressDialog();
        }

        private void OnClickAddressAddButton()
        {
            _view.HideAddressDialog();
            OpenAddressSearch();
        }

        private void SelectAddress(ScanAddressItem address)
        {
            _view.SetSelectedAddress(address.address);
            _view.HideAddressDialog();
        }

        private void OpenAddressSearch()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using var bridge = new AndroidJavaClass(ANDROID_BRIDGE_CLASS);
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

            ScanAddressSaveRequest result = JsonUtility.FromJson<ScanAddressSaveRequest>(payloadJson);
            if (result == null || result.address.Length == 0)
            {
                return;
            }

            _ = SaveAddressAsync(result);
        }

        private async Awaitable LoadAddressesAsync()
        {
            ScanAddressListResponse addressList = await _scanAddressService.LoadAddressesAsync();
            _view.RebuildAddressItems(addressList.addresses, SelectAddress);
        }

        private async Awaitable SaveAddressAsync(ScanAddressSaveRequest result)
        {
            ScanAddressSaveResult saveResult = await _scanAddressService.SaveAddressAsync(result);
            _view.RebuildAddressItems(saveResult.AddressList.addresses, SelectAddress);
            if (saveResult.IsSuccess)
            {
                _view.SetSelectedAddress(result.address);
            }
        }
    }
}
