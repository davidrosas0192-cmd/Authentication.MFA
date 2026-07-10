# Architecture

This document describes the high-level design of the Authentication Fido2 API.

## Overview

The API uses a layered architecture with controllers, services, repositories, and SQL Server persistence.

```mermaid
flowchart LR
    Client[Client / Browser] --> AuthController[AuthController]
    Client --> MfaController[MfaController]
    Client --> Fido2Controller[Fido2Controller]

    AuthController --> AuthService[AuthService]
    MfaController --> MfaService[MfaService]
    Fido2Controller --> Fido2MfaService[Fido2MfaService]

    AuthService --> TokenService[TokenService]
    MfaService --> TokenService
    MfaService --> TwilioService[TwilioOtpService]
    Fido2MfaService --> Fido2Lib[Fido2/WebAuthn Library]

    AuthService --> Repos[Repositories]
    MfaService --> Repos
    Fido2MfaService --> Repos
    Repos --> PrimaryDb[(Primary SQL Server DB)]

    AuthService --> AuditSvc[AuditService]
    MfaService --> AuditSvc
    Fido2MfaService --> AuditSvc
    AuditSvc --> PrimaryDb

    Controllers[Controllers] --> Serilog[ILogger/Serilog]
    Serilog --> LogDb[(Secondary SQL Logging DB)]
```

## Layers

## 1. Presentation layer

Controllers expose HTTP endpoints for:

- password login
- MFA methods and challenge orchestration
- SMS/Email enrollment
- FIDO2 enrollment
- FIDO2 login

Key files:

- Controllers/AuthController.cs
- Controllers/MfaController.cs
- Controllers/Fido2Controller.cs

## 2. Application layer

Services implement business workflows:

- AuthService: password validation and MFA-gated login decisions
- MfaService: MFA method resolution, OTP challenge start/verify, enrollment start/verify
- Fido2MfaService: WebAuthn registration and assertion workflows
- TokenService: full access token, refresh token, and temporary MFA token issuance
- TwilioOtpService: Twilio Verify integration for sms/email OTP

Key files:

- Services/Implementatons/AuthService.cs
- Services/Implementatons/MfaService.cs
- Services/Implementatons/Fido2MfaService.cs
- Services/Implementatons/TokenService.cs
- Services/Implementatons/TwilioOtpService.cs

## 3. Data access layer

Repositories abstract EF Core access for users, credentials, transactions, methods, and challenges.

Key responsibilities:

- user lookup and updates
- FIDO2 credential persistence
- FIDO2 transaction persistence
- MFA method registry access
- MFA challenge lifecycle persistence

## 4. Persistence layer

EF Core maps entities and migrations to SQL Server.

Key files:

- Data/ApplicationDbContext.cs
- Data/Configurations/*.cs
- Migrations/

## 5. Observability and audit layer

Two logging paths are implemented:

- Application diagnostics logging:
  - ILogger + Serilog sink to secondary SQL logging DB
  - Table: dbo.ApplicationLogs
  - Dev: verbose, Prod: errors only

- Security/authentication auditing:
  - Explicit audit records through AuditService
  - Tables: AuthenticationAuditEvents, SecurityAuditEvents

## Domain entities

- User: account identity and status
- UserFido2Credential: stored WebAuthn credentials
- Fido2Transaction: FIDO2 challenge transactions
- UserMfaMethod: per-user MFA methods and verification state
- MfaChallenge: MFA login/enrollment challenge lifecycle
- AuthenticationAuditEvent / SecurityAuditEvent: audit trails

## Token model

- Full access token:
  - Used for authenticated API operations
  - Issued when MFA is not required or after MFA completes

- MFA token:
  - Temporary token issued only when login requires MFA
  - Bound to mfa transaction claim and validated on MFA challenge endpoints

## Authentication flows

## Standard login

1. Client calls /api/auth/login with username/password.
2. If no MFA methods are enabled, full access token is returned.
3. If MFA is required, response includes:
   - AllowedMfaMethods
   - MfaTransactionId
   - MfaToken
4. Client completes MFA to receive full access token.

## SMS/Email enrollment

1. Authenticated user starts enrollment (/api/mfa/enrollment/start).
2. Service creates enrollment challenge and sends OTP via Twilio Verify.
3. User verifies OTP (/api/mfa/enrollment/verify).
4. UserMfaMethod is inserted/updated as enabled and verified.

## SMS/Email MFA login challenge

1. Client starts challenge with MFA token (/api/mfa/challenges/start).
2. Client verifies OTP with MFA token (/api/mfa/challenges/verify).
3. Service returns full access token on success.

## FIDO2 enrollment/login

FIDO2 flows remain available through Fido2Controller and Fido2MfaService.

## Configuration sections

- ConnectionStrings: DefaultConnection, LoggingConnection
- Jwt
- MfaJwt
- Fido2
- Twilio

## Design notes

- Controllers are thin and delegate to services.
- MFA method combinations are modeled in UserMfaMethods.
- MFA challenge state is explicit and auditable.
- Result wrapper provides a consistent API response envelope.
