using IdentityServer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Infrastructure.Configurations;

public class UserConsentConfiguration : IEntityTypeConfiguration<UserConsent>
{
    public void Configure(EntityTypeBuilder<UserConsent> builder)
    {
        builder.ToTable("UserConsents");

        builder.HasKey(uc => uc.Id);

        builder.Property(uc => uc.Scopes)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(uc => uc.GrantedAt)
            .IsRequired();

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

        builder.HasOne(uc => uc.User)
            .WithMany()
            .HasForeignKey(uc => uc.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(uc => uc.Client)
            .WithMany()
            .HasForeignKey(uc => uc.ClientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}