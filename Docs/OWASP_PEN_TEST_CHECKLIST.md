# OWASP Penetration Test Checklist

This checklist is intended for manual security review and penetration testing of the Authentication Fido2 REST API.

## Goals

- Verify authentication and MFA flows are resistant to tampering, replay, enumeration, and token abuse.
- Verify audit records are complete, bounded, and free of secrets.
- Verify error handling does not reveal internal state or account existence.
- Verify endpoints follow REST-oriented resource semantics where practical for authentication ceremonies.

## Pre-Test Readiness

- [ ] HTTPS is enforced in the target environment.
- [ ] FIDO2 origins are restricted to exact allowed origins.
- [ ] Twilio and JWT secrets are stored in a secret manager or environment variables.
- [ ] Audit tables exist and are writable.
- [ ] Correlation IDs are propagated for requests.
- [ ] Logging is configured to avoid plaintext passwords, OTPs, and bearer tokens.
- [ ] Rate limiting and brute-force controls are enforced for login, OTP verify, and public user creation.

## Endpoint Coverage Checklist

### Public user creation

- [ ] `POST /api/users` accepts valid user data.
- [ ] `POST /api/users` rejects invalid payloads with generic validation errors.
- [ ] `POST /api/users` does not leak whether username or email already exists.
- [ ] `POST /api/users` is rate limited.
- [ ] `POST /api/users` creates an audit trail entry.

### Login and token lifecycle

- [ ] `POST /api/auth/login` returns access and refresh tokens on success.
- [ ] `POST /api/auth/login` returns MFA token and allowed methods when MFA is required.
- [ ] `POST /api/auth/login` uses generic failure messages for invalid credentials.
- [ ] `POST /api/auth/logout` revokes the current access-token session.
- [ ] `POST /api/auth/cancel-authentication` revokes the current MFA token session.
- [ ] Successful login invalidates prior active sessions for the user.
- [ ] Login failure attempts are auditable and searchable by correlation id, IP, and username/email.

### MFA methods and setup

- [ ] `GET /api/mfa/methods` returns only methods enabled for the authenticated user.
- [ ] `GET /api/mfa/devices/available` returns allowed methods plus remaining setup options.
- [ ] `GET /api/mfa/methods` and `GET /api/mfa/devices/available` emit audit events.
- [ ] `POST /api/mfa/enrollment/start` requires a full access token.
- [ ] `POST /api/mfa/enrollment/verify` requires a full access token.
- [ ] Enrollment responses do not expose secrets or provider tokens.
- [ ] Enrollment start and verify actions are auditable.

### MFA challenges

- [ ] `POST /api/mfa/challenges/start` requires the MFA temp token scheme.
- [ ] `POST /api/mfa/challenges/verify` requires the MFA temp token scheme.
- [ ] Challenge context is resolved server-side from MFA token claims and session state.
- [ ] Request-body transaction identifiers are not required for OTP challenge start/verify.
- [ ] Replayed or expired MFA tokens are rejected.
- [ ] OTP brute-force attempts are rate limited and auditable.

### FIDO2 enrollment and login

- [ ] `POST /api/fido2/enrollment/options` requires a full access token.
- [ ] `POST /api/fido2/enrollment/complete` requires a full access token.
- [ ] `POST /api/fido2/enrollment/complete` is bound to the authenticated user that created the transaction.
- [ ] `POST /api/fido2/login/options` requires the MFA temp token scheme.
- [ ] `POST /api/fido2/login/complete` requires the MFA temp token scheme.
- [ ] WebAuthn attestation and assertion payloads are not logged in full.
- [ ] FIDO2 transaction replay is rejected.
- [ ] FIDO2 origin validation matches the configured application origin exactly.

## Audit and Logging Checklist

- [ ] AuthenticationAuditEvents are written for login, MFA, and FIDO2 flows.
- [ ] SecurityAuditEvents are written for security-relevant events and failures.
- [ ] Public user creation is audited.
- [ ] Logs include `CorrelationId`, user id when known, outcome, IP, and user agent.
- [ ] Logs do not include passwords, OTP codes, refresh tokens, access tokens, or raw credential payloads.
- [ ] Failed tampering attempts are captured as audit entries.
- [ ] Audit rows remain bounded and queryable by time, user, IP, and outcome.

## Error Handling Checklist

- [ ] Invalid credentials return a generic message.
- [ ] Invalid MFA token returns a generic message.
- [ ] Invalid OTP returns a generic message.
- [ ] Invalid or expired FIDO2 transactions return a generic message.
- [ ] Authorization failures do not reveal account existence.
- [ ] 4xx responses are consistent and do not leak stack traces or internal exception details.

## Operational Checklist

- [ ] Hot retention exists for recent audit events.
- [ ] Archive or export exists for older audit events.
- [ ] SIEM export is available for high-severity security events.
- [ ] Alerts exist for repeated failed logins, OTP failures, and replay attempts.
- [ ] Test data is removed before production pentesting.

## Review Notes

- Preferred REST aliases exist for session and ceremony-style endpoints, but legacy routes remain for compatibility.
- Ceremony endpoints are acceptable in REST APIs when they represent state transitions rather than resource CRUD.
- The highest-risk areas for pentesting are login, OTP verification, FIDO2 completion, and public user creation.