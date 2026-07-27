using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASP.NET_core_MemoAnchor_Server.Controllers;

[ApiController]
[Authorize]
[Route("api/scan/maps")]
public sealed class ScanMapsController : ControllerBase
{
    private readonly IMapMemoStore mapMemoStore;

    public ScanMapsController(IMapMemoStore mapMemoStore)
    {
        this.mapMemoStore = mapMemoStore;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        string? playerId = GetUnityPlayerId();
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return Unauthorized();
        }

        IReadOnlyList<ScanMapInfo> maps = await mapMemoStore.LoadMapsAsync(playerId, cancellationToken);
        return Ok(new ScanMapListResponse(maps));
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] SaveScanMapRequest request, CancellationToken cancellationToken)
    {
        string? playerId = GetUnityPlayerId();
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Address))
        {
            return BadRequest(new { message = "Address is required." });
        }

        if (string.IsNullOrWhiteSpace(request.SpaceName))
        {
            return BadRequest(new { message = "SpaceName is required." });
        }

        ScanMapCreateInfo result = await mapMemoStore.AddMapAsync(playerId, request, cancellationToken);
        return Ok(new ScanMapCreateResponse(result.CreatedMapId, result.Maps));
    }

    [HttpPost("{mapId}/invite")]
    public async Task<IActionResult> IssueInvite(string mapId, CancellationToken cancellationToken)
    {
        string? playerId = GetUnityPlayerId();
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return Unauthorized();
        }

        try
        {
            MapInviteInfo invite = await mapMemoStore.IssueMapInviteAsync(playerId, mapId, cancellationToken);
            return Ok(invite);
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

    [HttpPost("{mapId}/members")]
    public async Task<IActionResult> AddMembers(string mapId, [FromBody] InviteMapMembersRequest request, CancellationToken cancellationToken)
    {
        string? playerId = GetUnityPlayerId();
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return Unauthorized();
        }

        try
        {
            IReadOnlyList<ScanMapInfo> maps = await mapMemoStore.AddMapMembersAsync(playerId, mapId, request.Members, cancellationToken);
            return Ok(new ScanMapListResponse(maps));
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("friend-profiles")]
    public async Task<IActionResult> GetFriendProfiles([FromBody] MapFriendProfilesRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(GetUnityPlayerId()))
        {
            return Unauthorized();
        }

        IReadOnlyList<MapFriendProfileInfo> profiles = await mapMemoStore.LoadMapFriendProfilesAsync(request.PlayerIds, cancellationToken);
        return Ok(new { profiles });
    }

    [HttpPost("read-only")]
    public async Task<IActionResult> OpenReadOnly([FromBody] ReadOnlyMapRequest request, CancellationToken cancellationToken)
    {
        string? playerId = GetUnityPlayerId();
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return Unauthorized();
        }

        ReadOnlyMapInfo? result = await mapMemoStore.LoadReadOnlyMapAsync(playerId, request.Code, request.JoinAsMember, request.JoinAsReader, cancellationToken);
        return result == null ? NotFound(new { message = "유효한 참여 코드를 찾지 못했습니다." }) : Ok(result);
    }

    [HttpPost("{mapId}/members/{memberPlayerId}/promote")]
    public async Task<IActionResult> PromoteMember(string mapId, string memberPlayerId, CancellationToken cancellationToken)
    {
        return await HandleMemberMutation(mapId, memberPlayerId,
            (playerId, id, targetId) => mapMemoStore.PromoteMapMemberAsync(playerId, id, targetId, cancellationToken));
    }

    [HttpDelete("{mapId}/members/{memberPlayerId}")]
    public async Task<IActionResult> RemoveMember(string mapId, string memberPlayerId, CancellationToken cancellationToken)
    {
        return await HandleMemberMutation(mapId, memberPlayerId,
            (playerId, id, targetId) => mapMemoStore.RemoveMapMemberAsync(playerId, id, targetId, cancellationToken));
    }

    [HttpDelete("{mapId}")]
    public async Task<IActionResult> DeleteMap(string mapId, CancellationToken cancellationToken)
    {
        string? playerId = GetUnityPlayerId();
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return Unauthorized();
        }

        try
        {
            IReadOnlyList<ScanMapInfo> maps = await mapMemoStore.DeleteMapAsync(playerId, mapId, cancellationToken);
            return Ok(new ScanMapListResponse(maps));
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private async Task<IActionResult> HandleMemberMutation(string mapId, string memberPlayerId, Func<string, string, string, Task<IReadOnlyList<ScanMapInfo>>> mutation)
    {
        string? playerId = GetUnityPlayerId();
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return Unauthorized();
        }

        try
        {
            IReadOnlyList<ScanMapInfo> maps = await mutation(playerId, mapId, memberPlayerId);
            return Ok(new ScanMapListResponse(maps));
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
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

public sealed record ScanMapListResponse(IReadOnlyList<ScanMapInfo> Maps);
public sealed record ScanMapCreateResponse(string CreatedMapId, IReadOnlyList<ScanMapInfo> Maps);
