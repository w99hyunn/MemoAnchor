using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    /// <summary>
    /// UIDocument 오버레이를 이용한 씬 페이드 전환.
    /// "fade-overlay"가 없으면 런타임에 자동 생성해서 사용
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [RequireComponent(typeof(UIDocument))]
    public class FadeTransition : MonoBehaviour
    {
        private UIDocument _uiDocument;
        private VisualElement _fadeOverlay;
        private bool _isTransitioning;
        private const string fadeOverlayName = "fade-overlay";
        private float fadeDuration = 0.35f;

        private void Awake()
        {
            TryGetComponent<UIDocument>(out _uiDocument);
        }

        private void Start()
        {
            CacheOverlay();
            _ = FadeInAsync();
        }

        private void CacheOverlay()
        {
            VisualElement root = _uiDocument.rootVisualElement;

            _fadeOverlay = CreateFadeOverlay(root);
            _fadeOverlay.style.display = DisplayStyle.Flex;
            _fadeOverlay.pickingMode = PickingMode.Ignore;
            _fadeOverlay.style.opacity = 1f;
            _fadeOverlay.BringToFront();
        }

        private VisualElement CreateFadeOverlay(VisualElement root)
        {
            var overlay = new VisualElement
            {
                name = fadeOverlayName,
                pickingMode = PickingMode.Ignore
            };

            overlay.AddToClassList("fade-overlay");
            overlay.style.position = Position.Absolute;
            overlay.style.left = 0f;
            overlay.style.top = 0f;
            overlay.style.right = 0f;
            overlay.style.bottom = 0f;
            overlay.style.backgroundColor = Color.black;
            overlay.style.opacity = 1f;

            root.Add(overlay);
            return overlay;
        }

        public async Awaitable FadeInAsync()
        {
            if (_fadeOverlay == null)
            {
                CacheOverlay();
            }

            float elapsedTime = 0f;
            _fadeOverlay.style.opacity = 1f;

            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / fadeDuration);
                _fadeOverlay.style.opacity = 1f - t;
                await Awaitable.NextFrameAsync();
            }

            _fadeOverlay.style.opacity = 0f;
        }

        public async Awaitable FadeOutAsync()
        {
            if (_fadeOverlay == null)
            {
                CacheOverlay();
            }

            float elapsedTime = 0f;
            _fadeOverlay.style.display = DisplayStyle.Flex;
            _fadeOverlay.style.opacity = 0f;

            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / fadeDuration);
                _fadeOverlay.style.opacity = t;
                await Awaitable.NextFrameAsync();
            }

            _fadeOverlay.style.opacity = 1f;
        }

        public async Awaitable FadeOutAndLoadSceneAsync(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
        {
            if (_isTransitioning)
            {
                return;
            }

            _isTransitioning = true;
            await FadeOutAsync();
            SceneManager.LoadScene(sceneName, mode);
        }
    }
}
