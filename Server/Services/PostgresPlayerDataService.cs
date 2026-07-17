using ASP.NET_core_MemoAnchor_Server.Data;
using Microsoft.EntityFrameworkCore;

public sealed class PostgresPlayerDataService : IProfileStore
{
    private readonly MemoAnchorDbContext db;

    public PostgresPlayerDataService(MemoAnchorDbContext db)
    {
        this.db = db;
    }

    public async Task<UserAccountInfo?> LoadUserAccountInfoAsync(string playerId, CancellationToken cancellationToken)
    {
        AppUserEntity? user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.UnityPlayerId == playerId, cancellationToken);

        return user == null
            ? null
            : new UserAccountInfo(user.Name, user.Email, user.CompanyName, user.UpdatedAt);
    }

    public async Task SaveUserAccountInfoAsync(string playerId, UserAccountInfo accountInfo, CancellationToken cancellationToken)
    {
        AppUserEntity? user = await db.Users
            .FirstOrDefaultAsync(item => item.UnityPlayerId == playerId, cancellationToken);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (user == null)
        {
            user = new AppUserEntity
            {
                Id = Guid.NewGuid(),
                UnityPlayerId = playerId,
                CreatedAt = now
            };
            db.Users.Add(user);
        }

        user.Name = Normalize(accountInfo.Name);
        user.Email = Normalize(accountInfo.Email);
        user.CompanyName = Normalize(accountInfo.CompanyName);
        user.UpdatedAt = now;

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string Normalize(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }
}
