using System.Runtime.InteropServices;
using UnityEngine;

namespace MemoAnchor.UI
{
    public static class NativeShareService
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void MemoAnchor_ShareText(string subject, string text);
#endif

        public static bool ShareText(string subject, string text)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using AndroidJavaClass unityPlayer = new("com.unity3d.player.UnityPlayer");
            using AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            using AndroidJavaClass shareBridge = new("com.memoanchor.systemui.ShareBridge");
            shareBridge.CallStatic("shareText", activity, subject, text);
            return true;
#elif UNITY_IOS && !UNITY_EDITOR
            MemoAnchor_ShareText(subject, text);
            return true;
#else
            GUIUtility.systemCopyBuffer = text;
            return false;
#endif
        }
    }
}
