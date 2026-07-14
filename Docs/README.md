# Authentication Fido2 REST API

This project is an ASP.NET Core REST API for password-based authentication plus MFA using SMS, email OTP, and FIDO2/WebAuthn.

## What This API Does

- Authenticates users with username/email and password
- Supports SMS, email OTP, and FIDO2 enrollment
- Supports FIDO2 login assertions for passwordless MFA
- Supports Twilio Verify OTP enrollment and verification for SMS and email methods
- Issues JWT access and refresh tokens
- Stores user, credential, token-session, and transaction data in SQL Server using Entity Framework Core
- Stores OWASP-aligned security audit events for authentication, MFA, FIDO2, and public user creation flows

## Tech stack

- .NET 10
- ASP.NET Core REST API
- Entity Framework Core
- SQL Server
- FIDO2/WebAuthn via the Fido2 library
- Swagger/OpenAPI

## Project Structure

- Controllers: exposes REST endpoints for auth, MFA, and FIDO2 flows
- Services: contains business logic for authentication and MFA operations
- Repositories: wraps persistence logic for users, credentials, and transactions
- Data: EF Core DbContext and entity configurations
- DTOs: request and response contracts for APIs
- Entities: domain models such as users and FIDO2 artifacts

## Getting Started

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

## Response Pattern

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

## Security Audit Logging

OWASP-aligned audit logging is implemented for authentication, MFA, FIDO2, and public user creation flows.

- AuthenticationAuditEvents: authentication attempt telemetry
- SecurityAuditEvents: security event ledger with contextual metadata

Audit writes are centralized in Services/Implementatons/AuditService.cs and integrated into AuthService, MfaService, Fido2MfaService, and UserRegistrationService.

See:

- OWASP_AUDIT_PLAN.md
- migration AddOwaspAuditTables

## Repository hygiene

A root gitignore is configured to ignore build output directories (bin and obj).
If those files were previously tracked, they must be removed from index once with:

- git rm -r --cached bin obj

## Main API Flows

## REST Alignment

The API is REST-friendly, but not pure CRUD REST everywhere because authentication involves ceremony-style flows.

- Resource-oriented endpoints now have preferred aliases such as `POST /api/sessions`, `DELETE /api/sessions/current`, `POST /api/mfa/challenges`, `PATCH /api/mfa/challenges/current`, `POST /api/mfa/enrollments`, and `POST /api/fido2/authentications`.
- The legacy action-style routes remain supported for backward compatibility.
- Stateless token validation is preserved on protected endpoints.
- Login and MFA still return workflow-specific data because they represent security ceremonies, not simple entity CRUD.

### Login

1. Send a login request to `POST /api/auth/login`.
2. If MFA is required, the login response returns `AllowedMfaMethods` and `MfaToken`.
3. For SMS/email verification, use `MfaToken` with `POST /api/mfa/challenges/start` and `POST /api/mfa/challenges/verify`.
4. When an enrollment completes, any `recoveryCodes` in the response must be shown once and downloadable immediately.
4. Transaction context is resolved server-side from the MFA token claims.
5. A full access token is issued only after successful MFA verification.
6. If MFA is not required, a full access token is issued directly by login.
7. After full authentication, call `GET /api/mfa/devices/available` to retrieve remaining setup options.

### SMS/Email Enrollment (Twilio Verify)

1. Authenticate with a full access token.
2. Start enrollment with `POST /api/mfa/enrollment/start` and method/contact value.
3. Receive OTP via the selected channel (sms or email).
4. Verify enrollment with `POST /api/mfa/enrollment/verify`.
5. The method becomes enabled and verified for the user.

### FIDO2 Enrollment

1. Authenticate with a full access token.
2. Request enrollment options with `POST /api/fido2/enrollment/options`.
3. Complete attestation with the authenticator through `POST /api/fido2/enrollment/complete`.
4. The credential is stored and FIDO2 MFA is enabled for the user.
5. Completion is bound to the authenticated user that created the transaction.

### FIDO2 Login

1. Request login options using `POST /api/fido2/login/options`.
2. Complete assertion with the authenticator through `POST /api/fido2/login/complete`.
3. Receive JWT tokens after successful verification.

## Web Test Client

The project serves a browser-based REST client from the app root using static files in wwwroot.

- Open the API base URL in the browser (for example `https://localhost:7190/`).
- The client guides flows by token state:
	- RequiresMfa: shows only registered verification methods.
	- Authenticated: loads setup options from /api/mfa/devices/available and shows only enrollment options not configured yet.
- Selecting a method shows the exact endpoints to call for that method.

See [WWWROOT_CLIENT_PLAN.md](./WWWROOT_CLIENT_PLAN.md) for interaction details.

## Token Rules

- Full access token:
	- Issued when MFA is not required, or after MFA verification succeeds.
	- Used for standard protected endpoints and enrollment endpoints.

- MFA token:
	- Issued only when login returns `RequiresMfa`.
	- Used for MFA login challenge endpoints:
		- `POST /api/mfa/challenges/start`
		- `POST /api/mfa/challenges/verify`
		- `POST /api/fido2/login/options`
		- `POST /api/fido2/login/complete`
	- For SMS/email challenge endpoints, the request body no longer sends `mfaTransactionId`; it is resolved server-side from the `mfa_tx` claim.
	- Full access token is rejected on these endpoints.

## Security Notes

- Replace default secrets before deploying
- Use a production-grade secret store or environment variables
- Restrict FIDO2 origins in configuration
- Enable HTTPS in production

## Related Documentation

- [Final Backend Technical Documentation](./FINAL_BACKEND_TECHNICAL_DOCUMENTATION.md)
- [Architecture](./ARCHITECTURE.md)
- [Database Schema Reference](./DATABASE_SCHEMA.md)
- [OWASP Audit Plan](./OWASP_AUDIT_PLAN.md)
- [OWASP Pen Test Checklist](./OWASP_PEN_TEST_CHECKLIST.md)
- [Twilio MFA Implementation Plan](./TWILIO_MFA_IMPLEMENTATION_PLAN.md)
- [MFA Enrollment Guide](./MFA_ENROLLMENT_GUIDE.md)
- [API Endpoint Flow Guide](./API_ENDPOINT_FLOW_GUIDE.md)
