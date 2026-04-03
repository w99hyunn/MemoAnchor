using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 프리팹을 클릭하면 색깔이 변하고 메모 타입에 따라 다른 씬으로 전환
/// </summary>
public class PinClickHandler : MonoBehaviour
{
    [Header("Click Settings")]
    [Tooltip("클릭 감지를 위한 레이캐스트 카메라 (비우면 Main Camera 자동 사용)")]
    [SerializeField] private Camera raycastCamera;

    [Header("Color Change Settings")]
    [Tooltip("클릭 시 변경할 색상")]
    [SerializeField] private Color clickedColor = Color.yellow;

    [Tooltip("원래 색상으로 돌아가는 시간 (초, 0이면 안돌아감)")]
    [SerializeField] private float colorResetTime = 0f;

    [Tooltip("씬 전환 전 색상 유지 시간 (초)")]
    [SerializeField] private float sceneChangeDelay = 0.3f;

    [Header("Scene Names by Memo Type")]
    [Tooltip("text 타입 메모일 때 이동할 씬 이름")]
    [SerializeField] private string textMemoSceneName = "TextMemoScene";

    [Tooltip("image 타입 메모일 때 이동할 씬 이름")]
    [SerializeField] private string imageMemoSceneName = "ImageMemoScene";

    [Tooltip("checklist 타입 메모일 때 이동할 씬 이름")]
    [SerializeField] private string checklistMemoSceneName = "ChecklistMemoScene";

    [Tooltip("voice 타입 메모일 때 이동할 씬 이름")]
    [SerializeField] private string voiceMemoSceneName = "VoiceMemoScene";

    [Header("Memo Data")]
    [Tooltip("이 프리팹이 나타내는 메모 타입 (text, image, checklist, voice)")]
    [SerializeField] private string memoType = "text";

    [Tooltip("연결된 메모 ID (선택사항)")]
    [SerializeField] private string memoId = "";

    [Header("Debug")]
    [Tooltip("디버그 로그 출력")]
    [SerializeField] private bool verboseDebug = true;

    private Renderer objectRenderer;
    private Color originalColor;
    private bool isClicked = false;

    private void Start()
    {
        // 카메라 자동 설정
        if (raycastCamera == null)
        {
            raycastCamera = Camera.main;
            if (verboseDebug && raycastCamera != null)
            {
                Debug.Log($"[PinClickHandler] Camera auto-assigned: {raycastCamera.name}");
            }
        }

        // Renderer 컴포넌트 가져오기
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer != null)
        {
            // 원래 색상 저장 (Material의 첫 번째 색상)
            originalColor = objectRenderer.material.color;

            if (verboseDebug)
            {
                Debug.Log($"[PinClickHandler] Original color saved: {originalColor}");
            }
        }
        else
        {
            Debug.LogWarning($"[PinClickHandler] No Renderer found on {gameObject.name}! Color change won't work.");
        }
    }

    private void OnMouseDown()
    {
        // 이미 클릭 처리 중이면 무시
        if (isClicked) return;

        if (verboseDebug)
        {
            Debug.Log($"[PinClickHandler] Clicked! Object: {gameObject.name}, MemoType: {memoType}");
        }

        // 클릭 처리 시작
        isClicked = true;

        // 색상 변경
        ChangeColor();

        // 씬 전환 시작
        StartCoroutine(LoadSceneAfterDelay());
    }

    /// <summary>
    /// 오브젝트 색상 변경
    /// </summary>
    private void ChangeColor()
    {
        if (objectRenderer == null) return;

        // 색상 변경
        objectRenderer.material.color = clickedColor;

        if (verboseDebug)
        {
            Debug.Log($"[PinClickHandler] Color changed to: {clickedColor}");
        }

        // 원래 색상으로 돌아가기 (colorResetTime > 0일 때만)
        if (colorResetTime > 0f)
        {
            StartCoroutine(ResetColorAfterDelay());
        }
    }

    /// <summary>
    /// 일정 시간 후 원래 색상으로 복구
    /// </summary>
    private IEnumerator ResetColorAfterDelay()
    {
        yield return new WaitForSeconds(colorResetTime);

        if (objectRenderer != null)
        {
            objectRenderer.material.color = originalColor;

            if (verboseDebug)
            {
                Debug.Log($"[PinClickHandler] Color reset to original: {originalColor}");
            }
        }
    }

    /// <summary>
    /// 딜레이 후 씬 전환
    /// </summary>
    private IEnumerator LoadSceneAfterDelay()
    {
        // 색상 변화를 보여주기 위한 딜레이
        yield return new WaitForSeconds(sceneChangeDelay);

        // 메모 타입에 따른 씬 이름 결정
        string targetScene = GetSceneNameByMemoType();

        if (string.IsNullOrEmpty(targetScene))
        {
            Debug.LogError($"[PinClickHandler] No scene name set for memo type: {memoType}");
            isClicked = false;
            yield break;
        }

        if (verboseDebug)
        {
            Debug.Log($"[PinClickHandler] Loading scene: {targetScene} (MemoType: {memoType})");
        }

        // 메모 ID를 PlayerPrefs에 저장 (다음 씬에서 사용 가능)
        if (!string.IsNullOrEmpty(memoId))
        {
            PlayerPrefs.SetString("SELECTED_MEMO_ID", memoId);
            PlayerPrefs.Save();
        }

        // 메모 타입도 저장
        PlayerPrefs.SetString("SELECTED_MEMO_TYPE", memoType);
        PlayerPrefs.Save();

        // 씬 전환 (XRSpacePersistence가 3D 맵을 자동으로 이동시킴)
        try
        {
            SceneManager.LoadScene(targetScene);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[PinClickHandler] Failed to load scene '{targetScene}': {e.Message}");
            isClicked = false;
        }
    }

    /// <summary>
    /// 메모 타입에 따른 씬 이름 반환
    /// </summary>
    private string GetSceneNameByMemoType()
    {
        switch (memoType.ToLower())
        {
            case "text":
                return textMemoSceneName;
            case "image":
                return imageMemoSceneName;
            case "checklist":
                return checklistMemoSceneName;
            case "voice":
                return voiceMemoSceneName;
            default:
                Debug.LogWarning($"[PinClickHandler] Unknown memo type: {memoType}, using text scene");
                return textMemoSceneName;
        }
    }

    /// <summary>
    /// 외부에서 메모 타입 설정 (JustRandomSpawner에서 사용 가능)
    /// </summary>
    public void SetMemoType(string type)
    {
        memoType = type;

        if (verboseDebug)
        {
            Debug.Log($"[PinClickHandler] MemoType set to: {memoType}");
        }
    }

    /// <summary>
    /// 외부에서 메모 ID 설정
    /// </summary>
    public void SetMemoId(string id)
    {
        memoId = id;

        if (verboseDebug)
        {
            Debug.Log($"[PinClickHandler] MemoId set to: {memoId}");
        }
    }

    /// <summary>
    /// 메모 타입과 ID를 한번에 설정
    /// </summary>
    public void SetMemoData(string type, string id)
    {
        memoType = type;
        memoId = id;

        if (verboseDebug)
        {
            Debug.Log($"[PinClickHandler] MemoData set - Type: {memoType}, ID: {memoId}");
        }
    }

    /// <summary>
    /// 클릭 가능 여부 설정
    /// </summary>
    public void SetClickable(bool clickable)
    {
        isClicked = !clickable;
    }

    // Gizmos로 클릭 가능 범위 표시 (Scene 뷰에서만 보임)
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;

        // 콜라이더가 있으면 콜라이더 범위 표시
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        }
        else
        {
            // 없으면 간단한 구 표시
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
    }
}