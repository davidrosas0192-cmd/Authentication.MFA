using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authentication.Fido2.Migrations
{
    /// <inheritdoc />
    public partial class AddMfaChallengeContinuationToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContinuationToken",
                table: "MfaChallenges",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "StepVersion",
                table: "MfaChallenges",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_MfaChallenges_ContinuationToken",
                table: "MfaChallenges",
                column: "ContinuationToken");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MfaChallenges_ContinuationToken",
                table: "MfaChallenges");

            migrationBuilder.DropColumn(
                name: "ContinuationToken",
                table: "MfaChallenges");

            migrationBuilder.DropColumn(
                name: "StepVersion",
                table: "MfaChallenges");
        }
    }
}
