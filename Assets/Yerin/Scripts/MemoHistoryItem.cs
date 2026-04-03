using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MemoHistoryItem : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text metaText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Image statusIcon;
    [SerializeField] private Image background;

    [Header("Buttons")]
    [SerializeField] private Button btnDetail;
    [SerializeField] private Button btnRestore;

    [Header("Status Colors")]
    [SerializeField] private Color activeColor = new Color(0.2f, 0.8f, 0.2f);
    [SerializeField] private Color completedColor = new Color(0.2f, 0.6f, 1f);
    [SerializeField] private Color archivedColor = new Color(0.7f, 0.7f, 0.7f);
    [SerializeField] private Color deletedColor = new Color(0.9f, 0.3f, 0.3f);

    private MemoData memo;
    private MemoHistoryViewer viewer;


    public void Initialize(MemoData memoData, MemoHistoryViewer historyViewer)
    {
        memo = memoData;
        viewer = historyViewer;

        UpdateUI();
        SetupButtons();
    }


    private void UpdateUI()
    {
        if (memo == null) return;

        // 제목
        if (titleText)
        {
            titleText.text = string.IsNullOrEmpty(memo.title) ? "(제목 없음)" : memo.title;
        }

        // 메타 정보
        if (metaText)
        {
            string dateStr = "날짜 미상";
            if (!string.IsNullOrEmpty(memo.createdAt) && memo.createdAt.Length >= 10)
                dateStr = memo.createdAt.Substring(0, 10);

            string assignee = string.IsNullOrEmpty(memo.assignee) ? "" : $" | {memo.assignee}";
            metaText.text = $"{dateStr}{assignee}";
        }

        // 상태 텍스트
        if (statusText)
        {
            statusText.text = GetStatusKorean(memo.status);
        }

        // 상태별 색상
        Color statusColor = GetStatusColor(memo.status);

        if (statusIcon)
            statusIcon.color = statusColor;

        if (background)
        {
            Color bgColor = statusColor;
            bgColor.a = 0.1f; // 반투명 배경
            background.color = bgColor;
        }
    }


    private void SetupButtons()
    {
        // 상세보기 버튼
        if (btnDetail)
        {
            btnDetail.onClick.RemoveAllListeners();
            btnDetail.onClick.AddListener(OnDetailClicked);
        }

        // 복원 버튼
        if (btnRestore)
        {
            bool canRestore = (memo.status == MemoStatus.Archived || memo.status == MemoStatus.Deleted);
            btnRestore.gameObject.SetActive(canRestore);

            if (canRestore)
            {
                btnRestore.onClick.RemoveAllListeners();
                btnRestore.onClick.AddListener(OnRestoreClicked);
            }
        }
    }


    private void OnDetailClicked()
    {
        // MemoHistoryViewer의 상세보기 패널 열기 (리플렉션 사용)
        if (viewer != null)
        {
            var method = viewer.GetType().GetMethod("ShowDetailPanel",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (method != null)
                method.Invoke(viewer, new object[] { memo });
        }
    }


    private void OnRestoreClicked()
    {
        // MemoHistoryViewer의 복원 메서드 호출 (리플렉션 사용)
        if (viewer != null)
        {
            var method = viewer.GetType().GetMethod("RestoreMemo",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (method != null)
                method.Invoke(viewer, new object[] { memo });
        }
    }


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


    private Color GetStatusColor(MemoStatus status)
    {
        switch (status)
        {
            case MemoStatus.Active: return activeColor;
            case MemoStatus.Completed: return completedColor;
            case MemoStatus.Archived: return archivedColor;
            case MemoStatus.Deleted: return deletedColor;
            default: return Color.white;
        }
    }
}
