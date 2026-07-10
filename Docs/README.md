# Authentication Fido2 API

This project is an ASP.NET Core Web API that provides authentication using traditional credentials plus passwordless FIDO2/WebAuthn multi-factor authentication.

## What this service does

- Authenticates users with username/email and password
- Supports FIDO2 enrollment for passkey-based MFA
- Supports FIDO2 login assertions for passwordless MFA
- Supports Twilio Verify OTP enrollment and verification for SMS and Email MFA methods
- Issues JWT access and refresh tokens
- Stores user, credential, and transaction data in SQL Server using Entity Framework Core
- Stores OWASP-aligned security audit events for authentication flows

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
- JWT settings: Jwt
- MFA JWT settings: MfaJwt
- FIDO2 settings: Fido2
- Twilio settings: Twilio

### Run locally

```bash
dotnet restore
dotnet ef database update
dotnet run
```

Notes:

- The command dotnet ef database update applies the main EF schema, including OWASP audit tables.

Then open the Swagger UI at:

- http://localhost:5190/swagger
- https://localhost:7190/swagger

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

## Repository hygiene

A root gitignore is configured to ignore build output directories (bin and obj).
If those files were previously tracked, they must be removed from index once with:

- git rm -r --cached bin obj

## Main API flows

### Login

1. Send a login request to the auth endpoint
2. If MFA is required, the API returns AllowedMfaMethods, MfaTransactionId, and MfaToken
3. For sms/email, use MfaToken with api/mfa/challenges/start and api/mfa/challenges/verify (transaction context is resolved from MFA token claims)
4. Full access token is issued only after successful MFA verification
5. If MFA is not required, a full access token is issued directly by login
6. After full authentication, call GET /api/mfa/devices/available to retrieve remaining setup options

### SMS/Email enrollment (Twilio Verify)

1. Authenticate with JWT
2. Start enrollment with api/mfa/enrollment/start and method/contact value
3. Receive OTP via selected channel (sms or email)
4. Verify enrollment with api/mfa/enrollment/verify
5. The method becomes enabled and verified for the user

### FIDO2 enrollment

1. Authenticate with JWT
2. Request enrollment options
3. Complete attestation with the authenticator
4. The credential is stored and MFA is enabled for the user

### FIDO2 login

1. Request login options using username or email
2. Complete assertion with the authenticator
3. Receive JWT tokens after successful verification

## Web test client

The project serves a browser test client from the app root using static files in wwwroot.

- Open the API base URL in the browser (for example https://localhost:xxxx/).
- The client guides flows by token state:
	- RequiresMfa: shows only registered verification methods.
	- Authenticated: loads setup options from /api/mfa/devices/available and shows only enrollment options not configured yet.
- Selecting a method shows the exact endpoints to call for that method.

See WWWROOT_CLIENT_PLAN.md for interaction details.

## Token rules

- Full access token:
	- Issued when MFA is not required, or after MFA verification succeeds
	- Used for standard protected endpoints and enrollment endpoints

- MFA token:
	- Issued only when login returns RequiresMfa
	- Used for MFA login challenge endpoints:
		- /api/mfa/challenges/start
		- /api/mfa/challenges/verify
		- /api/fido2/login/options
		- /api/fido2/login/complete
	- For sms/email challenge endpoints, request body no longer sends mfaTransactionId; it is resolved server-side from mfa_tx claim.
	- Full access token is rejected on these endpoints

## Security notes

- Replace default secrets before deploying
- Use a production-grade secret store or environment variables
- Restrict FIDO2 origins in configuration
- Enable HTTPS in production

## Related documentation

- [Architecture](./ARCHITECTURE.md)
- [OWASP Audit Plan](./OWASP_AUDIT_PLAN.md)
- [Twilio MFA Implementation Plan](./TWILIO_MFA_IMPLEMENTATION_PLAN.md)
- [MFA Enrollment Guide](./MFA_ENROLLMENT_GUIDE.md)
- [API Endpoint Flow Guide](./API_ENDPOINT_FLOW_GUIDE.md)
