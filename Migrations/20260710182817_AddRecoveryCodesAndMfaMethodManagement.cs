using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authentication.Fido2.Migrations
{
    /// <inheritdoc />
    public partial class AddRecoveryCodesAndMfaMethodManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserRecoveryCodeBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReplacedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRecoveryCodeBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserRecoveryCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    CodeHash = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRecoveryCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRecoveryCodes_UserRecoveryCodeBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "UserRecoveryCodeBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserRecoveryCodeBatches_UserId_IssuedAtUtc",
                table: "UserRecoveryCodeBatches",
                columns: new[] { "UserId", "IssuedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRecoveryCodeBatches_UserId_ReplacedAtUtc",
                table: "UserRecoveryCodeBatches",
                columns: new[] { "UserId", "ReplacedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserRecoveryCodes_BatchId",
                table: "UserRecoveryCodes",
                column: "BatchId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRecoveryCodes_UserId_UsedAtUtc",
                table: "UserRecoveryCodes",
                columns: new[] { "UserId", "UsedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserRecoveryCodes");

            migrationBuilder.DropTable(
                name: "UserRecoveryCodeBatches");
        }
    }
}
