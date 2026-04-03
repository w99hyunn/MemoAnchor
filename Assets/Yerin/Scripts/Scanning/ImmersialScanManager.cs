using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// 스캔 매니저 - 버튼 클릭으로 캡처
/// </summary>
public class ImmersialScanManager : MonoBehaviour
{
    [Header("UI References")]
    public Button captureButton; // 이 버튼 클릭할 때마다 캡처! ⭐
    public Button submitMapButton;
    public TMPro.TextMeshProUGUI statusText;

    [Header("Settings")]
    public string homeSceneName = "HomeScene";
    public string mapName = "MyScannedMap";
    public int minimumCaptures = 50;

    private int captureCount = 0;
    private float scanTime = 0f;
    private float scanStartTime = 0f;
    private bool isScanning = false;

    void Start()
    {
        SetupButtons();
        UpdateStatus("준비 완료\n버튼을 눌러 이미지 캡처");

        // 초기 버튼 상태
        if (submitMapButton != null)
            submitMapButton.interactable = false;
    }

    void Update()
    {
        if (isScanning)
        {
            scanTime = Time.time - scanStartTime;
            UpdateStatus($"캡처 중...\n시간: {scanTime:F1}초\n캡처: {captureCount}개");
        }
    }

    void SetupButtons()
    {
        if (captureButton != null)
            captureButton.onClick.AddListener(OnCaptureButtonClick); // 캡처! ⭐

        if (submitMapButton != null)
            submitMapButton.onClick.AddListener(SubmitMap);
    }

    // 버튼 클릭 = 캡처! ⭐⭐⭐
    public void OnCaptureButtonClick()
    {
        // 처음 캡처 시 스캔 시작
        if (!isScanning)
        {
            isScanning = true;
            scanStartTime = Time.time;
            captureCount = 0;
            Debug.Log("스캔 시작!");
        }

        // 캡처 카운트 증가
        captureCount++;
        Debug.Log($"캡처! 총 {captureCount}개");

        UpdateStatus($"캡처 완료! ✓\n시간: {scanTime:F1}초\n캡처: {captureCount}개");

        // 충분히 캡처했으면 제출 가능
        if (captureCount >= minimumCaptures)
        {
            if (submitMapButton != null)
                submitMapButton.interactable = true;

            UpdateStatus($"캡처 충분!\n캡처: {captureCount}개\n맵 제출 가능 ✓");
        }
    }

    public void SubmitMap()
    {
        if (captureCount < minimumCaptures)
        {
            UpdateStatus($"이미지 부족\n최소: {minimumCaptures}개\n현재: {captureCount}개");
            return;
        }

        UpdateStatus("맵 제출 중...");
        Debug.Log("맵 제출 시작");

        if (submitMapButton != null)
            submitMapButton.interactable = false;
        if (captureButton != null)
            captureButton.interactable = false;

        StartCoroutine(SimulateMapCreation());
    }

    IEnumerator SimulateMapCreation()
    {
        // 이미지 업로드
        UpdateStatus($"이미지 업로드 중...\n{captureCount}개");
        yield return new WaitForSeconds(2f);

        // 맵 생성 요청
        UpdateStatus("맵 생성 요청...");
        yield return new WaitForSeconds(1f);

        int jobId = Random.Range(10000, 99999);
        UpdateStatus($"맵 생성 중\nJob ID: {jobId}");
        Debug.Log($"Job ID: {jobId}");

        // 맵 처리 (30초 시뮬레이션)
        for (int i = 0; i < 6; i++)
        {
            yield return new WaitForSeconds(5f);
            int progress = (int)((float)(i + 1) / 6 * 100);
            UpdateStatus($"맵 생성 중...\n{progress}%");
        }

        // 완료
        int mapId = Random.Range(1000, 9999);
        UpdateStatus("맵 생성 완료!");
        Debug.Log($"Map ID: {mapId}");

        yield return new WaitForSeconds(1f);

        SaveMapAndReturn(mapId);
    }

    void SaveMapAndReturn(int mapId)
    {
        PlayerPrefs.SetInt("LastScannedMapId", mapId);

        int mapCount = PlayerPrefs.GetInt("TotalMapCount", 0);
        mapCount++;
        PlayerPrefs.SetInt("TotalMapCount", mapCount);
        PlayerPrefs.SetInt($"MapId_{mapCount}", mapId);

        PlayerPrefs.Save();

        Debug.Log($"맵 저장: Map #{mapCount}, ID: {mapId}");

        UpdateStatus("저장 완료!\n홈으로 이동...");

        Invoke("ReturnToHome", 2f);
    }

    void ReturnToHome()
    {
        SceneManager.LoadScene(homeSceneName);
    }

    void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        Debug.Log($"[ScanManager] {message.Replace("\n", " | ")}");
    }

    void OnDestroy()
    {
        if (captureButton != null)
            captureButton.onClick.RemoveListener(OnCaptureButtonClick);

        if (submitMapButton != null)
            submitMapButton.onClick.RemoveListener(SubmitMap);
    }
}