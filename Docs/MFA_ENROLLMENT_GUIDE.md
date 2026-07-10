# MFA Enrollment Guide

This guide describes the MFA enrollment options currently available in the REST API.

## Supported Enrollment Paths

1. FIDO2 enrollment (passkey)
- Controller: Fido2Controller
- Endpoints:
  - POST /api/fido2/enrollment/options
  - POST /api/fido2/enrollment/complete

2. SMS/Email OTP enrollment (Twilio Verify)
- Controller: MfaController
- Endpoints:
  - POST /api/mfa/enrollment/start
  - POST /api/mfa/enrollment/verify

## FIDO2 Enrollment (Already Implemented)

1. User authenticates with a full access token.
2. Client requests options from `POST /api/fido2/enrollment/options`.
3. Client completes attestation and sends payload to `POST /api/fido2/enrollment/complete`.
4. FIDO2 credential is stored and the user FIDO2 capability remains available.

## SMS/Email Enrollment (implemented)

### Start Enrollment

POST /api/mfa/enrollment/start

Request body example:

```json
{
  "method": "sms",
  "contactValue": "+15555550100"
}
```

or

```json
{
  "method": "email",
  "contactValue": "user@example.com"
}
```

Response includes:
- enrollmentTransactionId
- method
- status
- expiresAtUtc

### Verify Enrollment

POST /api/mfa/enrollment/verify

Request body example:

```json
{
  "enrollmentTransactionId": "00000000-0000-0000-0000-000000000000",
  "code": "123456"
}
```

On success:
- UserMfaMethod is upserted for the user
- IsEnabled = true
- IsVerified = true
- ContactValue set to verified destination

## Login Behavior With MFA

After password validation:
- If MFA methods exist, login returns:
  - MfaRequired = true
  - AllowedMfaMethods
  - MfaTransactionId
  - MfaToken
- Client chooses method:
  - sms/email: use /api/mfa/challenges/start and /api/mfa/challenges/verify with MfaToken (transaction context comes from token claims)
  - fido2: continue with FIDO2 login endpoints

After full authentication:
- Call GET /api/mfa/devices/available to populate remaining setup options.

## Token Requirements

- Enrollment endpoints require a full access token:
  - `POST /api/mfa/enrollment/start`
  - `POST /api/mfa/enrollment/verify`

- Login challenge endpoints require an MFA token:
  - `POST /api/mfa/challenges/start`
  - `POST /api/mfa/challenges/verify`

- For challenge start/verify, request body no longer sends `mfaTransactionId`.
- Server resolves transaction context from `mfa_tx` claim + active MFA token session.

- Full access token is issued after MFA verification succeeds.

## Required configuration

Twilio settings in appsettings or secret store:
- Twilio:AccountSid
- Twilio:AuthToken
- Twilio:VerifyServiceSid

## Security notes

- Never log OTP code values.
- Use production secret storage for Twilio credentials.
- Use real E.164 phone format for sms channel.
- Keep rate limits and lockout protections enabled in future hardening.

## Current Behavior

- SMS and email enrollment are exposed as REST endpoints under `/api/mfa/enrollment/*`.
- The login response remains the source of truth for allowed MFA methods.
- Enrollment state is persisted in `UserMfaMethods`.
