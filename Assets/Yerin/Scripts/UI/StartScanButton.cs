// StartScanButton¿¡ Ãß°¡
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartScanButton : MonoBehaviour
{
    public Button button;

    void Start()
    {
        button.onClick.AddListener(() => {
            SceneManager.LoadScene("ScanningScene");
        });
    }
}
