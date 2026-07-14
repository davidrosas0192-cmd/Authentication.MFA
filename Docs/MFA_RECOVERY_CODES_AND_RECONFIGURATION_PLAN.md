# MFA Recovery Codes + Method Removal/Reconfiguration Plan

## Objective
Implement two security features with low regression risk:

1. Recovery codes as a break-glass MFA factor.
2. Safe removal and reconfiguration of MFA options (sms, email, fido2) without account lockout.

This plan is aligned to the existing architecture and REST style in this repository.

## Current Baseline (What Exists)
- MFA methods are stored in UserMfaMethods and exposed by MfaService.
- OTP login challenge flow is in MfaController + MfaService.
- FIDO2 enrollment/login flow is in Fido2Controller + Fido2MfaService.
- Security/authentication events are centrally audited via IAuditService.
- FIDO2 availability is still partly coupled to Users.IsFido2MfaEnabled.

## Best-Practice Principles
- Recovery codes must be one-time use and stored hashed (never plaintext).
- Sensitive MFA-management operations require step-up confirmation, not just a long-lived access token.
- Never allow disabling/removing the last viable MFA path unless policy explicitly allows it.
- Avoid revealing which code failed in error messages.
- Apply rate limiting, attempt throttling, and replay protection.
- Audit all management and recovery-code events with non-sensitive metadata only.

## Scope
### In Scope
- Generate, view-once, regenerate, and consume recovery codes.
- Remove/disable an existing MFA method.
- Reconfigure MFA method contact/device (sms/email/fido2).
- API, service, repository, migration, and tests.

### Out of Scope (Phase 1)
- Web UI implementation.
- Multi-tenant policy engine.
- External notification templates beyond existing providers.

## Data Model Changes

### 1) New table: UserRecoveryCodeBatches
Purpose: group sets of issued recovery codes and support safe regeneration.

Columns:
- Id (GUID PK)
- UserId (FK Users)
- IssuedAtUtc
- ReplacedAtUtc (nullable)
- CreatedBy (optional, e.g., self/admin)

Indexes:
- (UserId, IssuedAtUtc DESC)
- (UserId, ReplacedAtUtc)

### 2) New table: UserRecoveryCodes
Purpose: store one-time codes (hashed) for verification.

Columns:
- Id (GUID PK)
- BatchId (FK UserRecoveryCodeBatches)
- UserId (FK Users, denormalized for query speed)
- CodeHash (string, PBKDF2 format)
- UsedAtUtc (nullable)
- ExpiresAtUtc (nullable, optional policy)
- CreatedAtUtc

Indexes:
- (UserId, UsedAtUtc)
- (BatchId)

Notes:
- Use existing PasswordHasher for code hash + verification.
- Keep plaintext codes only in memory for immediate API response.

### 3) Optional hardening field in UserMfaMethod
Add:
- DisabledAtUtc (nullable)
- DisabledReason (nullable)

This is optional; can be deferred if IsEnabled + UpdatedAtUtc are sufficient.

## Domain/Constant Additions
- Add method type constant: recovery_code in MfaMethodTypes.
- Add challenge purpose constant (optional): mfa_management_reauth for step-up management actions.

## API Design (REST-Compatible)

### Recovery Codes Management (Access Token + Step-Up)
1. POST /api/mfa/recovery-codes
- Generates a new batch (e.g., 10 codes), invalidates prior active batch.
- Response includes plaintext codes once.
- Alias style can match existing pattern if desired.

2. GET /api/mfa/recovery-codes/status
- Returns metadata only:
  - hasRecoveryCodes
  - remainingCodes
  - issuedAtUtc
- Never returns codes.

3. POST /api/mfa/recovery-codes/regenerate
- Same output as create.
- Must invalidate old active codes atomically.

### Login Challenge Integration (MFA Token)
4. POST /api/mfa/challenges with method = recovery_code
- No external provider call.
- Creates/updates challenge state for code verification.

5. PATCH /api/mfa/challenges/current
- Existing endpoint should accept recovery code in code field.
- On success: mark exactly one recovery code as used and issue full tokens.

### Remove and Reconfigure MFA Methods (Access Token + Step-Up)
6. DELETE /api/mfa/methods/{method}
- method in {sms,email,fido2}
- Performs lockout-safety checks before disable/remove.

7. POST /api/mfa/methods/{method}/reconfigure
- For sms/email: starts verification for new contact.
- For fido2: returns fresh enrollment options.
- Uses transaction type marked as reconfigure to keep audit semantics clean.

8. PATCH /api/mfa/methods/{method}/reconfigure/current
- For sms/email: verifies OTP and atomically updates contact.
- For fido2: finalizes credential rotation/replace behavior.

### Optional FIDO2 Device Management
9. GET /api/fido2/credentials
10. DELETE /api/fido2/credentials/{credentialId}
- Needed when users want to remove a specific passkey while preserving others.

## Lockout-Safety Rules
Apply before delete/disable and before reconfigure finalization:

1. Count viable factors:
- enabled sms/email/fido2
- plus any unused recovery codes

2. Block operation if it would leave user with zero viable factors, unless:
- an explicit policy allows password-only fallback, or
- user is simultaneously completing setup of another factor in same transaction.

3. For fido2 removal:
- If removing last credential and no other factor/recovery code exists, reject.

4. For recovery-code regeneration:
- Generate new batch and commit before invalidating old batch (or in one transaction) to avoid a no-code window.

## Step-Up Authentication for MFA Management
Because access tokens can be long-lived, require one of:
- Recent auth claim (auth_time within N minutes), or
- Explicit re-auth endpoint (password + existing MFA challenge) issuing a short management token.

Recommended phase-1 path:
- Add a short-lived management challenge in MfaService based on existing challenge model.

## Service/Repository Changes

### Service Interface: IMfaService
Add methods such as:
- RemoveMethodAsync
- StartReconfigureMethodAsync
- CompleteReconfigureMethodAsync

### Repository Interfaces
1. New repository: IUserRecoveryCodeRepository
- CreateIfMissingAsync
- GetStatusAsync
- TryConsumeCodeAsync

2. Extend IUserMfaMethodRepository
- GetAllByUserIdAsync
- DisableAsync (or UpdateAsync with IsEnabled=false)

3. Extend IFido2CredentialRepository
- GetByIdAndUserAsync
- DeleteAsync
- CountByUserAsync

## Migration Strategy (Low Risk)

### Phase A: Additive schema
- Add new recovery-code tables and indexes.
- Add optional UserMfaMethod hardening columns if needed.
- No destructive drops.

### Phase B: Dual-path logic
- Implement recovery code verification while preserving current OTP/FIDO2 paths.
- Keep IsFido2MfaEnabled compatibility behavior initially.

### Phase C: FIDO2 source-of-truth cleanup
- Move fully to UserMfaMethods + credential count as source of truth.
- Remove dependency on Users.IsFido2MfaEnabled in runtime checks.

### Phase D: Decommission legacy flag
- Separate migration to remove IsFido2MfaEnabled only after full rollout and verification.

## Security Controls
- Hash recovery codes with existing PBKDF2 utility.
- Generate high-entropy codes (for example 10 x 12-char base32 or grouped digits with checksum).
- One-time use enforced by UsedAtUtc update in transaction.
- Constant-time verification via existing Verify logic.
- Challenge expiry and max-attempt counters.
- Per-user and per-IP rate limits on start/verify endpoints.
- Generic failure message: Invalid or expired code.
- No sensitive values in logs (OTP/recovery code/token).

### Audit Event Additions
Add consistent events via IAuditService:
- auth.mfa.recovery_codes.issued
- auth.mfa.recovery_code.consume
- auth.mfa.method.remove
- auth.mfa.method.reconfigure.start
- auth.mfa.method.reconfigure.complete
- auth.mfa.method.reconfigure.fail

Track:
- userId
- method
- outcome
- reason (safe string)
- correlation metadata

## Testing Plan

### Unit Tests
- Recovery code generation count/format.
- Hash/verify and one-time consumption behavior.
- Remove/reconfigure lockout prevention rules.
- FIDO2 removal rules with credential counts.

### Controller Tests
- New route metadata for added endpoints.
- Unauthorized/forbidden behavior for missing or wrong token type.
- Happy path + failure path for create/regenerate/remove/reconfigure.

### Integration/Behavior Tests
- Login with recovery code end-to-end.
- Reconfigure sms/email contact end-to-end.
- Remove method while keeping at least one fallback factor.
- Concurrent consume attempts on same recovery code (only one succeeds).

### Abuse/Negative Tests
- Brute-force attempts on recovery code verification.
- Replay of used code.
- Tampered method route values.
- Removing final factor attempt.

## Implementation Sequence (Recommended)
1. Add DTOs, constants, and repository interfaces for recovery codes + method management.
2. Add EF entities/configurations and migration for recovery code tables.
3. Implement repository logic with transactional consume/regenerate behavior.
4. Extend MfaService for recovery code flows and method remove/reconfigure workflows.
5. Add controller endpoints in MfaController (and optional Fido2 credential endpoints).
6. Add audit events and rate-limiting hooks.
7. Add/adjust tests in Authentication.Fido2.Tests.
8. Run full test suite and document endpoint changes.

## File Touch Map (Expected)
- Controllers/MfaController.cs
- Controllers/Fido2Controller.cs (optional device delete/list)
- Services/Interfaces/IMfaService.cs
- Services/Implementatons/MfaService.cs
- Services/Interfaces/IFido2MfaService.cs (optional)
- Services/Implementatons/Fido2MfaService.cs
- Data/Repositories/Interfaces/IUserMfaMethodRepository.cs
- Data/Repositories/Implementations/UserMfaMethodRepository.cs
- Data/Repositories/Interfaces/IFido2CredentialRepository.cs
- Data/Repositories/Implementations/Fido2CredentialRepository.cs
- Data/Configurations/UserMfaMethodConfiguration.cs (optional)
- New recovery code entities + configurations + repositories
- DTOs/Mfa/* (new request/response models)
- Constants/MfaMethodTypes.cs
- Migrations/*
- Authentication.Fido2.Tests/Controllers/*
- Authentication.Fido2.Tests/TestSupport/RecordingServices.cs

## Acceptance Criteria
- User can generate recovery codes and see them only once.
- Recovery code can complete MFA login exactly once per code.
- User can remove an MFA method only when lockout-safety checks pass.
- User can reconfigure sms/email/fido2 through verified flow.
- Audit logs exist for all new flows without sensitive data leakage.
- Existing login and enrollment flows remain backward compatible.
- Route metadata tests include new REST aliases.

## Rollout Notes
- Release behind feature flags if possible:
  - EnableRecoveryCodes
  - EnableMfaMethodManagement
- Monitor failure rates and lockout-related support events after rollout.
- Defer destructive schema changes (legacy IsFido2MfaEnabled removal) to a later release window.
