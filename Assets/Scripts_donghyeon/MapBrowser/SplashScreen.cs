
// Splash화면 애니메이션 스크립트
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SplashScreen : MonoBehaviour
{
    // Splash 스킵 플래그 (로그아웃 시 사용 - StartCanvas만 표시)
    public static bool SkipSplashAndShowStart { get; set; } = false;

    // 앱 실행 후 Splash를 이미 표시했는지 추적 (다른 씬에서 돌아올 때 스킵용)
    private static bool hasShownSplashOnce = false;

    [Header("Wiring")]
    [Tooltip("Splash Screen CanvasGroup 넣는 자리")]
    [SerializeField] private CanvasGroup splashCanvasGroup;

    [Header("Timing")]
    [Tooltip("페이드인 시간 (0이면 바로 표시)")]
    [SerializeField] private float fadeInDuration = 0.3f;

    [Tooltip("유지 시간 (완전히 보이는 시간)")]
    [SerializeField] private float displayDuration = 2f;

    [Tooltip("페이드아웃 시간")]
    [SerializeField] private float fadeOutDuration = 0.5f;

    [Header("Start Screen")]
    [Tooltip("Splash 후 표시할 Start Canvas")]
    [SerializeField] private Canvas startCanvas;

    [Tooltip("Start Canvas의 Start Button")]
    [SerializeField] private Button startButton;

    [Header("Next Screen")]
    [Tooltip("Splash 종료 후 활성화할 Canvas (AuthCanvas)")]
    [SerializeField] private Canvas authCanvas;

    [Tooltip("AuthFlowController 스크립트 참조")]
    [SerializeField] private AuthFlowController authFlowController;

    [Tooltip("AuthCanvas가 비활성화되어 있을 때 표시할 HomeCanvas")]
    [SerializeField] private Canvas homeCanvas;

    [Header("Transition")]
    [Tooltip("Auth 화면 전환 시 흰색 페이드 사용")]
    [SerializeField] private bool useWhiteFadeToAuth = true;

    [Header("Debug")]
    [Tooltip("디버그 로그 출력")]
    [SerializeField] private bool showDebugLogs = true;

    private void Awake()
    {
        // StartCanvas와 HomeCanvas를 먼저 숨김 (SplashCanvas가 먼저 보이도록)
        if (startCanvas != null)
        {
            startCanvas.gameObject.SetActive(false);
        }

        if (homeCanvas != null)
        {
            homeCanvas.gameObject.SetActive(false);
        }

        // 초기 설정: 시작 시 투명하게
        if (splashCanvasGroup != null)
        {
            splashCanvasGroup.alpha = 0f;
            splashCanvasGroup.blocksRaycasts = true;
        }
        else
        {
            Debug.LogError("[SplashScreen] CanvasGroup이 연결되지 않았습니다!");
        }

        // Start Button 이벤트 등록
        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartButtonClicked);
        }

        // AuthFlowController 자동 찾기 (연결되지 않은 경우)
        if (authFlowController == null && authCanvas != null)
        {
            authFlowController = authCanvas.GetComponentInChildren<AuthFlowController>(true);
            if (authFlowController != null && showDebugLogs)
            {
                Debug.Log("[SplashScreen] AuthFlowController를 자동으로 찾았습니다.");
            }
        }
    }

    private void Start()
    {
        // 1순위: 로그아웃으로 인한 Splash 스킵 체크 (StartCanvas만 표시)
        if (SkipSplashAndShowStart)
        {
            SkipSplashAndShowStart = false; // 플래그 초기화

            if (showDebugLogs)
                Debug.Log("[SplashScreen] 로그아웃으로 인해 Splash 스킵, StartCanvas로 바로 이동");

            // Splash 숨기기
            if (splashCanvasGroup != null)
            {
                splashCanvasGroup.alpha = 0f;
                splashCanvasGroup.blocksRaycasts = false;
            }
            gameObject.SetActive(false);

            // StartCanvas 바로 표시
            if (startCanvas != null)
            {
                startCanvas.gameObject.SetActive(true);
            }

            return;
        }

        // 2순위: 이미 Splash를 본 적 있음 (다른 씬에서 돌아옴) → 바로 HomeCanvas 표시
        if (hasShownSplashOnce)
        {
            if (showDebugLogs)
                Debug.Log("[SplashScreen] 이미 Splash를 표시한 적 있음, 바로 HomeCanvas 표시");

            // Splash 숨기기
            if (splashCanvasGroup != null)
            {
                splashCanvasGroup.alpha = 0f;
                splashCanvasGroup.blocksRaycasts = false;
            }
            gameObject.SetActive(false);

            // StartCanvas도 숨기기
            if (startCanvas != null)
            {
                startCanvas.gameObject.SetActive(false);
            }

            // HomeCanvas 바로 표시
            if (homeCanvas != null)
            {
                homeCanvas.gameObject.SetActive(true);
            }

            return;
        }

        // 3순위: 앱 첫 실행 → Splash 표시
        if (splashCanvasGroup == null)
        {
            Debug.LogError("[SplashScreen] CanvasGroup이 null입니다. 실행을 중단합니다.");
            return;
        }

        if (showDebugLogs)
            Debug.Log($"[SplashScreen] 시작 - 페이드인:{fadeInDuration}s, 유지:{displayDuration}s, 페이드아웃:{fadeOutDuration}s");

        // Splash를 표시할 것이므로 플래그 설정
        hasShownSplashOnce = true;

        StartCoroutine(ShowSplash());
    }

    private IEnumerator ShowSplash()
    {
        // 1단계: 페이드인 (0 → 1)
        if (fadeInDuration > 0f)
        {
            if (showDebugLogs)
                Debug.Log("[SplashScreen] 페이드인 시작");

            float elapsed = 0f;
            while (elapsed < fadeInDuration)
            {
                elapsed += Time.deltaTime;
                splashCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
                yield return null;
            }
        }

        // 완전히 보이게
        splashCanvasGroup.alpha = 1f;

        if (showDebugLogs)
            Debug.Log($"[SplashScreen] 유지 시작 ({displayDuration}초)");

        // 2단계: 유지 시간
        yield return new WaitForSeconds(displayDuration);

        // 3단계: 페이드아웃 (1 → 0)
        if (showDebugLogs)
            Debug.Log("[SplashScreen] 페이드아웃 시작");

        if (fadeOutDuration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                splashCanvasGroup.alpha = 1f - Mathf.Clamp01(elapsed / fadeOutDuration);
                yield return null;
            }
        }

        // 완전히 사라진 후 오브젝트 비활성화
        splashCanvasGroup.alpha = 0f;
        splashCanvasGroup.blocksRaycasts = false;

        if (showDebugLogs)
            Debug.Log("[SplashScreen] Splash 종료");

        // StartCanvas가 설정되어 있으면 StartCanvas를 표시하고 종료
        if (startCanvas != null)
        {
            // Splash 화면 비활성화
            gameObject.SetActive(false);

            // StartCanvas 활성화
            startCanvas.gameObject.SetActive(true);

            if (showDebugLogs)
                Debug.Log("[SplashScreen] StartCanvas 활성화 완료. StartButton을 기다립니다.");

            yield break; // StartButton 클릭을 기다리므로 여기서 종료
        }

        // AuthCanvas가 활성화되어 있는지 확인
        bool authCanvasActive = authCanvas != null && authCanvas.gameObject.activeInHierarchy;

        // AuthCanvas가 활성화되어 있고 흰색 전환 효과를 사용하는 경우
        if (authCanvasActive && useWhiteFadeToAuth)
        {
            // 흰색으로 변환 (Splash 배경을 흰색으로)
            UnityEngine.UI.Image[] images = GetComponentsInChildren<UnityEngine.UI.Image>(true);
            foreach (var img in images)
            {
                img.color = Color.white;
            }

            splashCanvasGroup.alpha = 1f;

            // 짧은 흰색 화면 유지
            yield return new WaitForSeconds(0.2f);
        }

        // AuthFlowController를 통해 LoginMainPanel 표시
        if (authCanvasActive && authFlowController != null)
        {
            authFlowController.ShowLoginMainAfterSplash();

            if (showDebugLogs)
                Debug.Log("[SplashScreen] AuthFlowController.ShowLoginMainAfterSplash() 호출 완료");
        }
        else if (authCanvasActive && authCanvas != null)
        {
            // authFlowController가 연결되지 않은 경우 기본 동작 (하위 호환성)
            authCanvas.gameObject.SetActive(true);

            if (showDebugLogs)
                Debug.Log("[SplashScreen] 인증 화면 활성화 (기본 동작)");
        }
        else if (homeCanvas != null)
        {
            // AuthCanvas가 비활성화되어 있으면 바로 HomeCanvas 활성화
            homeCanvas.gameObject.SetActive(true);

            if (showDebugLogs)
                Debug.Log("[SplashScreen] HomeCanvas 활성화 (AuthCanvas 건너뛰기)");
        }

        // Splash 화면 비활성화
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Start Button 클릭 시 호출되는 메서드
    /// StartCanvas를 숨기고 HomeCanvas 또는 AuthCanvas를 표시
    /// </summary>
    private void OnStartButtonClicked()
    {
        if (showDebugLogs)
            Debug.Log("[SplashScreen] StartButton 클릭됨");

        // StartCanvas 숨기기
        if (startCanvas != null)
        {
            startCanvas.gameObject.SetActive(false);
        }

        // AuthCanvas가 활성화되어 있는지 확인
        bool authCanvasActive = authCanvas != null && authCanvas.gameObject.activeInHierarchy;

        // AuthFlowController를 통해 LoginMainPanel 표시
        if (authCanvasActive && authFlowController != null)
        {
            authFlowController.ShowLoginMainAfterSplash();

            if (showDebugLogs)
                Debug.Log("[SplashScreen] AuthFlowController.ShowLoginMainAfterSplash() 호출 완료");
        }
        else if (authCanvasActive && authCanvas != null)
        {
            // authFlowController가 연결되지 않은 경우 기본 동작 (하위 호환성)
            authCanvas.gameObject.SetActive(true);

            if (showDebugLogs)
                Debug.Log("[SplashScreen] 인증 화면 활성화 (기본 동작)");
        }
        else if (homeCanvas != null)
        {
            // AuthCanvas가 비활성화되어 있으면 바로 HomeCanvas 활성화
            homeCanvas.gameObject.SetActive(true);

            if (showDebugLogs)
                Debug.Log("[SplashScreen] HomeCanvas 활성화 (AuthCanvas 건너뛰기)");
        }
    }

    private void OnDestroy()
    {
        // Start Button 이벤트 리스너 해제
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(OnStartButtonClicked);
        }
    }
}