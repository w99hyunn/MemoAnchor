using UnityEngine;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    [UxmlElement]
    public partial class NativeTextField : TextField
    {
        private TouchScreenKeyboard _nativeKeyboard;

        public NativeTextField()
        {
            hideMobileInput = true;
            RegisterCallback<FocusInEvent>(OnFocusIn, TrickleDown.TrickleDown);
            RegisterCallback<FocusOutEvent>(OnFocusOut, TrickleDown.TrickleDown);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        public void DismissNativeKeyboard()
        {
            if (!TouchScreenKeyboard.isSupported)
            {
                return;
            }

            TouchScreenKeyboard keyboard = touchScreenKeyboard ?? _nativeKeyboard;
            if (keyboard != null && keyboard.active)
            {
                keyboard.active = false;
            }

            _nativeKeyboard = null;
        }

        private void OnFocusIn(FocusInEvent evt)
        {
            // UI Toolkit focuses an internal text-input element after the outer
            // TextField receives focus. Capture on the next tick, once that
            // internal focus transition has settled.
            schedule.Execute(CaptureNativeKeyboardIfStillFocused);
        }

        private void OnFocusOut(FocusOutEvent evt)
        {
            // FocusOut bubbles through a TextField's internal input hierarchy.
            // It also fires while handing focus from one TextField to another.
            // Closing the native keyboard in either case makes iOS show it and
            // immediately dismiss it, so only close after focus truly leaves
            // every text field.
            if (evt.relatedTarget is VisualElement nextFocused && IsTextFieldTarget(nextFocused))
            {
                _nativeKeyboard = null;
                return;
            }

            schedule.Execute(DismissKeyboardIfFocusLeftTextFields);
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            DismissNativeKeyboard();
        }

        private void CaptureNativeKeyboardIfStillFocused()
        {
            if (TouchScreenKeyboard.isSupported && IsFocusedWithinThisField())
            {
                _nativeKeyboard = touchScreenKeyboard;
            }
        }

        private void DismissKeyboardIfFocusLeftTextFields()
        {
            if (panel?.focusController?.focusedElement is VisualElement focusedElement
                && IsTextFieldTarget(focusedElement))
            {
                _nativeKeyboard = null;
                return;
            }

            DismissNativeKeyboard();
        }

        private bool IsFocusedWithinThisField()
        {
            if (panel?.focusController?.focusedElement is not VisualElement focusedElement)
            {
                return false;
            }

            return focusedElement == this || Contains(focusedElement);
        }

        private static bool IsTextFieldTarget(VisualElement element)
        {
            for (VisualElement current = element; current != null; current = current.parent)
            {
                if (current is TextField)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
