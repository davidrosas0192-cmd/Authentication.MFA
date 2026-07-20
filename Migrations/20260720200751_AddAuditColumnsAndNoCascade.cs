using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authentication.Fido2.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditColumnsAndNoCascade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserFido2Credentials_Users_UserId",
                table: "UserFido2Credentials");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRecoveryCodes_UserRecoveryCodeBatches_BatchId",
                table: "UserRecoveryCodes");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "UserMfaMethods",
                newName: "ModifiedAtUtc");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "MfaSessions",
                newName: "ModifiedAtUtc");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "MfaManagementSessions",
                newName: "ModifiedAtUtc");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "MfaLoginEnrollmentSessions",
                newName: "ModifiedAtUtc");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Users",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "UserRecoveryCodes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "UserRecoveryCodes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "UserRecoveryCodeBatches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "UserRecoveryCodeBatches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "UserMfaMethods",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "UserMfaMethods",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "UserFido2Credentials",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "UserFido2Credentials",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "RefreshTokenSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "RefreshTokenSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "MfaSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "MfaSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "MfaManagementSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "MfaManagementSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "MfaLoginEnrollmentSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "MfaLoginEnrollmentSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "MfaChallenges",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "MfaChallenges",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Fido2Transactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "Fido2Transactions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "AccessTokenSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModifiedBy",
                table: "AccessTokenSessions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1L,
                columns: new[] { "CreatedBy", "ModifiedBy" },
                values: new object[] { null, null });

            migrationBuilder.AddForeignKey(
                name: "FK_UserFido2Credentials_Users_UserId",
                table: "UserFido2Credentials",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserRecoveryCodes_UserRecoveryCodeBatches_BatchId",
                table: "UserRecoveryCodes",
                column: "BatchId",
                principalTable: "UserRecoveryCodeBatches",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserFido2Credentials_Users_UserId",
                table: "UserFido2Credentials");

            migrationBuilder.DropForeignKey(
                name: "FK_UserRecoveryCodes_UserRecoveryCodeBatches_BatchId",
                table: "UserRecoveryCodes");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "UserRecoveryCodes");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "UserRecoveryCodes");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "UserRecoveryCodeBatches");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "UserRecoveryCodeBatches");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "UserMfaMethods");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "UserMfaMethods");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "UserFido2Credentials");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "UserFido2Credentials");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "RefreshTokenSessions");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "RefreshTokenSessions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "MfaSessions");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "MfaSessions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "MfaManagementSessions");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "MfaManagementSessions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "MfaLoginEnrollmentSessions");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "MfaLoginEnrollmentSessions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "MfaChallenges");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "MfaChallenges");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Fido2Transactions");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "Fido2Transactions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "AccessTokenSessions");

            migrationBuilder.DropColumn(
                name: "ModifiedBy",
                table: "AccessTokenSessions");

            migrationBuilder.RenameColumn(
                name: "ModifiedAtUtc",
                table: "UserMfaMethods",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "ModifiedAtUtc",
                table: "MfaSessions",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "ModifiedAtUtc",
                table: "MfaManagementSessions",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "ModifiedAtUtc",
                table: "MfaLoginEnrollmentSessions",
                newName: "UpdatedAtUtc");

            migrationBuilder.AddForeignKey(
                name: "FK_UserFido2Credentials_Users_UserId",
                table: "UserFido2Credentials",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRecoveryCodes_UserRecoveryCodeBatches_BatchId",
                table: "UserRecoveryCodes",
                column: "BatchId",
                principalTable: "UserRecoveryCodeBatches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
