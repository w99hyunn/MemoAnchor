
// ConstructionVPS 씬 로드 시 이전 씬의 UI Canvas를 정리하는 스크립트
using UnityEngine;
using UnityEngine.SceneManagement;

public class CleanupPreviousSceneUI : MonoBehaviour
{
    [Header("Cleanup Settings")]
    [Tooltip("정리할 Canvas 이름들 (MapBrowser 씬의 Canvas)")]
    [SerializeField]
    private string[] canvasNamesToCleanup = new string[]
    {
        "SplashCanvas",
        "AuthCanvas",
        "HomeCanvas",
        "BackgroundCanvas"
    };

    [Header("Debug")]
    [Tooltip("디버그 로그 출력")]
    [SerializeField] private bool showDebugLogs = true;

    private void Awake()
    {
        // ConstructionVPS 씬이 로드되었을 때 실행
        CleanupMapBrowserUI();
    }

    private void CleanupMapBrowserUI()
    {
        if (showDebugLogs)
            Debug.Log("[CleanupPreviousSceneUI] ConstructionVPS 씬 로드됨 - MapBrowser UI 정리 시작");

        // 모든 Canvas를 찾아서 정리
        Canvas[] allCanvases = FindObjectsOfType<Canvas>(true);

        int cleanedCount = 0;
        foreach (Canvas canvas in allCanvases)
        {
            // 정리 대상 Canvas인지 확인
            foreach (string targetName in canvasNamesToCleanup)
            {
                if (canvas.gameObject.name == targetName)
                {
                    // Canvas 비활성화
                    canvas.gameObject.SetActive(false);
                    cleanedCount++;

                    if (showDebugLogs)
                        Debug.Log($"[CleanupPreviousSceneUI] Canvas 비활성화: {canvas.gameObject.name}");

                    break;
                }
            }
        }

        if (showDebugLogs)
            Debug.Log($"[CleanupPreviousSceneUI] 정리 완료 - {cleanedCount}개 Canvas 비활성화");
    }

    // 씬이 변경될 때마다 정리 (선택적)
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // ConstructionVPS 씬이 로드되었을 때만 정리
        if (scene.name == "ConstructionVPS")
        {
            CleanupMapBrowserUI();
        }
    }
}
