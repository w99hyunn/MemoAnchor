using System.Net.Http.Headers;
using System.Security.Claims;
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

    private readonly IMapMemoStore mapMemoStore;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly ReconstructionOptions options;

    public ScanReconstructionController(
        IMapMemoStore mapMemoStore,
        IHttpClientFactory httpClientFactory,
        IOptions<ReconstructionOptions> options)
    {
        this.mapMemoStore = mapMemoStore;
        this.httpClientFactory = httpClientFactory;
        this.options = options.Value;
    }

    [HttpPost("upload")]
    [DisableRequestSizeLimit]
    public async Task Upload(string mapId, CancellationToken cancellationToken)
    {
        string? playerId = GetUnityPlayerId();
        if (string.IsNullOrWhiteSpace(playerId))
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (!Guid.TryParse(mapId, out Guid mapGuid))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (!await mapMemoStore.CanManageMapAsync(playerId, mapId, cancellationToken))
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (!string.Equals(Request.ContentType, ZIP_CONTENT_TYPE, StringComparison.OrdinalIgnoreCase))
        {
            Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
            return;
        }

        long? contentLength = Request.ContentLength;
        if (!contentLength.HasValue)
        {
            Response.StatusCode = StatusCodes.Status411LengthRequired;
            return;
        }

        if (contentLength.Value <= 0 || contentLength.Value > options.MaxUploadBytes)
        {
            Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

        string clientScanId = NormalizeScanId(Request.Headers[SCAN_ID_HEADER].ToString());
        string serverScanId = $"{mapGuid:N}-{clientScanId}";

        using var proxyRequest = new HttpRequestMessage(HttpMethod.Post, "upload");
        proxyRequest.Headers.TryAddWithoutValidation(SCAN_ID_HEADER, serverScanId);
        proxyRequest.Headers.TryAddWithoutValidation(FILE_NAME_HEADER, Request.Headers[FILE_NAME_HEADER].ToString());
        proxyRequest.Content = new StreamContent(Request.Body);
        proxyRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(ZIP_CONTENT_TYPE);
        proxyRequest.Content.Headers.ContentLength = contentLength.Value;

        using HttpResponseMessage proxyResponse = await CreateClient().SendAsync(
            proxyRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        await CopyResponseAsync(proxyResponse, cancellationToken);
    }

    [HttpGet("{scanId}/status")]
    public async Task Status(string mapId, string scanId, CancellationToken cancellationToken)
    {
        if (!await CanReadScanAsync(mapId, scanId, cancellationToken))
        {
            return;
        }

        using HttpResponseMessage proxyResponse = await CreateClient().GetAsync(
            $"status/{Uri.EscapeDataString(scanId)}",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        await CopyResponseAsync(proxyResponse, cancellationToken);
    }

    [HttpGet("{scanId}/result")]
    public async Task Result(string mapId, string scanId, CancellationToken cancellationToken)
    {
        if (!await CanReadScanAsync(mapId, scanId, cancellationToken))
        {
            return;
        }

        using HttpResponseMessage proxyResponse = await CreateClient().GetAsync(
            $"result/{Uri.EscapeDataString(scanId)}",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        await CopyResponseAsync(proxyResponse, cancellationToken);
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

        if (!await mapMemoStore.CanAccessMapAsync(playerId, mapId, cancellationToken))
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return false;
        }

        return true;
    }

    private HttpClient CreateClient()
    {
        return httpClientFactory.CreateClient(ReconstructionOptions.HTTP_CLIENT_NAME);
    }

    private async Task CopyResponseAsync(HttpResponseMessage proxyResponse, CancellationToken cancellationToken)
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
}
