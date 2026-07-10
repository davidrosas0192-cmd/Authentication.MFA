using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authentication.Fido2.Migrations
{
    /// <inheritdoc />
    public partial class SeedDefaultUserMfaMethods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF EXISTS (SELECT 1 FROM Users WHERE Id = 1)
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM UserMfaMethods WHERE UserId = 1 AND Method = 'email')
                    BEGIN
                        INSERT INTO UserMfaMethods
                            (UserId, Method, IsEnabled, IsPrimary, IsVerified, ContactValue, CreatedAtUtc, UpdatedAtUtc)
                        VALUES
                            (1, 'email', 1, 0, 1, 'davidrosas0192@gmail.com', SYSUTCDATETIME(), SYSUTCDATETIME());
                    END;

                    IF NOT EXISTS (SELECT 1 FROM UserMfaMethods WHERE UserId = 1 AND Method = 'sms')
                    BEGIN
                        INSERT INTO UserMfaMethods
                            (UserId, Method, IsEnabled, IsPrimary, IsVerified, ContactValue, CreatedAtUtc, UpdatedAtUtc)
                        VALUES
                            (1, 'sms', 1, 0, 0, '+15555550100', SYSUTCDATETIME(), SYSUTCDATETIME());
                    END;

                    IF NOT EXISTS (SELECT 1 FROM UserMfaMethods WHERE UserId = 1 AND Method = 'fido2')
                    BEGIN
                        INSERT INTO UserMfaMethods
                            (UserId, Method, IsEnabled, IsPrimary, IsVerified, ContactValue, CreatedAtUtc, UpdatedAtUtc)
                        VALUES
                            (1, 'fido2', 1, 1, 1, NULL, SYSUTCDATETIME(), SYSUTCDATETIME());
                    END;
                END;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM UserMfaMethods
                WHERE UserId = 1
                  AND Method IN ('email', 'sms', 'fido2')
                  AND (ContactValue IS NULL OR ContactValue IN ('davidrosas0192@gmail.com', '+15555550100'));
                """
            );
        }
    }
}
