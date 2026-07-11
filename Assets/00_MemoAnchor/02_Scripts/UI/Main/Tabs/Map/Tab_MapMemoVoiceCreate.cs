using System;
using System.Collections.Generic;
using System.IO;
using MemoAnchor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    public partial class MainView
    {
        private const int MEMO_VOICE_SAMPLE_RATE = 44100;
        private const int MEMO_VOICE_MAX_SECONDS = 300;
        private const int MEMO_VOICE_WAVEFORM_SAMPLES = 128;
        private const int MEMO_VOICE_WAVEFORM_INPUT_SAMPLES = 256;
        private const int MEMO_VOICE_WAVEFORM_POINTS_PER_UPDATE = 4;

        [SerializeField] private VisualTreeAsset _memoVoiceItemAsset;

        private readonly List<MemoCreateVoiceSelection> _memoCreateVoiceSelections = new();
        private readonly List<MemoVoiceRecordingSegment> _memoVoiceRecordingSegments = new();
        private readonly float[] _memoVoiceWaveformSamples = new float[MEMO_VOICE_WAVEFORM_SAMPLES];
        private readonly float[] _memoVoiceWaveformInputSamples = new float[MEMO_VOICE_WAVEFORM_INPUT_SAMPLES];
        private VisualElement _memoCreateVoiceContent, _memoCreateVoiceList, _memoVoiceRecorderPage, _memoVoiceRecorderWaveform, _memoVoiceRecorderActions;
        private Button _memoCreateVoiceAddButton, _memoVoiceRecorderBackButton, _memoVoiceRecorderListButton, _memoVoiceRecorderMicButton;
        private Button _memoVoiceRecorderDeleteButton, _memoVoiceRecorderPauseButton, _memoVoiceRecorderSaveButton;
        private Label _memoVoiceRecorderTimeLabel, _memoVoiceRecorderGuideLabel, _memoVoiceRecorderPauseLabel;
        private VisualElement _memoVoiceRecorderPauseIcon;
        private IVisualElementScheduledItem _memoVoiceRecorderSchedule;
        private AudioSource _memoVoiceAudioSource;
        private AudioClip _memoVoiceRecordingClip;
        private AudioClip _memoVoicePreviewClip;
        private VisualElement _memoVoicePreviewPanel, _memoVoicePreviewProgress, _memoVoicePreviewSeekBar;
        private Button _memoVoicePreviewToggleButton;
        private Label _memoVoicePreviewCurrentLabel, _memoVoicePreviewDurationLabel;
        private VisualElement _memoVoicePreviewToggleIcon;
        private IVisualElementScheduledItem _memoVoicePreviewSchedule;
        private string _memoVoiceMicrophoneDevice;
        private float _memoVoiceRecordedSeconds;
        private bool _isMemoVoiceStarting;
        private bool _isMemoVoiceRecording;
        private bool _isMemoVoicePaused;
        private bool _isMemoVoicePreviewPlaying;
        private bool _hasMemoVoicePreviewStarted;
        private int _memoVoiceRecorderSessionToken;
        private int _memoVoicePreviewToken;

        private void RegisterMemoVoiceCreatePage()
        {
            _memoCreateVoiceContent = _root.Q<VisualElement>("memo-create-voice-content");
            _memoCreateVoiceList = _root.Q<VisualElement>("memo-create-voice-list");
            _memoCreateVoiceAddButton = _root.Q<Button>("memo-create-voice-add-button");
            _memoVoiceRecorderPage = _root.Q<VisualElement>("memo-voice-recorder-page");
            _memoVoiceRecorderWaveform = _root.Q<VisualElement>("memo-voice-recorder-waveform");
            _memoVoiceRecorderActions = _root.Q<VisualElement>("memo-voice-recorder-actions");
            _memoVoiceRecorderBackButton = _root.Q<Button>("memo-voice-recorder-back-button");
            _memoVoiceRecorderListButton = _root.Q<Button>("memo-voice-recorder-list-button");
            _memoVoiceRecorderMicButton = _root.Q<Button>("memo-voice-recorder-mic-button");
            _memoVoiceRecorderDeleteButton = _root.Q<Button>("memo-voice-recorder-delete-button");
            _memoVoiceRecorderPauseButton = _root.Q<Button>("memo-voice-recorder-pause-button");
            _memoVoiceRecorderSaveButton = _root.Q<Button>("memo-voice-recorder-save-button");
            _memoVoiceRecorderTimeLabel = _root.Q<Label>("memo-voice-recorder-time-label");
            _memoVoiceRecorderGuideLabel = _root.Q<Label>("memo-voice-recorder-guide-label");
            _memoVoiceRecorderPauseIcon = _root.Q<VisualElement>("memo-voice-recorder-pause-icon");
            _memoVoiceRecorderPauseLabel = _root.Q<Label>("memo-voice-recorder-pause-label");

            if (!TryGetComponent<AudioSource>(out _memoVoiceAudioSource))
            {
                _memoVoiceAudioSource = gameObject.AddComponent<AudioSource>();
            }

            _memoVoiceAudioSource.playOnAwake = false;
            _memoVoiceRecorderWaveform.generateVisualContent += DrawMemoVoiceWaveform;
            _memoCreateVoiceAddButton.clicked += ShowMemoVoiceRecorderPage;
            _memoVoiceRecorderBackButton.clicked += RequestCloseMemoVoiceRecorder;
            _memoVoiceRecorderListButton.clicked += RequestCloseMemoVoiceRecorder;
            _memoVoiceRecorderMicButton.clicked += OnClickMemoVoiceRecorderMic;
            _memoVoiceRecorderDeleteButton.clicked += ResetMemoVoiceRecorder;
            _memoVoiceRecorderPauseButton.clicked += ToggleMemoVoiceRecordingPause;
            _memoVoiceRecorderSaveButton.clicked += SaveMemoVoiceRecording;
            ResetMemoVoiceRecorder();
            SetVisible(_memoVoiceRecorderPage, false);
        }

        private void UnregisterMemoVoiceCreatePage()
        {
            _memoVoiceRecorderWaveform.generateVisualContent -= DrawMemoVoiceWaveform;
            _memoCreateVoiceAddButton.clicked -= ShowMemoVoiceRecorderPage;
            _memoVoiceRecorderBackButton.clicked -= RequestCloseMemoVoiceRecorder;
            _memoVoiceRecorderListButton.clicked -= RequestCloseMemoVoiceRecorder;
            _memoVoiceRecorderMicButton.clicked -= OnClickMemoVoiceRecorderMic;
            _memoVoiceRecorderDeleteButton.clicked -= ResetMemoVoiceRecorder;
            _memoVoiceRecorderPauseButton.clicked -= ToggleMemoVoiceRecordingPause;
            _memoVoiceRecorderSaveButton.clicked -= SaveMemoVoiceRecording;
            StopMemoVoicePreview();
            ResetMemoVoiceRecorder();
            ClearMemoCreateVoiceSelections();
        }

        private void ShowMemoVoiceRecorderPage()
        {
            if (_memoCreateVoiceSelections.Count >= 3)
            {
                return;
            }

            ResetMemoVoiceRecorder();
            SetVisible(_memoVoiceRecorderPage, true);
            SetVisible(_memoCreateBottomBar, false);
            _memoVoiceRecorderPage.BringToFront();
        }

        private void RequestCloseMemoVoiceRecorder()
        {
            if (!_isMemoVoiceRecording && _memoVoiceRecordingSegments.Count == 0)
            {
                HideMemoVoiceRecorderPage();
                return;
            }

            PopupManager.ShowConfirm("녹음 취소", "녹음 중인 내용을 삭제하고 목록으로 돌아갑니다.", "계속 녹음", "삭제", HideMemoVoiceRecorderPage);
        }

        private void HideMemoVoiceRecorderPage()
        {
            ResetMemoVoiceRecorder();
            SetVisible(_memoVoiceRecorderPage, false);
            SetVisible(_memoCreateBottomBar, !_memoCreatePage.ClassListContains(HIDDEN_CLASS));
        }

        private void OnClickMemoVoiceRecorderMic()
        {
            if (_isMemoVoiceStarting || _isMemoVoiceRecording)
            {
                return;
            }

            _ = StartMemoVoiceRecordingAsync();
        }

        private async Awaitable StartMemoVoiceRecordingAsync()
        {
            _isMemoVoiceStarting = true;
            int sessionToken = _memoVoiceRecorderSessionToken;
            try
            {
                await StartMemoVoiceRecordingCoreAsync(sessionToken);
            }
            finally
            {
                _isMemoVoiceStarting = false;
            }
        }

        private async Awaitable StartMemoVoiceRecordingCoreAsync(int sessionToken)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
                PopupManager.ShowMessage("마이크 권한", "녹음을 시작하려면 마이크 권한을 허용한 뒤 다시 눌러주세요.", "확인");
                return;
            }
#endif
#if UNITY_IOS && !UNITY_EDITOR
            if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
            {
                AsyncOperation permissionRequest = Application.RequestUserAuthorization(UserAuthorization.Microphone);
                while (!permissionRequest.isDone)
                {
                    await Awaitable.NextFrameAsync();
                }
                if (sessionToken != _memoVoiceRecorderSessionToken)
                {
                    return;
                }
                if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
                {
                    PopupManager.ShowMessage("마이크 권한", "녹음을 시작하려면 마이크 권한이 필요합니다.", "확인");
                    return;
                }
            }
#endif
            if (Microphone.devices.Length == 0)
            {
                PopupManager.ShowMessage("녹음 실패", "사용할 수 있는 마이크를 찾지 못했습니다.", "확인");
                return;
            }

            _memoVoiceMicrophoneDevice = Microphone.devices[0];
            int remainingSeconds = Mathf.Max(1, MEMO_VOICE_MAX_SECONDS - Mathf.CeilToInt(_memoVoiceRecordedSeconds));
            _memoVoiceRecordingClip = Microphone.Start(_memoVoiceMicrophoneDevice, false, remainingSeconds, MEMO_VOICE_SAMPLE_RATE);
            if (_memoVoiceRecordingClip == null)
            {
                PopupManager.ShowMessage("녹음 실패", "마이크 녹음을 시작하지 못했습니다.", "확인");
                return;
            }

            float startTimeout = Time.realtimeSinceStartup + 2f;
            while (Microphone.GetPosition(_memoVoiceMicrophoneDevice) <= 0 && Time.realtimeSinceStartup < startTimeout)
            {
                await Awaitable.NextFrameAsync();
            }
            if (sessionToken != _memoVoiceRecorderSessionToken)
            {
                Microphone.End(_memoVoiceMicrophoneDevice);
                Destroy(_memoVoiceRecordingClip);
                _memoVoiceRecordingClip = null;
                return;
            }
            if (Microphone.GetPosition(_memoVoiceMicrophoneDevice) <= 0)
            {
                Microphone.End(_memoVoiceMicrophoneDevice);
                Destroy(_memoVoiceRecordingClip);
                _memoVoiceRecordingClip = null;
                PopupManager.ShowMessage("녹음 실패", "마이크 입력을 확인하지 못했습니다.", "확인");
                return;
            }

            _isMemoVoiceRecording = true;
            _isMemoVoicePaused = false;
            _memoVoiceRecorderPage.AddToClassList("is-recording");
            SetVisible(_memoVoiceRecorderWaveform, true);
            SetVisible(_memoVoiceRecorderTimeLabel, true);
            SetVisible(_memoVoiceRecorderGuideLabel, false);
            SetVisible(_memoVoiceRecorderActions, true);
            RefreshMemoVoicePauseState();
            _memoVoiceRecorderSchedule?.Pause();
            _memoVoiceRecorderSchedule = _memoVoiceRecorderWaveform.schedule.Execute(UpdateMemoVoiceRecorder).Every(50);
        }

        private void ToggleMemoVoiceRecordingPause()
        {
            if (_isMemoVoiceRecording)
            {
                CaptureMemoVoiceRecordingSegment();
                _isMemoVoicePaused = true;
                RefreshMemoVoicePauseState();
                return;
            }

            if (_isMemoVoicePaused)
            {
                _ = StartMemoVoiceRecordingAsync();
            }
        }

        private void CaptureMemoVoiceRecordingSegment()
        {
            if (!_isMemoVoiceRecording)
            {
                return;
            }

            int sampleFrames = Microphone.GetPosition(_memoVoiceMicrophoneDevice);
            Microphone.End(_memoVoiceMicrophoneDevice);
            _isMemoVoiceRecording = false;
            if (_memoVoiceRecordingClip != null && sampleFrames > 0)
            {
                _memoVoiceRecordingSegments.Add(new MemoVoiceRecordingSegment(_memoVoiceRecordingClip, sampleFrames));
                _memoVoiceRecordedSeconds += (float)sampleFrames / _memoVoiceRecordingClip.frequency;
            }
            else if (_memoVoiceRecordingClip != null)
            {
                Destroy(_memoVoiceRecordingClip);
            }

            _memoVoiceRecordingClip = null;
            _memoVoiceRecorderSchedule?.Pause();
            RefreshMemoVoiceRecorderTime();
        }

        private void UpdateMemoVoiceRecorder()
        {
            if (!_isMemoVoiceRecording)
            {
                return;
            }

            int position = Microphone.GetPosition(_memoVoiceMicrophoneDevice);
            if (position >= MEMO_VOICE_WAVEFORM_INPUT_SAMPLES)
            {
                _memoVoiceRecordingClip.GetData(_memoVoiceWaveformInputSamples, position - MEMO_VOICE_WAVEFORM_INPUT_SAMPLES);
                AppendMemoVoiceWaveformPoints();
                _memoVoiceRecorderWaveform.MarkDirtyRepaint();
            }

            RefreshMemoVoiceRecorderTime();
            if (_memoVoiceRecordedSeconds + (float)position / MEMO_VOICE_SAMPLE_RATE >= MEMO_VOICE_MAX_SECONDS)
            {
                CaptureMemoVoiceRecordingSegment();
                _isMemoVoicePaused = true;
                RefreshMemoVoicePauseState();
            }
        }

        private void RefreshMemoVoiceRecorderTime()
        {
            float seconds = _memoVoiceRecordedSeconds;
            if (_isMemoVoiceRecording)
            {
                seconds += (float)Mathf.Max(0, Microphone.GetPosition(_memoVoiceMicrophoneDevice)) / _memoVoiceRecordingClip.frequency;
            }

            TimeSpan time = TimeSpan.FromSeconds(seconds);
            _memoVoiceRecorderTimeLabel.text = $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00}";
        }

        private void RefreshMemoVoicePauseState()
        {
            _memoVoiceRecorderPauseIcon.EnableInClassList("is-paused", _isMemoVoicePaused);
            _memoVoiceRecorderPauseLabel.text = _isMemoVoicePaused ? "계속" : "일시정지";
        }

        private void DrawMemoVoiceWaveform(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            Rect rect = _memoVoiceRecorderWaveform.contentRect;
            float centerY = rect.height * 0.5f;
            painter.strokeColor = new Color(0.48f, 0.60f, 0.92f);
            painter.lineWidth = 4f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(0f, centerY - _memoVoiceWaveformSamples[0] * centerY * 0.82f));
            for (int i = 0; i < _memoVoiceWaveformSamples.Length - 1; i++)
            {
                float p0 = _memoVoiceWaveformSamples[Mathf.Max(0, i - 1)];
                float p1 = _memoVoiceWaveformSamples[i];
                float p2 = _memoVoiceWaveformSamples[i + 1];
                float p3 = _memoVoiceWaveformSamples[Mathf.Min(_memoVoiceWaveformSamples.Length - 1, i + 2)];
                for (int step = 1; step <= 3; step++)
                {
                    float t = step / 3f;
                    float t2 = t * t;
                    float t3 = t2 * t;
                    float value = 0.5f * ((2f * p1)
                        + (-p0 + p2) * t
                        + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                        + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
                    float x = rect.width * (i + t) / (_memoVoiceWaveformSamples.Length - 1);
                    float y = centerY - Mathf.Clamp(value, -1f, 1f) * centerY * 0.82f;
                    painter.LineTo(new Vector2(x, y));
                }
            }
            painter.Stroke();
        }

        private void AppendMemoVoiceWaveformPoints()
        {
            Array.Copy(
                _memoVoiceWaveformSamples,
                MEMO_VOICE_WAVEFORM_POINTS_PER_UPDATE,
                _memoVoiceWaveformSamples,
                0,
                _memoVoiceWaveformSamples.Length - MEMO_VOICE_WAVEFORM_POINTS_PER_UPDATE);

            int bucketSize = _memoVoiceWaveformInputSamples.Length / MEMO_VOICE_WAVEFORM_POINTS_PER_UPDATE;
            float previous = _memoVoiceWaveformSamples[_memoVoiceWaveformSamples.Length - MEMO_VOICE_WAVEFORM_POINTS_PER_UPDATE - 1];
            for (int bucket = 0; bucket < MEMO_VOICE_WAVEFORM_POINTS_PER_UPDATE; bucket++)
            {
                int start = bucket * bucketSize;
                float squareSum = 0f;
                float signedPeak = 0f;
                for (int i = start; i < start + bucketSize; i++)
                {
                    float sample = _memoVoiceWaveformInputSamples[i];
                    squareSum += sample * sample;
                    if (Mathf.Abs(sample) > Mathf.Abs(signedPeak))
                    {
                        signedPeak = sample;
                    }
                }

                float rms = Mathf.Sqrt(squareSum / bucketSize);
                float target = Mathf.Clamp(rms * 3.2f, 0f, 1f) * Mathf.Sign(signedPeak);
                previous = Mathf.Lerp(previous, target, 0.42f);
                _memoVoiceWaveformSamples[_memoVoiceWaveformSamples.Length - MEMO_VOICE_WAVEFORM_POINTS_PER_UPDATE + bucket] = previous;
            }
        }

        private void SaveMemoVoiceRecording()
        {
            CaptureMemoVoiceRecordingSegment();
            if (_memoVoiceRecordingSegments.Count == 0)
            {
                PopupManager.ShowMessage("녹음 저장 실패", "저장할 녹음 내용이 없습니다.", "확인");
                return;
            }

            string directory = Path.Combine(Application.temporaryCachePath, "MemoVoice");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, $"memo_voice_{DateTime.Now:yyyyMMdd_HHmmss_fff}.wav");
            WriteMemoVoiceWav(path, _memoVoiceRecordingSegments);
            _memoCreateVoiceSelections.Add(new MemoCreateVoiceSelection($"음성녹음 {_memoCreateVoiceSelections.Count + 1}", path, false));
            RebuildMemoCreateVoiceItems();
            HideMemoVoiceRecorderPage();
        }

        private static void WriteMemoVoiceWav(string path, IReadOnlyList<MemoVoiceRecordingSegment> segments)
        {
            int totalSampleFrames = 0;
            foreach (MemoVoiceRecordingSegment segment in segments)
            {
                totalSampleFrames += segment.SampleFrames;
            }

            int channels = 1;
            int bitsPerSample = 16;
            int dataSize = totalSampleFrames * channels * bitsPerSample / 8;
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920);
            using var writer = new BinaryWriter(stream);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(MEMO_VOICE_SAMPLE_RATE);
            writer.Write(MEMO_VOICE_SAMPLE_RATE * channels * bitsPerSample / 8);
            writer.Write((short)(channels * bitsPerSample / 8));
            writer.Write((short)bitsPerSample);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);

            var samples = new float[4096];
            foreach (MemoVoiceRecordingSegment segment in segments)
            {
                int offset = 0;
                while (offset < segment.SampleFrames)
                {
                    int count = Mathf.Min(samples.Length, segment.SampleFrames - offset);
                    segment.Clip.GetData(samples, offset);

                    for (int i = 0; i < count; i++)
                    {
                        writer.Write((short)(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue));
                    }
                    offset += count;
                }
            }
        }

        private void ResetMemoVoiceRecorder()
        {
            _memoVoiceRecorderSessionToken++;
            if (_isMemoVoiceRecording)
            {
                Microphone.End(_memoVoiceMicrophoneDevice);
            }

            _memoVoiceRecorderSchedule?.Pause();
            _isMemoVoiceStarting = false;
            _isMemoVoiceRecording = false;
            _isMemoVoicePaused = false;
            if (_memoVoiceRecordingClip != null)
            {
                Destroy(_memoVoiceRecordingClip);
                _memoVoiceRecordingClip = null;
            }

            foreach (MemoVoiceRecordingSegment segment in _memoVoiceRecordingSegments)
            {
                Destroy(segment.Clip);
            }
            _memoVoiceRecordingSegments.Clear();
            Array.Clear(_memoVoiceWaveformSamples, 0, _memoVoiceWaveformSamples.Length);
            Array.Clear(_memoVoiceWaveformInputSamples, 0, _memoVoiceWaveformInputSamples.Length);
            _memoVoiceRecordedSeconds = 0f;
            _memoVoiceRecorderPage.RemoveFromClassList("is-recording");
            SetVisible(_memoVoiceRecorderWaveform, false);
            SetVisible(_memoVoiceRecorderTimeLabel, false);
            SetVisible(_memoVoiceRecorderActions, false);
            SetVisible(_memoVoiceRecorderGuideLabel, true);
            _memoVoiceRecorderTimeLabel.text = "00:00:00";
            RefreshMemoVoicePauseState();
        }

        private void PopulateMemoCreateVoiceSelections(List<MemoVoiceEntry> voiceItems)
        {
            ClearMemoCreateVoiceSelections();
            foreach (MemoVoiceEntry item in voiceItems)
            {
                _memoCreateVoiceSelections.Add(new MemoCreateVoiceSelection(item.name, item.url, true));
            }
            RebuildMemoCreateVoiceItems();
        }

        private void RebuildMemoCreateVoiceItems()
        {
            _memoCreateVoiceList.Clear();
            foreach (MemoCreateVoiceSelection selection in _memoCreateVoiceSelections)
            {
                TemplateContainer template = _memoVoiceItemAsset.Instantiate();
                VisualElement item = template.Q<VisualElement>("memo-voice-item");
                TextField nameInput = template.Q<TextField>("memo-voice-item-name");
                Button playButton = template.Q<Button>("memo-voice-item-play-button");
                Button removeButton = template.Q<Button>("memo-voice-item-remove-button");
                nameInput.SetValueWithoutNotify(selection.Name);
                nameInput.RegisterValueChangedCallback(evt => selection.Name = evt.newValue);
                ConfigureMemoVoicePreview(template, selection.Path, selection.IsRemote, playButton);
                removeButton.clicked += () => RemoveMemoCreateVoiceSelection(selection);
                _memoCreateVoiceList.Add(item);
            }

            SetVisible(_memoCreateVoiceAddButton, _memoCreateVoiceSelections.Count < 3);
            InputValidationFeedback.ClearError(_memoCreateVoiceAddButton);
        }

        private void RemoveMemoCreateVoiceSelection(MemoCreateVoiceSelection selection)
        {
            StopMemoVoicePreview();
            if (!selection.IsRemote && File.Exists(selection.Path))
            {
                File.Delete(selection.Path);
            }
            _memoCreateVoiceSelections.Remove(selection);
            RebuildMemoCreateVoiceItems();
        }

        private void ClearMemoCreateVoiceSelections()
        {
            StopMemoVoicePreview();
            foreach (MemoCreateVoiceSelection selection in _memoCreateVoiceSelections)
            {
                if (!selection.IsRemote && File.Exists(selection.Path))
                {
                    File.Delete(selection.Path);
                }
            }
            _memoCreateVoiceSelections.Clear();
            _memoCreateVoiceList?.Clear();
        }

        private void ConfigureMemoVoicePreview(TemplateContainer template, string path, bool isRemote, Button openButton)
        {
            VisualElement row = template.Q<VisualElement>("memo-voice-item-row");
            TextField nameInput = template.Q<TextField>("memo-voice-item-name");
            Button removeButton = template.Q<Button>("memo-voice-item-remove-button");
            VisualElement panel = template.Q<VisualElement>("memo-voice-preview-player");
            VisualElement progress = template.Q<VisualElement>("memo-voice-preview-progress");
            VisualElement seekBar = template.Q<VisualElement>("memo-voice-preview-slider-bar");
            Button toggleButton = template.Q<Button>("memo-voice-preview-toggle-button");
            Label currentLabel = template.Q<Label>("memo-voice-preview-current-label");
            Label durationLabel = template.Q<Label>("memo-voice-preview-duration-label");
            VisualElement toggleIcon = template.Q<VisualElement>("memo-voice-preview-toggle-icon");
            row.RegisterCallback<ClickEvent>(evt =>
            {
                VisualElement target = evt.target as VisualElement;
                if (!openButton.enabledSelf || IsMemoVoiceEventInside(target, nameInput) || IsMemoVoiceEventInside(target, removeButton))
                {
                    return;
                }

                evt.StopPropagation();
                _ = ShowMemoVoicePreviewAsync(path, isRemote, panel, progress, seekBar, toggleButton, currentLabel, durationLabel, toggleIcon);
            });
            toggleButton.clicked += ToggleMemoVoicePreviewPlayback;
            seekBar.RegisterCallback<PointerDownEvent>(evt => OnMemoVoicePreviewSeekPointerDown(seekBar, evt));
            seekBar.RegisterCallback<PointerMoveEvent>(evt => OnMemoVoicePreviewSeekPointerMove(seekBar, evt));
            seekBar.RegisterCallback<PointerUpEvent>(evt => OnMemoVoicePreviewSeekPointerUp(seekBar, evt));
            seekBar.RegisterCallback<PointerCancelEvent>(evt => OnMemoVoicePreviewSeekPointerCancel(seekBar, evt));
        }

        private static bool IsMemoVoiceEventInside(VisualElement target, VisualElement ancestor)
        {
            for (VisualElement current = target; current != null; current = current.parent)
            {
                if (current == ancestor)
                {
                    return true;
                }
            }
            return false;
        }

        private async Awaitable ShowMemoVoicePreviewAsync(string path, bool isRemote, VisualElement panel, VisualElement progress, VisualElement seekBar, Button toggleButton, Label currentLabel, Label durationLabel, VisualElement toggleIcon)
        {
            if (_memoVoicePreviewPanel == panel)
            {
                StopMemoVoicePreview();
                return;
            }

            StopMemoVoicePreview();
            int previewToken = _memoVoicePreviewToken;
            _memoVoicePreviewPanel = panel;
            _memoVoicePreviewProgress = progress;
            _memoVoicePreviewSeekBar = seekBar;
            _memoVoicePreviewToggleButton = toggleButton;
            _memoVoicePreviewCurrentLabel = currentLabel;
            _memoVoicePreviewDurationLabel = durationLabel;
            _memoVoicePreviewToggleIcon = toggleIcon;
            progress.style.width = Length.Percent(0f);
            currentLabel.text = FormatMemoVoiceDuration(0f);
            durationLabel.text = FormatMemoVoiceDuration(0f);
            toggleIcon.RemoveFromClassList("is-playing");
            toggleButton.SetEnabled(false);
            SetVisible(panel, true);
            string url = isRemote ? GetMemoMediaUrl(path) : new Uri(path).AbsoluteUri;
            if (isRemote)
            {
                LoadingSpinnerController.ShowOverlay(_mainLoadingOverlay, _mainLoadingSpinner);
            }

            try
            {
                using UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV);
                await ServicesManager.SendRequestAsync(request);
                if (request.result != UnityWebRequest.Result.Success)
                {
                    PopupManager.ShowMessage("재생 실패", "음성 녹음을 불러오지 못했습니다.", "확인");
                    return;
                }
                if (previewToken != _memoVoicePreviewToken)
                {
                    return;
                }

                _memoVoicePreviewClip = DownloadHandlerAudioClip.GetContent(request);
                _memoVoiceAudioSource.clip = _memoVoicePreviewClip;
                UpdateMemoVoicePreviewProgress(0f, _memoVoicePreviewClip.length);
                durationLabel.text = FormatMemoVoiceDuration(_memoVoicePreviewClip.length);
                toggleButton.SetEnabled(true);
                _memoVoicePreviewSchedule = panel.schedule.Execute(UpdateMemoVoicePreview).Every(100);
            }
            finally
            {
                if (isRemote)
                {
                LoadingSpinnerController.HideOverlay(_mainLoadingOverlay, _mainLoadingSpinner);
                }
            }
        }

        private void ToggleMemoVoicePreviewPlayback()
        {
            if (_memoVoicePreviewClip == null)
            {
                return;
            }

            if (_memoVoiceAudioSource.isPlaying)
            {
                _memoVoiceAudioSource.Pause();
                _isMemoVoicePreviewPlaying = false;
                _memoVoicePreviewToggleIcon.RemoveFromClassList("is-playing");
                return;
            }

            if (_memoVoiceAudioSource.time >= _memoVoicePreviewClip.length - 0.05f)
            {
                _memoVoiceAudioSource.time = 0f;
            }
            _memoVoiceAudioSource.Play();
            _isMemoVoicePreviewPlaying = true;
            _hasMemoVoicePreviewStarted = true;
            _memoVoicePreviewToggleIcon.AddToClassList("is-playing");
            _memoVoicePreviewDurationLabel.text = FormatMemoVoiceRemainingTime(_memoVoiceAudioSource.time, _memoVoicePreviewClip.length);
        }

        private void OnMemoVoicePreviewSeekPointerDown(VisualElement seekBar, PointerDownEvent evt)
        {
            if (_memoVoicePreviewSeekBar != seekBar || _memoVoicePreviewClip == null)
            {
                return;
            }

            seekBar.CapturePointer(evt.pointerId);
            SeekMemoVoicePreview(seekBar, evt.localPosition.x);
            evt.StopPropagation();
        }

        private void OnMemoVoicePreviewSeekPointerMove(VisualElement seekBar, PointerMoveEvent evt)
        {
            if (!seekBar.HasPointerCapture(evt.pointerId))
            {
                return;
            }

            SeekMemoVoicePreview(seekBar, evt.localPosition.x);
            evt.StopPropagation();
        }

        private void OnMemoVoicePreviewSeekPointerUp(VisualElement seekBar, PointerUpEvent evt)
        {
            if (!seekBar.HasPointerCapture(evt.pointerId))
            {
                return;
            }

            SeekMemoVoicePreview(seekBar, evt.localPosition.x);
            seekBar.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private static void OnMemoVoicePreviewSeekPointerCancel(VisualElement seekBar, PointerCancelEvent evt)
        {
            if (seekBar.HasPointerCapture(evt.pointerId))
            {
                seekBar.ReleasePointer(evt.pointerId);
            }
        }

        private void SeekMemoVoicePreview(VisualElement seekBar, float localX)
        {
            float normalizedTime = Mathf.Clamp01(localX / seekBar.contentRect.width);
            _memoVoiceAudioSource.time = normalizedTime * _memoVoicePreviewClip.length;
            _hasMemoVoicePreviewStarted = true;
            UpdateMemoVoicePreviewProgress(_memoVoiceAudioSource.time, _memoVoicePreviewClip.length);
            _memoVoicePreviewCurrentLabel.text = FormatMemoVoiceDuration(_memoVoiceAudioSource.time);
            _memoVoicePreviewDurationLabel.text = FormatMemoVoiceRemainingTime(_memoVoiceAudioSource.time, _memoVoicePreviewClip.length);
        }

        private void UpdateMemoVoicePreview()
        {
            if (_memoVoicePreviewClip == null)
            {
                return;
            }

            if (_isMemoVoicePreviewPlaying && !_memoVoiceAudioSource.isPlaying)
            {
                _isMemoVoicePreviewPlaying = false;
                UpdateMemoVoicePreviewProgress(_memoVoicePreviewClip.length, _memoVoicePreviewClip.length);
                _memoVoicePreviewCurrentLabel.text = FormatMemoVoiceDuration(_memoVoicePreviewClip.length);
                _memoVoicePreviewDurationLabel.text = FormatMemoVoiceRemainingTime(_memoVoicePreviewClip.length, _memoVoicePreviewClip.length);
                _memoVoicePreviewToggleIcon.RemoveFromClassList("is-playing");
                return;
            }

            float currentTime = Mathf.Clamp(_memoVoiceAudioSource.time, 0f, _memoVoicePreviewClip.length);
            UpdateMemoVoicePreviewProgress(currentTime, _memoVoicePreviewClip.length);
            _memoVoicePreviewCurrentLabel.text = FormatMemoVoiceDuration(currentTime);
            _memoVoicePreviewDurationLabel.text = _hasMemoVoicePreviewStarted
                ? FormatMemoVoiceRemainingTime(currentTime, _memoVoicePreviewClip.length)
                : FormatMemoVoiceDuration(_memoVoicePreviewClip.length);
            if (!_memoVoiceAudioSource.isPlaying && currentTime >= _memoVoicePreviewClip.length - 0.05f)
            {
                _memoVoicePreviewToggleIcon.RemoveFromClassList("is-playing");
            }
        }

        private void UpdateMemoVoicePreviewProgress(float currentTime, float duration)
        {
            float progress = duration <= 0f ? 0f : Mathf.Clamp01(currentTime / duration) * 100f;
            _memoVoicePreviewProgress.style.width = Length.Percent(progress);
        }

        private static string FormatMemoVoiceRemainingTime(float currentTime, float duration)
        {
            int remainingSeconds = Mathf.Max(0, Mathf.CeilToInt(duration - currentTime));
            return $"-{remainingSeconds / 60}:{remainingSeconds % 60:00}";
        }

        private static string FormatMemoVoiceDuration(float duration)
        {
            int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(duration));
            return $"{totalSeconds / 60}:{totalSeconds % 60:00}";
        }

        private void StopMemoVoicePreview()
        {
            _memoVoicePreviewToken++;
            _memoVoicePreviewSchedule?.Pause();
            _isMemoVoicePreviewPlaying = false;
            _hasMemoVoicePreviewStarted = false;
            if (_memoVoiceAudioSource != null)
            {
                _memoVoiceAudioSource.Stop();
                _memoVoiceAudioSource.clip = null;
            }
            if (_memoVoicePreviewClip != null)
            {
                Destroy(_memoVoicePreviewClip);
                _memoVoicePreviewClip = null;
            }
            if (_memoVoicePreviewPanel != null)
            {
                SetVisible(_memoVoicePreviewPanel, false);
            }
            _memoVoicePreviewPanel = null;
            _memoVoicePreviewProgress = null;
            _memoVoicePreviewSeekBar = null;
            _memoVoicePreviewToggleButton = null;
            _memoVoicePreviewCurrentLabel = null;
            _memoVoicePreviewDurationLabel = null;
            _memoVoicePreviewToggleIcon = null;
        }

        private async Awaitable<List<MemoVoiceEntry>> UploadMemoCreateVoiceAsync()
        {
            var voiceItems = new List<MemoVoiceEntry>(_memoCreateVoiceSelections.Count);
            var localSelections = new List<MemoCreateVoiceSelection>();
            var localPaths = new List<string>();
            foreach (MemoCreateVoiceSelection selection in _memoCreateVoiceSelections)
            {
                if (selection.IsRemote)
                {
                    voiceItems.Add(new MemoVoiceEntry { name = selection.Name.Trim(), url = selection.Path });
                }
                else
                {
                    localSelections.Add(selection);
                    localPaths.Add(selection.Path);
                }
            }

            MemoMediaUploadResult result = await _memoService.UploadMemoVoiceAsync(localPaths);
            if (!result.IsSuccess)
            {
                return null;
            }
            for (int i = 0; i < localSelections.Count; i++)
            {
                voiceItems.Add(new MemoVoiceEntry { name = localSelections[i].Name.Trim(), url = result.Urls[i] });
            }
            return voiceItems;
        }

        private sealed class MemoCreateVoiceSelection
        {
            public string Name;
            public readonly string Path;
            public readonly bool IsRemote;

            public MemoCreateVoiceSelection(string name, string path, bool isRemote)
            {
                Name = name;
                Path = path;
                IsRemote = isRemote;
            }
        }

        private sealed class MemoVoiceRecordingSegment
        {
            public readonly AudioClip Clip;
            public readonly int SampleFrames;

            public MemoVoiceRecordingSegment(AudioClip clip, int sampleFrames)
            {
                Clip = clip;
                SampleFrames = sampleFrames;
            }
        }
    }
}
