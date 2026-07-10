using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authentication.Fido2.Migrations
{
    /// <inheritdoc />
    public partial class AddTwilioMfaMethodsAndChallenges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MfaChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ProviderRequestId = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Channel = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MfaChallenges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserMfaMethods",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    IsVerified = table.Column<bool>(type: "bit", nullable: false),
                    ContactValue = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMfaMethods", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MfaChallenges_ProviderRequestId",
                table: "MfaChallenges",
                column: "ProviderRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_MfaChallenges_UserId_Status_ExpiresAtUtc",
                table: "MfaChallenges",
                columns: new[] { "UserId", "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserMfaMethods_UserId_IsEnabled",
                table: "UserMfaMethods",
                columns: new[] { "UserId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_UserMfaMethods_UserId_Method",
                table: "UserMfaMethods",
                columns: new[] { "UserId", "Method" },
                unique: true);

                        migrationBuilder.Sql(
                                """
                                INSERT INTO UserMfaMethods (UserId, Method, IsEnabled, IsPrimary, IsVerified, ContactValue, CreatedAtUtc, UpdatedAtUtc)
                                SELECT u.Id, 'fido2', 1, 1, 1, NULL, SYSUTCDATETIME(), SYSUTCDATETIME()
                                FROM Users u
                                WHERE u.IsFido2MfaEnabled = 1
                                    AND NOT EXISTS (
                                            SELECT 1
                                            FROM UserMfaMethods m
                                            WHERE m.UserId = u.Id
                                                AND m.Method = 'fido2'
                                    );
                                """
                        );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM UserMfaMethods WHERE Method = 'fido2';");

            migrationBuilder.DropTable(
                name: "MfaChallenges");

            migrationBuilder.DropTable(
                name: "UserMfaMethods");
        }
    }
}
