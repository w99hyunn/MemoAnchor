using System;
using UnityEngine;
using TMPro;

/// <summary>
/// 모든 메모 씬에서 공통으로 사용하는 베이스 클래스
/// 제목, 메모ID, 작성날짜, 마감날짜 등 공통 정보 표시
/// </summary>
public class MemoViewerBase : MonoBehaviour
{
    [Header("Common UI Elements")]
    [Tooltip("메모 제목을 표시할 텍스트")]
    [SerializeField] protected TMP_Text titleText;

    [Tooltip("메모 ID를 표시할 텍스트")]
    [SerializeField] protected TMP_Text memoIdText;

    [Tooltip("작성 날짜를 표시할 텍스트")]
    [SerializeField] protected TMP_Text createdAtText;

    [Tooltip("수정 날짜를 표시할 텍스트")]
    [SerializeField] protected TMP_Text updatedAtText;

    [Tooltip("마감 날짜를 표시할 텍스트")]
    [SerializeField] protected TMP_Text dueDateText;

    [Tooltip("마감 시간을 표시할 텍스트")]
    [SerializeField] protected TMP_Text dueTimeText;

    [Tooltip("상태를 표시할 텍스트")]
    [SerializeField] protected TMP_Text statusText;

    [Tooltip("담당자를 표시할 텍스트")]
    [SerializeField] protected TMP_Text assigneeText;

    [Tooltip("우선순위를 표시할 텍스트")]
    [SerializeField] protected TMP_Text priorityText;

    [Header("Debug")]
    [SerializeField] protected bool verboseDebug = true;

    protected string currentMemoId;
    protected string currentMemoType;
    protected TabPinCreate.PinData currentMemoData;

    protected virtual void Start()
    {
        // PlayerPrefs에서 선택된 메모 정보 가져오기
        currentMemoId = PlayerPrefs.GetString("SELECTED_MEMO_ID", "");
        currentMemoType = PlayerPrefs.GetString("SELECTED_MEMO_TYPE", "text");

        if (verboseDebug)
        {
            Debug.Log($"[MemoViewerBase] Loaded memo - ID: {currentMemoId}, Type: {currentMemoType}");
        }

        // 메모 데이터 로드
        LoadMemoData();

        // UI 표시
        DisplayCommonData();
    }

    /// <summary>
    /// TabPinCreate에서 메모 데이터 로드
    /// </summary>
    protected virtual void LoadMemoData()
    {
        if (TabPinCreate.Instance == null)
        {
            Debug.LogError("[MemoViewerBase] TabPinCreate.Instance is null!");
            return;
        }

        if (string.IsNullOrEmpty(currentMemoId))
        {
            Debug.LogWarning("[MemoViewerBase] No memo ID found in PlayerPrefs!");
            return;
        }

        // 모든 메모 데이터에서 해당 ID 찾기
        var allMemos = TabPinCreate.Instance.GetAllPinData();
        currentMemoData = allMemos.Find(m => m.id == currentMemoId);

        if (currentMemoData == null)
        {
            Debug.LogError($"[MemoViewerBase] Memo not found with ID: {currentMemoId}");
            return;
        }

        if (verboseDebug)
        {
            Debug.Log($"[MemoViewerBase] Memo data loaded successfully - Title: {currentMemoData.title}");
        }
    }

    /// <summary>
    /// 공통 데이터 UI에 표시
    /// </summary>
    protected virtual void DisplayCommonData()
    {
        if (currentMemoData == null)
        {
            Debug.LogWarning("[MemoViewerBase] No memo data to display!");
            return;
        }

        // 제목
        if (titleText != null)
        {
            titleText.text = !string.IsNullOrEmpty(currentMemoData.title) 
                ? currentMemoData.title 
                : "제목 없음";
        }

        // 메모 ID
        if (memoIdText != null)
        {
            memoIdText.text = $"ID: {currentMemoData.id}";
        }

        // 작성 날짜
        if (createdAtText != null)
        {
            createdAtText.text = !string.IsNullOrEmpty(currentMemoData.createdAt) 
                ? $"작성: {FormatDateTime(currentMemoData.createdAt)}" 
                : "작성일 없음";
        }

        // 수정 날짜
        if (updatedAtText != null)
        {
            updatedAtText.text = !string.IsNullOrEmpty(currentMemoData.updatedAt) 
                ? $"수정: {FormatDateTime(currentMemoData.updatedAt)}" 
                : "수정일 없음";
        }

        // 마감 날짜
        if (dueDateText != null)
        {
            if (!string.IsNullOrEmpty(currentMemoData.dueDate))
            {
                dueDateText.text = $"마감: {currentMemoData.dueDate}";
            }
            else
            {
                dueDateText.text = "마감일 없음";
            }
        }

        // 마감 시간
        if (dueTimeText != null)
        {
            if (!string.IsNullOrEmpty(currentMemoData.dueTime))
            {
                dueTimeText.text = $"시간: {currentMemoData.dueTime}";
            }
            else
            {
                dueTimeText.text = "";
            }
        }

        // 상태
        if (statusText != null)
        {
            statusText.text = $"상태: {currentMemoData.status}";
        }

        // 담당자
        if (assigneeText != null)
        {
            if (!string.IsNullOrEmpty(currentMemoData.assignee))
            {
                assigneeText.text = $"담당자: {currentMemoData.assignee}";
            }
            else
            {
                assigneeText.text = "담당자 없음";
            }
        }

        // 우선순위
        if (priorityText != null)
        {
            priorityText.text = $"우선순위: {currentMemoData.priority}";
        }

        if (verboseDebug)
        {
            Debug.Log("[MemoViewerBase] Common data displayed successfully");
        }
    }

    /// <summary>
    /// 날짜/시간 포맷 변환
    /// </summary>
    protected string FormatDateTime(string dateTimeString)
    {
        if (string.IsNullOrEmpty(dateTimeString)) return "";

        try
        {
            // "yyyy-MM-dd HH:mm:ss" 형식을 "yyyy년 MM월 dd일 HH:mm"로 변환
            DateTime dt = DateTime.Parse(dateTimeString);
            return dt.ToString("yyyy년 MM월 dd일 HH:mm");
        }
        catch
        {
            return dateTimeString; // 파싱 실패 시 원본 반환
        }
    }

    /// <summary>
    /// 현재 메모 데이터 반환 (자식 클래스에서 사용)
    /// </summary>
    public TabPinCreate.PinData GetCurrentMemoData()
    {
        return currentMemoData;
    }
}
