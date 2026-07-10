# Twilio MFA Implementation Plan (SMS OTP + Email OTP + FIDO2 Combinations)

## Goal
Enable users to have one or more MFA methods enabled at the same time:

- SMS only
- Email OTP only
- FIDO2 only
- SMS + FIDO2
- SMS + Email OTP
- Email OTP + FIDO2
- SMS + Email OTP + FIDO2

This plan uses Twilio for OTP delivery and keeps FIDO2 as an existing factor.

## Current State
- User MFA model is a single flag: IsFido2MfaEnabled in Entities/User.cs.
- Login currently supports password + optional FIDO2 challenge flow.
- No method registry exists to represent multiple active factors.

## Target Design
Use a factor registry model (recommended) instead of boolean flags.

### New Tables
1. UserMfaMethods
- Purpose: registry of enabled/disabled MFA methods per user.
- Columns:
  - Id (PK)
  - UserId (FK Users)
  - Method (string: sms, email, fido2)
  - IsEnabled (bool)
  - IsPrimary (bool)
  - IsVerified (bool)
  - ContactValue (nullable string; phone/email for OTP methods)
  - CreatedAtUtc
  - UpdatedAtUtc
- Constraints:
  - Unique index on (UserId, Method)

Note:
- `IsEnabled` remains a database/internal control flag.
- It is managed by internal workflows/admin processes, not by public user endpoints.

2. MfaChallenges
- Purpose: track MFA challenge lifecycle for login verification.
- Columns:
  - Id (GUID PK)
  - UserId (FK Users)
  - Method (sms, email, fido2)
  - Provider (twilio, fido2)
  - ProviderRequestId (nullable; Twilio SID)
  - Channel (sms, email)
  - Status (pending, verified, expired, failed, canceled)
  - ExpiresAtUtc
  - VerifiedAtUtc (nullable)
  - IpAddress
  - UserAgent
  - CreatedAtUtc
- Indexes:
  - (UserId, Status, ExpiresAtUtc)
  - (ProviderRequestId)

3. Optional: UserMfaPreferences
- Purpose: policy and UX defaults.
- Columns:
  - UserId (PK/FK)
  - PreferredMethod (nullable)
  - RequireMfaEveryLogin (bool)

## Twilio Integration Strategy
Use Twilio Verify API (recommended):
- Start verification with channel sms/email.
- Check verification code with Verify endpoint.
- Avoid storing OTP codes in DB.

Configuration:
- Twilio:AccountSid
- Twilio:AuthToken
- Twilio:VerifyServiceSid
- Twilio:FromPhoneNumber (if needed for fallback)

Security notes:
- Store Twilio secrets in environment variables/secret manager.
- Never log OTP values.
- Rate-limit start/check endpoints.

## API Contract Changes
### New/Updated Endpoints
1. GET /api/mfa/methods
- Returns all available/enabled methods for current user.

2. POST /api/mfa/challenges/start
- Input: method (sms/email/fido2), login transaction context.
- Output: challenge metadata and expiration.

3. POST /api/mfa/challenges/verify
- Input: challengeId + OTP code (for sms/email) or fido2 payload.
- Output: authenticated tokens if successful.

### Login Flow Update
- After password validation, fetch enabled methods from UserMfaMethods.
- If no enabled method: issue tokens directly.
- If methods exist:
  - Return RequiresMfa = true
  - Return AllowedMfaMethods array in LoginResponse
  - Client chooses method and starts challenge

This is the primary place where allowed methods are returned.

## DTO Changes
Update LoginResponse:
- Add AllowedMfaMethods: string[]
- Keep MfaRequired and MfaTransactionId (or rename to ChallengeId for consistency)

Add DTOs:
- StartMfaChallengeRequest
- VerifyMfaChallengeRequest
- MfaMethodsResponse

## Service Layer Plan
1. Add interfaces:
- IMfaMethodService
- IMfaChallengeService
- ITwilioOtpService

2. Implement services:
- MfaMethodService: CRUD/validation for user methods.
- MfaChallengeService: challenge creation, expiration, verification orchestration.
- TwilioOtpService: start/check OTP via Twilio Verify.

3. Update AuthService:
- Replace IsFido2MfaEnabled decision with method registry lookup.

4. Keep Fido2MfaService focused on FIDO2 flows; integrate via challenge orchestration.

## Audit & Logging Plan
Log both domain audit and app logs:
- Domain audit tables (already implemented):
  - AuthenticationAuditEvents
  - SecurityAuditEvents
- Add new event types:
  - auth.mfa.method.enabled
  - auth.mfa.method.disabled
  - auth.mfa.challenge.started
  - auth.mfa.challenge.verified
  - auth.mfa.challenge.failed

Do not log OTP codes or Twilio auth tokens.

## Migration Plan
Phase 1: Schema introduction
- Add UserMfaMethods and MfaChallenges tables.
- Add EF migration.

Phase 2: Backfill
- For users where IsFido2MfaEnabled = true, insert UserMfaMethods(method=fido2, isEnabled=true).

Phase 3: Dual-read
- Read new table first; fallback to old IsFido2MfaEnabled during transition.

Phase 4: Cutover
- Remove fallback logic.
- Deprecate/remove IsFido2MfaEnabled column in later migration.

## Validation Rules
- SMS method requires verified E.164 phone number.
- Email method requires verified email address.
- Prevent duplicate enabled rows for same method/user.
- Enforce OTP attempt limits and challenge expiry.
- Internal updates to IsEnabled must prevent unsafe lockout states (policy-based).

## Penetration Testing Readiness
Test cases:
- OTP brute-force protection (attempt limits, lockout windows).
- Challenge replay prevention.
- Expired challenge rejection.
- Method tampering attempts (user A cannot verify user B challenge).
- Secret leakage checks in logs and responses.
- Twilio webhook/signature validation if callbacks are used.

## Implementation Order (Execution)
1. Add entities, configurations, migration for UserMfaMethods and MfaChallenges.
2. Add Twilio options + ITwilioOtpService and concrete implementation.
3. Add methods read endpoint (list allowed methods) and challenge endpoints (start/verify for sms/email).
4. Integrate with AuthService login decision and LoginResponse contract (return AllowedMfaMethods when MFA required).
5. Add audit events for all new flows.
6. Add tests (unit + integration + abuse/negative cases).

## Acceptance Criteria
- A user can enable any combination of sms/email/fido2.
- Login returns available MFA methods when MFA is required.
- SMS and Email OTP flows work through Twilio Verify.
- Audit logs capture MFA enrollment and challenge lifecycle.
- Production logging remains error-focused while security audit persists.
