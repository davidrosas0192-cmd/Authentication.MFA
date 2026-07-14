using Authentication.Fido2.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authentication.Fido2.Data.Configurations;

public class MfaManagementSessionConfiguration : IEntityTypeConfiguration<MfaManagementSession>
{
    public void Configure(EntityTypeBuilder<MfaManagementSession> builder)
    {
        builder.ToTable("MfaManagementSessions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ContinuationToken).HasMaxLength(100).IsRequired();
        builder.Property(x => x.StepVersion).IsRequired();

        builder.HasIndex(x => new { x.UserId, x.Status, x.ExpiresAtUtc });
        builder.HasIndex(x => x.ContinuationToken);
        builder.HasIndex(x => x.ChallengeId);
    }
}
