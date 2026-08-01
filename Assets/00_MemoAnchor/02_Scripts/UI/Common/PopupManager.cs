using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class PopupManager : MonoBehaviour
    {
        private const string BOTTOM_SHEET_OPEN_CLASS = "is-open";
        private const string BOTTOM_SHEET_ANIMATION_READY_CLASS = "is-anim-ready";
        private const string BOTTOM_SHEET_HIDDEN_CLASS = "is-hidden";
        private const int BOTTOM_SHEET_OPEN_DELAY_MS = 16;
        private const int BOTTOM_SHEET_CLOSE_DURATION_MS = 240;

        private static PopupManager Instance { get; set; }
        private static readonly Dictionary<VisualElement, BottomSheetState> BottomSheets = new();

        private VisualElement _root;
        private VisualElement _confirmRoot;
        private VisualElement _confirmOverlay;
        private VisualElement _confirmActions;
        private VisualElement _confirmInputBox;
        private TextField _confirmInput;
        private Button _confirmCancelButton, _confirmSubmitButton;
        private Label _confirmTitleLabel, _confirmMessageLabel, _confirmCancelLabel, _confirmSubmitLabel, _confirmStatusLabel;
        private Action _onConfirmSubmit;
        private Action _onConfirmCancel;
        private Action _onInputEmptySubmit;
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
            InitializeConfirmPopup();
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            ClearBottomSheets();
            Instance = null;
        }

        public static void RegisterBottomSheet(
            VisualElement overlay,
            VisualElement sheet,
            Action onDismissRequested = null,
            Action onHidden = null)
        {
            if (BottomSheets.ContainsKey(overlay))
            {
                return;
            }

            var state = new BottomSheetState(overlay, sheet, onDismissRequested, onHidden);
            BottomSheets.Add(overlay, state);
            overlay.RegisterCallback<ClickEvent>(state.OnOverlayClicked);
            sheet.RegisterCallback<ClickEvent>(state.OnSheetClicked);
            overlay.pickingMode = PickingMode.Ignore;
            overlay.RemoveFromClassList(BOTTOM_SHEET_OPEN_CLASS);
            overlay.AddToClassList(BOTTOM_SHEET_ANIMATION_READY_CLASS);
            overlay.AddToClassList(BOTTOM_SHEET_HIDDEN_CLASS);
        }

        public static void UnregisterBottomSheet(VisualElement overlay)
        {
            if (!BottomSheets.Remove(overlay, out BottomSheetState state))
            {
                return;
            }

            state.TransitionVersion++;
            state.Overlay.UnregisterCallback<ClickEvent>(state.OnOverlayClicked);
            state.Sheet.UnregisterCallback<ClickEvent>(state.OnSheetClicked);
            state.Overlay.pickingMode = PickingMode.Ignore;
            state.Overlay.RemoveFromClassList(BOTTOM_SHEET_OPEN_CLASS);
            state.Overlay.AddToClassList(BOTTOM_SHEET_HIDDEN_CLASS);
        }

        public static void ShowBottomSheet(VisualElement overlay)
        {
            BottomSheetState state = BottomSheets[overlay];
            int version = ++state.TransitionVersion;
            state.Overlay.pickingMode = PickingMode.Position;
            state.Overlay.RemoveFromClassList(BOTTOM_SHEET_OPEN_CLASS);
            state.Overlay.RemoveFromClassList(BOTTOM_SHEET_HIDDEN_CLASS);
            state.Overlay.BringToFront();
            state.Overlay.schedule.Execute(() =>
            {
                if (version == state.TransitionVersion)
                {
                    state.Overlay.AddToClassList(BOTTOM_SHEET_OPEN_CLASS);
                }
            }).ExecuteLater(BOTTOM_SHEET_OPEN_DELAY_MS);
        }

        public static void HideBottomSheet(VisualElement overlay)
        {
            BottomSheetState state = BottomSheets[overlay];
            state.Overlay.pickingMode = PickingMode.Ignore;
            if (state.Overlay.ClassListContains(BOTTOM_SHEET_HIDDEN_CLASS))
            {
                return;
            }

            int version = ++state.TransitionVersion;
            state.Overlay.RemoveFromClassList(BOTTOM_SHEET_OPEN_CLASS);
            state.Overlay.schedule.Execute(() =>
            {
                if (version != state.TransitionVersion)
                {
                    return;
                }

                state.Overlay.AddToClassList(BOTTOM_SHEET_HIDDEN_CLASS);
                state.OnHidden?.Invoke();
            }).ExecuteLater(BOTTOM_SHEET_CLOSE_DURATION_MS);
        }

        private static void ClearBottomSheets()
        {
            foreach (BottomSheetState state in BottomSheets.Values)
            {
                state.TransitionVersion++;
                state.Overlay.UnregisterCallback<ClickEvent>(state.OnOverlayClicked);
                state.Sheet.UnregisterCallback<ClickEvent>(state.OnSheetClicked);
                state.Overlay.pickingMode = PickingMode.Ignore;
            }

            BottomSheets.Clear();
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

        public static void ShowMessage(string title, string message, string submitText, Action onSubmit)
        {
            Instance.ShowConfirmInternal(title, message, string.Empty, submitText, false, onSubmit);
        }

        public static void ShowTextInput(string title, string message, string value, string cancelText, string submitText, Action<string> onSubmit)
        {
            Instance.ShowTextInputInternal(title, message, value, "코드 입력", cancelText, submitText, null, onSubmit);
        }

        public static void ShowTextInput(string title, string message, string value, string placeholder, string cancelText, string submitText, Action<string> onSubmit)
        {
            Instance.ShowTextInputInternal(title, message, value, placeholder, cancelText, submitText, null, onSubmit);
        }

        public static void ShowTextInput(string title, string message, string value, string placeholder, string cancelText, string submitText, Action onEmptySubmit, Action<string> onSubmit)
        {
            Instance.ShowTextInputInternal(title, message, value, placeholder, cancelText, submitText, onEmptySubmit, onSubmit);
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
            _confirmTitleLabel.text = title;
            _confirmMessageLabel.text = message;
            _confirmCancelLabel.text = cancelText;
            _confirmSubmitLabel.text = submitText;
            _onConfirmSubmit = onSubmit;
            _onConfirmCancel = null;
            _onInputEmptySubmit = null;
            _onInputSubmit = null;
            _confirmUsesInput = false;
            _confirmInputBox.style.display = DisplayStyle.None;
            _confirmStatusLabel.AddToClassList("is-hidden");
            _confirmStatusLabel.text = string.Empty;
            _confirmActions.EnableInClassList("is-single-action", !showCancelButton);
            _confirmCancelButton.style.display = showCancelButton ? DisplayStyle.Flex : DisplayStyle.None;
            SetConfirmButtonsEnabledInternal(true);

            _confirmPresentationVersion++;
            _confirmRoot.style.display = DisplayStyle.Flex;
            _confirmRoot.BringToFront();
            PopupPresentation.ScheduleOpen(_confirmOverlay);
        }

        private void ShowTextInputInternal(string title, string message, string value, string placeholder, string cancelText, string submitText, Action onEmptySubmit, Action<string> onSubmit)
        {
            _confirmTitleLabel.text = title;
            _confirmMessageLabel.text = message;
            _confirmCancelLabel.text = cancelText;
            _confirmSubmitLabel.text = submitText;
            _confirmInput.value = value;
            _confirmInput.textEdition.placeholder = placeholder;
            _onConfirmSubmit = null;
            _onConfirmCancel = null;
            _onInputEmptySubmit = onEmptySubmit;
            _onInputSubmit = onSubmit;
            _confirmUsesInput = true;
            _confirmInputBox.style.display = DisplayStyle.Flex;
            _confirmStatusLabel.AddToClassList("is-hidden");
            _confirmStatusLabel.text = string.Empty;
            _confirmActions.RemoveFromClassList("is-single-action");
            _confirmCancelButton.style.display = DisplayStyle.Flex;
            SetConfirmButtonsEnabledInternal(true);

            _confirmPresentationVersion++;
            _confirmRoot.style.display = DisplayStyle.Flex;
            _confirmRoot.BringToFront();
            PopupPresentation.ScheduleOpen(_confirmOverlay);
            _confirmInput.Focus();
        }

        private void HideConfirmInternal()
        {
            _ = HideConfirmAsync();
        }

        private async Awaitable HideConfirmAsync()
        {
            _onConfirmSubmit = null;
            _onConfirmCancel = null;
            _onInputEmptySubmit = null;
            _onInputSubmit = null;
            _confirmUsesInput = false;

            if (_confirmRoot.style.display.value == DisplayStyle.None)
            {
                return;
            }

            SetConfirmButtonsEnabledInternal(false);
            int closeVersion = ++_confirmPresentationVersion;
            await PopupPresentation.CloseAsync(
                _confirmOverlay,
                () => _confirmPresentationVersion == closeVersion,
                () => _confirmRoot.style.display = DisplayStyle.None);
        }

        private void SubmitConfirm()
        {
            if (_confirmUsesInput)
            {
                SubmitTextInput();
                return;
            }

            _ = SubmitConfirmAsync();
        }

        private async Awaitable SubmitConfirmAsync()
        {
            Action onSubmit = _onConfirmSubmit;
            await HideConfirmAsync();
            onSubmit?.Invoke();
        }

        private void CancelConfirm()
        {
            _ = CancelConfirmAsync();
        }

        private async Awaitable CancelConfirmAsync()
        {
            Action onCancel = _onConfirmCancel;
            await HideConfirmAsync();
            onCancel?.Invoke();
        }

        private void SubmitTextInput()
        {
            string value = _confirmInput.value.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                if (_onInputEmptySubmit != null)
                {
                    _ = SubmitEmptyTextInputAsync();
                    return;
                }

                _confirmStatusLabel.text = "내용을 입력해주세요.";
                _confirmStatusLabel.RemoveFromClassList("is-hidden");
                return;
            }

            _ = SubmitTextInputAsync(value);
        }

        private async Awaitable SubmitTextInputAsync(string value)
        {
            Action<string> onSubmit = _onInputSubmit;
            await HideConfirmAsync();
            onSubmit?.Invoke(value);
        }

        private async Awaitable SubmitEmptyTextInputAsync()
        {
            Action onEmptySubmit = _onInputEmptySubmit;
            await HideConfirmAsync();
            onEmptySubmit.Invoke();
        }

        private void SetConfirmButtonsEnabledInternal(bool enabled)
        {
            _confirmCancelButton.SetEnabled(enabled);
            _confirmSubmitButton.SetEnabled(enabled);
        }

        private void InitializeConfirmPopup()
        {
            _confirmRoot = _root.Q<VisualElement>("common-confirm-popup-root");
            _confirmOverlay = _confirmRoot.Q<VisualElement>("common-confirm-popup-overlay");
            VisualElement confirmSheet = _confirmRoot.Q<VisualElement>("common-confirm-popup-sheet");
            _confirmTitleLabel = _confirmRoot.Q<Label>("common-confirm-popup-title");
            _confirmMessageLabel = _confirmRoot.Q<Label>("common-confirm-popup-message");
            _confirmInputBox = _confirmRoot.Q<VisualElement>("common-confirm-input-box");
            _confirmInput = _confirmRoot.Q<TextField>("common-confirm-input");
            _confirmStatusLabel = _confirmRoot.Q<Label>("common-confirm-status-label");
            _confirmActions = _confirmRoot.Q<VisualElement>(className: "common-confirm-popup-actions");
            _confirmCancelLabel = _confirmRoot.Q<Label>("common-confirm-cancel-label");
            _confirmSubmitLabel = _confirmRoot.Q<Label>("common-confirm-submit-label");
            _confirmCancelButton = _confirmRoot.Q<Button>("common-confirm-cancel-button");
            _confirmSubmitButton = _confirmRoot.Q<Button>("common-confirm-submit-button");

            _confirmOverlay.RegisterCallback<ClickEvent>(_ => HideConfirmInternal());
            confirmSheet.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            _confirmCancelButton.clicked += CancelConfirm;
            _confirmSubmitButton.clicked += SubmitConfirm;
        }

        private sealed class BottomSheetState
        {
            public readonly VisualElement Overlay;
            public readonly VisualElement Sheet;
            public readonly Action OnDismissRequested;
            public readonly Action OnHidden;
            public readonly EventCallback<ClickEvent> OnOverlayClicked;
            public readonly EventCallback<ClickEvent> OnSheetClicked;
            public int TransitionVersion;

            public BottomSheetState(
                VisualElement overlay,
                VisualElement sheet,
                Action onDismissRequested,
                Action onHidden)
            {
                Overlay = overlay;
                Sheet = sheet;
                OnDismissRequested = onDismissRequested;
                OnHidden = onHidden;
                OnOverlayClicked = _ =>
                {
                    if (OnDismissRequested == null)
                    {
                        HideBottomSheet(Overlay);
                        return;
                    }

                    OnDismissRequested.Invoke();
                };
                OnSheetClicked = evt => evt.StopPropagation();
            }
        }
    }
}
