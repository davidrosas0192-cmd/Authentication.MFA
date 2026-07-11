using Authentication.Fido2.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authentication.Fido2.Data.Configurations;

public class UserRecoveryCodeConfiguration : IEntityTypeConfiguration<UserRecoveryCode>
{
    public void Configure(EntityTypeBuilder<UserRecoveryCode> builder)
    {
        builder.ToTable("UserRecoveryCodes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CodeHash).HasMaxLength(400).IsRequired();

        builder.HasIndex(x => new { x.UserId, x.UsedAtUtc });
        builder.HasIndex(x => x.BatchId);
    }
}
