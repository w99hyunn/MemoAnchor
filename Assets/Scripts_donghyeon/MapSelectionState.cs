
// 씬이 바뀌어도 유지돼야 하는 선택된 맵 정보를 담아두는 ScriptableObject
using UnityEngine;

// 에셋 생성 메뉴에 등록
[CreateAssetMenu(menuName = "App/State/Map Selection State", fileName = "MapSelectionState")]
public class MapSelectionState : ScriptableObject
{
    [Header("Save Map Data")]
    [Tooltip("선택된 맵의 ID가 들어갈 자리")]
    [SerializeField] private string selectedMapId;

    [Tooltip("선택된 맵의 이름이 들어갈 자리")]
    [SerializeField] private string selectedMapName;

    [Tooltip("다운로드 받은 맵 파일이 어디에 저장됐는지 경로")]
    [SerializeField] private string downloadedMapFilePath;

    // 선택된 맵 ID 프로퍼티
    public string SelectedMapId
    {
        get => selectedMapId;
        set => selectedMapId = value;
    }

    // 선택된 맵 이름 프로퍼티
    public string SelectedMapName
    {
        get => selectedMapName;
        set => selectedMapName = value;
    }

    // 선택된 맵 파일 경로 프로퍼티
    public string DownloadedMapFilePath
    {
        get => downloadedMapFilePath;
        set => downloadedMapFilePath = value;
    }
}
