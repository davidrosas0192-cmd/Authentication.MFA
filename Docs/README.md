# Authentication Fido2 API

This project is an ASP.NET Core Web API that provides authentication using traditional credentials plus passwordless FIDO2/WebAuthn multi-factor authentication.

## What this service does

- Authenticates users with username/email and password
- Supports FIDO2 enrollment for passkey-based MFA
- Supports FIDO2 login assertions for passwordless MFA
- Issues JWT access and refresh tokens
- Stores user, credential, and transaction data in SQL Server using Entity Framework Core

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

### Run locally

```bash
dotnet restore
dotnet ef database update
dotnet run
```

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
