using Authentication.Fido2.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authentication.Fido2.Data.Configurations;

public class MfaChallengeConfiguration : IEntityTypeConfiguration<MfaChallenge>
{
    public void Configure(EntityTypeBuilder<MfaChallenge> builder)
    {
        builder.ToTable("MfaChallenges");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Purpose).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Method).HasMaxLength(30);
        builder.Property(x => x.Provider).HasMaxLength(30);
        builder.Property(x => x.ProviderRequestId).HasMaxLength(120);
        builder.Property(x => x.Channel).HasMaxLength(30);
        builder.Property(x => x.ContactValue).HasMaxLength(320);
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();
        builder.Property(x => x.IpAddress).HasMaxLength(100);
        builder.Property(x => x.UserAgent).HasMaxLength(500);

        builder.HasIndex(x => new { x.UserId, x.Status, x.ExpiresAtUtc });
        builder.HasIndex(x => new { x.UserId, x.Purpose, x.Status, x.ExpiresAtUtc });
        builder.HasIndex(x => x.ProviderRequestId);
    }
}
