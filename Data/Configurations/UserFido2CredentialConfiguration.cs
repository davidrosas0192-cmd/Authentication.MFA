using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Authentication.Fido2.Entities;

namespace Authentication.Fido2.Data.Configurations;

public class UserFido2CredentialConfiguration : IEntityTypeConfiguration<UserFido2Credential>
{
    public void Configure(EntityTypeBuilder<UserFido2Credential> builder)
    {
        builder.ToTable("UserFido2Credentials");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CredentialId)

            .IsRequired();

        builder.Property(x => x.PublicKey)

            .IsRequired();

        builder.Property(x => x.UserHandle)

            .IsRequired();

        builder.HasIndex(x => x.CredentialId)

            .IsUnique();

        builder.HasOne(x => x.User)

            .WithMany()

            .HasForeignKey(x => x.UserId);
    }
}
