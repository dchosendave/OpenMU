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
