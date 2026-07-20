SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Requires 01_Users.sql and 02_Mfa.sql to be applied first.

BEGIN TRANSACTION;

DROP TABLE IF EXISTS dbo.Fido2Transactions;
DROP TABLE IF EXISTS dbo.UserFido2Credentials;
CREATE TABLE dbo.UserFido2Credentials (
    Id bigint NOT NULL IDENTITY(1,1),
    UserId uniqueidentifier NOT NULL,
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

CREATE TABLE dbo.Fido2Transactions (
    Id uniqueidentifier NOT NULL,
    UserId uniqueidentifier NOT NULL,
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

ALTER TABLE dbo.UserFido2Credentials
    ADD CONSTRAINT FK_UserFido2Credentials_Users_UserId
    FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION;

ALTER TABLE dbo.Fido2Transactions
    ADD CONSTRAINT FK_Fido2Transactions_Users_UserId
    FOREIGN KEY (UserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION;

ALTER TABLE dbo.Fido2Transactions
    ADD CONSTRAINT FK_Fido2Transactions_MfaChallenges_ParentMfaTransactionId
    FOREIGN KEY (ParentMfaTransactionId) REFERENCES dbo.MfaChallenges (Id) ON DELETE NO ACTION;

COMMIT;
