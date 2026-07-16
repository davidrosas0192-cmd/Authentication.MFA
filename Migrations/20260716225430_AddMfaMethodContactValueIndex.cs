using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authentication.Fido2.Migrations
{
    /// <inheritdoc />
    public partial class AddMfaMethodContactValueIndex : Migration
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
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContinuationToken = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StepVersion = table.Column<int>(type: "int", nullable: false),
                    TokenJti = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                name: "IX_UserMfaMethods_Method_ContactValue_Active",
                table: "UserMfaMethods",
                columns: new[] { "Method", "ContactValue" },
                filter: "[IsEnabled] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MfaLoginEnrollmentSessions");

            migrationBuilder.DropIndex(
                name: "IX_UserMfaMethods_Method_ContactValue_Active",
                table: "UserMfaMethods");
        }
    }
}
