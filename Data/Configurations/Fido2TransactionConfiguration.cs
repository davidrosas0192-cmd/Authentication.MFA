using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Authentication.Fido2.Entities;


namespace Authentication.Fido2.Data.Configurations;

public class Fido2TransactionConfiguration : IEntityTypeConfiguration<Fido2Transaction>
{
    public void Configure(EntityTypeBuilder<Fido2Transaction> builder)
    {

        builder.ToTable("Fido2Transactions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type)

            .HasMaxLength(50)

            .IsRequired();

        builder.Property(x => x.OptionsJson)

            .IsRequired();

        builder.Property(x => x.IpAddress)

            .HasMaxLength(100)

            .IsRequired();

        builder.Property(x => x.UserAgent)

            .HasMaxLength(500)

            .IsRequired();

        builder.HasIndex(x => new { x.UserId, x.Type, x.IsUsed });
        builder.HasIndex(x => x.ParentMfaTransactionId);
    }
}