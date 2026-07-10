# Penetration Hardening Plan (Token-Driven MFA Flow)

## Execution Status
- Implemented.
- OTP challenge start/verify no longer require `mfaTransactionId` in request body.
- Controller and service flow are now token-driven for MFA transaction context.
- Frontend challenge forms no longer send transaction id.
- API docs updated to reflect the hardened REST contract.

## Objective
Strengthen the MFA login flow for penetration testing by removing client-controlled transaction coupling in request payloads and enforcing a server-trusted token-driven model.

## Security Goal
Use the MFA JWT claims (`mfa_tx`, `jti`, `sub`) and server-side session checks as the only source of truth for MFA transaction context.

## Current Risk Summary
- Parameter tampering risk has been reduced by removing request-body transaction coupling.
- Remaining abuse cases are handled by token/session validation and short-lived challenge TTLs.
- Pentesting focus should now be on replay attempts, expired token use, and session mismatch.

## Final Design
1. Keep issuing `mfaTransactionId` during login for traceability.
2. Do not require `mfaTransactionId` in MFA verification request bodies.
3. Resolve transaction context only from MFA token claims plus DB session validation.
4. Keep one active session policy and token revocation lifecycle.

## Scope Of Changes

### 1) DTO Contract Simplification
Purpose: remove tamperable transaction parameters from client payloads in MFA verification stage.

Files to modify:
- `DTOs/Mfa/StartMfaChallengeRequest.cs`
- `DTOs/Mfa/VerifyMfaChallengeRequest.cs`

Changes:
- Remove `MfaTransactionId` from request DTOs.
- Keep only method/code fields needed by business logic.

### 2) Controller Context Resolution
Purpose: bind all MFA challenge operations to token claims only.

Files to modify:
- `Controllers/MfaController.cs`

Changes:
- In `StartChallenge` and `VerifyChallenge`, stop comparing body transaction id.
- Use `ValidateMfaTokenContext` output (`MfaTransactionId`) directly.
- Build service requests internally from token context.

### 3) Service Method Signatures
Purpose: avoid passing transaction identifiers from external caller when token context is authoritative.

Files to modify:
- `Services/Interfaces/IMfaService.cs`
- `Services/Implementatons/MfaService.cs`

Changes:
- Update service signatures to accept transaction id from controller context argument, not from external body field.
- Keep challenge purpose/status/expiry checks unchanged.

### 4) Frontend Client Payload Cleanup
Purpose: align UI with hardened API contract and remove redundant transaction inputs.

Files to modify:
- `wwwroot/index.html`
- `wwwroot/app.js`

Changes:
- Remove manual MFA transaction input dependencies for challenge start/verify.
- Keep transaction display for observability only (optional read-only).
- Send only required payload fields.

### 5) API Documentation Alignment
Purpose: ensure pentest and QA use correct hardened contracts.

Files to modify:
- `Docs/API_ENDPOINT_FLOW_GUIDE.md`
- `Docs/README.md` (if endpoint examples are duplicated there)

Changes:
- Update examples for challenge start/verify without transaction id in request body.
- Add note: transaction context is derived from MFA token claims and session.

### 6) Audit Enhancements For Pentest Evidence
Purpose: provide forensic traceability for tampering/replay attempts.

Files to review/modify:
- `Services/Implementatons/AuditService.cs`
- `Services/Implementatons/MfaService.cs`
- `Services/Implementatons/Fido2MfaService.cs`

Changes:
- Add explicit security event types for:
  - token session mismatch
  - transaction mismatch
  - revoked/expired token usage
  - replayed FIDO2 transaction usage

## Additional Recommended Hardening (Pentest-Focused)
1. Add rate limits for:
- password login endpoint
- OTP challenge verification endpoint
- FIDO2 login completion endpoint

2. Add account/IP lockout strategy for repeated OTP failures.

3. Ensure all auth errors remain generic to avoid user enumeration.

4. Keep strict TTLs:
- MFA temp token short expiration
- MFA challenge short expiration
- consume/revoke immediately after successful completion

5. Ensure CORS/origin restrictions remain exact for WebAuthn origins.

## Testing Plan

### Functional Tests
1. `RequiresMfa` login returns MFA token and allowed methods.
2. Start/verify challenge succeeds without body transaction id.
3. FIDO2 login options/complete succeeds with token-bound context.
4. Full auth session can still call `/api/mfa/devices/available`.

### Negative Security Tests
1. Replay old MFA token -> 401.
2. Reuse consumed MFA challenge -> blocked.
3. Use revoked full token after new login -> 401.
4. Use mismatched FIDO2 assertion transaction -> blocked.

### Pentest Checklist Evidence
1. Parameter tampering does not alter MFA transaction context.
2. Token/session revocation is enforced server-side.
3. Security audit entries exist for failed tampering attempts.

## Implementation Order
1. DTO updates.
2. Controller updates.
3. Service signature and implementation updates.
4. Frontend payload updates.
5. Documentation updates.
6. Build + regression tests.

## Current State

- Completed: DTO, controller, service, frontend, and documentation updates.
- Verified: build passes after the hardened flow changes.
- Open only if desired: rate limits, lockout policy tuning, and additional security audit event types.

## Rollback Strategy
1. Keep migration-free API contract changes in a dedicated commit.
2. If needed, reintroduce deprecated DTO fields temporarily but ignore them server-side.
3. Do not remove token/session validation paths during rollback.

## Definition Of Done
1. No MFA transaction id required from client request body for challenge start/verify.
2. All MFA context derived from validated token claims + DB session.
3. Frontend fully compatible with hardened contract.
4. Docs and test evidence updated for pentesting.
