// Assets/Scripts/EditorCameraController.cs
using UnityEngine;

#if UNITY_EDITOR
public class EditorCameraController : MonoBehaviour
{
    public float moveSpeed = 3f;
    public float lookSpeed = 2f;
    public float sprintMultiplier = 2f;

    private float rotationX = 0f;
    private float rotationY = 0f;

    void Start()
    {
        // 초기 회전값 설정
        Vector3 rot = transform.localRotation.eulerAngles;
        rotationY = rot.y;
        rotationX = rot.x;
    }

    void Update()
    {
        // WASD: 이동
        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift))
            speed *= sprintMultiplier;

        Vector3 move = Vector3.zero;

        if (Input.GetKey(KeyCode.W))
            move += transform.forward;
        if (Input.GetKey(KeyCode.S))
            move -= transform.forward;
        if (Input.GetKey(KeyCode.A))
            move -= transform.right;
        if (Input.GetKey(KeyCode.D))
            move += transform.right;
        if (Input.GetKey(KeyCode.Q))
            move -= transform.up;
        if (Input.GetKey(KeyCode.E))
            move += transform.up;

        transform.position += move * speed * Time.deltaTime;

        // 우클릭 드래그: 카메라 회전
        if (Input.GetMouseButton(1))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            rotationX -= Input.GetAxis("Mouse Y") * lookSpeed;
            rotationY += Input.GetAxis("Mouse X") * lookSpeed;

            rotationX = Mathf.Clamp(rotationX, -90f, 90f);

            transform.localRotation = Quaternion.Euler(rotationX, rotationY, 0);
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
#endif