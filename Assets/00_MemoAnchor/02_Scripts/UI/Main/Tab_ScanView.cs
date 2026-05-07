using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class Tab_ScanView : MonoBehaviour
    {
        private Button _addressButton;
        private TextField _addressButtonText;

        public Button AddressButton => _addressButton;

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


            SetSelectedAddress(string.Empty);
        }

        public void SetSelectedAddress(string address)
        {
            SelectedAddress = address;
            _addressButtonText.SetValueWithoutNotify(address);
        }
    }
}
