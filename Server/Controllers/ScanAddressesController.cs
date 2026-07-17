using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASP.NET_core_MemoAnchor_Server.Controllers;

[ApiController]
[Authorize]
[Route("api/scan/addresses")]
public sealed class ScanAddressesController : ControllerBase
{
    private readonly IMapMemoStore mapMemoStore;

    public ScanAddressesController(IMapMemoStore mapMemoStore)
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

        IReadOnlyList<ScanAddressInfo> addresses = await mapMemoStore.LoadAddressesAsync(playerId, cancellationToken);
        return Ok(new ScanAddressListResponse(addresses));
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] SaveScanAddressRequest request, CancellationToken cancellationToken)
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

        IReadOnlyList<ScanAddressInfo> addresses = await mapMemoStore.AddAddressAsync(playerId, request, cancellationToken);
        return Ok(new ScanAddressListResponse(addresses));
    }

    private string? GetUnityPlayerId()
    {
        return User.FindFirstValue("sub")
            ?? User.FindFirstValue("player_id");
    }
}

public sealed record ScanAddressListResponse(IReadOnlyList<ScanAddressInfo> Addresses);
