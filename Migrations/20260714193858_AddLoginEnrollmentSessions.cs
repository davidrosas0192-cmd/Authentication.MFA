using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authentication.Fido2.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginEnrollmentSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MfaLoginEnrollmentSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ContinuationToken = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StepVersion = table.Column<int>(type: "int", nullable: false),
                    TokenJti = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ChallengeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MfaLoginEnrollmentSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MfaLoginEnrollmentSessions_ChallengeId",
                table: "MfaLoginEnrollmentSessions",
                column: "ChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_MfaLoginEnrollmentSessions_ContinuationToken",
                table: "MfaLoginEnrollmentSessions",
                column: "ContinuationToken");

            migrationBuilder.CreateIndex(
                name: "IX_MfaLoginEnrollmentSessions_TokenJti",
                table: "MfaLoginEnrollmentSessions",
                column: "TokenJti",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MfaLoginEnrollmentSessions_UserId_Status_ExpiresAtUtc",
                table: "MfaLoginEnrollmentSessions",
                columns: new[] { "UserId", "Status", "ExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MfaLoginEnrollmentSessions");
        }
    }
}
