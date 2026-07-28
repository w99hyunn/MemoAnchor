using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    public partial class MainView
    {
        [SerializeField] private VisualTreeAsset _scanActionDialogAsset;

        private Button _scanActionCreateButton, _scanActionJoinButton;
        private VisualElement _scanActionDialogOverlay;
        private TemplateContainer _scanActionDialogTree;
        private Action _onScanActionCreate, _onScanActionJoin;

        public void ShowScanActionDialog(Action onCreate, Action onJoin)
        {
            EnsureScanActionDialog();
            _onScanActionCreate = onCreate;
            _onScanActionJoin = onJoin;

            if (_scanActionDialogTree.parent == null)
            {
                _root.Add(_scanActionDialogTree);
            }

            PopupManager.ShowBottomSheet(_scanActionDialogOverlay);
        }

        public void HideScanActionDialog()
        {
            if (_scanActionDialogOverlay == null)
            {
                return;
            }

            PopupManager.HideBottomSheet(_scanActionDialogOverlay);
        }

        public void ShowScanJoinDialog()
        {
            HideScanActionDialog();
            PopupManager.ShowTextInput(
                "맵 참여",
                "알림창을 통해 참여코드를 확인해주세요.",
                string.Empty,
                "취소",
                "참여하기",
                code => _ = OpenReadOnlyMapAsync(code));
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

            PopupManager.RegisterBottomSheet(
                _scanActionDialogOverlay,
                dialogSheet,
                onHidden: () => _scanActionDialogTree.RemoveFromHierarchy());
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

        private void UnregisterScanActionDialog()
        {
            if (_scanActionDialogOverlay == null)
            {
                return;
            }

            PopupManager.UnregisterBottomSheet(_scanActionDialogOverlay);
        }
    }
}
