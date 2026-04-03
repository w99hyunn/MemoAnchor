using UnityEngine;

public class MapPersistentManager : MonoBehaviour
{
    private void Awake()
    {
        // 이 오브젝트와 모든 자식(Spawner, 핀들)을 다음 씬으로 가져갑니다.
        DontDestroyOnLoad(this.gameObject);
    }
}