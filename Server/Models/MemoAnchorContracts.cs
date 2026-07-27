public interface IProfileStore
{
    Task<UserAccountInfo?> LoadUserAccountInfoAsync(string playerId, CancellationToken cancellationToken);
    Task SaveUserAccountInfoAsync(string playerId, UserAccountInfo accountInfo, CancellationToken cancellationToken);
}

public interface IMapMemoStore
{
    Task<IReadOnlyList<ScanAddressInfo>> LoadAddressesAsync(string playerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ScanAddressInfo>> AddAddressAsync(string playerId, SaveScanAddressRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ScanMapInfo>> LoadMapsAsync(string playerId, CancellationToken cancellationToken);
    Task<ScanMapCreateInfo> AddMapAsync(string playerId, SaveScanMapRequest request, CancellationToken cancellationToken);
    Task<bool> CanAccessMapAsync(string playerId, string mapId, CancellationToken cancellationToken);
    Task<bool> CanManageMapAsync(string playerId, string mapId, CancellationToken cancellationToken);
    Task<MapReconstructionInfo?> LoadMapReconstructionAsync(string playerId, string mapId, CancellationToken cancellationToken);
    Task BeginMapReconstructionAsync(string mapId, string scanId, CancellationToken cancellationToken);
    Task UpdateMapReconstructionAsync(string mapId, string scanId, string state, string message, string resultFile, DateTimeOffset updatedAt, CancellationToken cancellationToken);
    Task<IReadOnlyList<ScanMapInfo>> AddMapMembersAsync(string playerId, string mapId, IReadOnlyList<InviteMapMemberInfo> members, CancellationToken cancellationToken);
    Task<IReadOnlyList<MapFriendProfileInfo>> LoadMapFriendProfilesAsync(IReadOnlyList<string> playerIds, CancellationToken cancellationToken);
    Task<MapInviteInfo> IssueMapInviteAsync(string playerId, string mapId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ScanMapInfo>> PromoteMapMemberAsync(string playerId, string mapId, string memberPlayerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ScanMapInfo>> RemoveMapMemberAsync(string playerId, string mapId, string memberPlayerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ScanMapInfo>> DeleteMapAsync(string playerId, string mapId, CancellationToken cancellationToken);
    Task<ReadOnlyMapInfo?> LoadReadOnlyMapAsync(string playerId, string inviteCode, bool joinAsMember, bool joinAsReader, CancellationToken cancellationToken);
    Task<IReadOnlyList<MemoInfo>> LoadMemosAsync(string playerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MemoInfo>> LoadTrashedMemosAsync(string playerId, CancellationToken cancellationToken);
    Task<MemoCreateResult> AddMemoAsync(string playerId, SaveMemoRequest request, CancellationToken cancellationToken);
    Task<MemoCreateResult> UpdateMemoAsync(string playerId, string memoId, SaveMemoRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<MemoInfo>> MoveMemoToTrashAsync(string playerId, string memoId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MemoInfo>> RestoreMemoAsync(string playerId, string memoId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MemoInfo>> DeleteMemoPermanentlyAsync(string playerId, string memoId, CancellationToken cancellationToken);
    Task<IReadOnlyList<MemoInfo>> SetMemoWorkStatusAsync(string playerId, string memoId, string status, CancellationToken cancellationToken);
}

public sealed record UserAccountInfo(
    string Name,
    string Email,
    string CompanyName,
    DateTimeOffset UpdatedAt);

public sealed record ScanAddressInfo(
    string Id,
    string Zonecode,
    string Address,
    string RoadAddress,
    string JibunAddress,
    string BuildingName,
    string Bname,
    DateTimeOffset CreatedAt);

public sealed record ScanMapInfo(
    string Id,
    string AddressId,
    string Address,
    string RoadAddress,
    string SpaceName,
    string CurrentUserRole,
    IReadOnlyList<ScanMapMemberInfo> Members,
    string InviteCode,
    DateTimeOffset? InviteCodeExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ScanCreatedAt,
    string ReconstructionScanId,
    string ReconstructionState,
    string ReconstructionMessage,
    string ReconstructionResultFile,
    DateTimeOffset? ReconstructionUpdatedAt);

public sealed record MapReconstructionInfo(
    string ScanId,
    string State,
    string Message,
    string ResultFile,
    DateTimeOffset? UpdatedAt);

public sealed record ScanMapCreateInfo(
    string CreatedMapId,
    IReadOnlyList<ScanMapInfo> Maps);

public sealed record ScanMapMemberInfo(
    string PlayerId,
    string Role,
    string Name,
    string CompanyName);

public sealed record MapInviteInfo(
    string Code,
    DateTimeOffset ExpiresAt);

public sealed record MapFriendProfileInfo(
    string PlayerId,
    string Name,
    string CompanyName);

public sealed record ReadOnlyMapInfo(
    ScanMapInfo Map,
    IReadOnlyList<MemoInfo> Memos,
    string CreatorPlayerId);

public sealed record MemoChecklistEntry(
    string Text,
    bool Done);

public sealed record MemoVoiceEntry(
    string Name,
    string Url);

public sealed record MemoInfo(
    string Id,
    string MapId,
    string MapName,
    string Address,
    string LocationName,
    string Kind,
    string Urgency,
    string Title,
    string Body,
    string AuthorPlayerId,
    string AuthorName,
    string AssigneePlayerId,
    string AssigneeName,
    string WorkStatus,
    string DueText,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? DeletedAt,
    IReadOnlyList<MemoChecklistEntry> ChecklistItems,
    IReadOnlyList<MemoVoiceEntry> VoiceItems,
    IReadOnlyList<string> ImageUrls);

public sealed record MemoCreateResult(
    MemoInfo Memo,
    IReadOnlyList<MemoInfo> Memos);

public static class ScanMapRoles
{
    public const string MANAGER = "manager";
    public const string REPAIRER = "repairer";
    public const string READ_ONLY = "read-only";
}

public sealed class SaveScanAddressRequest
{
    public string Zonecode { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string RoadAddress { get; set; } = string.Empty;
    public string JibunAddress { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public string Bname { get; set; } = string.Empty;
}

public sealed class SaveScanMapRequest
{
    public string AddressId { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string RoadAddress { get; set; } = string.Empty;
    public string SpaceName { get; set; } = string.Empty;
    public string RepairerPlayerId { get; set; } = string.Empty;
    public string ManagerPlayerId { get; set; } = string.Empty;
}

public sealed class InviteMapMembersRequest
{
    public List<InviteMapMemberInfo> Members { get; set; } = [];
}

public sealed record InviteMapMemberInfo(
    string PlayerId,
    string Name,
    string CompanyName);

public sealed class MapFriendProfilesRequest
{
    public List<string> PlayerIds { get; set; } = [];
}

public sealed class ReadOnlyMapRequest
{
    public string Code { get; set; } = string.Empty;
    public bool JoinAsMember { get; set; }
    public bool JoinAsReader { get; set; }
}

public sealed class SaveMemoRequest
{
    public string MapId { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string Urgency { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string AssigneePlayerId { get; set; } = string.Empty;
    public string AssigneeName { get; set; } = string.Empty;
    public string DueText { get; set; } = string.Empty;
    public List<MemoChecklistEntry> ChecklistItems { get; set; } = [];
    public List<MemoVoiceEntry> VoiceItems { get; set; } = [];
    public List<string> ImageUrls { get; set; } = [];
}
