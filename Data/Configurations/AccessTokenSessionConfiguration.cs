using Authentication.Fido2.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authentication.Fido2.Data.Configurations;

public class AccessTokenSessionConfiguration : IEntityTypeConfiguration<AccessTokenSession>
{
    public void Configure(EntityTypeBuilder<AccessTokenSession> builder)
    {
        builder.ToTable("AccessTokenSessions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TokenJti).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RevokeReason).HasMaxLength(100);
        builder.Property(x => x.IpAddress).HasMaxLength(100);
        builder.Property(x => x.UserAgent).HasMaxLength(500);

        builder.HasIndex(x => x.TokenJti).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.ExpiresAtUtc });
    }
}
