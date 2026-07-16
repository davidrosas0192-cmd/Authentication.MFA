using Authentication.Fido2.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authentication.Fido2.Data.Configurations;

public class RefreshTokenSessionConfiguration : IEntityTypeConfiguration<RefreshTokenSession>
{
    public void Configure(EntityTypeBuilder<RefreshTokenSession> builder)
    {
        builder.ToTable("RefreshTokenSessions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TokenHash).HasMaxLength(256).IsRequired();
        builder.Property(x => x.IpAddress).HasMaxLength(100);
        builder.Property(x => x.UserAgent).HasMaxLength(500);
        builder.Property(x => x.RevokeReason).HasMaxLength(100);

        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.ExpiresAtUtc, x.RevokedAtUtc });
        builder.HasIndex(x => x.AccessTokenSessionId);
        builder.HasIndex(x => x.PreviousTokenSessionId);
        builder.HasIndex(x => new { x.UserId, x.ExpiresAtUtc })
               .HasFilter("[RevokedAtUtc] IS NULL")
               .HasDatabaseName("IX_RefreshTokenSessions_Active");
    }
}
