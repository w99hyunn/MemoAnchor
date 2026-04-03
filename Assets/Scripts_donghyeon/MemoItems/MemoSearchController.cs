using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


// 메모 제목 검색 기능을 제공하는 컨트롤러
// MemoListManager와 연동하여 실시간 검색 필터링 및 검색 기록 관리
public class MemoSearchController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button searchButton;
    [SerializeField] private GameObject searchPanel;
    [SerializeField] private TMP_InputField searchInputField;
    [SerializeField] private Button closeSearchButton;

    [Header("Search History")]
    [SerializeField] private GameObject recentSearchContainer;
    [SerializeField] private Transform searchHistoryContent;
    [SerializeField] private GameObject recentMemoPrefab;  // 검색 기록용 프리팹

    [Header("Search Results")]
    [SerializeField] private GameObject searchResultsContainer;  // 검색 결과 컨테이너
    [SerializeField] private Transform searchResultsContent;     // 검색 결과 Content
    [SerializeField] private GameObject researchMemoPrefab;      // 검색 결과용 프리팹

    [Header("Manager Reference")]
    [SerializeField] private MemoListManager memoListManager;
    [SerializeField] private Transform memoContentRoot;  // MemoListManager의 Content Root 직접 참조
    [SerializeField] private string constructionSceneName = "ConstructionVPS";  // 이동할 씬 이름

    private const int MAX_SEARCH_HISTORY = 5;
    private const string SEARCH_HISTORY_KEY = "MemoSearchHistory";

    private bool isSearchPanelOpen = false;
    private List<SearchHistoryItem> searchHistory = new List<SearchHistoryItem>();

    // 검색 기록 아이템 클래스 (날짜 포함)
    [Serializable]
    private class SearchHistoryItem
    {
        public string query;
        public string dateTime;  // "MM.dd" 형식
    }

    private void Start()
    {
        Debug.Log("[MemoSearchController] Start() 시작");

        // 검색 기록 로드
        LoadSearchHistory();

        // 초기 상태: 검색 패널 숨김
        if (searchPanel != null)
        {
            searchPanel.SetActive(false);
            Debug.Log("[MemoSearchController] SearchPanel 초기 비활성화");
        }
        else
        {
            Debug.LogError("[MemoSearchController] SearchPanel이 null입니다!");
        }

        // 버튼 이벤트 연결
        if (searchButton != null)
        {
            searchButton.onClick.AddListener(ToggleSearchPanel);
            Debug.Log("[MemoSearchController] SearchButton 이벤트 연결 완료");
        }
        else
        {
            Debug.LogError("[MemoSearchController] SearchButton이 null입니다!");
        }

        if (closeSearchButton != null)
        {
            closeSearchButton.onClick.AddListener(CloseSearchPanel);
            Debug.Log("[MemoSearchController] CloseSearchButton 이벤트 연결 완료");
        }
        else
        {
            Debug.LogError("[MemoSearchController] CloseSearchButton이 null입니다!");
        }

        // 입력 필드 이벤트 연결 (실시간 검색)
        if (searchInputField != null)
        {
            searchInputField.onValueChanged.AddListener(OnSearchTextChanged);
            searchInputField.onEndEdit.AddListener(OnSearchSubmit);
            Debug.Log("[MemoSearchController] SearchInputField 이벤트 연결 완료");
        }
        else
        {
            Debug.LogError("[MemoSearchController] SearchInputField가 null입니다!");
        }

        // UI 컴포넌트 확인
        Debug.Log($"[MemoSearchController] recentSearchContainer: {(recentSearchContainer != null ? "OK" : "NULL")}");
        Debug.Log($"[MemoSearchController] searchHistoryContent: {(searchHistoryContent != null ? "OK" : "NULL")}");
        Debug.Log($"[MemoSearchController] recentMemoPrefab: {(recentMemoPrefab != null ? "OK" : "NULL")}");
        Debug.Log($"[MemoSearchController] searchResultsContainer: {(searchResultsContainer != null ? "OK" : "NULL")}");
        Debug.Log($"[MemoSearchController] searchResultsContent: {(searchResultsContent != null ? "OK" : "NULL")}");
        Debug.Log($"[MemoSearchController] researchMemoPrefab: {(researchMemoPrefab != null ? "OK" : "NULL")}");
        Debug.Log($"[MemoSearchController] memoListManager: {(memoListManager != null ? "OK" : "NULL")}");
        Debug.Log($"[MemoSearchController] memoContentRoot: {(memoContentRoot != null ? "OK" : "NULL")}");
        Debug.Log($"[MemoSearchController] 로드된 검색 기록 개수: {searchHistory.Count}");

        // 검색 결과 컨테이너 초기 숨김
        if (searchResultsContainer != null)
            searchResultsContainer.SetActive(false);
    }


    // 검색 패널 열기/닫기 토글
    private void ToggleSearchPanel()
    {
        isSearchPanelOpen = !isSearchPanelOpen;

        if (searchPanel != null)
        {
            searchPanel.SetActive(isSearchPanelOpen);

            // 패널이 열리면 입력 필드에 포커스 및 검색 기록 표시
            if (isSearchPanelOpen)
            {
                if (searchInputField != null)
                {
                    searchInputField.text = "";
                    searchInputField.Select();
                    searchInputField.ActivateInputField();
                }

                // 검색 기록 표시
                ShowSearchHistory();
            }
        }

        Debug.Log($"[MemoSearchController] Search panel {(isSearchPanelOpen ? "opened" : "closed")}");
    }


    // 검색 패널 닫기
    private void CloseSearchPanel()
    {
        isSearchPanelOpen = false;

        if (searchPanel != null)
            searchPanel.SetActive(false);

        // 검색어 초기화
        if (searchInputField != null)
        {
            searchInputField.text = "";
            OnSearchTextChanged("");  // 필터 해제
        }
    }


    // 검색어 변경 시 호출 (실시간 검색)
    private void OnSearchTextChanged(string searchText)
    {
        Debug.Log($"[MemoSearchController] Search text: '{searchText}'");

        if (string.IsNullOrWhiteSpace(searchText))
        {
            // 검색어가 없으면 검색 기록 표시, 검색 결과 숨김
            if (recentSearchContainer != null)
                recentSearchContainer.SetActive(true);
            if (searchResultsContainer != null)
                searchResultsContainer.SetActive(false);

            ShowSearchHistory();
        }
        else
        {
            // 검색어가 있으면 검색 기록 숨김, 검색 결과 표시
            if (recentSearchContainer != null)
                recentSearchContainer.SetActive(false);
            if (searchResultsContainer != null)
                searchResultsContainer.SetActive(true);

            FilterAndShowMemoItems(searchText);
        }
    }

    // 검색 제출 시 호출 (Enter 키 또는 완료)
    private void OnSearchSubmit(string searchText)
    {
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            // 검색 기록에 추가
            AddToSearchHistory(searchText);
            Debug.Log($">>> [MemoSearchController] 검색 실행 및 기록 저장: '{searchText}'");
        }
    }


    // 검색어로 메모 찾아서 검색 결과 컨테이너에 표시
    // JSON 파일에서 직접 메모를 검색하여 표시
    private void FilterAndShowMemoItems(string searchText)
    {
        if (searchResultsContent == null)
        {
            Debug.LogError(">>> [MemoSearchController] ❌ searchResultsContent가 null입니다!");
            return;
        }

        if (researchMemoPrefab == null)
        {
            Debug.LogError(">>> [MemoSearchController] ❌ researchMemoPrefab이 null입니다!");
            return;
        }

        Debug.Log($">>> [MemoSearchController] 검색 시작: searchText='{searchText}'");

        // 기존 검색 결과 제거 (즉시 삭제)
        for (int i = searchResultsContent.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(searchResultsContent.GetChild(i).gameObject);
        }

        int matchCount = 0;
        string lowerSearchText = searchText.ToLower();
        List<MemoSearchResult> results = new List<MemoSearchResult>();

        // JSON 파일에서 직접 메모 검색
        SearchMemosFromJsonFiles(lowerSearchText, results);

        Debug.Log($">>> [MemoSearchController] 검색 결과: {results.Count}개");

        // 검색 결과를 ResearchMemoPrefab으로 표시
        foreach (var result in results)
        {
            GameObject resultItem = Instantiate(researchMemoPrefab, searchResultsContent);

            // MemoTitleText 설정 (Header 하위에 있음)
            TMP_Text titleText = resultItem.transform.Find("Header/MemoTitleText")?.GetComponent<TMP_Text>();
            if (titleText == null)
                titleText = resultItem.transform.Find("MemoTitleText")?.GetComponent<TMP_Text>();
            if (titleText != null)
            {
                titleText.text = result.title;
                Debug.Log($">>> [MemoSearchController] ✓ 검색 결과 표시: '{result.title}'");
            }

            // Button 이벤트 - ConstructionVPS 씬으로 이동
            Button btn = resultItem.GetComponent<Button>();
            if (btn != null)
            {
                int capturedMapId = result.mapId;
                string capturedMapName = result.mapName;
                string capturedMemoId = result.memoId;

                btn.onClick.AddListener(() =>
                {
                    Debug.Log($">>> [MemoSearchController] 검색 결과 탭: memoId={capturedMemoId}, mapId={capturedMapId}");
                    LoadSceneWithMapAndMemo(capturedMapId, capturedMapName, capturedMemoId);
                });
            }

            matchCount++;
        }

        Debug.Log($">>> [MemoSearchController] ✅ 검색 완료: {matchCount}개 매칭됨 (검색어: '{searchText}')");
    }

    // JSON 파일들에서 메모 검색
    private void SearchMemosFromJsonFiles(string lowerSearchText, List<MemoSearchResult> results)
    {
        string persistentPath = Application.persistentDataPath;

        // immersal_pins_*.json 파일들 찾기
        string[] jsonFiles;
        try
        {
            jsonFiles = Directory.GetFiles(persistentPath, "immersal_pins_*.json");
        }
        catch (Exception e)
        {
            Debug.LogError($">>> [MemoSearchController] 파일 검색 실패: {e.Message}");
            return;
        }

        Debug.Log($">>> [MemoSearchController] 발견된 메모 파일 수: {jsonFiles.Length}");

        foreach (string filePath in jsonFiles)
        {
            try
            {
                // 파일 이름에서 맵 ID 추출 (immersal_pins_1234.json -> 1234)
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                string mapIdStr = fileName.Replace("immersal_pins_", "");

                if (!int.TryParse(mapIdStr, out int mapId))
                {
                    Debug.LogWarning($">>> [MemoSearchController] 맵 ID 파싱 실패: {fileName}");
                    continue;
                }

                // JSON 파일 읽기
                string json = File.ReadAllText(filePath);
                var pinDB = JsonUtility.FromJson<PinDatabase>(json);

                if (pinDB == null || pinDB.pins == null)
                {
                    continue;
                }

                // 맵 이름 찾기 (memoContentRoot에서 MapItem 찾기)
                string mapName = FindMapNameFromUI(mapId);

                // 각 핀에서 검색어와 매칭되는 것 찾기
                foreach (var pin in pinDB.pins)
                {
                    if (string.IsNullOrEmpty(pin.title))
                        continue;

                    if (pin.title.ToLower().Contains(lowerSearchText))
                    {
                        results.Add(new MemoSearchResult
                        {
                            memoId = pin.id,
                            title = pin.title,
                            mapId = mapId,
                            mapName = mapName
                        });
                        Debug.Log($">>> [MemoSearchController] 매칭된 메모: '{pin.title}', mapId={mapId}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($">>> [MemoSearchController] 파일 읽기 실패: {filePath}, 오류: {e.Message}");
            }
        }
    }

    // UI에서 맵 이름 찾기
    private string FindMapNameFromUI(int mapId)
    {
        if (memoContentRoot == null)
            return "";

        foreach (Transform child in memoContentRoot)
        {
            if (child.name.StartsWith($"MapItem_{mapId}_"))
            {
                string[] parts = child.name.Split('_');
                if (parts.Length >= 3)
                {
                    return string.Join("_", parts, 2, parts.Length - 2);
                }
            }
        }
        return "";
    }

    // JSON 데이터 구조
    [Serializable]
    private class PinDatabase
    {
        public int mapId;
        public PinData[] pins;
    }

    [Serializable]
    private class PinData
    {
        public string id;
        public string title;
        public string body;
        public string location;
        public float localPosX;
        public float localPosY;
        public float localPosZ;
        public bool isAssigned;
    }

    // 맵과 메모 정보를 저장하고 ConstructionVPS 씬으로 이동
    private void LoadSceneWithMapAndMemo(int mapId, string mapName, string memoId)
    {
        // 맵 정보 저장
        PlayerPrefs.SetInt("IMMERSAL_SELECTED_MAP_ID", mapId);
        PlayerPrefs.SetString("IMMERSAL_SELECTED_MAP_NAME", mapName ?? "");

        // 메모 ID 저장 (ConstructionVPS 씬에서 해당 메모를 자동으로 선택하도록)
        PlayerPrefs.SetString("SELECTED_MEMO_ID", memoId);
        PlayerPrefs.Save();

        Debug.Log($">>> [MemoSearchController] ✅ 씬 이동: mapId={mapId}, mapName={mapName}, memoId={memoId}");
        Debug.Log($">>> [MemoSearchController] Loading scene: {constructionSceneName}");

        // 씬 이동
        SceneTransitionFade.LoadScene(constructionSceneName);
    }


    private void OnDestroy()
    {
        // 메모리 누수 방지
        if (searchButton != null)
            searchButton.onClick.RemoveListener(ToggleSearchPanel);

        if (closeSearchButton != null)
            closeSearchButton.onClick.RemoveListener(CloseSearchPanel);

        if (searchInputField != null)
        {
            searchInputField.onValueChanged.RemoveListener(OnSearchTextChanged);
            searchInputField.onEndEdit.RemoveListener(OnSearchSubmit);
        }
    }

    // ===== 검색 기록 관리 =====

    // 검색 기록 로드 (PlayerPrefs에서)
    private void LoadSearchHistory()
    {
        string json = PlayerPrefs.GetString(SEARCH_HISTORY_KEY, "");
        Debug.Log($">>> [MemoSearchController] LoadSearchHistory() - JSON: {json}");

        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var wrapper = JsonUtility.FromJson<SearchHistoryWrapper>(json);
                if (wrapper != null && wrapper.items != null)
                {
                    searchHistory = new List<SearchHistoryItem>(wrapper.items);
                    Debug.Log($">>> [MemoSearchController] 검색 기록 로드 완료: {searchHistory.Count}개");
                    foreach (var item in searchHistory)
                    {
                        Debug.Log($">>> [MemoSearchController]   - '{item.query}' ({item.dateTime})");
                    }
                }
                else
                {
                    Debug.LogWarning(">>> [MemoSearchController] JSON 파싱 결과가 null이거나 items가 없습니다.");
                    searchHistory = new List<SearchHistoryItem>();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($">>> [MemoSearchController] 검색 기록 로드 실패: {e.Message}");
                searchHistory = new List<SearchHistoryItem>();
            }
        }
        else
        {
            Debug.Log(">>> [MemoSearchController] 저장된 검색 기록이 없습니다. 테스트 데이터 추가 중...");
            // 테스트용 검색 기록 추가
            searchHistory = new List<SearchHistoryItem>
            {
                new SearchHistoryItem { query = "메모 1", dateTime = "01.12" },
                new SearchHistoryItem { query = "메모 4", dateTime = "01.08" }
            };
            SaveSearchHistory();
            Debug.Log(">>> [MemoSearchController] 테스트 검색 기록 2개 추가 완료");
        }
    }

    // 검색 기록 저장
    private void SaveSearchHistory()
    {
        try
        {
            var wrapper = new SearchHistoryWrapper { items = searchHistory.ToArray() };
            string json = JsonUtility.ToJson(wrapper);
            PlayerPrefs.SetString(SEARCH_HISTORY_KEY, json);
            PlayerPrefs.Save();
            Debug.Log($">>> [MemoSearchController] 검색 기록 저장 완료: {searchHistory.Count}개");
        }
        catch (Exception e)
        {
            Debug.LogError($">>> [MemoSearchController] 검색 기록 저장 실패: {e.Message}");
        }
    }

    // 검색 기록 추가
    private void AddToSearchHistory(string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText)) return;

        // 현재 날짜 (MM.dd 형식)
        string currentDate = DateTime.Now.ToString("MM.dd");

        // 중복 제거 (이미 있으면 제거 후 맨 앞에 추가)
        searchHistory.RemoveAll(item => item.query == searchText);

        // 맨 앞에 추가
        searchHistory.Insert(0, new SearchHistoryItem
        {
            query = searchText,
            dateTime = currentDate
        });

        // 최대 5개로 제한
        if (searchHistory.Count > MAX_SEARCH_HISTORY)
        {
            searchHistory.RemoveRange(MAX_SEARCH_HISTORY,
                searchHistory.Count - MAX_SEARCH_HISTORY);
        }

        SaveSearchHistory();
        Debug.Log($">>> [MemoSearchController] 검색 기록 추가: '{searchText}' ({currentDate})");
    }

    // 검색 기록 UI 표시
    private void ShowSearchHistory()
    {
        Debug.Log(">>> [MemoSearchController] ShowSearchHistory() 호출");

        if (recentSearchContainer == null)
        {
            Debug.LogError(">>> [MemoSearchController] recentSearchContainer가 null입니다!");
            return;
        }

        if (searchHistoryContent == null)
        {
            Debug.LogError(">>> [MemoSearchController] searchHistoryContent가 null입니다!");
            return;
        }

        if (recentMemoPrefab == null)
        {
            Debug.LogError(">>> [MemoSearchController] recentMemoPrefab이 null입니다!");
            return;
        }

        Debug.Log($">>> [MemoSearchController] 검색 기록 개수: {searchHistory.Count}");

        // 기존 항목 제거 (즉시 삭제)
        int childCount = searchHistoryContent.childCount;
        for (int i = childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(searchHistoryContent.GetChild(i).gameObject);
        }
        Debug.Log($">>> [MemoSearchController] 기존 항목 {childCount}개 제거");

        // 검색 기록이 없으면 컨테이너 숨김
        if (searchHistory.Count == 0)
        {
            recentSearchContainer.SetActive(false);
            Debug.Log(">>> [MemoSearchController] 검색 기록이 없어서 컨테이너 숨김");
            return;
        }

        // 검색 기록 생성
        Debug.Log($">>> [MemoSearchController] {searchHistory.Count}개 검색 기록 항목 생성 시작");
        foreach (var historyItem in searchHistory)
        {
            Debug.Log($">>> [MemoSearchController] 검색 기록 항목 생성: '{historyItem.query}' ({historyItem.dateTime})");
            GameObject item = Instantiate(recentMemoPrefab, searchHistoryContent);

            if (item == null)
            {
                Debug.LogError(">>> [MemoSearchController] 항목 생성 실패!");
                continue;
            }

            // MemoTitleText 설정 (Header 하위에 있음)
            TMP_Text titleText = item.transform.Find("Header/MemoTitleText")?.GetComponent<TMP_Text>();
            if (titleText == null)
                titleText = item.transform.Find("MemoTitleText")?.GetComponent<TMP_Text>();
            if (titleText != null)
            {
                titleText.text = historyItem.query;
                Debug.Log($">>> [MemoSearchController] MemoTitleText 설정 완료: '{historyItem.query}'");
            }
            else
            {
                Debug.LogWarning(">>> [MemoSearchController] MemoTitleText를 찾을 수 없습니다!");
            }

            // TimeText 설정 (Header 하위에 있음)
            TMP_Text timeText = item.transform.Find("Header/TimeText")?.GetComponent<TMP_Text>();
            if (timeText == null)
                timeText = item.transform.Find("TimeText")?.GetComponent<TMP_Text>();
            if (timeText != null)
            {
                timeText.text = historyItem.dateTime;
                Debug.Log($">>> [MemoSearchController] TimeText 설정 완료: '{historyItem.dateTime}'");
            }
            else
            {
                Debug.LogWarning(">>> [MemoSearchController] TimeText를 찾을 수 없습니다!");
            }

            // DeleteIcon 버튼 (X 버튼, Header 하위에 있음)
            Button deleteBtn = item.transform.Find("Header/DeleteIcon")?.GetComponent<Button>();
            if (deleteBtn == null)
                deleteBtn = item.transform.Find("DeleteIcon")?.GetComponent<Button>();
            if (deleteBtn != null)
            {
                string capturedQuery = historyItem.query;
                deleteBtn.onClick.AddListener(() =>
                {
                    Debug.Log($">>> [MemoSearchController] 검색 기록 삭제 버튼 클릭: '{capturedQuery}'");
                    RemoveFromSearchHistory(capturedQuery);
                });
            }
            else
            {
                Debug.LogWarning(">>> [MemoSearchController] DeleteIcon 버튼을 찾을 수 없습니다!");
            }
        }

        recentSearchContainer.SetActive(true);
        Debug.Log($">>> [MemoSearchController] ✅ 검색 기록 표시 완료: {searchHistory.Count}개, 컨테이너 활성화");
    }

    // 검색 기록 삭제
    private void RemoveFromSearchHistory(string searchQuery)
    {
        searchHistory.RemoveAll(item => item.query == searchQuery);
        SaveSearchHistory();
        ShowSearchHistory();
        Debug.Log($">>> [MemoSearchController] 검색 기록 삭제: '{searchQuery}'");
    }

    // 검색 기록 데이터 래퍼 클래스
    [Serializable]
    private class SearchHistoryWrapper
    {
        public SearchHistoryItem[] items;
    }

    // 검색 결과 클래스
    private class MemoSearchResult
    {
        public string memoId;
        public string title;
        public int mapId;
        public string mapName;
    }
}