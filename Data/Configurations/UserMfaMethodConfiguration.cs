using Authentication.Fido2.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Authentication.Fido2.Data.Configurations;

public class UserMfaMethodConfiguration : IEntityTypeConfiguration<UserMfaMethod>
{
    public void Configure(EntityTypeBuilder<UserMfaMethod> builder)
    {
        builder.ToTable("UserMfaMethods");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Method).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ContactValue).HasMaxLength(320);

        builder.HasIndex(x => new { x.UserId, x.Method }).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.IsEnabled });
    }
}
