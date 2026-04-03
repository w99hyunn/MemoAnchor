
// Immersal 서버에서 맵 파일을 받아 기기 내부 저장소에 저장 (이미 같은 버전이면 X)
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class ImmersalMapDownloader : MonoBehaviour
{
    [Header("Map File Download URL")]
    [Tooltip("맵 파일 다운로드 URL 넣는 자리")]
    [SerializeField] private string mapUrl = "https://your-domain.com/maps/myMap.bytes";

    [Header("persistentDataPath File Name")]
    [Tooltip("기기내부 저장소에 저장될 파일명")]
    [SerializeField] private string persistentFileName = "myMap.bytes";

    [Header("맵 파일을 항상 다시 받을지 선택")]
    [Tooltip("true면 무조건 다시 다운로드")]
    [SerializeField] private bool forceRedownload = false;


    // 다운로드 완료 여부를 다른 스크립트가 기다릴 수 있게 공개

    // 다운로드 완료 여부를 외부에서 확인 할 수 있도록
    public bool IsReady { get; private set; } 
    public string PersistentFullPath { get; private set; }

    // 맵 버전 키를  확인할 수 있도록 > 버전이 같으면 재다운로드 안 함
    private const string EtagKeyPrefix = "IMMERSAL_MAP_ETAG_";


    private IEnumerator Start() // 맵 파일 다운로드와 나머지 기능을 병렬로 실행하기 위해
    {
        // 다운로드 여부 초기화 > OS에 맞게 파일 경로 설정
        IsReady = false;
        PersistentFullPath = Path.Combine(Application.persistentDataPath, persistentFileName);

        // 다운로드 시도
        yield return DownloadIfNeeded();
        IsReady = File.Exists(PersistentFullPath);

        //다운로드 여부와 경로 출력
        Debug.Log($"[MapDownloader] Ready={IsReady}, Path={PersistentFullPath}");
    }


    // 맵 파일 다운로드 수행 함수
    private IEnumerator DownloadIfNeeded()
    {
        // 변경 됐을 때에만 버전 키를 받기
        string etagKey = EtagKeyPrefix + persistentFileName;
        string savedEtag = PlayerPrefs.GetString(etagKey, "");

        if (forceRedownload) // 강제 다운로드 가능하게 하기 위함
            savedEtag = "";

        // 서버에 맵 파일 요청
        using (var req = UnityWebRequest.Get(mapUrl))
        {
            // 버전 키 있으면 추가해서 요청
            if (!string.IsNullOrEmpty(savedEtag))
                req.SetRequestHeader("If-None-Match", savedEtag);

            // 요청 제한 시간 설정
            req.timeout = 30; 

            //요청 URL과 버전 키 비교값 출력
            Debug.Log($"[MapDownloader] GET {mapUrl} (If-None-Match={savedEtag})");

            // 서버 요청
            yield return req.SendWebRequest();

            // 1. 요청 실패 처리
            bool is304 = req.responseCode == 304;
            if (req.result != UnityWebRequest.Result.Success && !is304)
            {
                Debug.LogError($"[MapDownloader] Download failed: {req.error} code={req.responseCode}");
                yield break;
            }

            // 2. 변경 사항 없음 처리
            if (is304)
            {
                Debug.Log("[MapDownloader] Not modified (304). Use cached file.");
                yield break;
            }

            // 3. 새 파일 줄 때 처리 
            byte[] bytes = req.downloadHandler.data;
            if (bytes == null || bytes.Length == 0)
            {
                Debug.LogError("[MapDownloader] Empty bytes.");
                yield break;
            }

            // 임시 파일에 저장 후 이동(부분 다운로드/중간 종료 대비)
            string tmpPath = PersistentFullPath + ".tmp";
            try
            {
                File.WriteAllBytes(tmpPath, bytes);

                if (File.Exists(PersistentFullPath))
                    File.Delete(PersistentFullPath);

                File.Move(tmpPath, PersistentFullPath);

                // 새 버전 키 저장
                string newEtag = req.GetResponseHeader("ETag");
                if (!string.IsNullOrEmpty(newEtag))
                {
                    PlayerPrefs.SetString(etagKey, newEtag);
                    PlayerPrefs.Save();
                    Debug.Log($"[MapDownloader] Saved ETag: {newEtag}");
                }

                Debug.Log($"[MapDownloader] Saved file: {PersistentFullPath} ({bytes.Length} bytes)");
            }

            // 파일 저장 실패 시 처리 
            catch (System.SystemException e)
            {
                Debug.LogError($"[MapDownloader] Save failed: {e}");
                // temp 정리
                if (File.Exists(tmpPath)) File.Delete(tmpPath);
            }
        }
    }
}
