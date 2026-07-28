using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace MemoAnchor
{
    public static class MapScanSession
    {
        public readonly struct ExistingMemoMarker
        {
            public readonly Vector3 Position;
            public readonly bool IsCompletionRequested;

            public ExistingMemoMarker(Vector3 position, bool isCompletionRequested)
            {
                Position = position;
                IsCompletionRequested = isCompletionRequested;
            }
        }

        public const string SCAN_SCENE_NAME = "ARKitMeshScanScene";

        public enum SessionMode
        {
            None,
            Scan,
            ResultView,
            MemoPlacement
        }

        public static SessionMode Mode { get; private set; }
        public static string MapId { get; private set; } = string.Empty;
        public static string ReconstructionScanId { get; private set; } = string.Empty;
        public static string ReconstructionResultFile { get; private set; } = string.Empty;
        public static ScanMapCreateRequest PendingMapRequest { get; private set; }
        public static bool ReturnToMapOnClose { get; private set; }
        public static Mesh CompletedReconstructionMesh { get; private set; }
        public static Material CompletedReconstructionMaterial { get; private set; }
        public static bool HasPendingMemoPlacement { get; private set; }
        public static string MemoPlacementMapId { get; private set; } = string.Empty;
        public static string MemoPlacementScanId { get; private set; } = string.Empty;
        public static string MemoPlacementKind { get; private set; } = string.Empty;
        public static Vector3 MemoPlacementPosition { get; private set; }
        public static Quaternion MemoPlacementRotation { get; private set; } = Quaternion.identity;
        public static IReadOnlyList<ExistingMemoMarker> ExistingMemoMarkers => existingMemoMarkers;

        private static readonly List<ExistingMemoMarker> existingMemoMarkers = new();

        public static bool HasActiveMap => !string.IsNullOrWhiteSpace(MapId);
        public static bool HasPendingMap => PendingMapRequest != null;
        public static bool HasScanTarget => HasActiveMap || HasPendingMap;
        public static bool IsViewingStoredResult => Mode == SessionMode.ResultView;
        public static bool IsMemoPlacement => Mode == SessionMode.MemoPlacement;

        public static void BeginScan(ScanMapCreateRequest pendingMapRequest)
        {
            existingMemoMarkers.Clear();
            Mode = SessionMode.Scan;
            MapId = string.Empty;
            ReconstructionScanId = string.Empty;
            ReconstructionResultFile = string.Empty;
            PendingMapRequest = pendingMapRequest;
            ReturnToMapOnClose = false;
            CompletedReconstructionMesh = null;
            CompletedReconstructionMaterial = null;
            ClearMemoPlacement();
        }

        public static void ConfirmMap(string mapId)
        {
            MapId = mapId?.Trim() ?? string.Empty;
            PendingMapRequest = null;
        }

        public static void BeginResultView(string mapId, string scanId, string resultFile)
        {
            existingMemoMarkers.Clear();
            Mode = SessionMode.ResultView;
            MapId = mapId?.Trim() ?? string.Empty;
            ReconstructionScanId = scanId?.Trim() ?? string.Empty;
            ReconstructionResultFile = resultFile?.Trim() ?? string.Empty;
            PendingMapRequest = null;
            ReturnToMapOnClose = true;
            CompletedReconstructionMesh = null;
            CompletedReconstructionMaterial = null;
            ClearMemoPlacement();
        }

        public static void BeginMemoPlacement(
            string mapId,
            string scanId,
            string resultFile,
            IReadOnlyList<ExistingMemoMarker> memoMarkers)
        {
            existingMemoMarkers.Clear();
            Mode = SessionMode.MemoPlacement;
            MapId = mapId?.Trim() ?? string.Empty;
            ReconstructionScanId = scanId?.Trim() ?? string.Empty;
            ReconstructionResultFile = resultFile?.Trim() ?? string.Empty;
            PendingMapRequest = null;
            ReturnToMapOnClose = true;
            CompletedReconstructionMesh = null;
            CompletedReconstructionMaterial = null;
            ClearMemoPlacement();
            for (int i = 0; i < memoMarkers.Count; i++)
                existingMemoMarkers.Add(memoMarkers[i]);
        }

        public static void CompleteMemoPlacement(Vector3 position, Quaternion rotation, string kind)
        {
            HasPendingMemoPlacement = true;
            MemoPlacementMapId = MapId;
            MemoPlacementScanId = ReconstructionScanId;
            MemoPlacementKind = kind?.Trim() ?? string.Empty;
            MemoPlacementPosition = position;
            MemoPlacementRotation = rotation;
        }

        public static void RequestReturnToMap()
        {
            ReturnToMapOnClose = true;
        }

        public static void SetCompletedReconstruction(Mesh mesh, Material material)
        {
            CompletedReconstructionMesh = mesh;
            CompletedReconstructionMaterial = material;
        }

        public static void ClearCompletedReconstruction()
        {
            CompletedReconstructionMesh = null;
            CompletedReconstructionMaterial = null;
        }

        public static string BuildUploadPath()
        {
            return $"/api/scan/maps/{Escape(MapId)}/reconstruction/upload";
        }

        public static string BuildStatusPath(string scanId)
        {
            return $"/api/scan/maps/{Escape(MapId)}/reconstruction/{Escape(scanId)}/status";
        }

        public static string BuildResultPath(string scanId)
        {
            return $"/api/scan/maps/{Escape(MapId)}/reconstruction/{Escape(scanId)}/result";
        }

        public static string BuildLocalizationPath(string scanId)
        {
            return $"/api/scan/maps/{Escape(MapId)}/reconstruction/{Escape(scanId)}/localize";
        }

        public static void ClearSceneState()
        {
            Mode = SessionMode.None;
            MapId = string.Empty;
            ReconstructionScanId = string.Empty;
            ReconstructionResultFile = string.Empty;
            PendingMapRequest = null;
            ReturnToMapOnClose = false;
            CompletedReconstructionMesh = null;
            CompletedReconstructionMaterial = null;
            existingMemoMarkers.Clear();
        }

        public static void ClearMemoPlacement()
        {
            HasPendingMemoPlacement = false;
            MemoPlacementMapId = string.Empty;
            MemoPlacementScanId = string.Empty;
            MemoPlacementKind = string.Empty;
            MemoPlacementPosition = default;
            MemoPlacementRotation = Quaternion.identity;
        }

        public static void Clear()
        {
            ClearSceneState();
            ClearMemoPlacement();
        }

        private static string Escape(string value)
        {
            return UnityWebRequest.EscapeURL(value ?? string.Empty);
        }
    }
}
