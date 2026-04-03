using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// 음성메모 아이템의 녹음 UI를 제어하는 클래스
/// Android 네이티브 녹음 앱(Galaxy 음성 녹음)을 호출하여 녹음/재생 UI 제공
/// </summary>
public class VoiceRecorderUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("녹음 버튼 (Android 네이티브 녹음 앱 실행)")]
    [SerializeField] private Button recordButton;
    
    [Tooltip("재생 버튼 (녹음된 파일 재생)")]
    [SerializeField] private Button playButton;
    
    [Tooltip("삭제 버튼 (녹음된 파일 삭제)")]
    [SerializeField] private Button deleteButton;
    
    [Tooltip("녹음 상태 표시 텍스트")]
    [SerializeField] private TMP_Text statusText;
    
    [Tooltip("녹음 버튼 아이콘")]
    [SerializeField] private Image recordButtonIcon;
    
    [Header("Icons")]
    [Tooltip("녹음 대기 아이콘")]
    [SerializeField] private Sprite recordIdleIcon;
    
    [Header("Colors")]
    [SerializeField] private Color idleColor = Color.white;
    [SerializeField] private Color hasRecordingColor = new Color(0.2f, 0.8f, 0.2f); // 녹음 완료 시 초록색
    
    // AndroidVoiceRecorder 참조
    private AndroidVoiceRecorder androidRecorder;
    private AudioSource audioSource;
    
    // 녹음 파일 경로
    private string recordedFilePath;
    
    // 재생 중 상태
    private bool isPlaying = false;
    
    // 아이템 번호 (음성 녹음1, 2, 3...)
    private int itemNumber = 1;
    
    // 이 인스턴스가 녹음을 시작했는지 추적
    private bool isRecordingActive = false;
    
    /// <summary>
    /// 아이템 번호 설정
    /// </summary>
    public void SetItemNumber(int number)
    {
        itemNumber = number;
        Debug.Log($"[VoiceRecorderUI] 아이템 번호 설정: {itemNumber}");
    }
    
    void Awake()
    {
        // AndroidVoiceRecorder 찾기 또는 생성
        androidRecorder = FindObjectOfType<AndroidVoiceRecorder>();
        if (androidRecorder == null)
        {
            GameObject recorderObj = new GameObject("AndroidVoiceRecorder");
            androidRecorder = recorderObj.AddComponent<AndroidVoiceRecorder>();
            DontDestroyOnLoad(recorderObj); // 씬 전환 시에도 유지
            Debug.Log("[VoiceRecorderUI] AndroidVoiceRecorder 오브젝트 생성");
        }
        
        // AudioSource 추가 (재생용)
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }
    
    void Start()
    {
        // 버튼 이벤트 연결
        if (recordButton != null)
        {
            recordButton.onClick.AddListener(OnRecordButtonClicked);
        }
        else
        {
            Debug.LogWarning("[VoiceRecorderUI] recordButton이 연결되지 않았습니다!");
        }
        
        if (playButton != null)
        {
            playButton.onClick.AddListener(OnPlayButtonClicked);
            playButton.gameObject.SetActive(false); // 초기에는 숨김
        }
        
        if (deleteButton != null)
        {
            deleteButton.onClick.AddListener(OnDeleteButtonClicked);
            deleteButton.gameObject.SetActive(true); // ★ 항상 표시
        }
        
        // AndroidVoiceRecorder 이벤트 연결
        if (androidRecorder != null)
        {
            androidRecorder.OnRecordingCompleted += OnRecordingCompleted;
            androidRecorder.OnRecordingCancelled += OnRecordingCancelled;
        }
        
        // 초기 UI 상태 설정
        UpdateUI();
    }
    
    void Update()
    {
        // 재생 중일 때 재생 완료 확인
        if (isPlaying && audioSource != null && !audioSource.isPlaying)
        {
            isPlaying = false;
            if (statusText != null)
                statusText.text = "재생 완료";
        }
    }
    
    /// <summary>
    /// 녹음 버튼 클릭 핸들러 - Android 네이티브 녹음 앱 실행
    /// </summary>
    private void OnRecordButtonClicked()
    {
        Debug.Log($"[VoiceRecorderUI] ★★★ 녹음 버튼 클릭됨! (아이템 번호: {itemNumber})");
        Debug.Log($"[VoiceRecorderUI] isRecordingActive 현재 상태: {isRecordingActive}");
        
        if (androidRecorder == null)
        {
            Debug.LogError("[VoiceRecorderUI] ✗ AndroidVoiceRecorder가 null입니다!");
            
            // AndroidVoiceRecorder 다시 찾기 시도
            androidRecorder = FindObjectOfType<AndroidVoiceRecorder>();
            if (androidRecorder == null)
            {
                Debug.LogError("[VoiceRecorderUI] ✗ AndroidVoiceRecorder를 찾을 수 없습니다! 생성합니다...");
                GameObject recorderObj = new GameObject("AndroidVoiceRecorder");
                androidRecorder = recorderObj.AddComponent<AndroidVoiceRecorder>();
                DontDestroyOnLoad(recorderObj);
                
                // 이벤트 다시 연결
                androidRecorder.OnRecordingCompleted += OnRecordingCompleted;
                androidRecorder.OnRecordingCancelled += OnRecordingCancelled;
            }
        }
        
        Debug.Log($"[VoiceRecorderUI] ✓ AndroidVoiceRecorder 찾음: {androidRecorder.name}");
        
        // 이 인스턴스가 이미 녹음 완료 상태가 아니라면, AndroidVoiceRecorder 상태 초기화
        // (다른 VoiceItem이 녹음했을 수 있으므로)
        if (!isRecordingActive && string.IsNullOrEmpty(recordedFilePath))
        {
            Debug.Log($"[VoiceRecorderUI] ★★★ 새 녹음 시작. AndroidVoiceRecorder 상태 초기화! (아이템 번호: {itemNumber})");
            androidRecorder.ResetState();
        }
        
        // 이 인스턴스가 녹음을 시작함을 표시
        isRecordingActive = true;
        Debug.Log($"[VoiceRecorderUI] ★★★ isRecordingActive를 true로 설정! (아이템 번호: {itemNumber})");
        
        // Android 네이티브 녹음 앱 실행 (Galaxy 음성 녹음)
        Debug.Log("[VoiceRecorderUI] StartNativeRecording() 호출 중...");
        androidRecorder.StartNativeRecording();
        
        if (statusText != null)
            statusText.text = "녹음 앱 실행 중...";
        
        Debug.Log("[VoiceRecorderUI] ✓ StartNativeRecording() 호출 완료");
    }
    
    /// <summary>
    /// 재생 버튼 클릭 핸들러
    /// </summary>
    private void OnPlayButtonClicked()
    {
        if (string.IsNullOrEmpty(recordedFilePath))
        {
            Debug.LogWarning("[VoiceRecorderUI] 재생할 파일이 없습니다!");
            return;
        }
        
        if (isPlaying)
        {
            // 재생 중지
            audioSource.Stop();
            isPlaying = false;
            if (statusText != null)
                statusText.text = "재생 중지";
            Debug.Log("[VoiceRecorderUI] 재생 중지");
        }
        else
        {
            // 재생 시작
            StartCoroutine(LoadAndPlayAudio(recordedFilePath));
        }
    }
    
    /// <summary>
    /// 삭제 버튼 클릭 핸들러 - 상태 초기화
    /// </summary>
    private void OnDeleteButtonClicked()
    {
        // 녹음 파일 삭제 (voice_status는 실제 파일이 아니므로 삭제하지 않음)
        if (!string.IsNullOrEmpty(recordedFilePath) && recordedFilePath != "voice_status")
        {
            if (androidRecorder != null)
            {
                androidRecorder.DeleteRecording(recordedFilePath);
            }
        }
        
        // 경로 초기화
        recordedFilePath = null;
        
        // 녹음 활성 상태 해제
        isRecordingActive = false;
        
        // AndroidVoiceRecorder 상태 초기화
        if (androidRecorder != null)
        {
            androidRecorder.ResetState();
        }
        
        // UI 상태 초기화
        if (statusText != null)
        {
            statusText.text = "음성을 녹음해주세요";
            // 텍스트 색상도 원래대로
            statusText.color = UnityEngine.Color.white;
        }
        
        UpdateUI();
        Debug.Log("[VoiceRecorderUI] 상태 초기화 완료");
    }
    
    /// <summary>
    /// 녹음 완료 이벤트 핸들러
    /// </summary>
    private void OnRecordingCompleted(string filePath)
    {
        recordedFilePath = filePath;
        
        // 파일명 추출
        string fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
        
        if (statusText != null)
            statusText.text = fileName; // 녹음 앱에서 지정한 파일명 표시
        
        UpdateUI();
        Debug.Log($"[VoiceRecorderUI] ✓ 녹음 완료: {fileName}");
    }
    
    /// <summary>
    /// 녹음 취소 이벤트 핸들러 (녹음 앱에서 돌아왔을 때)
    /// </summary>
    private void OnRecordingCancelled()
    {
        Debug.Log($"[VoiceRecorderUI] ★★★ OnRecordingCancelled 호출됨! (아이템 번호: {itemNumber}, isRecordingActive: {isRecordingActive})");
        
        // 이 인스턴스가 녹음을 시작한 경우에만 처리
        if (!isRecordingActive)
        {
            Debug.Log($"[VoiceRecorderUI] 다른 인스턴스의 녹음. 무시. (아이템 번호: {itemNumber})");
            return;
        }
        
        Debug.Log($"[VoiceRecorderUI] ★★★ 이 인스턴스가 녹음을 시작함. 상태 업데이트 진행. (아이템 번호: {itemNumber})");
        
        // 녹음 활성 상태 해제
        isRecordingActive = false;
        
        // 녹음 앱에서 돌아왔을 때 자동으로 "음성 녹음X" 표시
        if (statusText != null)
        {
            statusText.text = $"음성 녹음{itemNumber}";
            // 텍스트 색상을 검은색으로 설정
            statusText.color = UnityEngine.Color.black;
            Debug.Log($"[VoiceRecorderUI] ★★★ statusText 업데이트 완료: {statusText.text}, 색상: 검은색");
        }
        else
        {
            Debug.LogError($"[VoiceRecorderUI] statusText가 null입니다! (아이템 번호: {itemNumber})");
        }
        
        // UpdateUI 호출하지 않음 (텍스트가 덮어씌워지는 것 방지)
        // 버튼 상태만 수동으로 업데이트
        if (playButton != null)
            playButton.gameObject.SetActive(false);
        if (deleteButton != null)
            deleteButton.gameObject.SetActive(true);
        
        Debug.Log($"[VoiceRecorderUI] 녹음 앱 복귀. 텍스트 변경: 음성 녹음{itemNumber}");
    }
    
    /// <summary>
    /// 오디오 파일 로드 및 재생
    /// </summary>
    private System.Collections.IEnumerator LoadAndPlayAudio(string filePath)
    {
        if (audioSource == null)
        {
            Debug.LogError("[VoiceRecorderUI] AudioSource가 null입니다!");
            yield break;
        }
        
        // WAV 파일 로드
        string url = "file:///" + filePath.Replace("\\", "/");
        using (UnityEngine.Networking.UnityWebRequest www = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
        {
            yield return www.SendWebRequest();
            
            if (www.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                AudioClip clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(www);
                audioSource.clip = clip;
                audioSource.Play();
                isPlaying = true;
                
                if (statusText != null)
                    statusText.text = "재생 중...";
                
                Debug.Log($"[VoiceRecorderUI] 재생 시작: {filePath}");
            }
            else
            {
                Debug.LogError($"[VoiceRecorderUI] 오디오 로드 실패: {www.error}");
                
                if (statusText != null)
                    statusText.text = "재생 실패";
            }
        }
    }
    
    /// <summary>
    /// UI 상태 업데이트
    /// </summary>
    private void UpdateUI()
    {
        Debug.Log($"[🔍TRACE] [VoiceRecorderUI] UpdateUI 호출됨! (아이템 번호: {itemNumber})");
        
        bool hasRecording = !string.IsNullOrEmpty(recordedFilePath);
        bool isVoiceStatus = recordedFilePath == "voice_status"; // "음성 녹음X" 상태
        
        Debug.Log($"[🔍TRACE] [VoiceRecorderUI] hasRecording: {hasRecording}, isVoiceStatus: {isVoiceStatus}, recordedFilePath: '{recordedFilePath}'");
        
        // 녹음 버튼 아이콘 색상 변경 (녹음 파일이 있으면 초록색)
        if (recordButtonIcon != null && recordIdleIcon != null)
        {
            recordButtonIcon.sprite = recordIdleIcon;
            recordButtonIcon.color = hasRecording ? hasRecordingColor : idleColor;
        }
        
        // 재생 버튼 표시 상태 (실제 녹음 파일이 있을 때만)
        if (playButton != null)
            playButton.gameObject.SetActive(hasRecording && !isVoiceStatus);
        
        // 삭제 버튼은 항상 표시
        if (deleteButton != null)
            deleteButton.gameObject.SetActive(true);
        
        // 상태 텍스트 (녹음이 없고 voice_status도 아닐 때만 기본 텍스트 표시)
        if (statusText != null && !hasRecording)
        {
            Debug.Log($"[🔍TRACE] [VoiceRecorderUI] ⚠️ statusText를 '음성을 녹음해주세요'로 덮어쓰기!");
            statusText.text = "음성을 녹음해주세요";
        }
        else if (isVoiceStatus)
        {
            Debug.Log($"[🔍TRACE] [VoiceRecorderUI] ✓ voice_status 상태. statusText 유지: '{statusText?.text}'");
        }
    }
    
    /// <summary>
    /// 녹음 파일 경로 반환 (외부에서 접근)
    /// </summary>
    public string GetRecordedFilePath()
    {
        return recordedFilePath;
    }
    
    /// <summary>
    /// 녹음 파일 경로 설정 (로드 시)
    /// </summary>
    public void SetRecordedFilePath(string filePath)
    {
        recordedFilePath = filePath;
        UpdateUI();
        
        if (!string.IsNullOrEmpty(filePath) && statusText != null)
        {
            // 파일명 추출해서 표시
            string fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
            statusText.text = fileName;
        }
    }
    
    /// <summary>
    /// 상태 텍스트 직접 설정 (로드 시 - "음성 녹음X" 등)
    /// </summary>
    public void SetStatusText(string text)
    {
        Debug.Log($"[🔍TRACE] [VoiceRecorderUI] SetStatusText 시작: '{text}', statusText={(statusText != null ? "존재" : "NULL")}");
        
        if (statusText != null)
        {
            statusText.text = text;
            Debug.Log($"[🔍TRACE] [VoiceRecorderUI] statusText.text 설정 완료: '{statusText.text}'");
            
            // "음성 녹음X" 형태면 검은색으로
            if (text.StartsWith("음성 녹음") && char.IsDigit(text[text.Length - 1]))
            {
                statusText.color = UnityEngine.Color.black;
                Debug.Log($"[🔍TRACE] [VoiceRecorderUI] 텍스트 색상 검은색으로 설정");
                
                // ★ recordedFilePath를 "voice_status"로 설정하여 UpdateUI()가 덮어쓰지 않도록 함
                recordedFilePath = "voice_status";
                Debug.Log($"[🔍TRACE] [VoiceRecorderUI] recordedFilePath를 'voice_status'로 설정 (UpdateUI 보호)");
            }
        }
        else
        {
            Debug.LogError($"[🔍TRACE] [VoiceRecorderUI] statusText가 NULL입니다!");
        }
        
        // 버튼 상태 업데이트
        if (playButton != null)
            playButton.gameObject.SetActive(false);
        if (deleteButton != null)
            deleteButton.gameObject.SetActive(true);
            
        Debug.Log($"[🔍TRACE] [VoiceRecorderUI] SetStatusText 완료: {text}");
    }
    
    /// <summary>
    /// 녹음 여부 확인
    /// </summary>
    public bool HasRecording()
    {
        return !string.IsNullOrEmpty(recordedFilePath);
    }
    
    /// <summary>
    /// 현재 상태 텍스트 반환 (저장용)
    /// </summary>
    public string GetStatusText()
    {
        if (statusText != null)
            return statusText.text;
        return "";
    }
    
    /// <summary>
    /// 녹음 완료 상태인지 확인 ("음성 녹음X" 텍스트)
    /// </summary>
    public bool IsRecordingCompleted()
    {
        return isRecordingActive == false && statusText != null && statusText.text.StartsWith("음성 녹음");
    }
    
    void OnDestroy()
    {
        // 이벤트 해제
        if (androidRecorder != null)
        {
            androidRecorder.OnRecordingCompleted -= OnRecordingCompleted;
            androidRecorder.OnRecordingCancelled -= OnRecordingCancelled;
        }
        
        // 버튼 이벤트 해제
        if (recordButton != null)
            recordButton.onClick.RemoveListener(OnRecordButtonClicked);
        if (playButton != null)
            playButton.onClick.RemoveListener(OnPlayButtonClicked);
        if (deleteButton != null)
            deleteButton.onClick.RemoveListener(OnDeleteButtonClicked);
    }
}
