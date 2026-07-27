using UnityEngine.Networking;

namespace MemoAnchor
{
    public static class MapScanSession
    {
        public const string SCAN_SCENE_NAME = "ARKitMeshScanScene";

        public static string MapId { get; private set; } = string.Empty;

        public static bool HasActiveMap => !string.IsNullOrWhiteSpace(MapId);

        public static void Begin(string mapId)
        {
            MapId = mapId?.Trim() ?? string.Empty;
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
        }

        private static string Escape(string value)
        {
            return UnityWebRequest.EscapeURL(value ?? string.Empty);
        }
    }
}
