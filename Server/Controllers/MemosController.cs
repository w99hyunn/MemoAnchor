using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASP.NET_core_MemoAnchor_Server.Controllers;

[ApiController]
[Authorize]
[Route("api/memos")]
public sealed class MemosController : ControllerBase
{
    private readonly IMapMemoStore mapMemoStore;
    private readonly IWebHostEnvironment environment;
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".heic", ".heif", ".bmp", ".tif", ".tiff"
    };
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".avi", ".webm", ".m4v", ".3gp", ".mkv"
    };

    public MemosController(IMapMemoStore mapMemoStore, IWebHostEnvironment environment)
    {
        this.mapMemoStore = mapMemoStore;
        this.environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        string? playerId = GetUnityPlayerId();
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return Unauthorized();
        }

        IReadOnlyList<MemoInfo> memos = await mapMemoStore.LoadMemosAsync(playerId, cancellationToken);
        return Ok(new MemoListResponse(memos));
    }

    [HttpGet("trash")]
    public async Task<IActionResult> GetTrash(CancellationToken cancellationToken)
    {
        string? playerId = GetUnityPlayerId();
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return Unauthorized();
        }

        IReadOnlyList<MemoInfo> memos = await mapMemoStore.LoadTrashedMemosAsync(playerId, cancellationToken);
        return Ok(new MemoListResponse(memos));
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] SaveMemoRequest request, CancellationToken cancellationToken)
    {
        string? playerId = GetUnityPlayerId();
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.MapId))
        {
            return BadRequest(new { message = "MapId is required." });
        }

        try
        {
            MemoCreateResult result = await mapMemoStore.AddMemoAsync(playerId, request, cancellationToken);
            return Ok(new MemoCreateResponse(result.Memo, result.Memos));
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("media")]
    [RequestSizeLimit(1_073_741_824)]
    public async Task<IActionResult> UploadMedia([FromQuery] string extension, CancellationToken cancellationToken)
    {
        string? playerId = GetUnityPlayerId();
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return Unauthorized();
        }

        string normalizedExtension = (Path.GetExtension(extension) ?? string.Empty).ToLowerInvariant();
        bool isImage = ImageExtensions.Contains(normalizedExtension);
        bool isVideo = VideoExtensions.Contains(normalizedExtension);
        if (!isImage && !isVideo)
        {
            return BadRequest(new { message = "Unsupported media file type." });
        }

        string contentType = Request.ContentType ?? string.Empty;
        if ((isImage && !contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            || (isVideo && !contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest(new { message = "Media content type does not match the file type." });
        }

        if (Request.ContentLength is <= 0)
        {
            return BadRequest(new { message = "Media file is empty." });
        }

        string webRootPath = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        string uploadDirectory = Path.Combine(webRootPath, "uploads", "memos");
        Directory.CreateDirectory(uploadDirectory);
        string fileName = $"{Guid.NewGuid():N}{normalizedExtension}";
        string filePath = Path.Combine(uploadDirectory, fileName);

        try
        {
            await using FileStream output = new(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            await Request.Body.CopyToAsync(output, cancellationToken);
        }
        catch (Microsoft.AspNetCore.Server.Kestrel.Core.BadHttpRequestException exception)
            when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            return StatusCode(StatusCodes.Status413PayloadTooLarge, new { message = "Media file is too large. The maximum size is 1 GB." });
        }
        catch
        {
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            throw;
        }

        return Ok(new { url = $"/uploads/memos/{fileName}" });
    }

    [HttpPost("voice")]
    [RequestSizeLimit(104_857_600)]
    public async Task<IActionResult> UploadVoice(CancellationToken cancellationToken)
    {
        string? playerId = GetUnityPlayerId();
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return Unauthorized();
        }

        if (!string.Equals(Request.ContentType, "audio/wav", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Voice content type must be audio/wav." });
        }

        if (Request.ContentLength is <= 0)
        {
            return BadRequest(new { message = "Voice file is empty." });
        }

        string webRootPath = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        string uploadDirectory = Path.Combine(webRootPath, "uploads", "memos");
        Directory.CreateDirectory(uploadDirectory);
        string fileName = $"{Guid.NewGuid():N}.wav";
        string filePath = Path.Combine(uploadDirectory, fileName);

        try
        {
            await using FileStream output = new(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            await Request.Body.CopyToAsync(output, cancellationToken);
        }
        catch (Microsoft.AspNetCore.Server.Kestrel.Core.BadHttpRequestException exception)
            when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            return StatusCode(StatusCodes.Status413PayloadTooLarge, new { message = "Voice file is too large. The maximum size is 100 MB." });
        }
        catch
        {
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

            throw;
        }

        return Ok(new { url = $"/uploads/memos/{fileName}" });
    }

    [HttpDelete("{memoId}")]
    public async Task<IActionResult> MoveToTrash(string memoId, CancellationToken cancellationToken)
    {
        return await HandleMemoMutation(memoId, (playerId, id) => mapMemoStore.MoveMemoToTrashAsync(playerId, id, cancellationToken));
    }

    [HttpPut("{memoId}")]
    public async Task<IActionResult> Update(string memoId, [FromBody] SaveMemoRequest request, CancellationToken cancellationToken)
    {
        string? playerId = GetUnityPlayerId();
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return Unauthorized();
        }

        try
        {
            MemoCreateResult result = await mapMemoStore.UpdateMemoAsync(playerId, memoId, request, cancellationToken);
            return Ok(new MemoCreateResponse(result.Memo, result.Memos));
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("{memoId}/restore")]
    public async Task<IActionResult> Restore(string memoId, CancellationToken cancellationToken)
    {
        return await HandleMemoMutation(memoId, (playerId, id) => mapMemoStore.RestoreMemoAsync(playerId, id, cancellationToken));
    }

    [HttpDelete("{memoId}/permanent")]
    public async Task<IActionResult> DeletePermanently(string memoId, CancellationToken cancellationToken)
    {
        return await HandleMemoMutation(memoId, (playerId, id) => mapMemoStore.DeleteMemoPermanentlyAsync(playerId, id, cancellationToken));
    }

    [HttpPost("{memoId}/work-status/{status}")]
    public async Task<IActionResult> SetWorkStatus(string memoId, string status, CancellationToken cancellationToken)
    {
        return await HandleMemoMutation(memoId, (playerId, id) => mapMemoStore.SetMemoWorkStatusAsync(playerId, id, status, cancellationToken));
    }

    private async Task<IActionResult> HandleMemoMutation(string memoId, Func<string, string, Task<IReadOnlyList<MemoInfo>>> action)
    {
        string? playerId = GetUnityPlayerId();
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return Unauthorized();
        }

        try
        {
            IReadOnlyList<MemoInfo> memos = await action(playerId, memoId);
            return Ok(new MemoListResponse(memos));
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return NotFound(new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private string? GetUnityPlayerId()
    {
        return User.FindFirstValue("sub")
            ?? User.FindFirstValue("player_id");
    }
}

public sealed record MemoListResponse(IReadOnlyList<MemoInfo> Memos);

public sealed record MemoCreateResponse(MemoInfo Memo, IReadOnlyList<MemoInfo> Memos);
