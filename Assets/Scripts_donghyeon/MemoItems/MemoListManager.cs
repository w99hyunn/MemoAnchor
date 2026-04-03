
// MemoList 씬에서 맵 리스트와 각 맵의 메모 리스트를 3단계 계층으로 표시하는 매니저
// 계층 구조: 맵 이름 -> 메모 위치 -> 메모 타이틀
// Immersal 서버에서 맵 목록을 받아와서 리스트로 생성하고,
// 각 맵을 클릭하면 해당 맵에 저장된 메모 위치들을 표시하고,
// 위치를 클릭하면 해당 위치의 메모 타이틀들을 표시
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class MemoListManager : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("맵 리스트 프리팹이 생성되어 붙을 부모 Transform을 넣는 자리")]
    [SerializeField] private Transform contentRoot;

    [Tooltip("맵 리스트 프리팹 GameObject를 넣는 자리 (MapListItemUI 컴포넌트가 있어야 함)")]
    [SerializeField] private GameObject mapItemPrefab;

    [Tooltip("메모 위치 프리팹 GameObject를 넣는 자리 (MemoLocationUI 컴포넌트가 있어야 함)")]
    [SerializeField] private GameObject locationItemPrefab;

    [Tooltip("메모 타이틀 프리팹 GameObject를 넣는 자리 (MemoItemUI 컴포넌트가 있어야 함)")]
    [SerializeField] private GameObject memoItemPrefab;

    [Tooltip("메모 추가 버튼 프리팹 GameObject를 넣는 자리 (AddMemoButton 컴포넌트가 있어야 함)")]
    [SerializeField] private GameObject addMemoButtonPrefab;

    [Tooltip("메모 위치 리스트가 생성될 들여쓰기 크기 (픽셀)")]
    [SerializeField] private float locationIndentSize = 20f;

    [Tooltip("메모 타이틀 리스트가 생성될 들여쓰기 크기 (픽셀)")]
    [SerializeField] private float memoIndentSize = 40f;

    [Header("Immersal Token")]
    [Tooltip("Immersal Developer Portal에서 발급받은 developer token을 넣는 자리")]
    [SerializeField] private string developerToken = "";

    [Header("Immersal Map API")]
    [Tooltip("유니티로 가져올 맵 리스트 API 요청 주소를 넣는 자리")]
    [SerializeField] private string listEndpoint = "https://api.immersal.com/list";

    [Header("Move Scene")]
    [Tooltip("메모 클릭 시 이동할 씬의 이름을 적는 자리")]
    [SerializeField] private string constructionSceneName = "ConstructionVPS";

    [Header("Layout Settings")]
    [Tooltip("맵 아이템 좌우 여백 (픽셀)")]
    [SerializeField] private int itemPaddingLeft = 20;
    [SerializeField] private int itemPaddingRight = 20;
    [Tooltip("맵 아이템 상하 여백 (픽셀)")]
    [SerializeField] private int itemPaddingTop = 20;
    [SerializeField] private int itemPaddingBottom = 20;
    [Tooltip("맵 아이템 간 간격 (픽셀)")]
    [SerializeField] private int itemSpacing = 30;

    [Header("Dev Check")]
    [Tooltip("유니티 에디터에서, 리스트 생성 동작이 잘 되는지 확인하기 위한 스위치")]
    [SerializeField] private bool useMockListInEditor = true;

    // PlayerPrefs에 저장하는 맵 ID/NAME 키
    private const string PREF_SELECTED_MAP_ID = "IMMERSAL_SELECTED_MAP_ID";
    private const string PREF_SELECTED_MAP_NAME = "IMMERSAL_SELECTED_MAP_NAME";

    // 맵별 확장 상태 추적
    private Dictionary<int, bool> mapExpanded = new Dictionary<int, bool>();
    private Dictionary<int, List<GameObject>> mapLocationItems = new Dictionary<int, List<GameObject>>();
    private Dictionary<int, MapListItemUI> mapItemUIRefs = new Dictionary<int, MapListItemUI>();
    private Dictionary<int, GameObject> mapAddMemoButtons = new Dictionary<int, GameObject>(); // AddMemoButton 추적

    // 위치별 확장 상태 추적 (mapId_locationKey 형식)
    private Dictionary<string, bool> locationExpanded = new Dictionary<string, bool>();
    private Dictionary<string, List<GameObject>> locationMemoItems = new Dictionary<string, List<GameObject>>();
    private Dictionary<string, MemoLocationUI> locationItemUIRefs = new Dictionary<string, MemoLocationUI>();
    private Dictionary<string, GameObject> locationAddMemoButtons = new Dictionary<string, GameObject>(); // LocationItem의 AddMemoButton 추적

    private void Awake()
    {
        Debug.Log($"[MemoListManager] Awake() scene={SceneManager.GetActiveScene().name}");
        Debug.Log($"[MemoListManager] listEndpoint={listEndpoint}");
        Debug.Log($"[MemoListManager] developerToken={(string.IsNullOrEmpty(developerToken) ? "EMPTY" : $"LEN={developerToken.Length}")}");
    }

    private void Start()
    {
        Debug.Log("=== [MemoListManager] Start() BEGIN ===");
        Debug.Log($"[MemoListManager] Start() platform={Application.platform}, internetReachability={Application.internetReachability}");
        Debug.Log($"[MemoListManager] contentRoot={contentRoot?.name ?? "NULL"}");

        // contentRoot에 VerticalLayoutGroup 설정
        SetupContentRoot();

        StartCoroutine(RefreshList());
        Debug.Log("=== [MemoListManager] Start() END ===");
    }

    // contentRoot에 레이아웃 컴포넌트 설정
    private void SetupContentRoot()
    {
        if (contentRoot == null)
        {
            Debug.LogError("[MemoListManager] SetupContentRoot: contentRoot is null!");
            return;
        }

        Debug.Log($"[MemoListManager] SetupContentRoot: contentRoot={contentRoot.name}");

        // VerticalLayoutGroup 추가하여 MapItemPrefab들을 수직으로 배치
        VerticalLayoutGroup verticalLayout = contentRoot.GetComponent<VerticalLayoutGroup>();
        if (verticalLayout == null)
        {
            verticalLayout = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            Debug.Log("[MemoListManager] VerticalLayoutGroup added to contentRoot");
        }
        verticalLayout.childControlHeight = false;  // false로 설정하여 각 아이템의 LayoutElement.preferredHeight 사용
        verticalLayout.childControlWidth = true;  // true로 변경
        verticalLayout.childForceExpandHeight = false;
        verticalLayout.childForceExpandWidth = true;  // true로 변경
        verticalLayout.spacing = itemSpacing;  // 맵 아이템 간의 간격
        verticalLayout.padding = new RectOffset(itemPaddingLeft, itemPaddingRight, itemPaddingTop, itemPaddingBottom);  // Inspector에서 조절 가능
        verticalLayout.childAlignment = TextAnchor.UpperCenter;

        Debug.Log($"[MemoListManager] VerticalLayoutGroup configured: spacing={verticalLayout.spacing}, padding={verticalLayout.padding}");

        // ContentSizeFitter 추가하여 전체 높이를 자식들에 맞춤
        ContentSizeFitter sizeFitter = contentRoot.GetComponent<ContentSizeFitter>();
        if (sizeFitter == null)
        {
            sizeFitter = contentRoot.gameObject.AddComponent<ContentSizeFitter>();
            Debug.Log("[MemoListManager] ContentSizeFitter added to contentRoot");
        }
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // 레이아웃 강제 갱신
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot as RectTransform);
        Debug.Log("[MemoListManager] SetupContentRoot completed");
    }

    // Reset 버튼 클릭 시 리스트 목록 갱신
    public void RefreshButton()
    {
        Debug.Log("[MemoListManager] RefreshButton() called");
        StartCoroutine(RefreshList());
    }

    // 서버에서 데이터를 받아와 화면에 리스트 생성
    private IEnumerator RefreshList()
    {
        // UI 참조 여부 확인
        if (!contentRoot)
        {
            Debug.LogError("[MemoListManager] contentRoot가 할당되지 않았습니다! Inspector에서 ScrollView의 Content를 할당하세요.");
            yield break;
        }

        if (!mapItemPrefab)
        {
            Debug.LogError("[MemoListManager] mapItemPrefab이 할당되지 않았습니다! Inspector에서 맵 리스트 프리팹을 할당하세요.");
            yield break;
        }

        if (!locationItemPrefab)
        {
            Debug.LogError("[MemoListManager] locationItemPrefab이 할당되지 않았습니다! Inspector에서 위치 리스트 프리팹을 할당하세요.");
            yield break;
        }

        if (!memoItemPrefab)
        {
            Debug.LogError("[MemoListManager] memoItemPrefab이 할당되지 않았습니다! Inspector에서 메모 리스트 프리팹을 할당하세요.");
            yield break;
        }

        // 프리팹에 필요한 컴포넌트가 있는지 확인 (자식 오브젝트에 있을 수도 있으므로 GetComponentInChildren 사용)
        MapListItemUI mapUI = mapItemPrefab.GetComponentInChildren<MapListItemUI>();
        if (mapUI == null)
        {
            Debug.LogError($"[MemoListManager] mapItemPrefab '{mapItemPrefab.name}' 또는 하위 오브젝트에 MapListItemUI 컴포넌트가 없습니다! " +
                "프리팹에 MapListItemUI 스크립트를 추가하거나, 올바른 프리팹을 할당하세요.");
            Debug.LogError($"[MemoListManager] 현재 할당된 오브젝트: {mapItemPrefab.name}, 최상위 컴포넌트 목록: {string.Join(", ", mapItemPrefab.GetComponents<Component>().Select(c => c.GetType().Name))}");
            yield break;
        }

        MemoLocationUI locUI = locationItemPrefab.GetComponentInChildren<MemoLocationUI>();
        if (locUI == null)
        {
            Debug.LogError($"[MemoListManager] locationItemPrefab '{locationItemPrefab.name}' 또는 하위 오브젝트에 MemoLocationUI 컴포넌트가 없습니다! " +
                "프리팹에 MemoLocationUI 스크립트를 추가하거나, 올바른 프리팹을 할당하세요.");
            Debug.LogError($"[MemoListManager] 현재 할당된 오브젝트: {locationItemPrefab.name}, 최상위 컴포넌트 목록: {string.Join(", ", locationItemPrefab.GetComponents<Component>().Select(c => c.GetType().Name))}");
            yield break;
        }

        MemoItemUI memoUI = memoItemPrefab.GetComponentInChildren<MemoItemUI>();
        if (memoUI == null)
        {
            Debug.LogError($"[MemoListManager] memoItemPrefab '{memoItemPrefab.name}' 또는 하위 오브젝트에 MemoItemUI 컴포넌트가 없습니다! " +
                "프리팹에 MemoItemUI 스크립트를 추가하거나, 올바른 프리팹을 할당하세요.");
            Debug.LogError($"[MemoListManager] 현재 할당된 오브젝트: {memoItemPrefab.name}, 최상위 컴포넌트 목록: {string.Join(", ", memoItemPrefab.GetComponents<Component>().Select(c => c.GetType().Name))}");
            yield break;
        }

        ClearContent();  // 새로고침 전 리스트 목록 제거 (중복 방지)

        // 유니티 에디터 실행 시 MOCK 리스트 사용
#if UNITY_EDITOR
        if (useMockListInEditor)
        {
            Debug.Log("[MemoListManager] Using MOCK list (Editor only).");
            AddMapItemToUI(new JobItem { id = 1111, name = "MOCK_MAP_A", status = "done" });
            AddMapItemToUI(new JobItem { id = 2222, name = "MOCK_MAP_B", status = "done" });
            yield break;
        }
#endif

        // 네트워크 상태 확인 (토큰 입력 여부 확인)
        if (string.IsNullOrEmpty(developerToken))
        {
            Debug.LogError("[MemoListManager] developerToken이 비어있습니다. 인스펙터에서 token을 입력해야 /list가 동작합니다.");
            yield break;
        }

        // 요청 토큰을 JSON으로 만들기
        var requestBody = JsonUtility.ToJson(new ListRequest { token = developerToken });
        Debug.Log($"[MemoListManager] POST {listEndpoint}");
        Debug.Log($"[MemoListManager] RequestBody={requestBody}");

        // listEndpoint 주소에서 맵 리스트 받아오기
        using (var req = new UnityWebRequest(listEndpoint, "POST"))
        {
            // 요청 설정
            byte[] bodyRaw = Encoding.UTF8.GetBytes(requestBody);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            // 요청 전송 및 완료 대기
            yield return req.SendWebRequest();

            Debug.Log($"[MemoListManager] /list result={req.result}, code={req.responseCode}");
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[MemoListManager] /list request failed: {req.error}");
                Debug.LogError($"[MemoListManager] /list response text: {req.downloadHandler?.text}");
                yield break;
            }

            // 서버 응답을 JSON으로 저장/출력
            var json = req.downloadHandler.text;
            Debug.Log($"[MemoListManager] /list response json: {TrimForLog(json, 2000)}");

            // JSON 해석
            JobListResponse parsed = null;
            try
            {
                parsed = JsonUtility.FromJson<JobListResponse>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MemoListManager] JSON parse exception: {e}");
                yield break;
            }

            if (parsed == null)
            {
                Debug.LogError("[MemoListManager] JSON parse result is null.");
                yield break;
            }

            Debug.Log($"[MemoListManager] parsed.error={parsed.error}, parsed.count={parsed.count}, jobs={(parsed.jobs == null ? "NULL" : parsed.jobs.Length.ToString())}");

            // 에러 체크
            if (!string.Equals(parsed.error, "none", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError($"[MemoListManager] Immersal returned error='{parsed.error}'. (token/auth 문제일 가능성이 큽니다)");
                yield break;
            }

            // 맵 목록 체크
            if (parsed.jobs == null || parsed.jobs.Length == 0)
            {
                Debug.LogWarning("[MemoListManager] jobs가 0개입니다. (계정에 맵이 없거나, 권한/프라이버시/상태가 맞지 않을 수 있습니다)");
                yield break;
            }

            // 맵 목록을 UI에 추가
            int added = 0;
            foreach (var job in parsed.jobs)
            {
                // 맵이 사용 가능한지 판단
                if (!string.Equals(job.status, "done", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log($"[MemoListManager] skip job id={job.id} name={job.name} status={job.status}");
                    continue;
                }

                // 맵 아이템을 UI에 추가
                AddMapItemToUI(job);
                added++;
            }

            Debug.Log($"[MemoListManager] UI populated. added(done)={added}, totalReturned={parsed.jobs.Length}");
        }
    }

    // 맵 아이템을 UI에 추가
    private void AddMapItemToUI(JobItem job)
    {
        Debug.Log($"=== [MemoListManager] AddMapItemToUI START: id={job.id}, name={job.name} ===");

        // 맵 프리팹 생성
        GameObject mapObj = Instantiate(mapItemPrefab, contentRoot);
        mapObj.name = $"MapItem_{job.id}_{job.name}";
        Debug.Log($"[MemoListManager] MapItem instantiated: {mapObj.name}");

        // 컴포넌트 가져오기 (자식 오브젝트에 있을 수 있으므로 GetComponentInChildren 사용)
        MapListItemUI item = mapObj.GetComponentInChildren<MapListItemUI>();
        if (item == null)
        {
            Debug.LogError("[MemoListManager] Instantiated map prefab does not have MapListItemUI component in itself or children!");
            Destroy(mapObj);
            return;
        }

        Debug.Log($"[MemoListManager] MapListItemUI found: id={job.id}, name={job.name}, status={job.status}");

        // 맵 클릭 이벤트 연결 - 메모 위치 리스트 펼치기/접기
        item.Bind(job.id, job.name, () =>
        {
            Debug.Log($"[MemoListManager] CLICK map id={job.id}, name={job.name}");
            ToggleLocationList(job.id, job.name, mapObj.transform);
        });

        Debug.Log($"[MemoListManager] Item bound, contentRoot.childCount={contentRoot.childCount}");

        // 레이아웃 강제 갱신
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot as RectTransform);

        Debug.Log($"=== [MemoListManager] AddMapItemToUI END: {job.name} ===");

        // 맵 아이템 UI 참조 저장
        mapItemUIRefs[job.id] = item;

        // 초기 상태 설정 (접힌 상태)
        item.SetExpanded(false);
    }

    // 메모 위치 리스트 펼치기/접기
    private void ToggleLocationList(int mapId, string mapName, Transform mapItemTransform)
    {
        // 이미 펼쳐져 있으면 접기
        if (mapExpanded.ContainsKey(mapId) && mapExpanded[mapId])
        {
            Debug.Log($"[MemoListManager] Collapsing location list for map {mapId}");
            CollapseLocationList(mapId);

            // UI 아이콘 업데이트
            if (mapItemUIRefs.ContainsKey(mapId))
                mapItemUIRefs[mapId].SetExpanded(false);
        }
        else
        {
            Debug.Log($"[MemoListManager] Expanding location list for map {mapId}");
            ExpandLocationList(mapId, mapName, mapItemTransform);

            // UI 아이콘 업데이트
            if (mapItemUIRefs.ContainsKey(mapId))
                mapItemUIRefs[mapId].SetExpanded(true);
        }
    }

    // 메모 위치 리스트 펼치기
    private void ExpandLocationList(int mapId, string mapName, Transform mapItemTransform)
    {
        // MapListItemUI에서 childContainer 가져오기
        MapListItemUI mapListUI = mapItemTransform.GetComponent<MapListItemUI>();
        if (mapListUI == null)
        {
            mapListUI = mapItemTransform.GetComponentInChildren<MapListItemUI>();
        }

        Transform targetContainer = contentRoot;
        if (mapListUI != null)
        {
            RectTransform childContainer = mapListUI.GetChildContainer();
            if (childContainer != null)
            {
                targetContainer = childContainer;
            }
        }

        // 메모 데이터를 위치별로 그룹화하여 로드
        var locationGroups = LoadMemosGroupedByLocation(mapId);

        if (locationGroups == null || locationGroups.Count == 0)
        {
            Debug.Log($"[MemoListManager] No memo locations found for map {mapId}");

            // 메모 추가 버튼 생성
            CreateAddMemoButton(mapId, mapName, mapItemTransform);

            // 메모가 없어도 상태는 펼침으로 표시
            mapExpanded[mapId] = true;
            return;
        }

        // 위치 아이템 리스트 생성
        List<GameObject> locationItems = new List<GameObject>();
        int siblingIndex = mapItemTransform.GetSiblingIndex() + 1;

        foreach (var locationGroup in locationGroups)
        {
            GameObject locationObj = Instantiate(locationItemPrefab, targetContainer);
            locationObj.name = $"Location_{mapId}_{locationGroup.Key}";

            // 컴포넌트 가져오기 (자식 오브젝트에 있을 수 있으므로 GetComponentInChildren 사용)
            MemoLocationUI locationItem = locationObj.GetComponentInChildren<MemoLocationUI>();
            if (locationItem == null)
            {
                Debug.LogError("[MemoListManager] Instantiated location prefab does not have MemoLocationUI component in itself or children!");
                Destroy(locationObj);
                continue;
            }

            // childContainer에 추가될 때는 들여쓰기 불필요
            // var rectTransform = locationObj.GetComponent<RectTransform>();
            // if (rectTransform != null)
            // {
            //     rectTransform.offsetMin = new Vector2(locationIndentSize, rectTransform.offsetMin.y);
            // }

            // 위치 표시 텍스트 생성 (사용자 입력 위치 우선, 없으면 좌표)
            string userLocation = locationGroup.Value[0].location;
            string locationDisplay;

            if (!string.IsNullOrWhiteSpace(userLocation))
            {
                // 사용자가 입력한 위치 정보가 있으면 표시
                locationDisplay = userLocation;
            }
            else
            {
                // 없으면 좌표 표시
                locationDisplay = $"위치: ({locationGroup.Value[0].posX:F1}, {locationGroup.Value[0].posY:F1}, {locationGroup.Value[0].posZ:F1})";
            }

            // 위치 클릭 이벤트 연결 - 메모 타이틀 리스트 펼치기/접기
            string locationKey = $"{mapId}_{locationGroup.Key}";
            locationItem.Bind(locationGroup.Key, locationDisplay, () =>
            {
                Debug.Log($"[MemoListManager] CLICK location key={locationKey}");
                ToggleMemoList(mapId, mapName, locationKey, locationGroup.Value, locationObj.transform);
            });

            // 위치 아이템 UI 참조 저장
            locationItemUIRefs[locationKey] = locationItem;

            // 초기 상태 설정 (접힌 상태)
            locationItem.SetExpanded(false);

            // 해당 위치 그룹에 isAssigned=true인 메모가 있는지 확인하여 초기 색상 설정
            bool hasAssignedMemo = locationGroup.Value.Exists(m => m.isAssigned);
            locationItem.SetInitialColors(hasAssignedMemo);
            Debug.Log($"[MemoListManager] LocationItem 초기 색상 설정: locationKey={locationKey}, hasAssigned={hasAssignedMemo}");

            // childContainer에 추가될 때는 sibling index 설정 불필요 (VerticalLayoutGroup이 자동 배치)
            // locationObj.transform.SetSiblingIndex(siblingIndex);
            // siblingIndex++;

            locationItems.Add(locationObj);
        }

        // 메모 추가 버튼을 제일 마지막에 생성
        CreateAddMemoButton(mapId, mapName, mapItemTransform);

        // 상태 저장
        mapExpanded[mapId] = true;
        mapLocationItems[mapId] = locationItems;

        Debug.Log($"[MemoListManager] Expanded {locationItems.Count} locations for map {mapId}");
    }

    // 메모 위치 리스트 접기
    private void CollapseLocationList(int mapId)
    {
        if (!mapLocationItems.ContainsKey(mapId))
            return;

        // 각 위치의 메모들도 모두 접기
        foreach (var locationObj in mapLocationItems[mapId])
        {
            if (locationObj != null)
            {
                // 해당 위치의 메모들 먼저 정리
                string locationKey = ExtractLocationKeyFromName(locationObj.name, mapId);
                if (!string.IsNullOrEmpty(locationKey))
                {
                    CollapseMemoList(locationKey);
                }

                Destroy(locationObj);
            }
        }

        mapLocationItems[mapId].Clear();

        // AddMemoButton 제거
        if (mapAddMemoButtons.ContainsKey(mapId) && mapAddMemoButtons[mapId] != null)
        {
            Destroy(mapAddMemoButtons[mapId]);
            mapAddMemoButtons.Remove(mapId);
        }

        mapExpanded[mapId] = false;

        Debug.Log($"[MemoListManager] Collapsed location list for map {mapId}");
    }

    // 메모 타이틀 리스트 펼치기/접기
    private void ToggleMemoList(int mapId, string mapName, string locationKey, List<MemoInfo> memos, Transform locationItemTransform)
    {
        // 이미 펼쳐져 있으면 접기
        if (locationExpanded.ContainsKey(locationKey) && locationExpanded[locationKey])
        {
            Debug.Log($"[MemoListManager] Collapsing memo list for location {locationKey}");
            CollapseMemoList(locationKey);

            // UI 아이콘 업데이트
            if (locationItemUIRefs.ContainsKey(locationKey))
                locationItemUIRefs[locationKey].SetExpanded(false);
        }
        else
        {
            Debug.Log($"[MemoListManager] Expanding memo list for location {locationKey}");
            ExpandMemoList(mapId, mapName, locationKey, memos, locationItemTransform);

            // UI 아이콘 업데이트
            if (locationItemUIRefs.ContainsKey(locationKey))
                locationItemUIRefs[locationKey].SetExpanded(true);
        }
    }

    // 메모 타이틀 리스트 펼치기
    private void ExpandMemoList(int mapId, string mapName, string locationKey, List<MemoInfo> memos, Transform locationItemTransform)
    {
        // MemoLocationUI에서 childContainer 가져오기
        MemoLocationUI locationUI = locationItemTransform.GetComponent<MemoLocationUI>();
        if (locationUI == null)
        {
            locationUI = locationItemTransform.GetComponentInChildren<MemoLocationUI>();
        }

        // 메모 아이템이 추가될 컨테이너 결정
        Transform targetContainer = contentRoot;
        if (locationUI != null)
        {
            RectTransform childContainer = locationUI.GetChildContainer();
            if (childContainer != null)
            {
                targetContainer = childContainer;
            }
        }

        // 메모 아이템 리스트 생성
        List<GameObject> memoItems = new List<GameObject>();

        foreach (var memo in memos)
        {
            GameObject memoObj = Instantiate(memoItemPrefab, targetContainer);
            memoObj.name = $"Memo_{memo.id}";

            // 컴포넌트 가져오기 (자식 오브젝트에 있을 수 있으므로 GetComponentInChildren 사용)
            MemoItemUI memoItem = memoObj.GetComponentInChildren<MemoItemUI>();
            if (memoItem == null)
            {
                Debug.LogError("[MemoListManager] Instantiated memo prefab does not have MemoItemUI component in itself or children!");
                Destroy(memoObj);
                continue;
            }

            // 메모 UI 바인딩 (제목, 색상 설정) - MeetingScene으로만 이동 (ToScanBtController가 처리)
            Debug.Log($"★★★ [ASSIGNEE] [MemoListManager] MemoItemUI.Bind 호출: id={memo.id}, title={memo.title}, isAssigned={memo.isAssigned}");

            memoItem.Bind(memo.id, memo.title, memo.isAssigned, () =>
            {
                Debug.Log($"[MemoListManager] CLICK memo id={memo.id}, title={memo.title}");
                // ConstructionVPS로 이동하지 않음 - ToScanBtController가 MeetingScene으로 이동
            });

            memoItems.Add(memoObj);
        }

        // 상태 저장
        locationExpanded[locationKey] = true;
        locationMemoItems[locationKey] = memoItems;

        // 레이아웃 강제 갱신
        Canvas.ForceUpdateCanvases();
        if (targetContainer is RectTransform containerRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        }
        if (locationItemTransform is RectTransform locationRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(locationRect);
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot as RectTransform);

        // LocationItem의 색상을 하위 메모들의 isAssigned에 따라 업데이트
        if (locationUI != null)
        {
            locationUI.UpdateColorsBasedOnChildren();
        }

        Debug.Log($"[MemoListManager] Expanded {memoItems.Count} memos for location {locationKey}");
    }

    // 메모 타이틀 리스트 접기
    private void CollapseMemoList(string locationKey)
    {
        if (!locationMemoItems.ContainsKey(locationKey))
            return;

        // 메모 아이템들 제거
        foreach (var memoItem in locationMemoItems[locationKey])
        {
            if (memoItem != null)
                Destroy(memoItem);
        }

        locationMemoItems[locationKey].Clear();
        locationExpanded[locationKey] = false;

        Debug.Log($"[MemoListManager] Collapsed memo list for location {locationKey}");
    }

    // 메모 추가 버튼 생성 (각 맵의 제일 하위에 배치)
    private void CreateAddMemoButton(int mapId, string mapName, Transform mapItemTransform, int? siblingIndex = null)
    {
        if (addMemoButtonPrefab == null)
        {
            Debug.LogWarning("[MemoListManager] addMemoButtonPrefab is not assigned. 메모 추가 버튼을 생성하지 않습니다.");
            return;
        }

        // 이미 존재하는 AddMemoButton이 있으면 제거
        if (mapAddMemoButtons.ContainsKey(mapId) && mapAddMemoButtons[mapId] != null)
        {
            Destroy(mapAddMemoButtons[mapId]);
            mapAddMemoButtons.Remove(mapId);
        }

        // MapListItemUI에서 childContainer 가져오기
        MapListItemUI mapListUI = mapItemTransform.GetComponent<MapListItemUI>();
        if (mapListUI == null)
        {
            mapListUI = mapItemTransform.GetComponentInChildren<MapListItemUI>();
        }

        Transform targetContainer = contentRoot;
        if (mapListUI != null)
        {
            RectTransform childContainer = mapListUI.GetChildContainer();
            if (childContainer != null)
            {
                targetContainer = childContainer;
            }
        }

        GameObject btnObj = Instantiate(addMemoButtonPrefab, targetContainer);
        btnObj.name = $"AddMemoBtn_{mapId}";

        // Dictionary에 저장
        mapAddMemoButtons[mapId] = btnObj;

        // childContainer에 추가할 때는 들여쓰기 불필요 (이미 childContainer 자체가 들여쓰기 되어있음)
        // var rectTransform = btnObj.GetComponent<RectTransform>();
        // if (rectTransform != null)
        // {
        //     rectTransform.offsetMin = new Vector2(memoIndentSize, rectTransform.offsetMin.y);
        // }

        // 버튼 설정 (자식 오브젝트에 있을 수 있으므로 GetComponentInChildren 사용)
        AddMemoButton addBtn = btnObj.GetComponentInChildren<AddMemoButton>();
        if (addBtn != null)
        {
            addBtn.Setup(mapId, mapName);
        }
        else
        {
            Debug.LogError($"[MemoListManager] addMemoButtonPrefab '{addMemoButtonPrefab.name}' 또는 하위 오브젝트에 AddMemoButton 컴포넌트가 없습니다!");
        }

        // childContainer에 추가할 때는 sibling index 설정 불필요
        // if (siblingIndex.HasValue)
        // {
        //     btnObj.transform.SetSiblingIndex(siblingIndex.Value);
        // }
        // else
        // {
        //     btnObj.transform.SetSiblingIndex(mapItemTransform.GetSiblingIndex() + 1);
        // }

        // 맵 아이템 리스트에 추가 (접을 때 같이 삭제되도록)
        if (!mapLocationItems.ContainsKey(mapId))
        {
            mapLocationItems[mapId] = new List<GameObject>();
        }
        mapLocationItems[mapId].Add(btnObj);

        // 레이아웃 강제 갱신
        Canvas.ForceUpdateCanvases();
        if (targetContainer is RectTransform containerRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        }
        if (mapItemTransform is RectTransform mapRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(mapRect);
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot as RectTransform);

        Debug.Log($"[MemoListManager] Created AddMemoButton for map {mapId}");
    }

    // 특정 맵의 메모 데이터를 위치별로 그룹화하여 로드
    private Dictionary<string, List<MemoInfo>> LoadMemosGroupedByLocation(int mapId)
    {
        string jsonPath = Path.Combine(Application.persistentDataPath, $"immersal_pins_{mapId}.json");

        Debug.Log($"[MemoListManager] Loading memos from: {jsonPath}");

        if (!File.Exists(jsonPath))
        {
            Debug.Log($"[MemoListManager] No memo file found for map {mapId}");
            return null;
        }

        try
        {
            string json = File.ReadAllText(jsonPath);
            Debug.Log($"[MemoListManager] Loaded JSON: {TrimForLog(json, 500)}");

            var pinDB = JsonUtility.FromJson<PinDatabase>(json);

            if (pinDB == null || pinDB.pins == null || pinDB.pins.Length == 0)
            {
                Debug.Log($"[MemoListManager] No pins in database for map {mapId}");
                return null;
            }

            // 위치별로 메모 그룹화 (사용자 입력 위치 우선, 없으면 좌표로 그룹화)
            Dictionary<string, List<MemoInfo>> locationGroups = new Dictionary<string, List<MemoInfo>>();

            foreach (var pin in pinDB.pins)
            {
                string locationKey;
                float posX;
                float posY;
                float posZ;

                // 사용자가 입력한 위치 정보가 있으면 그것을 키로 사용
                if (!string.IsNullOrWhiteSpace(pin.location))
                {
                    locationKey = pin.location;
                    // 좌표도 저장 (표시용)
                    posX = Mathf.Round(pin.localPosX * 2f) / 2f;
                    posY = Mathf.Round(pin.localPosY * 2f) / 2f;
                    posZ = Mathf.Round(pin.localPosZ * 2f) / 2f;
                }
                else
                {
                    // 없으면 좌표를 0.5m 단위로 그룹화
                    posX = Mathf.Round(pin.localPosX * 2f) / 2f;
                    posY = Mathf.Round(pin.localPosY * 2f) / 2f;
                    posZ = Mathf.Round(pin.localPosZ * 2f) / 2f;
                    locationKey = $"{posX:F1}_{posY:F1}_{posZ:F1}";
                }

                if (!locationGroups.ContainsKey(locationKey))
                {
                    locationGroups[locationKey] = new List<MemoInfo>();
                }

                locationGroups[locationKey].Add(new MemoInfo
                {
                    id = pin.id,
                    title = pin.title,
                    location = pin.location,
                    posX = posX,
                    posY = posY,
                    posZ = posZ,
                    isAssigned = pin.isAssigned
                });
            }

            Debug.Log($"[MemoListManager] Grouped {pinDB.pins.Length} memos into {locationGroups.Count} locations for map {mapId}");
            return locationGroups;
        }
        catch (Exception e)
        {
            Debug.LogError($"[MemoListManager] Failed to load memos for map {mapId}: {e}");
            return null;
        }
    }

    // GameObject 이름에서 location key 추출
    private string ExtractLocationKeyFromName(string objName, int mapId)
    {
        // "Location_1111_1.0_2.0_3.0" 형식에서 "1111_1.0_2.0_3.0" 추출
        string prefix = $"Location_{mapId}_";
        if (objName.StartsWith(prefix))
        {
            return $"{mapId}_{objName.Substring(prefix.Length)}";
        }
        return "";
    }

    // 맵과 메모 정보를 저장하고 ConstructionVPS 씬으로 이동
    private void LoadSceneWithMapAndMemo(int mapId, string mapName, string memoId)
    {
        // 맵 정보 저장
        PlayerPrefs.SetInt(PREF_SELECTED_MAP_ID, mapId);
        PlayerPrefs.SetString(PREF_SELECTED_MAP_NAME, mapName ?? "");

        // 메모 ID 저장 (ConstructionVPS 씬에서 해당 메모를 자동으로 선택하도록)
        PlayerPrefs.SetString("SELECTED_MEMO_ID", memoId);
        PlayerPrefs.Save();

        Debug.Log($"[MemoListManager] Saved PlayerPrefs: mapId={mapId}, mapName={mapName}, memoId={memoId}");
        Debug.Log($"[MemoListManager] Loading scene: {constructionSceneName}");

        // 씬 이동 (이전 씬 자동 저장됨)
        SceneTransitionFade.LoadScene(constructionSceneName);
    }

    // contentRoot의 자식 오브젝트 모두 제거
    private void ClearContent()
    {
        Debug.Log($"[MemoListManager] ClearContent() childCount(before)={contentRoot.childCount}");
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(contentRoot.GetChild(i).gameObject);
        }

        mapExpanded.Clear();
        mapLocationItems.Clear();
        mapItemUIRefs.Clear();
        locationExpanded.Clear();
        locationMemoItems.Clear();
        locationItemUIRefs.Clear();
    }

    // 긴 문자열 로그 출력을 위한 자르기
    private static string TrimForLog(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (s.Length <= max) return s;
        return s.Substring(0, max) + $"... (trimmed, len={s.Length})";
    }

    // ===== 데이터 구조 =====

    // 서버에 보낼 요청 JSON
    [Serializable]
    private class ListRequest
    {
        public string token;
    }

    // 서버가 준 응답 JSON
    [Serializable]
    private class JobListResponse
    {
        public string error;
        public int count;
        public JobItem[] jobs;
    }

    // 응답 안의 jobs 배열에서 맵 1개
    [Serializable]
    public class JobItem
    {
        public int id;
        public string name;
        public string status;
    }

    // 메모 정보 (위치 포함)
    [Serializable]
    private class MemoInfo
    {
        public string id;
        public string title;
        public string location;  // 사용자가 입력한 위치
        public float posX;
        public float posY;
        public float posZ;
        public bool isAssigned;  // AssigneeRow Toggle 상태
    }

    // TabPinCreate에서 사용하는 PinDatabase 구조 (호환성)
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
        public string location;        // 사용자가 입력한 위치 정보
        public float localPosX;
        public float localPosY;
        public float localPosZ;
        public bool isAssigned;        // AssigneeRow Toggle 상태 저장
    }
}
