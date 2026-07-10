# OWASP-Aligned Audit Plan For Penetration Testing

## Scope
This API handles sensitive authentication workflows (password login + FIDO2 MFA). To align with OWASP guidance (OWASP ASVS + OWASP Logging Cheat Sheet), auditing must capture authentication and security-relevant events with tamper-resistant metadata and no sensitive secrets.

## OWASP Goals
- Log all security-relevant events.
- Include who/what/when/where/outcome for each event.
- Avoid logging secrets (passwords, tokens, private keys, full attestation payloads).
- Support incident response and penetration-test evidence.
- Keep logging failures non-blocking for primary authentication flow.

## Audit Tables
1. `SecurityAuditEvents`
- Purpose: canonical security event ledger.
- Captures event category, type, severity, outcome, actor, source, request context, and details JSON.
- Supports investigations and pentest evidence export.

2. `AuthenticationAuditEvents`
- Purpose: authentication-focused telemetry for brute-force, abuse, and MFA troubleshooting.
- Captures stage (password login, FIDO2 options, FIDO2 complete), method, outcome, and reason.
- Supports rate-limit and anomaly analytics.

## Required Fields (Both Tables)
- `OccurredAtUtc`
- `Outcome` (`Success` / `Failure`)
- `IpAddress`
- `UserAgent`
- `CorrelationId` (request traceability)
- `UserId` (nullable where unknown)

## Event Coverage (Implemented)
- Password login success/failure.
- FIDO2 enrollment option issuance success/failure.
- FIDO2 enrollment completion success/failure.
- FIDO2 login option issuance success/failure.
- FIDO2 login completion success/failure.
- Validation failures (invalid/expired/used transaction).

## Data Handling Rules
- Never log request passwords or bearer tokens.
- Never log complete credential public key payloads or authenticator raw payloads.
- Log concise error reason strings safe for operations.
- Keep `DetailsJson` bounded and schema-like.

## Pentest Readiness Checks
- Verify all auth endpoints create at least one audit row per request.
- Verify failed auth attempts are queryable by `IpAddress`, `UsernameOrEmail`, and time window.
- Verify correlation of events by `CorrelationId`.
- Verify PII minimization in logs.
- Verify DB indexes for event lookup are present.

## Operational Follow-up
- Add retention and archival policy (for example: hot 90 days, archive 1 year).
- Add SIEM export job for high-severity events.
- Add alerting rules for repeated failures from same source.
