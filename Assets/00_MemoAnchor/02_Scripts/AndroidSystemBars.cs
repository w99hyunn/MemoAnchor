using UnityEngine;

namespace MemoAnchor
{
    public static class AndroidSystemBars
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ShowSystemBars()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Screen.fullScreen = false;
#endif
        }
    }
}
