using UnityEngine;
using UnityEngine.EventSystems;

public class SimpleModelRotator : MonoBehaviour, IDragHandler, IBeginDragHandler
{
    [Header("3D Model")]
    public Transform model3D; // 회전시킬 3D 모델

    [Header("Settings")]
    [Range(0.1f, 2f)]
    public float rotationSpeed = 0.5f;

    private Vector2 lastPosition;

    public void OnBeginDrag(PointerEventData eventData)
    {
        lastPosition = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (model3D == null) return;

        // 드래그 거리 계산
        Vector2 delta = eventData.position - lastPosition;
        lastPosition = eventData.position;

        // 회전 적용
        float rotationY = delta.x * rotationSpeed;
        float rotationX = -delta.y * rotationSpeed;

        model3D.Rotate(Vector3.up, rotationY, Space.World);
        model3D.Rotate(Vector3.right, rotationX, Space.World);
    }

    /// <summary>
    /// 회전 리셋
    /// </summary>
    public void ResetRotation()
    {
        if (model3D != null)
        {
            model3D.rotation = Quaternion.identity;
        }
    }
}