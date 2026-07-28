using Microsoft.EntityFrameworkCore;

namespace ASP.NET_core_MemoAnchor_Server.Data;

public sealed class MemoAnchorDbContext : DbContext
{
    public MemoAnchorDbContext(DbContextOptions<MemoAnchorDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppUserEntity> Users => Set<AppUserEntity>();
    public DbSet<AddressEntity> Addresses => Set<AddressEntity>();
    public DbSet<UserAddressEntity> UserAddresses => Set<UserAddressEntity>();
    public DbSet<MapEntity> Maps => Set<MapEntity>();
    public DbSet<MapMemberEntity> MapMembers => Set<MapMemberEntity>();
    public DbSet<MemoEntity> Memos => Set<MemoEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUserEntity>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.UnityPlayerId).IsUnique();
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.UnityPlayerId).HasColumnName("unity_player_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
            entity.Property(item => item.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
            entity.Property(item => item.CompanyName).HasColumnName("company_name").HasMaxLength(180).IsRequired();
            entity.Property(item => item.CreatedAt).HasColumnName("created_at");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<AddressEntity>(entity =>
        {
            entity.ToTable("addresses");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.Zonecode, item.Address, item.RoadAddress, item.JibunAddress }).IsUnique();
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.Zonecode).HasColumnName("zonecode").HasMaxLength(32).IsRequired();
            entity.Property(item => item.Address).HasColumnName("address").HasMaxLength(500).IsRequired();
            entity.Property(item => item.RoadAddress).HasColumnName("road_address").HasMaxLength(500).IsRequired();
            entity.Property(item => item.JibunAddress).HasColumnName("jibun_address").HasMaxLength(500).IsRequired();
            entity.Property(item => item.BuildingName).HasColumnName("building_name").HasMaxLength(240).IsRequired();
            entity.Property(item => item.Bname).HasColumnName("bname").HasMaxLength(120).IsRequired();
            entity.Property(item => item.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<UserAddressEntity>(entity =>
        {
            entity.ToTable("user_addresses");
            entity.HasKey(item => new { item.UnityPlayerId, item.AddressId });
            entity.HasIndex(item => item.AddressId);
            entity.Property(item => item.UnityPlayerId).HasColumnName("unity_player_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.AddressId).HasColumnName("address_id");
            entity.Property(item => item.CreatedAt).HasColumnName("created_at");
            entity.HasOne(item => item.Address)
                .WithMany(item => item.UserAddresses)
                .HasForeignKey(item => item.AddressId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MapEntity>(entity =>
        {
            entity.ToTable("maps");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => item.AddressId);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.AddressId).HasColumnName("address_id");
            entity.Property(item => item.SpaceName).HasColumnName("space_name").HasMaxLength(160).IsRequired();
            entity.Property(item => item.InviteCode).HasColumnName("invite_code").HasMaxLength(6);
            entity.Property(item => item.InviteCodeExpiresAt).HasColumnName("invite_code_expires_at");
            entity.Property(item => item.CreatedAt).HasColumnName("created_at");
            entity.Property(item => item.ScanCreatedAt).HasColumnName("scan_created_at");
            entity.Property(item => item.ReconstructionScanId).HasColumnName("reconstruction_scan_id").HasMaxLength(256).IsRequired();
            entity.Property(item => item.ReconstructionState).HasColumnName("reconstruction_state").HasMaxLength(32).IsRequired();
            entity.Property(item => item.ReconstructionMessage).HasColumnName("reconstruction_message").HasMaxLength(1000).IsRequired();
            entity.Property(item => item.ReconstructionResultFile).HasColumnName("reconstruction_result_file").HasMaxLength(500).IsRequired();
            entity.Property(item => item.ReconstructionUpdatedAt).HasColumnName("reconstruction_updated_at");
            entity.HasOne(item => item.Address)
                .WithMany(item => item.Maps)
                .HasForeignKey(item => item.AddressId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MapMemberEntity>(entity =>
        {
            entity.ToTable("map_members");
            entity.HasKey(item => new { item.MapId, item.UnityPlayerId });
            entity.HasIndex(item => item.UnityPlayerId);
            entity.Property(item => item.MapId).HasColumnName("map_id");
            entity.Property(item => item.UnityPlayerId).HasColumnName("unity_player_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.Role).HasColumnName("role").HasMaxLength(32).IsRequired();
            entity.Property(item => item.DisplayName).HasColumnName("display_name").HasMaxLength(120).IsRequired();
            entity.Property(item => item.CompanyName).HasColumnName("company_name").HasMaxLength(180).IsRequired();
            entity.HasOne(item => item.Map)
                .WithMany(item => item.Members)
                .HasForeignKey(item => item.MapId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MemoEntity>(entity =>
        {
            entity.ToTable("memos");
            entity.HasKey(item => item.Id);
            entity.HasIndex(item => new { item.MapId, item.CreatedAt });
            entity.HasIndex(item => item.AuthorUnityPlayerId);
            entity.HasIndex(item => item.AssigneeUnityPlayerId);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.MapId).HasColumnName("map_id");
            entity.Property(item => item.Kind).HasColumnName("kind").HasMaxLength(32).IsRequired();
            entity.Property(item => item.Urgency).HasColumnName("urgency").HasMaxLength(32).IsRequired();
            entity.Property(item => item.Title).HasColumnName("title").HasMaxLength(240).IsRequired();
            entity.Property(item => item.Body).HasColumnName("body").HasMaxLength(4000).IsRequired();
            entity.Property(item => item.AuthorUnityPlayerId).HasColumnName("author_unity_player_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.AssigneeUnityPlayerId).HasColumnName("assignee_unity_player_id").HasMaxLength(128).IsRequired();
            entity.Property(item => item.AssigneeName).HasColumnName("assignee_name").HasMaxLength(120).IsRequired();
            entity.Property(item => item.WorkStatus).HasColumnName("work_status").HasMaxLength(32).IsRequired();
            entity.Property(item => item.DueText).HasColumnName("due_text").HasMaxLength(80).IsRequired();
            entity.Property(item => item.ChecklistItemsJson).HasColumnName("checklist_items").HasColumnType("jsonb").IsRequired();
            entity.Property(item => item.VoiceItemsJson).HasColumnName("voice_items").HasColumnType("jsonb").IsRequired();
            entity.Property(item => item.ImageUrlsJson).HasColumnName("image_urls").HasColumnType("jsonb").IsRequired();
            entity.Property(item => item.CreatedAt).HasColumnName("created_at");
            entity.Property(item => item.UpdatedAt).HasColumnName("updated_at");
            entity.Property(item => item.DeletedAt).HasColumnName("deleted_at");
            entity.HasOne(item => item.Map)
                .WithMany(item => item.Memos)
                .HasForeignKey(item => item.MapId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

public sealed class AppUserEntity
{
    public Guid Id { get; set; }
    public string UnityPlayerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class AddressEntity
{
    public Guid Id { get; set; }
    public string Zonecode { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string RoadAddress { get; set; } = string.Empty;
    public string JibunAddress { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public string Bname { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public List<UserAddressEntity> UserAddresses { get; set; } = [];
    public List<MapEntity> Maps { get; set; } = [];
}

public sealed class UserAddressEntity
{
    public string UnityPlayerId { get; set; } = string.Empty;
    public Guid AddressId { get; set; }
    public AddressEntity Address { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class MapEntity
{
    public Guid Id { get; set; }
    public Guid AddressId { get; set; }
    public AddressEntity Address { get; set; } = null!;
    public string SpaceName { get; set; } = string.Empty;
    public string InviteCode { get; set; } = string.Empty;
    public DateTimeOffset? InviteCodeExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ScanCreatedAt { get; set; }
    public string ReconstructionScanId { get; set; } = string.Empty;
    public string ReconstructionState { get; set; } = string.Empty;
    public string ReconstructionMessage { get; set; } = string.Empty;
    public string ReconstructionResultFile { get; set; } = string.Empty;
    public DateTimeOffset? ReconstructionUpdatedAt { get; set; }
    public List<MapMemberEntity> Members { get; set; } = [];
    public List<MemoEntity> Memos { get; set; } = [];
}

public sealed class MapMemberEntity
{
    public Guid MapId { get; set; }
    public MapEntity Map { get; set; } = null!;
    public string UnityPlayerId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
}

public sealed class MemoEntity
{
    public Guid Id { get; set; }
    public Guid MapId { get; set; }
    public MapEntity Map { get; set; } = null!;
    public string Kind { get; set; } = string.Empty;
    public string Urgency { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string AuthorUnityPlayerId { get; set; } = string.Empty;
    public string AssigneeUnityPlayerId { get; set; } = string.Empty;
    public string AssigneeName { get; set; } = string.Empty;
    public string WorkStatus { get; set; } = "active";
    public string DueText { get; set; } = string.Empty;
    public string ChecklistItemsJson { get; set; } = "[]";
    public string VoiceItemsJson { get; set; } = "[]";
    public string ImageUrlsJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
