
// Immersal 서버에서 맵 목록을 받아 리스트로 생성 후 탭 시 씬 이동까지 담당하는 코드
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class MapBrowserManager : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("맵 리스트 프리팹이 생성되어 붙을 부모 Transform을 넣는 자리")]
    [SerializeField] private Transform contentRoot;

    [Tooltip("맵 리스트 프리팹을 넣는 자리")]
    [SerializeField] private MapListItemUI itemPrefab;

    [Header("Immersal Token")]
    [Tooltip("Immersal Developer Portal에서 발급받은 developer token을 넣는 자리")]
    [SerializeField] private string developerToken = "";

    [Header("Immersal Map API")]
    [Tooltip("유니티로 가져올 맵 리스트 API 요청 주소를 넣는 자리")]
    [SerializeField] private string listEndpoint = "https://api.immersal.com/list";

    [Header("Move Scene")]
    [Tooltip("맵 리스트 클릭 시 이동한 씬의 이름을 적는 자리")]
    [SerializeField] private string constructionSceneName = "ConstructionVPS";

    [Header("Dev Check")]
    [Tooltip("유니티 에디터에서, 리스트 생성 동작이 잘 되는지 확인하기 위한 스위치")]
    [SerializeField] private bool useMockListInEditor = true;


    // PlayerPrefs(유니티 저장소)에 저장하는 맵 ID/NAME 키
    private const string PREF_SELECTED_MAP_ID = "IMMERSAL_SELECTED_MAP_ID";
    private const string PREF_SELECTED_MAP_NAME = "IMMERSAL_SELECTED_MAP_NAME";

    // 시작 전 주요 설정값 점검 (Android Logcat으로 확인)
    private void Awake()
    {
        Debug.Log($"[MapBrowserManager] Awake() scene={SceneManager.GetActiveScene().name}"); // 현재 실행 중인 씬 이름 확인하기 위함
        Debug.Log($"[MapBrowserManager] listEndpoint={listEndpoint}"); // 맵 리스트 API 요청 주소 확인하기 위함
        Debug.Log($"[MapBrowserManager] developerToken={(string.IsNullOrEmpty(developerToken) ? "EMPTY" : $"LEN={developerToken.Length}")}"); //토큰이 비어있는지 확인하기 위함
    }

    // 현재 플랫폼과 네트워크 상태 확인 후 리스트 목록 갱신 코루틴 실행
    private void Start()
    {
        Debug.Log($"[MapBrowserManager] Start() platform={Application.platform}, internetReachability={Application.internetReachability}");
        StartCoroutine(RefreshList());
    }

    // Reset 버튼 클릭 시 리스트 목록 갱신 코루틴 실행 
    public void RefreshButton()
    {
        Debug.Log("[MapBrowserManager] RefreshButton() called");
        StartCoroutine(RefreshList());
    }

    // 서버에서 데이터를 받아와 화면에 리스트 생성
    private IEnumerator RefreshList()
    {
        // UI 참조 여부 확인
        if (!contentRoot || !itemPrefab)
        {
            Debug.LogError("[MapBrowserManager] UI references missing. contentRoot/itemPrefab을 인스펙터에서 확인하세요.");
            yield break;
        }

        ClearContent();  // 새로고침 전 리스트 목록 제거 위함 (중복 방지)

        // 유니티 에디터 실생 시 MOCK 리스트 사용 > 생성 확인
#if UNITY_EDITOR 
        if (useMockListInEditor) // 인스펙터 useMockListInEditor 체크박스로 조절 위함
        {
            Debug.Log("[MapBrowserManager] Using MOCK list (Editor only).");
            AddItemToUI(new JobItem { id = 1111, name = "MOCK_MAP_A", status = "done" });
            AddItemToUI(new JobItem { id = 2222, name = "MOCK_MAP_B", status = "done" });
            yield break;
        }

#endif

        // 네트워크 상태 확인 (토큰 입력 여부 확인)
        if (string.IsNullOrEmpty(developerToken))
        {
            Debug.LogError("[MapBrowserManager] developerToken이 비어있습니다. 인스펙터에서 token을 입력해야 /list가 동작합니다.");
            yield break;
        }

        // 요청 토큰을 JSON으로 만들기
        var requestBody = JsonUtility.ToJson(new ListRequest { token = developerToken });
        Debug.Log($"[MapBrowserManager] POST {listEndpoint}");        // 요청 주소 확인 위함
        Debug.Log($"[MapBrowserManager] RequestBody={requestBody}");  // 요청 내용 확인 위함 


        // listEndpoint입력 주소에서 맵 리스트 받아오기
        using (var req = new UnityWebRequest(listEndpoint, "POST"))
        {

            //요청 설정
            byte[] bodyRaw = Encoding.UTF8.GetBytes(requestBody);      // JSON을 바이트 배열로 변환하기 위함
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);         // 바이트 배열을 보내기 위함
            req.downloadHandler = new DownloadHandlerBuffer();         // 응답을 잠시 저장하기 위함
            req.SetRequestHeader("Content-Type", "application/json");  // 서버에 JSON 타입임을 알려주기 위함

            // 요청 전송 및 완료 대기
            yield return req.SendWebRequest();

            // 요청 실패 시 > 로그 출력
            Debug.Log($"[MapBrowserManager] /list result={req.result}, code={req.responseCode}"); // 결과와 응답 코드 기록 위함
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[MapBrowserManager] /list request failed: {req.error}");
                Debug.LogError($"[MapBrowserManager] /list response text: {req.downloadHandler?.text}");
                yield break;
            }

            // 요청 성공 시 > 서버 응답을 JSON으로 저장/출력
            var json = req.downloadHandler.text;
            Debug.Log($"[MapBrowserManager] /list response json: {TrimForLog(json, 2000)}");

            // 받아온 JSON 맵 데이터 해석 (예외 처리 알림 포함)
            JobListResponse parsed = null;
            try
            {
                parsed = JsonUtility.FromJson<JobListResponse>(json); // JSON 해석 시도 위함
            }
            catch (Exception e)
            {
                Debug.LogError($"[MapBrowserManager] JSON parse exception: {e}");
                yield break;
            }

            if (parsed == null)
            {
                Debug.LogError("[MapBrowserManager] JSON parse result is null.");
                yield break;
            }

            // 에러 문자열, 객체 개수, 목록 배열 길이 출력
            Debug.Log($"[MapBrowserManager] parsed.error={parsed.error}, parsed.count={parsed.count}, jobs={(parsed.jobs == null ? "NULL" : parsed.jobs.Length.ToString())}");


            // 에러의 NULL 판단/알림 및 중단
            if (!string.Equals(parsed.error, "none", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError($"[MapBrowserManager] Immersal returned error='{parsed.error}'. (token/auth 문제일 가능성이 큽니다)");
                yield break;
            }

            // 목록 배열(jobs) 유무 판단/알림 및 중단
            if (parsed.jobs == null || parsed.jobs.Length == 0)
            {
                Debug.LogWarning("[MapBrowserManager] jobs가 0개입니다. (계정에 맵이 없거나, 권한/프라이버시/상태가 맞지 않을 수 있습니다)");
                yield break;
            }

            // 목록 배열(jobs)을 UI에 추가
            int added = 0;
            foreach (var job in parsed.jobs)
            {
                // 목록 배열(jobs) 즉 맵 리스트가 사용 가능한지 판단 > 아니라면 로그 출력
                if (!string.Equals(job.status, "done", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log($"[MapBrowserManager] skip job id={job.id} name={job.name} status={job.status}");
                    continue;
                }

                // 목록 배열(jobs)을 UI 프리팹으로 생성
                AddItemToUI(job);
                added++;
            }

            Debug.Log($"[MapBrowserManager] UI populated. added(done)={added}, totalReturned={parsed.jobs.Length}");
        }
    }

    // 생성된 프리팹에 클릭 이벤트 연결
    private void AddItemToUI(JobItem job)
    {
        // 생성된 프리팹을 XR Pace의 자식으로 복제
        var item = Instantiate(itemPrefab, contentRoot);
        item.gameObject.name = $"MapItem_{job.id}_{job.name}";
        Debug.Log($"[MapBrowserManager] Instantiate item: id={job.id}, name={job.name}, status={job.status}");

        // UI 프리팹에 표시할 값과 할 일을 전달
        item.Bind(job.id, job.name, () =>
        {
            Debug.Log($"[MapBrowserManager] CLICK map id={job.id}, name={job.name}");

            // 클릭 시 선택한 맵 ID/NAME을 PlayerPrefs에 저장
            PlayerPrefs.SetInt(PREF_SELECTED_MAP_ID, job.id);
            PlayerPrefs.SetString(PREF_SELECTED_MAP_NAME, job.name ?? "");
            PlayerPrefs.Save();

            Debug.Log($"[MapBrowserManager] Saved PlayerPrefs: {PREF_SELECTED_MAP_ID}={job.id}, {PREF_SELECTED_MAP_NAME}={job.name}");
            Debug.Log($"[MapBrowserManager] Loading scene: {constructionSceneName}");

            // 씬 이동 (이전 씬 자동 저장됨)
            SceneTransitionFade.LoadScene(constructionSceneName);
        });
    }

    // contentRoot의 자식 오브젝트 모두 제거
    private void ClearContent()
    {
        Debug.Log($"[MapBrowserManager] ClearContent() childCount(before)={contentRoot.childCount}"); // 지우기 전 개수 확인 위함
        for (int i = contentRoot.childCount - 1; i >= 0; i--) // 역순으로 제거 위함 (안정성)
        {
            Destroy(contentRoot.GetChild(i).gameObject);
        }
    }

    // 긴 문자열 로그 출력을 위한 자르기
    private static string TrimForLog(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (s.Length <= max) return s;
        return s.Substring(0, max) + $"... (trimmed, len={s.Length})";
    }

    // 서버에 보낼 요청 JSON을 만들기
    [Serializable]
    private class ListRequest
    {
        public string token;
    }

    // 서버가 준 응답 JSON을 C#로 받기
    [Serializable]
    private class JobListResponse
    {
        public string error;
        public int count;
        public JobItem[] jobs;
    }

    // 응답 안의 jobs 배열에서 맵 1개 꺼내기
    [Serializable]
    public class JobItem
    {
        public int id;
        public string name;
        public string status;
    }
}
