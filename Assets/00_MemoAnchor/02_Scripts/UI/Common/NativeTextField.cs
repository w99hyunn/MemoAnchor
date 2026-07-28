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
            schedule.Execute(CaptureNativeKeyboard);
        }

        private void OnFocusOut(FocusOutEvent evt)
        {
            DismissNativeKeyboard();
        }

        private void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            DismissNativeKeyboard();
        }

        private void CaptureNativeKeyboard()
        {
            if (TouchScreenKeyboard.isSupported)
            {
                _nativeKeyboard = touchScreenKeyboard;
            }
        }
    }
}
