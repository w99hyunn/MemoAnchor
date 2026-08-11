using System;
using UnityEngine;

namespace MemoAnchor.UI
{
    public enum MapBackfaceDisplayMode
    {
        DoubleSidedColor,
        Hidden,
        SolidColor
    }

    public static class MapBackfaceDisplaySettings
    {
        private const string PLAYER_PREFS_KEY = "MemoAnchor.MapBackfaceDisplayMode";

        public static event Action Changed;

        public static MapBackfaceDisplayMode Current => (MapBackfaceDisplayMode)Mathf.Clamp(
            PlayerPrefs.GetInt(PLAYER_PREFS_KEY, (int)MapBackfaceDisplayMode.SolidColor),
            (int)MapBackfaceDisplayMode.DoubleSidedColor,
            (int)MapBackfaceDisplayMode.SolidColor);

        public static void Set(MapBackfaceDisplayMode mode)
        {
            if (Current == mode)
            {
                return;
            }

            PlayerPrefs.SetInt(PLAYER_PREFS_KEY, (int)mode);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
