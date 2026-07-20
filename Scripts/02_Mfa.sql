SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Requires 01_Users.sql to be applied first.

BEGIN TRANSACTION;

DROP TABLE IF EXISTS dbo.RefreshTokenSessions;
DROP TABLE IF EXISTS dbo.AccessTokenSessions;
DROP TABLE IF EXISTS dbo.MfaTokenSessions;
DROP TABLE IF EXISTS dbo.MfaEnrollmentTokenSessions;
DROP TABLE IF EXISTS dbo.MfaManagementSessions;
DROP TABLE IF EXISTS dbo.MfaChallenges;
DROP TABLE IF EXISTS dbo.UserMfaMethods;
CREATE TABLE dbo.UserMfaMethods (
    Id bigint NOT NULL IDENTITY(1,1),
    UserId uniqueidentifier NOT NULL,
    Method nvarchar(30) NOT NULL,
    IsEnabled bit NOT NULL CONSTRAINT DF_UserMfaMethods_IsEnabled DEFAULT (1),
    IsPrimary bit NOT NULL,
    IsVerified bit NOT NULL,
    ContactValue nvarchar(320) NULL,
    CreatedAtUtc datetime2 NOT NULL,
    ModifiedAtUtc datetime2 NOT NULL,
    CreatedBy nvarchar(450) NULL,
    ModifiedBy nvarchar(450) NULL,
    CONSTRAINT PK_UserMfaMethods PRIMARY KEY (Id)
);

CREATE UNIQUE INDEX IX_UserMfaMethods_UserId_Method ON dbo.UserMfaMethods (UserId, Method);
CREATE INDEX IX_UserMfaMethods_UserId_IsEnabled ON dbo.UserMfaMethods (UserId, IsEnabled);
CREATE INDEX IX_UserMfaMethods_Method_ContactValue_Active ON dbo.UserMfaMethods (Method, ContactValue) WHERE IsEnabled = 1;

CREATE TABLE dbo.AccessTokenSessions (
    Id uniqueidentifier NOT NULL,
    UserId uniqueidentifier NOT NULL,
    TokenJti nvarchar(100) NOT NULL,
    IssuedAtUtc datetime2 NOT NULL,
    ExpiresAtUtc datetime2 NOT NULL,
    RevokedAtUtc datetime2 NULL,
    RevokeReason nvarchar(100) NULL,
    IpAddress nvarchar(100) NULL,
    UserAgent nvarchar(500) NULL,
    CreatedBy nvarchar(450) NULL,
    ModifiedBy nvarchar(450) NULL,
    CONSTRAINT PK_AccessTokenSessions PRIMARY KEY (Id)
);

CREATE UNIQUE INDEX IX_AccessTokenSessions_TokenJti ON dbo.AccessTokenSessions (TokenJti);
CREATE INDEX IX_AccessTokenSessions_UserId_ExpiresAtUtc ON dbo.AccessTokenSessions (UserId, ExpiresAtUtc);
CREATE INDEX IX_AccessTokenSessions_Active ON dbo.AccessTokenSessions (UserId, ExpiresAtUtc) WHERE RevokedAtUtc IS NULL;

CREATE TABLE dbo.RefreshTokenSessions (
    Id uniqueidentifier NOT NULL,
    UserId uniqueidentifier NOT NULL,
    TokenHash nvarchar(256) NOT NULL,
    AccessTokenSessionId uniqueidentifier NOT NULL,
    IssuedAtUtc datetime2 NOT NULL,
    ExpiresAtUtc datetime2 NOT NULL,
    RevokedAtUtc datetime2 NULL,
    RevokeReason nvarchar(100) NULL,
    LastRotatedAtUtc datetime2 NULL,
    PreviousTokenSessionId uniqueidentifier NULL,
    IpAddress nvarchar(100) NULL,
    UserAgent nvarchar(500) NULL,
    CreatedBy nvarchar(450) NULL,
    ModifiedBy nvarchar(450) NULL,
    CONSTRAINT PK_RefreshTokenSessions PRIMARY KEY (Id)
);

CREATE UNIQUE INDEX IX_RefreshTokenSessions_TokenHash ON dbo.RefreshTokenSessions (TokenHash);
CREATE INDEX IX_RefreshTokenSessions_UserId_ExpiresAtUtc_RevokedAtUtc ON dbo.RefreshTokenSessions (UserId, ExpiresAtUtc, RevokedAtUtc);
CREATE INDEX IX_RefreshTokenSessions_AccessTokenSessionId ON dbo.RefreshTokenSessions (AccessTokenSessionId);
CREATE INDEX IX_RefreshTokenSessions_PreviousTokenSessionId ON dbo.RefreshTokenSessions (PreviousTokenSessionId);
CREATE INDEX IX_RefreshTokenSessions_Active ON dbo.RefreshTokenSessions (UserId, ExpiresAtUtc) WHERE RevokedAtUtc IS NULL;

CREATE TABLE dbo.MfaChallenges (
    Id uniqueidentifier NOT NULL,
    UserId uniqueidentifier NOT NULL,
    Purpose nvarchar(30) NOT NULL,
    ContinuationToken nvarchar(100) NOT NULL,
    StepVersion int NOT NULL,
    Method nvarchar(30) NULL,
    Provider nvarchar(30) NULL,
    ProviderRequestId nvarchar(120) NULL,
    Channel nvarchar(30) NULL,
    ContactValue nvarchar(320) NULL,
    Status nvarchar(30) NOT NULL,
    FailedAttempts int NOT NULL CONSTRAINT DF_MfaChallenges_FailedAttempts DEFAULT (0),
    LastFailedAttemptAtUtc datetime2 NULL,
    ExpiresAtUtc datetime2 NOT NULL,
    VerifiedAtUtc datetime2 NULL,
    IpAddress nvarchar(100) NULL,
    UserAgent nvarchar(500) NULL,
    CreatedAtUtc datetime2 NOT NULL,
    CreatedBy nvarchar(450) NULL,
    ModifiedBy nvarchar(450) NULL,
    CONSTRAINT PK_MfaChallenges PRIMARY KEY (Id)
);

CREATE INDEX IX_MfaChallenges_UserId_Status_ExpiresAtUtc ON dbo.MfaChallenges (UserId, Status, ExpiresAtUtc);
CREATE INDEX IX_MfaChallenges_UserId_Purpose_Status_ExpiresAtUtc ON dbo.MfaChallenges (UserId, Purpose, Status, ExpiresAtUtc);
CREATE INDEX IX_MfaChallenges_ContinuationToken ON dbo.MfaChallenges (ContinuationToken);
CREATE INDEX IX_MfaChallenges_ProviderRequestId ON dbo.MfaChallenges (ProviderRequestId);
CREATE INDEX IX_MfaChallenges_Status_CreatedAtUtc ON dbo.MfaChallenges (Status, CreatedAtUtc);

CREATE TABLE dbo.MfaEnrollmentTokenSessions (
    Id uniqueidentifier NOT NULL,
    UserId uniqueidentifier NOT NULL,
    Status nvarchar(max) NOT NULL,
    ContinuationToken nvarchar(max) NOT NULL,
    StepVersion int NOT NULL,
    TokenJti nvarchar(max) NOT NULL,
    ChallengeId uniqueidentifier NULL,
    ExpiresAtUtc datetime2 NOT NULL,
    CompletedAtUtc datetime2 NULL,
    CreatedAtUtc datetime2 NOT NULL,
    ModifiedAtUtc datetime2 NOT NULL,
    CreatedBy nvarchar(450) NULL,
    ModifiedBy nvarchar(450) NULL,
    CONSTRAINT PK_MfaEnrollmentTokenSessions PRIMARY KEY (Id)
);

CREATE INDEX IX_MfaEnrollmentTokenSessions_UserId_Status_ExpiresAtUtc ON dbo.MfaEnrollmentTokenSessions (UserId, Status, ExpiresAtUtc);
CREATE INDEX IX_MfaEnrollmentTokenSessions_ContinuationToken ON dbo.MfaEnrollmentTokenSessions (ContinuationToken);
CREATE INDEX IX_MfaEnrollmentTokenSessions_ChallengeId ON dbo.MfaEnrollmentTokenSessions (ChallengeId);

CREATE TABLE dbo.MfaManagementSessions (
    Id uniqueidentifier NOT NULL,
    UserId uniqueidentifier NOT NULL,
    Status nvarchar(40) NOT NULL,
    ContinuationToken nvarchar(100) NOT NULL,
    StepVersion int NOT NULL,
    ChallengeId uniqueidentifier NULL,
    ExpiresAtUtc datetime2 NOT NULL,
    VerifiedAtUtc datetime2 NULL,
    CreatedAtUtc datetime2 NOT NULL,
    ModifiedAtUtc datetime2 NOT NULL,
    CreatedBy nvarchar(450) NULL,
    ModifiedBy nvarchar(450) NULL,
    CONSTRAINT PK_MfaManagementSessions PRIMARY KEY (Id)
);

CREATE INDEX IX_MfaManagementSessions_UserId_Status_ExpiresAtUtc ON dbo.MfaManagementSessions (UserId, Status, ExpiresAtUtc);
CREATE INDEX IX_MfaManagementSessions_ContinuationToken ON dbo.MfaManagementSessions (ContinuationToken);
CREATE INDEX IX_MfaManagementSessions_ChallengeId ON dbo.MfaManagementSessions (ChallengeId);

CREATE TABLE dbo.MfaTokenSessions (
    Id uniqueidentifier NOT NULL,
    UserId uniqueidentifier NOT NULL,
    SessionType nvarchar(40) NOT NULL,
    TokenJti nvarchar(100) NOT NULL,
    ExpiresAtUtc datetime2 NOT NULL,
    CreatedAtUtc datetime2 NOT NULL,
    ModifiedAtUtc datetime2 NOT NULL,
    CreatedBy nvarchar(450) NULL,
    ModifiedBy nvarchar(450) NULL,
    Status nvarchar(40) NULL,
    ContinuationToken nvarchar(100) NULL,
    StepVersion int NULL,
    ChallengeId uniqueidentifier NULL,
    CompletedAtUtc datetime2 NULL,
    MfaTransactionId uniqueidentifier NULL,
    IssuedAtUtc datetime2 NULL,
    ConsumedAtUtc datetime2 NULL,
    RevokedAtUtc datetime2 NULL,
    IpAddress nvarchar(100) NULL,
    UserAgent nvarchar(500) NULL,
    CONSTRAINT PK_MfaTokenSessions PRIMARY KEY (Id),
    CONSTRAINT CK_MfaTokenSessions_SessionType CHECK ([SessionType] IN ('TempToken'))
);

CREATE UNIQUE INDEX IX_MfaTokenSessions_TokenJti ON dbo.MfaTokenSessions (TokenJti);
CREATE INDEX IX_MfaTokenSessions_UserId_SessionType_ExpiresAtUtc ON dbo.MfaTokenSessions (UserId, SessionType, ExpiresAtUtc);
CREATE INDEX IX_MfaTokenSessions_SessionType_Status_ExpiresAtUtc ON dbo.MfaTokenSessions (SessionType, Status, ExpiresAtUtc);
CREATE INDEX IX_MfaTokenSessions_ContinuationToken ON dbo.MfaTokenSessions (ContinuationToken);
CREATE INDEX IX_MfaTokenSessions_ChallengeId ON dbo.MfaTokenSessions (ChallengeId);
CREATE INDEX IX_MfaTokenSessions_MfaTransactionId ON dbo.MfaTokenSessions (MfaTransactionId);

ALTER TABLE dbo.UserMfaMethods
    ADD CONSTRAINT FK_UserMfaMethods_Users_UserId
    FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION;

ALTER TABLE dbo.AccessTokenSessions
    ADD CONSTRAINT FK_AccessTokenSessions_Users_UserId
    FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION;

ALTER TABLE dbo.RefreshTokenSessions
    ADD CONSTRAINT FK_RefreshTokenSessions_Users_UserId
    FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION;

ALTER TABLE dbo.RefreshTokenSessions
    ADD CONSTRAINT FK_RefreshTokenSessions_AccessTokenSessions_AccessTokenSessionId
    FOREIGN KEY (AccessTokenSessionId) REFERENCES dbo.AccessTokenSessions (Id) ON DELETE NO ACTION;

ALTER TABLE dbo.RefreshTokenSessions
    ADD CONSTRAINT FK_RefreshTokenSessions_RefreshTokenSessions_PreviousTokenSessionId
    FOREIGN KEY (PreviousTokenSessionId) REFERENCES dbo.RefreshTokenSessions (Id) ON DELETE NO ACTION;

ALTER TABLE dbo.MfaChallenges
    ADD CONSTRAINT FK_MfaChallenges_Users_UserId
    FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION;

ALTER TABLE dbo.MfaEnrollmentTokenSessions
    ADD CONSTRAINT FK_MfaEnrollmentTokenSessions_Users_UserId
    FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION;

ALTER TABLE dbo.MfaManagementSessions
    ADD CONSTRAINT FK_MfaManagementSessions_Users_UserId
    FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION;

ALTER TABLE dbo.MfaTokenSessions
    ADD CONSTRAINT FK_MfaTokenSessions_Users_UserId
    FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION;

COMMIT;
