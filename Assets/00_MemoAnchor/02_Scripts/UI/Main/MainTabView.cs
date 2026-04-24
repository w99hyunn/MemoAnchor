using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class MainTabView : MonoBehaviour
    {
        private const string DialogOpenClass = "is-open";

        [SerializeField] private VisualTreeAsset _scanActionDialogAsset;

        private Button _homeButton, _menuButton, _scanButton, _mapButton, _profileButton, _scanStartButton;
        private Button _scanActionCreateButton, _scanActionJoinButton;

        private VisualElement _root, _tabViewport, _tabStrip, _bottomNavWrapper, _bottomNav;
        private VisualElement _homeTab, _menuTab, _scanTab, _mapTab, _profileTab;
        private VisualElement _scanActionDialogOverlay;
        private TemplateContainer _scanActionDialogTree;
        private Action _onScanActionCreate, _onScanActionJoin;
        private int _scanActionDialogTransitionToken;

        public Button HomeButton => _homeButton;
        public Button MenuButton => _menuButton;
        public Button ScanButton => _scanButton;
        public Button MapButton => _mapButton;
        public Button ProfileButton => _profileButton;
        public VisualElement TabViewport => _tabViewport;

        private void Awake()
        {
            _root = GetComponent<UIDocument>().rootVisualElement;
            _homeButton = _root.Q<Button>("nav-home");
            _menuButton = _root.Q<Button>("nav-menu");
            _scanButton = _root.Q<Button>("nav-scan");
            _mapButton = _root.Q<Button>("nav-map");
            _profileButton = _root.Q<Button>("nav-profile");
            _scanStartButton = _root.Q<Button>("nav-scan-start");

            _tabViewport = _root.Q<VisualElement>("tab-viewport");
            _tabStrip = _root.Q<VisualElement>("tab-strip");
            _bottomNavWrapper = _root.Q<VisualElement>("bottom-nav-wrapper");
            _bottomNav = _root.Q<VisualElement>("bottom-nav");
            _homeTab = _root.Q<VisualElement>("tab-home");
            _menuTab = _root.Q<VisualElement>("tab-menu");
            _scanTab = _root.Q<VisualElement>("tab-scan");
            _mapTab = _root.Q<VisualElement>("tab-map");
            _profileTab = _root.Q<VisualElement>("tab-profile");
        }

        public void SetScanNavMode(bool enabled)
        {
            _bottomNavWrapper.EnableInClassList("is-scan-mode", enabled);
            _bottomNav.EnableInClassList("is-scan-mode", enabled);
            _scanStartButton.pickingMode = enabled ? PickingMode.Position : PickingMode.Ignore;
        }

        public void SetTabStripOffset(float x)
        {
            _tabStrip.style.left = x;
        }

        public void SetTabPageWidth(float width)
        {
            _homeTab.style.width = width;
            _menuTab.style.width = width;
            _scanTab.style.width = width;
            _mapTab.style.width = width;
            _profileTab.style.width = width;
            _tabStrip.style.width = width * 5f;
        }

        public void ShowScanActionDialog(Action onCreate, Action onJoin)
        {
            EnsureScanActionDialog();
            _onScanActionCreate = onCreate;
            _onScanActionJoin = onJoin;

            _scanActionDialogTransitionToken++;

            if (_scanActionDialogTree.parent == null)
            {
                _scanActionDialogOverlay.RemoveFromClassList(DialogOpenClass);
                _root.Add(_scanActionDialogTree);
            }

            int token = _scanActionDialogTransitionToken;
            _scanActionDialogOverlay.schedule.Execute(() =>
            {
                if (token != _scanActionDialogTransitionToken)
                {
                    return;
                }

                _scanActionDialogOverlay.AddToClassList(DialogOpenClass);
            }).ExecuteLater(16);
        }

        public void HideScanActionDialog()
        {
            if (_scanActionDialogOverlay == null)
            {
                return;
            }

            _scanActionDialogTransitionToken++;
            int token = _scanActionDialogTransitionToken;

            _scanActionDialogOverlay.RemoveFromClassList(DialogOpenClass);
            _scanActionDialogOverlay.schedule.Execute(() =>
            {
                if (token != _scanActionDialogTransitionToken)
                {
                    return;
                }

                _scanActionDialogTree.RemoveFromHierarchy();
            }).ExecuteLater(240);
        }

        private void EnsureScanActionDialog()
        {
            if (_scanActionDialogOverlay != null)
            {
                return;
            }

            _scanActionDialogTree = _scanActionDialogAsset.Instantiate();
            _scanActionDialogTree.style.position = Position.Absolute;
            _scanActionDialogTree.style.left = 0;
            _scanActionDialogTree.style.right = 0;
            _scanActionDialogTree.style.top = 0;
            _scanActionDialogTree.style.bottom = 0;

            _scanActionDialogOverlay = _scanActionDialogTree.Q<VisualElement>("scan-action-dialog-overlay");
            VisualElement dialogSheet = _scanActionDialogTree.Q<VisualElement>("scan-action-dialog-sheet");
            _scanActionCreateButton = _scanActionDialogTree.Q<Button>("scan-action-create-button");
            _scanActionJoinButton = _scanActionDialogTree.Q<Button>("scan-action-join-button");

            _scanActionDialogOverlay.RegisterCallback<ClickEvent>(_ => HideScanActionDialog());
            dialogSheet.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            _scanActionCreateButton.clicked += () =>
            {
                HideScanActionDialog();
                _onScanActionCreate?.Invoke();
            };
            _scanActionJoinButton.clicked += () =>
            {
                HideScanActionDialog();
                _onScanActionJoin?.Invoke();
            };

        }
    }
}
