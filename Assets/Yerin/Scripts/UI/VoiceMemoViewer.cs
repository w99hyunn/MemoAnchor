using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 음성 메모 씬에서 녹음 파일을 재생
/// </summary>
public class VoiceMemoViewer : MemoViewerBase
{
    [Header("Voice Memo Specific")]
    [Tooltip("오디오를 재생할 AudioSource")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("녹음 리스트를 담을 부모 Transform (Scroll View의 Content)")]
    [SerializeField] private Transform recordingListContainer;

    [Tooltip("녹음 아이템 프리팹 (버튼 + 텍스트 포함)")]
    [SerializeField] private GameObject recordingItemPrefab;

    [Tooltip("녹음 개수를 표시할 텍스트")]
    [SerializeField] private TMP_Text recordingCountText;

    [Tooltip("현재 재생 중인 녹음 정보 표시")]
    [SerializeField] private TMP_Text currentPlayingText;

    [Tooltip("재생 시간 표시 (현재/전체)")]
    [SerializeField] private TMP_Text playbackTimeText;

    [Tooltip("진행 바 슬라이더")]
    [SerializeField] private Slider progressSlider;

    [Header("Audio Import")]
    [Tooltip("오디오 파일 가져오기 버튼")]
    [SerializeField] private Button importAudioButton;

    [Header("Empty State")]
    [Tooltip("녹음이 없을 때 표시할 패널 (선택사항)")]
    [SerializeField] private GameObject emptyStatePanel;

    [Header("Recording Item UI Names")]
    [Tooltip("녹음 아이템 프리팹 내부의 재생 버튼 이름")]
    [SerializeField] private string playButtonName = "PlayButton";

    [Tooltip("녹음 아이템 프리팹 내부의 제목 텍스트 이름")]
    [SerializeField] private string titleTextName = "TitleText";

    [Tooltip("녹음 아이템 프리팹 내부의 시간 텍스트 이름")]
    [SerializeField] private string durationTextName = "DurationText";

    [Tooltip("녹음 아이템 프리팹 내부의 삭제 버튼 이름 (선택사항)")]
    [SerializeField] private string deleteButtonName = "DeleteButton";

    private List<AudioClip> loadedRecordings = new List<AudioClip>();
    private int currentRecordingIndex = 0;
    private bool isPlaying = false;
    private bool isDraggingSlider = false;

    // 녹음 아이템 리스트
    private List<RecordingItem> recordingItems = new List<RecordingItem>();

    // 녹음 아이템 데이터 구조
    private class RecordingItem
    {
        public GameObject gameObject;
        public Button playButton;
        public Button deleteButton;
        public TMP_Text titleText;
        public TMP_Text durationText;
        public TMP_Text playButtonText;
        public int index;
        public AudioClip clip;
        public string filePath; // 파일 경로 추가
    }

    protected override void Start()
    {
        base.Start();

        // AudioSource 자동 설정
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        // 오디오 가져오기 버튼 설정 (항상 활성화)
        if (importAudioButton != null)
        {
            importAudioButton.onClick.AddListener(ImportAudioFromGallery);
            Debug.Log("[VoiceMemoViewer] Import button connected!");
        }
        else
        {
            Debug.LogWarning("[VoiceMemoViewer] Import Audio Button is not assigned in Inspector!");
        }

        // 진행 바 슬라이더 설정 (있으면)
        if (progressSlider != null)
        {
            progressSlider.onValueChanged.AddListener(OnSliderValueChanged);
        }

        // 음성 메모 전용 데이터 표시
        DisplayVoiceMemoData();

        // 초기 상태 로그
        Debug.Log($"[VoiceMemoViewer] Started. Memo data: {currentMemoData != null}");
        Debug.Log($"[VoiceMemoViewer] Recording paths: {currentMemoData?.voiceRecordingPaths?.Count ?? 0}");
    }

    private void Update()
    {
        // 재생 중일 때 UI 업데이트
        if (isPlaying && audioSource != null && audioSource.clip != null)
        {
            UpdatePlaybackUI();
        }
    }

    /// <summary>
    /// NativeGallery로 오디오 파일 가져오기
    /// </summary>
    public void ImportAudioFromGallery()
    {
        Debug.Log("[VoiceMemoViewer] ImportAudioFromGallery called");

        // 오디오 파일 선택
        NativeGallery.GetAudioFromGallery((path) =>
        {
            Debug.Log($"[VoiceMemoViewer] Callback received, path: {path}");

            if (!string.IsNullOrEmpty(path))
            {
                Debug.Log($"[VoiceMemoViewer] Selected audio path: {path}");
                StartCoroutine(ImportAndSaveAudio(path));
            }
            else
            {
                Debug.Log("[VoiceMemoViewer] Audio selection cancelled or failed");
            }
        }, "오디오 파일 선택", "audio/*");

        Debug.Log("[VoiceMemoViewer] GetAudioFromGallery executed");
    }

    /// <summary>
    /// 외부 오디오 파일을 가져와서 저장
    /// </summary>
    private IEnumerator ImportAndSaveAudio(string sourcePath)
    {
        if (!File.Exists(sourcePath))
        {
            Debug.LogError($"[VoiceMemoViewer] File not found: {sourcePath}");
            yield break;
        }

        // 파일 정보
        string fileName = Path.GetFileName(sourcePath);
        string extension = Path.GetExtension(sourcePath).ToLower();

        Debug.Log($"[VoiceMemoViewer] Importing file: {fileName}");
        Debug.Log($"[VoiceMemoViewer] Extension: {extension}");

        // 저장 경로 생성
        string saveFolderPath = Path.Combine(Application.persistentDataPath, "VoiceRecordings");

        if (!Directory.Exists(saveFolderPath))
        {
            Directory.CreateDirectory(saveFolderPath);
            Debug.Log($"[VoiceMemoViewer] Created directory: {saveFolderPath}");
        }

        // 타임스탬프 추가하여 고유한 파일명 생성
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string newFileName = $"recording_{timestamp}{extension}";
        string targetPath = Path.Combine(saveFolderPath, newFileName);

        // 파일 복사
        try
        {
            File.Copy(sourcePath, targetPath, true);
            Debug.Log($"[VoiceMemoViewer] Audio copied to: {targetPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[VoiceMemoViewer] Failed to copy file: {e.Message}");
            yield break;
        }

        // MemoData에 경로 추가
        if (currentMemoData != null)
        {
            if (currentMemoData.voiceRecordingPaths == null)
            {
                currentMemoData.voiceRecordingPaths = new List<string>();
            }

            // 상대 경로로 저장
            string relativePath = Path.Combine("VoiceRecordings", newFileName);
            currentMemoData.voiceRecordingPaths.Add(relativePath);

            Debug.Log($"[VoiceMemoViewer] Added to memo data: {relativePath}");

            // TODO: MemoData 저장 - MemoManager에 따라 구현 필요
            // SaveCurrentMemoData();
        }

        // 오디오 로드
        yield return StartCoroutine(LoadSingleAudioAndAddToList(targetPath));

        // UI 갱신
        UpdateRecordingCount();
    }

    /// <summary>
    /// 단일 오디오 파일 로드 후 리스트에 추가
    /// </summary>
    private IEnumerator LoadSingleAudioAndAddToList(string path)
    {
        string extension = Path.GetExtension(path).ToLower();
        AudioType audioType = GetAudioType(extension);

        Debug.Log($"[VoiceMemoViewer] Loading audio: {path}");
        Debug.Log($"[VoiceMemoViewer] AudioType: {audioType}");

        string url = "file://" + path;

        using (UnityEngine.Networking.UnityWebRequest www =
               UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(url, audioType))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                AudioClip clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(www);
                if (clip != null)
                {
                    clip.name = Path.GetFileNameWithoutExtension(path);
                    loadedRecordings.Add(clip);

                    Debug.Log($"[VoiceMemoViewer] Successfully loaded: {clip.name}, Length: {clip.length}s");

                    // UI 아이템 생성
                    CreateRecordingItem(loadedRecordings.Count - 1, clip, path);
                }
                else
                {
                    Debug.LogError($"[VoiceMemoViewer] AudioClip is null");
                }
            }
            else
            {
                Debug.LogError($"[VoiceMemoViewer] Failed to load: {www.error}");
                Debug.LogError($"[VoiceMemoViewer] Response code: {www.responseCode}");
            }
        }
    }

    /// <summary>
    /// 확장자에 따른 AudioType 반환
    /// </summary>
    private AudioType GetAudioType(string extension)
    {
        switch (extension)
        {
            case ".mp3":
                return AudioType.MPEG;
            case ".wav":
                return AudioType.WAV;
            case ".ogg":
                return AudioType.OGGVORBIS;
            case ".m4a":
            case ".mp4":
                return AudioType.MPEG; // MP4 오디오
            case ".aac":
                return AudioType.ACC;
            default:
                Debug.LogWarning($"[VoiceMemoViewer] Unknown audio type: {extension}, trying MPEG");
                return AudioType.MPEG; // 기본값
        }
    }

    /// <summary>
    /// 음성 메모 전용 데이터 표시
    /// </summary>
    private void DisplayVoiceMemoData()
    {
        if (currentMemoData == null)
        {
            Debug.LogWarning("[VoiceMemoViewer] No memo data to display!");

            // 녹음 개수 표시
            if (recordingCountText != null)
            {
                recordingCountText.text = "메모 데이터 없음";
            }
            return;
        }

        // 녹음 개수 표시 (항상 업데이트)
        UpdateRecordingCount();

        // 녹음 경로 목록 확인
        if (currentMemoData.voiceRecordingPaths == null || currentMemoData.voiceRecordingPaths.Count == 0)
        {
            Debug.Log("[VoiceMemoViewer] No voice recordings found in memo data. Ready to import.");

            // 빈 리스트로 초기화
            if (currentMemoData.voiceRecordingPaths == null)
            {
                currentMemoData.voiceRecordingPaths = new List<string>();
            }

            // 여기서 return 하지 않음! UI는 계속 표시되어야 함
            return;
        }

        // 녹음 파일 로드 (코루틴 시작)
        StartCoroutine(LoadAllRecordingsAndCreateList());
    }

    /// <summary>
    /// 녹음 개수 텍스트 업데이트
    /// </summary>
    private void UpdateRecordingCount()
    {
        if (recordingCountText != null)
        {
            int count = currentMemoData?.voiceRecordingPaths?.Count ?? 0;
            recordingCountText.text = count > 0 ? $"녹음: {count}개" : "녹음 없음";
        }

        // 빈 상태 패널 표시/숨김 (있으면)
        if (emptyStatePanel != null)
        {
            int count = currentMemoData?.voiceRecordingPaths?.Count ?? 0;
            emptyStatePanel.SetActive(count == 0);
        }
    }

    /// <summary>
    /// 모든 녹음 파일 로드 후 리스트 UI 생성
    /// </summary>
    private IEnumerator LoadAllRecordingsAndCreateList()
    {
        loadedRecordings.Clear();
        ClearRecordingItems();

        // 모든 녹음 파일 로드
        for (int i = 0; i < currentMemoData.voiceRecordingPaths.Count; i++)
        {
            string recordingPath = currentMemoData.voiceRecordingPaths[i];

            if (string.IsNullOrEmpty(recordingPath))
            {
                Debug.LogWarning("[VoiceMemoViewer] Empty recording path, skipping");
                continue;
            }

            // 전체 경로 생성
            string fullPath = recordingPath;

            // 상대 경로인 경우 persistentDataPath와 결합
            if (!Path.IsPathRooted(recordingPath))
            {
                fullPath = Path.Combine(Application.persistentDataPath, recordingPath);
            }

            if (verboseDebug)
            {
                Debug.Log($"[VoiceMemoViewer] Loading recording from: {fullPath}");
            }

            // 파일 존재 확인
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning($"[VoiceMemoViewer] Recording file not found: {fullPath}");
                continue;
            }

            // 오디오 클립 로드
            yield return StartCoroutine(LoadAudioClipCoroutine(fullPath, i));
        }

        if (verboseDebug)
        {
            Debug.Log($"[VoiceMemoViewer] Loaded {loadedRecordings.Count} recordings");
        }
    }

    /// <summary>
    /// 오디오 클립을 비동기로 로드
    /// </summary>
    private IEnumerator LoadAudioClipCoroutine(string path, int index)
    {
        string extension = Path.GetExtension(path).ToLower();
        AudioType audioType = GetAudioType(extension);

        string url = "file://" + path;

        using (UnityEngine.Networking.UnityWebRequest www =
               UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(url, audioType))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                AudioClip clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(www);
                if (clip != null)
                {
                    clip.name = Path.GetFileNameWithoutExtension(path);
                    loadedRecordings.Add(clip);

                    if (verboseDebug)
                    {
                        Debug.Log($"[VoiceMemoViewer] Successfully loaded recording: {path}");
                    }

                    // UI 아이템 생성
                    CreateRecordingItem(loadedRecordings.Count - 1, clip, path);
                }
            }
            else
            {
                Debug.LogError($"[VoiceMemoViewer] Failed to load recording: {www.error}");
            }
        }
    }

    /// <summary>
    /// 개별 녹음 아이템 UI 생성
    /// </summary>
    private void CreateRecordingItem(int index, AudioClip clip, string filePath)
    {
        if (recordingItemPrefab == null || recordingListContainer == null)
        {
            Debug.LogError("[VoiceMemoViewer] recordingItemPrefab or recordingListContainer is null!");
            return;
        }

        GameObject itemObj = Instantiate(recordingItemPrefab, recordingListContainer);

        RecordingItem item = new RecordingItem
        {
            gameObject = itemObj,
            index = index,
            clip = clip,
            filePath = filePath
        };

        // 디버그: 프리팹 구조 확인
        if (verboseDebug)
        {
            Debug.Log($"[VoiceMemoViewer] Creating item {index}, children:");
            foreach (Transform child in itemObj.transform)
            {
                Debug.Log($"  - {child.name}");
            }
        }

        // 재생 버튼 찾기
        Transform playButtonTransform = itemObj.transform.Find(playButtonName);
        if (playButtonTransform != null)
        {
            item.playButton = playButtonTransform.GetComponent<Button>();
            if (item.playButton != null)
            {
                int capturedIndex = index;
                item.playButton.onClick.AddListener(() => OnRecordingItemClicked(capturedIndex));

                item.playButtonText = item.playButton.GetComponentInChildren<TMP_Text>();
                if (item.playButtonText != null)
                {
                    item.playButtonText.text = "▶️ 재생";
                }
            }
        }
        else
        {
            // 프리팹 자체가 버튼일 수도 있음
            item.playButton = itemObj.GetComponent<Button>();
            if (item.playButton != null)
            {
                int capturedIndex = index;
                item.playButton.onClick.AddListener(() => OnRecordingItemClicked(capturedIndex));
                item.playButtonText = item.playButton.GetComponentInChildren<TMP_Text>();
                if (item.playButtonText != null)
                {
                    item.playButtonText.text = "▶️ 재생";
                }
            }
        }

        // 삭제 버튼 찾기 (선택사항)
        Transform deleteButtonTransform = itemObj.transform.Find(deleteButtonName);
        if (deleteButtonTransform != null)
        {
            item.deleteButton = deleteButtonTransform.GetComponent<Button>();
            if (item.deleteButton != null)
            {
                int capturedIndex = index;
                item.deleteButton.onClick.AddListener(() => DeleteRecording(capturedIndex));
            }
        }

        // 제목 텍스트 찾기
        Transform titleTransform = itemObj.transform.Find(titleTextName);
        if (titleTransform != null)
        {
            item.titleText = titleTransform.GetComponent<TMP_Text>();
            if (item.titleText != null)
            {
                item.titleText.text = $"녹음 {index + 1}";
            }
        }

        // 시간 텍스트 찾기
        Transform durationTransform = itemObj.transform.Find(durationTextName);
        if (durationTransform != null)
        {
            item.durationText = durationTransform.GetComponent<TMP_Text>();
            if (item.durationText != null && clip != null)
            {
                item.durationText.text = FormatTime(clip.length);
            }
        }

        recordingItems.Add(item);

        if (verboseDebug)
        {
            Debug.Log($"[VoiceMemoViewer] Created recording item {index + 1}: {FormatTime(clip.length)}");
        }
    }

    /// <summary>
    /// 녹음 삭제
    /// </summary>
    private void DeleteRecording(int index)
    {
        if (index < 0 || index >= recordingItems.Count) return;

        var item = recordingItems[index];

        // 현재 재생 중이면 정지
        if (currentRecordingIndex == index && audioSource.clip == item.clip)
        {
            audioSource.Stop();
            isPlaying = false;
            if (currentPlayingText != null)
            {
                currentPlayingText.text = "";
            }
        }

        // 파일 삭제
        if (!string.IsNullOrEmpty(item.filePath) && File.Exists(item.filePath))
        {
            try
            {
                File.Delete(item.filePath);
                Debug.Log($"[VoiceMemoViewer] Deleted file: {item.filePath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[VoiceMemoViewer] Failed to delete file: {e.Message}");
            }
        }

        // MemoData에서 제거
        if (currentMemoData != null && currentMemoData.voiceRecordingPaths != null)
        {
            string relativePath = item.filePath.Replace(Application.persistentDataPath + Path.DirectorySeparatorChar, "");
            currentMemoData.voiceRecordingPaths.Remove(relativePath);

            // TODO: MemoData 저장
            // SaveCurrentMemoData();
        }

        // UI 제거
        Destroy(item.gameObject);
        recordingItems.RemoveAt(index);
        loadedRecordings.RemoveAt(index);

        // 인덱스 재조정
        for (int i = 0; i < recordingItems.Count; i++)
        {
            recordingItems[i].index = i;
            if (recordingItems[i].titleText != null)
            {
                recordingItems[i].titleText.text = $"녹음 {i + 1}";
            }
        }

        // 녹음 개수 업데이트
        UpdateRecordingCount();

        Debug.Log($"[VoiceMemoViewer] Deleted recording {index}");
    }

    /// <summary>
    /// 녹음 아이템 클릭 시
    /// </summary>
    private void OnRecordingItemClicked(int index)
    {
        if (index < 0 || index >= loadedRecordings.Count) return;

        // 같은 녹음을 재생 중이면 일시정지/재생 토글
        if (currentRecordingIndex == index && audioSource.clip == loadedRecordings[index])
        {
            TogglePlayPause();
        }
        else
        {
            // 다른 녹음으로 전환
            PlayRecording(index);
        }
    }

    /// <summary>
    /// 특정 녹음 재생
    /// </summary>
    private void PlayRecording(int index)
    {
        if (loadedRecordings.Count == 0 || index < 0 || index >= loadedRecordings.Count) return;

        // 이전 녹음 정지
        if (audioSource != null)
        {
            audioSource.Stop();
        }

        currentRecordingIndex = index;
        audioSource.clip = loadedRecordings[index];
        audioSource.time = 0f;
        audioSource.Play();
        isPlaying = true;

        // 모든 버튼 텍스트 업데이트
        UpdateAllPlayButtonTexts();

        // 현재 재생 중인 녹음 표시
        if (currentPlayingText != null)
        {
            currentPlayingText.text = $"재생 중: 녹음 {index + 1}";
        }

        if (verboseDebug)
        {
            Debug.Log($"[VoiceMemoViewer] Playing recording {index + 1}");
        }
    }

    /// <summary>
    /// 재생/일시정지 토글
    /// </summary>
    public void TogglePlayPause()
    {
        if (audioSource == null || audioSource.clip == null) return;

        if (isPlaying)
        {
            audioSource.Pause();
            isPlaying = false;
        }
        else
        {
            // 완전히 끝났으면 처음부터
            if (audioSource.time >= audioSource.clip.length - 0.1f)
            {
                audioSource.time = 0f;
            }

            audioSource.Play();
            isPlaying = true;
        }

        UpdateAllPlayButtonTexts();

        if (verboseDebug)
        {
            Debug.Log($"[VoiceMemoViewer] Playback {(isPlaying ? "resumed" : "paused")}");
        }
    }

    /// <summary>
    /// 모든 재생 버튼 텍스트 업데이트
    /// </summary>
    private void UpdateAllPlayButtonTexts()
    {
        for (int i = 0; i < recordingItems.Count; i++)
        {
            var item = recordingItems[i];
            if (item.playButtonText == null) continue;

            if (i == currentRecordingIndex && audioSource.clip == item.clip)
            {
                // 현재 재생 중인 녹음
                item.playButtonText.text = isPlaying ? "⏸️ 일시정지" : "▶️ 재생";
            }
            else
            {
                // 다른 녹음들
                item.playButtonText.text = "▶️ 재생";
            }
        }
    }

    /// <summary>
    /// 녹음 아이템들 삭제
    /// </summary>
    private void ClearRecordingItems()
    {
        foreach (var item in recordingItems)
        {
            if (item.gameObject != null)
            {
                Destroy(item.gameObject);
            }
        }
        recordingItems.Clear();
    }

    /// <summary>
    /// 재생 UI 업데이트 (시간, 진행 바)
    /// </summary>
    private void UpdatePlaybackUI()
    {
        if (audioSource.clip == null) return;

        float currentTime = audioSource.time;
        float totalTime = audioSource.clip.length;

        // 시간 표시
        if (playbackTimeText != null)
        {
            playbackTimeText.text = $"{FormatTime(currentTime)} / {FormatTime(totalTime)}";
        }

        // 진행 바 업데이트 (사용자가 드래그 중이 아닐 때만)
        if (progressSlider != null && !isDraggingSlider)
        {
            progressSlider.value = currentTime / totalTime;
        }

        // 재생 완료 체크
        if (!audioSource.isPlaying && isPlaying)
        {
            isPlaying = false;
            audioSource.time = 0f;
            if (progressSlider != null)
            {
                progressSlider.value = 0f;
            }
            UpdateAllPlayButtonTexts();

            if (verboseDebug)
            {
                Debug.Log("[VoiceMemoViewer] Playback completed, reset to start");
            }
        }
    }

    /// <summary>
    /// 슬라이더 값 변경 시
    /// </summary>
    private void OnSliderValueChanged(float value)
    {
        if (isDraggingSlider && audioSource != null && audioSource.clip != null)
        {
            audioSource.time = value * audioSource.clip.length;
        }
    }

    /// <summary>
    /// 슬라이더 드래그 시작
    /// </summary>
    public void OnSliderBeginDrag()
    {
        isDraggingSlider = true;
    }

    /// <summary>
    /// 슬라이더 드래그 종료
    /// </summary>
    public void OnSliderEndDrag()
    {
        isDraggingSlider = false;
    }

    /// <summary>
    /// 시간을 MM:SS 형식으로 변환
    /// </summary>
    private string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
        return $"{minutes:00}:{seconds:00}";
    }

    private void OnDestroy()
    {
        // 녹음 아이템 정리
        ClearRecordingItems();

        // 오디오 클립 메모리 해제
        foreach (var clip in loadedRecordings)
        {
            if (clip != null)
            {
                Destroy(clip);
            }
        }
        loadedRecordings.Clear();
    }

    // 디버깅용 임시 버튼 (OnGUI)
    void OnGUI()
    {
        // 화면 제일 위에 테스트 버튼 표시
        if (GUI.Button(new Rect(10, 10, 200, 80), "오디오 가져오기\n(테스트)"))
        {
            Debug.Log("GUI 테스트 버튼 클릭됨!");
            ImportAudioFromGallery();
        }

        // 녹음 개수 표시
        int count = loadedRecordings?.Count ?? 0;
        GUI.Label(new Rect(10, 100, 300, 30), $"로드된 녹음: {count}개");

        // 버튼 연결 상태
        GUI.Label(new Rect(10, 130, 400, 30), $"Import Button: {(importAudioButton != null ? "연결됨" : "없음")}");
    }
}