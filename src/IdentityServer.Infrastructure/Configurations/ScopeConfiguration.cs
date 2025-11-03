using IdentityServer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Infrastructure.Configurations;

/// <summary>
/// Entity Framework Core configuration for Scope entity
/// </summary>
public class ScopeConfiguration : IEntityTypeConfiguration<Scope>
{
    public void Configure(EntityTypeBuilder<Scope> builder)
    {
        // Table name
        builder.ToTable("Scopes");

        // Primary Key
        builder.HasKey(s => s.Id);

        // Scope properties - Core OAuth/OpenID Connect fields
        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnType("nvarchar(200)")
            .HasComment("Unique scope name (e.g., 'openid', 'profile', 'api:read')");

        builder.Property(s => s.DisplayName)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnType("nvarchar(200)")
            .HasComment("User-friendly display name for the scope");

        builder.Property(s => s.Description)
            .HasMaxLength(1000)
            .HasColumnType("nvarchar(1000)")
            .IsRequired(false)
            .HasComment("Detailed description of what this scope provides access to");

        builder.Property(s => s.IsEnabled)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnType("bit")
            .HasComment("Whether this scope is currently enabled and can be requested");

        builder.Property(s => s.ShowInDiscoveryDocument)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnType("bit")
            .HasComment("Whether this scope should appear in the discovery document");

        // Audit fields from BaseEntity
        builder.Property(s => s.CreatedAt)
            .IsRequired()
            .HasColumnType("datetime2")
            .HasComment("Timestamp when the scope was created");

        builder.Property(s => s.UpdatedAt)
            .IsRequired(false)
            .HasColumnType("datetime2")
            .HasComment("Timestamp when the scope was last updated");

        builder.Property(s => s.CreatedBy)
            .HasMaxLength(200)
            .HasColumnType("nvarchar(200)")
            .IsRequired(false)
            .HasComment("User who created this record");

        builder.Property(s => s.UpdatedBy)
            .HasMaxLength(200)
            .HasColumnType("nvarchar(200)")
            .IsRequired(false)
            .HasComment("User who last updated this record");

        // Soft delete
        builder.Property(s => s.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnType("bit")
            .HasComment("Soft delete flag");

        // Query filter for soft deletes - automatically exclude deleted records
        builder.HasQueryFilter(s => !s.IsDeleted);

        // Indexes for performance optimization
        
        // Unique index on Name for active scopes only
        builder.HasIndex(s => s.Name)
            .IsUnique()
            .HasDatabaseName("IX_Scopes_Name")
            .HasFilter("[IsDeleted] = 0");

        // Index on IsEnabled for filtering enabled scopes
        builder.HasIndex(s => s.IsEnabled)
            .HasDatabaseName("IX_Scopes_IsEnabled")
            .HasFilter("[IsDeleted] = 0 AND [IsEnabled] = 1");

        // Index on ShowInDiscoveryDocument for discovery endpoint queries
        builder.HasIndex(s => s.ShowInDiscoveryDocument)
            .HasDatabaseName("IX_Scopes_ShowInDiscoveryDocument")
            .HasFilter("[IsDeleted] = 0 AND [ShowInDiscoveryDocument] = 1");

        // Composite index for common query patterns (enabled + show in discovery)
        builder.HasIndex(s => new { s.IsEnabled, s.ShowInDiscoveryDocument })
            .HasDatabaseName("IX_Scopes_IsEnabled_ShowInDiscovery")
            .HasFilter("[IsDeleted] = 0 AND [IsEnabled] = 1 AND [ShowInDiscoveryDocument] = 1");

        // Index on IsDeleted for soft delete queries
        builder.HasIndex(s => s.IsDeleted)
            .HasDatabaseName("IX_Scopes_IsDeleted")
            .HasFilter("[IsDeleted] = 0");

        // Index on CreatedAt for temporal queries
        builder.HasIndex(s => s.CreatedAt)
            .HasDatabaseName("IX_Scopes_CreatedAt");

        // Composite index for active enabled scopes
        builder.HasIndex(s => new { s.IsDeleted, s.IsEnabled })
            .HasDatabaseName("IX_Scopes_IsDeleted_IsEnabled")
            .HasFilter("[IsDeleted] = 0 AND [IsEnabled] = 1");
    }
}