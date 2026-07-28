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
            _view.ApplyHomeMode(_isHomeWorkMode);
            _mainTabView.ApplyMemoRoleMode(_isHomeWorkMode);
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
