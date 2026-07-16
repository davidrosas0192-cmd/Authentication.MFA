# Users API

Base URL: `/api/users`

---

## POST /api/users — Create User

Creates a new user account. No authentication required.

**Request**
```json
{
  "username": "jdoe",
  "email": "jdoe@example.com",
  "password": "MySecureP@ssword1"
}
```

**Response 200**
```json
{
  "success": true,
  "message": "User created successfully.",
  "data": {
    "userId": 42,
    "username": "jdoe",
    "email": "jdoe@example.com"
  }
}
```

**Response 409 — Username/email already taken**
```json
{
  "status": 409,
  "title": "Conflict",
  "detail": "Username or email is already in use."
}
```

---

## POST /api/sessions — Login

Authenticates a user with username/password. Returns either a full access token (no MFA) or an MFA token to continue the flow.

**Request**
```json
{
  "username": "jdoe",
  "password": "MySecureP@ssword1"
}
```

**Response 200 — Authenticated (no MFA)**
```json
{
  "success": true,
  "message": "Login successful.",
  "data": {
    "status": "Authenticated",
    "mfaRequired": false,
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2ggdG9rZW4...",
    "expiresIn": 900,
    "allowedMfaMethods": []
  }
}
```

**Response 200 — MFA required**
```json
{
  "success": true,
  "message": "MFA required.",
  "data": {
    "status": "MfaRequired",
    "mfaRequired": true,
    "mfaToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "mfaExpiresIn": 300,
    "allowedMfaMethods": ["sms", "email", "recovery_code"]
  }
}
```

**Response 200 — MFA enrollment required at login**
```json
{
  "success": true,
  "message": "MFA enrollment required.",
  "data": {
    "status": "EnrollmentRequired",
    "mfaRequired": true,
    "enrollmentToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "enrollmentExpiresIn": 600,
    "enrollmentSessionId": "a1b2c3d4-...",
    "enrollmentContinuationToken": "tok_abc123",
    "availableMfaSetupOptions": ["sms", "email", "fido2"]
  }
}
```

**Response 401 — Invalid credentials**
```json
{
  "status": 401,
  "title": "Unauthorized",
  "detail": "Invalid username or password."
}
```

**Response 429 — Rate limited**
```json
{
  "status": 429,
  "title": "Too Many Requests",
  "detail": "Too many login attempts. Please try again later."
}
```

---

## DELETE /api/sessions/current — Logout

Revokes the current access token session.

**Headers**
```
Authorization: Bearer <access_token>
```

**Response 200**
```json
{
  "success": true,
  "message": "Logged out successfully."
}
```

---

## POST /api/sessions/refresh — Refresh Token

Issues a new access token and refresh token, revoking the old refresh token.

**Request**
```json
{
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2ggdG9rZW4..."
}
```

**Response 200**
```json
{
  "success": true,
  "message": "Token refreshed successfully.",
  "data": {
    "status": "Authenticated",
    "mfaRequired": false,
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "bmV3UmVmcmVzaFRva2VuSGVyZQ...",
    "expiresIn": 900
  }
}
```

**Response 401 — Invalid or expired refresh token**
```json
{
  "status": 401,
  "title": "Unauthorized",
  "detail": "Invalid or expired refresh token."
}
```

---

## DELETE /api/mfa/sessions/current — Cancel MFA Authentication

Cancels an in-progress MFA authentication and invalidates the MFA temp token.

**Headers**
```
Authorization: Bearer <mfa_token>
```

**Response 200**
```json
{
  "success": true,
  "message": "MFA authentication cancelled."
}
```
