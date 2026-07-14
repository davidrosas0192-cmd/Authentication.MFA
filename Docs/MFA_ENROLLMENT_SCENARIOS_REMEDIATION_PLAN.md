# MFA Enrollment Scenarios Remediation Plan

## Objective

Define a secure remediation plan for two enrollment-related scenarios before implementation.

This document focuses on:

1. Login-time enrollment when MFA is required by policy but the user has no configured factor.
2. Additional enrollments from an already authenticated session.

This document does not implement changes. It defines the target design, security model, API changes, and rollout sequence.

## Security Problem Summary

### Scenario 1

A user starts login, MFA is required by policy, but the user has no configured factor yet.

Current risk:

- If the API returns a full access token before enrollment is completed, the user can continue from the login page.
- After completing one enrollment, the same token could still be reused to enroll more methods.
- If that token is stolen, an attacker may be able to keep adding MFA methods or continue the bootstrap flow beyond the intended boundary.

### Scenario 2

A user is already fully authenticated and opens a settings screen to add more MFA methods.

Current risk:

- A long-lived full access token alone may be enough to continue adding new MFA methods.
- If the full access token is stolen, the attacker could add factors and move toward account takeover.

## Design Principles

The remediation must follow these principles:

1. No full authentication until required login-time enrollment is explicitly completed.
2. Sensitive MFA lifecycle actions must not rely only on a standard full access token.
3. Every multi-step flow must use short-lived, purpose-bound, rotating continuation state.
4. Reuse of prior step state must fail with conflict semantics.
5. Partial flow tokens must be least-privilege and allow only the exact endpoints needed.
6. Finalization must revoke or consume transitional tokens immediately.
7. Enrollment from account settings must require a fresh step-up.

## OWASP / Best-Practice Alignment

This plan is aligned with the architecture and security direction already documented in:

- `Docs/FINAL_BACKEND_TECHNICAL_DOCUMENTATION.md`
- `Docs/MFA_TWILIO_OWASP_CHANGE_EVALUATION_PLAN.md`
- `Docs/MFA_TWILIO_OWASP_GAP_MATRIX.md`
- `Docs/PENETRATION_HARDENING_TOKEN_DRIVEN_PLAN.md`
- `Docs/OWASP_PEN_TEST_CHECKLIST.md`

Relevant OWASP-aligned principles:

- reauthentication for sensitive actions
- least-privilege session scopes
- short-lived tokens for transitional states
- replay resistance with server-side invalidation
- secure MFA factor lifecycle management

## Target Solution Overview

Use two different protected flow types:

1. `login_enrollment`
- used only when login cannot complete because policy requires MFA setup first
- does not grant normal authenticated access
- can call only login-bootstrap enrollment endpoints

2. `manage_mfa`
- used only after full authentication for sensitive MFA administration
- must be unlocked by explicit step-up
- required for adding more methods from account settings, removing methods, and reconfiguring methods

## Scenario 1 Solution: Login-Time Enrollment Session

### Goal

Allow the user to enroll one or more MFA methods during login bootstrap without granting a reusable full access token before the bootstrap flow is explicitly completed.

### Required Behavior

1. User submits credentials.
2. If MFA is required by policy and no factor is configured, the API must not return a full access token.
3. Instead, the API returns a dedicated `login_enrollment` token/session.
4. The user may enroll one method.
5. The user may optionally enroll more methods while staying inside the same `login_enrollment` session.
6. Each step rotates continuation state.
7. Once the user clicks continue/finish, the bootstrap session is explicitly completed.
8. Only then is the full access token issued.
9. The `login_enrollment` token/session is immediately consumed or revoked.

### Recommended API Shape

#### 1. Login response

Current `POST /api/sessions` should gain a new state, for example:

- `RequiresEnrollment`

Response should include:

- `status = RequiresEnrollment`
- `enrollmentToken`
- `enrollmentExpiresIn`
- `enrollmentSessionId`
- `continuationToken`
- `availableSetupOptions`
- optional policy flags such as `minimumRequiredMethods`

#### 2. Login-time enrollment start

New or specialized endpoint, protected only by `login_enrollment` token:

- `POST /api/mfa/login-enrollments`

Purpose:

- start enrollment for sms/email or begin FIDO2 bootstrap enrollment

#### 3. Login-time enrollment verify/complete step

- `PATCH /api/mfa/login-enrollments/current`

Purpose:

- complete the current enrollment step
- rotate the continuation token
- optionally return updated `availableSetupOptions`

#### 4. Login-time enrollment session completion

- `POST /api/mfa/login-enrollment-sessions/complete`

Purpose:

- explicitly finalize the bootstrap process
- consume the `login_enrollment` session
- issue the full access token and refresh token

#### 5. Optional cancellation

- `DELETE /api/mfa/login-enrollment-sessions/current`

Purpose:

- revoke the enrollment bootstrap session
- require re-login

### Session and Token Rules

The `login_enrollment` token/session must:

- be short-lived
- carry a purpose claim such as `token_type=login_enrollment`
- be bound to user id and enrollment session id
- be rejected on normal authenticated endpoints
- be rejected on standard MFA challenge endpoints
- be consumed immediately when the bootstrap flow is completed

### Why This Fixes the Risk

If the token is stolen:

- it is not a full access token
- it cannot access the rest of the account
- it can only continue the current bootstrap scope
- old continuation values become invalid after each step
- once completion occurs, the session becomes unusable

## Scenario 2 Solution: Settings Enrollment Requires Step-Up

### Goal

Prevent a normal full access token from being sufficient to add MFA methods from settings.

### Required Behavior

1. User is already fully authenticated.
2. User wants to add another MFA method from settings.
3. API requires a `manage_mfa` step-up session first.
4. After successful step-up, the user may start additional enrollments inside a short management window.
5. Once the operation is done, the management session is completed or expires quickly.

### Target Rule

Adding a new MFA method from settings must be treated as a sensitive MFA-administration operation, the same class of risk as:

- removing a method
n- reconfiguring a method
- regenerating recovery codes

### Recommended API Behavior

Reuse the existing management-session architecture:

- `POST /api/mfa/management-sessions`
- `POST /api/mfa/management-sessions/challenges/start`
- `POST /api/mfa/management-sessions/challenges/verify`
- `POST /api/mfa/management-sessions/complete`

Then require a valid recent management step-up before allowing:

- `POST /api/mfa/enrollments`
- `PATCH /api/mfa/enrollments/current`
- `POST /api/fido2/enrollments`
- `PATCH /api/fido2/enrollments/current`

### Why This Fixes the Risk

If a full access token is stolen:

- attacker still cannot add MFA methods directly
- attacker must first complete step-up using an existing factor
- step-up window is short-lived and auditable

## Data Model Changes

### 1. New dedicated login enrollment session

Add a dedicated entity/table, for example:

- `MfaLoginEnrollmentSession`

Suggested fields:

- `Id`
- `UserId`
- `Status`
- `ContinuationToken`
- `StepVersion`
- `ExpiresAtUtc`
- `CompletedAtUtc`
- `CreatedAtUtc`
- `UpdatedAtUtc`

Suggested statuses:

- `EnrollmentRequired`
- `EnrollmentInProgress`
- `ReadyToComplete`
- `Completed`
- `Cancelled`
- `Expired`

### 2. Optional purpose expansion

If reusing a more generic flow-session model is preferred, define an immutable purpose such as:

- `login_enrollment`
- `login_challenge`
- `manage_mfa`
- `account_recovery`

### 3. Keep continuation-token rotation

Every flow step must maintain:

- `ContinuationToken`
- `StepVersion`

This is already aligned with current `MfaChallenge` and `MfaManagementSession` behavior.

## Endpoint Authorization Rules

### `login_enrollment` token can access only

- login bootstrap enrollment start
- login bootstrap enrollment verify
- login bootstrap completion
- login bootstrap cancel

### full access token can access

- normal authenticated endpoints
- read-only setup discovery
- management-session creation

### full access token cannot by itself access

- sensitive MFA administration without recent step-up
- direct add-factor from settings without management step-up

### `manage_mfa` validated context can access

- add factor from settings
- remove method
- reconfigure method
- regenerate recovery codes

## Response and Error Semantics

### Recommended statuses

- `401` for wrong token type or invalid token
- `403` for missing required step-up
- `409` for reused continuation token or already-advanced flow
- `410` for expired flow/challenge/session
- `429` for throttling

### Required behavior

- replay or stale continuation token must fail with conflict
- expired bootstrap or management session must fail with gone
- error responses should stay generic and not reveal unnecessary state

## Audit Requirements

Add or formalize audit events for:

### Login enrollment bootstrap

- `auth.mfa.login_enrollment.start`
- `auth.mfa.login_enrollment.step.completed`
- `auth.mfa.login_enrollment.completed`
- `auth.mfa.login_enrollment.cancelled`
- `auth.mfa.login_enrollment.expired`

### Sensitive settings enrollments

- `auth.mfa.method.enrollment.start`
- `auth.mfa.method.enrollment.complete`
- `auth.mfa.method.enrollment.fail`
- `auth.mfa.management_session.start`
- `auth.mfa.management_session.stepup.completed`

No audit row should log:

- OTP value
- bearer token
- continuation token
- raw WebAuthn payloads

## Implementation Plan

### Phase 1: Introduce login enrollment session

1. Add entity + migration for `MfaLoginEnrollmentSession`.
2. Add token issuance path from login for `RequiresEnrollment`.
3. Add start/verify/complete/cancel endpoints for login bootstrap enrollment.
4. Add tests for token scope and completion semantics.

### Phase 2: Restrict settings enrollment behind step-up

1. Extend service policy so adding methods requires recent management step-up.
2. Reuse existing management-session endpoints.
3. Block direct enrollment from settings if no recent step-up exists.
4. Add tests for unauthorized and forbidden cases.

### Phase 3: Hardening

1. Add throttling and replay tests.
2. Add strict expiry semantics for session completion.
3. Add explicit documentation updates.

## Testing Plan

### Scenario 1 tests

1. Login with no configured factor and enrollment required returns `RequiresEnrollment`, not full access token.
2. `login_enrollment` token cannot call normal authenticated endpoints.
3. User can complete one enrollment and then optionally start a second within the same bootstrap session.
4. Each step rotates continuation token.
5. Reusing old continuation token fails with `409`.
6. Completing the bootstrap session revokes/consumes the bootstrap token.
7. After completion, old bootstrap token cannot enroll more methods.

### Scenario 2 tests

1. Full access token alone cannot add an MFA method from settings.
2. Starting add-factor from settings without step-up returns `403`.
3. After management step-up, add-factor succeeds.
4. Expired management session blocks further enrollment attempts.

## Acceptance Criteria

1. Login bootstrap enrollment does not issue full access token before explicit completion.
2. Additional enrollments during login bootstrap stay inside a short-lived, purpose-limited session.
3. Completing login bootstrap consumes the partial token/session immediately.
4. Full access token alone cannot add MFA methods from settings.
5. Settings-based enrollment requires recent management step-up.
6. Replay and stale-step attempts fail consistently.
7. Audit coverage exists for both scenarios.

## Decision Summary

### Scenario 1

Use a dedicated `login_enrollment` token/session.

Do not issue full access token until bootstrap enrollment is explicitly completed.

### Scenario 2

Treat adding MFA methods from settings as a sensitive operation.

Require management step-up before additional enrollments.

## Recommended Next Step

After approval of this plan, implementation should start with the login bootstrap enrollment session model before changing settings-based enrollment policy.
