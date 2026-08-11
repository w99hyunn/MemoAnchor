using System;
using System.Collections.Generic;
using Unity.Services.Authentication;
using UnityEngine;

namespace MemoAnchor.UI
{
    [RequireComponent(typeof(Tab_ScanView))]
    public class Tab_ScanController : MonoBehaviour
    {
        private Tab_ScanView _view;
        private MainView _mainView;
        private KakaoPostcodeWebView _postcodeWebView;
        private readonly ScanAddressService _scanAddressService = new();
        private readonly List<ScanFriendOption> _cachedFriendOptions = new();
        private FriendSelectionTarget _friendSelectionTarget;

        private void Awake()
        {
            TryGetComponent<Tab_ScanView>(out _view);
            TryGetComponent<MainView>(out _mainView);
            _postcodeWebView = new KakaoPostcodeWebView(OnAddressSearchResult);
        }

        private void Start()
        {
            _view.AddressButton.clicked += OnClickAddressButton;
            _view.AddressAddButton.clicked += OnClickAddressAddButton;
            _view.RepairerButton.clicked += OnClickRepairerButton;
            _view.ManagerButton.clicked += OnClickManagerButton;
            _mainView.FriendsCacheChanged += HandleFriendsCacheChanged;
            _ = LoadAddressesAsync();
        }

        private void OnDisable()
        {
            _view.AddressButton.clicked -= OnClickAddressButton;
            _view.AddressAddButton.clicked -= OnClickAddressAddButton;
            _view.RepairerButton.clicked -= OnClickRepairerButton;
            _view.ManagerButton.clicked -= OnClickManagerButton;
            _mainView.FriendsCacheChanged -= HandleFriendsCacheChanged;
            _postcodeWebView.Close();
        }

        private void OnClickAddressButton()
        {
            _ = LoadAddressesAsync();
            _view.ShowAddressDialog();
        }

        private void OnClickAddressAddButton()
        {
            OpenAddressSearch();
        }

        private void SelectAddress(ScanAddressItem address)
        {
            _view.SetSelectedAddress(address);
            _view.HideAddressDialog();
        }

        private void OnClickRepairerButton()
        {
            OpenFriendSelection(FriendSelectionTarget.Repairer);
        }

        private void OnClickManagerButton()
        {
            OpenFriendSelection(FriendSelectionTarget.Manager);
        }

        private void SelectFriends(IReadOnlyList<ScanFriendOption> friends)
        {
            if (_friendSelectionTarget == FriendSelectionTarget.Repairer)
            {
                _view.SetSelectedRepairers(friends);
            }
            else
            {
                _view.SetSelectedManagers(friends);
            }

            _view.HideFriendDialog();
        }

        private void OpenAddressSearch()
        {
            _postcodeWebView.Open();
        }

        public void OnAddressSearchResult(string payloadJson)
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                return;
            }

            ScanAddressSaveRequest result;
            try
            {
                result = JsonUtility.FromJson<ScanAddressSaveRequest>(payloadJson);
            }
            catch
            {
                Debug.LogWarning($"Address search returned invalid payload: {payloadJson}");
                return;
            }

            if (result == null || string.IsNullOrWhiteSpace(result.address))
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
            _view.ShowAddressDialog();
            if (saveResult.IsSuccess)
            {
                ScanAddressItem savedAddress = saveResult.AddressList.addresses.Find(address =>
                    string.Equals(address.address, result.address, StringComparison.OrdinalIgnoreCase));
                _view.SetSelectedAddress(savedAddress ?? new ScanAddressItem
                {
                    address = result.address,
                    roadAddress = result.roadAddress
                });
            }
        }

        public ScanMapCreateRequest CreateScanDraft()
        {
            ScanAddressItem address = _view.SelectedAddressItem;
            return new ScanMapCreateRequest
            {
                addressId = address?.id ?? string.Empty,
                address = _view.SelectedAddress,
                roadAddress = address?.roadAddress ?? _view.SelectedAddress,
                spaceName = _view.SpaceName,
                repairerPlayerIds = new List<string>(_view.SelectedRepairers.Keys),
                managerPlayerIds = new List<string>(_view.SelectedManagers.Keys)
            };
        }

        private void OpenFriendSelection(FriendSelectionTarget target)
        {
            _friendSelectionTarget = target;
            _view.ShowFriendDialog(target == FriendSelectionTarget.Repairer ? "수리자 선택" : "관리자 선택");
            RebuildFriendSelectionFromCache();
        }

        private void HandleFriendsCacheChanged()
        {
            if (_view.IsFriendDialogVisible)
            {
                RebuildFriendSelectionFromCache();
            }
        }

        private void RebuildFriendSelectionFromCache()
        {
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                _view.RebuildFriendStatus("로그인 후 친구를 선택할 수 있습니다.");
                return;
            }

            if (!_mainView.AreFriendsInitialized)
            {
                if (_mainView.DidFriendsInitializationFail)
                {
                    _view.RebuildFriendStatus("친구 정보를 불러오지 못했습니다.");
                    _mainView.EnsureFriendsInitialized();
                    return;
                }

                _view.RebuildFriendStatus("친구 정보를 불러오는 중입니다.");
                return;
            }

            _mainView.CopyCachedFriendOptions(_cachedFriendOptions);
            if (_cachedFriendOptions.Count == 0)
            {
                _view.RebuildFriendStatus("등록된 친구가 없습니다.");
                return;
            }

            IReadOnlyDictionary<string, ScanFriendOption> selectedFriends = _friendSelectionTarget == FriendSelectionTarget.Repairer
                ? _view.SelectedRepairers
                : _view.SelectedManagers;
            _view.RebuildFriendItems(_cachedFriendOptions, selectedFriends, SelectFriends);
        }

        private enum FriendSelectionTarget
        {
            Repairer,
            Manager
        }
    }
}
