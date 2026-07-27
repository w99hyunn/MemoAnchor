using UnityEngine.Networking;

namespace MemoAnchor
{
    public static class MapScanSession
    {
        public const string SCAN_SCENE_NAME = "ARKitMeshScanScene";

        public static string MapId { get; private set; } = string.Empty;
        public static string ReconstructionScanId { get; private set; } = string.Empty;
        public static string ReconstructionResultFile { get; private set; } = string.Empty;
        public static ScanMapCreateRequest PendingMapRequest { get; private set; }

        public static bool HasActiveMap => !string.IsNullOrWhiteSpace(MapId);
        public static bool HasPendingMap => PendingMapRequest != null;
        public static bool HasScanTarget => HasActiveMap || HasPendingMap;
        public static bool IsViewingStoredResult => HasActiveMap && !string.IsNullOrWhiteSpace(ReconstructionScanId);

        public static void BeginScan(ScanMapCreateRequest pendingMapRequest)
        {
            MapId = string.Empty;
            ReconstructionScanId = string.Empty;
            ReconstructionResultFile = string.Empty;
            PendingMapRequest = pendingMapRequest;
        }

        public static void ConfirmMap(string mapId)
        {
            MapId = mapId?.Trim() ?? string.Empty;
            PendingMapRequest = null;
        }

        public static void BeginResultView(string mapId, string scanId, string resultFile)
        {
            MapId = mapId?.Trim() ?? string.Empty;
            ReconstructionScanId = scanId?.Trim() ?? string.Empty;
            ReconstructionResultFile = resultFile?.Trim() ?? string.Empty;
            PendingMapRequest = null;
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

        public static void Clear()
        {
            MapId = string.Empty;
            ReconstructionScanId = string.Empty;
            ReconstructionResultFile = string.Empty;
            PendingMapRequest = null;
        }

        private static string Escape(string value)
        {
            return UnityWebRequest.EscapeURL(value ?? string.Empty);
        }
    }
}
