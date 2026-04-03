
// 메모 추가 버튼 컴포넌트
// 각 맵 아이템의 가장 하위에 배치되어 ConstructionVPS 씬으로 이동
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class AddMemoButton : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Button button;

    [Header("Layout Settings")]
    [Tooltip("아이템의 최소 높이")]
    [SerializeField] private float minHeight = 138f;

    // 현재 맵 정보
    private int mapId;
    private string mapName;

    private void Awake()
    {
        // 높이 설정 (Instantiate 직후에 호출되도록 Awake에서 실행)
        SetupRectTransform();
    }

    private void Start()
    {
        // 버튼이 할당되지 않았으면 자동으로 찾기
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        // 버튼이 없으면 에러 출력
        if (button == null)
        {
            Debug.LogError("[AddMemoButton] Button component is missing!");
        }
    }

    // RectTransform 크기를 자동으로 설정
    private void SetupRectTransform()
    {
        RectTransform rectTransform = GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, minHeight);
        }

        // LayoutElement 추가하여 높이 보장
        LayoutElement layoutElement = GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = gameObject.AddComponent<LayoutElement>();
        }
        layoutElement.minHeight = minHeight;
        layoutElement.preferredHeight = minHeight;
    }

    // 맵 정보 설정
    public void Setup(int id, string name)
    {
        mapId = id;
        mapName = name;

        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnAddMemoClicked);
        }
        else
        {
            Debug.LogError("[AddMemoButton] Button reference is missing.");
        }
    }

    // 메모 추가 버튼 클릭 시 실행
    private void OnAddMemoClicked()
    {
        // 맵 정보를 PlayerPrefs에 저장
        PlayerPrefs.SetInt("IMMERSAL_SELECTED_MAP_ID", mapId);
        PlayerPrefs.SetString("IMMERSAL_SELECTED_MAP_NAME", mapName);
        PlayerPrefs.Save();

        Debug.Log($"[AddMemoButton] Moving to ConstructionVPS for map {mapId} ({mapName})");

        // ConstructionVPS 씬으로 이동 (이전 씬 자동 저장됨)
        SceneTransitionFade.LoadScene("ConstructionVPS");
    }
}
