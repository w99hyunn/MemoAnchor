using System.Text.Json;
using System.Security.Cryptography;
using ASP.NET_core_MemoAnchor_Server.Data;
using Microsoft.EntityFrameworkCore;

public sealed class PostgresMapMemoStore : IMapMemoStore
{
    private const string MAP_INVITE_CODE_CHARACTERS = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly MemoAnchorDbContext db;
    private readonly ILogger<PostgresMapMemoStore> logger;
    private readonly string memoUploadDirectory;

    public PostgresMapMemoStore(MemoAnchorDbContext db, IWebHostEnvironment environment, ILogger<PostgresMapMemoStore> logger)
    {
        this.db = db;
        this.logger = logger;
        string webRootPath = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        memoUploadDirectory = Path.GetFullPath(Path.Combine(webRootPath, "uploads", "memos"));
    }

    public async Task<IReadOnlyList<ScanAddressInfo>> LoadAddressesAsync(string playerId, CancellationToken cancellationToken)
    {
        return await db.Addresses
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => ToScanAddressInfo(item))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ScanAddressInfo>> AddAddressAsync(string playerId, SaveScanAddressRequest request, CancellationToken cancellationToken)
    {
        await EnsureAddressAsync(request, cancellationToken);
        return await LoadAddressesAsync(playerId, cancellationToken);
    }

    public async Task<IReadOnlyList<ScanMapInfo>> LoadMapsAsync(string playerId, CancellationToken cancellationToken)
    {
        List<MapEntity> maps = await db.Maps
            .AsNoTracking()
            .Include(item => item.Address)
            .Include(item => item.Members)
            .Where(item => item.Members.Any(member => member.UnityPlayerId == playerId))
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        List<string> memberPlayerIds = maps.SelectMany(map => map.Members).Select(member => member.UnityPlayerId).Distinct().ToList();
        Dictionary<string, AppUserEntity> usersByPlayerId = await db.Users
            .AsNoTracking()
            .Where(user => memberPlayerIds.Contains(user.UnityPlayerId))
            .ToDictionaryAsync(user => user.UnityPlayerId, StringComparer.OrdinalIgnoreCase, cancellationToken);

        return maps.Select(map => ToScanMapInfo(map, playerId, usersByPlayerId)).ToList();
    }

    public async Task<ScanMapCreateInfo> AddMapAsync(string playerId, SaveScanMapRequest request, CancellationToken cancellationToken)
    {
        string spaceName = Normalize(request.SpaceName);
        if (string.IsNullOrWhiteSpace(spaceName))
        {
            throw new ArgumentException("SpaceName is required.", nameof(request));
        }

        AddressEntity address = await EnsureAddressAsync(request, cancellationToken);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Dictionary<string, string> rolesByPlayerId = new(StringComparer.OrdinalIgnoreCase)
        {
            [playerId] = ScanMapRoles.MANAGER
        };

        string managerPlayerId = Normalize(request.ManagerPlayerId);
        if (!string.IsNullOrWhiteSpace(managerPlayerId))
        {
            rolesByPlayerId[managerPlayerId] = ScanMapRoles.MANAGER;
        }

        string repairerPlayerId = Normalize(request.RepairerPlayerId);
        if (!string.IsNullOrWhiteSpace(repairerPlayerId) && !rolesByPlayerId.ContainsKey(repairerPlayerId))
        {
            rolesByPlayerId[repairerPlayerId] = ScanMapRoles.REPAIRER;
        }

        var map = new MapEntity
        {
            Id = Guid.NewGuid(),
            AddressId = address.Id,
            SpaceName = spaceName,
            CreatedAt = now,
            ScanCreatedAt = now,
            Members = rolesByPlayerId
                .Select(item => new MapMemberEntity { UnityPlayerId = item.Key, Role = item.Value })
                .ToList()
        };

        db.Maps.Add(map);
        await db.SaveChangesAsync(cancellationToken);
        IReadOnlyList<ScanMapInfo> maps = await LoadMapsAsync(playerId, cancellationToken);
        return new ScanMapCreateInfo(map.Id.ToString("N"), maps);
    }

    public async Task<bool> CanAccessMapAsync(string playerId, string mapId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(mapId, out Guid id))
        {
            return false;
        }

        return await db.MapMembers
            .AsNoTracking()
            .AnyAsync(member => member.MapId == id && member.UnityPlayerId == playerId, cancellationToken);
    }

    public async Task<bool> CanManageMapAsync(string playerId, string mapId, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(mapId, out Guid id))
        {
            return false;
        }

        return await db.MapMembers
            .AsNoTracking()
            .AnyAsync(member => member.MapId == id
                && member.UnityPlayerId == playerId
                && member.Role == ScanMapRoles.MANAGER, cancellationToken);
    }

    public async Task<MapInviteInfo> IssueMapInviteAsync(string playerId, string mapId, CancellationToken cancellationToken)
    {
        MapEntity map = await LoadManagedMapAsync(playerId, mapId, cancellationToken);
        string inviteCode;
        do
        {
            inviteCode = GenerateMapInviteCode();
        }
        while (await db.Maps.AnyAsync(item => item.InviteCode == inviteCode && item.InviteCodeExpiresAt > DateTimeOffset.UtcNow, cancellationToken));

        map.InviteCode = inviteCode;
        map.InviteCodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
        await db.SaveChangesAsync(cancellationToken);
        return new MapInviteInfo(map.InviteCode, map.InviteCodeExpiresAt.Value);
    }

    public async Task<IReadOnlyList<ScanMapInfo>> AddMapMembersAsync(string playerId, string mapId, IReadOnlyList<InviteMapMemberInfo> members, CancellationToken cancellationToken)
    {
        MapEntity map = await LoadManagedMapAsync(playerId, mapId, cancellationToken);
        Dictionary<string, MapMemberEntity> existingMembers = map.Members
            .ToDictionary(member => member.UnityPlayerId, StringComparer.OrdinalIgnoreCase);
        List<InviteMapMemberInfo> normalizedMembers = members
            .Select(member => new InviteMapMemberInfo(Normalize(member.PlayerId), Normalize(member.Name), Normalize(member.CompanyName)))
            .Where(member => !string.IsNullOrWhiteSpace(member.PlayerId))
            .GroupBy(member => member.PlayerId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(20)
            .ToList();
        if (normalizedMembers.Count == 0)
        {
            return await LoadMapsAsync(playerId, cancellationToken);
        }

        foreach (InviteMapMemberInfo member in normalizedMembers)
        {
            if (existingMembers.TryGetValue(member.PlayerId, out MapMemberEntity? existingMember))
            {
                if (string.Equals(existingMember.Role, ScanMapRoles.READ_ONLY, StringComparison.OrdinalIgnoreCase))
                {
                    existingMember.Role = ScanMapRoles.REPAIRER;
                    existingMember.DisplayName = member.Name;
                    existingMember.CompanyName = member.CompanyName;
                }
                continue;
            }

            map.Members.Add(new MapMemberEntity
            {
                MapId = map.Id,
                UnityPlayerId = member.PlayerId,
                Role = ScanMapRoles.REPAIRER,
                DisplayName = member.Name,
                CompanyName = member.CompanyName
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return await LoadMapsAsync(playerId, cancellationToken);
    }

    public async Task<IReadOnlyList<MapFriendProfileInfo>> LoadMapFriendProfilesAsync(IReadOnlyList<string> playerIds, CancellationToken cancellationToken)
    {
        List<string> normalizedPlayerIds = playerIds
            .Select(Normalize)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
        return await db.Users
            .AsNoTracking()
            .Where(user => normalizedPlayerIds.Contains(user.UnityPlayerId))
            .Select(user => new MapFriendProfileInfo(user.UnityPlayerId, user.Name, user.CompanyName))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ScanMapInfo>> PromoteMapMemberAsync(string playerId, string mapId, string memberPlayerId, CancellationToken cancellationToken)
    {
        MapEntity map = await LoadManagedMapAsync(playerId, mapId, cancellationToken);
        MapMemberEntity member = map.Members.FirstOrDefault(item => string.Equals(item.UnityPlayerId, memberPlayerId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Map member not found.");
        member.Role = string.Equals(member.Role, ScanMapRoles.READ_ONLY, StringComparison.OrdinalIgnoreCase)
            ? ScanMapRoles.REPAIRER
            : ScanMapRoles.MANAGER;
        await db.SaveChangesAsync(cancellationToken);
        return await LoadMapsAsync(playerId, cancellationToken);
    }

    public async Task<ReadOnlyMapInfo?> LoadReadOnlyMapAsync(string playerId, string inviteCode, bool joinAsMember, bool joinAsReader, CancellationToken cancellationToken)
    {
        string code = Normalize(inviteCode).ToUpperInvariant();
        MapEntity? map = await db.Maps
            .Include(item => item.Address)
            .Include(item => item.Members)
            .FirstOrDefaultAsync(item => item.InviteCode == code && item.InviteCodeExpiresAt > DateTimeOffset.UtcNow, cancellationToken);
        if (map == null)
        {
            return null;
        }

        MapMemberEntity? currentMember = map.Members.FirstOrDefault(member => string.Equals(member.UnityPlayerId, playerId, StringComparison.OrdinalIgnoreCase));
        if ((joinAsMember || joinAsReader) && currentMember == null)
        {
            AppUserEntity? user = await db.Users.AsNoTracking().FirstOrDefaultAsync(item => item.UnityPlayerId == playerId, cancellationToken);
            map.Members.Add(new MapMemberEntity
            {
                MapId = map.Id,
                UnityPlayerId = playerId,
                Role = joinAsMember ? ScanMapRoles.REPAIRER : ScanMapRoles.READ_ONLY,
                DisplayName = user?.Name ?? string.Empty,
                CompanyName = user?.CompanyName ?? string.Empty
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        else if (joinAsMember && string.Equals(currentMember?.Role, ScanMapRoles.READ_ONLY, StringComparison.OrdinalIgnoreCase))
        {
            currentMember.Role = ScanMapRoles.REPAIRER;
            await db.SaveChangesAsync(cancellationToken);
        }

        List<string> memberPlayerIds = map.Members.Select(member => member.UnityPlayerId).Distinct().ToList();
        Dictionary<string, AppUserEntity> usersByPlayerId = await db.Users
            .AsNoTracking()
            .Where(user => memberPlayerIds.Contains(user.UnityPlayerId))
            .ToDictionaryAsync(user => user.UnityPlayerId, StringComparer.OrdinalIgnoreCase, cancellationToken);
        List<MemoEntity> memos = await db.Memos
            .AsNoTracking()
            .Include(item => item.Map).ThenInclude(item => item.Address)
            .Include(item => item.Map).ThenInclude(item => item.Members)
            .Where(item => item.MapId == map.Id && item.DeletedAt == null)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        bool isPersistentJoin = joinAsMember || joinAsReader;
        ScanMapInfo mapInfo = ToScanMapInfo(map, isPersistentJoin ? playerId : string.Empty, usersByPlayerId);
        if (!joinAsMember)
        {
            mapInfo = mapInfo with { CurrentUserRole = ScanMapRoles.READ_ONLY, Members = [] };
        }
        string creatorPlayerId = map.Members.FirstOrDefault(member => string.Equals(member.Role, ScanMapRoles.MANAGER, StringComparison.OrdinalIgnoreCase))?.UnityPlayerId ?? string.Empty;
        return new ReadOnlyMapInfo(mapInfo, await ToMemoInfosAsync(memos, cancellationToken), creatorPlayerId);
    }

    public async Task<IReadOnlyList<ScanMapInfo>> RemoveMapMemberAsync(string playerId, string mapId, string memberPlayerId, CancellationToken cancellationToken)
    {
        MapEntity map = await LoadManagedMapAsync(playerId, mapId, cancellationToken);
        MapMemberEntity member = map.Members.FirstOrDefault(item => string.Equals(item.UnityPlayerId, memberPlayerId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("Map member not found.");
        if (string.Equals(member.Role, ScanMapRoles.MANAGER, StringComparison.OrdinalIgnoreCase)
            && map.Members.Count(item => string.Equals(item.Role, ScanMapRoles.MANAGER, StringComparison.OrdinalIgnoreCase)) <= 1)
        {
            throw new InvalidOperationException("The last map manager cannot be removed.");
        }

        db.MapMembers.Remove(member);
        await db.SaveChangesAsync(cancellationToken);
        return await LoadMapsAsync(playerId, cancellationToken);
    }

    public async Task<IReadOnlyList<ScanMapInfo>> DeleteMapAsync(string playerId, string mapId, CancellationToken cancellationToken)
    {
        Guid id = ParseGuid(mapId, nameof(mapId));
        MapEntity map = await db.Maps
            .Include(item => item.Members)
            .Include(item => item.Memos)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Map not found.");
        if (!map.Members.Any(member => string.Equals(member.UnityPlayerId, playerId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(member.Role, ScanMapRoles.MANAGER, StringComparison.OrdinalIgnoreCase)))
        {
            throw new UnauthorizedAccessException("Only a map manager can delete the map.");
        }

        var mediaUrls = new List<string>();
        foreach (MemoEntity memo in map.Memos)
        {
            mediaUrls.AddRange(DeserializeJson<List<string>>(memo.ImageUrlsJson));
            mediaUrls.AddRange(DeserializeVoiceItems(memo.VoiceItemsJson).Select(item => item.Url));
        }

        var remainingMediaUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var remainingMediaJson = await db.Memos
            .AsNoTracking()
            .Where(item => item.MapId != map.Id)
            .Select(item => new { item.ImageUrlsJson, item.VoiceItemsJson })
            .ToListAsync(cancellationToken);
        foreach (var mediaJson in remainingMediaJson)
        {
            remainingMediaUrls.UnionWith(DeserializeJson<List<string>>(mediaJson.ImageUrlsJson));
            remainingMediaUrls.UnionWith(DeserializeVoiceItems(mediaJson.VoiceItemsJson).Select(item => item.Url));
        }

        db.Maps.Remove(map);
        await db.SaveChangesAsync(cancellationToken);
        DeleteMemoMediaFiles(mediaUrls, remainingMediaUrls);
        return await LoadMapsAsync(playerId, cancellationToken);
    }

    public async Task<IReadOnlyList<MemoInfo>> LoadMemosAsync(string playerId, CancellationToken cancellationToken)
    {
        List<MemoEntity> memos = await QueryJoinedMemos(playerId, false)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        return await ToMemoInfosAsync(memos, cancellationToken);
    }

    public async Task<IReadOnlyList<MemoInfo>> LoadTrashedMemosAsync(string playerId, CancellationToken cancellationToken)
    {
        List<MemoEntity> memos = await QueryJoinedMemos(playerId, true)
            .Where(item => item.AuthorUnityPlayerId == playerId)
            .OrderByDescending(item => item.DeletedAt)
            .ToListAsync(cancellationToken);

        return await ToMemoInfosAsync(memos, cancellationToken);
    }

    public async Task<MemoCreateResult> AddMemoAsync(string playerId, SaveMemoRequest request, CancellationToken cancellationToken)
    {
        Guid mapId = ParseGuid(request.MapId, nameof(request.MapId));
        MapEntity? map = await db.Maps
            .Include(item => item.Members)
            .FirstOrDefaultAsync(item => item.Id == mapId, cancellationToken);

        if (map == null)
        {
            throw new InvalidOperationException("Map not found.");
        }

        if (!map.Members.Any(member => member.UnityPlayerId == playerId))
        {
            throw new UnauthorizedAccessException("Player is not a map member.");
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        string memoKind = NormalizeMemoKind(request.Kind);
        IReadOnlyList<MemoChecklistEntry> checklistItems = memoKind == "checklist"
            ? NormalizeChecklistItems(request.ChecklistItems)
            : [];
        IReadOnlyList<string> imageUrls = memoKind == "image"
            ? NormalizeImageUrls(request.ImageUrls)
            : [];
        IReadOnlyList<MemoVoiceEntry> voiceItems = memoKind == "voice"
            ? NormalizeVoiceItems(request.VoiceItems)
            : [];
        if (memoKind == "image" && imageUrls.Count == 0)
        {
            throw new ArgumentException("At least one photo or video is required.", nameof(request.ImageUrls));
        }
        if (memoKind == "voice" && voiceItems.Count == 0)
        {
            throw new ArgumentException("At least one voice recording is required.", nameof(request.VoiceItems));
        }

        var memo = new MemoEntity
        {
            Id = Guid.NewGuid(),
            MapId = map.Id,
            Kind = memoKind,
            Urgency = NormalizeMemoUrgency(request.Urgency),
            Title = GetFallbackText(request.Title, "Untitled memo"),
            Body = memoKind is "checklist" or "voice" ? string.Empty : Normalize(request.Body),
            AuthorUnityPlayerId = playerId,
            AssigneeUnityPlayerId = Normalize(request.AssigneePlayerId),
            AssigneeName = Normalize(request.AssigneeName),
            DueText = Normalize(request.DueText),
            ChecklistItemsJson = SerializeJson(checklistItems),
            VoiceItemsJson = SerializeJson(voiceItems),
            ImageUrlsJson = SerializeJson(imageUrls),
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Memos.Add(memo);
        await db.SaveChangesAsync(cancellationToken);

        MemoEntity createdMemo = await QueryMemoById(memo.Id).FirstAsync(cancellationToken);
        MemoInfo createdInfo = (await ToMemoInfosAsync([createdMemo], cancellationToken))[0];
        IReadOnlyList<MemoInfo> memos = await LoadMemosAsync(playerId, cancellationToken);
        return new MemoCreateResult(createdInfo, memos);
    }

    public async Task<MemoCreateResult> UpdateMemoAsync(string playerId, string memoId, SaveMemoRequest request, CancellationToken cancellationToken)
    {
        Guid id = ParseGuid(memoId, nameof(memoId));
        MemoEntity? memo = await db.Memos
            .Include(item => item.Map)
            .ThenInclude(item => item.Members)
            .FirstOrDefaultAsync(item => item.Id == id && item.DeletedAt == null, cancellationToken);

        if (memo == null)
        {
            throw new InvalidOperationException("Memo not found.");
        }

        bool isAuthor = string.Equals(memo.AuthorUnityPlayerId, playerId, StringComparison.OrdinalIgnoreCase);
        bool isManager = memo.Map.Members.Any(member =>
            string.Equals(member.UnityPlayerId, playerId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(member.Role, ScanMapRoles.MANAGER, StringComparison.OrdinalIgnoreCase));
        if (!isAuthor && !isManager)
        {
            throw new UnauthorizedAccessException("Only the memo author or map manager can edit this memo.");
        }

        string memoKind = NormalizeMemoKind(request.Kind);
        IReadOnlyList<MemoChecklistEntry> checklistItems = memoKind == "checklist"
            ? NormalizeChecklistItems(request.ChecklistItems)
            : [];
        IReadOnlyList<string> imageUrls = memoKind == "image"
            ? NormalizeImageUrls(request.ImageUrls)
            : [];
        IReadOnlyList<MemoVoiceEntry> voiceItems = memoKind == "voice"
            ? NormalizeVoiceItems(request.VoiceItems)
            : [];
        if (memoKind == "image" && imageUrls.Count == 0)
        {
            throw new ArgumentException("At least one photo or video is required.", nameof(request.ImageUrls));
        }
        if (memoKind == "voice" && voiceItems.Count == 0)
        {
            throw new ArgumentException("At least one voice recording is required.", nameof(request.VoiceItems));
        }
        List<string> previousImageUrls = DeserializeJson<List<string>>(memo.ImageUrlsJson);
        List<MemoVoiceEntry> previousVoiceItems = DeserializeVoiceItems(memo.VoiceItemsJson);
        List<string> currentMediaUrls = imageUrls.Concat(voiceItems.Select(item => item.Url)).ToList();
        List<string> removedMediaUrls = previousImageUrls
            .Concat(previousVoiceItems.Select(item => item.Url))
            .Except(currentMediaUrls, StringComparer.OrdinalIgnoreCase)
            .ToList();
        HashSet<string> remainingMediaUrls = removedMediaUrls.Count > 0
            ? await LoadReferencedMediaUrlsAsync(memo.Id, currentMediaUrls, cancellationToken)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        memo.Kind = memoKind;
        memo.Urgency = NormalizeMemoUrgency(request.Urgency);
        memo.Title = GetFallbackText(request.Title, "Untitled memo");
        memo.Body = memoKind is "checklist" or "voice" ? string.Empty : Normalize(request.Body);
        memo.AssigneeUnityPlayerId = Normalize(request.AssigneePlayerId);
        memo.AssigneeName = Normalize(request.AssigneeName);
        memo.DueText = Normalize(request.DueText);
        memo.ChecklistItemsJson = SerializeJson(checklistItems);
        memo.VoiceItemsJson = SerializeJson(voiceItems);
        memo.ImageUrlsJson = SerializeJson(imageUrls);
        memo.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        DeleteMemoMediaFiles(removedMediaUrls, remainingMediaUrls);

        MemoEntity updatedMemo = await QueryMemoById(memo.Id).FirstAsync(cancellationToken);
        MemoInfo updatedInfo = (await ToMemoInfosAsync([updatedMemo], cancellationToken))[0];
        IReadOnlyList<MemoInfo> memos = await LoadMemosAsync(playerId, cancellationToken);
        return new MemoCreateResult(updatedInfo, memos);
    }

    public async Task<IReadOnlyList<MemoInfo>> MoveMemoToTrashAsync(string playerId, string memoId, CancellationToken cancellationToken)
    {
        MemoEntity memo = await LoadOwnedMemoAsync(playerId, memoId, cancellationToken);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        memo.DeletedAt = now;
        memo.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return await LoadMemosAsync(playerId, cancellationToken);
    }

    public async Task<IReadOnlyList<MemoInfo>> RestoreMemoAsync(string playerId, string memoId, CancellationToken cancellationToken)
    {
        MemoEntity memo = await LoadOwnedMemoAsync(playerId, memoId, cancellationToken);
        memo.DeletedAt = null;
        memo.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return await LoadTrashedMemosAsync(playerId, cancellationToken);
    }

    public async Task<IReadOnlyList<MemoInfo>> SetMemoWorkStatusAsync(string playerId, string memoId, string status, CancellationToken cancellationToken)
    {
        MemoEntity memo = await db.Memos
            .Include(item => item.Map).ThenInclude(item => item.Members)
            .FirstOrDefaultAsync(item => item.Id == ParseGuid(memoId, nameof(memoId)) && item.DeletedAt == null, cancellationToken)
            ?? throw new InvalidOperationException("Memo not found.");
        bool isAssignee = string.Equals(memo.AssigneeUnityPlayerId, playerId, StringComparison.OrdinalIgnoreCase);
        bool isManager = memo.Map.Members.Any(member =>
            string.Equals(member.UnityPlayerId, playerId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(member.Role, ScanMapRoles.MANAGER, StringComparison.OrdinalIgnoreCase));
        string normalizedStatus = Normalize(status);
        bool isAllowed = normalizedStatus switch
        {
            "completion-requested" => isAssignee && string.Equals(memo.WorkStatus, "active", StringComparison.OrdinalIgnoreCase),
            "active" => (isAssignee || isManager) && string.Equals(memo.WorkStatus, "completion-requested", StringComparison.OrdinalIgnoreCase),
            "completed" => isManager && string.Equals(memo.WorkStatus, "completion-requested", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
        if (!isAllowed)
        {
            throw new UnauthorizedAccessException("Memo work status cannot be changed.");
        }

        memo.WorkStatus = normalizedStatus;
        memo.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return await LoadMemosAsync(playerId, cancellationToken);
    }

    public async Task<IReadOnlyList<MemoInfo>> DeleteMemoPermanentlyAsync(string playerId, string memoId, CancellationToken cancellationToken)
    {
        MemoEntity memo = await LoadOwnedMemoAsync(playerId, memoId, cancellationToken);
        List<string> mediaUrls = DeserializeJson<List<string>>(memo.ImageUrlsJson);
        mediaUrls.AddRange(DeserializeVoiceItems(memo.VoiceItemsJson).Select(item => item.Url));
        HashSet<string> remainingMediaUrls = await LoadReferencedMediaUrlsAsync(memo.Id, [], cancellationToken);

        db.Memos.Remove(memo);
        await db.SaveChangesAsync(cancellationToken);
        DeleteMemoMediaFiles(mediaUrls, remainingMediaUrls);
        return await LoadTrashedMemosAsync(playerId, cancellationToken);
    }

    private async Task<HashSet<string>> LoadReferencedMediaUrlsAsync(Guid excludedMemoId, IEnumerable<string> includedUrls, CancellationToken cancellationToken)
    {
        var remainingMediaJson = await db.Memos
            .AsNoTracking()
            .Where(item => item.Id != excludedMemoId)
            .Select(item => new { item.ImageUrlsJson, item.VoiceItemsJson })
            .ToListAsync(cancellationToken);
        var remainingMediaUrls = new HashSet<string>(includedUrls, StringComparer.OrdinalIgnoreCase);
        foreach (var mediaJson in remainingMediaJson)
        {
            remainingMediaUrls.UnionWith(DeserializeJson<List<string>>(mediaJson.ImageUrlsJson));
            remainingMediaUrls.UnionWith(DeserializeVoiceItems(mediaJson.VoiceItemsJson).Select(item => item.Url));
        }

        return remainingMediaUrls;
    }

    private void DeleteMemoMediaFiles(IEnumerable<string> mediaUrls, HashSet<string> remainingMediaUrls)
    {
        foreach (string mediaUrl in mediaUrls)
        {
            if (remainingMediaUrls.Contains(mediaUrl))
            {
                continue;
            }

            string fileName = Path.GetFileName(mediaUrl.Split('?')[0]);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                continue;
            }

            string filePath = Path.GetFullPath(Path.Combine(memoUploadDirectory, fileName));
            if (!string.Equals(Path.GetDirectoryName(filePath), memoUploadDirectory, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Failed to delete memo media file {FilePath}", filePath);
            }
        }
    }

    private async Task<MemoEntity> LoadOwnedMemoAsync(string playerId, string memoId, CancellationToken cancellationToken)
    {
        Guid id = ParseGuid(memoId, nameof(memoId));
        MemoEntity? memo = await db.Memos.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (memo == null)
        {
            throw new InvalidOperationException("Memo not found.");
        }

        if (!string.Equals(memo.AuthorUnityPlayerId, playerId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Only the memo author can delete this memo.");
        }

        return memo;
    }

    private IQueryable<MemoEntity> QueryJoinedMemos(string playerId, bool trashed)
    {
        return db.Memos
            .AsNoTracking()
            .Include(item => item.Map).ThenInclude(item => item.Address)
            .Include(item => item.Map).ThenInclude(item => item.Members)
            .Where(item => item.Map.Members.Any(member => member.UnityPlayerId == playerId)
                && (trashed ? item.DeletedAt != null : item.DeletedAt == null));
    }

    private IQueryable<MemoEntity> QueryMemoById(Guid memoId)
    {
        return db.Memos
            .AsNoTracking()
            .Include(item => item.Map).ThenInclude(item => item.Address)
            .Include(item => item.Map).ThenInclude(item => item.Members)
            .Where(item => item.Id == memoId);
    }

    private async Task<List<MemoInfo>> ToMemoInfosAsync(IReadOnlyList<MemoEntity> memos, CancellationToken cancellationToken)
    {
        string[] playerIds = memos
            .SelectMany(item => new[] { item.AuthorUnityPlayerId, item.AssigneeUnityPlayerId })
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Dictionary<string, AppUserEntity> usersByPlayerId = await db.Users
            .AsNoTracking()
            .Where(item => playerIds.Contains(item.UnityPlayerId))
            .ToDictionaryAsync(item => item.UnityPlayerId, StringComparer.OrdinalIgnoreCase, cancellationToken);

        return memos.Select(memo => ToMemoInfo(memo, usersByPlayerId)).ToList();
    }

    private async Task<AddressEntity> EnsureAddressAsync(SaveScanMapRequest request, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(request.AddressId, out Guid addressId))
        {
            AddressEntity? address = await db.Addresses.FirstOrDefaultAsync(item => item.Id == addressId, cancellationToken);
            if (address != null)
            {
                return address;
            }
        }

        return await EnsureAddressAsync(new SaveScanAddressRequest
        {
            Address = request.Address,
            RoadAddress = request.RoadAddress
        }, cancellationToken);
    }

    private async Task<AddressEntity> EnsureAddressAsync(SaveScanAddressRequest request, CancellationToken cancellationToken)
    {
        string addressText = Normalize(request.Address);
        if (string.IsNullOrWhiteSpace(addressText))
        {
            throw new ArgumentException("Address is required.", nameof(request));
        }

        string zonecode = Normalize(request.Zonecode);
        string roadAddress = Normalize(request.RoadAddress);
        string jibunAddress = Normalize(request.JibunAddress);

        AddressEntity? address = await db.Addresses.FirstOrDefaultAsync(item =>
            item.Zonecode == zonecode
            && item.Address == addressText
            && item.RoadAddress == roadAddress
            && item.JibunAddress == jibunAddress,
            cancellationToken);

        if (address != null)
        {
            return address;
        }

        address = new AddressEntity
        {
            Id = Guid.NewGuid(),
            Zonecode = zonecode,
            Address = addressText,
            RoadAddress = roadAddress,
            JibunAddress = jibunAddress,
            BuildingName = Normalize(request.BuildingName),
            Bname = Normalize(request.Bname),
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Addresses.Add(address);
        await db.SaveChangesAsync(cancellationToken);
        return address;
    }

    private static ScanAddressInfo ToScanAddressInfo(AddressEntity address)
    {
        return new ScanAddressInfo(
            address.Id.ToString("N"),
            address.Zonecode,
            address.Address,
            address.RoadAddress,
            address.JibunAddress,
            address.BuildingName,
            address.Bname,
            address.CreatedAt);
    }

    private static ScanMapInfo ToScanMapInfo(MapEntity map, string currentPlayerId, IReadOnlyDictionary<string, AppUserEntity> usersByPlayerId)
    {
        string currentRole = map.Members.FirstOrDefault(member => member.UnityPlayerId == currentPlayerId)?.Role ?? string.Empty;
        bool canViewInviteCode = string.Equals(currentRole, ScanMapRoles.MANAGER, StringComparison.OrdinalIgnoreCase)
            && map.InviteCodeExpiresAt > DateTimeOffset.UtcNow;
        return new ScanMapInfo(
            map.Id.ToString("N"),
            map.AddressId.ToString("N"),
            map.Address.Address,
            map.Address.RoadAddress,
            map.SpaceName,
            currentRole,
            string.Equals(currentRole, ScanMapRoles.READ_ONLY, StringComparison.OrdinalIgnoreCase)
                ? Array.Empty<ScanMapMemberInfo>()
                : map.Members
                .OrderBy(member => string.Equals(member.Role, ScanMapRoles.MANAGER, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(member => usersByPlayerId.TryGetValue(member.UnityPlayerId, out AppUserEntity? user) ? user.Name : member.UnityPlayerId)
                .Select(member =>
            {
                usersByPlayerId.TryGetValue(member.UnityPlayerId, out AppUserEntity? user);
                return new ScanMapMemberInfo(
                    member.UnityPlayerId,
                    member.Role,
                    GetFallbackText(user?.Name, GetFallbackText(member.DisplayName, member.UnityPlayerId)),
                    GetFallbackText(user?.CompanyName, member.CompanyName));
            }).ToList(),
            canViewInviteCode ? map.InviteCode : string.Empty,
            canViewInviteCode ? map.InviteCodeExpiresAt : null,
            map.CreatedAt,
            map.ScanCreatedAt);
    }

    private async Task<MapEntity> LoadManagedMapAsync(string playerId, string mapId, CancellationToken cancellationToken)
    {
        Guid id = ParseGuid(mapId, nameof(mapId));
        MapEntity? map = await db.Maps
            .Include(item => item.Members)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (map == null)
        {
            throw new InvalidOperationException("Map not found.");
        }
        if (!map.Members.Any(member => string.Equals(member.UnityPlayerId, playerId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(member.Role, ScanMapRoles.MANAGER, StringComparison.OrdinalIgnoreCase)))
        {
            throw new UnauthorizedAccessException("Only a map manager can manage members.");
        }
        return map;
    }

    private static string GenerateMapInviteCode()
    {
        var characters = new char[6];
        for (int i = 0; i < characters.Length; i++)
        {
            characters[i] = MAP_INVITE_CODE_CHARACTERS[RandomNumberGenerator.GetInt32(MAP_INVITE_CODE_CHARACTERS.Length)];
        }
        return new string(characters);
    }

    private static MemoInfo ToMemoInfo(MemoEntity memo, IReadOnlyDictionary<string, AppUserEntity> usersByPlayerId)
    {
        usersByPlayerId.TryGetValue(memo.AuthorUnityPlayerId, out AppUserEntity? author);
        usersByPlayerId.TryGetValue(memo.AssigneeUnityPlayerId, out AppUserEntity? assignee);

        return new MemoInfo(
            memo.Id.ToString("N"),
            memo.MapId.ToString("N"),
            memo.Map.SpaceName,
            string.IsNullOrWhiteSpace(memo.Map.Address.RoadAddress) ? memo.Map.Address.Address : memo.Map.Address.RoadAddress,
            memo.Map.SpaceName,
            memo.Kind,
            memo.Urgency,
            memo.Title,
            memo.Body,
            memo.AuthorUnityPlayerId,
            GetFallbackText(author?.Name, memo.AuthorUnityPlayerId),
            memo.AssigneeUnityPlayerId,
            GetFallbackText(assignee?.Name, memo.AssigneeName),
            memo.WorkStatus,
            memo.DueText,
            memo.CreatedAt,
            memo.UpdatedAt,
            memo.DeletedAt,
            DeserializeJson<List<MemoChecklistEntry>>(memo.ChecklistItemsJson),
            DeserializeVoiceItems(memo.VoiceItemsJson),
            DeserializeJson<List<string>>(memo.ImageUrlsJson));
    }

    private static Guid ParseGuid(string value, string paramName)
    {
        if (Guid.TryParse(value, out Guid parsed))
        {
            return parsed;
        }

        if (Guid.TryParseExact(value, "N", out parsed))
        {
            return parsed;
        }

        throw new ArgumentException($"{paramName} is invalid.", paramName);
    }

    private static string Normalize(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static string GetFallbackText(string? value, string fallback)
    {
        string normalized = Normalize(value);
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static string NormalizeMemoKind(string? value)
    {
        string normalized = Normalize(value).ToLowerInvariant();
        return normalized switch
        {
            "checklist" => "checklist",
            "voice" => "voice",
            "voicememo" => "voice",
            "image" => "image",
            "gallery" => "image",
            _ => "text"
        };
    }

    private static IReadOnlyList<string> NormalizeImageUrls(IEnumerable<string>? values)
    {
        if (values == null)
        {
            return [];
        }

        return values
            .Select(Normalize)
            .Where(value => value.StartsWith("/uploads/memos/", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
    }

    private static IReadOnlyList<MemoVoiceEntry> NormalizeVoiceItems(IEnumerable<MemoVoiceEntry>? values)
    {
        if (values == null)
        {
            return [];
        }

        return values
            .Select(item => new MemoVoiceEntry(Normalize(item.Name), Normalize(item.Url)))
            .Where(item => item.Url.StartsWith("/uploads/memos/", StringComparison.OrdinalIgnoreCase))
            .GroupBy(item => item.Url, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(3)
            .Select((item, index) => new MemoVoiceEntry(
                string.IsNullOrWhiteSpace(item.Name) ? $"음성녹음 {index + 1}" : item.Name,
                item.Url))
            .ToList();
    }

    private static string NormalizeMemoUrgency(string? value)
    {
        string normalized = Normalize(value).ToLowerInvariant();
        return normalized switch
        {
            "high" => "high",
            "1" => "high",
            "low" => "low",
            "3" => "low",
            _ => "middle"
        };
    }

    private static IReadOnlyList<MemoChecklistEntry> NormalizeChecklistItems(IEnumerable<MemoChecklistEntry>? items)
    {
        if (items == null)
        {
            return [];
        }

        List<MemoChecklistEntry> normalizedItems = [];
        foreach (MemoChecklistEntry item in items)
        {
            string text = Normalize(item.Text);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            normalizedItems.Add(new MemoChecklistEntry(text, item.Done));
            if (normalizedItems.Count >= 10)
            {
                break;
            }
        }

        return normalizedItems;
    }

    private static string SerializeJson<T>(T value)
    {
        return JsonSerializer.Serialize(value, JsonOptions);
    }

    private static T DeserializeJson<T>(string value) where T : class, new()
    {
        return JsonSerializer.Deserialize<T>(value, JsonOptions) ?? new T();
    }

    private static List<MemoVoiceEntry> DeserializeVoiceItems(string value)
    {
        using JsonDocument document = JsonDocument.Parse(value);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<MemoVoiceEntry>();
        int index = 0;
        foreach (JsonElement element in document.RootElement.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                string legacyValue = element.GetString() ?? string.Empty;
                bool isUrl = legacyValue.StartsWith("/uploads/memos/", StringComparison.OrdinalIgnoreCase);
                result.Add(new MemoVoiceEntry(isUrl ? $"음성녹음 {index + 1}" : legacyValue, isUrl ? legacyValue : string.Empty));
            }
            else if (element.ValueKind == JsonValueKind.Object)
            {
                MemoVoiceEntry? item = element.Deserialize<MemoVoiceEntry>(JsonOptions);
                if (item != null)
                {
                    result.Add(item);
                }
            }
            index++;
        }

        return result;
    }
}
