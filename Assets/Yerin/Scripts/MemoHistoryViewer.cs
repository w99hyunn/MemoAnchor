using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class MemoHistoryViewer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MemoArchiveManager archiveManager;
    [SerializeField] private GameObject historyPanel;

    [Header("Filter Buttons")]
    [SerializeField] private Button btnFilterAll;
    [SerializeField] private Button btnFilterActive;
    [SerializeField] private Button btnFilterCompleted;
    [SerializeField] private Button btnFilterArchived;

    [Header("Scroll View")]
    [SerializeField] private Transform contentParent;  // ScrollView의 Content
    [SerializeField] private GameObject itemPrefab;    // MemoHistoryItem 프리팹

    [Header("UI Elements")]
    [SerializeField] private Button btnClose;
    [SerializeField] private TMP_Text statsText;       // 통계 표시
    [SerializeField] private TMP_Text emptyText;       // "메모가 없습니다" 표시

    [Header("Detail View (Optional)")]
    [SerializeField] private GameObject detailPanel;   // 상세보기 패널
    [SerializeField] private TMP_Text detailTitle;
    [SerializeField] private TMP_Text detailBody;
    [SerializeField] private TMP_Text detailMeta;
    [SerializeField] private Button btnCloseDetail;

    [Header("Settings")]
    [SerializeField] private bool autoRefreshOnOpen = true;
    [SerializeField] private bool logDebug = true;

    // 현재 선택된 필터
    private MemoStatus? currentFilter = null;

    // 현재 표시 중인 메모 리스트
    private List<MemoData> displayedMemos = new List<MemoData>();


    private void Awake()
    {
        // 필터 버튼 연결
        if (btnFilterAll)
        {
            btnFilterAll.onClick.RemoveListener(() => ShowMemos(null));
            btnFilterAll.onClick.AddListener(() => ShowMemos(null));
        }

        if (btnFilterActive)
        {
            btnFilterActive.onClick.RemoveListener(() => ShowMemos(MemoStatus.Active));
            btnFilterActive.onClick.AddListener(() => ShowMemos(MemoStatus.Active));
        }

        if (btnFilterCompleted)
        {
            btnFilterCompleted.onClick.RemoveListener(() => ShowMemos(MemoStatus.Completed));
            btnFilterCompleted.onClick.AddListener(() => ShowMemos(MemoStatus.Completed));
        }

        if (btnFilterArchived)
        {
            btnFilterArchived.onClick.RemoveListener(() => ShowMemos(MemoStatus.Archived));
            btnFilterArchived.onClick.AddListener(() => ShowMemos(MemoStatus.Archived));
        }

        // 닫기 버튼 연결
        if (btnClose)
        {
            btnClose.onClick.RemoveListener(CloseHistoryPanel);
            btnClose.onClick.AddListener(CloseHistoryPanel);
        }

        if (btnCloseDetail)
        {
            btnCloseDetail.onClick.RemoveListener(CloseDetailPanel);
            btnCloseDetail.onClick.AddListener(CloseDetailPanel);
        }

        // 초기 상태: 패널 숨김
        if (historyPanel) historyPanel.SetActive(false);
        if (detailPanel) detailPanel.SetActive(false);
    }


    // ==================== Public Methods ====================

    /// <summary>히스토리 패널 열기</summary>
    public void OpenHistoryPanel()
    {
        if (!historyPanel)
        {
            if (logDebug) Debug.LogWarning("[HistoryViewer] historyPanel is null");
            return;
        }

        historyPanel.SetActive(true);

        if (autoRefreshOnOpen)
        {
            RefreshMemoList();
            ShowMemos(null); // 전체 보기
        }

        if (logDebug) Debug.Log("[HistoryViewer] Panel opened");
    }

    /// <summary>히스토리 패널 닫기</summary>
    public void CloseHistoryPanel()
    {
        if (historyPanel) historyPanel.SetActive(false);
        CloseDetailPanel();

        if (logDebug) Debug.Log("[HistoryViewer] Panel closed");
    }

    /// <summary>메모 리스트 새로고침 (MemoArchiveManager에서 다시 가져오기)</summary>
    public void RefreshMemoList()
    {
        if (!archiveManager)
        {
            if (logDebug) Debug.LogWarning("[HistoryViewer] archiveManager is null");
            return;
        }

        archiveManager.RefreshMemoList();
        UpdateStatistics();

        if (logDebug) Debug.Log("[HistoryViewer] Memo list refreshed");
    }


    // ==================== Private Methods ====================

    /// <summary>필터에 따라 메모 표시</summary>
    private void ShowMemos(MemoStatus? filter)
    {
        if (!archiveManager)
        {
            if (logDebug) Debug.LogWarning("[HistoryViewer] archiveManager is null");
            return;
        }

        currentFilter = filter;

        // 기존 아이템 삭제
        ClearContent();

        // 필터에 따라 메모 가져오기
        List<MemoData> memos = GetFilteredMemos(filter);
        displayedMemos = memos;

        // 빈 상태 처리
        if (memos.Count == 0)
        {
            ShowEmptyMessage(filter);
            return;
        }

        if (emptyText) emptyText.gameObject.SetActive(false);

        // UI 아이템 생성
        foreach (var memo in memos)
        {
            CreateMemoItem(memo);
        }

        // 통계 업데이트
        UpdateStatistics();

        if (logDebug)
            Debug.Log($"[HistoryViewer] Showing {memos.Count} memos (filter={filter})");
    }

    /// <summary>필터에 맞는 메모 가져오기</summary>
    private List<MemoData> GetFilteredMemos(MemoStatus? filter)
    {
        if (filter == null)
        {
            // 전체
            return archiveManager.GetAllMemos();
        }

        switch (filter.Value)
        {
            case MemoStatus.Active:
                return archiveManager.GetActiveMemos();

            case MemoStatus.Completed:
                return archiveManager.GetCompletedMemos();

            case MemoStatus.Archived:
                return archiveManager.GetArchivedMemos();

            default:
                return new List<MemoData>();
        }
    }

    /// <summary>메모 아이템 UI 생성</summary>
    private void CreateMemoItem(MemoData memo)
    {
        if (!itemPrefab || !contentParent) return;

        GameObject item = Instantiate(itemPrefab, contentParent);

        // MemoHistoryItem 컴포넌트 가져오기
        MemoHistoryItem itemComponent = item.GetComponent<MemoHistoryItem>();
        if (itemComponent != null)
        {
            itemComponent.Initialize(memo, this);
        }
        else
        {
            // 컴포넌트 없으면 직접 설정 (fallback)
            SetupItemManually(item, memo);
        }
    }

    /// <summary>컴포넌트 없을 때 수동 설정</summary>
    private void SetupItemManually(GameObject item, MemoData memo)
    {
        // 제목
        TMP_Text titleText = item.transform.Find("TitleText")?.GetComponent<TMP_Text>();
        if (titleText) titleText.text = string.IsNullOrEmpty(memo.title) ? "(제목 없음)" : memo.title;

        // 메타 정보
        TMP_Text metaText = item.transform.Find("MetaText")?.GetComponent<TMP_Text>();
        if (metaText)
        {
            string dateStr = string.IsNullOrEmpty(memo.createdAt) ? "날짜 미상" : memo.createdAt.Substring(0, 10);
            string assignee = string.IsNullOrEmpty(memo.assignee) ? "" : $" | {memo.assignee}";
            metaText.text = $"{dateStr}{assignee}";
        }

        // 상태 뱃지
        TMP_Text statusText = item.transform.Find("StatusBadge/Text")?.GetComponent<TMP_Text>();
        if (statusText) statusText.text = GetStatusKorean(memo.status);

        // 버튼들
        Transform buttonGroup = item.transform.Find("ButtonGroup");
        if (buttonGroup)
        {
            Button btnDetail = buttonGroup.Find("BtnDetail")?.GetComponent<Button>();
            Button btnRestore = buttonGroup.Find("BtnRestore")?.GetComponent<Button>();

            if (btnDetail)
            {
                btnDetail.onClick.RemoveAllListeners();
                btnDetail.onClick.AddListener(() => ShowDetailPanel(memo));
            }

            if (btnRestore)
            {
                // 보관/삭제 상태일 때만 복원 버튼 표시
                bool canRestore = (memo.status == MemoStatus.Archived || memo.status == MemoStatus.Deleted);
                btnRestore.gameObject.SetActive(canRestore);

                if (canRestore)
                {
                    btnRestore.onClick.RemoveAllListeners();
                    btnRestore.onClick.AddListener(() => RestoreMemo(memo));
                }
            }
        }
    }

    /// <summary>기존 아이템 모두 삭제</summary>
    private void ClearContent()
    {
        if (!contentParent) return;

        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }
    }

    /// <summary>빈 상태 메시지 표시</summary>
    private void ShowEmptyMessage(MemoStatus? filter)
    {
        if (!emptyText) return;

        string message = filter switch
        {
            MemoStatus.Active => "활성 메모가 없습니다",
            MemoStatus.Completed => "완료된 메모가 없습니다",
            MemoStatus.Archived => "보관된 메모가 없습니다",
            _ => "메모가 없습니다"
        };

        emptyText.text = message;
        emptyText.gameObject.SetActive(true);
    }

    /// <summary>통계 정보 업데이트</summary>
    private void UpdateStatistics()
    {
        if (!statsText || !archiveManager) return;

        var stats = archiveManager.GetStatistics();

        statsText.text = $"전체: {stats.totalCount} | " +
                        $"활성: {stats.activeCount} | " +
                        $"완료: {stats.completedCount} | " +
                        $"보관: {stats.archivedCount}";
    }

    /// <summary>상세보기 패널 열기</summary>
    private void ShowDetailPanel(MemoData memo)
    {
        if (!detailPanel) return;

        detailPanel.SetActive(true);

        if (detailTitle)
            detailTitle.text = string.IsNullOrEmpty(memo.title) ? "(제목 없음)" : memo.title;

        if (detailBody)
            detailBody.text = string.IsNullOrEmpty(memo.body) ? "(내용 없음)" : memo.body;

        if (detailMeta)
        {
            string meta = $"상태: {GetStatusKorean(memo.status)}\n";
            meta += $"생성: {memo.createdAt}\n";
            meta += $"수정: {memo.updatedAt}\n";

            if (!string.IsNullOrEmpty(memo.completedAt))
                meta += $"완료: {memo.completedAt}\n";

            if (!string.IsNullOrEmpty(memo.archivedAt))
                meta += $"보관: {memo.archivedAt}\n";

            if (!string.IsNullOrEmpty(memo.assignee))
                meta += $"담당자: {memo.assignee}\n";

            detailMeta.text = meta;
        }

        if (logDebug) Debug.Log($"[HistoryViewer] Detail opened: {memo.title}");
    }

    /// <summary>상세보기 패널 닫기</summary>
    private void CloseDetailPanel()
    {
        if (detailPanel) detailPanel.SetActive(false);
    }

    /// <summary>메모 복원</summary>
    private void RestoreMemo(MemoData memo)
    {
        if (!archiveManager)
        {
            if (logDebug) Debug.LogWarning("[HistoryViewer] archiveManager is null");
            return;
        }

        archiveManager.RestoreMemo(memo.id);

        // 리스트 새로고침
        ShowMemos(currentFilter);

        if (logDebug) Debug.Log($"[HistoryViewer] Memo restored: {memo.title}");
    }

    /// <summary>상태를 한글로 변환</summary>
    private string GetStatusKorean(MemoStatus status)
    {
        switch (status)
        {
            case MemoStatus.Active: return "활성";
            case MemoStatus.Completed: return "완료";
            case MemoStatus.Archived: return "보관";
            case MemoStatus.Deleted: return "삭제";
            default: return "알 수 없음";
        }
    }
}