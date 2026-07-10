# API Endpoint Flow Guide

This guide explains how to use every endpoint in the API, including Auth, MFA (SMS/Email OTP), and FIDO2.

## Base URL

Use your local host/port from launch settings or Swagger:

- https://localhost:7183
- http://localhost:5183

## Response Envelope

All controller responses follow this shape:

- Success:

```json
{
  "success": true,
  "message": "...",
  "data": { }
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

1. POST /api/auth/login
2. GET /api/mfa/methods (Authorize: access token)
3. POST /api/mfa/enrollment/start (Authorize: access token)
4. POST /api/mfa/enrollment/verify (Authorize: access token)
5. POST /api/mfa/challenges/start (Authorize: mfa token)
6. POST /api/mfa/challenges/verify (Authorize: mfa token)
7. POST /api/fido2/enrollment/options (Authorize: access token)
8. POST /api/fido2/enrollment/complete (Authorize: access token)
9. POST /api/fido2/login/options
10. POST /api/fido2/login/complete

## 1) Password Login Entry Point

### POST /api/auth/login

Request:

```json
{
  "username": "cruzrx2",
  "password": "Rdavid58@"
}
```

Possible outcomes:

1. Authenticated immediately (no MFA required):
- data.status = Authenticated
- data.accessToken / data.refreshToken present

2. MFA required:
- data.status = RequiresMfa
- data.mfaRequired = true
- data.mfaTransactionId present
- data.mfaToken present (temporary token for MFA step only)
- data.allowedMfaMethods contains one or more of sms, email, fido2

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

### GET /api/mfa/methods

Headers:
- Authorization: Bearer <access_token>

Response data:

```json
{
  "allowedMfaMethods": ["sms", "email", "fido2"]
}
```

## 3) Enroll SMS/Email MFA Method

Use this flow to register sms or email MFA method for a logged-in user.

### 3.1 POST /api/mfa/enrollment/start

Headers:
- Authorization: Bearer <access_token>

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

### 3.2 POST /api/mfa/enrollment/verify

Headers:
- Authorization: Bearer <access_token>

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

## 4) Complete Login With SMS/Email MFA

After /api/auth/login returns mfaTransactionId and allowed methods.

### 4.1 POST /api/mfa/challenges/start

Headers:
- Authorization: Bearer <mfa_token>

Request:

```json
{
  "mfaTransactionId": "11111111-1111-1111-1111-111111111111",
  "method": "sms"
}
```

Response data:

```json
{
  "mfaTransactionId": "11111111-1111-1111-1111-111111111111",
  "method": "sms",
  "status": "pending",
  "expiresAtUtc": "2026-07-09T12:00:00Z"
}
```

### 4.2 POST /api/mfa/challenges/verify

Headers:
- Authorization: Bearer <mfa_token>

Request:

```json
{
  "mfaTransactionId": "11111111-1111-1111-1111-111111111111",
  "code": "123456"
}
```

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

## 5) FIDO2 Enrollment Flow

### 5.1 POST /api/fido2/enrollment/options

Headers:
- Authorization: Bearer <access_token>

Request body:
- none

Returns:
- data.transactionId
- data.options (WebAuthn credential creation options)

### 5.2 POST /api/fido2/enrollment/complete

Headers:
- Authorization: Bearer <access_token>

Request shape:

```json
{
  "transactionId": "33333333-3333-3333-3333-333333333333",
  "attestationResponse": {
    "id": "...",
    "rawId": "...",
    "type": "public-key",
    "response": { }
  }
}
```

Note:
- attestationResponse is produced by browser WebAuthn APIs and should be forwarded as-is.

## 6) FIDO2 Login Flow

### 6.1 POST /api/fido2/login/options

Request:

```json
{
  "usernameOrEmail": "cruzrx2"
}
```

Returns:
- data.transactionId
- data.options (WebAuthn assertion options)

### 6.2 POST /api/fido2/login/complete

Request shape:

```json
{
  "transactionId": "44444444-4444-4444-4444-444444444444",
  "assertionResponse": {
    "id": "...",
    "rawId": "...",
    "type": "public-key",
    "response": { }
  }
}
```

Note:
- assertionResponse is produced by browser WebAuthn APIs and should be forwarded as-is.

## End-to-End Quick Paths

1. Password only user:
- POST /api/auth/login -> tokens

2. Password + SMS/Email MFA user:
- POST /api/auth/login -> RequiresMfa + allowedMfaMethods + mfaTransactionId
- POST /api/mfa/challenges/start
- POST /api/mfa/challenges/verify -> tokens

3. Password + FIDO2 MFA user:
- POST /api/auth/login -> RequiresMfa + includes fido2
- POST /api/fido2/login/options
- POST /api/fido2/login/complete -> tokens

Note:
- Current FIDO2 login endpoints are transaction-based and do not yet enforce MfaToken.
- SMS/Email MFA challenge endpoints enforce MfaToken.

4. User enrolls new SMS/Email method:
- POST /api/mfa/enrollment/start (Authorize)
- POST /api/mfa/enrollment/verify (Authorize)

5. User enrolls FIDO2 method:
- POST /api/fido2/enrollment/options (Authorize)
- POST /api/fido2/enrollment/complete (Authorize)

## Common Error Cases

- Invalid credentials -> 401
- Invalid/expired MFA transaction or challenge -> 400
- Full access token used on MFA challenge endpoints -> 401 (MFA token required)
- Invalid OTP code -> 401
- Method not enabled for user -> 400
- Invalid token on protected endpoints -> 401

## Setup Checklist Before Testing

1. Configure Twilio credentials:
- Twilio:AccountSid
- Twilio:AuthToken
- Twilio:VerifyServiceSid

2. Ensure user has at least one enabled method in UserMfaMethods.

3. Use valid E.164 phone numbers for sms channel.
