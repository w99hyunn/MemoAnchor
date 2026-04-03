using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class ModelRotatorYAxisOnly : MonoBehaviour, IDragHandler, IBeginDragHandler, IPointerClickHandler
{
    [Header("3D Models List")]
    // 여러 개의 모델을 인스펙터에서 드래그하여 넣을 수 있습니다.
    public Transform[] models3D;

    [Header("Auto Find XR Space")]
    [Tooltip("체크하면 XR Space의 자식 오브젝트를 자동으로 찾아서 추가")]
    public bool autoFindXRSpace = false;  // 기본값 false

    [Tooltip("XR Space 부모 오브젝트 이름")]
    public string xrSpaceParentName = "XR Space_1";

    [Tooltip("회전시킬 자식 오브젝트 이름")]
    public string xrSpaceChildName = "XR Map 140760-XspaceRoomC";

    [Header("Rotation Settings")]
    [Range(0.1f, 5f)]
    public float rotationSpeed = 1f;
    public bool onlyYAxis = true;

    [Header("Zoom Settings")]
    public bool enableZoom = true;
    public float zoomSpeed = 0.5f;
    public float minScale = 0.5f;
    public float maxScale = 5f;

    [Header("Scene Settings")]
    public string sceneNameAndMove;

    private Dictionary<Transform, Vector3> initialScales = new Dictionary<Transform, Vector3>();
    private float lastClickTime = 0f;
    private const float doubleClickThreshold = 0.3f;

    void Start()
    {
        // XR Space 자동 찾기
        if (autoFindXRSpace)
        {
            FindAndAddXRSpace();
        }

        // 각 모델의 초기 스케일을 저장해둡니다. (리셋 기능용)
        if (models3D != null)
        {
            foreach (Transform model in models3D)
            {
                if (model != null && !initialScales.ContainsKey(model))
                {
                    initialScales.Add(model, model.localScale);
                }
            }
        }
    }

    /// <summary>
    /// XR Space의 자식 오브젝트를 찾아서 models3D 배열에 추가
    /// </summary>
    private void FindAndAddXRSpace()
    {
        // 먼저 부모 찾기
        GameObject xrSpaceParent = GameObject.Find(xrSpaceParentName);

        if (xrSpaceParent == null)
        {
            Debug.LogWarning($"[ModelRotator] {xrSpaceParentName}을 찾을 수 없습니다!");
            return;
        }

        // 자식에서 회전시킬 오브젝트 찾기
        Transform xrMapChild = xrSpaceParent.transform.Find(xrSpaceChildName);

        if (xrMapChild != null)
        {
            // 기존 배열에 자식 오브젝트 추가
            List<Transform> modelsList = new List<Transform>();

            // 기존 모델들 추가
            if (models3D != null)
            {
                modelsList.AddRange(models3D);
            }

            // 자식이 이미 있는지 확인
            if (!modelsList.Contains(xrMapChild))
            {
                modelsList.Add(xrMapChild);
                Debug.Log($"[ModelRotator] {xrSpaceChildName} 자동 추가됨!");
            }

            // 배열 업데이트
            models3D = modelsList.ToArray();
        }
        else
        {
            Debug.LogWarning($"[ModelRotator] {xrSpaceParentName}의 자식 '{xrSpaceChildName}'을 찾을 수 없습니다!");

            // 디버그: 모든 자식 출력
            Debug.Log($"[ModelRotator] {xrSpaceParentName}의 자식들:");
            foreach (Transform child in xrSpaceParent.transform)
            {
                Debug.Log($"  - {child.name}");
            }
        }
    }

    // --- 더블 클릭 시 씬 이동 ---
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.clickCount == 2)
        {
            MoveToTargetScene();
        }
    }

    void Update()
    {
        if (!enableZoom || models3D == null) return;

        // 핀치 줌 (모바일 터치 2개)
        if (Input.touchCount == 2)
        {
            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            float prevMag = (t0.position - t0.deltaPosition - (t1.position - t1.deltaPosition)).magnitude;
            float currentMag = (t0.position - t1.position).magnitude;
            Zoom((currentMag - prevMag) * zoomSpeed * 0.01f);
        }

        // 마우스 휠 (에디터)
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0) Zoom(scroll * zoomSpeed * 10f);

        // PC용 강제 더블 클릭 감지 (OnPointerClick이 안 먹힐 경우 대비)
        if (Input.GetMouseButtonDown(0))
        {
            float currentTime = Time.time;
            if (currentTime - lastClickTime < doubleClickThreshold)
            {
                MoveToTargetScene();
            }
            lastClickTime = currentTime;
        }
    }

    private void MoveToTargetScene()
    {
        if (!string.IsNullOrEmpty(sceneNameAndMove))
        {
            Debug.Log(sceneNameAndMove + " 씬으로 이동!");
            SceneManager.LoadScene(sceneNameAndMove);
        }
    }

    public void OnBeginDrag(PointerEventData eventData) { }

    public void OnDrag(PointerEventData eventData)
    {
        if (models3D == null || models3D.Length == 0) return;

        Vector2 delta = eventData.delta;

        foreach (Transform model in models3D)
        {
            if (model == null) continue;

            // 좌우 회전 (Y축 기준)
            model.Rotate(Vector3.up, -delta.x * rotationSpeed * 0.1f, Space.World);

            // 상하 회전 (X축 기준) - onlyYAxis가 false일 때만
            if (!onlyYAxis)
            {
                model.Rotate(Vector3.right, delta.y * rotationSpeed * 0.1f, Space.World);
            }
        }
    }

    void Zoom(float increment)
    {
        foreach (Transform model in models3D)
        {
            if (model == null) continue;

            float targetScale = Mathf.Clamp(model.localScale.x + increment, minScale, maxScale);
            model.localScale = Vector3.one * targetScale;
        }
    }

    public void ResetAll()
    {
        foreach (Transform model in models3D)
        {
            if (model == null) continue;

            model.rotation = Quaternion.identity;
            if (initialScales.ContainsKey(model))
            {
                model.localScale = initialScales[model];
            }
        }
    }

    /// <summary>
    /// 런타임에 XR Space를 다시 찾기 (씬 전환 후 호출 가능)
    /// </summary>
    public void RefreshXRSpace()
    {
        if (autoFindXRSpace)
        {
            FindAndAddXRSpace();
        }
    }
}