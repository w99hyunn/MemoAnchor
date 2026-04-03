using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// TabPinCreate의 총 메모 개수만큼 프리팹을 랜덤 위치에 생성하는 스크립트
/// 3D 모델에 붙여서 사용
/// </summary>
public class JustRandomSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("생성할 프리팹")]
    [SerializeField] private GameObject prefabToSpawn;

    [Tooltip("이 오브젝트 주변에 생성됨")]
    [SerializeField] private Transform spawnCenter;

    [Header("Spawn Range")]
    [Tooltip("X축 랜덤 범위 (중심에서 ±)")]
    [SerializeField] private float randomRangeX = 5f;

    [Tooltip("Y축 랜덤 범위 (중심에서 ±)")]
    [SerializeField] private float randomRangeY = 2f;

    [Tooltip("Z축 랜덤 범위 (중심에서 ±)")]
    [SerializeField] private float randomRangeZ = 5f;

    [Header("Spawn Timing")]
    [Tooltip("Start()에서 자동으로 생성할지 여부")]
    [SerializeField] private bool spawnOnStart = true;

    [Tooltip("생성 전 대기 시간 (초)")]
    [SerializeField] private float spawnDelay = 0.5f;

    [Header("Parent Settings")]
    [Tooltip("생성된 오브젝트를 담을 부모 (비우면 이 오브젝트가 부모)")]
    [SerializeField] private Transform spawnParent;

    [Header("Debug")]
    [Tooltip("디버그 로그 출력")]
    [SerializeField] private bool verboseDebug = true;

    private List<GameObject> spawnedObjects = new List<GameObject>();

    private void Start()
    {
        // spawnCenter가 없으면 자기 자신으로 설정
        if (spawnCenter == null)
        {
            spawnCenter = transform;
            if (verboseDebug) Debug.Log("[JustRandomSpawner] spawnCenter not set, using self");
        }

        // spawnParent가 없으면 자기 자신으로 설정
        if (spawnParent == null)
        {
            spawnParent = transform;
            if (verboseDebug) Debug.Log("[JustRandomSpawner] spawnParent not set, using self");
        }

        if (spawnOnStart)
        {
            StartCoroutine(SpawnAfterDelay());
        }
    }

    private IEnumerator SpawnAfterDelay()
    {
        yield return new WaitForSeconds(spawnDelay);
        SpawnPrefabs();
    }

    /// <summary>
    /// 프리팹들을 생성하는 메인 메서드
    /// </summary>
    public void SpawnPrefabs()
    {
        // 필수 체크
        if (prefabToSpawn == null)
        {
            Debug.LogError("[JustRandomSpawner] prefabToSpawn is null! Please assign a prefab in Inspector.");
            return;
        }

        if (TabPinCreate.Instance == null)
        {
            Debug.LogWarning("[JustRandomSpawner] TabPinCreate.Instance is null! Make sure TabPinCreate exists in the scene.");
            return;
        }

        // 기존에 생성된 오브젝트들 삭제
        ClearSpawnedObjects();

        // 총 메모 개수 가져오기
        int memoCount = TabPinCreate.Instance.GetTotalCount();

        if (verboseDebug)
        {
            Debug.Log($"[JustRandomSpawner] Total memo count: {memoCount}");
            Debug.Log($"[JustRandomSpawner] Spawning {memoCount} prefabs around {spawnCenter.name}");
        }

        // 메모 개수만큼 생성
        for (int i = 0; i < memoCount; i++)
        {
            SpawnSinglePrefab(i);
        }

        if (verboseDebug)
        {
            Debug.Log($"[JustRandomSpawner] Spawning completed! Total spawned: {spawnedObjects.Count}");
        }
    }

    /// <summary>
    /// 프리팹 하나를 랜덤 위치에 생성 (메모 데이터 연결)
    /// </summary>
    private void SpawnSinglePrefab(int index)
    {
        // 랜덤 위치 계산
        Vector3 randomOffset = new Vector3(
            Random.Range(-randomRangeX, randomRangeX),
            Random.Range(-randomRangeY, randomRangeY),
            Random.Range(-randomRangeZ, randomRangeZ)
        );

        Vector3 spawnPosition = spawnCenter.position + randomOffset;

        // 프리팹 생성
        GameObject spawnedObj = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity, spawnParent);
        spawnedObj.name = $"{prefabToSpawn.name}_{index}";

        // PinClickHandler가 있으면 메모 데이터 설정
        PinClickHandler clickHandler = spawnedObj.GetComponent<PinClickHandler>();
        if (clickHandler != null)
        {
            // TabPinCreate에서 메모 타입 가져오기
            string memoType = GetMemoTypeAtIndex(index);
            string memoId = GetMemoIdAtIndex(index);

            clickHandler.SetMemoData(memoType, memoId);

            if (verboseDebug)
            {
                Debug.Log($"[JustRandomSpawner] Set memo data for #{index} - Type: {memoType}, ID: {memoId}");
            }
        }

        // 리스트에 추가
        spawnedObjects.Add(spawnedObj);

        if (verboseDebug)
        {
            Debug.Log($"[JustRandomSpawner] Spawned #{index}: {spawnedObj.name} at {spawnPosition}");
        }
    }

    /// <summary>
    /// 특정 인덱스의 메모 타입 가져오기
    /// </summary>
    private string GetMemoTypeAtIndex(int index)
    {
        if (TabPinCreate.Instance == null) return "text";

        TabPinCreate.PinData pinData = TabPinCreate.Instance.GetPinDataAtIndex(index);
        if (pinData != null && !string.IsNullOrEmpty(pinData.memoType))
        {
            return pinData.memoType;
        }

        return "text"; // 기본값
    }

    /// <summary>
    /// 특정 인덱스의 메모 ID 가져오기
    /// </summary>
    private string GetMemoIdAtIndex(int index)
    {
        if (TabPinCreate.Instance == null) return "";

        TabPinCreate.PinData pinData = TabPinCreate.Instance.GetPinDataAtIndex(index);
        if (pinData != null && !string.IsNullOrEmpty(pinData.id))
        {
            return pinData.id;
        }

        return $"memo_{index}"; // 기본값
    }

    /// <summary>
    /// 생성된 모든 오브젝트 삭제
    /// </summary>
    public void ClearSpawnedObjects()
    {
        if (spawnedObjects.Count == 0) return;

        if (verboseDebug)
        {
            Debug.Log($"[JustRandomSpawner] Clearing {spawnedObjects.Count} spawned objects");
        }

        foreach (var obj in spawnedObjects)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }

        spawnedObjects.Clear();
    }

    /// <summary>
    /// 다시 생성 (기존 것 삭제 후 재생성)
    /// </summary>
    public void Respawn()
    {
        if (verboseDebug)
        {
            Debug.Log("[JustRandomSpawner] Respawning...");
        }

        SpawnPrefabs();
    }

    /// <summary>
    /// 현재 생성된 오브젝트 개수 반환
    /// </summary>
    public int GetSpawnedCount()
    {
        return spawnedObjects.Count;
    }

    // Gizmos로 생성 범위 표시 (Scene 뷰에서만 보임)
    private void OnDrawGizmosSelected()
    {
        if (spawnCenter == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(spawnCenter.position, new Vector3(randomRangeX * 2, randomRangeY * 2, randomRangeZ * 2));
    }
}