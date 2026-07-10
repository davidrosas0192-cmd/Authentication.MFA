using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authentication.Fido2.Migrations
{
    /// <inheritdoc />
    public partial class AddOwaspAuditTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuthenticationAuditEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    UsernameOrEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    Stage = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FailureReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthenticationAuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SecurityAuditEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: true),
                    UsernameOrEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RequestPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    HttpMethod = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityAuditEvents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationAuditEvents_IpAddress_OccurredAtUtc",
                table: "AuthenticationAuditEvents",
                columns: new[] { "IpAddress", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationAuditEvents_OccurredAtUtc",
                table: "AuthenticationAuditEvents",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationAuditEvents_Outcome_OccurredAtUtc",
                table: "AuthenticationAuditEvents",
                columns: new[] { "Outcome", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationAuditEvents_UserId_OccurredAtUtc",
                table: "AuthenticationAuditEvents",
                columns: new[] { "UserId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthenticationAuditEvents_UsernameOrEmail_OccurredAtUtc",
                table: "AuthenticationAuditEvents",
                columns: new[] { "UsernameOrEmail", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditEvents_Category_OccurredAtUtc",
                table: "SecurityAuditEvents",
                columns: new[] { "Category", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditEvents_IpAddress_OccurredAtUtc",
                table: "SecurityAuditEvents",
                columns: new[] { "IpAddress", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditEvents_OccurredAtUtc",
                table: "SecurityAuditEvents",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditEvents_Outcome_OccurredAtUtc",
                table: "SecurityAuditEvents",
                columns: new[] { "Outcome", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityAuditEvents_UserId_OccurredAtUtc",
                table: "SecurityAuditEvents",
                columns: new[] { "UserId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuthenticationAuditEvents");

            migrationBuilder.DropTable(
                name: "SecurityAuditEvents");
        }
    }
}
