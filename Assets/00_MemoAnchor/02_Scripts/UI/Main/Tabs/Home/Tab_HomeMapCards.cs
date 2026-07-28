using System;
using System.Collections.Generic;
using Unity.Services.Authentication;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace MemoAnchor.UI
{
    public partial class MainView
    {
        [SerializeField] private VisualTreeAsset _homeMapCardAsset;

        private VisualElement _homeMapCardContainer;
        private Label _homeMapEmptyLabel;
        private readonly Dictionary<string, Texture2D> _homeMapThumbnailCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<VisualElement> _homeMapThumbnailSpinners = new();
        private int _homeMapThumbnailLoadToken;

        private void RegisterHomeMapCards()
        {
            _homeMapCardContainer = _root.Q<VisualElement>("home-map-card-container");
            _homeMapEmptyLabel = _root.Q<Label>("home-map-empty-label");
            RebuildHomeMapCards();
        }

        private void RebuildHomeMapCards()
        {
            if (_homeMapCardContainer == null)
            {
                return;
            }

            int loadToken = ++_homeMapThumbnailLoadToken;
            List<(ScanMapItem Map, Image Thumbnail, VisualElement Spinner)> thumbnailRequests = new();
            StopHomeMapThumbnailSpinners();
            _homeMapCardContainer.Clear();
            List<ScanMapItem> visibleMaps = new();
            foreach (ScanMapItem map in _scanMaps)
            {
                if (IsParticipatingMap(map))
                {
                    visibleMaps.Add(map);
                }
            }

            for (int favoritePass = 0; favoritePass < 2; favoritePass++)
            {
                bool favoriteGroup = favoritePass == 0;
                foreach (ScanMapItem map in visibleMaps)
                {
                    if (IsHomeMapFavorite(map.id) != favoriteGroup)
                    {
                        continue;
                    }

                    VisualElement card = CreateHomeMapCard(map, out Image thumbnail, out VisualElement spinner);
                    _homeMapCardContainer.Add(card);
                    string thumbnailKey = GetHomeMapThumbnailKey(map);
                    if (_homeMapThumbnailCache.TryGetValue(thumbnailKey, out Texture2D cachedThumbnail))
                    {
                        thumbnail.image = cachedThumbnail;
                    }
                    else if (IsCompletedReconstruction(map))
                    {
                        SetVisible(spinner, true);
                        LoadingSpinnerController.Start(spinner);
                        thumbnailRequests.Add((map, thumbnail, spinner));
                    }
                }
            }

            SetVisible(_homeMapEmptyLabel, visibleMaps.Count == 0);
            _ = LoadHomeMapThumbnailsAsync(thumbnailRequests, loadToken);
        }

        private VisualElement CreateHomeMapCard(
            ScanMapItem map,
            out Image thumbnail,
            out VisualElement spinner)
        {
            TemplateContainer template = _homeMapCardAsset.Instantiate();
            VisualElement card = template.Q<VisualElement>("home-map-card");
            thumbnail = template.Q<Image>("home-map-thumbnail-image");
            spinner = template.Q<VisualElement>("home-map-thumbnail-spinner");
            Button favoriteButton = template.Q<Button>("home-map-favorite-button");
            _homeMapThumbnailSpinners.Add(spinner);
            template.Q<Label>("home-map-title").text = map.spaceName;
            template.Q<Label>("home-map-address").text = GetFirstNonEmpty(map.roadAddress, map.address, string.Empty);
            ApplyHomeMapFavoriteState(favoriteButton, IsHomeMapFavorite(map.id));
            favoriteButton.RegisterCallback<ClickEvent>(evt =>
            {
                evt.StopPropagation();
                ToggleHomeMapFavorite(map.id);
            });
            card.RegisterCallback<ClickEvent>(_ =>
            {
                _selectedMapId = map.id;
                ApplySelectedMap();
                RequestTabSwitch(3);
            });
            return card;
        }

        private void ToggleHomeMapFavorite(string mapId)
        {
            string key = GetHomeMapFavoriteKey(mapId);
            PlayerPrefs.SetInt(key, IsHomeMapFavorite(mapId) ? 0 : 1);
            PlayerPrefs.Save();
            RebuildHomeMapCards();
        }

        private static void ApplyHomeMapFavoriteState(Button button, bool favorite)
        {
            button.EnableInClassList("is-favorite", favorite);
        }

        private static bool IsHomeMapFavorite(string mapId)
        {
            return PlayerPrefs.GetInt(GetHomeMapFavoriteKey(mapId), 0) == 1;
        }

        private static string GetHomeMapFavoriteKey(string mapId)
        {
            return $"MemoAnchor.HomeMapFavorite.{AuthenticationService.Instance.PlayerId}.{mapId}";
        }

        private async Awaitable LoadHomeMapThumbnailsAsync(
            IReadOnlyList<(ScanMapItem Map, Image Thumbnail, VisualElement Spinner)> requests,
            int loadToken)
        {
            foreach ((ScanMapItem map, Image thumbnail, VisualElement spinner) in requests)
            {
                if (loadToken != _homeMapThumbnailLoadToken)
                {
                    return;
                }

                string thumbnailKey = GetHomeMapThumbnailKey(map);
                if (_homeMapThumbnailCache.TryGetValue(thumbnailKey, out Texture2D cachedThumbnail))
                {
                    thumbnail.image = cachedThumbnail;
                    StopHomeMapThumbnailSpinner(spinner);
                    continue;
                }

                using (UnityWebRequest thumbnailRequest = UnityWebRequestTexture.GetTexture(
                    ServicesManager.BuildServerUrl(GetHomeMapThumbnailPath(map))))
                {
                    ServicesManager.Authorize(thumbnailRequest);
                    await ServicesManager.SendRequestAsync(thumbnailRequest);
                    if (loadToken != _homeMapThumbnailLoadToken)
                    {
                        return;
                    }
                    if (thumbnailRequest.result == UnityWebRequest.Result.Success)
                    {
                        Texture2D storedThumbnail = DownloadHandlerTexture.GetContent(thumbnailRequest);
                        _homeMapThumbnailCache[thumbnailKey] = storedThumbnail;
                        thumbnail.image = storedThumbnail;
                        StopHomeMapThumbnailSpinner(spinner);
                        continue;
                    }
                    if (thumbnailRequest.responseCode != 404)
                    {
                        Debug.LogWarning(
                            $"[MainView] Home map thumbnail download failed ({thumbnailRequest.responseCode}): {thumbnailRequest.error}");
                        StopHomeMapThumbnailSpinner(spinner);
                        continue;
                    }
                }

                byte[] data;
                string mapId = UnityWebRequest.EscapeURL(map.id);
                string scanId = UnityWebRequest.EscapeURL(map.reconstructionScanId);
                using (UnityWebRequest request = ServicesManager.CreateAuthorizedGetRequest(
                    $"/api/scan/maps/{mapId}/reconstruction/{scanId}/result"))
                {
                    request.timeout = 900;
                    await ServicesManager.SendRequestAsync(request);
                    if (loadToken != _homeMapThumbnailLoadToken)
                    {
                        return;
                    }
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning($"[MainView] Home map thumbnail download failed: {request.error}");
                        StopHomeMapThumbnailSpinner(spinner);
                        continue;
                    }

                    data = request.downloadHandler.data;
                }

                if (!ARKitMeshScanController.TryCreateMeshFromPly(data, out Mesh mesh, out string error))
                {
                    Debug.LogWarning($"[MainView] Home map thumbnail mesh failed: {error}");
                    StopHomeMapThumbnailSpinner(spinner);
                    continue;
                }

                Texture2D renderedThumbnail = await RenderHomeMapThumbnailAsync(mesh);
                if (loadToken != _homeMapThumbnailLoadToken)
                {
                    Destroy(renderedThumbnail);
                    return;
                }

                _homeMapThumbnailCache[thumbnailKey] = renderedThumbnail;
                thumbnail.image = renderedThumbnail;
                StopHomeMapThumbnailSpinner(spinner);
                byte[] jpeg = renderedThumbnail.EncodeToJPG(88);
                renderedThumbnail.Apply(false, true);
                await UploadHomeMapThumbnailAsync(map, jpeg);
            }
        }

        private static void StopHomeMapThumbnailSpinner(VisualElement spinner)
        {
            LoadingSpinnerController.Stop(spinner);
            SetVisible(spinner, false);
        }

        private static async Awaitable UploadHomeMapThumbnailAsync(ScanMapItem map, byte[] jpeg)
        {
            using UnityWebRequest request = ServicesManager.CreateAuthorizedRequest(
                GetHomeMapThumbnailPath(map),
                UnityWebRequest.kHttpVerbPUT);
            request.uploadHandler = new UploadHandlerRaw(jpeg);
            request.SetRequestHeader("Content-Type", "image/jpeg");
            request.timeout = 60;
            await ServicesManager.SendRequestAsync(request);
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning(
                    $"[MainView] Home map thumbnail upload failed ({request.responseCode}): {request.error}");
            }
        }

        private async Awaitable<Texture2D> RenderHomeMapThumbnailAsync(Mesh mesh)
        {
            await Awaitable.NextFrameAsync();

            int previewLayer = 31;
            int width = Mathf.Clamp(Mathf.RoundToInt(_homeMapCardContainer.resolvedStyle.width * 0.7f), 320, 768);
            int height = Mathf.Clamp(Mathf.RoundToInt(width * 0.66f), 240, 512);
            Vector3 previewOrigin = new(-10000f, 10000f, -10000f);

            GameObject previewRoot = new("Home Map Thumbnail Preview");
            previewRoot.transform.position = previewOrigin;
            previewRoot.layer = previewLayer;

            GameObject surface = new("Home Map Thumbnail Surface");
            surface.transform.SetParent(previewRoot.transform, false);
            surface.transform.localScale = new Vector3(1f, 1f, -1f);
            surface.layer = previewLayer;
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer meshRenderer = surface.AddComponent<MeshRenderer>();
            Material material = ARKitMeshScanController.CreateReconstructionPreviewMaterial(mesh);
            meshRenderer.sharedMaterial = material;

            GameObject cameraObject = new("Home Map Thumbnail Camera");
            cameraObject.layer = previewLayer;
            Camera previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color32(172, 172, 172, 255);
            previewCamera.cullingMask = 1 << previewLayer;
            previewCamera.fieldOfView = 40f;
            previewCamera.allowHDR = false;
            previewCamera.allowMSAA = true;

            Bounds bounds = mesh.bounds;
            float radius = Mathf.Max(bounds.extents.magnitude, 0.1f);
            float verticalFov = previewCamera.fieldOfView * Mathf.Deg2Rad;
            float horizontalFov = 2f * Mathf.Atan(Mathf.Tan(verticalFov * 0.5f) * ((float)width / height));
            float distance = radius / Mathf.Tan(Mathf.Min(verticalFov, horizontalFov) * 0.5f);
            Vector3 center = surface.transform.TransformPoint(bounds.center);
            Quaternion orbit = Quaternion.Euler(28f, -35f, 0f);
            previewCamera.transform.position = center - orbit * Vector3.forward * distance;
            previewCamera.transform.rotation = Quaternion.LookRotation(center - previewCamera.transform.position, Vector3.up);
            previewCamera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 2f);
            previewCamera.farClipPlane = distance + radius * 4f;

            GameObject lightObject = new("Home Map Thumbnail Light");
            lightObject.transform.position = previewOrigin;
            lightObject.transform.rotation = new Quaternion(0.40821794f, -0.23456973f, 0.10938166f, 0.8754261f);
            lightObject.layer = previewLayer;
            Light previewLight = lightObject.AddComponent<Light>();
            previewLight.type = LightType.Directional;
            previewLight.intensity = 0.6f;
            previewLight.cullingMask = 1 << previewLayer;
            previewLight.shadows = LightShadows.None;

            RenderTexture renderTexture = new(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "Home Map Thumbnail",
                antiAliasing = 4
            };
            renderTexture.Create();
            previewCamera.targetTexture = renderTexture;
            previewCamera.enabled = true;
            await Awaitable.EndOfFrameAsync();
            previewCamera.enabled = false;

            RenderTexture resolvedTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(renderTexture, resolvedTexture);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = resolvedTexture;
            Texture2D thumbnail = new(width, height, TextureFormat.RGB24, false)
            {
                name = "Home Map Thumbnail"
            };
            thumbnail.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            thumbnail.Apply(false, false);
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(resolvedTexture);

            previewCamera.targetTexture = null;
            renderTexture.Release();
            Destroy(renderTexture);
            Destroy(cameraObject);
            Destroy(lightObject);
            Destroy(previewRoot);
            Destroy(material);
            Destroy(mesh);
            return thumbnail;
        }

        private void ReleaseHomeMapThumbnails()
        {
            _homeMapThumbnailLoadToken++;
            StopHomeMapThumbnailSpinners();
            foreach (Texture2D thumbnail in _homeMapThumbnailCache.Values)
            {
                Destroy(thumbnail);
            }
            _homeMapThumbnailCache.Clear();
        }

        private void StopHomeMapThumbnailSpinners()
        {
            foreach (VisualElement spinner in _homeMapThumbnailSpinners)
            {
                LoadingSpinnerController.Stop(spinner);
            }
            _homeMapThumbnailSpinners.Clear();
        }

        private static bool IsCompletedReconstruction(ScanMapItem map)
        {
            return string.Equals(map.reconstructionState?.Trim(), "done", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(map.reconstructionScanId);
        }

        private static string GetHomeMapThumbnailKey(ScanMapItem map)
        {
            return $"{map.id}:{map.reconstructionScanId}";
        }

        private static string GetHomeMapThumbnailPath(ScanMapItem map)
        {
            string mapId = UnityWebRequest.EscapeURL(map.id);
            string scanId = UnityWebRequest.EscapeURL(map.reconstructionScanId);
            return $"/api/scan/maps/{mapId}/reconstruction/{scanId}/thumbnail";
        }

        private static bool IsParticipatingMap(ScanMapItem map)
        {
            return string.Equals(map.currentUserRole, "manager", StringComparison.OrdinalIgnoreCase)
                || string.Equals(map.currentUserRole, "repairer", StringComparison.OrdinalIgnoreCase);
        }
    }
}
