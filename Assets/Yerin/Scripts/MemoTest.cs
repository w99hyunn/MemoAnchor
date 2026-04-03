using UnityEngine;
using System.Collections.Generic;

public class MemoTest : MonoBehaviour
{
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            PlaceMemoSmart(Input.mousePosition);
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetAllMemos();
        }
    }

    void PlaceMemoSmart(Vector3 screenPosition)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            Vector3 worldPos = hit.point;
            Vector3 normal = hit.normal;
            Quaternion rotation;
            string surfaceType;

            // 법선 벡터로 표면 타입 판단
            float angleFromUp = Vector3.Angle(normal, Vector3.up);

            if (angleFromUp < 30f) // 바닥
            {
                rotation = Quaternion.LookRotation(Vector3.forward, normal);
                surfaceType = "바닥";
            }
            else if (angleFromUp > 150f) // 천장
            {
                rotation = Quaternion.LookRotation(Vector3.forward, -normal);
                surfaceType = "천장";
            }
            else // 벽
            {
                rotation = Quaternion.LookRotation(-normal, Vector3.up);
                surfaceType = "벽";
            }

            // 표면에서 살짝 띄우기
            worldPos += normal * 0.01f;

            // AR Space 로컬 좌표로 변환
            Vector3 localPos = MemoManager.Instance.arSpace.InverseTransformPoint(worldPos);
            Quaternion localRot = Quaternion.Inverse(MemoManager.Instance.arSpace.rotation) * rotation;

            // 메모 내용
            string[] contents = {
                "배관 누수 확인 필요",
                "전기 배선 점검",
                "안전 조치 완료",
                "자재 부족",
                "작업 완료"
            };
            string content = contents[Random.Range(0, contents.Length)];

            MemoManager.Instance.AddMemo(content, localPos, localRot);
            Debug.Log($"✓ {surfaceType}에 메모 배치: {content}");

            // 디버그 라인
            Debug.DrawRay(hit.point, hit.normal * 0.5f, Color.cyan, 2f);
        }
    }

    void ResetAllMemos()
    {
        MemoManager.Instance.memos.Clear();
        MemoManager.Instance.SaveMemos();

        foreach (Transform child in MemoManager.Instance.arSpace)
        {
            Destroy(child.gameObject);
        }

        Debug.Log("모든 메모 삭제됨!");
    }
}