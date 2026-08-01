using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    public partial class MainView
    {
        [SerializeField] private string _splashScene = "Splash";

        private Button _profileSettingsButton, _profileAccountSettingsBackButton;
        private Button _profileLogoutButton;
        private Button _profileFriendListButton, _profileFriendAddButton;
        private Button _profilePushToggle, _profileSoundToggle;
        private VisualElement _profileMainContent, _profileAccountSettingsPage;
        private VisualElement _profileFriendListCard, _profileFriendList, _profileFriendItemsList, _profileFriendListChevron;
        private Label _profileNameLabel, _profileCompanyLabel;
        private FadeTransition _fadeTransition;
        private bool _profilePushEnabled;
        private bool _profileSoundEnabled = false;
        private bool _profileFriendListExpanded;
        private bool _isLoggingOut;

        private void ApplyProfileSummary()
        {
            MemoAnchor.PlayerProfile profile = MemoAnchor.PlayerSession.Profile;
            if (!string.IsNullOrWhiteSpace(profile.Name))
            {
                _profileNameLabel.text = profile.Name;
            }

            if (!string.IsNullOrWhiteSpace(profile.CompanyName))
            {
                _profileCompanyLabel.text = profile.CompanyName;
            }
        }

        private void ToggleProfilePush()
        {
            _profilePushEnabled = !_profilePushEnabled;
            ApplyProfileSwitches();
        }

        private void ShowProfileAccountSettings()
        {
            PopupManager.HideConfirm();
            SetVisible(_profileMainContent, false);
            SetVisible(_profileAccountSettingsPage, true);
        }

        public void HideProfileAccountSettings()
        {
            PopupManager.HideConfirm();
            SetVisible(_profileMainContent, true);
            SetVisible(_profileAccountSettingsPage, false);
        }

        private void ShowProfileLogoutConfirmPopup()
        {
            PopupManager.ShowConfirm("로그아웃", "정말 로그아웃할까요?", "취소", "로그아웃", ConfirmProfileLogout);
        }

        private void ConfirmProfileLogout()
        {
            _ = LogoutAsync();
        }

        private async Awaitable LogoutAsync()
        {
            if (_isLoggingOut)
            {
                return;
            }

            _isLoggingOut = true;
            PopupManager.SetConfirmButtonsEnabled(false);
            UnregisterFriendsCallbacks();
            AuthenticationService.Instance.SignOut(true);
            AuthenticationService.Instance.ClearSessionToken();
            MemoAnchor.PlayerSession.Clear();
            await _fadeTransition.FadeOutAndLoadSceneAsync(_splashScene);
        }

        private void ToggleProfileFriendList()
        {
            _profileFriendListExpanded = !_profileFriendListExpanded;
            ApplyProfileFriendList();
        }

        private void ApplyProfileFriendList()
        {
            SetVisible(_profileFriendList, _profileFriendListExpanded);
            _profileFriendListCard.EnableInClassList(SELECTED_CLASS, _profileFriendListExpanded);
            _profileFriendListChevron.EnableInClassList(SELECTED_CLASS, _profileFriendListExpanded);
        }

        private void ToggleProfileSound()
        {
            _profileSoundEnabled = !_profileSoundEnabled;
            ApplyProfileSwitches();
        }

        private void ApplyProfileSwitches()
        {
            _profilePushToggle.EnableInClassList(SELECTED_CLASS, _profilePushEnabled);
            _profileSoundToggle.EnableInClassList(SELECTED_CLASS, _profileSoundEnabled);
        }
    }
}
