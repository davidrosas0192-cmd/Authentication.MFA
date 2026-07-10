using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authentication.Fido2.Migrations
{
    /// <inheritdoc />
    public partial class initialSeed2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "CreatedAtUtc", "Email", "IsActive", "IsFido2MfaEnabled", "LastLoginAtUtc", "PasswordHash", "Username" },
                values: new object[] { 1L, new DateTime(2026, 6, 28, 0, 0, 0, 0, DateTimeKind.Utc), "davidrosas0192@gmail.com", true, false, null, "Rdavid58@", "cruzrx2" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 1L);
        }
    }
}
