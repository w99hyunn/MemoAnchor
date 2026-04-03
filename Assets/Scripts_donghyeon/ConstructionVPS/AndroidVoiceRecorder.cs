using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Collections;

/// <summary>
/// Android 네이티브 녹음 앱 실행을 담당하는 클래스
/// Galaxy 음성 녹음 앱을 직접 실행하고 결과 처리
/// </summary>
public class AndroidVoiceRecorder : MonoBehaviour
{
    // 이벤트
    public event Action<string> OnRecordingCompleted;  // 녹음 완료 시 파일 경로 전달
    public event Action OnRecordingCancelled;          // 녹음 취소
    
    private string[] possibleRecordingFolders = new string[]
    {
        "/storage/emulated/0/Recordings",                    // Galaxy 기본
        "/storage/emulated/0/Voice Recorder",                // 일부 기기
        "/storage/emulated/0/Music/Voice Recorder",          // 음악 폴더
        "/storage/emulated/0/Download",                      // 다운로드 폴더
        "/storage/emulated/0/Documents",                     // 문서 폴더
        "/storage/emulated/0/Sounds/Voice Recorder",         // 추가
        "/storage/emulated/0/Audio",                         // 추가
        "/storage/emulated/0/Media/Audio",                   // 추가
        "/storage/emulated/0/DCIM/Voice Recorder",           // 추가 (일부 앱이 DCIM에 저장)
        "/storage/emulated/0/Android/data/com.sec.android.app.voicenote/files" // Galaxy 앱 전용 폴더
    };
    private string lastRecordingPath = "";
    private DateTime recordingStartTime;
    
    void OnApplicationFocus(bool hasFocus)
    {
        Debug.Log($"[AndroidVoiceRecorder] ★★★ OnApplicationFocus 호출됨! hasFocus: {hasFocus}, lastRecordingPath: '{lastRecordingPath}'");
        
        // 앱으로 돌아왔을 때 (녹음 완료 후)
        if (hasFocus && !string.IsNullOrEmpty(lastRecordingPath) && lastRecordingPath == "recording")
        {
            Debug.Log("[AndroidVoiceRecorder] ★ 앱으로 돌아옴. 녹음 진행 중 상태 확인됨.");
            Debug.Log("[AndroidVoiceRecorder] 녹음 완료 상태로 변경. 버튼 클릭 시 녹음 앱 파일 목록으로 이동 가능.");
            
            // 상태를 completed로 변경
            lastRecordingPath = "completed";
            
            // 토스트 메시지 표시
            ShowToast("녹음 완료! 다시 버튼을 눌러 녹음 파일을 확인하세요.");
            
            // OnRecordingCancelled 이벤트 발생 (UI 업데이트용)
            Debug.Log("[AndroidVoiceRecorder] ★★★ OnRecordingCancelled 이벤트 발생!");
            OnRecordingCancelled?.Invoke();
        }
        else
        {
            Debug.Log($"[AndroidVoiceRecorder] 이벤트 발생 조건 불만족. hasFocus: {hasFocus}, lastRecordingPath: '{lastRecordingPath}'");
        }
    }
    
    /// <summary>
    /// Android 토스트 메시지 표시
    /// </summary>
    private void ShowToast(string message)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaClass toastClass = new AndroidJavaClass("android.widget.Toast"))
            {
                AndroidJavaObject context = currentActivity;
                currentActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    using (AndroidJavaObject toast = toastClass.CallStatic<AndroidJavaObject>("makeText", 
                        context, message, 1)) // 1 = LENGTH_LONG
                    {
                        toast.Call("show");
                    }
                }));
            }
            Debug.Log($"[AndroidVoiceRecorder] 토스트 메시지: {message}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AndroidVoiceRecorder] 토스트 표시 실패: {ex.Message}");
        }
#endif
    }
    
    /// <summary>
    /// 녹음 후 새 파일 확인 (MediaStore API 사용)
    /// </summary>
    private void CheckForNewRecording()
    {
        try
        {
            Debug.Log("[AndroidVoiceRecorder] === 녹음 파일 검색 시작 (MediaStore API 사용) ===");
            Debug.Log($"[AndroidVoiceRecorder] 녹음 시작 시간: {recordingStartTime:yyyy-MM-dd HH:mm:ss}");
            
#if UNITY_ANDROID && !UNITY_EDITOR
            string foundFile = FindLatestAudioFileViaMediaStore();
            
            if (!string.IsNullOrEmpty(foundFile))
            {
                Debug.Log($"[AndroidVoiceRecorder] ✓✓✓ MediaStore에서 녹음 파일 발견!");
                Debug.Log($"[AndroidVoiceRecorder] 파일명: {Path.GetFileName(foundFile)}");
                Debug.Log($"[AndroidVoiceRecorder] 경로: {foundFile}");
                lastRecordingPath = "";
                OnRecordingCompleted?.Invoke(foundFile);
                return;
            }
#endif
            
            // MediaStore에서 못 찾으면 기존 폴더 검색 방법 시도
            Debug.Log("[AndroidVoiceRecorder] MediaStore에서 못 찾음. 직접 폴더 검색 시도...");
            string foundFileByFolder = FindLatestAudioFileByFolder();
            
            if (!string.IsNullOrEmpty(foundFileByFolder))
            {
                Debug.Log($"[AndroidVoiceRecorder] ✓✓✓ 폴더 검색으로 녹음 파일 발견!");
                Debug.Log($"[AndroidVoiceRecorder] 파일명: {Path.GetFileName(foundFileByFolder)}");
                Debug.Log($"[AndroidVoiceRecorder] 경로: {foundFileByFolder}");
                lastRecordingPath = "";
                OnRecordingCompleted?.Invoke(foundFileByFolder);
            }
            else
            {
                Debug.LogWarning("[AndroidVoiceRecorder] ✗ 새 녹음 파일을 찾을 수 없습니다.");
                Debug.LogWarning("[AndroidVoiceRecorder] 가능한 원인:");
                Debug.LogWarning("[AndroidVoiceRecorder] 1. 녹음을 취소했습니다");
                Debug.LogWarning("[AndroidVoiceRecorder] 2. 다른 폴더에 저장되었습니다");
                Debug.LogWarning("[AndroidVoiceRecorder] 3. 파일 권한 문제");
                lastRecordingPath = "";
                OnRecordingCancelled?.Invoke();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AndroidVoiceRecorder] ✗✗✗ 파일 확인 실패: {ex.GetType().Name}");
            Debug.LogError($"[AndroidVoiceRecorder] 에러 메시지: {ex.Message}");
            Debug.LogError($"[AndroidVoiceRecorder] 스택 트레이스: {ex.StackTrace}");
            lastRecordingPath = "";
            OnRecordingCancelled?.Invoke();
        }
    }
    
    /// <summary>
    /// MediaStore API를 통해 최근 오디오 파일 찾기
    /// </summary>
    private string FindLatestAudioFileViaMediaStore()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            Debug.Log("[AndroidVoiceRecorder] [MediaStore] API를 통한 파일 검색 시작");
            
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject contentResolver = currentActivity.Call<AndroidJavaObject>("getContentResolver"))
            {
                // MediaStore.Audio.Media.EXTERNAL_CONTENT_URI
                using (AndroidJavaClass mediaStoreAudio = new AndroidJavaClass("android.provider.MediaStore$Audio$Media"))
                {
                    AndroidJavaObject externalContentUri = mediaStoreAudio.GetStatic<AndroidJavaObject>("EXTERNAL_CONTENT_URI");
                    
                    // 쿼리할 컬럼들
                    string[] projection = new string[] 
                    { 
                        "_id",           // MediaStore.Audio.Media._ID
                        "_data",         // MediaStore.Audio.Media.DATA (파일 경로)
                        "date_added"     // MediaStore.Audio.Media.DATE_ADDED
                    };
                    
                    // 정렬 순서 (최신순)
                    string sortOrder = "date_added DESC";
                    
                    Debug.Log("[AndroidVoiceRecorder] [MediaStore] 쿼리 실행 중...");
                    
                    using (AndroidJavaObject cursor = contentResolver.Call<AndroidJavaObject>("query", 
                        externalContentUri, projection, null, null, sortOrder))
                    {
                        if (cursor != null && cursor.Call<bool>("moveToFirst"))
                        {
                            Debug.Log($"[AndroidVoiceRecorder] [MediaStore] 오디오 파일 발견. 확인 중...");
                            
                            int dataColumnIndex = cursor.Call<int>("getColumnIndexOrThrow", "_data");
                            int dateAddedColumnIndex = cursor.Call<int>("getColumnIndexOrThrow", "date_added");
                            
                            string bestCandidate = null;
                            DateTime bestCandidateTime = DateTime.MinValue;
                            
                            int count = 0;
                            
                            do
                            {
                                string filePath = cursor.Call<string>("getString", dataColumnIndex);
                                long dateAdded = cursor.Call<long>("getLong", dateAddedColumnIndex);
                                
                                // Unix timestamp를 DateTime으로 변환
                                DateTime fileDateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                                    .AddSeconds(dateAdded).ToLocalTime();
                                
                                // ★★★ 디버깅: 모든 파일 경로 출력 (처음 30개) ★★★
                                if (count < 30)
                                {
                                    Debug.Log($"[AndroidVoiceRecorder] [MediaStore] 전체 후보 [{count}]: {Path.GetFileName(filePath)}");
                                    Debug.Log($"[AndroidVoiceRecorder] [MediaStore]   전체 경로: {filePath}");
                                    Debug.Log($"[AndroidVoiceRecorder] [MediaStore]   MediaStore 등록: {fileDateTime:yyyy-MM-dd HH:mm:ss}");
                                }
                                
                                // 실제 파일의 수정 시간 확인
                                if (File.Exists(filePath))
                                {
                                    DateTime fileModifiedTime = File.GetLastWriteTime(filePath);
                                    
                                    if (count < 30)
                                    {
                                        Debug.Log($"[AndroidVoiceRecorder] [MediaStore]   실제 수정 시간: {fileModifiedTime:yyyy-MM-dd HH:mm:ss}");
                                        Debug.Log($"[AndroidVoiceRecorder] [MediaStore]   녹음 시작 시간: {recordingStartTime:yyyy-MM-dd HH:mm:ss}");
                                        Debug.Log($"[AndroidVoiceRecorder] [MediaStore]   시간 차이: {(fileModifiedTime - recordingStartTime).TotalSeconds:F1}초");
                                    }
                                    
                                    // 실제 파일 수정 시간으로 판단 (경로 필터 제거!)
                                    if (fileModifiedTime > recordingStartTime)
                                    {
                                        if (fileModifiedTime > bestCandidateTime)
                                        {
                                            bestCandidate = filePath;
                                            bestCandidateTime = fileModifiedTime;
                                            Debug.Log($"[AndroidVoiceRecorder] [MediaStore] ★★★ 새로운 최적 후보 발견!");
                                            Debug.Log($"[AndroidVoiceRecorder] [MediaStore] ★★★ 파일: {Path.GetFileName(bestCandidate)}");
                                            Debug.Log($"[AndroidVoiceRecorder] [MediaStore] ★★★ 경로: {bestCandidate}");
                                        }
                                    }
                                }
                                
                                count++;
                                if (count >= 50) break; // 50개까지 확인
                                
                            } while (cursor.Call<bool>("moveToNext"));
                            
                            cursor.Call("close");
                            
                            if (bestCandidate != null)
                            {
                                Debug.Log($"[AndroidVoiceRecorder] [MediaStore] ✓✓✓ 최적 파일 선택!");
                                Debug.Log($"[AndroidVoiceRecorder] [MediaStore] 파일: {Path.GetFileName(bestCandidate)}");
                                Debug.Log($"[AndroidVoiceRecorder] [MediaStore] 경로: {bestCandidate}");
                                Debug.Log($"[AndroidVoiceRecorder] [MediaStore] 시간: {bestCandidateTime:yyyy-MM-dd HH:mm:ss}");
                                return bestCandidate;
                            }
                            
                            Debug.Log($"[AndroidVoiceRecorder] [MediaStore] 조건에 맞는 파일 없음 (총 {count}개 확인)");
                        }
                        else
                        {
                            Debug.Log("[AndroidVoiceRecorder] [MediaStore] 오디오 파일이 없음");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AndroidVoiceRecorder] [MediaStore] 에러: {ex.GetType().Name}");
            Debug.LogError($"[AndroidVoiceRecorder] [MediaStore] 메시지: {ex.Message}");
        }
#endif
        return null;
    }
    
    /// <summary>
    /// 폴더 직접 검색으로 최근 오디오 파일 찾기 (기존 방식)
    /// </summary>
    private string FindLatestAudioFileByFolder()
    {
        foreach (string folderPath in possibleRecordingFolders)
        {
            Debug.Log($"[AndroidVoiceRecorder] 폴더 확인 중: {folderPath}");
            
            if (!Directory.Exists(folderPath))
            {
                Debug.Log($"[AndroidVoiceRecorder] ✗ 폴더 없음: {folderPath}");
                continue;
            }
            
            Debug.Log($"[AndroidVoiceRecorder] ✓ 폴더 존재. 파일 검색 중...");
            
            try
            {
                // 모든 파일 나열 (디버깅용)
                var allFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly);
                Debug.Log($"[AndroidVoiceRecorder] 폴더 내 전체 파일: {allFiles.Length}개");
                
                // 오디오 파일만 필터링
                var audioFiles = allFiles.Where(f => {
                    var extension = Path.GetExtension(f).ToLower();
                    return extension == ".m4a" || extension == ".mp3" || 
                           extension == ".wav" || extension == ".3gp" || 
                           extension == ".amr" || extension == ".aac";
                }).ToArray();
                
                Debug.Log($"[AndroidVoiceRecorder] 오디오 파일: {audioFiles.Length}개");
                
                // 각 오디오 파일의 시간 확인
                foreach (var f in audioFiles.Take(10)) // 최대 10개만 로그
                {
                    var creationTime = File.GetCreationTime(f);
                    var modifiedTime = File.GetLastWriteTime(f);
                    Debug.Log($"[AndroidVoiceRecorder]   파일: {Path.GetFileName(f)}");
                    Debug.Log($"[AndroidVoiceRecorder]     생성: {creationTime:yyyy-MM-dd HH:mm:ss}");
                    Debug.Log($"[AndroidVoiceRecorder]     수정: {modifiedTime:yyyy-MM-dd HH:mm:ss}");
                    Debug.Log($"[AndroidVoiceRecorder]     기준: {recordingStartTime:yyyy-MM-dd HH:mm:ss}");
                }
                
                // 녹음 시작 시간 이후에 생성/수정된 파일 찾기
                var files = audioFiles
                    .Where(f => {
                        var creationTime = File.GetCreationTime(f);
                        var modifiedTime = File.GetLastWriteTime(f);
                        
                        // 생성 시간 또는 수정 시간이 녹음 시작 시간 이후인지 확인
                        bool isNew = creationTime > recordingStartTime || modifiedTime > recordingStartTime;
                        
                        if (isNew)
                        {
                            Debug.Log($"[AndroidVoiceRecorder] ★ 조건 만족 파일: {Path.GetFileName(f)}");
                        }
                        
                        return isNew;
                    })
                    .OrderByDescending(f => File.GetLastWriteTime(f)) // 수정 시간 기준 정렬
                    .ToArray();
                
                Debug.Log($"[AndroidVoiceRecorder] 폴더 내 새 오디오 파일: {files.Length}개");
                
                if (files.Length > 0)
                {
                    return files[0];
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AndroidVoiceRecorder] 폴더 검색 에러: {ex.Message}");
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Android 네이티브 녹음 앱 실행
    /// 이미 녹음한 상태면 파일 선택 UI 표시
    /// </summary>
    public void StartNativeRecording()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        Debug.Log("[AndroidVoiceRecorder] ======== StartNativeRecording 시작 ========");
        
        // 이미 녹음 완료 상태면 녹음 앱 파일 목록으로 이동
        if (!string.IsNullOrEmpty(lastRecordingPath) && lastRecordingPath == "completed")
        {
            Debug.Log("[AndroidVoiceRecorder] 녹음 완료 상태. 녹음 앱 파일 목록으로 이동...");
            OpenRecordingApp();
            return;
        }
        
        // 녹음 시작 시간 기록
        recordingStartTime = DateTime.Now;
        lastRecordingPath = "recording"; // 표시자
        
        try
        {
            Debug.Log("[AndroidVoiceRecorder] [1/10] Unity Player 클래스 가져오는 중...");
            
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                Debug.Log("[AndroidVoiceRecorder] [2/10] Unity Player 클래스 로드 성공");
                
                using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    Debug.Log("[AndroidVoiceRecorder] [3/10] Current Activity 가져오기 성공");
                    
                    using (AndroidJavaObject packageManager = currentActivity.Call<AndroidJavaObject>("getPackageManager"))
                    {
                        Debug.Log("[AndroidVoiceRecorder] [4/10] PackageManager 가져오기 성공");
                        Debug.Log("[AndroidVoiceRecorder] === Galaxy 음성 녹음 앱 실행 시도 ===");
                        
                        try
                        {
                            Debug.Log("[AndroidVoiceRecorder] [5/10] Intent 생성 중...");
                            
                            // Galaxy 음성 녹음 앱 직접 실행
                            using (AndroidJavaObject launchIntent = new AndroidJavaObject("android.content.Intent"))
                            {
                                Debug.Log("[AndroidVoiceRecorder] [6/10] Intent 객체 생성 완료");
                                
                                // 정확한 Activity 경로 지정
                                launchIntent.Call<AndroidJavaObject>("setClassName", 
                                    "com.sec.android.app.voicenote", 
                                    "com.sec.android.app.voicenote.main.VNMainActivity");
                                
                                Debug.Log("[AndroidVoiceRecorder] [7/10] ✓ Activity 경로 설정 완료!");
                                
                                // 플래그 설정
                                launchIntent.Call<AndroidJavaObject>("addFlags", 0x10000000); // FLAG_ACTIVITY_NEW_TASK
                                Debug.Log("[AndroidVoiceRecorder] [8/10] 플래그 설정 완료");
                                
                                Debug.Log("[AndroidVoiceRecorder] [9/10] startActivity 호출 중...");
                                currentActivity.Call("startActivity", launchIntent);
                                
                                Debug.Log("[AndroidVoiceRecorder] [10/10] ✓✓✓ Galaxy 음성 녹음 앱 실행 성공!");
                                Debug.Log("[AndroidVoiceRecorder] 녹음 완료 후 다시 버튼을 눌러 녹음 파일을 확인하세요.");
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[AndroidVoiceRecorder] ✗✗✗ 실행 실패: {ex.GetType().Name}");
                            Debug.LogError($"[AndroidVoiceRecorder] 에러 메시지: {ex.Message}");
                            Debug.LogError($"[AndroidVoiceRecorder] 스택 트레이스: {ex.StackTrace}");
                            lastRecordingPath = "";
                            OnRecordingCancelled?.Invoke();
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[AndroidVoiceRecorder] ✗✗✗ 네이티브 녹음 앱 실행 실패: {e.GetType().Name}");
            Debug.LogError($"[AndroidVoiceRecorder] 에러 메시지: {e.Message}");
            Debug.LogError($"[AndroidVoiceRecorder] 스택 트레이스: {e.StackTrace}");
            lastRecordingPath = "";
            OnRecordingCancelled?.Invoke();
        }
#else
        Debug.LogWarning("[AndroidVoiceRecorder] Android 빌드가 아니므로 네이티브 녹음을 실행할 수 없습니다.");
        OnRecordingCancelled?.Invoke();
#endif
    }
    
    /// <summary>
    /// 녹음 앱 열기 (파일 목록 화면)
    /// </summary>
    private void OpenRecordingApp()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            Debug.Log("[AndroidVoiceRecorder] === 녹음 앱 열기 (파일 목록) ===");
            
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                using (AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent"))
                {
                    intent.Call<AndroidJavaObject>("setClassName", 
                        "com.sec.android.app.voicenote", 
                        "com.sec.android.app.voicenote.main.VNMainActivity");
                    
                    intent.Call<AndroidJavaObject>("addFlags", 0x10000000);
                    
                    Debug.Log("[AndroidVoiceRecorder] 녹음 앱 파일 목록 열기 완료!");
                    currentActivity.Call("startActivity", intent);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AndroidVoiceRecorder] 녹음 앱 열기 실패: {ex.Message}");
            ShowToast("녹음 앱을 열 수 없습니다");
        }
#endif
    }
    
    /// <summary>
    /// Android 파일 선택 UI 표시
    /// </summary>
    private void ShowFilePicker()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            Debug.Log("[AndroidVoiceRecorder] === 파일 선택 UI 표시 ===");
            
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                // ACTION_GET_CONTENT: 오디오 파일 선택
                using (AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent"))
                {
                    intent.Call<AndroidJavaObject>("setAction", "android.intent.action.GET_CONTENT");
                    intent.Call<AndroidJavaObject>("setType", "audio/*");
                    intent.Call<AndroidJavaObject>("addCategory", "android.intent.category.OPENABLE");
                    
                    // Chooser로 감싸서 표시
                    using (AndroidJavaObject chooser = intent.CallStatic<AndroidJavaObject>("createChooser", 
                        intent, "녹음한 파일 선택"))
                    {
                        Debug.Log("[AndroidVoiceRecorder] 파일 선택 창을 띄웁니다...");
                        currentActivity.Call("startActivity", chooser);
                        
                        // 파일 선택 후에도 결과를 받을 수 없으므로, 플래그만 리셋
                        lastRecordingPath = "";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AndroidVoiceRecorder] 파일 선택 UI 표시 실패: {ex.Message}");
            lastRecordingPath = "";
            OnRecordingCancelled?.Invoke();
        }
#endif
    }
    
    /// <summary>
    /// 녹음 파일 삭제
    /// </summary>
    public bool DeleteRecording(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            Debug.LogWarning($"[AndroidVoiceRecorder] 삭제할 파일이 존재하지 않습니다: {filePath}");
            return false;
        }
        
        try
        {
            File.Delete(filePath);
            Debug.Log($"[AndroidVoiceRecorder] 녹음 파일 삭제: {filePath}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[AndroidVoiceRecorder] 파일 삭제 실패: {e.Message}");
            return false;
        }
    }
    
    /// <summary>
    /// 녹음 상태 초기화
    /// </summary>
    public void ResetState()
    {
        lastRecordingPath = "";
        Debug.Log("[AndroidVoiceRecorder] 상태 초기화됨");
    }
}
