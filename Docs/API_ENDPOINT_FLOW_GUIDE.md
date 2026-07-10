# REST API Endpoint Flow Guide

This guide documents the REST endpoints for auth, MFA, and FIDO2.

## Base URL

Use the configured local host/port from launch settings or Swagger:

- `http://localhost:5190`
- `https://localhost:7190`

## Response Envelope

All controller responses follow this shape:

- Success:

```json
{
  "success": true,
  "message": "...",
  "data": {}
}
```

- Failure:

```json
{
  "success": false,
  "message": "..."
}
```

## Endpoint Index

1. `POST /api/auth/login`
2. `POST /api/auth/logout` (`Authorize: access token`)
3. `POST /api/auth/cancel-authentication` (`Authorize: mfa token`)
4. `GET /api/mfa/methods` (`Authorize: access token`)
5. `GET /api/mfa/devices/available` (`Authorize: access token`)
6. `POST /api/mfa/enrollment/start` (`Authorize: access token`)
7. `POST /api/mfa/enrollment/verify` (`Authorize: access token`)
8. `POST /api/mfa/challenges/start` (`Authorize: mfa token`)
9. `POST /api/mfa/challenges/verify` (`Authorize: mfa token`)
10. `POST /api/fido2/enrollment/options` (`Authorize: access token`)
11. `POST /api/fido2/enrollment/complete` (`Authorize: access token`)
12. `POST /api/fido2/login/options` (`Authorize: mfa token`)
13. `POST /api/fido2/login/complete` (`Authorize: mfa token`)

## REST Alignment

The API follows REST principles where practical for authentication workflows.

- Preferred resource-style aliases:
  - `POST /api/sessions` for login
  - `DELETE /api/sessions/current` for logout
  - `DELETE /api/mfa/sessions/current` for canceling an MFA temp session
  - `POST /api/mfa/challenges` for starting an MFA challenge
  - `PATCH /api/mfa/challenges/current` for verifying an MFA challenge
  - `POST /api/mfa/enrollments` for starting MFA enrollment
  - `PATCH /api/mfa/enrollments/current` for verifying MFA enrollment
  - `POST /api/fido2/enrollments` for starting FIDO2 enrollment
  - `PATCH /api/fido2/enrollments/current` for completing FIDO2 enrollment
  - `POST /api/fido2/authentications` for starting FIDO2 login
  - `PATCH /api/fido2/authentications/current` for completing FIDO2 login
- Legacy action-style routes remain supported for compatibility.
- The login and challenge flows remain ceremony-style endpoints because they model state transitions, not CRUD over a single persisted entity.

## Token Session Behavior

- Full access tokens include a `jti` and are validated against server-side sessions.
- MFA temp tokens include a `jti` and are validated against `MfaTempTokenSessions`.
- A new successful login invalidates previous active token sessions for that user.
- `POST /api/auth/logout` revokes the current full token session.
- `POST /api/auth/cancel-authentication` revokes the current MFA temp token session.

## 1) Password Login Entry Point

### `POST /api/auth/login`

Request:

```json
{
  "username": "cruzrx2",
  "password": "Rdavid58@"
}
```

Possible outcomes:

1. Authenticated immediately (no MFA required):
- `data.status = Authenticated`
- `data.accessToken` / `data.refreshToken` present

2. MFA required:
- `data.status = RequiresMfa`
- `data.mfaRequired = true`
- `data.mfaTransactionId` present
- `data.mfaToken` present (temporary token for MFA step only)
- `data.allowedMfaMethods` contains one or more of `sms`, `email`, `fido2`

Example MFA-required response:

```json
{
  "success": true,
  "message": "MFA verification required.",
  "data": {
    "status": "RequiresMfa",
    "mfaRequired": true,
    "mfaTransactionId": "11111111-1111-1111-1111-111111111111",
    "mfaToken": "...",
    "mfaExpiresIn": 300,
    "allowedMfaMethods": ["sms", "email", "fido2"]
  }
}
```

## 2) Get Enabled MFA Methods For Current User

### `GET /api/mfa/methods`

Headers:
- `Authorization: Bearer <access_token>`

Response data:

```json
{
  "allowedMfaMethods": ["sms", "email", "fido2"]
}
```

## 3) Get Available Devices For Setup

### `GET /api/mfa/devices/available`

Headers:
- `Authorization: Bearer <access_token>`

Response data:

```json
{
  "allowedMfaMethods": ["sms"],
  "availableMfaSetupOptions": ["email", "fido2"]
}
```

## 4) Enroll SMS/Email MFA Method

Use this flow to register SMS or email MFA for a logged-in user.

### `POST /api/mfa/enrollment/start`

Headers:
- `Authorization: Bearer <access_token>`

Request examples:

```json
{
  "method": "sms",
  "contactValue": "+15555550100"
}
```

```json
{
  "method": "email",
  "contactValue": "user@example.com"
}
```

Response data:

```json
{
  "enrollmentTransactionId": "22222222-2222-2222-2222-222222222222",
  "method": "sms",
  "status": "pending",
  "expiresAtUtc": "2026-07-09T12:00:00Z"
}
```

### `POST /api/mfa/enrollment/verify`

Headers:
- `Authorization: Bearer <access_token>`

Request:

```json
{
  "enrollmentTransactionId": "22222222-2222-2222-2222-222222222222",
  "code": "123456"
}
```

Success result:
- Method is marked enabled and verified for the user.

Response data:

```json
{
  "method": "sms",
  "isVerified": true
}
```

## 5) Complete Login With SMS/Email MFA

After `POST /api/auth/login` returns `mfaTransactionId` and allowed methods.

### `POST /api/mfa/challenges/start`

Headers:
- `Authorization: Bearer <mfa_token>`

Request:

```json
{
  "method": "sms"
}
```

Note:
- `mfaTransactionId` is resolved server-side from the MFA token (`mfa_tx` claim).
- The client does not send `mfaTransactionId` in the request payload.

Response data:

```json
{
  "mfaTransactionId": "11111111-1111-1111-1111-111111111111",
  "method": "sms",
  "status": "pending",
  "expiresAtUtc": "2026-07-09T12:00:00Z"
}
```

### `POST /api/mfa/challenges/verify`

Headers:
- `Authorization: Bearer <mfa_token>`

Request:

```json
{
  "code": "123456"
}
```

Note:
- `mfaTransactionId` is resolved server-side from the MFA token (`mfa_tx` claim).
- The client does not send `mfaTransactionId` in the request payload.

Success response returns tokens:

```json
{
  "success": true,
  "message": "MFA verification succeeded.",
  "data": {
    "status": "Authenticated",
    "accessToken": "...",
    "refreshToken": "...",
    "expiresIn": 900,
    "mfaRequired": false
  }
}
```

## 6) FIDO2 Enrollment Flow

### `POST /api/fido2/enrollment/options`

Headers:
- `Authorization: Bearer <access_token>`

Request body:
- none

Returns:
- `data.transactionId`
- `data.options` (WebAuthn credential creation options)

### `POST /api/fido2/enrollment/complete`

Headers:
- `Authorization: Bearer <access_token>`

Request shape:

```json
{
  "transactionId": "33333333-3333-3333-3333-333333333333",
  "attestationResponse": {
    "id": "...",
    "rawId": "...",
    "type": "public-key",
    "response": {}
  }
}
```

Note:
- `attestationResponse` is produced by browser WebAuthn APIs and should be forwarded as-is.

## 7) FIDO2 Login Flow

### `POST /api/fido2/login/options`

Headers:
- `Authorization: Bearer <mfa_token>`

Request:

```json
{
  "usernameOrEmail": "cruzrx2"
}
```

Returns:
- `data.transactionId`
- `data.options` (WebAuthn assertion options)

### `POST /api/fido2/login/complete`

Headers:
- `Authorization: Bearer <mfa_token>`

Request shape:

```json
{
  "transactionId": "44444444-4444-4444-4444-444444444444",
  "assertionResponse": {
    "id": "...",
    "rawId": "...",
    "type": "public-key",
    "response": {}
  }
}
```

Note:
- `assertionResponse` is produced by browser WebAuthn APIs and should be forwarded as-is.

## End-to-End Quick Paths

1. Password-only user:
- `POST /api/auth/login` -> tokens

2. Password + SMS/email MFA user:
- `POST /api/auth/login` -> `RequiresMfa` + `allowedMfaMethods` + `mfaTransactionId`
- `POST /api/mfa/challenges/start`
- `POST /api/mfa/challenges/verify` -> tokens

3. Password + FIDO2 MFA user:
- `POST /api/auth/login` -> `RequiresMfa` + includes `fido2`
- `POST /api/fido2/login/options`
- `POST /api/fido2/login/complete` -> tokens

Note:
- FIDO2 login endpoints enforce `MfaToken` and validate `mfa_tx` and token session context.
- SMS/email MFA challenge endpoints also enforce `MfaToken`.

4. User enrolls a new SMS/email method:
- `POST /api/mfa/enrollment/start` (`Authorize`)
- `POST /api/mfa/enrollment/verify` (`Authorize`)

5. User enrolls a FIDO2 method:
- `POST /api/fido2/enrollment/options` (`Authorize`)
- `POST /api/fido2/enrollment/complete` (`Authorize`)

## Common Error Cases

- Invalid credentials -> `401`
- Invalid/expired MFA transaction or challenge -> `400`
- Full access token used on MFA challenge endpoints -> `401` (`MFA token required`)
- Full access token used on FIDO2 login endpoints -> `401` (`MFA token required`)
- Invalid OTP code -> `401`
- Method not enabled for user -> `400`
- Invalid token on protected endpoints -> `401`

## Setup Checklist Before Testing

1. Configure Twilio credentials:
- `Twilio:AccountSid`
- `Twilio:AuthToken`
- `Twilio:VerifyServiceSid`

2. Ensure the user has at least one enabled method in `UserMfaMethods`.

3. Use valid E.164 phone numbers for sms channel.
