using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authentication.Fido2.Migrations
{
    /// <inheritdoc />
    public partial class AddMfaManagementSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MfaManagementSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ChallengeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MfaManagementSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MfaManagementSessions_ChallengeId",
                table: "MfaManagementSessions",
                column: "ChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_MfaManagementSessions_UserId_Status_ExpiresAtUtc",
                table: "MfaManagementSessions",
                columns: new[] { "UserId", "Status", "ExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MfaManagementSessions");
        }
    }
}
