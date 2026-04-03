using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 메모 씬에서 AR 씬으로 돌아가는 버튼 핸들러
/// </summary>
public class MemoSceneCloser : MonoBehaviour
{
    [Header("Scene Settings")]
    [Tooltip("돌아갈 AR 씬 이름")]
    [SerializeField] private string arSceneName = "MeetingScene";

    [Header("Debug")]
    [Tooltip("디버그 로그 출력")]
    [SerializeField] private bool verboseDebug = true;

    /// <summary>
    /// AR 씬으로 돌아가기
    /// UI 버튼의 OnClick 이벤트에 연결
    /// </summary>
    public void ReturnToARScene()
    {
        if (string.IsNullOrEmpty(arSceneName))
        {
            Debug.LogError("[MemoSceneCloser] AR scene name is not set!");
            return;
        }

        if (verboseDebug)
        {
            Debug.Log($"[MemoSceneCloser] Returning to AR scene: {arSceneName}");
        }

        // AR 씬으로 전환 (XRSpacePersistence가 3D 맵을 자동으로 원래 위치로 이동)
        SceneManager.LoadScene(arSceneName);
    }

    /// <summary>
    /// ESC 키로 닫기 (모바일에서는 Back 버튼)
    /// </summary>
    private void Update()
    {
        // ESC 키 또는 안드로이드 Back 버튼
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ReturnToARScene();
        }
    }
}
