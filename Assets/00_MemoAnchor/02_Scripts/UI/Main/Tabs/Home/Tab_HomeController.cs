using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    [RequireComponent(typeof(Tab_HomeView))]
    [RequireComponent(typeof(MainView))]
    public class Tab_HomeController : MonoBehaviour
    {
        private Tab_HomeView _view;
        private MainView _mainTabView;
        private bool _isHomeWorkMode = true;
        private bool _isRegistered;

        private void Awake()
        {
            TryGetComponent<Tab_HomeView>(out _view);
            TryGetComponent<MainView>(out _mainTabView);
        }

        private void Start()
        {
            _view.AlertButton.clicked += ShowAlertDialog;
            _view.AlertBackButton.clicked += HideAlertDialog;
            _view.HomeModeToggle.clicked += ToggleHomeMode;
            _view.MemoModeToggle.clicked += ToggleHomeMode;
            _view.MemoViewAllButton.clicked += ShowMemoCollection;
            _mainTabView.FriendRequestAlertsChanged += RebuildAlertItems;
            _isRegistered = true;

            _isHomeWorkMode = PlayerPrefs.GetInt(GetRoleModePrefsKey(), 1) == 1;
            _view.ApplyGreeting(MemoAnchor.PlayerSession.Profile.Name);
            _view.HideAlertDialog();
            _view.ApplyHomeMode(_isHomeWorkMode);
            _mainTabView.ApplyMemoRoleMode(_isHomeWorkMode);
            RebuildAlertItems();
        }

        private void OnDisable()
        {
            if (!_isRegistered)
            {
                return;
            }

            _view.AlertButton.clicked -= ShowAlertDialog;
            _view.AlertBackButton.clicked -= HideAlertDialog;
            _view.HomeModeToggle.clicked -= ToggleHomeMode;
            _view.MemoModeToggle.clicked -= ToggleHomeMode;
            _view.MemoViewAllButton.clicked -= ShowMemoCollection;
            _mainTabView.FriendRequestAlertsChanged -= RebuildAlertItems;
            _isRegistered = false;
        }

        private void ToggleHomeMode()
        {
            _isHomeWorkMode = !_isHomeWorkMode;
            PlayerPrefs.SetInt(GetRoleModePrefsKey(), _isHomeWorkMode ? 1 : 0);
            PlayerPrefs.Save();
            _view.ApplyHomeMode(_isHomeWorkMode);
            _mainTabView.ApplyMemoRoleMode(_isHomeWorkMode);
        }

        private static string GetRoleModePrefsKey()
        {
            return $"MemoAnchor.RoleMode.{AuthenticationService.Instance.PlayerId}";
        }

        private void ShowMemoCollection()
        {
            _mainTabView.ApplyMemoRoleMode(_isHomeWorkMode);
            _mainTabView.ShowMemoCollectionTab();
        }

        private void ShowAlertDialog()
        {
            RebuildAlertItems();
            _view.ShowAlertDialog();
        }

        private void HideAlertDialog()
        {
            _view.HideAlertDialog();
        }

        private void RebuildAlertItems()
        {
            _view.ClearAlertItems();
            int alertCount = _mainTabView.AddFriendRequestAlertsTo(_view.AlertList);
            _view.SetAlertIndicatorVisible(alertCount > 0);
            _view.RefreshAlertEmptyState();
        }
    }
}
