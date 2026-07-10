using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Authentication.Fido2.Entities;

namespace Authentication.Fido2.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Username).IsRequired().HasMaxLength(100);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(255);
        builder.Property(u => u.PasswordHash).IsRequired();

        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => u.Username).IsUnique();

        builder.HasData(
            new User
            {
                Id = 1,
                Username = "cruzrx2",
                Email = "davidrosas0192@gmail.com",
                PasswordHash = "Rdavid58@",
                IsActive = true,
                CreatedAtUtc = new DateTime(2026, 6, 28, 0, 0, 0, DateTimeKind.Utc),
                LastLoginAtUtc = null
            
            });
    }
}