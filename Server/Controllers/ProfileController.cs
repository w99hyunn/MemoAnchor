using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ASP.NET_core_MemoAnchor_Server.Controllers;

[ApiController]
[Authorize]
[Route("api/profile")]
public sealed class ProfileController : ControllerBase
{
    private readonly IProfileStore profileStore;

    public ProfileController(IProfileStore profileStore)
    {
        this.profileStore = profileStore;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
    {
        string? playerId = GetUnityPlayerId();
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return Unauthorized();
        }

        UserAccountInfo? accountInfo = await profileStore.LoadUserAccountInfoAsync(playerId, cancellationToken);
        if (accountInfo == null)
        {
            return Ok(new { exists = false });
        }

        return Ok(new
        {
            exists = true,
            name = accountInfo.Name,
            email = accountInfo.Email,
            companyName = accountInfo.CompanyName,
            updatedAt = accountInfo.UpdatedAt,
        });
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SaveProfileRequest request, CancellationToken cancellationToken)
    {
        string? playerId = GetUnityPlayerId();
        if (string.IsNullOrWhiteSpace(playerId))
        {
            return Unauthorized();
        }

        UserAccountInfo accountInfo = new(
            request.Name,
            request.Email,
            request.CompanyName,
            DateTimeOffset.UtcNow);

        await profileStore.SaveUserAccountInfoAsync(playerId, accountInfo, cancellationToken);
        return Ok(accountInfo);
    }

    private string? GetUnityPlayerId()
    {
        return User.FindFirstValue("sub")
            ?? User.FindFirstValue("player_id");
    }
}

public sealed class SaveProfileRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
}
