using Authentication.Fido2.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authentication.Fido2.Data.Configurations;

public class UserRecoveryCodeBatchConfiguration : IEntityTypeConfiguration<UserRecoveryCodeBatch>
{
    public void Configure(EntityTypeBuilder<UserRecoveryCodeBatch> builder)
    {
        builder.ToTable("UserRecoveryCodeBatches");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.UserId, x.ReplacedAtUtc });
        builder.HasIndex(x => new { x.UserId, x.IssuedAtUtc });

        builder.HasMany(x => x.Codes)
            .WithOne(x => x.Batch)
            .HasForeignKey(x => x.BatchId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
