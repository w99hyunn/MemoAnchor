using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class ScanUIManager : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private GameObject m_ScanningPanel;
    [SerializeField] private GameObject m_SaveCompletePanel;

    [Header("State Buttons (Objects)")]
    [SerializeField] private GameObject m_StartButtonObj;
    [SerializeField] private GameObject m_StopButtonObj;
    [SerializeField] private GameObject m_ConfirmButtonObj;

    [Header("Timer UI")]
    [SerializeField] private TextMeshProUGUI m_TimerText;          // 스캔 중 실시간 타이머
    [SerializeField] private TextMeshProUGUI m_FinalTimeText;     // 저장 완료 후 보여줄 최종 시간
    private float m_ElapsedTime = 0f;
    private bool m_IsTimerRunning = false;

    [Header("Control Buttons")]
    [SerializeField] private Button m_ResetButton;

    [Header("Navigation")]
    [SerializeField] private string m_HomeSceneName = "Home";

    private enum State { Ready, Scanning, Saved }
    private State m_CurrentState = State.Ready;

    void Start()
    {
        if (m_ResetButton != null)
            m_ResetButton.onClick.AddListener(ResetToReady);

        ResetToReady();
    }

    void Update()
    {
        if (m_IsTimerRunning)
        {
            m_ElapsedTime += Time.deltaTime;
            UpdateTimerText(m_TimerText);
        }
    }

    public void OnButtonClick()
    {
        switch (m_CurrentState)
        {
            case State.Ready: StartScanning(); break;
            case State.Scanning: StopScanning(); break;
            case State.Saved: GoToHome(); break;
        }
    }

    private void StartScanning()
    {
        m_CurrentState = State.Scanning;
        m_ElapsedTime = 0f;
        m_IsTimerRunning = true;

        if (m_ScanningPanel) m_ScanningPanel.SetActive(true);
        m_StartButtonObj.SetActive(false);
        m_StopButtonObj.SetActive(true);
    }

    private void StopScanning()
    {
        m_CurrentState = State.Saved;
        m_IsTimerRunning = false;

        // 저장 완료 패널로 최종 시간 기록 전달
        if (m_FinalTimeText != null)
        {
            UpdateTimerText(m_FinalTimeText);
        }

        if (m_ScanningPanel) m_ScanningPanel.SetActive(false);
        if (m_SaveCompletePanel) m_SaveCompletePanel.SetActive(true);
        m_StopButtonObj.SetActive(false);
        m_ConfirmButtonObj.SetActive(true);
    }

    public void ResetToReady()
    {
        m_CurrentState = State.Ready;
        m_IsTimerRunning = false;
        m_ElapsedTime = 0f;
        UpdateTimerText(m_TimerText);

        if (m_ScanningPanel) m_ScanningPanel.SetActive(false);
        if (m_SaveCompletePanel) m_SaveCompletePanel.SetActive(false);

        if (m_StartButtonObj) m_StartButtonObj.SetActive(true);
        if (m_StopButtonObj) m_StopButtonObj.SetActive(false);
        if (m_ConfirmButtonObj) m_ConfirmButtonObj.SetActive(false);
    }

    // 텍스트 컴포넌트를 넘겨받아 시간을 포맷팅해주는 공용 함수
    private void UpdateTimerText(TextMeshProUGUI targetText)
    {
        if (targetText != null)
        {
            int minutes = Mathf.FloorToInt(m_ElapsedTime / 60f);
            int seconds = Mathf.FloorToInt(m_ElapsedTime % 60f);
            targetText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }

    private void GoToHome()
    {
        if (!string.IsNullOrEmpty(m_HomeSceneName))
            SceneManager.LoadScene(m_HomeSceneName);
    }
}