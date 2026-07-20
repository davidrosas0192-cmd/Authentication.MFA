SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DROP TABLE IF EXISTS dbo.UserRecoveryCodes;
DROP TABLE IF EXISTS dbo.UserRecoveryCodeBatches;
DROP TABLE IF EXISTS dbo.AuthenticationAuditEvents;
DROP TABLE IF EXISTS dbo.SecurityAuditEvents;
DROP TABLE IF EXISTS dbo.Users;

CREATE TABLE dbo.Users (
    Id uniqueidentifier NOT NULL,
    Username nvarchar(100) NOT NULL,
    Email nvarchar(255) NOT NULL,
    PasswordHash nvarchar(max) NOT NULL,
    IsActive bit NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
    IsFido2MfaEnabled bit NOT NULL CONSTRAINT DF_Users_IsFido2MfaEnabled DEFAULT (0),
    CreatedAtUtc datetime2 NOT NULL,
    CreatedBy nvarchar(450) NULL,
    ModifiedBy nvarchar(450) NULL,
    LastLoginAtUtc datetime2 NULL,
    CONSTRAINT PK_Users PRIMARY KEY (Id)
);

CREATE UNIQUE INDEX IX_Users_Email ON dbo.Users (Email);
CREATE UNIQUE INDEX IX_Users_Username ON dbo.Users (Username);

CREATE TABLE dbo.AuthenticationAuditEvents (
    Id bigint NOT NULL IDENTITY(1,1),
    OccurredAtUtc datetime2 NOT NULL,
    UserId uniqueidentifier NULL,
    UsernameOrEmail nvarchar(320) NULL,
    Stage nvarchar(60) NOT NULL,
    Method nvarchar(30) NOT NULL,
    Outcome nvarchar(20) NOT NULL,
    FailureReason nvarchar(400) NULL,
    IpAddress nvarchar(100) NULL,
    UserAgent nvarchar(500) NULL,
    CorrelationId nvarchar(100) NULL,
    CONSTRAINT PK_AuthenticationAuditEvents PRIMARY KEY (Id)
);

CREATE TABLE dbo.SecurityAuditEvents (
    Id bigint NOT NULL IDENTITY(1,1),
    OccurredAtUtc datetime2 NOT NULL,
    Category nvarchar(60) NOT NULL,
    EventType nvarchar(120) NOT NULL,
    Severity nvarchar(30) NOT NULL,
    Outcome nvarchar(20) NOT NULL,
    UserId uniqueidentifier NULL,
    UsernameOrEmail nvarchar(320) NULL,
    IpAddress nvarchar(100) NULL,
    UserAgent nvarchar(500) NULL,
    CorrelationId nvarchar(100) NULL,
    RequestPath nvarchar(300) NULL,
    HttpMethod nvarchar(10) NULL,
    FailureReason nvarchar(400) NULL,
    DetailsJson nvarchar(max) NULL,
    CONSTRAINT PK_SecurityAuditEvents PRIMARY KEY (Id)
);

CREATE INDEX IX_AuthenticationAuditEvents_IpAddress_OccurredAtUtc ON dbo.AuthenticationAuditEvents (IpAddress, OccurredAtUtc);
CREATE INDEX IX_AuthenticationAuditEvents_OccurredAtUtc ON dbo.AuthenticationAuditEvents (OccurredAtUtc);
CREATE INDEX IX_AuthenticationAuditEvents_Outcome_OccurredAtUtc ON dbo.AuthenticationAuditEvents (Outcome, OccurredAtUtc);
CREATE INDEX IX_AuthenticationAuditEvents_UserId_OccurredAtUtc ON dbo.AuthenticationAuditEvents (UserId, OccurredAtUtc);
CREATE INDEX IX_AuthenticationAuditEvents_UsernameOrEmail_OccurredAtUtc ON dbo.AuthenticationAuditEvents (UsernameOrEmail, OccurredAtUtc);
CREATE INDEX IX_SecurityAuditEvents_Category_OccurredAtUtc ON dbo.SecurityAuditEvents (Category, OccurredAtUtc);
CREATE INDEX IX_SecurityAuditEvents_IpAddress_OccurredAtUtc ON dbo.SecurityAuditEvents (IpAddress, OccurredAtUtc);
CREATE INDEX IX_SecurityAuditEvents_OccurredAtUtc ON dbo.SecurityAuditEvents (OccurredAtUtc);
CREATE INDEX IX_SecurityAuditEvents_Outcome_OccurredAtUtc ON dbo.SecurityAuditEvents (Outcome, OccurredAtUtc);
CREATE INDEX IX_SecurityAuditEvents_UserId_OccurredAtUtc ON dbo.SecurityAuditEvents (UserId, OccurredAtUtc);

CREATE TABLE dbo.UserRecoveryCodeBatches (
    Id uniqueidentifier NOT NULL,
    UserId uniqueidentifier NOT NULL,
    IssuedAtUtc datetime2 NOT NULL,
    ReplacedAtUtc datetime2 NULL,
    CreatedBy nvarchar(450) NULL,
    ModifiedBy nvarchar(450) NULL,
    CONSTRAINT PK_UserRecoveryCodeBatches PRIMARY KEY (Id)
);

CREATE TABLE dbo.UserRecoveryCodes (
    Id uniqueidentifier NOT NULL,
    BatchId uniqueidentifier NOT NULL,
    UserId uniqueidentifier NOT NULL,
    CodeHash nvarchar(max) NOT NULL,
    CreatedAtUtc datetime2 NOT NULL,
    CreatedBy nvarchar(450) NULL,
    ModifiedBy nvarchar(450) NULL,
    UsedAtUtc datetime2 NULL,
    CONSTRAINT PK_UserRecoveryCodes PRIMARY KEY (Id)
);

CREATE INDEX IX_UserRecoveryCodeBatches_UserId_IssuedAtUtc ON dbo.UserRecoveryCodeBatches (UserId, IssuedAtUtc);
CREATE INDEX IX_UserRecoveryCodeBatches_UserId_ReplacedAtUtc ON dbo.UserRecoveryCodeBatches (UserId, ReplacedAtUtc);
CREATE INDEX IX_UserRecoveryCodes_BatchId ON dbo.UserRecoveryCodes (BatchId);
CREATE INDEX IX_UserRecoveryCodes_UserId ON dbo.UserRecoveryCodes (UserId);

ALTER TABLE dbo.UserRecoveryCodeBatches
    ADD CONSTRAINT FK_UserRecoveryCodeBatches_Users_UserId
    FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION;

ALTER TABLE dbo.UserRecoveryCodes
    ADD CONSTRAINT FK_UserRecoveryCodes_UserRecoveryCodeBatches_BatchId
    FOREIGN KEY (BatchId) REFERENCES dbo.UserRecoveryCodeBatches (Id) ON DELETE NO ACTION;

ALTER TABLE dbo.UserRecoveryCodes
    ADD CONSTRAINT FK_UserRecoveryCodes_Users_UserId
    FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION;

COMMIT;
