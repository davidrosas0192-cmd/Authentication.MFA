using Authentication.Fido2.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authentication.Fido2.Data.Configurations;

public class AuthenticationAuditEventConfiguration : IEntityTypeConfiguration<AuthenticationAuditEvent>
{
    public void Configure(EntityTypeBuilder<AuthenticationAuditEvent> builder)
    {
        builder.ToTable("AuthenticationAuditEvents");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Stage).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Method).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Outcome).HasMaxLength(20).IsRequired();
        builder.Property(x => x.UsernameOrEmail).HasMaxLength(320);
        builder.Property(x => x.FailureReason).HasMaxLength(400);
        builder.Property(x => x.IpAddress).HasMaxLength(100);
        builder.Property(x => x.UserAgent).HasMaxLength(500);
        builder.Property(x => x.CorrelationId).HasMaxLength(100);

        builder.HasIndex(x => x.OccurredAtUtc);
        builder.HasIndex(x => new { x.UsernameOrEmail, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.IpAddress, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.Outcome, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.UserId, x.OccurredAtUtc });
    }
}
