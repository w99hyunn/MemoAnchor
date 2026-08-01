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
        [SerializeField] private VisualTreeAsset _friendItemAsset;

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
        private Button _friendDialogBackButton;
        private Button _friendDialogSubmitButton;
        private Label _friendDialogTitle;
        private readonly Dictionary<string, ScanFriendOption> _selectedRepairers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ScanFriendOption> _selectedManagers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ScanFriendOption> _pendingFriendSelections = new(StringComparer.OrdinalIgnoreCase);
        private Action<IReadOnlyList<ScanFriendOption>> _submitFriendSelection;

        public Button AddressButton => _addressButton;
        public Button AddressAddButton => _addressAddButton;
        public Button RepairerButton => _repairerButton;
        public Button ManagerButton => _managerButton;

        public string SelectedAddress { get; private set; }
        public ScanAddressItem SelectedAddressItem { get; private set; }
        public string SpaceName => _spaceNameField.value;
        public IReadOnlyDictionary<string, ScanFriendOption> SelectedRepairers => _selectedRepairers;
        public IReadOnlyDictionary<string, ScanFriendOption> SelectedManagers => _selectedManagers;
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
            _friendDialogBackButton = root.Q<Button>("scan-friend-dialog-back-button");
            _friendDialogSubmitButton = root.Q<Button>("scan-friend-dialog-submit-button");
            _friendDialogTitle = root.Q<Label>("scan-friend-dialog-title");
            _friendDialogBackButton.clicked += HideFriendDialog;
            _friendDialogSubmitButton.clicked += SubmitFriendSelection;

            mainRoot.Add(_addressDialogOverlay);
            PopupManager.RegisterBottomSheet(_addressDialogOverlay, addressDialogSheet, HideAddressDialog);

            mainRoot.Add(_friendDialogOverlay);
            PopupManager.RegisterBottomSheet(_friendDialogOverlay, friendDialogSheet, HideFriendDialog);

            SetSelectedAddress(string.Empty);
            SetSelectedRepairers(Array.Empty<ScanFriendOption>());
            SetSelectedManagers(Array.Empty<ScanFriendOption>());
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

        public void RebuildFriendItems(
            IReadOnlyList<ScanFriendOption> friends,
            IReadOnlyDictionary<string, ScanFriendOption> selectedFriends,
            Action<IReadOnlyList<ScanFriendOption>> onSelectFriends)
        {
            _friendItemsList.Clear();
            _pendingFriendSelections.Clear();
            foreach (KeyValuePair<string, ScanFriendOption> selectedFriend in selectedFriends)
            {
                _pendingFriendSelections[selectedFriend.Key] = selectedFriend.Value;
            }
            _submitFriendSelection = onSelectFriends;

            for (int i = 0; i < friends.Count; i++)
            {
                ScanFriendOption friend = friends[i];
                TemplateContainer template = _friendItemAsset.Instantiate();
                Button button = template.Q<Button>("map-friend-invite-item");
                button.name = $"scan-friend-item-{i}";
                template.Q<Label>("map-friend-invite-name").text = friend.DisplayName;
                template.Q<Label>("map-friend-invite-company").text = friend.CompanyName;
                bool isSelected = _pendingFriendSelections.ContainsKey(friend.Id);
                if (isSelected)
                {
                    _pendingFriendSelections[friend.Id] = friend;
                }
                button.EnableInClassList("is-selected", isSelected);
                button.clicked += () => SelectFriendItem(friend, button);
                _friendItemsList.Add(template);
            }

            _friendDialogSubmitButton.SetEnabled(true);
        }

        public void RebuildFriendStatus(string message)
        {
            _friendItemsList.Clear();
            _pendingFriendSelections.Clear();
            _submitFriendSelection = null;
            _friendDialogSubmitButton.SetEnabled(false);

            TemplateContainer template = _friendItemAsset.Instantiate();
            Button item = template.Q<Button>("map-friend-invite-item");
            template.Q<Label>("map-friend-invite-name").text = message;
            template.Q<Label>("map-friend-invite-company").AddToClassList("is-hidden");
            template.Q<VisualElement>("map-friend-invite-check").AddToClassList("is-hidden");
            item.SetEnabled(false);
            _friendItemsList.Add(template);
        }

        public void ShowAddressDialog()
        {
            PopupManager.ShowBottomSheet(_addressDialogOverlay);
        }

        public void HideAddressDialog()
        {
            PopupManager.HideBottomSheet(_addressDialogOverlay);
        }

        public void ShowFriendDialog(string title)
        {
            _friendDialogTitle.text = title;
            PopupManager.ShowBottomSheet(_friendDialogOverlay);
        }

        public void HideFriendDialog()
        {
            PopupManager.HideBottomSheet(_friendDialogOverlay);
        }

        public bool TryHandleSystemBack()
        {
            if (_friendDialogOverlay.pickingMode != PickingMode.Position)
            {
                return false;
            }

            HideFriendDialog();
            return true;
        }

        private void SelectFriendItem(ScanFriendOption friend, Button selectedButton)
        {
            if (!_pendingFriendSelections.Remove(friend.Id))
            {
                _pendingFriendSelections[friend.Id] = friend;
            }
            selectedButton.EnableInClassList("is-selected", _pendingFriendSelections.ContainsKey(friend.Id));
        }

        private void SubmitFriendSelection()
        {
            _submitFriendSelection(new List<ScanFriendOption>(_pendingFriendSelections.Values));
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

        public void SetSelectedRepairers(IReadOnlyList<ScanFriendOption> friends)
        {
            ReplaceFriendSelections(_selectedRepairers, friends);
            _repairerButtonText.SetValueWithoutNotify(BuildFriendSelectionText(_selectedRepairers));
        }

        public void SetSelectedManagers(IReadOnlyList<ScanFriendOption> friends)
        {
            ReplaceFriendSelections(_selectedManagers, friends);
            _managerButtonText.SetValueWithoutNotify(BuildFriendSelectionText(_selectedManagers));
        }

        public void ResetScanForm()
        {
            SetSelectedAddress(string.Empty);
            _spaceNameField.SetValueWithoutNotify(string.Empty);
            SetSelectedRepairers(Array.Empty<ScanFriendOption>());
            SetSelectedManagers(Array.Empty<ScanFriendOption>());
            ClearSpaceNameError();
            ScanStartReadinessChanged?.Invoke();
        }

        private static void ReplaceFriendSelections(
            Dictionary<string, ScanFriendOption> target,
            IReadOnlyList<ScanFriendOption> friends)
        {
            target.Clear();
            for (int i = 0; i < friends.Count; i++)
            {
                target[friends[i].Id] = friends[i];
            }
        }

        private static string BuildFriendSelectionText(Dictionary<string, ScanFriendOption> friends)
        {
            var displayNames = new List<string>(friends.Count);
            foreach (ScanFriendOption friend in friends.Values)
            {
                displayNames.Add(friend.DisplayName);
            }
            return string.Join(", ", displayNames);
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
