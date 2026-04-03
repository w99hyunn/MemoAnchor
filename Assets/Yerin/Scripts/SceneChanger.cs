using UnityEngine;
using UnityEngine.SceneManagement; // 씬 전환을 위해 필수!

public class SceneChanger : MonoBehaviour
{
    // [SerializeField]를 사용하면 변수가 private이어도 인스펙터에 노출됩니다.
    [SerializeField] private string sceneName;

    // 버튼의 OnClick 이벤트에서 호출할 함수
    public void ChangeScene()
    {
        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            Debug.LogError("전환할 씬 이름이 설정되지 않았습니다!");
        }
    }
}