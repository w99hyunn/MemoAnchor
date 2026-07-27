using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class GeminiSemanticMapClient : MonoBehaviour
{
    [SerializeField] private string modelName = "gemini-2.5-flash";
    [SerializeField] private float temperature = 0.2f;
    [SerializeField] private int requestTimeoutSeconds = 120;
    [SerializeField] private int maxRetryCount = 3;
    [SerializeField] private float initialRetryDelaySeconds = 2f;

    private const string API_KEY_NAME = "GEMINI_API_KEY";
    private const string ENDPOINT_FORMAT = "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent";

    public async Awaitable GenerateSemanticMapAsync(
        SemanticScanPackage scanPackage,
        IReadOnlyList<GeminiImageInput> images,
        Action<string, GeminiSemanticMapResult> onSuccess,
        Action<string> onError,
        Action<int, int, long, float> onRetry = null)
    {
        var apiKey = GeminiEnvLoader.Get(API_KEY_NAME);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            onError?.Invoke("GEMINI_API_KEY is missing. Add it to .env or StreamingAssets/gemini.env.");
            return;
        }

        var packageJson = JsonUtility.ToJson(scanPackage, true);
        var requestJson = BuildRequestJson(packageJson, images);
        var requestBytes = Encoding.UTF8.GetBytes(requestJson);
        var endpoint = string.Format(ENDPOINT_FORMAT, modelName);
        var maxAttempts = Mathf.Max(1, maxRetryCount + 1);
        string lastError = null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using (var request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(requestBytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = Mathf.Max(10, requestTimeoutSeconds);
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("x-goog-api-key", apiKey);

                UnityWebRequestAsyncOperation operation = request.SendWebRequest();
                while (!operation.isDone)
                    await Awaitable.NextFrameAsync();

                var body = request.downloadHandler != null ? request.downloadHandler.text : string.Empty;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    lastError = $"Gemini request failed ({request.responseCode}): {request.error}\n{body}";
                    if (attempt < maxAttempts && IsRetryable(request.result, request.responseCode))
                    {
                        var delay = GetRetryDelaySeconds(attempt);
                        onRetry?.Invoke(attempt, maxAttempts, request.responseCode, delay);
                        await Awaitable.WaitForSecondsAsync(delay);
                        continue;
                    }

                    onError?.Invoke(attempt > 1 ? $"{lastError}\nAttempts: {attempt}/{maxAttempts}" : lastError);
                    return;
                }

                var text = ExtractFirstText(body);
                if (string.IsNullOrWhiteSpace(text))
                {
                    onError?.Invoke("Gemini response did not contain text.");
                    return;
                }

                var semanticJson = CleanJsonText(text);
                GeminiSemanticMapResult parsed = null;
                try
                {
                    parsed = JsonUtility.FromJson<GeminiSemanticMapResult>(semanticJson);
                }
                catch (Exception ex)
                {
                    onError?.Invoke($"Could not parse Gemini semantic JSON: {ex.Message}\n{text}");
                    return;
                }

                onSuccess?.Invoke(semanticJson, parsed);
                return;
            }
        }

        onError?.Invoke(lastError ?? "Gemini request failed.");
    }

    private float GetRetryDelaySeconds(int failedAttempt)
    {
        var baseDelay = Mathf.Max(0.5f, initialRetryDelaySeconds);
        var exponentialDelay = baseDelay * Mathf.Pow(2f, Mathf.Max(0, failedAttempt - 1));
        var jitter = UnityEngine.Random.Range(0f, 0.35f * baseDelay);
        return Mathf.Min(20f, exponentialDelay + jitter);
    }

    private static bool IsRetryable(UnityWebRequest.Result result, long responseCode)
    {
        if (result == UnityWebRequest.Result.ConnectionError)
            return true;

        if (result == UnityWebRequest.Result.DataProcessingError)
            return false;

        return responseCode == 408 || responseCode == 429 || responseCode == 500 || responseCode == 502 || responseCode == 503 || responseCode == 504;
    }

    private string BuildRequestJson(string packageJson, IReadOnlyList<GeminiImageInput> images)
    {
        var prompt =
            "You are enriching an ARKit LiDAR mesh scan into a semantic map layer. " +
            "Use the mesh/keyframe JSON for geometry and pose, and the images for color, materials, objects, and room context. " +
            "Return only valid JSON matching this shape: " +
            "{\"summary\":\"...\",\"dominantPalette\":[{\"name\":\"warm white\",\"hex\":\"#F5F0E8\",\"confidence\":0.8}]," +
            "\"zones\":[{\"name\":\"desk area\",\"description\":\"...\",\"position\":{\"x\":0,\"y\":0,\"z\":0},\"confidence\":0.7}]," +
            "\"objects\":[{\"name\":\"chair\",\"category\":\"furniture\",\"color\":\"black\",\"position\":{\"x\":0,\"y\":0,\"z\":0},\"evidenceKeyframe\":\"keyframe_001.jpg\",\"confidence\":0.7}]," +
            "\"surfaces\":[{\"type\":\"wall\",\"color\":\"white\",\"material\":\"paint\",\"position\":{\"x\":0,\"y\":0,\"z\":0},\"confidence\":0.7}]," +
            "\"mapNotes\":[\"...\"]}. " +
            "If exact 3D location is uncertain, estimate from camera pose/surfacePoint and lower confidence. " +
            "Do not invent objects that are not visible.";

        var builder = new StringBuilder(1024 + packageJson.Length);
        builder.Append("{\"contents\":[{\"role\":\"user\",\"parts\":[");
        AppendTextPart(builder, prompt);
        builder.Append(',');
        AppendTextPart(builder, "AR scan JSON:\n" + packageJson);

        if (images != null)
        {
            foreach (var image in images)
            {
                if (image == null || image.Bytes == null || image.Bytes.Length == 0)
                    continue;

                builder.Append(',');
                AppendTextPart(builder, $"Image file: {image.FileName}");
                builder.Append(',');
                AppendInlineImagePart(builder, image.MimeType, Convert.ToBase64String(image.Bytes));
            }
        }

        builder.Append("]}],\"generationConfig\":{\"temperature\":");
        builder.Append(temperature.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.Append(",\"responseMimeType\":\"application/json\"}}");
        return builder.ToString();
    }

    private static void AppendTextPart(StringBuilder builder, string text)
    {
        builder.Append("{\"text\":\"");
        AppendEscapedJson(builder, text);
        builder.Append("\"}");
    }

    private static void AppendInlineImagePart(StringBuilder builder, string mimeType, string base64Data)
    {
        builder.Append("{\"inlineData\":{\"mimeType\":\"");
        AppendEscapedJson(builder, string.IsNullOrWhiteSpace(mimeType) ? "image/jpeg" : mimeType);
        builder.Append("\",\"data\":\"");
        AppendEscapedJson(builder, base64Data);
        builder.Append("\"}}");
    }

    private static void AppendEscapedJson(StringBuilder builder, string value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        foreach (var c in value)
        {
            switch (c)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                default:
                    if (c < 32)
                    {
                        builder.Append("\\u");
                        builder.Append(((int)c).ToString("x4"));
                    }
                    else
                    {
                        builder.Append(c);
                    }
                    break;
            }
        }
    }

    private static string ExtractFirstText(string responseJson)
    {
        if (string.IsNullOrWhiteSpace(responseJson))
            return null;

        try
        {
            var response = JsonUtility.FromJson<GeminiGenerateResponse>(responseJson);
            if (response?.candidates == null)
                return null;

            foreach (var candidate in response.candidates)
            {
                if (candidate?.content?.parts == null)
                    continue;

                foreach (var part in candidate.content.parts)
                {
                    if (!string.IsNullOrWhiteSpace(part.text))
                        return part.text;
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string CleanJsonText(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
            return trimmed;

        var firstNewline = trimmed.IndexOf('\n');
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (firstNewline >= 0 && lastFence > firstNewline)
            return trimmed.Substring(firstNewline + 1, lastFence - firstNewline - 1).Trim();

        return trimmed.Trim('`').Trim();
    }

#pragma warning disable 0649
    [Serializable]
    private sealed class GeminiContent
    {
        public string role;
        public GeminiPart[] parts;
    }

    [Serializable]
    private sealed class GeminiPart
    {
        public string text;
        public GeminiInlineData inlineData;
    }

    [Serializable]
    private sealed class GeminiInlineData
    {
        public string mimeType;
        public string data;
    }

    [Serializable]
    private sealed class GeminiGenerateResponse
    {
        public GeminiCandidate[] candidates;
    }

    [Serializable]
    private sealed class GeminiCandidate
    {
        public GeminiContent content;
    }
#pragma warning restore 0649
}

public static class GeminiEnvLoader
{
    public static string Get(string key)
    {
        var environmentValue = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrWhiteSpace(environmentValue))
            return environmentValue.Trim();

        foreach (var path in CandidateEnvPaths())
        {
            var value = ReadValue(path, key);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static IEnumerable<string> CandidateEnvPaths()
    {
        yield return Path.Combine(Application.persistentDataPath, ".env");
        yield return Path.Combine(Application.persistentDataPath, "gemini.env");

        if (!string.IsNullOrEmpty(Application.streamingAssetsPath))
        {
            yield return Path.Combine(Application.streamingAssetsPath, ".env");
            yield return Path.Combine(Application.streamingAssetsPath, "gemini.env");
        }

#if UNITY_EDITOR
        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (!string.IsNullOrEmpty(projectRoot))
        {
            yield return Path.Combine(projectRoot, ".env");
            yield return Path.Combine(projectRoot, "gemini.env");
        }
#endif
    }

    private static string ReadValue(string path, string key)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        try
        {
            foreach (var rawLine in File.ReadAllLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                var separator = line.IndexOf('=');
                if (separator <= 0)
                    continue;

                var name = line.Substring(0, separator).Trim();
                if (!string.Equals(name, key, StringComparison.Ordinal))
                    continue;

                var value = line.Substring(separator + 1).Trim();
                return value.Trim('"').Trim('\'');
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GeminiEnvLoader] Could not read {path}: {ex.Message}");
        }

        return null;
    }
}

public sealed class GeminiSemanticLabelBillboard : MonoBehaviour
{
    public Camera TargetCamera;

    private void LateUpdate()
    {
        var cameraToUse = TargetCamera ? TargetCamera : Camera.main;
        if (!cameraToUse)
            return;

        transform.rotation = Quaternion.LookRotation(transform.position - cameraToUse.transform.position, Vector3.up);
    }
}

[Serializable]
public sealed class GeminiImageInput
{
    public string FileName;
    public string FilePath;
    public string MimeType;
    public byte[] Bytes;
}

[Serializable]
public sealed class SemanticScanPackage
{
    public string schemaVersion;
    public string scanId;
    public string capturedAtUtc;
    public float durationSeconds;
    public BoundsDto bounds;
    public MeshSummaryDto mesh;
    public CameraKeyframeDto[] keyframes;
    public MeshSampleDto[] meshSamples;
}

[Serializable]
public sealed class MeshSummaryDto
{
    public int meshCount;
    public int vertexCount;
    public int triangleCount;
}

[Serializable]
public sealed class CameraKeyframeDto
{
    public string id;
    public string imageFileName;
    public string imagePath;
    public float timestampSeconds;
    public Vector3Dto position;
    public QuaternionDto rotation;
    public ColorDto sampleColor;
    public bool hasSurfacePoint;
    public Vector3Dto surfacePoint;
}

[Serializable]
public sealed class MeshSampleDto
{
    public Vector3Dto centroid;
    public Vector3Dto normal;
    public string surfaceHint;
}

[Serializable]
public sealed class BoundsDto
{
    public Vector3Dto center;
    public Vector3Dto size;
    public Vector3Dto min;
    public Vector3Dto max;
}

[Serializable]
public sealed class Vector3Dto
{
    public float x;
    public float y;
    public float z;

    public static Vector3Dto From(Vector3 value)
    {
        return new Vector3Dto { x = value.x, y = value.y, z = value.z };
    }

    public Vector3 ToVector3()
    {
        return new Vector3(x, y, z);
    }
}

[Serializable]
public sealed class QuaternionDto
{
    public float x;
    public float y;
    public float z;
    public float w;

    public static QuaternionDto From(Quaternion value)
    {
        return new QuaternionDto { x = value.x, y = value.y, z = value.z, w = value.w };
    }
}

[Serializable]
public sealed class ColorDto
{
    public float r;
    public float g;
    public float b;
    public float a;
    public string hex;

    public static ColorDto From(Color value)
    {
        return new ColorDto
        {
            r = value.r,
            g = value.g,
            b = value.b,
            a = value.a,
            hex = $"#{ColorUtility.ToHtmlStringRGB(value)}"
        };
    }
}

[Serializable]
public sealed class GeminiSemanticMapResult
{
    public string summary;
    public PaletteColorDto[] dominantPalette;
    public SemanticZoneDto[] zones;
    public SemanticObjectDto[] objects;
    public SemanticSurfaceDto[] surfaces;
    public string[] mapNotes;
}

[Serializable]
public sealed class PaletteColorDto
{
    public string name;
    public string hex;
    public float confidence;
}

[Serializable]
public sealed class SemanticZoneDto
{
    public string name;
    public string description;
    public Vector3Dto position;
    public float confidence;
}

[Serializable]
public sealed class SemanticObjectDto
{
    public string name;
    public string category;
    public string color;
    public Vector3Dto position;
    public string evidenceKeyframe;
    public float confidence;
}

[Serializable]
public sealed class SemanticSurfaceDto
{
    public string type;
    public string color;
    public string material;
    public Vector3Dto position;
    public float confidence;
}
