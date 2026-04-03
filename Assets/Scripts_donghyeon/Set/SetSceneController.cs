
// Set 씬 컨트롤러 - 로그아웃 등 설정 관련 기능
using UnityEngine;
using UnityEngine.UI;

public class SetSceneController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("로그아웃 버튼")]
    [SerializeField] private Button logoutBtn;

    [Header("Scene Settings")]
    [Tooltip("로그아웃 후 이동할 씬 이름")]
    [SerializeField] private string mapBrowserSceneName = "MapBrowser";

    [Header("Debug")]
    [Tooltip("디버그 로그 출력")]
    [SerializeField] private bool showDebugLogs = true;

    private void Start()
    {
        // LogoutBtn 클릭 이벤트 연결
        if (logoutBtn != null)
        {
            logoutBtn.onClick.AddListener(OnLogoutButtonClicked);
            if (showDebugLogs)
                Debug.Log("[SetSceneController] LogoutBtn listener registered");
        }
        else
        {
            Debug.LogError("[SetSceneController] LogoutBtn이 할당되지 않았습니다!");
        }
    }

    private void OnLogoutButtonClicked()
    {
        if (showDebugLogs)
            Debug.Log("[SetSceneController] 로그아웃 버튼 클릭됨");

        // 로그아웃 처리 (필요한 경우 여기에 추가)
        // 예: PlayerPrefs 삭제, 토큰 초기화 등

        // Splash 스킵 플래그 설정
        SplashScreen.SkipSplashAndShowStart = true;

        if (showDebugLogs)
            Debug.Log($"[SetSceneController] {mapBrowserSceneName} 씬으로 이동 (Splash 스킵)");

        // MapBrowser 씬으로 이동
        SceneTransitionFade.LoadScene(mapBrowserSceneName);
    }

    private void OnDestroy()
    {
        // 메모리 누수 방지를 위해 리스너 제거
        if (logoutBtn != null)
        {
            logoutBtn.onClick.RemoveListener(OnLogoutButtonClicked);
        }
    }
}
