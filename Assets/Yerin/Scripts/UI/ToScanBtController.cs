using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ToScanBtController : MonoBehaviour
{
    [Header("UI References")]
    public Button scanButton;

    [Header("Scene Names")]
    public string scanPreparationSceneName = "ScanPreparationScene";

    void Start()
    {
        // 버튼 클릭 이벤트 연결
        if (scanButton != null)
        {
            scanButton.onClick.AddListener(OnScanButtonClick);
        }
        else
        {
            // 현재 오브젝트의 Button 컴포넌트 사용
            scanButton = GetComponent<Button>();
            if (scanButton != null)
            {
                scanButton.onClick.AddListener(OnScanButtonClick);
            }
        }
    }

    void OnScanButtonClick()
    {
        Debug.Log("스캔 버튼 클릭 - 스캔 준비 화면으로 이동");

        // 스캔 준비 화면으로 씬 전환
        SceneManager.LoadScene(scanPreparationSceneName);
    }
}