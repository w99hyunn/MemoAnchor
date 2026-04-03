using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MemoArchiveManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TabPinCreate pinStore; // 기존 저장소

    [Header("Settings")]
    [SerializeField] private bool autoArchiveCompleted = true; // 완료 시 자동 보관
    [SerializeField] private int daysBeforeAutoArchive = 30;   // N일 후 자동 보관
    [SerializeField] private bool logDebug = true;

    // 전체 메모 리스트 (메모리 캐시)
    private List<MemoData> allMemos = new List<MemoData>();


    // ==================== 초기화 ====================

    private void Start()
    {
        // 씬에 있는 모든 MemoData 수집
        RefreshMemoList();
    }

    /// <summary>씬의 모든 메모를 다시 수집</summary>
    public void RefreshMemoList()
    {
        allMemos.Clear();
        MemoData[] memos = FindObjectsOfType<MemoData>();
        allMemos.AddRange(memos);

        if (logDebug)
            Debug.Log($"[Archive] 메모 {allMemos.Count}개 로드됨");
    }


    // ==================== 상태 변경 ====================

    /// <summary>메모를 완료 처리</summary>
    public void CompleteMemo(string memoId)
    {
        MemoData memo = FindMemoById(memoId);
        if (memo == null)
        {
            if (logDebug) Debug.LogWarning($"[Archive] 메모를 찾을 수 없음: {memoId}");
            return;
        }

        memo.status = MemoStatus.Completed;
        memo.completedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        memo.updatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        SaveMemo(memo);

        // 완료 시 자동 보관 옵션
        if (autoArchiveCompleted)
        {
            ArchiveMemo(memoId, "자동 보관: 해결 완료");
        }

        if (logDebug) Debug.Log($"[Archive] 메모 완료: {memo.title}");
    }

    /// <summary>메모를 보관 처리</summary>
    public void ArchiveMemo(string memoId, string reason = "")
    {
        MemoData memo = FindMemoById(memoId);
        if (memo == null)
        {
            if (logDebug) Debug.LogWarning($"[Archive] 메모를 찾을 수 없음: {memoId}");
            return;
        }

        memo.status = MemoStatus.Archived;
        memo.archivedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        memo.updatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        memo.archiveReason = reason;

        SaveMemo(memo);

        // 씬에서 메모 오브젝트 숨기기 (삭제 안 함!)
        if (memo.gameObject != null)
            memo.gameObject.SetActive(false);

        if (logDebug) Debug.Log($"[Archive] 메모 보관: {memo.title}, 사유: {reason}");
    }

    /// <summary>메모를 소프트 삭제 (실제로는 삭제 안 함)</summary>
    public void SoftDeleteMemo(string memoId)
    {
        MemoData memo = FindMemoById(memoId);
        if (memo == null)
        {
            if (logDebug) Debug.LogWarning($"[Archive] 메모를 찾을 수 없음: {memoId}");
            return;
        }

        memo.status = MemoStatus.Deleted;
        memo.updatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        SaveMemo(memo);

        if (memo.gameObject != null)
            memo.gameObject.SetActive(false);

        if (logDebug) Debug.Log($"[Archive] 메모 소프트 삭제: {memo.title}");
    }

    /// <summary>보관된 메모 복원</summary>
    public void RestoreMemo(string memoId)
    {
        MemoData memo = FindMemoById(memoId);
        if (memo == null)
        {
            if (logDebug) Debug.LogWarning($"[Archive] 메모를 찾을 수 없음: {memoId}");
            return;
        }

        memo.status = MemoStatus.Active;
        memo.updatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        SaveMemo(memo);

        if (memo.gameObject != null)
            memo.gameObject.SetActive(true);

        if (logDebug) Debug.Log($"[Archive] 메모 복원: {memo.title}");
    }


    // ==================== 조회 ====================

    /// <summary>활성 메모만 가져오기</summary>
    public List<MemoData> GetActiveMemos()
    {
        List<MemoData> result = new List<MemoData>();
        foreach (var memo in allMemos)
        {
            if (memo.status == MemoStatus.Active)
                result.Add(memo);
        }
        return result;
    }

    /// <summary>보관된 메모 가져오기</summary>
    public List<MemoData> GetArchivedMemos()
    {
        List<MemoData> result = new List<MemoData>();
        foreach (var memo in allMemos)
        {
            if (memo.status == MemoStatus.Archived)
                result.Add(memo);
        }
        return result;
    }

    /// <summary>완료된 메모 가져오기</summary>
    public List<MemoData> GetCompletedMemos()
    {
        List<MemoData> result = new List<MemoData>();
        foreach (var memo in allMemos)
        {
            if (memo.status == MemoStatus.Completed)
                result.Add(memo);
        }
        return result;
    }

    /// <summary>특정 기간의 메모 조회</summary>
    public List<MemoData> GetMemosByDateRange(DateTime start, DateTime end)
    {
        List<MemoData> result = new List<MemoData>();
        foreach (var memo in allMemos)
        {
            DateTime created;
            if (DateTime.TryParse(memo.createdAt, out created))
            {
                if (created >= start && created <= end)
                    result.Add(memo);
            }
        }
        return result;
    }

    /// <summary>특정 사용자의 메모 조회</summary>
    public List<MemoData> GetMemosByAssignee(string assignee)
    {
        List<MemoData> result = new List<MemoData>();
        foreach (var memo in allMemos)
        {
            if (memo.assignee == assignee && memo.status == MemoStatus.Active)
                result.Add(memo);
        }
        return result;
    }

    /// <summary>전체 메모 가져오기</summary>
    public List<MemoData> GetAllMemos()
    {
        return new List<MemoData>(allMemos);
    }


    // ==================== 자동 보관 ====================

    /// <summary>오래된 완료 메모 자동 보관</summary>
    public void AutoArchiveOldMemos()
    {
        DateTime threshold = DateTime.Now.AddDays(-daysBeforeAutoArchive);
        int archivedCount = 0;

        foreach (var memo in allMemos)
        {
            // 완료 상태이고 오래된 메모만
            if (memo.status == MemoStatus.Completed)
            {
                DateTime completed;
                if (DateTime.TryParse(memo.completedAt, out completed))
                {
                    if (completed < threshold)
                    {
                        ArchiveMemo(memo.id, string.Format("{0}일 경과로 자동 보관", daysBeforeAutoArchive));
                        archivedCount++;
                    }
                }
            }
        }

        if (logDebug)
            Debug.Log($"[Archive] 자동 보관 완료: {archivedCount}개");
    }


    // ==================== 통계 ====================

    /// <summary>메모 통계 정보</summary>
    public MemoStatistics GetStatistics()
    {
        MemoStatistics stats = new MemoStatistics();
        stats.totalCount = allMemos.Count;
        stats.activeCount = 0;
        stats.completedCount = 0;
        stats.archivedCount = 0;
        stats.deletedCount = 0;

        foreach (var memo in allMemos)
        {
            switch (memo.status)
            {
                case MemoStatus.Active:
                    stats.activeCount++;
                    break;
                case MemoStatus.Completed:
                    stats.completedCount++;
                    break;
                case MemoStatus.Archived:
                    stats.archivedCount++;
                    break;
                case MemoStatus.Deleted:
                    stats.deletedCount++;
                    break;
            }
        }

        return stats;
    }

    /// <summary>통계를 로그로 출력</summary>
    public void PrintStatistics()
    {
        MemoStatistics stats = GetStatistics();
        Debug.Log($"[Archive] 통계 - 전체:{stats.totalCount} 활성:{stats.activeCount} 완료:{stats.completedCount} 보관:{stats.archivedCount} 삭제:{stats.deletedCount}");
    }


    // ==================== 내부 헬퍼 ====================

    private MemoData FindMemoById(string id)
    {
        foreach (var memo in allMemos)
        {
            if (memo.id == id)
                return memo;
        }
        return null;
    }

    private void SaveMemo(MemoData memo)
    {
        // TabPinCreate를 통해 JSON 저장
        if (pinStore != null)
        {
            pinStore.SaveMemoComplete(memo);
        }
        else
        {
            if (logDebug)
                Debug.LogWarning("[Archive] pinStore가 null입니다. Inspector에서 TabPinCreate를 할당하세요.");
        }
    }

    /// <summary>새 메모를 리스트에 추가</summary>
    public void RegisterMemo(MemoData memo)
    {
        if (memo == null) return;

        // 중복 체크
        if (FindMemoById(memo.id) == null)
        {
            allMemos.Add(memo);
            if (logDebug)
                Debug.Log($"[Archive] 새 메모 등록: {memo.title}");
        }
    }

    /// <summary>메모를 리스트에서 제거 (실제 삭제용 - 거의 사용 안 함)</summary>
    public void UnregisterMemo(string memoId)
    {
        MemoData memo = FindMemoById(memoId);
        if (memo != null)
        {
            allMemos.Remove(memo);
            if (logDebug)
                Debug.Log($"[Archive] 메모 등록 해제: {memo.title}");
        }
    }
}

// 통계 데이터 구조
[System.Serializable]
public class MemoStatistics
{
    public int totalCount;
    public int activeCount;
    public int completedCount;
    public int archivedCount;
    public int deletedCount;
}