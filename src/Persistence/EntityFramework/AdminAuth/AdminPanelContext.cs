// <copyright file="AdminPanelContext.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence.EntityFramework.AdminAuth;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using MUnique.OpenMU.Persistence.AccountManager;
using MUnique.OpenMU.Persistence.AdminAuth;

/// <summary>
/// The context which holds the users of the admin panel and the data of its tools, like the item packages of the account manager.
/// </summary>
/// <remarks>
/// It uses an own schema, an own migration history and an own set of migrations.
/// That's on purpose: The admin panel needs its users before the game database exists,
/// because it's the tool which creates the game database in the first place.
/// None of the game server database roles (account, config, guild, friend) gets access
/// to this schema, so a game server process can't read or overwrite an admin password hash.
/// </remarks>
public class AdminPanelContext : DbContext
{
    /// <summary>
    /// Gets or sets the admin panel users.
    /// </summary>
    public DbSet<AdminUser> AdminUsers { get; set; } = null!;

    /// <summary>
    /// Gets or sets the API keys of the public API.
    /// </summary>
    public DbSet<ApiKey> ApiKeys { get; set; } = null!;

    /// <summary>
    /// Gets or sets the item packages of the account manager.
    /// </summary>
    public DbSet<ItemPackage> ItemPackages { get; set; } = null!;

    /// <summary>
    /// Gets or sets the item option presets of the account manager.
    /// </summary>
    public DbSet<ItemOptionPreset> ItemOptionPresets { get; set; } = null!;

    /// <summary>
    /// Gets or sets the plays of the mini games of the player website.
    /// </summary>
    public DbSet<WebMiniGamePlay> WebMiniGamePlays { get; set; } = null!;

    /// <summary>
    /// Gets or sets the daily check-ins of the player website.
    /// </summary>
    public DbSet<WebCheckIn> WebCheckIns { get; set; } = null!;

    /// <summary>
    /// Gets or sets the news posts of the player website.
    /// </summary>
    public DbSet<WebNewsPost> WebNewsPosts { get; set; } = null!;

    /// <summary>
    /// Gets or sets the listings of the player market.
    /// </summary>
    public DbSet<WebMarketListing> WebMarketListings { get; set; } = null!;

    /// <summary>
    /// Gets or sets the point mode Castle Siege battles.
    /// </summary>
    public DbSet<CastleSiegeBattle> CastleSiegeBattles { get; set; } = null!;

    /// <summary>
    /// Gets or sets the Castle Siege rewards for the vaults of guild masters.
    /// </summary>
    public DbSet<CastleSiegeVaultReward> CastleSiegeVaultRewards { get; set; } = null!;

    /// <summary>
    /// Gets or sets the flash sales of the web shop.
    /// </summary>
    public DbSet<WebFlashSale> WebFlashSales { get; set; } = null!;

    /// <summary>Gets or sets the grand reset milestone rewards.</summary>
    public DbSet<WebMilestoneClaim> WebMilestoneClaims { get; set; } = null!;

    /// <summary>Gets or sets the administrative audit trail.</summary>
    public DbSet<AdminAuditEntry> AdminAuditEntries { get; set; } = null!;

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        this.Configure(optionsBuilder);

        // The migration history of this context lives in its own schema, so it doesn't
        // interfere with the migrations of the game database.
        optionsBuilder.UseNpgsql(
            ConnectionConfigurator.GetConnectionString<AdminPanelContext>(),
            options => options.MigrationsHistoryTable(HistoryRepository.DefaultTableName, SchemaNames.AdminPanel));
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(SchemaNames.AdminPanel);
        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.ToTable(nameof(AdminUser), SchemaNames.AdminPanel);
            entity.HasKey(u => u.Id);
            entity.Property(u => u.LoginName).IsRequired().HasMaxLength(100);
            entity.Property(u => u.NormalizedLoginName).IsRequired().HasMaxLength(100);
            entity.HasIndex(u => u.NormalizedLoginName).IsUnique();
            entity.Property(u => u.PasswordHash).IsRequired();
            entity.Property(u => u.SecurityStamp).IsRequired();
            entity.Property(u => u.Roles).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<ApiKey>(entity =>
        {
            entity.ToTable(nameof(ApiKey), SchemaNames.AdminPanel);
            entity.HasKey(k => k.Id);
            entity.Property(k => k.Name).IsRequired().HasMaxLength(100);
            entity.Property(k => k.KeyHash).IsRequired().HasMaxLength(100);
            entity.HasIndex(k => k.KeyHash).IsUnique();
            entity.Property(k => k.KeyPrefix).IsRequired().HasMaxLength(16);
            entity.Property(k => k.Roles).IsRequired().HasMaxLength(200);
            entity.Property(k => k.AllowedToolGroups).IsRequired().HasMaxLength(1000).HasDefaultValue(string.Empty);
        });

        modelBuilder.Entity<AdminAuditEntry>(entity =>
        {
            entity.ToTable(nameof(AdminAuditEntry), SchemaNames.AdminPanel);
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PrincipalId).IsRequired().HasMaxLength(200);
            entity.Property(e => e.PrincipalName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Frontend).IsRequired().HasMaxLength(16);
            entity.Property(e => e.Operation).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ArgumentsJson).IsRequired().HasMaxLength(16384);
            entity.Property(e => e.ResultJson).IsRequired().HasMaxLength(16384);
            entity.Property(e => e.Outcome).IsRequired().HasMaxLength(32);
            entity.HasIndex(e => new { e.StartedAt, e.Id });
        });

        this.OnAccountManagerModelCreating(modelBuilder);
    }

    private void OnAccountManagerModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ItemPackage>(entity =>
        {
            entity.ToTable(nameof(ItemPackage), SchemaNames.AdminPanel);
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(ItemPackage.NameMaximumLength);
            entity.HasIndex(p => p.Name).IsUnique();
            entity.Property(p => p.Description).IsRequired().HasMaxLength(ItemPackage.DescriptionMaximumLength);
            entity.HasMany(p => p.Items).WithOne().HasForeignKey(i => i.ItemPackageId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ItemPackageItem>(entity =>
        {
            entity.ToTable(nameof(ItemPackageItem), SchemaNames.AdminPanel);
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Name).IsRequired().HasMaxLength(ItemPackageItem.NameMaximumLength);
            entity.HasMany(i => i.Options).WithOne().HasForeignKey(o => o.ItemPackageItemId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ItemPackageItemOption>(entity =>
        {
            entity.ToTable(nameof(ItemPackageItemOption), SchemaNames.AdminPanel);
            entity.HasKey(o => o.Id);
        });

        modelBuilder.Entity<WebCheckIn>(entity =>
        {
            entity.ToTable(nameof(WebCheckIn), SchemaNames.AdminPanel);
            entity.HasKey(c => c.Id);
            entity.Property(c => c.LoginName).IsRequired().HasMaxLength(10);
            entity.Property(c => c.CoinType).IsRequired().HasMaxLength(20);
            entity.HasIndex(c => new { c.LoginName, c.Day }).IsUnique();
        });

        modelBuilder.Entity<WebNewsPost>(entity =>
        {
            entity.ToTable(nameof(WebNewsPost), SchemaNames.AdminPanel);
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Slug).IsRequired().HasMaxLength(80);
            entity.HasIndex(p => p.Slug).IsUnique();
            entity.Property(p => p.Title).IsRequired().HasMaxLength(WebNewsPost.TitleMaximumLength);
            entity.Property(p => p.Tag).IsRequired().HasMaxLength(20);
            entity.Property(p => p.Summary).IsRequired().HasMaxLength(WebNewsPost.SummaryMaximumLength);
            entity.Property(p => p.Body).IsRequired().HasMaxLength(WebNewsPost.BodyMaximumLength);
            entity.Property(p => p.Author).IsRequired().HasMaxLength(10);
            entity.HasIndex(p => p.PublishedAt);

            // The posts the website started with.
            entity.HasData(
                new WebNewsPost
                {
                    Id = new Guid("6c1b9e7a-0001-4d2e-9f3a-5b8c7d6e4f01"),
                    Slug = "reset-rules",
                    Title = "Reset rules",
                    Tag = "Notice",
                    Summary = "Reset at level 400 for 1,000,000 zen. Stats go up to 32,767, so every build has room to grow.",
                    Body = "Reach **level 400** and pay **1,000,000 zen** to reset. You go back to level 10 and keep your stats, and every reset gives more points per level.\n\nStats go up to **32,767**, so there's room for every build. Use `/reset` in the game, or the character tools on the website.",
                    PublishedAt = new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc),
                    Author = "team",
                },
                new WebNewsPost
                {
                    Id = new Guid("6c1b9e7a-0002-4d2e-9f3a-5b8c7d6e4f02"),
                    Slug = "wind-combo",
                    Title = "Blade Knights: the wind combo",
                    Tag = "Update",
                    Summary = "Skill hits can now chain into up to four combo strikes, each weaker than the last. The fourth is rare, and lucky.",
                    Body = "Blade Knight skill hits can now chain into extra strikes: a second hit, sometimes a third, and with luck a fourth. Each strike is weaker than the one before.\n\nThe chances and damage are listed on the server info page.",
                    PublishedAt = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc),
                    Author = "team",
                },
                new WebNewsPost
                {
                    Id = new Guid("6c1b9e7a-0003-4d2e-9f3a-5b8c7d6e4f03"),
                    Slug = "website-open",
                    Title = "The website is open",
                    Tag = "Notice",
                    Summary = "Create your account here, then log in through the game client or play right in your browser.",
                    Body = "Create your account here, then log in through the game client or play right in your browser.\n\nOn the website you can also see your characters, move items between them, check the rankings and shop for coins.",
                    PublishedAt = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc),
                    Author = "team",
                });
        });

        modelBuilder.Entity<WebMarketListing>(entity =>
        {
            entity.ToTable(nameof(WebMarketListing), SchemaNames.AdminPanel);
            entity.HasKey(l => l.Id);
            entity.Property(l => l.SellerLogin).IsRequired().HasMaxLength(10);
            entity.Property(l => l.BuyerLogin).HasMaxLength(10);
            entity.Property(l => l.EscrowLogin).IsRequired().HasMaxLength(10);
            entity.Property(l => l.ItemName).IsRequired().HasMaxLength(100);
            entity.Property(l => l.CoinType).IsRequired().HasMaxLength(20);
            entity.Property(l => l.State).IsRequired().HasMaxLength(20);
            entity.HasIndex(l => new { l.State, l.CreatedAt });
            entity.HasIndex(l => l.SellerLogin);
        });

        modelBuilder.Entity<WebFlashSale>(entity =>
        {
            entity.ToTable(nameof(WebFlashSale), SchemaNames.AdminPanel);
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Title).IsRequired().HasMaxLength(WebFlashSale.TitleMaximumLength);
            entity.Property(s => s.CreatedBy).IsRequired().HasMaxLength(10);
            entity.HasIndex(s => s.StartsAt);
        });

        modelBuilder.Entity<WebMilestoneClaim>(entity =>
        {
            entity.ToTable(nameof(WebMilestoneClaim), SchemaNames.AdminPanel);
            entity.HasKey(c => c.Id);
            entity.Property(c => c.LoginName).IsRequired().HasMaxLength(10);
            entity.Property(c => c.CharacterName).IsRequired().HasMaxLength(10);
            entity.Property(c => c.ClaimedItem).HasMaxLength(WebMilestoneClaim.ClaimedItemMaximumLength);
            entity.HasIndex(c => c.LoginName);
            entity.HasIndex(c => new { c.CharacterName, c.GrandResets }).IsUnique();
        });

        modelBuilder.Entity<CastleSiegeBattle>(entity =>
        {
            entity.ToTable(nameof(CastleSiegeBattle), SchemaNames.AdminPanel);
            entity.HasKey(b => b.Id);
            entity.Property(b => b.HolderGuildName).HasMaxLength(20);
            entity.Property(b => b.WinnerGuildName).HasMaxLength(20);
            entity.Property(b => b.ScoresJson).IsRequired();
            entity.Property(b => b.RewardsJson).IsRequired();
            entity.HasIndex(b => b.StartedAt);
        });

        modelBuilder.Entity<CastleSiegeVaultReward>(entity =>
        {
            entity.ToTable(nameof(CastleSiegeVaultReward), SchemaNames.AdminPanel);
            entity.HasKey(r => r.Id);
            entity.Property(r => r.LoginName).IsRequired().HasMaxLength(10);
            entity.Property(r => r.GuildName).IsRequired().HasMaxLength(20);
            entity.Property(r => r.Placement).IsRequired().HasMaxLength(20);
            entity.Property(r => r.LastProblem).HasMaxLength(200);
            entity.HasIndex(r => new { r.DeliveredAt, r.CreatedAt });
            entity.HasIndex(r => r.BattleId);
        });

        modelBuilder.Entity<WebMiniGamePlay>(entity =>
        {
            entity.ToTable(nameof(WebMiniGamePlay), SchemaNames.AdminPanel);
            entity.HasKey(p => p.Id);
            entity.Property(p => p.LoginName).IsRequired().HasMaxLength(10);
            entity.Property(p => p.Game).IsRequired().HasMaxLength(20);
            entity.Property(p => p.Currency).IsRequired().HasMaxLength(20);
            entity.Property(p => p.Result).IsRequired().HasMaxLength(WebMiniGamePlay.ResultMaximumLength);
            entity.HasIndex(p => new { p.LoginName, p.PlayedAt });
            entity.HasIndex(p => p.PlayedAt);
        });

        modelBuilder.Entity<ItemOptionPreset>(entity =>
        {
            entity.ToTable(nameof(ItemOptionPreset), SchemaNames.AdminPanel);
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(ItemOptionPreset.NameMaximumLength);

            // The presets which a new installation starts with.
            var createdAt = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);
            entity.HasData(
                new ItemOptionPreset { Id = new Guid("7c6f4f5e-1d0b-4a51-9b1e-000000000001"), Name = "Luck, skill, max option", Skill = true, Luck = true, OptionLevel = ItemOptionPreset.MaximumOptionLevel, CreatedAt = createdAt },
                new ItemOptionPreset { Id = new Guid("7c6f4f5e-1d0b-4a51-9b1e-000000000002"), Name = "Full option", Skill = true, Luck = true, OptionLevel = ItemOptionPreset.MaximumOptionLevel, Excellent = true, Guardian = true, CreatedAt = createdAt.AddSeconds(1) },
                new ItemOptionPreset { Id = new Guid("7c6f4f5e-1d0b-4a51-9b1e-000000000003"), Name = "Freebie +13", Level = 13, Skill = true, Luck = true, OptionLevel = 3, CreatedAt = createdAt.AddSeconds(2) });
        });
    }
}
