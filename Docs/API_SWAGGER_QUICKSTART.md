# API Swagger Quickstart

## Purpose

This guide provides the shortest valid backend testing path for the current API using Swagger.

Scope:

- user creation
- login
- login-time MFA bootstrap enrollment
- access token authorization
- MFA enrollment after management step-up
- MFA login challenge
- MFA management step-up

Out of scope:

- UI flows
- browser WebAuthn/FIDO2 ceremony details

## Base URL

Use Swagger at:

- `http://localhost:5190/swagger`

## Token Model

You will use two token types:

1. Full access token
- Used for authenticated operations such as setup options and management-session creation.

2. MFA token
- Returned only when login requires MFA.
- Used only for:
  - `POST /api/mfa/challenges`
  - `PATCH /api/mfa/challenges/current`
  - `POST /api/fido2/authentications`
  - `PATCH /api/fido2/authentications/current`

3. Login enrollment token
- Returned only when login requires MFA setup before authentication can complete.
- Used only for:
  - `POST /api/mfa/login-enrollments`
  - `PATCH /api/mfa/login-enrollments/current`
  - `POST /api/mfa/login-enrollment-sessions/complete`

## Swagger Authorization

Use the `Authorize` button in Swagger.

When you test protected endpoints, paste:

```text
Bearer <token>
```

Replace the token depending on the flow:

- use full access token for standard authenticated endpoints
- use MFA token for MFA login challenge endpoints
- use login enrollment token only for login bootstrap enrollment endpoints

## 1. Create a User

Endpoint:

- `POST /api/users`

Example body:

```json
{
  "username": "swaggeruser1",
  "email": "swaggeruser1@example.com",
  "password": "StrongPass123!"
}
```

Expected result:

- user is created
- response returns user id and identity fields

## 2. Login

Endpoint:

- `POST /api/sessions`

Example body:

```json
{
  "username": "swaggeruser1",
  "password": "StrongPass123!"
}
```

Possible results:

1. `Authenticated`
- response contains `accessToken` and `refreshToken`

2. `RequiresMfa`
- response contains:
  - `mfaToken`
  - `allowedMfaMethods`
  - `mfaExpiresIn`

3. `RequiresEnrollment`
- response contains:
  - `enrollmentToken`
  - `enrollmentExpiresIn`
  - `enrollmentSessionId`
  - `enrollmentContinuationToken`
  - `availableMfaSetupOptions`

## 3. If Login Returns RequiresEnrollment

Authorize Swagger with the login enrollment token.

### 3.1 Start Login-Time Enrollment

Endpoint:

- `POST /api/mfa/login-enrollments`

Example body:

```json
{
  "continuationToken": "PUT-THE-SESSION-CONTINUATION-TOKEN-HERE",
  "method": "email",
  "contactValue": "swaggeruser1@example.com"
}
```

Save these response fields:

- `enrollmentSessionId`
- `enrollmentTransactionId`
- `sessionContinuationToken`
- `challengeContinuationToken`

### 3.2 Verify Login-Time Enrollment

Endpoint:

- `PATCH /api/mfa/login-enrollments/current`

Example body:

```json
{
  "enrollmentTransactionId": "PUT-THE-ENROLLMENT-TRANSACTION-ID-HERE",
  "continuationToken": "PUT-THE-CHALLENGE-CONTINUATION-TOKEN-HERE",
  "code": "123456"
}
```

Expected result:

- the method is verified
- you receive a rotated `sessionContinuationToken`
- you may optionally enroll another method while staying inside the bootstrap session

### 3.3 Complete Login Bootstrap Enrollment Session

Endpoint:

- `POST /api/mfa/login-enrollment-sessions/complete`

Example body:

```json
{
  "enrollmentSessionId": "PUT-THE-ENROLLMENT-SESSION-ID-HERE",
  "continuationToken": "PUT-THE-LATEST-SESSION-CONTINUATION-TOKEN-HERE"
}
```

Expected result:

- full `accessToken`
- `refreshToken`
- bootstrap token/session consumed

## 4. If Login Returns Authenticated

Authorize Swagger using the full access token.

### 4.1 Check Current MFA State

Endpoint:

- `GET /api/mfa/methods`

### 4.2 Check Remaining Setup Options

Endpoint:

- `GET /api/mfa/setup-options`

Expected result:

- `allowedMfaMethods`
- `availableMfaSetupOptions`

## 5. Add MFA Methods from Settings Requires Step-Up

Do not call enrollment endpoints directly with only a full access token.

The current security model requires management step-up before enrolling additional methods from settings.

Use the full access token.

### 5.1 Start Management Session

Endpoint:

- `POST /api/mfa/management-sessions`

Save from response:

- `mfaTransactionId`
- `continuationToken`
- `availableMethods`

### 5.2 Start Step-Up Challenge

Endpoint:

- `POST /api/mfa/management-sessions/challenges/start`

Example body:

```json
{
  "mfaTransactionId": "PUT-THE-MANAGEMENT-SESSION-ID-HERE",
  "continuationToken": "PUT-THE-CONTINUATION-TOKEN-HERE",
  "method": "email"
}
```

### 5.3 Verify Step-Up Challenge

Endpoint:

- `POST /api/mfa/management-sessions/challenges/verify`

Example body:

```json
{
  "mfaTransactionId": "PUT-THE-MANAGEMENT-SESSION-ID-HERE",
  "continuationToken": "PUT-THE-ROTATED-CONTINUATION-TOKEN-HERE",
  "code": "123456"
}
```

### 5.4 Start Additional Enrollment After Step-Up

Endpoint:

- `POST /api/mfa/enrollments`

Example body:

```json
{
  "method": "email",
  "contactValue": "swaggeruser1@example.com"
}
```

Save:

- `enrollmentTransactionId`
- `continuationToken`

### 5.5 Verify Additional Enrollment

Endpoint:

- `PATCH /api/mfa/enrollments/current`

Example body:

```json
{
  "enrollmentTransactionId": "PUT-THE-ID-HERE",
  "continuationToken": "PUT-THE-CONTINUATION-TOKEN-HERE",
  "code": "123456"
}
```

### 5.6 Complete Management Session

Endpoint:

- `POST /api/mfa/management-sessions/complete`

Example body:

```json
{
  "mfaTransactionId": "PUT-THE-MANAGEMENT-SESSION-ID-HERE",
  "continuationToken": "PUT-THE-LATEST-CONTINUATION-TOKEN-HERE"
}
```

## 6. Login Again to Enter MFA Challenge Flow

Call:

- `POST /api/sessions`

If MFA is enabled, the response should now return:

- `status = RequiresMfa`
- `mfaToken`
- `allowedMfaMethods`

Authorize Swagger again, but this time with the MFA token.

## 7. Complete MFA Login with SMS, Email, or Recovery Code

### 7.1 Start MFA Challenge

Endpoint:

- `POST /api/mfa/challenges`

Example body:

```json
{
  "method": "email",
}
```

Save these response fields:

- `mfaTransactionId`
- `continuationToken`

### 7.2 Verify MFA Challenge

Endpoint:

- `PATCH /api/mfa/challenges/current`

Example body:

```json
{
  "continuationToken": "PUT-THE-CONTINUATION-TOKEN-HERE",
  "code": "123456"
}
```

Expected result:

- response returns full `accessToken` and `refreshToken`
- the MFA temp token session is consumed on success

## 8. Common Failure Cases

### 401 Unauthorized

Typical causes:

- wrong token type in Swagger
- expired token
- missing `Bearer ` prefix
- attempting login bootstrap enrollment with a full access token or MFA token

### 409 Conflict

Typical causes:

- flow already advanced
- reused continuation token
- replayed verify request

### 410 Gone

Typical causes:

- challenge or enrollment expired

### 429 Too Many Requests

Typical causes:

- throttling/rate-limit behavior
- check `Retry-After` header

## 9. FIDO2 Note

FIDO2 endpoints are documented in Swagger, but a full valid request usually requires browser-generated WebAuthn payloads.

Relevant endpoints:

- `POST /api/fido2/enrollments`
- `PATCH /api/fido2/enrollments/current`
- `POST /api/fido2/authentications`
- `PATCH /api/fido2/authentications/current`

These are better tested from a browser client or dedicated WebAuthn-capable test harness.

## 10. Fastest End-to-End Backend Test

1. `POST /api/users`
2. `POST /api/sessions`
3. If `RequiresEnrollment`, authorize with login enrollment token
4. `POST /api/mfa/login-enrollments`
5. `PATCH /api/mfa/login-enrollments/current`
6. `POST /api/mfa/login-enrollment-sessions/complete`
7. Authorize with full access token
8. `POST /api/sessions`
9. Authorize with MFA token
10. `POST /api/mfa/challenges`
11. `PATCH /api/mfa/challenges/current`
12. Authorize with the new full access token
13. `POST /api/mfa/management-sessions`
14. `POST /api/mfa/management-sessions/challenges/start`
15. `POST /api/mfa/management-sessions/challenges/verify`
16. `POST /api/mfa/enrollments`
17. `PATCH /api/mfa/enrollments/current`
18. `POST /api/mfa/management-sessions/complete`
