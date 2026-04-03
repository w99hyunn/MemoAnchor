
using UnityEngine;
using System;

[System.Serializable]
public class MemoData2
{
    public string id;               // 고유 ID
    public string content;          // 메모 내용
    public Vector3 position;        // AR 공간 좌표
    public Quaternion rotation;     // 회전
    public string timestamp;        // 작성 시간
    public string author;           // 작성자
    public string category;         // 카테고리 (예: "배관", "전기", "안전")

    public MemoData2(string content, Vector3 pos, Quaternion rot, string author = "User")
    {
        this.id = System.Guid.NewGuid().ToString();
        this.content = content;
        this.position = pos;
        this.rotation = rot;
        this.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        this.author = author;
        this.category = "일반";
    }
}

[System.Serializable]
public class MemoList2  
{
    public MemoData2[] memos;  // MemoData → MemoData2
}
