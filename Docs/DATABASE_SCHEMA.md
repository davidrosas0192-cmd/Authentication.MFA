# Database Schema Reference

This document describes the current SQL Server schema used by the Authentication Fido2 REST API.

## Scope

The schema covers:

- user accounts
- MFA method registry and MFA challenges
- login-time MFA enrollment bootstrap sessions
- MFA management step-up sessions
- FIDO2 credentials and transactions
- access token and MFA temp token session tracking
- recovery code batches and one-time recovery codes
- authentication and security audit events

## Database Diagram

The diagram below shows the logical model used by the API. In the current implementation, only `UserFido2Credentials` has an enforced foreign key to `Users`; the other relationships are modeled by application logic and indexes.

```mermaid
erDiagram
    Users ||--o{ UserFido2Credentials : has
    Users ||--o{ UserMfaMethods : has
    Users ||--o{ MfaChallenges : has
    Users ||--o{ MfaLoginEnrollmentSessions : has
    Users ||--o{ MfaManagementSessions : has
    Users ||--o{ Fido2Transactions : has
    Users ||--o{ AccessTokenSessions : has
    Users ||--o{ MfaTempTokenSessions : has
    Users ||--o{ UserRecoveryCodeBatches : has
    UserRecoveryCodeBatches ||--o{ UserRecoveryCodes : has
    Users ||--o{ AuthenticationAuditEvents : has
    Users ||--o{ SecurityAuditEvents : has

    Users {
        bigint Id PK
        string Username
        string Email
        string PasswordHash
        bool IsActive
        bool IsFido2MfaEnabled
        datetime2 CreatedAtUtc
        datetime2 LastLoginAtUtc
    }

    UserFido2Credentials {
        bigint Id PK
        bigint UserId FK
        varbinary CredentialId
        varbinary PublicKey
        varbinary UserHandle
        bigint SignatureCounter
        string AaGuid
        string CredType
        datetime2 CreatedAtUtc
        datetime2 LastUsedAtUtc
    }

    UserMfaMethods {
        bigint Id PK
        bigint UserId FK
        string Method
        bool IsEnabled
        bool IsPrimary
        bool IsVerified
        string ContactValue
        datetime2 CreatedAtUtc
        datetime2 UpdatedAtUtc
    }

    MfaChallenges {
        uniqueidentifier Id PK
        bigint UserId FK
        string Purpose
        string ContinuationToken
        int StepVersion
        string Method
        string Provider
        string ProviderRequestId
        string Channel
        string ContactValue
        string Status
        datetime2 ExpiresAtUtc
        datetime2 VerifiedAtUtc
        string IpAddress
        string UserAgent
        datetime2 CreatedAtUtc
    }

    MfaLoginEnrollmentSessions {
        uniqueidentifier Id PK
        bigint UserId FK
        string Status
        string ContinuationToken
        int StepVersion
        string TokenJti
        uniqueidentifier ChallengeId
        datetime2 ExpiresAtUtc
        datetime2 CompletedAtUtc
        datetime2 CreatedAtUtc
        datetime2 UpdatedAtUtc
    }

    MfaManagementSessions {
        uniqueidentifier Id PK
        bigint UserId FK
        string Status
        string ContinuationToken
        int StepVersion
        uniqueidentifier ChallengeId
        datetime2 ExpiresAtUtc
        datetime2 VerifiedAtUtc
        datetime2 CreatedAtUtc
        datetime2 UpdatedAtUtc
    }

    Fido2Transactions {
        uniqueidentifier Id PK
        bigint UserId FK
        string Type
        string OptionsJson
        bool IsUsed
        string IpAddress
        string UserAgent
        datetime2 CreatedAtUtc
        datetime2 ExpiresAtUtc
        uniqueidentifier ParentMfaTransactionId
    }

    AccessTokenSessions {
        uniqueidentifier Id PK
        bigint UserId FK
        string TokenJti
        datetime2 IssuedAtUtc
        datetime2 ExpiresAtUtc
        datetime2 RevokedAtUtc
        string RevokeReason
        string IpAddress
        string UserAgent
    }

    MfaTempTokenSessions {
        uniqueidentifier Id PK
        bigint UserId FK
        uniqueidentifier MfaTransactionId
        string TokenJti
        datetime2 IssuedAtUtc
        datetime2 ExpiresAtUtc
        datetime2 ConsumedAtUtc
        datetime2 RevokedAtUtc
        string IpAddress
        string UserAgent
    }

    UserRecoveryCodeBatches {
        uniqueidentifier Id PK
        bigint UserId FK
        datetime2 IssuedAtUtc
        datetime2 ReplacedAtUtc
    }

    UserRecoveryCodes {
        uniqueidentifier Id PK
        uniqueidentifier BatchId FK
        bigint UserId FK
        string CodeHash
        datetime2 CreatedAtUtc
        datetime2 UsedAtUtc
    }

    AuthenticationAuditEvents {
        bigint Id PK
        datetime2 OccurredAtUtc
        bigint UserId FK
        string UsernameOrEmail
        string Stage
        string Method
        string Outcome
        string FailureReason
        string IpAddress
        string UserAgent
        string CorrelationId
    }

    SecurityAuditEvents {
        bigint Id PK
        datetime2 OccurredAtUtc
        bigint UserId FK
        string UsernameOrEmail
        string Category
        string EventType
        string Severity
        string Outcome
        string FailureReason
        string IpAddress
        string UserAgent
        string CorrelationId
        string RequestPath
        string HttpMethod
        string DetailsJson
    }
```

## Table Summary

### Users

Primary user table.

- `Username` and `Email` are unique.
- `PasswordHash` stores the authentication secret for login.
- `IsFido2MfaEnabled` remains for backward-compatible transition support.

### UserFido2Credentials

Stores WebAuthn/FIDO2 credentials.

- `CredentialId` is unique.
- `UserId` points to the owning user.

### UserMfaMethods

Stores enabled MFA methods per user.

- `Method` values: `sms`, `email`, `fido2`.
- Unique constraint on `(UserId, Method)`.

### MfaChallenges

Tracks MFA challenge lifecycle for login and enrollment.

- `Purpose` identifies the flow, such as login or enrollment.
- `ContinuationToken` and `StepVersion` enforce anti-replay and ordered flow progression.
- `ProviderRequestId` stores the external provider reference when present.
- Indexed by user, purpose, status, and expiration for fast validation.

### MfaManagementSessions

Tracks dedicated step-up sessions used before sensitive MFA administration operations.

- Stores session state separately from login/enrollment challenges.
- Uses `ContinuationToken` and `StepVersion` for step progression and replay resistance.
- `ChallengeId` links the current step-up challenge when one exists.

### MfaLoginEnrollmentSessions

Tracks dedicated bootstrap sessions used when login cannot complete until the user enrolls at least one MFA factor.

- Stores a separate least-privilege transitional session before full authentication is granted.
- Uses `ContinuationToken` and `StepVersion` for replay resistance and ordered progression.
- `TokenJti` binds the bootstrap JWT to a server-side session record.
- `CompletedAtUtc` marks explicit bootstrap completion before full access-token issuance.

### Fido2Transactions

Stores FIDO2 registration and assertion transactions.

- `Type` values are `registration` or `assertion`.
- `ParentMfaTransactionId` links FIDO2 login work to the parent MFA transaction.

### AccessTokenSessions

Tracks access-token JWT sessions by `jti` for revocation and replay protection.

### MfaTempTokenSessions

Tracks short-lived MFA JWT sessions by `jti`.

- One row is created per MFA login transaction.
- The row is consumed or revoked when the MFA step completes.

### UserRecoveryCodeBatches

Tracks issuance and rotation of recovery code sets.

- One batch groups a set of codes issued together.
- `ReplacedAtUtc` indicates the batch has been superseded.

### UserRecoveryCodes

Stores one-time recovery codes.

- Codes are stored hashed only (`CodeHash`).
- `UsedAtUtc` indicates one-time consumption.

### AuthenticationAuditEvents

Audit trail for authentication-specific events.

### SecurityAuditEvents

Audit trail for broader security-relevant events.

## SQL Script

The canonical schema script is stored in [DATABASE_SCHEMA.sql](./DATABASE_SCHEMA.sql).

## Notes

- The current implementation uses SQL Server and EF Core migrations as the source of truth.
- This document reflects the implemented schema, including token-session tables, login enrollment bootstrap sessions, MFA management sessions, recovery code tables, and OWASP audit tables.
- The SQL script is a recreate-style reference script for local documentation and review.
- The seed row shown in the SQL script matches the current development seed used by the project.
- If you add a new table or column, update the migration history and regenerate this document.