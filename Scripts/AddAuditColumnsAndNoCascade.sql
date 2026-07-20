IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [Users] (
    [Id] bigint NOT NULL IDENTITY,
    [Username] nvarchar(100) NOT NULL,
    [Email] nvarchar(255) NOT NULL,
    [PasswordHash] nvarchar(max) NOT NULL,
    [IsActive] bit NOT NULL,
    [IsFido2MfaEnabled] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [LastLoginAtUtc] datetime2 NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
);

CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);

CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260628025521_initialCreate', N'10.0.9');

COMMIT;
GO

BEGIN TRANSACTION;
INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260628025958_seedInitailUser', N'10.0.9');

COMMIT;
GO

BEGIN TRANSACTION;
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAtUtc', N'Email', N'IsActive', N'IsFido2MfaEnabled', N'LastLoginAtUtc', N'PasswordHash', N'Username') AND [object_id] = OBJECT_ID(N'[Users]'))
    SET IDENTITY_INSERT [Users] ON;
INSERT INTO [Users] ([Id], [CreatedAtUtc], [Email], [IsActive], [IsFido2MfaEnabled], [LastLoginAtUtc], [PasswordHash], [Username])
VALUES (CAST(1 AS bigint), '2026-06-28T00:00:00.0000000Z', N'davidrosas0192@gmail.com', CAST(1 AS bit), CAST(0 AS bit), NULL, N'Rdavid58@', N'cruzrx2');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAtUtc', N'Email', N'IsActive', N'IsFido2MfaEnabled', N'LastLoginAtUtc', N'PasswordHash', N'Username') AND [object_id] = OBJECT_ID(N'[Users]'))
    SET IDENTITY_INSERT [Users] OFF;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260628030850_initialSeed2', N'10.0.9');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [UserFido2Credentials] (
    [Id] bigint NOT NULL IDENTITY,
    [UserId] bigint NOT NULL,
    [CredentialId] varbinary(900) NOT NULL,
    [PublicKey] varbinary(max) NOT NULL,
    [UserHandle] varbinary(max) NOT NULL,
    [SignatureCounter] bigint NOT NULL,
    [AaGuid] nvarchar(max) NULL,
    [CredType] nvarchar(max) NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [LastUsedAtUtc] datetime2 NULL,
    CONSTRAINT [PK_UserFido2Credentials] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UserFido2Credentials_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE UNIQUE INDEX [IX_UserFido2Credentials_CredentialId] ON [UserFido2Credentials] ([CredentialId]);

CREATE INDEX [IX_UserFido2Credentials_UserId] ON [UserFido2Credentials] ([UserId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260628064059_CreateUserCredentialFido2', N'10.0.9');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [Fido2Transactions] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] bigint NOT NULL,
    [Type] nvarchar(50) NOT NULL,
    [OptionsJson] nvarchar(max) NOT NULL,
    [IsUsed] bit NOT NULL,
    [IpAddress] nvarchar(100) NOT NULL,
    [UserAgent] nvarchar(500) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [ExpiresAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Fido2Transactions] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_Fido2Transactions_UserId_Type_IsUsed] ON [Fido2Transactions] ([UserId], [Type], [IsUsed]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260628065014_AddFid2Transactions', N'10.0.9');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [AuthenticationAuditEvents] (
    [Id] bigint NOT NULL IDENTITY,
    [OccurredAtUtc] datetime2 NOT NULL,
    [UserId] bigint NULL,
    [UsernameOrEmail] nvarchar(320) NULL,
    [Stage] nvarchar(60) NOT NULL,
    [Method] nvarchar(30) NOT NULL,
    [Outcome] nvarchar(20) NOT NULL,
    [FailureReason] nvarchar(400) NULL,
    [IpAddress] nvarchar(100) NULL,
    [UserAgent] nvarchar(500) NULL,
    [CorrelationId] nvarchar(100) NULL,
    CONSTRAINT [PK_AuthenticationAuditEvents] PRIMARY KEY ([Id])
);

CREATE TABLE [SecurityAuditEvents] (
    [Id] bigint NOT NULL IDENTITY,
    [OccurredAtUtc] datetime2 NOT NULL,
    [Category] nvarchar(60) NOT NULL,
    [EventType] nvarchar(120) NOT NULL,
    [Severity] nvarchar(30) NOT NULL,
    [Outcome] nvarchar(20) NOT NULL,
    [UserId] bigint NULL,
    [UsernameOrEmail] nvarchar(320) NULL,
    [IpAddress] nvarchar(100) NULL,
    [UserAgent] nvarchar(500) NULL,
    [CorrelationId] nvarchar(100) NULL,
    [RequestPath] nvarchar(300) NULL,
    [HttpMethod] nvarchar(10) NULL,
    [FailureReason] nvarchar(400) NULL,
    [DetailsJson] nvarchar(max) NULL,
    CONSTRAINT [PK_SecurityAuditEvents] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_AuthenticationAuditEvents_IpAddress_OccurredAtUtc] ON [AuthenticationAuditEvents] ([IpAddress], [OccurredAtUtc]);

CREATE INDEX [IX_AuthenticationAuditEvents_OccurredAtUtc] ON [AuthenticationAuditEvents] ([OccurredAtUtc]);

CREATE INDEX [IX_AuthenticationAuditEvents_Outcome_OccurredAtUtc] ON [AuthenticationAuditEvents] ([Outcome], [OccurredAtUtc]);

CREATE INDEX [IX_AuthenticationAuditEvents_UserId_OccurredAtUtc] ON [AuthenticationAuditEvents] ([UserId], [OccurredAtUtc]);

CREATE INDEX [IX_AuthenticationAuditEvents_UsernameOrEmail_OccurredAtUtc] ON [AuthenticationAuditEvents] ([UsernameOrEmail], [OccurredAtUtc]);

CREATE INDEX [IX_SecurityAuditEvents_Category_OccurredAtUtc] ON [SecurityAuditEvents] ([Category], [OccurredAtUtc]);

CREATE INDEX [IX_SecurityAuditEvents_IpAddress_OccurredAtUtc] ON [SecurityAuditEvents] ([IpAddress], [OccurredAtUtc]);

CREATE INDEX [IX_SecurityAuditEvents_OccurredAtUtc] ON [SecurityAuditEvents] ([OccurredAtUtc]);

CREATE INDEX [IX_SecurityAuditEvents_Outcome_OccurredAtUtc] ON [SecurityAuditEvents] ([Outcome], [OccurredAtUtc]);

CREATE INDEX [IX_SecurityAuditEvents_UserId_OccurredAtUtc] ON [SecurityAuditEvents] ([UserId], [OccurredAtUtc]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260710011815_AddOwaspAuditTables', N'10.0.9');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [MfaChallenges] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] bigint NOT NULL,
    [Method] nvarchar(30) NULL,
    [Provider] nvarchar(30) NULL,
    [ProviderRequestId] nvarchar(120) NULL,
    [Channel] nvarchar(30) NULL,
    [Status] nvarchar(30) NOT NULL,
    [ExpiresAtUtc] datetime2 NOT NULL,
    [VerifiedAtUtc] datetime2 NULL,
    [IpAddress] nvarchar(100) NULL,
    [UserAgent] nvarchar(500) NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_MfaChallenges] PRIMARY KEY ([Id])
);

CREATE TABLE [UserMfaMethods] (
    [Id] bigint NOT NULL IDENTITY,
    [UserId] bigint NOT NULL,
    [Method] nvarchar(30) NOT NULL,
    [IsEnabled] bit NOT NULL,
    [IsPrimary] bit NOT NULL,
    [IsVerified] bit NOT NULL,
    [ContactValue] nvarchar(320) NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_UserMfaMethods] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_MfaChallenges_ProviderRequestId] ON [MfaChallenges] ([ProviderRequestId]);

CREATE INDEX [IX_MfaChallenges_UserId_Status_ExpiresAtUtc] ON [MfaChallenges] ([UserId], [Status], [ExpiresAtUtc]);

CREATE INDEX [IX_UserMfaMethods_UserId_IsEnabled] ON [UserMfaMethods] ([UserId], [IsEnabled]);

CREATE UNIQUE INDEX [IX_UserMfaMethods_UserId_Method] ON [UserMfaMethods] ([UserId], [Method]);

INSERT INTO UserMfaMethods (UserId, Method, IsEnabled, IsPrimary, IsVerified, ContactValue, CreatedAtUtc, UpdatedAtUtc)
SELECT u.Id, 'fido2', 1, 1, 1, NULL, SYSUTCDATETIME(), SYSUTCDATETIME()
FROM Users u
WHERE u.IsFido2MfaEnabled = 1
    AND NOT EXISTS (
            SELECT 1
            FROM UserMfaMethods m
            WHERE m.UserId = u.Id
                AND m.Method = 'fido2'
    );

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260710014547_AddTwilioMfaMethodsAndChallenges', N'10.0.9');

COMMIT;
GO

BEGIN TRANSACTION;
IF EXISTS (SELECT 1 FROM Users WHERE Id = 1)
BEGIN
    IF NOT EXISTS (SELECT 1 FROM UserMfaMethods WHERE UserId = 1 AND Method = 'email')
    BEGIN
        INSERT INTO UserMfaMethods
            (UserId, Method, IsEnabled, IsPrimary, IsVerified, ContactValue, CreatedAtUtc, UpdatedAtUtc)
        VALUES
            (1, 'email', 1, 0, 1, 'davidrosas0192@gmail.com', SYSUTCDATETIME(), SYSUTCDATETIME());
    END;

    IF NOT EXISTS (SELECT 1 FROM UserMfaMethods WHERE UserId = 1 AND Method = 'sms')
    BEGIN
        INSERT INTO UserMfaMethods
            (UserId, Method, IsEnabled, IsPrimary, IsVerified, ContactValue, CreatedAtUtc, UpdatedAtUtc)
        VALUES
            (1, 'sms', 1, 0, 0, '+15555550100', SYSUTCDATETIME(), SYSUTCDATETIME());
    END;

    IF NOT EXISTS (SELECT 1 FROM UserMfaMethods WHERE UserId = 1 AND Method = 'fido2')
    BEGIN
        INSERT INTO UserMfaMethods
            (UserId, Method, IsEnabled, IsPrimary, IsVerified, ContactValue, CreatedAtUtc, UpdatedAtUtc)
        VALUES
            (1, 'fido2', 1, 1, 1, NULL, SYSUTCDATETIME(), SYSUTCDATETIME());
    END;
END;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260710014906_SeedDefaultUserMfaMethods', N'10.0.9');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [MfaChallenges] ADD [ContactValue] nvarchar(320) NULL;

ALTER TABLE [MfaChallenges] ADD [Purpose] nvarchar(30) NOT NULL DEFAULT N'';

CREATE INDEX [IX_MfaChallenges_UserId_Purpose_Status_ExpiresAtUtc] ON [MfaChallenges] ([UserId], [Purpose], [Status], [ExpiresAtUtc]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260710015218_AddMfaEnrollmentChallengeFields', N'10.0.9');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [Fido2Transactions] ADD [ParentMfaTransactionId] uniqueidentifier NULL;

CREATE TABLE [MfaTempTokenSessions] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] bigint NOT NULL,
    [MfaTransactionId] uniqueidentifier NOT NULL,
    [TokenJti] nvarchar(100) NOT NULL,
    [IssuedAtUtc] datetime2 NOT NULL,
    [ExpiresAtUtc] datetime2 NOT NULL,
    [ConsumedAtUtc] datetime2 NULL,
    [RevokedAtUtc] datetime2 NULL,
    [IpAddress] nvarchar(100) NULL,
    [UserAgent] nvarchar(500) NULL,
    CONSTRAINT [PK_MfaTempTokenSessions] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_Fido2Transactions_ParentMfaTransactionId] ON [Fido2Transactions] ([ParentMfaTransactionId]);

CREATE INDEX [IX_MfaTempTokenSessions_MfaTransactionId] ON [MfaTempTokenSessions] ([MfaTransactionId]);

CREATE UNIQUE INDEX [IX_MfaTempTokenSessions_TokenJti] ON [MfaTempTokenSessions] ([TokenJti]);

CREATE INDEX [IX_MfaTempTokenSessions_UserId_ExpiresAtUtc] ON [MfaTempTokenSessions] ([UserId], [ExpiresAtUtc]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260710042024_AddMfaTempTokenSessionsAndFido2ParentTx', N'10.0.9');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [AccessTokenSessions] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] bigint NOT NULL,
    [TokenJti] nvarchar(100) NOT NULL,
    [IssuedAtUtc] datetime2 NOT NULL,
    [ExpiresAtUtc] datetime2 NOT NULL,
    [RevokedAtUtc] datetime2 NULL,
    [RevokeReason] nvarchar(100) NULL,
    [IpAddress] nvarchar(100) NULL,
    [UserAgent] nvarchar(500) NULL,
    CONSTRAINT [PK_AccessTokenSessions] PRIMARY KEY ([Id])
);

CREATE UNIQUE INDEX [IX_AccessTokenSessions_TokenJti] ON [AccessTokenSessions] ([TokenJti]);

CREATE INDEX [IX_AccessTokenSessions_UserId_ExpiresAtUtc] ON [AccessTokenSessions] ([UserId], [ExpiresAtUtc]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260710050933_AddAccessTokenSessions', N'10.0.9');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [UserRecoveryCodeBatches] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] bigint NOT NULL,
    [IssuedAtUtc] datetime2 NOT NULL,
    [ReplacedAtUtc] datetime2 NULL,
    CONSTRAINT [PK_UserRecoveryCodeBatches] PRIMARY KEY ([Id])
);

CREATE TABLE [UserRecoveryCodes] (
    [Id] uniqueidentifier NOT NULL,
    [BatchId] uniqueidentifier NOT NULL,
    [UserId] bigint NOT NULL,
    [CodeHash] nvarchar(400) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UsedAtUtc] datetime2 NULL,
    CONSTRAINT [PK_UserRecoveryCodes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_UserRecoveryCodes_UserRecoveryCodeBatches_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [UserRecoveryCodeBatches] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_UserRecoveryCodeBatches_UserId_IssuedAtUtc] ON [UserRecoveryCodeBatches] ([UserId], [IssuedAtUtc]);

CREATE INDEX [IX_UserRecoveryCodeBatches_UserId_ReplacedAtUtc] ON [UserRecoveryCodeBatches] ([UserId], [ReplacedAtUtc]);

CREATE INDEX [IX_UserRecoveryCodes_BatchId] ON [UserRecoveryCodes] ([BatchId]);

CREATE INDEX [IX_UserRecoveryCodes_UserId_UsedAtUtc] ON [UserRecoveryCodes] ([UserId], [UsedAtUtc]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260710182817_AddRecoveryCodesAndMfaMethodManagement', N'10.0.9');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [MfaManagementSessions] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] bigint NOT NULL,
    [Status] nvarchar(40) NOT NULL,
    [ChallengeId] uniqueidentifier NULL,
    [ExpiresAtUtc] datetime2 NOT NULL,
    [VerifiedAtUtc] datetime2 NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_MfaManagementSessions] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_MfaManagementSessions_ChallengeId] ON [MfaManagementSessions] ([ChallengeId]);

CREATE INDEX [IX_MfaManagementSessions_UserId_Status_ExpiresAtUtc] ON [MfaManagementSessions] ([UserId], [Status], [ExpiresAtUtc]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260714005000_AddMfaManagementSessions', N'10.0.9');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [MfaManagementSessions] ADD [ContinuationToken] nvarchar(100) NOT NULL DEFAULT N'';

ALTER TABLE [MfaManagementSessions] ADD [StepVersion] int NOT NULL DEFAULT 0;

CREATE INDEX [IX_MfaManagementSessions_ContinuationToken] ON [MfaManagementSessions] ([ContinuationToken]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260714005246_AddManagementSessionContinuationToken', N'10.0.9');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [MfaChallenges] ADD [ContinuationToken] nvarchar(100) NOT NULL DEFAULT N'';

ALTER TABLE [MfaChallenges] ADD [StepVersion] int NOT NULL DEFAULT 0;

CREATE INDEX [IX_MfaChallenges_ContinuationToken] ON [MfaChallenges] ([ContinuationToken]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260714005816_AddMfaChallengeContinuationToken', N'10.0.9');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [MfaLoginEnrollmentSessions] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] bigint NOT NULL,
    [Status] nvarchar(40) NOT NULL,
    [ContinuationToken] nvarchar(100) NOT NULL,
    [StepVersion] int NOT NULL,
    [TokenJti] nvarchar(100) NOT NULL,
    [ChallengeId] uniqueidentifier NULL,
    [ExpiresAtUtc] datetime2 NOT NULL,
    [CompletedAtUtc] datetime2 NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_MfaLoginEnrollmentSessions] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_MfaLoginEnrollmentSessions_ChallengeId] ON [MfaLoginEnrollmentSessions] ([ChallengeId]);

CREATE INDEX [IX_MfaLoginEnrollmentSessions_ContinuationToken] ON [MfaLoginEnrollmentSessions] ([ContinuationToken]);

CREATE UNIQUE INDEX [IX_MfaLoginEnrollmentSessions_TokenJti] ON [MfaLoginEnrollmentSessions] ([TokenJti]);

CREATE INDEX [IX_MfaLoginEnrollmentSessions_UserId_Status_ExpiresAtUtc] ON [MfaLoginEnrollmentSessions] ([UserId], [Status], [ExpiresAtUtc]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260714193858_AddLoginEnrollmentSessions', N'10.0.9');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [MfaSessions] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] bigint NOT NULL,
    [SessionType] nvarchar(40) NOT NULL,
    [TokenJti] nvarchar(100) NOT NULL,
    [ExpiresAtUtc] datetime2 NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [Status] nvarchar(40) NULL,
    [ContinuationToken] nvarchar(100) NULL,
    [StepVersion] int NULL,
    [ChallengeId] uniqueidentifier NULL,
    [CompletedAtUtc] datetime2 NULL,
    [MfaTransactionId] uniqueidentifier NULL,
    [IssuedAtUtc] datetime2 NULL,
    [ConsumedAtUtc] datetime2 NULL,
    [RevokedAtUtc] datetime2 NULL,
    [IpAddress] nvarchar(100) NULL,
    [UserAgent] nvarchar(500) NULL,
    CONSTRAINT [PK_MfaSessions] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_MfaSessions_SessionType] CHECK ([SessionType] IN ('temp_token', 'login_enrollment'))
);

CREATE INDEX [IX_MfaSessions_ChallengeId] ON [MfaSessions] ([ChallengeId]);

CREATE INDEX [IX_MfaSessions_ContinuationToken] ON [MfaSessions] ([ContinuationToken]);

CREATE INDEX [IX_MfaSessions_MfaTransactionId] ON [MfaSessions] ([MfaTransactionId]);

CREATE INDEX [IX_MfaSessions_SessionType_Status_ExpiresAtUtc] ON [MfaSessions] ([SessionType], [Status], [ExpiresAtUtc]);

CREATE UNIQUE INDEX [IX_MfaSessions_TokenJti] ON [MfaSessions] ([TokenJti]);

CREATE INDEX [IX_MfaSessions_UserId_SessionType_ExpiresAtUtc] ON [MfaSessions] ([UserId], [SessionType], [ExpiresAtUtc]);


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


INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260715203148_ConsolidateMfaSessionTables', N'10.0.9');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [MfaChallenges] ADD [FailedAttempts] int NOT NULL DEFAULT 0;

ALTER TABLE [MfaChallenges] ADD [LastFailedAttemptAtUtc] datetime2 NULL;

CREATE TABLE [RefreshTokenSessions] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] bigint NOT NULL,
    [TokenHash] nvarchar(256) NOT NULL,
    [AccessTokenSessionId] uniqueidentifier NOT NULL,
    [IssuedAtUtc] datetime2 NOT NULL,
    [ExpiresAtUtc] datetime2 NOT NULL,
    [RevokedAtUtc] datetime2 NULL,
    [RevokeReason] nvarchar(100) NULL,
    [LastRotatedAtUtc] datetime2 NULL,
    [PreviousTokenSessionId] uniqueidentifier NULL,
    [IpAddress] nvarchar(100) NULL,
    [UserAgent] nvarchar(500) NULL,
    CONSTRAINT [PK_RefreshTokenSessions] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_RefreshTokenSessions_AccessTokenSessionId] ON [RefreshTokenSessions] ([AccessTokenSessionId]);

CREATE INDEX [IX_RefreshTokenSessions_PreviousTokenSessionId] ON [RefreshTokenSessions] ([PreviousTokenSessionId]);

CREATE UNIQUE INDEX [IX_RefreshTokenSessions_TokenHash] ON [RefreshTokenSessions] ([TokenHash]);

CREATE INDEX [IX_RefreshTokenSessions_UserId_ExpiresAtUtc_RevokedAtUtc] ON [RefreshTokenSessions] ([UserId], [ExpiresAtUtc], [RevokedAtUtc]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260716205914_updateSchema', N'10.0.9');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [MfaLoginEnrollmentSessions] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] bigint NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [ContinuationToken] nvarchar(max) NOT NULL,
    [StepVersion] int NOT NULL,
    [TokenJti] nvarchar(max) NOT NULL,
    [ChallengeId] uniqueidentifier NULL,
    [ExpiresAtUtc] datetime2 NOT NULL,
    [CompletedAtUtc] datetime2 NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_MfaLoginEnrollmentSessions] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_UserMfaMethods_Method_ContactValue_Active] ON [UserMfaMethods] ([Method], [ContactValue]) WHERE [IsEnabled] = 1;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260716225430_AddMfaMethodContactValueIndex', N'10.0.9');

COMMIT;
GO

BEGIN TRANSACTION;
DROP INDEX [IX_AccessTokenSessions_UserId_ExpiresAtUtc] ON [AccessTokenSessions];

CREATE INDEX [IX_SecurityAuditEvents_Severity_OccurredAtUtc] ON [SecurityAuditEvents] ([Severity], [OccurredAtUtc]);

CREATE INDEX [IX_RefreshTokenSessions_Active] ON [RefreshTokenSessions] ([UserId], [ExpiresAtUtc]) WHERE [RevokedAtUtc] IS NULL;

CREATE INDEX [IX_MfaChallenges_Status_CreatedAtUtc] ON [MfaChallenges] ([Status], [CreatedAtUtc]);

CREATE INDEX [IX_AuthenticationAuditEvents_Stage_OccurredAtUtc] ON [AuthenticationAuditEvents] ([Stage], [OccurredAtUtc]);

CREATE INDEX [IX_AccessTokenSessions_Active] ON [AccessTokenSessions] ([UserId], [ExpiresAtUtc]) WHERE [RevokedAtUtc] IS NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260716231259_AddPerformanceIndexes', N'10.0.9');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [UserFido2Credentials] DROP CONSTRAINT [FK_UserFido2Credentials_Users_UserId];

ALTER TABLE [UserRecoveryCodes] DROP CONSTRAINT [FK_UserRecoveryCodes_UserRecoveryCodeBatches_BatchId];

EXEC sp_rename N'[UserMfaMethods].[UpdatedAtUtc]', N'ModifiedAtUtc', 'COLUMN';

EXEC sp_rename N'[MfaSessions].[UpdatedAtUtc]', N'ModifiedAtUtc', 'COLUMN';

EXEC sp_rename N'[MfaManagementSessions].[UpdatedAtUtc]', N'ModifiedAtUtc', 'COLUMN';

EXEC sp_rename N'[MfaLoginEnrollmentSessions].[UpdatedAtUtc]', N'ModifiedAtUtc', 'COLUMN';

ALTER TABLE [Users] ADD [CreatedBy] nvarchar(max) NULL;

ALTER TABLE [Users] ADD [ModifiedBy] nvarchar(max) NULL;

ALTER TABLE [UserRecoveryCodes] ADD [CreatedBy] nvarchar(max) NULL;

ALTER TABLE [UserRecoveryCodes] ADD [ModifiedBy] nvarchar(max) NULL;

ALTER TABLE [UserRecoveryCodeBatches] ADD [CreatedBy] nvarchar(max) NULL;

ALTER TABLE [UserRecoveryCodeBatches] ADD [ModifiedBy] nvarchar(max) NULL;

ALTER TABLE [UserMfaMethods] ADD [CreatedBy] nvarchar(max) NULL;

ALTER TABLE [UserMfaMethods] ADD [ModifiedBy] nvarchar(max) NULL;

ALTER TABLE [UserFido2Credentials] ADD [CreatedBy] nvarchar(max) NULL;

ALTER TABLE [UserFido2Credentials] ADD [ModifiedBy] nvarchar(max) NULL;

ALTER TABLE [RefreshTokenSessions] ADD [CreatedBy] nvarchar(max) NULL;

ALTER TABLE [RefreshTokenSessions] ADD [ModifiedBy] nvarchar(max) NULL;

ALTER TABLE [MfaSessions] ADD [CreatedBy] nvarchar(max) NULL;

ALTER TABLE [MfaSessions] ADD [ModifiedBy] nvarchar(max) NULL;

ALTER TABLE [MfaManagementSessions] ADD [CreatedBy] nvarchar(max) NULL;

ALTER TABLE [MfaManagementSessions] ADD [ModifiedBy] nvarchar(max) NULL;

ALTER TABLE [MfaLoginEnrollmentSessions] ADD [CreatedBy] nvarchar(max) NULL;

ALTER TABLE [MfaLoginEnrollmentSessions] ADD [ModifiedBy] nvarchar(max) NULL;

ALTER TABLE [MfaChallenges] ADD [CreatedBy] nvarchar(max) NULL;

ALTER TABLE [MfaChallenges] ADD [ModifiedBy] nvarchar(max) NULL;

ALTER TABLE [Fido2Transactions] ADD [CreatedBy] nvarchar(max) NULL;

ALTER TABLE [Fido2Transactions] ADD [ModifiedBy] nvarchar(max) NULL;

ALTER TABLE [AccessTokenSessions] ADD [CreatedBy] nvarchar(max) NULL;

ALTER TABLE [AccessTokenSessions] ADD [ModifiedBy] nvarchar(max) NULL;

UPDATE [Users] SET [CreatedBy] = NULL, [ModifiedBy] = NULL
WHERE [Id] = CAST(1 AS bigint);
SELECT @@ROWCOUNT;


ALTER TABLE [UserFido2Credentials] ADD CONSTRAINT [FK_UserFido2Credentials_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]);

ALTER TABLE [UserRecoveryCodes] ADD CONSTRAINT [FK_UserRecoveryCodes_UserRecoveryCodeBatches_BatchId] FOREIGN KEY ([BatchId]) REFERENCES [UserRecoveryCodeBatches] ([Id]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260720200751_AddAuditColumnsAndNoCascade', N'10.0.9');

COMMIT;
GO

