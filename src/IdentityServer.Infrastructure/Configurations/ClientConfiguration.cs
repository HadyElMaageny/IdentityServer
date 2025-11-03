using IdentityServer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Infrastructure.Configurations;

/// <summary>
/// Entity Framework Core configuration for Client entity
/// </summary>
public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        // Table name
        builder.ToTable("Clients");

        // Primary Key
        builder.HasKey(c => c.Id);

        // Client Identity - Core OAuth/OIDC fields
        builder.Property(c => c.ClientId)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnType("nvarchar(200)")
            .HasComment("Unique client identifier used in OAuth/OIDC flows");

        builder.Property(c => c.ClientSecret)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnType("nvarchar(500)")
            .HasComment("Hashed client secret (use SHA256 or bcrypt)");

        builder.Property(c => c.ClientName)
            .IsRequired()
            .HasMaxLength(200)
            .HasColumnType("nvarchar(200)")
            .HasComment("Human-readable client name for consent screens");

        builder.Property(c => c.Description)
            .HasMaxLength(1000)
            .HasColumnType("nvarchar(1000)")
            .IsRequired(false)
            .HasComment("Detailed description of the client application");

        // Client URIs
        builder.Property(c => c.ClientUri)
            .HasMaxLength(2000)
            .HasColumnType("nvarchar(2000)")
            .IsRequired(false)
            .HasComment("URI to the client's homepage");

        builder.Property(c => c.LogoUri)
            .HasMaxLength(2000)
            .HasColumnType("nvarchar(2000)")
            .IsRequired(false)
            .HasComment("URI to the client's logo for consent screens");

        builder.Property(c => c.RedirectUris)
            .IsRequired()
            .HasMaxLength(4000)
            .HasColumnType("nvarchar(4000)")
            .HasComment("Allowed redirect URIs after authentication (JSON array or comma-separated)");

        builder.Property(c => c.PostLogoutRedirectUris)
            .HasMaxLength(4000)
            .HasColumnType("nvarchar(4000)")
            .IsRequired(false)
            .HasComment("Allowed redirect URIs after logout (JSON array or comma-separated)");

        // Client Configuration
        builder.Property(c => c.AllowedGrantTypes)
            .IsRequired()
            .HasMaxLength(1000)
            .HasColumnType("nvarchar(1000)")
            .HasComment("Allowed OAuth grant types (e.g., authorization_code, client_credentials, refresh_token)");

        builder.Property(c => c.AllowedScopes)
            .IsRequired()
            .HasMaxLength(2000)
            .HasColumnType("nvarchar(2000)")
            .HasComment("Allowed OAuth scopes for this client (JSON array or comma-separated)");

        builder.Property(c => c.ClientType)
            .IsRequired()
            .HasMaxLength(50)
            .HasColumnType("nvarchar(50)")
            .HasDefaultValue("confidential")
            .HasComment("Client type: confidential, public, spa, native");

        // Token Settings
        builder.Property(c => c.AccessTokenLifetime)
            .IsRequired()
            .HasDefaultValue(3600)
            .HasComment("Access token lifetime in seconds (default: 3600 = 1 hour)");

        builder.Property(c => c.RefreshTokenLifetime)
            .IsRequired()
            .HasDefaultValue(2592000)
            .HasComment("Refresh token lifetime in seconds (default: 2592000 = 30 days)");

        builder.Property(c => c.AllowOfflineAccess)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnType("bit")
            .HasComment("Whether client can request refresh tokens (offline_access scope)");

        // Security & Behavior
        builder.Property(c => c.RequireClientSecret)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnType("bit")
            .HasComment("Whether client must provide a secret for authentication");

        builder.Property(c => c.RequireConsent)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnType("bit")
            .HasComment("Whether user consent is required");

        builder.Property(c => c.RequirePkce)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnType("bit")
            .HasComment("Whether PKCE (Proof Key for Code Exchange) is required");

        builder.Property(c => c.AllowPlainTextPkce)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnType("bit")
            .HasComment("Whether plain text PKCE is allowed (should be false for security)");

        // Status
        builder.Property(c => c.IsEnabled)
            .IsRequired()
            .HasDefaultValue(true)
            .HasColumnType("bit")
            .HasComment("Whether the client is currently enabled and can authenticate");

        // Audit fields from BaseEntity
        builder.Property(c => c.CreatedAt)
            .IsRequired()
            .HasColumnType("datetime2")
            .HasComment("Timestamp when the client was created");

        builder.Property(c => c.UpdatedAt)
            .IsRequired(false)
            .HasColumnType("datetime2")
            .HasComment("Timestamp when the client was last updated");

        builder.Property(c => c.CreatedBy)
            .HasMaxLength(200)
            .HasColumnType("nvarchar(200)")
            .IsRequired(false)
            .HasComment("User who created this record");

        builder.Property(c => c.UpdatedBy)
            .HasMaxLength(200)
            .HasColumnType("nvarchar(200)")
            .IsRequired(false)
            .HasComment("User who last updated this record");

        // Soft delete
        builder.Property(c => c.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false)
            .HasColumnType("bit")
            .HasComment("Soft delete flag");

        // Query filter for soft deletes - automatically exclude deleted records
        builder.HasQueryFilter(c => !c.IsDeleted);

        // Indexes for performance optimization
        
        // Unique index on ClientId for active clients only
        builder.HasIndex(c => c.ClientId)
            .IsUnique()
            .HasDatabaseName("IX_Clients_ClientId")
            .HasFilter("[IsDeleted] = 0");

        // Index on IsEnabled for filtering enabled clients
        builder.HasIndex(c => c.IsEnabled)
            .HasDatabaseName("IX_Clients_IsEnabled")
            .HasFilter("[IsDeleted] = 0 AND [IsEnabled] = 1");

        // Index on ClientType for filtering by client type
        builder.HasIndex(c => c.ClientType)
            .HasDatabaseName("IX_Clients_ClientType")
            .HasFilter("[IsDeleted] = 0");

        // Composite index for common query patterns (enabled + type)
        builder.HasIndex(c => new { c.IsEnabled, c.ClientType })
            .HasDatabaseName("IX_Clients_IsEnabled_ClientType")
            .HasFilter("[IsDeleted] = 0 AND [IsEnabled] = 1");

        // Index on IsDeleted for soft delete queries
        builder.HasIndex(c => c.IsDeleted)
            .HasDatabaseName("IX_Clients_IsDeleted")
            .HasFilter("[IsDeleted] = 0");

        // Index on CreatedAt for temporal queries
        builder.HasIndex(c => c.CreatedAt)
            .HasDatabaseName("IX_Clients_CreatedAt");

        // Composite index for active enabled clients
        builder.HasIndex(c => new { c.IsDeleted, c.IsEnabled })
            .HasDatabaseName("IX_Clients_IsDeleted_IsEnabled")
            .HasFilter("[IsDeleted] = 0 AND [IsEnabled] = 1");

        // Index on ClientName for search functionality
        builder.HasIndex(c => c.ClientName)
            .HasDatabaseName("IX_Clients_ClientName")
            .HasFilter("[IsDeleted] = 0");

        // Composite index for client authentication (ClientId + IsEnabled)
        builder.HasIndex(c => new { c.ClientId, c.IsEnabled })
            .HasDatabaseName("IX_Clients_ClientId_IsEnabled")
            .HasFilter("[IsDeleted] = 0 AND [IsEnabled] = 1");
    }
}