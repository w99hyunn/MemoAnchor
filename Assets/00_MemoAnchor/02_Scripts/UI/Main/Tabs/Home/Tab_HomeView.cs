using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class Tab_HomeView : MonoBehaviour
    {
        private const string HIDDEN_CLASS = "is-hidden";
        private const string HOME_ADMIN_MODE_CLASS = "is-admin-mode";
        private const string HOME_WORK_MODE_CLASS = "is-work-mode";

        [SerializeField] private string _homeAdminTitle = "관리자";
        [SerializeField] private string _homeWorkTitle = "작업자";

        private Button _alertButton, _alertBackButton, _homeModeToggle, _memoModeToggle;
        private VisualElement _homeModeBack, _menuTab, _memoFilterPage;
        private VisualElement _alertDialogPage, _alertRequestList, _alertMapList;
        private Label _homeGreetingLabel, _homeModeTitle;

        public Button AlertButton => _alertButton;
        public Button AlertBackButton => _alertBackButton;
        public Button HomeModeToggle => _homeModeToggle;
        public Button MemoModeToggle => _memoModeToggle;
        public VisualElement AlertRequestList => _alertRequestList;

        private void Awake()
        {
            TryGetComponent<UIDocument>(out var uiDocument);
            VisualElement root = uiDocument.rootVisualElement;

            _alertButton = root.Q<Button>("alert");
            _alertBackButton = root.Q<Button>("alert-back-button");
            _homeModeToggle = root.Q<Button>("home-mode-toggle");
            _memoModeToggle = root.Q<Button>("memo-mode-toggle");
            _homeModeBack = root.Q<VisualElement>("mode-back");
            _menuTab = root.Q<VisualElement>("tab-menu");
            _memoFilterPage = root.Q<VisualElement>("memo-filter-page");
            _alertDialogPage = root.Q<VisualElement>("alert-dialog-page");
            _alertRequestList = root.Q<VisualElement>("alert-request-list");
            _alertMapList = root.Q<VisualElement>("alert-map-list");
            _homeGreetingLabel = root.Q<Label>("home-greeting-label");
            _homeModeTitle = root.Q<Label>("home-mode-title");
        }

        public void ApplyGreeting(string name)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                _homeGreetingLabel.text = $"{name}님, 안녕하세요";
            }
        }

        public void ApplyHomeMode(bool isWorkMode)
        {
            _homeModeTitle.text = isWorkMode ? _homeWorkTitle : _homeAdminTitle;
            _homeModeToggle.EnableInClassList(HOME_WORK_MODE_CLASS, isWorkMode);
            _memoModeToggle.EnableInClassList(HOME_WORK_MODE_CLASS, isWorkMode);
            _homeModeBack.EnableInClassList(HOME_ADMIN_MODE_CLASS, !isWorkMode);
            _menuTab.EnableInClassList(HOME_ADMIN_MODE_CLASS, !isWorkMode);
            _memoFilterPage.EnableInClassList(HOME_ADMIN_MODE_CLASS, !isWorkMode);
        }

        public void ShowAlertDialog()
        {
            SetVisible(_alertDialogPage, true);
            _alertDialogPage.BringToFront();
        }

        public void HideAlertDialog()
        {
            SetVisible(_alertDialogPage, false);
        }

        public void ClearAlertItems()
        {
            _alertRequestList.Clear();
            _alertMapList.Clear();
        }

        public void AddMapAlert(VisualTreeAsset mapItemAsset, string title, string description, string time, bool showsActions)
        {
            TemplateContainer item = mapItemAsset.Instantiate();
            item.Q<Label>("alert-primary-text").text = title;
            item.Q<Label>("alert-secondary-text").text = description;
            item.Q<Label>("alert-time-text").text = time;
            item.Q<Button>("alert-close-button").style.display = showsActions ? DisplayStyle.None : DisplayStyle.Flex;
            item.Q<VisualElement>("alert-action-row").style.display = showsActions ? DisplayStyle.Flex : DisplayStyle.None;
            _alertMapList.Add(item);
        }

        private static void SetVisible(VisualElement element, bool visible)
        {
            element.EnableInClassList(HIDDEN_CLASS, !visible);
        }
    }
}