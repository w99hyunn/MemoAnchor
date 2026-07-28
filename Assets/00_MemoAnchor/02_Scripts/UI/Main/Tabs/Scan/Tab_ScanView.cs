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
        private Button _addressButton;
        private TextField _addressButtonText;
        private VisualElement _addressInputBox;
        private TextField _spaceNameField;
        private VisualElement _spaceNameInputBox;
        private Button _repairerButton;
        private TextField _repairerButtonText;
        private Button _managerButton;
        private TextField _managerButtonText;
        private VisualElement _addressDialogOverlay;
        private VisualElement _addressItemsList;
        private Button _addressAddButton;
        private VisualElement _friendDialogOverlay;
        private VisualElement _friendItemsList;

        public Button AddressButton => _addressButton;
        public Button AddressAddButton => _addressAddButton;
        public Button RepairerButton => _repairerButton;
        public Button ManagerButton => _managerButton;

        public string SelectedAddress { get; private set; }
        public ScanAddressItem SelectedAddressItem { get; private set; }
        public string SpaceName => _spaceNameField.value;
        public ScanFriendOption SelectedRepairer { get; private set; }
        public ScanFriendOption SelectedManager { get; private set; }
        public event Action ScanStartReadinessChanged;

        private void Awake()
        {
            TryGetComponent<UIDocument>(out var uiDocument);
            VisualElement root = uiDocument.rootVisualElement;

            _addressButton = root.Q<Button>("scan-address-field");
            _addressButtonText = root.Q<TextField>("scan-address-field-text");
            _addressInputBox = root.Q<VisualElement>("scan-address-input-box");
            _spaceNameField = root.Q<TextField>("scan-space-name-field");
            _spaceNameInputBox = root.Q<VisualElement>("scan-space-name-input-box");
            _repairerButton = root.Q<Button>("scan-repairer-field");
            _repairerButtonText = root.Q<TextField>("scan-repairer-field-text");
            _managerButton = root.Q<Button>("scan-manager-field");
            _managerButtonText = root.Q<TextField>("scan-manager-field-text");

            DisableSelectTextFieldFocus(_addressButtonText);
            DisableSelectTextFieldFocus(_repairerButtonText);
            DisableSelectTextFieldFocus(_managerButtonText);
            _spaceNameField.RegisterValueChangedCallback(_ =>
            {
                ClearSpaceNameError();
                ScanStartReadinessChanged?.Invoke();
            });

            VisualElement mainRoot = root.Q<VisualElement>("main-root");
            _addressDialogOverlay = root.Q<VisualElement>("scan-address-dialog-overlay");
            VisualElement addressDialogSheet = root.Q<VisualElement>("scan-address-dialog-sheet");
            _addressItemsList = root.Q<VisualElement>("scan-address-items-list");
            _addressAddButton = root.Q<Button>("scan-address-add-button");
            _friendDialogOverlay = root.Q<VisualElement>("scan-friend-dialog-overlay");
            VisualElement friendDialogSheet = root.Q<VisualElement>("scan-friend-dialog-sheet");
            _friendItemsList = root.Q<VisualElement>("scan-friend-items-list");

            mainRoot.Add(_addressDialogOverlay);
            PopupManager.RegisterBottomSheet(_addressDialogOverlay, addressDialogSheet, HideAddressDialog);

            mainRoot.Add(_friendDialogOverlay);
            PopupManager.RegisterBottomSheet(_friendDialogOverlay, friendDialogSheet, HideFriendDialog);

            SetSelectedAddress(string.Empty);
            SetSelectedRepairer(ScanFriendOption.Empty);
            SetSelectedManager(ScanFriendOption.Empty);
        }

        private void OnDisable()
        {
            PopupManager.UnregisterBottomSheet(_addressDialogOverlay);
            PopupManager.UnregisterBottomSheet(_friendDialogOverlay);
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

                VisualElement chevron = new();
                chevron.AddToClassList("scan-address-list-chevron");
                button.Add(chevron);

                button.clicked += () => onSelectAddress(address);
                _addressItemsList.Add(button);
            }
        }

        public void RebuildFriendItems(IReadOnlyList<ScanFriendOption> friends, Action<ScanFriendOption> onSelectFriend)
        {
            _friendItemsList.Clear();

            for (int i = 0; i < friends.Count; i++)
            {
                ScanFriendOption friend = friends[i];
                Button button = new()
                {
                    name = $"scan-friend-item-{i}"
                };
                button.AddToClassList("scan-address-list-item");
                button.AddToClassList("scan-friend-list-item");

                Label nameLabel = new(friend.DisplayName);
                nameLabel.AddToClassList("scan-friend-name");
                button.Add(nameLabel);

                if (!string.IsNullOrWhiteSpace(friend.CompanyName))
                {
                    Label companyLabel = new(friend.CompanyName);
                    companyLabel.AddToClassList("scan-friend-company");
                    button.Add(companyLabel);
                }

                button.clicked += () => onSelectFriend(friend);
                _friendItemsList.Add(button);
            }
        }

        public void RebuildFriendStatus(string message)
        {
            _friendItemsList.Clear();

            VisualElement row = new();
            row.AddToClassList("scan-friend-status-row");

            Label label = new(message);
            label.AddToClassList("scan-friend-company");
            row.Add(label);

            _friendItemsList.Add(row);
        }

        public void ShowAddressDialog()
        {
            PopupManager.ShowBottomSheet(_addressDialogOverlay);
        }

        public void HideAddressDialog()
        {
            PopupManager.HideBottomSheet(_addressDialogOverlay);
        }

        public void ShowFriendDialog()
        {
            PopupManager.ShowBottomSheet(_friendDialogOverlay);
        }

        public void HideFriendDialog()
        {
            PopupManager.HideBottomSheet(_friendDialogOverlay);
        }

        public void SetSelectedAddress(string address)
        {
            SelectedAddressItem = null;
            SelectedAddress = address;
            _addressButtonText.SetValueWithoutNotify(address);
            ClearAddressError();
            ScanStartReadinessChanged?.Invoke();
        }

        public void SetSelectedAddress(ScanAddressItem address)
        {
            SelectedAddressItem = address;
            SelectedAddress = address.address;
            _addressButtonText.SetValueWithoutNotify(address.address);
            ClearAddressError();
            ScanStartReadinessChanged?.Invoke();
        }

        public void SetSelectedRepairer(ScanFriendOption friend)
        {
            SelectedRepairer = friend;
            _repairerButtonText.SetValueWithoutNotify(friend.DisplayName);
        }

        public void SetSelectedManager(ScanFriendOption friend)
        {
            SelectedManager = friend;
            _managerButtonText.SetValueWithoutNotify(friend.DisplayName);
        }

        public void ResetScanForm()
        {
            SetSelectedAddress(string.Empty);
            _spaceNameField.SetValueWithoutNotify(string.Empty);
            SetSelectedRepairer(ScanFriendOption.Empty);
            SetSelectedManager(ScanFriendOption.Empty);
            ClearSpaceNameError();
            ScanStartReadinessChanged?.Invoke();
        }

        public bool IsScanStartReady()
        {
            return !string.IsNullOrWhiteSpace(SelectedAddress) && !string.IsNullOrWhiteSpace(SpaceName);
        }

        public bool HasSpaceName()
        {
            return !string.IsNullOrWhiteSpace(SpaceName);
        }

        public bool HasSelectedAddress()
        {
            return !string.IsNullOrWhiteSpace(SelectedAddress);
        }

        public void HighlightSpaceNameError()
        {
            InputValidationFeedback.ShowError(_spaceNameInputBox);
            _ = InputValidationFeedback.ShakeAsync(_spaceNameInputBox);
        }

        public void HighlightAddressError()
        {
            InputValidationFeedback.ShowError(_addressInputBox);
            _ = InputValidationFeedback.ShakeAsync(_addressInputBox);
        }

        private void ClearAddressError()
        {
            InputValidationFeedback.ClearError(_addressInputBox);
        }

        private void ClearSpaceNameError()
        {
            InputValidationFeedback.ClearError(_spaceNameInputBox);
        }

        private static void DisableSelectTextFieldFocus(TextField textField)
        {
            textField.focusable = false;

            TextElement textElement = textField.Q<TextElement>();
            textElement.pickingMode = PickingMode.Ignore;
        }
    }

    public readonly struct ScanFriendOption
    {
        public static readonly ScanFriendOption Empty = new(string.Empty, string.Empty, string.Empty);

        public readonly string Id;
        public readonly string DisplayName;
        public readonly string CompanyName;

        public ScanFriendOption(string id, string displayName, string companyName)
        {
            Id = id;
            DisplayName = displayName;
            CompanyName = companyName;
        }
    }
}
