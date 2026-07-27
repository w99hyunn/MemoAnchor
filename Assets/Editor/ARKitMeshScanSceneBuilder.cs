using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public static class ARKitMeshScanSceneBuilder
{
    private const string SCENE_PATH = "Assets/Scenes/ARKitMeshScanScene.unity";
    private const string MATERIAL_PATH = "Assets/ARKitMeshing/Materials/ARKitMeshScanMaterial.mat";
    private const string MARKER_MATERIAL_PATH = "Assets/ARKitMeshing/Materials/ARKitMeshHitMarkerMaterial.mat";
    private const string MESH_PREFAB_PATH = "Assets/ARKitMeshing/Prefabs/ARKitMeshBlock.prefab";

    [MenuItem("MemoAnchor/ARKit Meshing/Rebuild ARKit Mesh Scan Scene")]
    public static void RebuildARKitMeshScanScene()
    {
        EnsureFolders();

        var meshMaterial = CreateOrUpdateMeshMaterial();
        var markerMaterial = CreateOrUpdateMarkerMaterial();
        var meshPrefab = CreateOrUpdateMeshPrefab(meshMaterial);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "ARKitMeshScanScene";

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.85f, 0.9f, 0.95f);

        CreateDirectionalLight();

        var arSession = CreateARSession();
        var xrOrigin = CreateXROrigin(out var arCamera, out var arCameraManager, out var arOcclusionManager);
        var meshManager = CreateMeshManager(xrOrigin.transform, meshPrefab);
        var planeManager = CreatePlaneManager(xrOrigin.transform);
        var hitMarker = CreateHitMarker(markerMaterial);
        var ui = CreateHud();

        var controllerGo = new GameObject("ARKitMeshScanController");
        var controller = controllerGo.AddComponent<ARKitMeshScanController>();
        SetObject(controller, "arSession", arSession);
        SetObject(controller, "meshManager", meshManager);
        SetObject(controller, "planeManager", planeManager);
        SetObject(controller, "arCamera", arCamera);
        SetObject(controller, "arCameraManager", arCameraManager);
        SetObject(controller, "arOcclusionManager", arOcclusionManager);
        SetObject(controller, "meshMaterial", meshMaterial);
        SetObject(controller, "sessionStateText", ui.SessionText);
        SetObject(controller, "meshStatsText", ui.StatsText);
        SetObject(controller, "exportStatusText", ui.ExportText);
        SetObject(controller, "resetButton", ui.ResetButton);
        SetObject(controller, "exportButton", ui.ExportButton);
        SetObject(controller, "backButton", ui.BackButton);
        SetString(controller, "fallbackSceneName", "Main");

        var probeGo = new GameObject("ARKitMeshRaycastProbe");
        var probe = probeGo.AddComponent<ARKitMeshRaycastProbe>();
        SetObject(probe, "arCamera", arCamera);
        SetObject(probe, "hitMarker", hitMarker.transform);
        SetObject(probe, "hitStatusText", ui.HitText);

        CreateEventSystem();
        AddSceneToBuildSettings(SCENE_PATH);

        EditorSceneManager.SaveScene(scene, SCENE_PATH);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ARKitMeshScanSceneBuilder] Rebuilt scene: {SCENE_PATH}");
    }

    private static void EnsureFolders()
    {
        CreateFolderIfMissing("Assets", "ARKitMeshing");
        CreateFolderIfMissing("Assets/ARKitMeshing", "Materials");
        CreateFolderIfMissing("Assets/ARKitMeshing", "Prefabs");
        CreateFolderIfMissing("Assets", "Editor");
    }

    private static void CreateFolderIfMissing(string parent, string name)
    {
        var path = $"{parent}/{name}";
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }

    private static Material CreateOrUpdateMeshMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);
        if (!material)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MATERIAL_PATH);
        }

        ConfigureTransparentMaterial(material, new Color(0.1f, 0.85f, 1f, 0.65f));
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateOrUpdateMarkerMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MARKER_MATERIAL_PATH);
        if (!material)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MARKER_MATERIAL_PATH);
        }

        ConfigureTransparentMaterial(material, new Color(1f, 0.77f, 0.16f, 0.9f));
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void ConfigureTransparentMaterial(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)CullMode.Off);

        material.renderQueue = (int)RenderQueue.Transparent;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHAPREMULTIPLY_ON");
    }

    private static MeshFilter CreateOrUpdateMeshPrefab(Material meshMaterial)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MESH_PREFAB_PATH);
        GameObject source;

        if (prefab)
            source = Object.Instantiate(prefab);
        else
            source = new GameObject("ARKitMeshBlock");

        var meshFilter = GetOrAdd<MeshFilter>(source);
        var meshRenderer = GetOrAdd<MeshRenderer>(source);
        GetOrAdd<MeshCollider>(source);

        meshRenderer.sharedMaterial = meshMaterial;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        PrefabUtility.SaveAsPrefabAsset(source, MESH_PREFAB_PATH);
        Object.DestroyImmediate(source);

        AssetDatabase.LoadAssetAtPath<GameObject>(MESH_PREFAB_PATH).TryGetComponent<MeshFilter>(out var savedMeshFilter);
        return savedMeshFilter;
    }

    private static ARSession CreateARSession()
    {
        var go = new GameObject("AR Session");
        var session = go.AddComponent<ARSession>();
        go.AddComponent<ARInputManager>();
        return session;
    }

    private static XROrigin CreateXROrigin(out Camera arCamera, out ARCameraManager arCameraManager, out AROcclusionManager arOcclusionManager)
    {
        var originGo = new GameObject("XR Origin");
        var origin = originGo.AddComponent<XROrigin>();

        var offsetGo = new GameObject("Camera Offset");
        offsetGo.transform.SetParent(originGo.transform, false);

        var cameraGo = new GameObject("Main Camera");
        cameraGo.transform.SetParent(offsetGo.transform, false);
        cameraGo.tag = "MainCamera";

        arCamera = cameraGo.AddComponent<Camera>();
        arCamera.clearFlags = CameraClearFlags.Color;
        arCamera.backgroundColor = Color.black;
        arCamera.nearClipPlane = 0.1f;
        arCamera.farClipPlane = 20f;

        cameraGo.AddComponent<AudioListener>();
        arCameraManager = cameraGo.AddComponent<ARCameraManager>();
        arOcclusionManager = cameraGo.AddComponent<AROcclusionManager>();
        arOcclusionManager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Best;
        arOcclusionManager.environmentDepthTemporalSmoothingRequested = true;
        cameraGo.AddComponent<ARCameraBackground>();
        ConfigureTrackedPoseDriver(cameraGo.AddComponent<TrackedPoseDriver>());

        origin.CameraFloorOffsetObject = offsetGo;
        origin.Camera = arCamera;

        originGo.AddComponent<ARRaycastManager>();
        originGo.AddComponent<ARAnchorManager>();

        return origin;
    }

    private static void ConfigureTrackedPoseDriver(TrackedPoseDriver trackedPoseDriver)
    {
        var positionAction = new InputAction("Position", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
        positionAction.AddBinding("<HandheldARInputDevice>/devicePosition");

        var rotationAction = new InputAction("Rotation", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
        rotationAction.AddBinding("<HandheldARInputDevice>/deviceRotation");

        trackedPoseDriver.positionInput = new InputActionProperty(positionAction);
        trackedPoseDriver.rotationInput = new InputActionProperty(rotationAction);
    }

    private static ARMeshManager CreateMeshManager(Transform xrOrigin, MeshFilter meshPrefab)
    {
        var go = new GameObject("AR Mesh Manager");
        go.transform.SetParent(xrOrigin, false);
        go.transform.localScale = Vector3.one;

        var meshManager = go.AddComponent<ARMeshManager>();
        meshManager.meshPrefab = meshPrefab;
        meshManager.density = 1f;
        meshManager.normals = true;
        meshManager.tangents = false;
        meshManager.textureCoordinates = false;
        meshManager.colors = false;
        meshManager.concurrentQueueSize = 4;

        return meshManager;
    }

    private static ARPlaneManager CreatePlaneManager(Transform xrOrigin)
    {
        var go = new GameObject("AR Plane Manager");
        go.transform.SetParent(xrOrigin, false);
        go.transform.localScale = Vector3.one;

        var planeManager = go.AddComponent<ARPlaneManager>();
        planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
        return planeManager;
    }

    private static GameObject CreateHitMarker(Material markerMaterial)
    {
        var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "Mesh Hit Marker";
        marker.transform.localScale = Vector3.one * 0.045f;
        marker.SetActive(false);

        if (marker.TryGetComponent<Collider>(out var collider))
            Object.DestroyImmediate(collider);

        if (marker.TryGetComponent<MeshRenderer>(out var renderer))
            renderer.sharedMaterial = markerMaterial;

        return marker;
    }

    private static HudRefs CreateHud()
    {
        var canvasGo = new GameObject("Scan HUD");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var canvasScaler = canvasGo.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1170f, 2532f);
        canvasScaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        var panel = CreatePanel("Top Status Panel", canvasGo.transform, new Color(0f, 0f, 0f, 0.46f));
        SetRect(panel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, -24f), new Vector2(-0f, 260f));

        var title = CreateText("Title", panel.transform, "ARKit Mesh Scan", 36, FontStyle.Bold, TextAnchor.UpperLeft);
        SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(32f, -24f), new Vector2(-32f, 52f));

        var sessionText = CreateText("Session State", panel.transform, "AR Session: checking", 25, FontStyle.Normal, TextAnchor.UpperLeft);
        SetRect(sessionText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(32f, -80f), new Vector2(-32f, 36f));

        var statsText = CreateText("Mesh Stats", panel.transform, "Meshes: 0", 25, FontStyle.Normal, TextAnchor.UpperLeft);
        SetRect(statsText.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(32f, -124f), new Vector2(-16f, 126f));

        var hitText = CreateText("Hit Status", panel.transform, "Center hit: none", 25, FontStyle.Normal, TextAnchor.UpperRight);
        SetRect(hitText.rectTransform, new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(16f, -124f), new Vector2(-32f, 40f));

        var exportText = CreateText("Export Status", panel.transform, "Export path: app persistent data", 22, FontStyle.Normal, TextAnchor.UpperRight);
        SetRect(exportText.rectTransform, new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(16f, -170f), new Vector2(-32f, 74f));

        CreateCrosshair(canvasGo.transform);

        var buttonBar = new GameObject("Bottom Button Bar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        buttonBar.transform.SetParent(canvasGo.transform, false);
        buttonBar.TryGetComponent<HorizontalLayoutGroup>(out var layout);
        layout.spacing = 18f;
        layout.padding = new RectOffset(28, 28, 20, 44);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        buttonBar.TryGetComponent<RectTransform>(out var buttonBarRect);
        SetRect(buttonBarRect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 148f));

        var resetButton = CreateButton("Scan Button", buttonBar.transform, "Scan");
        var exportButton = CreateButton("Stop Button", buttonBar.transform, "Stop");
        var backButton = CreateButton("Back Button", buttonBar.transform, "Back");

        return new HudRefs
        {
            SessionText = sessionText,
            StatsText = statsText,
            HitText = hitText,
            ExportText = exportText,
            ResetButton = resetButton,
            ExportButton = exportButton,
            BackButton = backButton
        };
    }

    private static Image CreatePanel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.TryGetComponent<Image>(out var image);
        image.color = color;
        return image;
    }

    private static Text CreateText(string name, Transform parent, string value, int fontSize, FontStyle style, TextAnchor alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        go.TryGetComponent<Text>(out var text);
        text.text = value;
        text.font = GetDefaultFont();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, string label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.TryGetComponent<Image>(out var image);
        image.color = new Color(0.08f, 0.1f, 0.12f, 0.92f);
        go.TryGetComponent<LayoutElement>(out var layoutElement);
        layoutElement.preferredHeight = 84f;

        go.TryGetComponent<Button>(out var button);
        var colors = button.colors;
        colors.normalColor = new Color(0.08f, 0.1f, 0.12f, 0.92f);
        colors.highlightedColor = new Color(0.16f, 0.2f, 0.24f, 0.96f);
        colors.pressedColor = new Color(0.02f, 0.55f, 0.7f, 1f);
        button.colors = colors;

        var text = CreateText("Label", go.transform, label, 27, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        return button;
    }

    private static void CreateCrosshair(Transform parent)
    {
        var horizontal = CreatePanel("Crosshair Horizontal", parent, new Color(1f, 1f, 1f, 0.8f));
        SetRect(horizontal.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46f, 3f));

        var vertical = CreatePanel("Crosshair Vertical", parent, new Color(1f, 1f, 1f, 0.8f));
        SetRect(vertical.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3f, 46f));
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

    private static void CreateDirectionalLight()
    {
        var lightGo = new GameObject("Directional Light");
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0.6f;
    }

    private static void CreateEventSystem()
    {
        var eventSystemGo = new GameObject("EventSystem");
        eventSystemGo.AddComponent<EventSystem>();
        eventSystemGo.AddComponent<StandaloneInputModule>();
    }

    private static void AddSceneToBuildSettings(string scenePath)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(scene => scene.path == scenePath))
            return;

        scenes.Add(new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        if (!go.TryGetComponent<T>(out var component))
            component = go.AddComponent<T>();

        return component;
    }

    private static Font GetDefaultFont()
    {
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
               Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private static void SetObject(Object target, string propertyName, Object value)
    {
        var serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(propertyName).objectReferenceValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetString(Object target, string propertyName, string value)
    {
        var serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(propertyName).stringValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private struct HudRefs
    {
        public Text SessionText;
        public Text StatsText;
        public Text HitText;
        public Text ExportText;
        public Button ResetButton;
        public Button ExportButton;
        public Button BackButton;
    }
}
