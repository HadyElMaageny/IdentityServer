using IdentityServer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Infrastructure.Configurations;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Clients");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.ClientIdentifier)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.ClientSecret)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.ClientName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.RedirectUris)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(c => c.PostLogoutRedirectUris)
            .HasMaxLength(1000);

        builder.Property(c => c.AllowedGrantTypes)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(c => c.AllowOfflineAccess)
            .IsRequired();

        builder.Property(c => c.Enabled)
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired(false);

        builder.Property(x => x.CreatedBy)
            .HasMaxLength(200);

        builder.Property(x => x.UpdatedBy)
            .HasMaxLength(200);

        builder.Property(x => x.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        // relationships
        builder.HasMany(c => c.ClientScopes)
            .WithOne(cs => cs.Client)
            .HasForeignKey(cs => cs.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.UserConsents)
            .WithOne(uc => uc.Client)
            .HasForeignKey(uc => uc.ClientId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasMany(c => c.AuthorizationCodes)
            .WithOne(ac => ac.Client)
            .HasForeignKey(ac => ac.ClientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.UserConsents)
            .WithOne(uc => uc.Client)
            .HasForeignKey(uc => uc.ClientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}