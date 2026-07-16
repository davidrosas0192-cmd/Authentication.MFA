using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authentication.Fido2.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AccessTokenSessions_UserId_ExpiresAtUtc",
                table: "AccessTokenSessions");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditEvents_Severity_OccurredAtUtc",
                table: "SecurityAuditEvents",
                columns: new[] { "Severity", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokenSessions_Active",
                table: "RefreshTokenSessions",
                columns: new[] { "UserId", "ExpiresAtUtc" },
                filter: "[RevokedAtUtc] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MfaChallenges_Status_CreatedAtUtc",
                table: "MfaChallenges",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationAuditEvents_Stage_OccurredAtUtc",
                table: "AuthenticationAuditEvents",
                columns: new[] { "Stage", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AccessTokenSessions_Active",
                table: "AccessTokenSessions",
                columns: new[] { "UserId", "ExpiresAtUtc" },
                filter: "[RevokedAtUtc] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SecurityAuditEvents_Severity_OccurredAtUtc",
                table: "SecurityAuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokenSessions_Active",
                table: "RefreshTokenSessions");

            migrationBuilder.DropIndex(
                name: "IX_MfaChallenges_Status_CreatedAtUtc",
                table: "MfaChallenges");

            migrationBuilder.DropIndex(
                name: "IX_AuthenticationAuditEvents_Stage_OccurredAtUtc",
                table: "AuthenticationAuditEvents");

            migrationBuilder.DropIndex(
                name: "IX_AccessTokenSessions_Active",
                table: "AccessTokenSessions");

            migrationBuilder.CreateIndex(
                name: "IX_AccessTokenSessions_UserId_ExpiresAtUtc",
                table: "AccessTokenSessions",
                columns: new[] { "UserId", "ExpiresAtUtc" });
        }
    }
}
