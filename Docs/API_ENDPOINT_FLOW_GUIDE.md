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

1. `POST /api/users`
2. `POST /api/sessions`
3. `DELETE /api/sessions/current` (`Authorize: access token`)
4. `DELETE /api/mfa/sessions/current` (`Authorize: mfa token`)
5. `GET /api/mfa/methods` (`Authorize: access token`)
6. `GET /api/mfa/setup-options` (`Authorize: access token`)
7. `POST /api/mfa/challenges` (`Authorize: mfa token`)
8. `PATCH /api/mfa/challenges/current` (`Authorize: mfa token`)
9. `POST /api/mfa/enrollments` (`Authorize: access token`)
10. `PATCH /api/mfa/enrollments/current` (`Authorize: access token`)
11. `POST /api/fido2/enrollments` (`Authorize: access token`)
12. `PATCH /api/fido2/enrollments/current` (`Authorize: access token`)
13. `POST /api/fido2/authentications` (`Authorize: mfa token`)
14. `PATCH /api/fido2/authentications/current` (`Authorize: mfa token`)
9/*15. `POST /api/mfa/login-enrollments` (`Authorize: login_enrollment token`)
16. `PATCH /api/mfa/login-enrollments/current` (`Authorize: login_enrollment token`)
17. `POST /api/mfa/login-enrollment-sessions/complete` (`Authorize: login_enrollment token`)
18. `POST /api/mfa/management-sessions` (`Authorize: access token`)
19. `POST /api/mfa/management-sessions/challenges/start` (`Authorize: access token`)
20. `POST /api/mfa/management-sessions/challenges/verify` (`Authorize: access token`)
21. `POST /api/mfa/management-sessions/complete` (`Authorize: access token`)
22. `DELETE /api/mfa/management-sessions/{mfaTransactionId}` (`Authorize: access token`)
23. `DELETE /api/mfa/methods/{method}` (`Authorize: access token`)
24. `POST /api/mfa/methods/{method}/reconfigure` (`Authorize: access token`)
25. `PATCH /api/mfa/methods/{method}/reconfigure/current` (`Authorize: access token`)

## REST Alignment

The API uses a single RESTful route per endpoint. Legacy action-style aliases were removed.

## Token Session Behavior

- Full access tokens include a `jti` and are validated against server-side sessions.
- MFA temp tokens include a `jti` and are validated against `MfaTempTokenSessions`.
- Login enrollment tokens include a `jti` and are validated against `MfaLoginEnrollmentSessions`.
- A new successful login invalidates previous active token sessions for that user.
- `DELETE /api/sessions/current` revokes the current full token session.
- `DELETE /api/mfa/sessions/current` revokes the current MFA temp token session.

## 1) Password Login Entry Point

### `POST /api/users`

Request:

```json
{
  "username": "cruzrx2",
  "email": "davidrosas0192@gmail.com",
  "password": "Rdavid58!"
}
```

Possible outcomes:

1. User created successfully:
- `data.userId` present
- `data.username` and `data.email` echoed back

2. Validation or conflict failure:
- `400` for invalid input
- `409` or `400` for duplicate user conditions depending on service implementation

Security notes:
- Treat user creation as a public endpoint subject to rate limiting and abuse monitoring.
- Do not leak whether the username or email already exists beyond a generic validation/conflict response.

Audit expectations:
- Log request source, outcome, and correlation id.
- Never log plaintext passwords.

### `POST /api/sessions`

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
- `data.mfaToken` present (temporary token for MFA step only)
- `data.allowedMfaMethods` contains one or more of `sms`, `email`, `fido2`, `recovery_code`

Example MFA-required response:

```json
{
  "success": true,
  "message": "MFA verification required.",
  "data": {
    "status": "RequiresMfa",
    "mfaRequired": true,
    "mfaToken": "...",
    "mfaExpiresIn": 300,
    "allowedMfaMethods": ["sms", "email", "fido2"]
  }
}
```

3. Enrollment required before authentication can complete:
- `data.status = RequiresEnrollment`
- `data.enrollmentToken` present
- `data.enrollmentSessionId` present
- `data.enrollmentContinuationToken` present
- `data.availableMfaSetupOptions` contains bootstrap-safe setup choices

Example enrollment-required response:

```json
{
  "success": true,
  "message": "MFA enrollment required before completing authentication.",
  "data": {
    "status": "RequiresEnrollment",
    "enrollmentToken": "...",
    "enrollmentExpiresIn": 300,
    "enrollmentSessionId": "11111111-1111-1111-1111-111111111111",
    "enrollmentContinuationToken": "bootstrap-ct-1",
    "availableMfaSetupOptions": ["email"]
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
  "allowedMfaMethods": ["sms", "email", "fido2", "recovery_code"]
}
```

## 3) Get Available Devices For Setup

### `GET /api/mfa/setup-options`

Headers:
- `Authorization: Bearer <access_token>`

Response data:

```json
{
  "allowedMfaMethods": ["sms"],
  "availableMfaSetupOptions": ["email", "fido2"]
}
```

## 4) Login-Time Enrollment Bootstrap

Use this flow only when `POST /api/sessions` returns `RequiresEnrollment`.

### `POST /api/mfa/login-enrollments`

Headers:
- `Authorization: Bearer <login_enrollment_token>`

Request example:

```json
{
  "continuationToken": "bootstrap-ct-1",
  "method": "email",
  "contactValue": "user@example.com"
}
```

Response data:

```json
{
  "enrollmentSessionId": "11111111-1111-1111-1111-111111111111",
  "enrollmentTransactionId": "22222222-2222-2222-2222-222222222222",
  "sessionContinuationToken": "bootstrap-ct-2",
  "challengeContinuationToken": "challenge-ct-1",
  "method": "email",
  "status": "pending",
  "expiresAtUtc": "2026-07-14T12:00:00Z"
}
```

### `PATCH /api/mfa/login-enrollments/current`

Headers:
- `Authorization: Bearer <login_enrollment_token>`

Request example:

```json
{
  "enrollmentTransactionId": "22222222-2222-2222-2222-222222222222",
  "continuationToken": "challenge-ct-1",
  "code": "123456"
}
```

### `POST /api/mfa/login-enrollment-sessions/complete`

Headers:
- `Authorization: Bearer <login_enrollment_token>`

Request example:

```json
{
  "enrollmentSessionId": "11111111-1111-1111-1111-111111111111",
  "continuationToken": "bootstrap-ct-3"
}
```

Success result:

- full access token is issued only here
- bootstrap token/session becomes unusable

## 5) Enroll SMS/Email MFA Method From Settings

Use this flow to register SMS or email MFA for a logged-in user only after management step-up.

Precondition:

- a recent `manage_mfa` step-up session must exist

### `POST /api/mfa/enrollments`

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

### `PATCH /api/mfa/enrollments/current`

Headers:
- `Authorization: Bearer <access_token>`

Request:

```json
{
  "enrollmentTransactionId": "22222222-2222-2222-2222-222222222222",
  "continuationToken": "token-from-start-response",
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

## 6) Complete Login With SMS/Email MFA

After `POST /api/sessions` returns allowed methods.

### `POST /api/mfa/challenges`

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

### `PATCH /api/mfa/challenges/current`

Headers:
- `Authorization: Bearer <mfa_token>`

Request:

```json
{
  "continuationToken": "token-from-start-response",
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

## 7) FIDO2 Enrollment Flow

### `POST /api/fido2/enrollments`

Headers:
- `Authorization: Bearer <access_token>`

Request body:
- none

Returns:
- `data.transactionId`
- `data.options` (WebAuthn credential creation options)

### `PATCH /api/fido2/enrollments/current`

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
- Enrollment completion is validated server-side against the authenticated user that created the transaction.

## 8) FIDO2 Login Flow

### `POST /api/fido2/authentications`

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

### `PATCH /api/fido2/authentications/current`

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
- `POST /api/sessions` -> tokens

2. Password + SMS/email MFA user:
- `POST /api/sessions` -> `RequiresMfa` + `allowedMfaMethods` + `mfaToken`
- `POST /api/mfa/challenges`
- `PATCH /api/mfa/challenges/current` -> tokens

3. Password + FIDO2 MFA user:
- `POST /api/sessions` -> `RequiresMfa` + includes `fido2`
- `POST /api/fido2/authentications`
- `PATCH /api/fido2/authentications/current` -> tokens

Note:
- FIDO2 login endpoints enforce `MfaToken` and validate `mfa_tx` and token session context.
- SMS/email MFA challenge endpoints also enforce `MfaToken`.

4. User enrolls a new SMS/email method:
- `POST /api/mfa/enrollments` (`Authorize`)
- `PATCH /api/mfa/enrollments/current` (`Authorize`)

5. User enrolls a FIDO2 method:
- `POST /api/fido2/enrollments` (`Authorize`)
- `PATCH /api/fido2/enrollments/current` (`Authorize`)

## Common Error Cases

- Invalid credentials -> `401`
- Invalid/expired MFA transaction or challenge -> `400`
- Full access token used on MFA challenge endpoints -> `401` (`MFA token required`)
- Full access token used on FIDO2 login endpoints -> `401` (`MFA token required`)
- Invalid OTP code -> `401`
- Method not enabled for user -> `400`
- Invalid token on protected endpoints -> `401`
- User creation validation or conflict -> `400` or `409`
- Public endpoint abuse or rate limit violation -> `429`

## Audit Expectations

- Every endpoint should emit a security or authentication audit event when the action is security-relevant.
- Login, logout, cancel-authentication, public user creation, MFA method discovery, MFA start/verify, enrollment start/verify, and FIDO2 option/complete flows should all be traceable by `CorrelationId`.
- Audit payloads must not contain passwords, OTP codes, bearer tokens, or raw WebAuthn assertions/attestations.
- Failed authentication and authorization checks should still be auditable.
- Public user creation should be auditable and rate limited.

## Setup Checklist Before Testing

1. Configure Twilio credentials:
- `Twilio:AccountSid`
- `Twilio:AuthToken`
- `Twilio:VerifyServiceSid`

2. Ensure the user has at least one enabled method in `UserMfaMethods`.

3. Use valid E.164 phone numbers for sms channel.
