SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DROP TABLE IF EXISTS dbo.RefreshTokenSessions;
DROP TABLE IF EXISTS dbo.MfaTempTokenSessions;
DROP TABLE IF EXISTS dbo.Fido2Transactions;
DROP TABLE IF EXISTS dbo.UserRecoveryCodes;
DROP TABLE IF EXISTS dbo.MfaLoginEnrollmentSessions;
DROP TABLE IF EXISTS dbo.MfaManagementSessions;
DROP TABLE IF EXISTS dbo.MfaSessions;
DROP TABLE IF EXISTS dbo.UserMfaMethods;
DROP TABLE IF EXISTS dbo.AccessTokenSessions;
DROP TABLE IF EXISTS dbo.MfaChallenges;
DROP TABLE IF EXISTS dbo.UserRecoveryCodeBatches;
DROP TABLE IF EXISTS dbo.UserFido2Credentials;
DROP TABLE IF EXISTS dbo.AuthenticationAuditEvents;
DROP TABLE IF EXISTS dbo.SecurityAuditEvents;
DROP TABLE IF EXISTS dbo.Users;

CREATE TABLE dbo.Users (
    Id bigint NOT NULL IDENTITY(1,1),
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

CREATE TABLE dbo.SecurityAuditEvents (
    Id bigint NOT NULL IDENTITY(1,1),
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
    UserId bigint NOT NULL,
    IssuedAtUtc datetime2 NOT NULL,
    ReplacedAtUtc datetime2 NULL,
    CreatedBy nvarchar(450) NULL,
    ModifiedBy nvarchar(450) NULL,
    CONSTRAINT PK_UserRecoveryCodeBatches PRIMARY KEY (Id)
);

CREATE TABLE dbo.UserRecoveryCodes (
    Id uniqueidentifier NOT NULL,
    BatchId uniqueidentifier NOT NULL,
    UserId bigint NOT NULL,
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

CREATE TABLE dbo.UserFido2Credentials (
    Id bigint NOT NULL IDENTITY(1,1),
    UserId bigint NOT NULL,
    CredentialId varbinary(900) NOT NULL,
    PublicKey varbinary(max) NOT NULL,
    UserHandle varbinary(max) NOT NULL,
    SignatureCounter bigint NOT NULL,
    AaGuid nvarchar(max) NULL,
    CredType nvarchar(max) NULL,
    CreatedAtUtc datetime2 NOT NULL,
    CreatedBy nvarchar(450) NULL,
    ModifiedBy nvarchar(450) NULL,
    LastUsedAtUtc datetime2 NULL,
    CONSTRAINT PK_UserFido2Credentials PRIMARY KEY (Id)
);

CREATE UNIQUE INDEX IX_UserFido2Credentials_CredentialId ON dbo.UserFido2Credentials (CredentialId);
CREATE INDEX IX_UserFido2Credentials_UserId ON dbo.UserFido2Credentials (UserId);

CREATE TABLE dbo.UserMfaMethods (
    Id bigint NOT NULL IDENTITY(1,1),
    UserId bigint NOT NULL,
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
    UserId bigint NOT NULL,
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
    UserId bigint NOT NULL,
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
    UserId bigint NOT NULL,
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

CREATE TABLE dbo.MfaTempTokenSessions (
    Id uniqueidentifier NOT NULL,
    UserId bigint NOT NULL,
    MfaTransactionId uniqueidentifier NOT NULL,
    TokenJti nvarchar(max) NOT NULL,
    IssuedAtUtc datetime2 NOT NULL,
    ExpiresAtUtc datetime2 NOT NULL,
    ConsumedAtUtc datetime2 NULL,
    RevokedAtUtc datetime2 NULL,
    IpAddress nvarchar(max) NULL,
    UserAgent nvarchar(max) NULL,
    CreatedBy nvarchar(450) NULL,
    ModifiedBy nvarchar(450) NULL,
    CONSTRAINT PK_MfaTempTokenSessions PRIMARY KEY (Id)
);

CREATE TABLE dbo.MfaLoginEnrollmentSessions (
    Id uniqueidentifier NOT NULL,
    UserId bigint NOT NULL,
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
    CONSTRAINT PK_MfaLoginEnrollmentSessions PRIMARY KEY (Id)
);

CREATE INDEX IX_MfaLoginEnrollmentSessions_UserId_Status_ExpiresAtUtc ON dbo.MfaLoginEnrollmentSessions (UserId, Status, ExpiresAtUtc);
CREATE INDEX IX_MfaLoginEnrollmentSessions_ContinuationToken ON dbo.MfaLoginEnrollmentSessions (ContinuationToken);
CREATE INDEX IX_MfaLoginEnrollmentSessions_ChallengeId ON dbo.MfaLoginEnrollmentSessions (ChallengeId);

CREATE TABLE dbo.MfaManagementSessions (
    Id uniqueidentifier NOT NULL,
    UserId bigint NOT NULL,
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

CREATE TABLE dbo.MfaSessions (
    Id uniqueidentifier NOT NULL,
    UserId bigint NOT NULL,
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
    CONSTRAINT PK_MfaSessions PRIMARY KEY (Id),
    CONSTRAINT CK_MfaSessions_SessionType CHECK ([SessionType] IN ('TempToken', 'LoginEnrollment'))
);

CREATE UNIQUE INDEX IX_MfaSessions_TokenJti ON dbo.MfaSessions (TokenJti);
CREATE INDEX IX_MfaSessions_UserId_SessionType_ExpiresAtUtc ON dbo.MfaSessions (UserId, SessionType, ExpiresAtUtc);
CREATE INDEX IX_MfaSessions_SessionType_Status_ExpiresAtUtc ON dbo.MfaSessions (SessionType, Status, ExpiresAtUtc);
CREATE INDEX IX_MfaSessions_ContinuationToken ON dbo.MfaSessions (ContinuationToken);
CREATE INDEX IX_MfaSessions_ChallengeId ON dbo.MfaSessions (ChallengeId);
CREATE INDEX IX_MfaSessions_MfaTransactionId ON dbo.MfaSessions (MfaTransactionId);

CREATE TABLE dbo.Fido2Transactions (
    Id uniqueidentifier NOT NULL,
    UserId bigint NOT NULL,
    Type nvarchar(50) NOT NULL,
    OptionsJson nvarchar(max) NOT NULL,
    IsUsed bit NOT NULL,
    IpAddress nvarchar(100) NOT NULL,
    UserAgent nvarchar(500) NOT NULL,
    CreatedAtUtc datetime2 NOT NULL,
    CreatedBy nvarchar(450) NULL,
    ModifiedBy nvarchar(450) NULL,
    ExpiresAtUtc datetime2 NOT NULL,
    ParentMfaTransactionId uniqueidentifier NULL,
    CONSTRAINT PK_Fido2Transactions PRIMARY KEY (Id)
);

CREATE INDEX IX_Fido2Transactions_UserId_Type_IsUsed ON dbo.Fido2Transactions (UserId, Type, IsUsed);

ALTER TABLE dbo.UserRecoveryCodeBatches
    ADD CONSTRAINT FK_UserRecoveryCodeBatches_Users_UserId
    FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION;

ALTER TABLE dbo.UserRecoveryCodes
    ADD CONSTRAINT FK_UserRecoveryCodes_UserRecoveryCodeBatches_BatchId
    FOREIGN KEY (BatchId) REFERENCES dbo.UserRecoveryCodeBatches (Id) ON DELETE NO ACTION;

ALTER TABLE dbo.UserRecoveryCodes
    ADD CONSTRAINT FK_UserRecoveryCodes_Users_UserId
    FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION;

ALTER TABLE dbo.UserFido2Credentials
    ADD CONSTRAINT FK_UserFido2Credentials_Users_UserId
    FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION;

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

ALTER TABLE dbo.MfaTempTokenSessions
    ADD CONSTRAINT FK_MfaTempTokenSessions_Users_UserId
    FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION;

ALTER TABLE dbo.MfaTempTokenSessions
    ADD CONSTRAINT FK_MfaTempTokenSessions_MfaChallenges_MfaTransactionId
    FOREIGN KEY (MfaTransactionId) REFERENCES dbo.MfaChallenges (Id) ON DELETE NO ACTION;

ALTER TABLE dbo.MfaLoginEnrollmentSessions
    ADD CONSTRAINT FK_MfaLoginEnrollmentSessions_Users_UserId
    FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION;

ALTER TABLE dbo.MfaManagementSessions
    ADD CONSTRAINT FK_MfaManagementSessions_Users_UserId
    FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION;

ALTER TABLE dbo.MfaSessions
    ADD CONSTRAINT FK_MfaSessions_Users_UserId
    FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION;

ALTER TABLE dbo.Fido2Transactions
    ADD CONSTRAINT FK_Fido2Transactions_Users_UserId
    FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION;

ALTER TABLE dbo.Fido2Transactions
    ADD CONSTRAINT FK_Fido2Transactions_MfaChallenges_ParentMfaTransactionId
    FOREIGN KEY (ParentMfaTransactionId) REFERENCES dbo.MfaChallenges (Id) ON DELETE NO ACTION;

COMMIT;
