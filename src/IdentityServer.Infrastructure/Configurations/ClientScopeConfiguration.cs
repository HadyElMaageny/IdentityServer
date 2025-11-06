using IdentityServer.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityServer.Infrastructure.Configurations;

public class ClientScopeConfiguration : IEntityTypeConfiguration<ClientScope>
{
    public void Configure(EntityTypeBuilder<ClientScope> builder)
    {
        builder.ToTable("ClientScopes");

        builder.HasKey(cs => new { cs.ClientId, cs.ScopeId });

        builder.HasOne(cs => cs.Client)
            .WithMany(c => c.ClientScopes)
            .HasForeignKey(cs => cs.ClientId);

        builder.HasOne(cs => cs.Scope)
            .WithMany(s => s.ClientScopes)
            .HasForeignKey(cs => cs.ScopeId);
    }
}