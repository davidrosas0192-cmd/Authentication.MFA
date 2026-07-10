using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authentication.Fido2.Migrations
{
    /// <inheritdoc />
    public partial class AddMfaTempTokenSessionsAndFido2ParentTx : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentMfaTransactionId",
                table: "Fido2Transactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MfaTempTokenSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    MfaTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenJti = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MfaTempTokenSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Fido2Transactions_ParentMfaTransactionId",
                table: "Fido2Transactions",
                column: "ParentMfaTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_MfaTempTokenSessions_MfaTransactionId",
                table: "MfaTempTokenSessions",
                column: "MfaTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_MfaTempTokenSessions_TokenJti",
                table: "MfaTempTokenSessions",
                column: "TokenJti",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MfaTempTokenSessions_UserId_ExpiresAtUtc",
                table: "MfaTempTokenSessions",
                columns: new[] { "UserId", "ExpiresAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MfaTempTokenSessions");

            migrationBuilder.DropIndex(
                name: "IX_Fido2Transactions_ParentMfaTransactionId",
                table: "Fido2Transactions");

            migrationBuilder.DropColumn(
                name: "ParentMfaTransactionId",
                table: "Fido2Transactions");
        }
    }
}
