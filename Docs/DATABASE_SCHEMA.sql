SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.SecurityAuditEvents', N'U') IS NOT NULL DROP TABLE dbo.SecurityAuditEvents;
IF OBJECT_ID(N'dbo.AuthenticationAuditEvents', N'U') IS NOT NULL DROP TABLE dbo.AuthenticationAuditEvents;
IF OBJECT_ID(N'dbo.MfaTempTokenSessions', N'U') IS NOT NULL DROP TABLE dbo.MfaTempTokenSessions;
IF OBJECT_ID(N'dbo.AccessTokenSessions', N'U') IS NOT NULL DROP TABLE dbo.AccessTokenSessions;
IF OBJECT_ID(N'dbo.Fido2Transactions', N'U') IS NOT NULL DROP TABLE dbo.Fido2Transactions;
IF OBJECT_ID(N'dbo.MfaChallenges', N'U') IS NOT NULL DROP TABLE dbo.MfaChallenges;
IF OBJECT_ID(N'dbo.UserMfaMethods', N'U') IS NOT NULL DROP TABLE dbo.UserMfaMethods;
IF OBJECT_ID(N'dbo.UserFido2Credentials', N'U') IS NOT NULL DROP TABLE dbo.UserFido2Credentials;
IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL DROP TABLE dbo.Users;
GO

CREATE TABLE dbo.Users (
    Id bigint IDENTITY(1,1) NOT NULL,
    Username nvarchar(100) NOT NULL,
    Email nvarchar(255) NOT NULL,
    PasswordHash nvarchar(max) NOT NULL,
    IsActive bit NOT NULL,
    IsFido2MfaEnabled bit NOT NULL,
    CreatedAtUtc datetime2 NOT NULL,
    LastLoginAtUtc datetime2 NULL,
    CONSTRAINT PK_Users PRIMARY KEY (Id)
);
GO

CREATE UNIQUE INDEX IX_Users_Username ON dbo.Users (Username);
CREATE UNIQUE INDEX IX_Users_Email ON dbo.Users (Email);
GO

CREATE TABLE dbo.UserFido2Credentials (
    Id bigint IDENTITY(1,1) NOT NULL,
    UserId bigint NOT NULL,
    CredentialId varbinary(900) NOT NULL,
    PublicKey varbinary(max) NOT NULL,
    UserHandle varbinary(max) NOT NULL,
    SignatureCounter bigint NOT NULL,
    AaGuid nvarchar(max) NULL,
    CredType nvarchar(max) NULL,
    CreatedAtUtc datetime2 NOT NULL,
    LastUsedAtUtc datetime2 NULL,
    CONSTRAINT PK_UserFido2Credentials PRIMARY KEY (Id),
    CONSTRAINT FK_UserFido2Credentials_Users_UserId FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE CASCADE
);
GO

CREATE UNIQUE INDEX IX_UserFido2Credentials_CredentialId ON dbo.UserFido2Credentials (CredentialId);
CREATE INDEX IX_UserFido2Credentials_UserId ON dbo.UserFido2Credentials (UserId);
GO

CREATE TABLE dbo.UserMfaMethods (
    Id bigint IDENTITY(1,1) NOT NULL,
    UserId bigint NOT NULL,
    Method nvarchar(30) NOT NULL,
    IsEnabled bit NOT NULL,
    IsPrimary bit NOT NULL,
    IsVerified bit NOT NULL,
    ContactValue nvarchar(320) NULL,
    CreatedAtUtc datetime2 NOT NULL,
    UpdatedAtUtc datetime2 NOT NULL,
    CONSTRAINT PK_UserMfaMethods PRIMARY KEY (Id)
);
GO

CREATE UNIQUE INDEX IX_UserMfaMethods_UserId_Method ON dbo.UserMfaMethods (UserId, Method);
CREATE INDEX IX_UserMfaMethods_UserId_IsEnabled ON dbo.UserMfaMethods (UserId, IsEnabled);
GO

CREATE TABLE dbo.MfaChallenges (
    Id uniqueidentifier NOT NULL,
    UserId bigint NOT NULL,
    Purpose nvarchar(30) NOT NULL,
    Method nvarchar(30) NULL,
    Provider nvarchar(30) NULL,
    ProviderRequestId nvarchar(120) NULL,
    Channel nvarchar(30) NULL,
    ContactValue nvarchar(320) NULL,
    Status nvarchar(30) NOT NULL,
    ExpiresAtUtc datetime2 NOT NULL,
    VerifiedAtUtc datetime2 NULL,
    IpAddress nvarchar(100) NULL,
    UserAgent nvarchar(500) NULL,
    CreatedAtUtc datetime2 NOT NULL,
    CONSTRAINT PK_MfaChallenges PRIMARY KEY (Id)
);
GO

CREATE INDEX IX_MfaChallenges_UserId_Status_ExpiresAtUtc ON dbo.MfaChallenges (UserId, Status, ExpiresAtUtc);
CREATE INDEX IX_MfaChallenges_UserId_Purpose_Status_ExpiresAtUtc ON dbo.MfaChallenges (UserId, Purpose, Status, ExpiresAtUtc);
CREATE INDEX IX_MfaChallenges_ProviderRequestId ON dbo.MfaChallenges (ProviderRequestId);
GO

CREATE TABLE dbo.Fido2Transactions (
    Id uniqueidentifier NOT NULL,
    UserId bigint NOT NULL,
    Type nvarchar(50) NOT NULL,
    OptionsJson nvarchar(max) NOT NULL,
    IsUsed bit NOT NULL,
    IpAddress nvarchar(100) NOT NULL,
    UserAgent nvarchar(500) NOT NULL,
    CreatedAtUtc datetime2 NOT NULL,
    ExpiresAtUtc datetime2 NOT NULL,
    ParentMfaTransactionId uniqueidentifier NULL,
    CONSTRAINT PK_Fido2Transactions PRIMARY KEY (Id)
);
GO

CREATE INDEX IX_Fido2Transactions_UserId_Type_IsUsed ON dbo.Fido2Transactions (UserId, Type, IsUsed);
CREATE INDEX IX_Fido2Transactions_ParentMfaTransactionId ON dbo.Fido2Transactions (ParentMfaTransactionId);
GO

CREATE TABLE dbo.AccessTokenSessions (
    Id uniqueidentifier NOT NULL,
    UserId bigint NOT NULL,
    TokenJti nvarchar(100) NOT NULL,
    IssuedAtUtc datetime2 NOT NULL,
    ExpiresAtUtc datetime2 NOT NULL,
    RevokedAtUtc datetime2 NULL,
    RevokeReason nvarchar(100) NULL,
    IpAddress nvarchar(100) NULL,
    UserAgent nvarchar(500) NULL,
    CONSTRAINT PK_AccessTokenSessions PRIMARY KEY (Id)
);
GO

CREATE UNIQUE INDEX IX_AccessTokenSessions_TokenJti ON dbo.AccessTokenSessions (TokenJti);
CREATE INDEX IX_AccessTokenSessions_UserId_ExpiresAtUtc ON dbo.AccessTokenSessions (UserId, ExpiresAtUtc);
GO

CREATE TABLE dbo.MfaTempTokenSessions (
    Id uniqueidentifier NOT NULL,
    UserId bigint NOT NULL,
    MfaTransactionId uniqueidentifier NOT NULL,
    TokenJti nvarchar(100) NOT NULL,
    IssuedAtUtc datetime2 NOT NULL,
    ExpiresAtUtc datetime2 NOT NULL,
    ConsumedAtUtc datetime2 NULL,
    RevokedAtUtc datetime2 NULL,
    IpAddress nvarchar(100) NULL,
    UserAgent nvarchar(500) NULL,
    CONSTRAINT PK_MfaTempTokenSessions PRIMARY KEY (Id)
);
GO

CREATE UNIQUE INDEX IX_MfaTempTokenSessions_TokenJti ON dbo.MfaTempTokenSessions (TokenJti);
CREATE INDEX IX_MfaTempTokenSessions_UserId_ExpiresAtUtc ON dbo.MfaTempTokenSessions (UserId, ExpiresAtUtc);
CREATE INDEX IX_MfaTempTokenSessions_MfaTransactionId ON dbo.MfaTempTokenSessions (MfaTransactionId);
GO

CREATE TABLE dbo.AuthenticationAuditEvents (
    Id bigint IDENTITY(1,1) NOT NULL,
    OccurredAtUtc datetime2 NOT NULL,
    UserId bigint NULL,
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
GO

CREATE INDEX IX_AuthenticationAuditEvents_OccurredAtUtc ON dbo.AuthenticationAuditEvents (OccurredAtUtc);
CREATE INDEX IX_AuthenticationAuditEvents_UsernameOrEmail_OccurredAtUtc ON dbo.AuthenticationAuditEvents (UsernameOrEmail, OccurredAtUtc);
CREATE INDEX IX_AuthenticationAuditEvents_IpAddress_OccurredAtUtc ON dbo.AuthenticationAuditEvents (IpAddress, OccurredAtUtc);
CREATE INDEX IX_AuthenticationAuditEvents_Outcome_OccurredAtUtc ON dbo.AuthenticationAuditEvents (Outcome, OccurredAtUtc);
CREATE INDEX IX_AuthenticationAuditEvents_UserId_OccurredAtUtc ON dbo.AuthenticationAuditEvents (UserId, OccurredAtUtc);
GO

CREATE TABLE dbo.SecurityAuditEvents (
    Id bigint IDENTITY(1,1) NOT NULL,
    OccurredAtUtc datetime2 NOT NULL,
    Category nvarchar(60) NOT NULL,
    EventType nvarchar(120) NOT NULL,
    Severity nvarchar(30) NOT NULL,
    Outcome nvarchar(20) NOT NULL,
    UserId bigint NULL,
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
GO

CREATE INDEX IX_SecurityAuditEvents_OccurredAtUtc ON dbo.SecurityAuditEvents (OccurredAtUtc);
CREATE INDEX IX_SecurityAuditEvents_Category_OccurredAtUtc ON dbo.SecurityAuditEvents (Category, OccurredAtUtc);
CREATE INDEX IX_SecurityAuditEvents_Outcome_OccurredAtUtc ON dbo.SecurityAuditEvents (Outcome, OccurredAtUtc);
CREATE INDEX IX_SecurityAuditEvents_UserId_OccurredAtUtc ON dbo.SecurityAuditEvents (UserId, OccurredAtUtc);
CREATE INDEX IX_SecurityAuditEvents_IpAddress_OccurredAtUtc ON dbo.SecurityAuditEvents (IpAddress, OccurredAtUtc);
GO

INSERT INTO dbo.Users (
    Username,
    Email,
    PasswordHash,
    IsActive,
    IsFido2MfaEnabled,
    CreatedAtUtc,
    LastLoginAtUtc
)
VALUES (
    'cruzrx2',
    'davidrosas0192@gmail.com',
    'Rdavid58@',
    1,
    0,
    '2026-06-28T00:00:00Z',
    NULL
);
GO