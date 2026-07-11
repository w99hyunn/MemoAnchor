using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class PopupManager : MonoBehaviour
    {
        private static PopupManager Instance { get; set; }

        [SerializeField] private VisualTreeAsset _confirmPopupAsset;

        private VisualElement _root;
        private VisualElement _confirmOverlay;
        private VisualElement _confirmActions;
        private VisualElement _confirmInputBox;
        private TextField _confirmInput;
        private Button _confirmCancelButton, _confirmSubmitButton;
        private Label _confirmTitleLabel, _confirmMessageLabel, _confirmCancelLabel, _confirmSubmitLabel, _confirmStatusLabel;
        private TemplateContainer _confirmTree;
        private Action _onConfirmSubmit;
        private Action _onConfirmCancel;
        private Action<string> _onInputSubmit;
        private bool _confirmUsesInput;
        private int _confirmPresentationVersion;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            TryGetComponent<UIDocument>(out var uiDocument);
            _root = uiDocument.rootVisualElement;
        }

        public static void ShowConfirm(string title, string message, string cancelText, string submitText, Action onSubmit)
        {
            Instance.ShowConfirmInternal(title, message, cancelText, submitText, true, onSubmit);
        }

        public static void ShowConfirm(string title, string message, string cancelText, string submitText, Action onCancel, Action onSubmit)
        {
            Instance.ShowConfirmInternal(title, message, cancelText, submitText, true, onSubmit);
            Instance._onConfirmCancel = onCancel;
        }

        public static void ShowMessage(string title, string message, string submitText)
        {
            Instance.ShowConfirmInternal(title, message, string.Empty, submitText, false, null);
        }

        public static void ShowTextInput(string title, string message, string value, string cancelText, string submitText, Action<string> onSubmit)
        {
            Instance.ShowTextInputInternal(title, message, value, cancelText, submitText, onSubmit);
        }

        public static void HideConfirm()
        {
            if (Instance == null)
            {
                return;
            }

            Instance.HideConfirmInternal();
        }

        public static void SetConfirmButtonsEnabled(bool enabled)
        {
            Instance.SetConfirmButtonsEnabledInternal(enabled);
        }

        private void ShowConfirmInternal(string title, string message, string cancelText, string submitText, bool showCancelButton, Action onSubmit)
        {
            EnsureConfirmPopup();
            _confirmTitleLabel.text = title;
            _confirmMessageLabel.text = message;
            _confirmCancelLabel.text = cancelText;
            _confirmSubmitLabel.text = submitText;
            _onConfirmSubmit = onSubmit;
            _onConfirmCancel = null;
            _onInputSubmit = null;
            _confirmUsesInput = false;
            _confirmInputBox.style.display = DisplayStyle.None;
            _confirmStatusLabel.AddToClassList("is-hidden");
            _confirmStatusLabel.text = string.Empty;
            _confirmActions.EnableInClassList("is-single-action", !showCancelButton);
            _confirmCancelButton.style.display = showCancelButton ? DisplayStyle.Flex : DisplayStyle.None;
            SetConfirmButtonsEnabledInternal(true);

            if (_confirmTree.parent == null)
            {
                _root.Add(_confirmTree);
            }

            _confirmPresentationVersion++;
            _confirmTree.BringToFront();
            PopupPresentation.ScheduleOpen(_confirmOverlay);
        }

        private void ShowTextInputInternal(string title, string message, string value, string cancelText, string submitText, Action<string> onSubmit)
        {
            EnsureConfirmPopup();
            _confirmTitleLabel.text = title;
            _confirmMessageLabel.text = message;
            _confirmCancelLabel.text = cancelText;
            _confirmSubmitLabel.text = submitText;
            _confirmInput.value = value;
            _onConfirmSubmit = null;
            _onConfirmCancel = null;
            _onInputSubmit = onSubmit;
            _confirmUsesInput = true;
            _confirmInputBox.style.display = DisplayStyle.Flex;
            _confirmStatusLabel.AddToClassList("is-hidden");
            _confirmStatusLabel.text = string.Empty;
            _confirmActions.RemoveFromClassList("is-single-action");
            _confirmCancelButton.style.display = DisplayStyle.Flex;
            SetConfirmButtonsEnabledInternal(true);

            if (_confirmTree.parent == null)
            {
                _root.Add(_confirmTree);
            }

            _confirmPresentationVersion++;
            _confirmTree.BringToFront();
            PopupPresentation.ScheduleOpen(_confirmOverlay);
            _confirmInput.Focus();
        }

        private void HideConfirmInternal()
        {
            _onConfirmSubmit = null;
            _onConfirmCancel = null;
            _onInputSubmit = null;
            _confirmUsesInput = false;

            if (_confirmTree?.parent == null)
            {
                return;
            }

            int closeVersion = ++_confirmPresentationVersion;
            PopupPresentation.ScheduleClose(
                _confirmOverlay,
                () => _confirmPresentationVersion == closeVersion,
                () => _confirmTree.RemoveFromHierarchy());
        }

        private void SubmitConfirm()
        {
            if (_confirmUsesInput)
            {
                SubmitTextInput();
                return;
            }

            Action onSubmit = _onConfirmSubmit;
            HideConfirmInternal();
            onSubmit?.Invoke();
        }

        private void CancelConfirm()
        {
            Action onCancel = _onConfirmCancel;
            HideConfirmInternal();
            onCancel?.Invoke();
        }

        private void SubmitTextInput()
        {
            string value = _confirmInput.value.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                _confirmStatusLabel.text = "친구코드를 입력해주세요.";
                _confirmStatusLabel.RemoveFromClassList("is-hidden");
                return;
            }

            Action<string> onSubmit = _onInputSubmit;
            HideConfirmInternal();
            onSubmit?.Invoke(value);
        }

        private void SetConfirmButtonsEnabledInternal(bool enabled)
        {
            _confirmCancelButton.SetEnabled(enabled);
            _confirmSubmitButton.SetEnabled(enabled);
        }

        private void EnsureConfirmPopup()
        {
            if (_confirmTree != null)
            {
                return;
            }

            _confirmTree = _confirmPopupAsset.Instantiate();
            _confirmTree.style.position = Position.Absolute;
            _confirmTree.style.left = 0;
            _confirmTree.style.right = 0;
            _confirmTree.style.top = 0;
            _confirmTree.style.bottom = 0;

            _confirmOverlay = _confirmTree.Q<VisualElement>("common-confirm-popup-overlay");
            VisualElement confirmSheet = _confirmTree.Q<VisualElement>("common-confirm-popup-sheet");
            _confirmTitleLabel = _confirmTree.Q<Label>("common-confirm-popup-title");
            _confirmMessageLabel = _confirmTree.Q<Label>("common-confirm-popup-message");
            _confirmInputBox = _confirmTree.Q<VisualElement>("common-confirm-input-box");
            _confirmInput = _confirmTree.Q<TextField>("common-confirm-input");
            _confirmStatusLabel = _confirmTree.Q<Label>("common-confirm-status-label");
            _confirmActions = _confirmTree.Q<VisualElement>(className: "common-confirm-popup-actions");
            _confirmCancelLabel = _confirmTree.Q<Label>("common-confirm-cancel-label");
            _confirmSubmitLabel = _confirmTree.Q<Label>("common-confirm-submit-label");
            _confirmCancelButton = _confirmTree.Q<Button>("common-confirm-cancel-button");
            _confirmSubmitButton = _confirmTree.Q<Button>("common-confirm-submit-button");

            _confirmOverlay.RegisterCallback<ClickEvent>(_ => HideConfirmInternal());
            confirmSheet.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            _confirmCancelButton.clicked += CancelConfirm;
            _confirmSubmitButton.clicked += SubmitConfirm;
        }
    }
}
