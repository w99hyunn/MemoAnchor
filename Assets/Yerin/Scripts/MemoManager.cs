// Assets/Scripts/MemoManager.cs
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class MemoManager : MonoBehaviour
{
    public static MemoManager Instance;
    public List<MemoData2> memos = new List<MemoData2>();  // MemoData → MemoData2
    public GameObject memoPrefab;
    public Transform arSpace;
    private string saveFilePath;

    void Awake()
    {
        // 싱글톤
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        saveFilePath = Application.persistentDataPath + "/memos.json";
        Debug.Log("Save path: " + saveFilePath);
    }

    void Start()
    {
        LoadMemos();
        SpawnAllMemos();
    }

    // 메모 추가
    public void AddMemo(string content, Vector3 localPos, Quaternion localRot)
    {
        MemoData2 newMemo = new MemoData2(content, localPos, localRot);  // MemoData → MemoData2
        memos.Add(newMemo);
        SaveMemos();

        // 즉시 생성
        SpawnMemo(newMemo);
        Debug.Log($"메모 추가됨: {content}");
    }

    // 메모 삭제
    public void DeleteMemo(string id)
    {
        memos.RemoveAll(m => m.id == id);
        SaveMemos();
        Debug.Log($"메모 삭제됨: {id}");
    }

    // 저장 (JSON)
    public void SaveMemos()
    {
        MemoList2 list = new MemoList2 { memos = memos.ToArray() };  // MemoList → MemoList2
        string json = JsonUtility.ToJson(list, true);
        File.WriteAllText(saveFilePath, json);
        Debug.Log($"메모 저장 완료: {memos.Count}개");
    }

    // 불러오기
    public void LoadMemos()
    {
        if (File.Exists(saveFilePath))
        {
            string json = File.ReadAllText(saveFilePath);
            MemoList2 list = JsonUtility.FromJson<MemoList2>(json);  // MemoList → MemoList2
            memos = new List<MemoData2>(list.memos);  // MemoData → MemoData2
            Debug.Log($"메모 로드 완료: {memos.Count}개");
        }
        else
        {
            Debug.Log("저장된 메모 없음");
        }
    }

    // 모든 메모 생성
    void SpawnAllMemos()
    {
        foreach (var memo in memos)
        {
            SpawnMemo(memo);
        }
    }

    // 메모 오브젝트 생성
    void SpawnMemo(MemoData2 data)  // MemoData → MemoData2
    {
        if (memoPrefab == null || arSpace == null)
        {
            Debug.LogWarning("MemoPrefab 또는 ARSpace가 없음!");
            return;
        }

        GameObject memoObj = Instantiate(memoPrefab, arSpace);
        memoObj.name = $"Memo_{data.id.Substring(0, 8)}";
        memoObj.transform.localPosition = data.position;
        memoObj.transform.localRotation = data.rotation;

        // MemoObject 컴포넌트에 데이터 전달 
        MemoObject memoScript = memoObj.GetComponent<MemoObject>();
        if (memoScript != null)
            memoScript.SetData(data);
    }

    // 날짜별 필터링 (히스토리용)
    public List<MemoData2> GetMemosByDate(string date)  // MemoData → MemoData2
    {
        return memos.Where(m => m.timestamp.StartsWith(date)).ToList();
    }

    // 작성자별 필터링
    public List<MemoData2> GetMemosByAuthor(string author)  // MemoData → MemoData2
    {
        return memos.Where(m => m.author == author).ToList();
    }
}