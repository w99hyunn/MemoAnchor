// 뒤로가기 버튼 - 이전 씬으로 이동 + AR 정리 작업 통합
using UnityEngine;
using UnityEngine.SceneManagement;

public class BackButton : MonoBehaviour
{
    [Header("Fallback Scene")]
    [Tooltip("이전 씬 정보가 없을 때 이동할 기본 씬 이름")]
    [SerializeField] private string fallbackSceneName = "Home";

    [Header("Components")]
    [Tooltip("씬 전환 관리자 (선택사항, 페이드 효과용)")]
    [SerializeField] private SceneTransitionManager transitionManager;

    [Tooltip("AR 세션 정리 관리자 (선택사항)")]
    [SerializeField] private ARSessionCleaner sessionCleaner;

    [Header("Android Back Key")]
    [Tooltip("Android 뒤로가기 키 처리 여부")]
    [SerializeField] private bool handleAndroidBackKey = true;

    [Header("Debug")]
    [Tooltip("디버그 로그 출력")]
    [SerializeField] private bool showDebugLogs = true;

    private void Awake()
    {
        // 컴포넌트 자동 찾기
        if (transitionManager == null)
        {
            transitionManager = GetComponent<SceneTransitionManager>();
            if (showDebugLogs && transitionManager == null)
                Debug.LogWarning("[BackButton] SceneTransitionManager not found");
        }

        if (sessionCleaner == null)
        {
            sessionCleaner = GetComponent<ARSessionCleaner>();
            if (showDebugLogs && sessionCleaner == null)
                Debug.LogWarning("[BackButton] ARSessionCleaner not found - AR 정리가 실행되지 않습니다");
        }

        if (showDebugLogs)
        {
            Debug.Log($"[BackButton] Awake complete - transitionManager={transitionManager != null}, sessionCleaner={sessionCleaner != null}, fallback={fallbackSceneName}");
        }
    }

    private void Update()
    {
        if (!handleAndroidBackKey) return;

        // Android 뒤로가기 키 감지
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            GoBack();
        }
    }

    // 뒤로 가기 처리 (버튼 OnClick에 연결)
    public void GoBack()
    {
        string previousScene = SceneHistoryManager.GetPreviousScene(fallbackSceneName);

        if (showDebugLogs)
        {
            Debug.Log($"[BackButton] GoBack 호출됨");
            Debug.Log($"[BackButton] 이전 씬: {previousScene}");
            Debug.Log($"[BackButton] 현재 씬: {SceneManager.GetActiveScene().name}");
        }

        // AR 세션 정리 (있는 경우)
        if (sessionCleaner != null)
        {
            sessionCleaner.CleanupAll();
            if (showDebugLogs) Debug.Log("[BackButton] AR 세션 정리 완료");
        }
        else
        {
            if (showDebugLogs) Debug.LogWarning("[BackButton] sessionCleaner가 없어 AR 정리를 건너뜁니다");
        }

        // 씬 전환
        if (transitionManager != null)
        {
            if (showDebugLogs) Debug.Log($"[BackButton] SceneTransitionManager로 씬 전환: {previousScene}");
            transitionManager.LoadScene(previousScene);
        }
        else
        {
            // TransitionManager가 없으면 직접 전환
            if (showDebugLogs) Debug.Log($"[BackButton] 직접 씬 전환: {previousScene}");
            SceneManager.LoadScene(previousScene);
        }
    }
}
