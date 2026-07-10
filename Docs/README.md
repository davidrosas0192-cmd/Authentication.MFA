# Authentication Fido2 API

This project is an ASP.NET Core Web API that provides authentication using traditional credentials plus passwordless FIDO2/WebAuthn multi-factor authentication.

## What this service does

- Authenticates users with username/email and password
- Supports FIDO2 enrollment for passkey-based MFA
- Supports FIDO2 login assertions for passwordless MFA
- Issues JWT access and refresh tokens
- Stores user, credential, and transaction data in SQL Server using Entity Framework Core
- Stores OWASP-aligned security audit events for authentication flows
- Writes infrastructure logs to a dedicated secondary SQL Server logging database

## Tech stack

- .NET 10
- ASP.NET Core Web API
- Entity Framework Core
- SQL Server
- FIDO2/WebAuthn via the Fido2 library
- Swagger/OpenAPI

## Project structure

- Controllers: exposes REST endpoints for auth and FIDO2 flows
- Services: contains business logic for authentication and MFA operations
- Repositories: wraps persistence logic for users, credentials, and transactions
- Data: EF Core DbContext and entity configurations
- DTOs: request and response contracts for APIs
- Entities: domain models such as users and FIDO2 artifacts

## Getting started

### Prerequisites

- .NET 10 SDK
- SQL Server instance (local container or installed instance)

### Configuration

Update the connection string and secrets in appsettings.json or environment-specific settings:

- Connection string: DefaultConnection
- Logging connection string: LoggingConnection
- JWT settings: Jwt
- MFA JWT settings: MfaJwt
- FIDO2 settings: Fido2

### Run locally

```bash
dotnet restore
dotnet ef database update
dotnet run
```

Notes:

- The command dotnet ef database update applies the main EF schema, including OWASP audit tables.
- On first startup, the API bootstraps the secondary logging database and ApplicationLogs table.

Then open the Swagger UI at:

- http://localhost:5000/swagger
- https://localhost:5001/swagger

## Result pattern

The API uses a shared Result pattern for service-layer operations. Each service method returns a Result or Result<T> that captures:

- success/failure state
- an optional message
- an optional error description
- an optional HTTP status code

Controllers translate those results into HTTP responses so callers receive consistent payloads.

Successful API responses are standardized with:

- success
- message
- data

Failure API responses are standardized with:

- success
- message

This response shaping is centralized in Common/Result.cs.

## Security audit logging

OWASP-aligned audit logging is implemented for authentication and FIDO2 flows.

- AuthenticationAuditEvents: authentication attempt telemetry
- SecurityAuditEvents: security event ledger with contextual metadata

Audit writes are centralized in Services/Implementatons/AuditService.cs and integrated into AuthService and Fido2MfaService.

See:

- OWASP_AUDIT_PLAN.md
- migration AddOwaspAuditTables

## Secondary logging database

Infrastructure logs from ILogger are written to a dedicated SQL Server database:

- Database: AuthenticationFido2Logs
- Table: dbo.ApplicationLogs

Environment behavior:

- Development: verbose logs
- Production: error logs only

Configuration is implemented in Extensions/LoggingExtensions.cs.

See LOGGING_DATABASE_MIGRATION.md for setup and verification.

## Repository hygiene

A root gitignore is configured to ignore build output directories (bin and obj).
If those files were previously tracked, they must be removed from index once with:

- git rm -r --cached bin obj

## Main API flows

### Login

1. Send a login request to the auth endpoint
2. If MFA is enabled, the API returns a response that requires FIDO2 verification
3. Complete the FIDO2 challenge with the browser or client SDK

### FIDO2 enrollment

1. Authenticate with JWT
2. Request enrollment options
3. Complete attestation with the authenticator
4. The credential is stored and MFA is enabled for the user

### FIDO2 login

1. Request login options using username or email
2. Complete assertion with the authenticator
3. Receive JWT tokens after successful verification

## Security notes

- Replace default secrets before deploying
- Use a production-grade secret store or environment variables
- Restrict FIDO2 origins in configuration
- Enable HTTPS in production

## Related documentation

- [Architecture](./ARCHITECTURE.md)
- [OWASP Audit Plan](./OWASP_AUDIT_PLAN.md)
- [Logging Database Migration](./LOGGING_DATABASE_MIGRATION.md)
- [Twilio MFA Implementation Plan](./TWILIO_MFA_IMPLEMENTATION_PLAN.md)
