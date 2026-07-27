using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ASP.NET_core_MemoAnchor_Server.Controllers;

[ApiController]
[Authorize]
[Route("api/scan/maps/{mapId}/reconstruction")]
public sealed class ScanReconstructionController : ControllerBase
{
    private const string ZIP_CONTENT_TYPE = "application/zip";
    private const string SCAN_ID_HEADER = "X-MemoAnchor-Scan-Id";
    private const string FILE_NAME_HEADER = "X-MemoAnchor-Filename";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IMapMemoStore mapMemoStore;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ReconstructionOptions options;
    private readonly ILogger<ScanReconstructionController> logger;

    public ScanReconstructionController(
        IMapMemoStore mapMemoStore,
        IHttpClientFactory httpClientFactory,
        IOptions<ReconstructionOptions> options,
        ILogger<ScanReconstructionController> logger)
    {
        this.mapMemoStore = mapMemoStore;
        this.httpClientFactory = httpClientFactory;
        this.options = options.Value;
        this.logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetCurrent(string mapId, CancellationToken cancellationToken)
    {
        string? playerId = GetUnityPlayerId();
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return Unauthorized();
        }

        MapReconstructionInfo? reconstruction = await mapMemoStore.LoadMapReconstructionAsync(playerId, mapId, cancellationToken);
        return reconstruction == null
            ? NotFound(new { message = "Reconstruction not found." })
            : Ok(reconstruction);
    }

    [HttpPost("upload")]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> Upload(string mapId, CancellationToken cancellationToken)
    {
        string? playerId = GetUnityPlayerId();
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return Unauthorized();
        }

        if (!Guid.TryParse(mapId, out Guid mapGuid))
        {
            return BadRequest();
        }

        if (!await mapMemoStore.CanManageMapAsync(playerId, mapId, cancellationToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        if (!string.Equals(Request.ContentType, ZIP_CONTENT_TYPE, StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }

        long? contentLength = Request.ContentLength;
        if (!contentLength.HasValue)
        {
            return StatusCode(StatusCodes.Status411LengthRequired);
        }

        if (contentLength.Value <= 0 || contentLength.Value > options.MaxUploadBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        string clientScanId = NormalizeScanId(Request.Headers[SCAN_ID_HEADER].ToString());
        string serverScanId = $"{mapGuid:N}-{clientScanId}";
        await mapMemoStore.BeginMapReconstructionAsync(mapId, serverScanId, cancellationToken);

        using var proxyRequest = new HttpRequestMessage(HttpMethod.Post, "upload");
        proxyRequest.Headers.TryAddWithoutValidation(SCAN_ID_HEADER, serverScanId);
        proxyRequest.Headers.TryAddWithoutValidation(FILE_NAME_HEADER, Request.Headers[FILE_NAME_HEADER].ToString());
        proxyRequest.Content = new StreamContent(Request.Body);
        proxyRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(ZIP_CONTENT_TYPE);
        proxyRequest.Content.Headers.ContentLength = contentLength.Value;

        try
        {
            using HttpResponseMessage proxyResponse = await CreateClient().SendAsync(proxyRequest, cancellationToken);
            string responseBody = await proxyResponse.Content.ReadAsStringAsync(cancellationToken);
            if (proxyResponse.IsSuccessStatusCode)
            {
                ReconstructionStatusPayload status = ParseStatus(responseBody, serverScanId, "queued", "Upload received");
                await SaveStatusAsync(mapId, status, cancellationToken);
            }
            else
            {
                await mapMemoStore.UpdateMapReconstructionAsync(
                    mapId,
                    serverScanId,
                    "failed",
                    responseBody,
                    string.Empty,
                    DateTimeOffset.UtcNow,
                    cancellationToken);
            }

            return CopyTextResponse(proxyResponse, responseBody);
        }
        catch (HttpRequestException exception)
        {
            await MarkProxyFailureAsync(mapId, serverScanId, exception.Message, cancellationToken);
            logger.LogError(exception, "Reconstruction upload proxy failed for map {MapId}", mapId);
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Reconstruction server is unavailable." });
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            await MarkProxyFailureAsync(mapId, serverScanId, exception.Message, cancellationToken);
            logger.LogError(exception, "Reconstruction upload proxy timed out for map {MapId}", mapId);
            return StatusCode(StatusCodes.Status504GatewayTimeout, new { message = "Reconstruction upload timed out." });
        }
    }

    [HttpGet("{scanId}/status")]
    public async Task<IActionResult> Status(string mapId, string scanId, CancellationToken cancellationToken)
    {
        if (!await CanReadScanAsync(mapId, scanId, cancellationToken))
        {
            return new EmptyResult();
        }

        try
        {
            using HttpResponseMessage proxyResponse = await CreateClient().GetAsync(
                $"status/{Uri.EscapeDataString(scanId)}",
                cancellationToken);
            string responseBody = await proxyResponse.Content.ReadAsStringAsync(cancellationToken);
            if (proxyResponse.IsSuccessStatusCode)
            {
                ReconstructionStatusPayload status = ParseStatus(responseBody, scanId, "processing", string.Empty);
                await SaveStatusAsync(mapId, status, cancellationToken);
            }

            return CopyTextResponse(proxyResponse, responseBody);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Reconstruction status proxy failed for map {MapId}", mapId);
            return StatusCode(StatusCodes.Status502BadGateway, new { message = "Reconstruction server is unavailable." });
        }
    }

    [HttpGet("{scanId}/result")]
    public async Task Result(string mapId, string scanId, CancellationToken cancellationToken)
    {
        if (!await CanReadScanAsync(mapId, scanId, cancellationToken))
        {
            return;
        }

        try
        {
            using HttpResponseMessage proxyResponse = await CreateClient().GetAsync(
                $"result/{Uri.EscapeDataString(scanId)}",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            await CopyStreamResponseAsync(proxyResponse, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(exception, "Reconstruction result proxy failed for map {MapId}", mapId);
            Response.StatusCode = StatusCodes.Status502BadGateway;
        }
    }

    private async Task<bool> CanReadScanAsync(string mapId, string scanId, CancellationToken cancellationToken)
    {
        string? playerId = GetUnityPlayerId();
        if (string.IsNullOrWhiteSpace(playerId))
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return false;
        }

        if (!Guid.TryParse(mapId, out Guid mapGuid)
            || !scanId.StartsWith($"{mapGuid:N}-", StringComparison.OrdinalIgnoreCase))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return false;
        }

        MapReconstructionInfo? reconstruction = await mapMemoStore.LoadMapReconstructionAsync(playerId, mapId, cancellationToken);
        if (reconstruction == null
            || !string.Equals(reconstruction.ScanId, scanId, StringComparison.OrdinalIgnoreCase))
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return false;
        }

        return true;
    }

    private async Task SaveStatusAsync(string mapId, ReconstructionStatusPayload status, CancellationToken cancellationToken)
    {
        DateTimeOffset updatedAt = DateTimeOffset.TryParse(status.UpdatedAt, out DateTimeOffset parsed)
            ? parsed
            : DateTimeOffset.UtcNow;
        await mapMemoStore.UpdateMapReconstructionAsync(
            mapId,
            status.ScanId,
            status.State,
            status.Message,
            status.ResultFile,
            updatedAt,
            cancellationToken);
    }

    private async Task MarkProxyFailureAsync(string mapId, string scanId, string message, CancellationToken cancellationToken)
    {
        await mapMemoStore.UpdateMapReconstructionAsync(
            mapId,
            scanId,
            "failed",
            message,
            string.Empty,
            DateTimeOffset.UtcNow,
            cancellationToken);
    }

    private static ReconstructionStatusPayload ParseStatus(
        string json,
        string fallbackScanId,
        string fallbackState,
        string fallbackMessage)
    {
        ReconstructionStatusPayload? status = null;
        try
        {
            status = JsonSerializer.Deserialize<ReconstructionStatusPayload>(json, JsonOptions);
        }
        catch (JsonException)
        {
        }

        return new ReconstructionStatusPayload
        {
            ScanId = string.IsNullOrWhiteSpace(status?.ScanId) ? fallbackScanId : status.ScanId,
            State = string.IsNullOrWhiteSpace(status?.State) ? fallbackState : status.State,
            Message = string.IsNullOrWhiteSpace(status?.Message) ? fallbackMessage : status.Message,
            ResultFile = status?.ResultFile ?? string.Empty,
            UpdatedAt = status?.UpdatedAt ?? string.Empty
        };
    }

    private static ContentResult CopyTextResponse(HttpResponseMessage proxyResponse, string responseBody)
    {
        return new ContentResult
        {
            StatusCode = (int)proxyResponse.StatusCode,
            ContentType = proxyResponse.Content.Headers.ContentType?.ToString() ?? "application/json",
            Content = responseBody
        };
    }

    private HttpClient CreateClient()
    {
        return httpClientFactory.CreateClient(ReconstructionOptions.HTTP_CLIENT_NAME);
    }

    private async Task CopyStreamResponseAsync(HttpResponseMessage proxyResponse, CancellationToken cancellationToken)
    {
        Response.StatusCode = (int)proxyResponse.StatusCode;
        if (proxyResponse.Content.Headers.ContentType != null)
        {
            Response.ContentType = proxyResponse.Content.Headers.ContentType.ToString();
        }
        Response.ContentLength = proxyResponse.Content.Headers.ContentLength;

        if (proxyResponse.Content.Headers.ContentDisposition != null)
        {
            Response.Headers["Content-Disposition"] = proxyResponse.Content.Headers.ContentDisposition.ToString();
        }

        await proxyResponse.Content.CopyToAsync(Response.Body, cancellationToken);
    }

    private static string NormalizeScanId(string value)
    {
        string normalized = new(value
            .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            .Take(80)
            .ToArray());
        return string.IsNullOrWhiteSpace(normalized)
            ? DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss")
            : normalized;
    }

    private string? GetUnityPlayerId()
    {
        return User.FindFirstValue("sub")
            ?? User.FindFirstValue("player_id");
    }

    private sealed class ReconstructionStatusPayload
    {
        public string ScanId { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string ResultFile { get; set; } = string.Empty;
        public string UpdatedAt { get; set; } = string.Empty;
    }
}
