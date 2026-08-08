using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    /// <summary>
    /// Keeps the phone-authored UI at a comfortable visual density on tablets.
    /// UI Toolkit's width-matched reference resolution otherwise makes 4:3 and
    /// 16:10 screens feel disproportionately large in the vertical direction.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    internal sealed class ResponsivePanelScaler : MonoBehaviour
    {
        internal const string TabletClass = "tablet-layout";
        internal const string TabletLandscapeClass = "tablet-landscape";

        private const float TabletAspectThreshold = 1.7f;
        // Tablets benefit from a denser canvas than simply matching a tall phone's
        // content height. 2.4 keeps touch targets comfortable while reducing the
        // oversized type and controls that are especially noticeable on iPad.
        private const float TabletContentAspect = 2.4f;
        private const float MaximumTabletScale = 1.8f;

        private static ResponsivePanelScaler _instance;

        private readonly Dictionary<PanelSettings, RuntimePanelSettings> _runtimeSettingsBySource = new();
        private readonly Dictionary<PanelSettings, RuntimePanelSettings> _runtimeSettingsByClone = new();

        private int _lastScreenWidth;
        private int _lastScreenHeight;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null)
            {
                return;
            }

            GameObject scalerObject = new(nameof(ResponsivePanelScaler));
            DontDestroyOnLoad(scalerObject);
            _instance = scalerObject.AddComponent<ResponsivePanelScaler>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (_instance != this)
            {
                return;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            _instance = null;
        }

        private void Update()
        {
            if (_lastScreenWidth == Screen.width && _lastScreenHeight == Screen.height)
            {
                return;
            }

            ApplyToLoadedDocuments();
        }

        private void OnSceneLoaded(Scene _, LoadSceneMode __)
        {
            ApplyToLoadedDocuments();
        }

        private void ApplyToLoadedDocuments()
        {
            int screenWidth = Screen.width;
            int screenHeight = Screen.height;
            if (screenWidth <= 0 || screenHeight <= 0)
            {
                return;
            }

            _lastScreenWidth = screenWidth;
            _lastScreenHeight = screenHeight;

            LayoutMetrics layout = CalculateLayout(screenWidth, screenHeight);
            UIDocument[] documents = FindObjectsByType<UIDocument>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (UIDocument document in documents)
            {
                ApplyToDocument(document, layout);
            }
        }

        private void ApplyToDocument(UIDocument document, LayoutMetrics layout)
        {
            if (document == null || document.panelSettings == null)
            {
                return;
            }

            RuntimePanelSettings runtimeSettings = GetOrCreateRuntimeSettings(document.panelSettings);
            if (document.panelSettings != runtimeSettings.Clone)
            {
                document.panelSettings = runtimeSettings.Clone;
            }

            Vector2Int referenceResolution = runtimeSettings.BaseReferenceResolution;
            referenceResolution.x = Mathf.RoundToInt(referenceResolution.x * layout.ReferenceWidthMultiplier);
            runtimeSettings.Clone.referenceResolution = referenceResolution;

            VisualElement root = document.rootVisualElement;
            if (root == null)
            {
                return;
            }

            root.EnableInClassList(TabletClass, layout.IsTablet);
            root.EnableInClassList(TabletLandscapeClass, layout.IsTablet && layout.IsLandscape);
        }

        private RuntimePanelSettings GetOrCreateRuntimeSettings(PanelSettings panelSettings)
        {
            if (_runtimeSettingsByClone.TryGetValue(panelSettings, out RuntimePanelSettings existingClone))
            {
                return existingClone;
            }

            if (_runtimeSettingsBySource.TryGetValue(panelSettings, out RuntimePanelSettings existingSource))
            {
                return existingSource;
            }

            PanelSettings clone = Instantiate(panelSettings);
            clone.name = $"{panelSettings.name} (Responsive Runtime)";
            clone.hideFlags = HideFlags.DontSave;

            RuntimePanelSettings created = new(panelSettings.referenceResolution, clone);
            _runtimeSettingsBySource.Add(panelSettings, created);
            _runtimeSettingsByClone.Add(clone, created);
            return created;
        }

        internal static LayoutMetrics CalculateLayout(int screenWidth, int screenHeight)
        {
            int shortSide = Mathf.Min(screenWidth, screenHeight);
            int longSide = Mathf.Max(screenWidth, screenHeight);
            float aspect = shortSide > 0 ? longSide / (float)shortSide : TabletContentAspect;
            bool isTablet = screenWidth > 0 && screenHeight > 0 && aspect <= TabletAspectThreshold;
            bool isLandscape = screenWidth > screenHeight;

            if (!isTablet)
            {
                return new LayoutMetrics(false, isLandscape, 1f);
            }

            float tabletScale = Mathf.Clamp(TabletContentAspect / aspect, 1f, MaximumTabletScale);
            float orientationScale = isLandscape ? aspect : 1f;
            return new LayoutMetrics(true, isLandscape, tabletScale * orientationScale);
        }

        internal readonly struct LayoutMetrics
        {
            internal LayoutMetrics(bool isTablet, bool isLandscape, float referenceWidthMultiplier)
            {
                IsTablet = isTablet;
                IsLandscape = isLandscape;
                ReferenceWidthMultiplier = referenceWidthMultiplier;
            }

            internal bool IsTablet { get; }
            internal bool IsLandscape { get; }
            internal float ReferenceWidthMultiplier { get; }
        }

        private sealed class RuntimePanelSettings
        {
            internal RuntimePanelSettings(Vector2Int baseReferenceResolution, PanelSettings clone)
            {
                BaseReferenceResolution = baseReferenceResolution;
                Clone = clone;
            }

            internal Vector2Int BaseReferenceResolution { get; }
            internal PanelSettings Clone { get; }
        }
    }
}
