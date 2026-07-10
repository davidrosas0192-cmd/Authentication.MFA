using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authentication.Fido2.Migrations
{
    /// <inheritdoc />
    public partial class AddMfaEnrollmentChallengeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContactValue",
                table: "MfaChallenges",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "MfaChallenges",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_MfaChallenges_UserId_Purpose_Status_ExpiresAtUtc",
                table: "MfaChallenges",
                columns: new[] { "UserId", "Purpose", "Status", "ExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MfaChallenges_UserId_Purpose_Status_ExpiresAtUtc",
                table: "MfaChallenges");

            migrationBuilder.DropColumn(
                name: "ContactValue",
                table: "MfaChallenges");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "MfaChallenges");
        }
    }
}
