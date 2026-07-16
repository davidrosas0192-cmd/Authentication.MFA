using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Authentication.Fido2.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateMfaSessionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MfaSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    SessionType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    TokenJti = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ContinuationToken = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StepVersion = table.Column<int>(type: "int", nullable: true),
                    ChallengeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MfaTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MfaSessions", x => x.Id);
                    table.CheckConstraint("CK_MfaSessions_SessionType", "[SessionType] IN ('temp_token', 'login_enrollment')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MfaSessions_ChallengeId",
                table: "MfaSessions",
                column: "ChallengeId");

            migrationBuilder.CreateIndex(
                name: "IX_MfaSessions_ContinuationToken",
                table: "MfaSessions",
                column: "ContinuationToken");

            migrationBuilder.CreateIndex(
                name: "IX_MfaSessions_MfaTransactionId",
                table: "MfaSessions",
                column: "MfaTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_MfaSessions_SessionType_Status_ExpiresAtUtc",
                table: "MfaSessions",
                columns: new[] { "SessionType", "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MfaSessions_TokenJti",
                table: "MfaSessions",
                column: "TokenJti",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MfaSessions_UserId_SessionType_ExpiresAtUtc",
                table: "MfaSessions",
                columns: new[] { "UserId", "SessionType", "ExpiresAtUtc" });

            migrationBuilder.Sql(
                @"
INSERT INTO MfaSessions
(
    Id,
    UserId,
    SessionType,
    TokenJti,
    ExpiresAtUtc,
    CreatedAtUtc,
    UpdatedAtUtc,
    Status,
    ContinuationToken,
    StepVersion,
    ChallengeId,
    CompletedAtUtc,
    MfaTransactionId,
    IssuedAtUtc,
    ConsumedAtUtc,
    RevokedAtUtc,
    IpAddress,
    UserAgent
)
SELECT
    Id,
    UserId,
    'login_enrollment',
    TokenJti,
    ExpiresAtUtc,
    CreatedAtUtc,
    UpdatedAtUtc,
    Status,
    ContinuationToken,
    StepVersion,
    ChallengeId,
    CompletedAtUtc,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL
FROM MfaLoginEnrollmentSessions;

INSERT INTO MfaSessions
(
    Id,
    UserId,
    SessionType,
    TokenJti,
    ExpiresAtUtc,
    CreatedAtUtc,
    UpdatedAtUtc,
    Status,
    ContinuationToken,
    StepVersion,
    ChallengeId,
    CompletedAtUtc,
    MfaTransactionId,
    IssuedAtUtc,
    ConsumedAtUtc,
    RevokedAtUtc,
    IpAddress,
    UserAgent
)
SELECT
    Id,
    UserId,
    'temp_token',
    TokenJti,
    ExpiresAtUtc,
    IssuedAtUtc,
    IssuedAtUtc,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    MfaTransactionId,
    IssuedAtUtc,
    ConsumedAtUtc,
    RevokedAtUtc,
    IpAddress,
    UserAgent
FROM MfaTempTokenSessions;

DROP TABLE MfaLoginEnrollmentSessions;
DROP TABLE MfaTempTokenSessions;
"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MfaLoginEnrollmentSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChallengeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ContinuationToken = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    StepVersion = table.Column<int>(type: "int", nullable: false),
                    TokenJti = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MfaLoginEnrollmentSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MfaTempTokenSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConsumedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IssuedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MfaTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TokenJti = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UserId = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MfaTempTokenSessions", x => x.Id);
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

            migrationBuilder.Sql(
                @"
INSERT INTO MfaLoginEnrollmentSessions
(
    Id,
    UserId,
    Status,
    ContinuationToken,
    StepVersion,
    TokenJti,
    ChallengeId,
    ExpiresAtUtc,
    CompletedAtUtc,
    CreatedAtUtc,
    UpdatedAtUtc
)
SELECT
    Id,
    UserId,
    ISNULL(Status, 'enrollment_required'),
    ISNULL(ContinuationToken, ''),
    ISNULL(StepVersion, 0),
    TokenJti,
    ChallengeId,
    ExpiresAtUtc,
    CompletedAtUtc,
    CreatedAtUtc,
    UpdatedAtUtc
FROM MfaSessions
WHERE SessionType = 'login_enrollment';

INSERT INTO MfaTempTokenSessions
(
    Id,
    UserId,
    MfaTransactionId,
    TokenJti,
    IssuedAtUtc,
    ExpiresAtUtc,
    ConsumedAtUtc,
    RevokedAtUtc,
    IpAddress,
    UserAgent
)
SELECT
    Id,
    UserId,
    MfaTransactionId,
    TokenJti,
    ISNULL(IssuedAtUtc, CreatedAtUtc),
    ExpiresAtUtc,
    ConsumedAtUtc,
    RevokedAtUtc,
    IpAddress,
    UserAgent
FROM MfaSessions
WHERE SessionType = 'temp_token'
  AND MfaTransactionId IS NOT NULL;

DROP TABLE MfaSessions;
"
            );
        }
    }
}
