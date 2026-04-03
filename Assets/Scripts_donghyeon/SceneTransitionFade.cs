
// 씬 전환 시 페이드 효과 (월드 가리기)
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SceneTransitionFade : MonoBehaviour
{
    [Header("Fade Settings")]
    [Tooltip("페이드 패널 (흰색 Image)")]
    [SerializeField] private Image fadeImage;

    [Tooltip("페이드 색상 (기본: 흰색)")]
    [SerializeField] private Color fadeColor = Color.white;

    [Tooltip("페이드 인 시간 (씬 시작 시)")]
    [SerializeField] private float fadeInDuration = 0.5f;

    [Tooltip("페이드 아웃 시간 (씬 전환 시)")]
    [SerializeField] private float fadeOutDuration = 0.5f;

    [Header("Auto Setup")]
    [Tooltip("시작 시 자동으로 페이드인")]
    [SerializeField] private bool fadeInOnStart = true;

    private static SceneTransitionFade instance;
    private Canvas fadeCanvas;

    private void Awake()
    {
        // 싱글톤 패턴 (씬 전환 시에도 유지)
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Canvas 리빌드 후 설정 (Unity 2022.3+ 호환성)
        StartCoroutine(InitializeAfterCanvasReady());
    }

    private IEnumerator InitializeAfterCanvasReady()
    {
        // Canvas 리빌드 완료 대기
        yield return null;

        // 자동 설정
        SetupFadePanel();

        if (fadeInOnStart && fadeImage != null)
        {
            // 시작 시 완전히 불투명 → 투명으로 페이드
            fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 1f);
            fadeImage.raycastTarget = true; // 초기 페이드 중 입력 차단
            StartCoroutine(FadeIn());
        }
        else if (fadeImage != null)
        {
            // 페이드인하지 않으면 투명한 상태로 시작
            fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
            fadeImage.raycastTarget = false;
        }
    }

    // 페이드 패널 자동 설정
    private void SetupFadePanel()
    {
        // Canvas 설정
        fadeCanvas = GetComponent<Canvas>();
        if (fadeCanvas == null)
            fadeCanvas = gameObject.AddComponent<Canvas>();

        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 9999; // 최상위에 표시

        // CanvasScaler 추가
        if (GetComponent<CanvasScaler>() == null)
        {
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
        }

        // GraphicRaycaster 추가 (클릭 차단용)
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();

        // Fade Image 생성
        if (fadeImage == null)
        {
            GameObject imageObj = new GameObject("FadeImage");
            imageObj.transform.SetParent(transform, false);

            fadeImage = imageObj.AddComponent<Image>();
            fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
            fadeImage.raycastTarget = false; // 기본은 입력 허용

            // 전체 화면 크기로 설정
            RectTransform rt = fadeImage.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }

    // 페이드 인 (불투명 → 투명)
    private IEnumerator FadeIn()
    {
        fadeImage.raycastTarget = true; // 페이드 중 입력 차단

        float elapsed = 0f;

        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / fadeInDuration);
            fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, alpha);
            yield return null;
        }

        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
        fadeImage.raycastTarget = false; // 페이드 완료 후 입력 허용
    }

    // 페이드 아웃 (투명 → 불투명)
    private IEnumerator FadeOut()
    {
        fadeImage.raycastTarget = true; // 페이드 중 입력 차단

        float elapsed = 0f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / fadeOutDuration);
            fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, alpha);
            yield return null;
        }

        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 1f);
        // 페이드아웃 완료 시에도 차단 유지 (씬 전환 중)
    }

    // === 외부에서 호출 가능한 함수들 ===

    // 씬 로드 (페이드 효과 포함)
    public static void LoadScene(string sceneName)
    {
        // 현재 씬을 이전 씬으로 저장 (BackButton용)
        SceneHistoryManager.SaveCurrentScene();

        if (instance != null)
        {
            instance.StartCoroutine(instance.LoadSceneWithFade(sceneName));
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    // 씬 로드 (인덱스)
    public static void LoadScene(int sceneIndex)
    {
        // 현재 씬을 이전 씬으로 저장 (BackButton용)
        SceneHistoryManager.SaveCurrentScene();

        if (instance != null)
        {
            instance.StartCoroutine(instance.LoadSceneWithFade(sceneIndex));
        }
        else
        {
            SceneManager.LoadScene(sceneIndex);
        }
    }

    // 페이드 효과와 함께 씬 로드 (이름)
    private IEnumerator LoadSceneWithFade(string sceneName)
    {
        // 페이드 아웃
        yield return StartCoroutine(FadeOut());

        // 씬 로드
        SceneManager.LoadScene(sceneName);

        // 페이드 인
        yield return StartCoroutine(FadeIn());
    }

    // 페이드 효과와 함께 씬 로드 (인덱스)
    private IEnumerator LoadSceneWithFade(int sceneIndex)
    {
        // 페이드 아웃
        yield return StartCoroutine(FadeOut());

        // 씬 로드
        SceneManager.LoadScene(sceneIndex);

        // 페이드 인
        yield return StartCoroutine(FadeIn());
    }

    // 수동 페이드 인 호출
    public static void DoFadeIn()
    {
        if (instance != null)
        {
            instance.StartCoroutine(instance.FadeIn());
        }
    }

    // 수동 페이드 아웃 호출
    public static void DoFadeOut()
    {
        if (instance != null)
        {
            instance.StartCoroutine(instance.FadeOut());
        }
    }
}
