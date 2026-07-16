using Authentication.Fido2.Constants;
using Authentication.Fido2.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authentication.Fido2.Data.Configurations;

public class MfaSessionConfiguration : IEntityTypeConfiguration<MfaSession>
{
    public void Configure(EntityTypeBuilder<MfaSession> builder)
    {
        builder.ToTable(
            "MfaSessions",
            t =>
                t.HasCheckConstraint(
                    "CK_MfaSessions_SessionType",
                    $"[SessionType] IN ('{MfaSessionTypes.TempToken}', '{MfaSessionTypes.LoginEnrollment}')"
                )
        );

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SessionType).HasMaxLength(40).IsRequired();
        builder.Property(x => x.TokenJti).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(40);
        builder.Property(x => x.ContinuationToken).HasMaxLength(100);
        builder.Property(x => x.IpAddress).HasMaxLength(100);
        builder.Property(x => x.UserAgent).HasMaxLength(500);

        builder.HasIndex(x => x.TokenJti).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.SessionType, x.ExpiresAtUtc });
        builder.HasIndex(x => new { x.SessionType, x.Status, x.ExpiresAtUtc });
        builder.HasIndex(x => x.ContinuationToken);
        builder.HasIndex(x => x.ChallengeId);
        builder.HasIndex(x => x.MfaTransactionId);

    }
}