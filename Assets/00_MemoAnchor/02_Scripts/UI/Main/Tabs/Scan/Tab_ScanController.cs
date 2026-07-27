using System;
using System.Collections.Generic;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Friends;
using Unity.Services.Friends.Exceptions;
using Unity.Services.Friends.Models;
using UnityEngine;

namespace MemoAnchor.UI
{
    [RequireComponent(typeof(Tab_ScanView))]
    public class Tab_ScanController : MonoBehaviour
    {
        private Tab_ScanView _view;
        private KakaoPostcodeWebView _postcodeWebView;
        private readonly ScanAddressService _scanAddressService = new();
        private readonly ScanMapService _scanMapService = new();
        private FriendSelectionTarget _friendSelectionTarget;
        private int _friendSelectionRequestToken;
        private bool _isCreatingTemporaryMap;

        private void Awake()
        {
            TryGetComponent<Tab_ScanView>(out _view);
            _postcodeWebView = new KakaoPostcodeWebView(OnAddressSearchResult);
        }

        private void Start()
        {
            _view.AddressButton.clicked += OnClickAddressButton;
            _view.AddressAddButton.clicked += OnClickAddressAddButton;
            _view.RepairerButton.clicked += OnClickRepairerButton;
            _view.ManagerButton.clicked += OnClickManagerButton;
            _ = LoadAddressesAsync();
        }

        private void OnDisable()
        {
            _view.AddressButton.clicked -= OnClickAddressButton;
            _view.AddressAddButton.clicked -= OnClickAddressAddButton;
            _view.RepairerButton.clicked -= OnClickRepairerButton;
            _view.ManagerButton.clicked -= OnClickManagerButton;
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
            _ = OpenFriendSelectionAsync(FriendSelectionTarget.Repairer);
        }

        private void OnClickManagerButton()
        {
            _ = OpenFriendSelectionAsync(FriendSelectionTarget.Manager);
        }

        private void SelectFriend(ScanFriendOption friend)
        {
            if (_friendSelectionTarget == FriendSelectionTarget.Repairer)
            {
                _view.SetSelectedRepairer(friend);
            }
            else
            {
                _view.SetSelectedManager(friend);
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

        public async Awaitable<string> CreateTemporaryMapAsync()
        {
            if (_isCreatingTemporaryMap)
            {
                return string.Empty;
            }

            _isCreatingTemporaryMap = true;

            try
            {
                ScanAddressItem address = _view.SelectedAddressItem;
                var payload = new ScanMapCreateRequest
                {
                    addressId = address?.id ?? string.Empty,
                    address = _view.SelectedAddress,
                    roadAddress = address?.roadAddress ?? _view.SelectedAddress,
                    spaceName = _view.SpaceName,
                    repairerPlayerId = _view.SelectedRepairer.Id,
                    managerPlayerId = _view.SelectedManager.Id
                };

                ScanMapCreateResult result = await _scanMapService.CreateMapAsync(payload);
                if (!result.IsSuccess)
                {
                    PopupManager.ShowMessage("맵 생성 실패", "서버에 맵을 저장하지 못했습니다.", "확인");
                }

                return result.IsSuccess ? result.CreatedMapId : string.Empty;
            }
            finally
            {
                _isCreatingTemporaryMap = false;
            }
        }

        private async Awaitable OpenFriendSelectionAsync(FriendSelectionTarget target)
        {
            _friendSelectionTarget = target;
            _friendSelectionRequestToken++;
            int token = _friendSelectionRequestToken;

            _view.RebuildFriendStatus("친구 정보를 불러오는 중입니다.");
            _view.ShowFriendDialog();

            try
            {
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    _view.RebuildFriendStatus("로그인 후 친구를 선택할 수 있습니다.");
                    return;
                }

                await FriendsService.Instance.InitializeAsync();
                await FriendsService.Instance.ForceRelationshipsRefreshAsync();

                if (token != _friendSelectionRequestToken)
                {
                    return;
                }

                if (FriendsService.Instance.Friends.Count == 0)
                {
                    _view.RebuildFriendStatus("등록된 친구가 없습니다.");
                    return;
                }

                List<ScanFriendOption> friends = new();
                foreach (Relationship relationship in FriendsService.Instance.Friends)
                {
                    friends.Add(new ScanFriendOption(
                        relationship.Member.Id,
                        GetMemberDisplayName(relationship.Member),
                        string.Empty));
                }

                _view.RebuildFriendItems(friends, SelectFriend);
            }
            catch (Exception exception) when (IsFriendsRecoverableException(exception))
            {
                Debug.LogWarning($"Scan friend selection load failed: {exception.Message}");
                if (token != _friendSelectionRequestToken)
                {
                    return;
                }

                _view.RebuildFriendStatus("친구 정보를 불러오지 못했습니다.");
            }
        }

        private static string GetMemberDisplayName(Member member)
        {
            return string.IsNullOrWhiteSpace(member.Profile.Name) ? member.Id : member.Profile.Name;
        }

        private static bool IsFriendsRecoverableException(Exception exception)
        {
            return exception is FriendsServiceException or InvalidOperationException or AuthenticationException or RequestFailedException;
        }

        private enum FriendSelectionTarget
        {
            Repairer,
            Manager
        }
    }
}
