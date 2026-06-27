using System;
using System.Collections.Generic;
using MemoAnchor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class Tab_ScanView : MonoBehaviour
    {
        private const string DIALOG_OPEN_CLASS = "is-open";
        private const string DIALOG_ANIM_READY_CLASS = "is-anim-ready";
        private const string HIDDEN_CLASS = "is-hidden";

        private Button _addressButton;
        private TextField _addressButtonText;
        private VisualElement _addressDialogOverlay;
        private VisualElement _addressItemsList;
        private Button _addressAddButton;
        private int _addressDialogTransitionToken;

        public Button AddressButton => _addressButton;
        public Button AddressAddButton => _addressAddButton;

        public string SelectedAddress { get; private set; }

        private void Awake()
        {
            TryGetComponent<UIDocument>(out var uiDocument);
            VisualElement root = uiDocument.rootVisualElement;

            _addressButton = root.Q<Button>("scan-address-field");
            _addressButtonText = root.Q<TextField>("scan-address-field-text");
            _addressButtonText.focusable = false;

            TextElement addressButtonTextElement = _addressButtonText.Q<TextElement>();
            addressButtonTextElement.pickingMode = PickingMode.Ignore;

            VisualElement mainRoot = root.Q<VisualElement>("main-root");
            _addressDialogOverlay = root.Q<VisualElement>("scan-address-dialog-overlay");
            VisualElement addressDialogSheet = root.Q<VisualElement>("scan-address-dialog-sheet");
            _addressItemsList = root.Q<VisualElement>("scan-address-items-list");
            _addressAddButton = root.Q<Button>("scan-address-add-button");

            mainRoot.Add(_addressDialogOverlay);
            _addressDialogOverlay.BringToFront();
            _addressDialogOverlay.AddToClassList(DIALOG_ANIM_READY_CLASS);
            _addressDialogOverlay.RegisterCallback<ClickEvent>(_ => HideAddressDialog());
            addressDialogSheet.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());

            SetSelectedAddress(string.Empty);
        }

        public void RebuildAddressItems(IReadOnlyList<ScanAddressItem> addresses, Action<ScanAddressItem> onSelectAddress)
        {
            _addressItemsList.Clear();

            for (int i = 0; i < addresses.Count; i++)
            {
                ScanAddressItem address = addresses[i];
                Button button = new()
                {
                    name = $"scan-address-item-{i}"
                };
                button.AddToClassList("scan-address-list-item");
                button.AddToClassList("scan-address-building-item");

                Label text = new(address.address);
                text.AddToClassList("scan-address-list-text");
                button.Add(text);

                Label chevron = new("›");
                chevron.AddToClassList("scan-address-list-chevron");
                button.Add(chevron);

                button.clicked += () => onSelectAddress(address);
                _addressItemsList.Add(button);
            }
        }

        public void ShowAddressDialog()
        {
            _addressDialogTransitionToken++;
            _addressDialogOverlay.RemoveFromClassList(HIDDEN_CLASS);
            _addressDialogOverlay.RemoveFromClassList(DIALOG_OPEN_CLASS);

            int token = _addressDialogTransitionToken;
            _addressDialogOverlay.schedule.Execute(() =>
            {
                if (token != _addressDialogTransitionToken)
                {
                    return;
                }

                _addressDialogOverlay.AddToClassList(DIALOG_OPEN_CLASS);
            }).ExecuteLater(16);
        }

        public void HideAddressDialog()
        {
            _addressDialogTransitionToken++;
            int token = _addressDialogTransitionToken;

            _addressDialogOverlay.RemoveFromClassList(DIALOG_OPEN_CLASS);
            _addressDialogOverlay.schedule.Execute(() =>
            {
                if (token != _addressDialogTransitionToken)
                {
                    return;
                }

                _addressDialogOverlay.AddToClassList(HIDDEN_CLASS);
            }).ExecuteLater(240);
        }

        public void SetSelectedAddress(string address)
        {
            SelectedAddress = address;
            _addressButtonText.SetValueWithoutNotify(address);
        }
    }
}
