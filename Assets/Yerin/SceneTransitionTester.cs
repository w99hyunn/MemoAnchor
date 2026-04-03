using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// XR Space 씬 전환 테스트용 - 실제 3D 맵으로 테스트
/// 간단한 버튼만으로 씬 전환 확인
/// </summary>
public class SceneTransitionTester : MonoBehaviour
{
    [Header("Test UI")]
    [Tooltip("현재 씬 이름 표시 (선택사항)")]
    [SerializeField] private TMP_Text currentSceneText;

    [Tooltip("XR Space 위치 표시 (선택사항)")]
    [SerializeField] private TMP_Text xrSpacePositionText;

    [Header("Scene Names")]
    [SerializeField] private string arSceneName = "MeetingScene";
    [SerializeField] private string textSceneName = "TextMemoScene";
    [SerializeField] private string imageSceneName = "ImageMemoScene";
    [SerializeField] private string voiceSceneName = "VoiceMemoScene";
    [SerializeField] private string checklistSceneName = "ChecklistMemoScene";

    private Transform xrSpaceTransform;

    private void Start()
    {
        // XR Space 찾기
        FindXRSpace();

        // UI 업데이트
        UpdateUI();
    }

    private void Update()
    {
        // 실시간으로 XR Space 위치 업데이트
        if (xrSpacePositionText != null)
        {
            UpdateXRSpacePosition();
        }

        // 숫자 키로 빠른 테스트
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            Debug.Log("[Test] 1키: TextMemoScene 이동");
            GoToTextScene();
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            Debug.Log("[Test] 2키: ImageMemoScene 이동");
            GoToImageScene();
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            Debug.Log("[Test] 3키: VoiceMemoScene 이동");
            GoToVoiceScene();
        }
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            Debug.Log("[Test] 4키: ChecklistMemoScene 이동");
            GoToChecklistScene();
        }
        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            Debug.Log("[Test] 0키: AR Scene 복귀");
            ReturnToARScene();
        }
    }

    /// <summary>
    /// XR Space 오브젝트 찾기
    /// </summary>
    private void FindXRSpace()
    {
        GameObject xrSpace = GameObject.Find("XR Space_1");
        if (xrSpace != null)
        {
            xrSpaceTransform = xrSpace.transform;
            Debug.Log($"[Test] XR Space 찾음! 현재 위치: {xrSpaceTransform.position}");
        }
        else
        {
            Debug.LogWarning("[Test] XR Space_1을 찾을 수 없습니다!");
        }
    }

    /// <summary>
    /// UI 업데이트
    /// </summary>
    private void UpdateUI()
    {
        if (currentSceneText != null)
        {
            currentSceneText.text = $"현재 씬: {SceneManager.GetActiveScene().name}";
        }
    }

    /// <summary>
    /// XR Space 위치 업데이트
    /// </summary>
    private void UpdateXRSpacePosition()
    {
        if (xrSpaceTransform == null)
        {
            FindXRSpace();
        }

        if (xrSpaceTransform != null)
        {
            Vector3 pos = xrSpaceTransform.position;
            xrSpacePositionText.text = $"3D 맵 위치:\nX: {pos.x:F2} Y: {pos.y:F2} Z: {pos.z:F2}";
        }
    }

    // ========== 공개 메서드 (버튼에 연결) ==========

    /// <summary>
    /// Text 메모 씬으로 이동
    /// </summary>
    public void GoToTextScene()
    {
        Debug.Log($"[Test] {textSceneName} 씬으로 이동 중...");
        LoadScene(textSceneName);
    }

    /// <summary>
    /// Image 메모 씬으로 이동
    /// </summary>
    public void GoToImageScene()
    {
        Debug.Log($"[Test] {imageSceneName} 씬으로 이동 중...");
        LoadScene(imageSceneName);
    }

    /// <summary>
    /// Voice 메모 씬으로 이동
    /// </summary>
    public void GoToVoiceScene()
    {
        Debug.Log($"[Test] {voiceSceneName} 씬으로 이동 중...");
        LoadScene(voiceSceneName);
    }

    /// <summary>
    /// Checklist 메모 씬으로 이동
    /// </summary>
    public void GoToChecklistScene()
    {
        Debug.Log($"[Test] {checklistSceneName} 씬으로 이동 중...");
        LoadScene(checklistSceneName);
    }

    /// <summary>
    /// AR 씬으로 돌아가기
    /// </summary>
    public void ReturnToARScene()
    {
        Debug.Log($"[Test] {arSceneName} 씬으로 복귀 중...");
        LoadScene(arSceneName);
    }

    /// <summary>
    /// 씬 로드
    /// </summary>
    private void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[Test] 씬 이름이 비어있습니다!");
            return;
        }

        // 테스트용 더미 메모 데이터
        PlayerPrefs.SetString("SELECTED_MEMO_ID", "test_memo_123");
        PlayerPrefs.SetString("SELECTED_MEMO_TYPE", "text");
        PlayerPrefs.Save();

        SceneManager.LoadScene(sceneName);
    }
}
