using Authentication.Fido2.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authentication.Fido2.Data.Configurations;

public class SecurityAuditEventConfiguration : IEntityTypeConfiguration<SecurityAuditEvent>
{
    public void Configure(EntityTypeBuilder<SecurityAuditEvent> builder)
    {
        builder.ToTable("SecurityAuditEvents");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Category).HasMaxLength(60).IsRequired();
        builder.Property(x => x.EventType).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Severity).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Outcome).HasMaxLength(20).IsRequired();
        builder.Property(x => x.UsernameOrEmail).HasMaxLength(320);
        builder.Property(x => x.IpAddress).HasMaxLength(100);
        builder.Property(x => x.UserAgent).HasMaxLength(500);
        builder.Property(x => x.CorrelationId).HasMaxLength(100);
        builder.Property(x => x.RequestPath).HasMaxLength(300);
        builder.Property(x => x.HttpMethod).HasMaxLength(10);
        builder.Property(x => x.FailureReason).HasMaxLength(400);

        builder.HasIndex(x => x.OccurredAtUtc);
        builder.HasIndex(x => new { x.Category, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.Outcome, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.UserId, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.IpAddress, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.Severity, x.OccurredAtUtc });
    }
}
