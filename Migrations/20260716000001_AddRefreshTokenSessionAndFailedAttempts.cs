using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authentication.Fido2.Migrations
{
    /// <inheritdoc />
    public partial class AddRefreshTokenSessionAndFailedAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create RefreshTokenSessions table
            migrationBuilder.CreateTable(
                name: "RefreshTokenSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AccessTokenSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokeReason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastRotatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PreviousTokenSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokenSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokenSessions_TokenHash",
                table: "RefreshTokenSessions",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokenSessions_UserId_ExpiresAtUtc_RevokedAtUtc",
                table: "RefreshTokenSessions",
                columns: new[] { "UserId", "ExpiresAtUtc", "RevokedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokenSessions_AccessTokenSessionId",
                table: "RefreshTokenSessions",
                column: "AccessTokenSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokenSessions_PreviousTokenSessionId",
                table: "RefreshTokenSessions",
                column: "PreviousTokenSessionId");

            // Add FailedAttempts and LastFailedAttemptAtUtc to MfaChallenges
            migrationBuilder.AddColumn<int>(
                name: "FailedAttempts",
                table: "MfaChallenges",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastFailedAttemptAtUtc",
                table: "MfaChallenges",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefreshTokenSessions");

            migrationBuilder.DropColumn(
                name: "FailedAttempts",
                table: "MfaChallenges");

            migrationBuilder.DropColumn(
                name: "LastFailedAttemptAtUtc",
                table: "MfaChallenges");
        }
    }
}
