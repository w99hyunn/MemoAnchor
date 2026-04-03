// 핀에 저장될 메모 데이터(ID, 제목, 본문, 내용)를 담는 스크립트
using UnityEngine;
using System;
using System.Collections.Generic;

public class MemoData : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("메모의 고유 ID")]
    public string id;

    [Header("Text")]
    [Tooltip("메모의 제목")]
    public string title;

    [Tooltip("메모의 본문")]
    public string body;

    [Tooltip("메모의 내용 (본문과 동일)")]
    public string content; // 호환성 유지를 위함

    [Header("Location")]
    [Tooltip("메모 위치 정보 (사용자 입력)")]
    public string location; // 사용자가 입력하는 위치 정보

    // ==========  아카이빙 시스템 추가 ==========

    [Header("Archive System")]
    [Tooltip("메모의 현재 상태")]
    public MemoStatus status = MemoStatus.Active;

    [Header("Timestamps")]
    [Tooltip("메모 생성 시간")]
    public string createdAt; // DateTime을 string으로 저장 (JSON 직렬화)

    [Tooltip("메모 수정 시간")]
    public string updatedAt;
    
    [Tooltip("메모 마감일 (사용자가 달력에서 선택한 날짜)")]
    public string dueDate; // 달력에서 선택한 날짜
    
    [Tooltip("메모 마감 시간 (사용자가 선택한 시간, HH:mm 형식)")]
    public string dueTime; // 시간 선택기에서 선택한 시간
    
    [Tooltip("메모 긴급도 (0=선택안함, 1=첫번째, 2=두번째, 3=세번째)")]
    public int emergencyLevel = 0; // 긴급도 버튼 인덱스 (0=미선택, 1~3=선택됨)

    [Tooltip("메모 보관 시간")]
    public string archivedAt;

    [Tooltip("메모 완료 시간")]
    public string completedAt;

    [Header("Archive Info")]
    [Tooltip("보관 사유")]
    public string archiveReason;

    [Tooltip("메모 버전 (수정 횟수)")]
    public int version = 1;

    [Header("Assignment (기존 기능)")]
    [Tooltip("메모 지정자 (담당자)")]
    public string assignee;

    [Tooltip("AssigneeRow Toggle 체크 상태")]
    public bool isAssigned;


    // ==========  추가 기능 (선택사항) ==========

    [Header("Optional Features")]
    [Tooltip("메모 우선순위")]
    public MemoPriority priority = MemoPriority.Normal;

    [Tooltip("메모 카테고리")]
    public string category;

    [Tooltip("메모 태그들")]
    public List<string> tags = new List<string>();

    [Header("Image Memo")]
    [Tooltip("첨부된 이미지 경로 목록 (최대 3개)")]
    public List<string> imagePaths = new List<string>();
    
    [Tooltip("메모 타입 (text, image 등)")]
    public string memoType = "text";  // "text" 또는 "image"
    
    [Header("Voice Memo")]
    [Tooltip("녹음된 오디오 파일 경로 목록 (최대 3개)")]
    public List<string> voiceRecordingPaths = new List<string>();

    // ========== 헬퍼 프로퍼티 ==========

    /// <summary>생성 시간을 DateTime으로 반환</summary>
    public DateTime CreatedAtDateTime
    {
        get
        {
            if (DateTime.TryParse(createdAt, out DateTime dt))
                return dt;
            return DateTime.MinValue;
        }
        set => createdAt = value.ToString("yyyy-MM-dd HH:mm:ss");
    }

    /// <summary>수정 시간을 DateTime으로 반환</summary>
    public DateTime UpdatedAtDateTime
    {
        get
        {
            if (DateTime.TryParse(updatedAt, out DateTime dt))
                return dt;
            return DateTime.MinValue;
        }
        set => updatedAt = value.ToString("yyyy-MM-dd HH:mm:ss");
    }
    
    /// <summary>마감일을 DateTime으로 반환 (nullable)</summary>
    public DateTime? DueDateDateTime
    {
        get
        {
            if (string.IsNullOrEmpty(dueDate))
                return null;
            if (DateTime.TryParse(dueDate, out DateTime dt))
                return dt;
            return null;
        }
        set => dueDate = value?.ToString("yyyy-MM-dd") ?? "";
    }

    /// <summary>보관 시간을 DateTime으로 반환 (nullable)</summary>
    public DateTime? ArchivedAtDateTime
    {
        get
        {
            if (string.IsNullOrEmpty(archivedAt))
                return null;
            if (DateTime.TryParse(archivedAt, out DateTime dt))
                return dt;
            return null;
        }
        set => archivedAt = value?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
    }

    /// <summary>완료 시간을 DateTime으로 반환 (nullable)</summary>
    public DateTime? CompletedAtDateTime
    {
        get
        {
            if (string.IsNullOrEmpty(completedAt))
                return null;
            if (DateTime.TryParse(completedAt, out DateTime dt))
                return dt;
            return null;
        }
        set => completedAt = value?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
    }

    // ========== 상태 체크 헬퍼 ==========

    /// <summary>메모가 활성 상태인지</summary>
    public bool IsActive => status == MemoStatus.Active;

    /// <summary>메모가 완료된 상태인지</summary>
    public bool IsCompleted => status == MemoStatus.Completed;

    /// <summary>메모가 보관된 상태인지</summary>
    public bool IsArchived => status == MemoStatus.Archived;

    /// <summary>메모가 삭제된 상태인지</summary>
    public bool IsDeleted => status == MemoStatus.Deleted;

    // ========== Unity 이벤트 ==========

    private void Awake()
    {
        // 생성 시간이 없으면 현재 시간으로 초기화
        if (string.IsNullOrEmpty(createdAt))
            CreatedAtDateTime = DateTime.Now;

        if (string.IsNullOrEmpty(updatedAt))
            UpdatedAtDateTime = DateTime.Now;
    }

    // 본문과 내용 동기화 함수 (기존 기능 유지)
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(body) && !string.IsNullOrEmpty(content))
            body = content;
        else if (string.IsNullOrEmpty(content) && !string.IsNullOrEmpty(body))
            content = body;
    }

    // ========== 상태 변경 메서드 ==========

    /// <summary>메모를 완료 상태로 변경</summary>
    public void MarkAsCompleted()
    {
        status = MemoStatus.Completed;
        CompletedAtDateTime = DateTime.Now;
        UpdatedAtDateTime = DateTime.Now;

        Debug.Log($"[MemoData] '{title}' 완료 처리됨");
    }

    /// <summary>메모를 보관 상태로 변경</summary>
    public void Archive(string reason = "")
    {
        status = MemoStatus.Archived;
        ArchivedAtDateTime = DateTime.Now;
        UpdatedAtDateTime = DateTime.Now;
        archiveReason = reason;

        Debug.Log($"[MemoData] '{title}' 보관됨 (사유: {reason})");
    }

    /// <summary>메모를 활성 상태로 복원</summary>
    public void Restore()
    {
        status = MemoStatus.Active;
        UpdatedAtDateTime = DateTime.Now;
        archivedAt = "";
        archiveReason = "";

        Debug.Log($"[MemoData] '{title}' 복원됨");
    }

    /// <summary>메모를 소프트 삭제 (실제로는 삭제 안 함)</summary>
    public void SoftDelete()
    {
        status = MemoStatus.Deleted;
        UpdatedAtDateTime = DateTime.Now;

        Debug.Log($"[MemoData] '{title}' 소프트 삭제됨");
    }

    /// <summary>메모 내용 업데이트 (버전 증가)</summary>
    public void UpdateContent(string newTitle, string newBody)
    {
        title = newTitle;
        body = newBody;
        content = newBody;

        version++;
        UpdatedAtDateTime = DateTime.Now;

        Debug.Log($"[MemoData] '{title}' 업데이트됨 (v{version})");
    }

    // ========== 디버그용 ==========

    /// <summary>메모 정보를 문자열로 반환</summary>
    public override string ToString()
    {
        return $"[{status}] {title} (ID: {id}, v{version})";
    }
}

// ========== 열거형 정의 ==========

/// <summary>메모 상태</summary>
public enum MemoStatus
{
    Active,      // 활성 (현재 사용 중)
    Completed,   // 해결 완료
    Archived,    // 보관됨
    Deleted      // 삭제됨 (소프트 삭제)
}

/// <summary>메모 우선순위</summary>
public enum MemoPriority
{
    Low,         // 낮음
    Normal,      // 보통
    High,        // 높음
    Urgent       // 긴급
}