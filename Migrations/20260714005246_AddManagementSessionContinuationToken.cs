using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authentication.Fido2.Migrations
{
    /// <inheritdoc />
    public partial class AddManagementSessionContinuationToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContinuationToken",
                table: "MfaManagementSessions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "StepVersion",
                table: "MfaManagementSessions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_MfaManagementSessions_ContinuationToken",
                table: "MfaManagementSessions",
                column: "ContinuationToken");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MfaManagementSessions_ContinuationToken",
                table: "MfaManagementSessions");

            migrationBuilder.DropColumn(
                name: "ContinuationToken",
                table: "MfaManagementSessions");

            migrationBuilder.DropColumn(
                name: "StepVersion",
                table: "MfaManagementSessions");
        }
    }
}
