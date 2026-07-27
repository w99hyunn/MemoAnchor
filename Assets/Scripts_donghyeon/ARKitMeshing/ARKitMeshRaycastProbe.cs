using UnityEngine;
using UnityEngine.UI;

public class ARKitMeshRaycastProbe : MonoBehaviour
{
    [SerializeField] private Camera arCamera;
    [SerializeField] private Transform hitMarker;
    [SerializeField] private Text hitStatusText;
    [SerializeField] private float maxDistance = 8f;
    [SerializeField] private LayerMask hitLayers = ~0;

    private void Awake()
    {
        if (!arCamera)
            arCamera = Camera.main;
    }

    private void Update()
    {
        if (!arCamera)
            return;

        var ray = arCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (Physics.Raycast(ray, out var hit, maxDistance, hitLayers, QueryTriggerInteraction.Ignore))
        {
            if (hitMarker)
            {
                hitMarker.gameObject.SetActive(true);
                hitMarker.position = hit.point;
                hitMarker.rotation = Quaternion.LookRotation(hit.normal);
            }

            if (hitStatusText)
                hitStatusText.text = $"Center hit: {hit.distance:F2}m";
        }
        else
        {
            if (hitMarker)
                hitMarker.gameObject.SetActive(false);

            if (hitStatusText)
                hitStatusText.text = "Center hit: none";
        }
    }
}
