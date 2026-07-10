# MFA Enrollment Guide

This guide describes MFA enrollment options currently available in the API.

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

## FIDO2 Enrollment (already implemented)

1. User authenticates with JWT.
2. Client requests options from /api/fido2/enrollment/options.
3. Client completes attestation and sends payload to /api/fido2/enrollment/complete.
4. FIDO2 credential is stored and user FIDO2 capability remains available.

## SMS/Email Enrollment (new)

### Start enrollment

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

### Verify enrollment

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

## Login behavior with MFA

After password validation:
- If MFA methods exist, login returns:
  - MfaRequired = true
  - AllowedMfaMethods
  - MfaTransactionId
- Client chooses method:
  - sms/email: use /api/mfa/challenges/start and /api/mfa/challenges/verify
  - fido2: continue with FIDO2 login endpoints

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
