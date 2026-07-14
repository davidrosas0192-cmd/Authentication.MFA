using Authentication.Fido2.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authentication.Fido2.Data.Configurations;

public class MfaLoginEnrollmentSessionConfiguration : IEntityTypeConfiguration<MfaLoginEnrollmentSession>
{
    public void Configure(EntityTypeBuilder<MfaLoginEnrollmentSession> builder)
    {
        builder.ToTable("MfaLoginEnrollmentSessions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ContinuationToken).HasMaxLength(100).IsRequired();
        builder.Property(x => x.TokenJti).HasMaxLength(100).IsRequired();
        builder.Property(x => x.StepVersion).IsRequired();

        builder.HasIndex(x => x.TokenJti).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.Status, x.ExpiresAtUtc });
        builder.HasIndex(x => x.ContinuationToken);
        builder.HasIndex(x => x.ChallengeId);
    }
}