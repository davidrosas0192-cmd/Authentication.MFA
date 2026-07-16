# MFA API

Base URL: `/api/mfa`

All endpoints require a `Bearer` access token unless noted with a different scheme.

**Auth Schemes:**
- `Bearer <access_token>` — Standard JWT from login
- `Bearer <mfa_token>` — Short-lived MFA challenge token (5 min)
- `Bearer <enrollment_token>` — Login-time enrollment token (10 min)

---

## Guía de flujos

Existen tres flujos independientes que pueden confundirse:

| Flujo | ¿Quién lo usa? | Token | Objetivo |
|-------|---------------|-------|----------|
| **Enrollment** | Usuario ya autenticado | `access_token` | Agregar un método MFA nuevo a su cuenta |
| **Login Enrollment** | Usuario sin MFA, durante el primer login | `enrollment_token` | Obligar a configurar MFA antes de entrar por primera vez |
| **Management Session** | Usuario ya autenticado | `access_token` | Eliminar o reconfigurar un método existente (requiere step-up) |

- **Enrollment** y **Login Enrollment** agregan métodos, pero en contextos distintos: uno desde adentro de la app ya autenticado, el otro durante el login sin acceso aún.
- **Management Session** nunca agrega métodos — solo permite operaciones destructivas (eliminar, cambiar contacto) y exige re-verificar identidad como capa de seguridad extra antes de ejecutarlas.

---

## GET /api/mfa/methods — Get Allowed MFA Methods

Returns the user's currently active MFA methods.

**Headers**
```
Authorization: Bearer <access_token>
```

**Response 200**
```json
{
  "success": true,
  "data": {
    "allowedMfaMethods": ["sms", "email", "recovery_code"]
  }
}
```

---

## GET /api/mfa/setup-options — Get Setup Options

Returns both allowed methods and methods still available to set up.

**Headers**
```
Authorization: Bearer <access_token>
```

**Response 200**
```json
{
  "success": true,
  "data": {
    "allowedMfaMethods": ["sms"],
    "availableMfaSetupOptions": ["email", "fido2"]
  }
}
```

---

## POST /api/mfa/challenges — Start MFA Challenge

Initiates an OTP delivery for the chosen method. Requires the MFA temp token from login.

**Headers**
```
Authorization: Bearer <mfa_token>
```

**Request**
```json
{
  "method": "sms"
}
```

> Valid values: `sms`, `email`, `recovery_code`, `fido2`

**Response 200**
```json
{
  "success": true,
  "message": "OTP sent via sms.",
  "data": {
    "mfaTransactionId": "d4e5f6a7-1234-5678-abcd-ef0123456789",
    "continuationToken": "tok_xyz789",
    "method": "sms",
    "status": "pending",
    "expiresAtUtc": "2026-07-16T12:05:00Z"
  }
}
```

**Response 429 — Too many challenge starts**
```json
{
  "status": 429,
  "title": "Too Many Requests",
  "detail": "Too many challenge requests. Please try again later."
}
```

---

## PATCH /api/mfa/challenges/current — Verify MFA Challenge

Verifies the OTP code. On success, returns full access + refresh tokens.

**Headers**
```
Authorization: Bearer <mfa_token>
```

**Request**
```json
{
  "continuationToken": "tok_xyz789",
  "code": "847201"
}
```

> For `recovery_code` method, pass the recovery code as `code` (e.g. `"ABCD-EFGH-WXYZ"`)

**Response 200 — Verified**
```json
{
  "success": true,
  "message": "MFA verification succeeded.",
  "data": {
    "status": "Authenticated",
    "mfaRequired": false,
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "bmV3UmVmcmVzaFRva2VuSGVyZQ...",
    "expiresIn": 900,
    "allowedMfaMethods": ["sms", "email"]
  }
}
```

**Response 401 — Invalid code**
```json
{
  "status": 401,
  "title": "Unauthorized",
  "detail": "Invalid OTP code."
}
```

**Response 429 — Challenge locked after 5 failed attempts**
```json
{
  "status": 429,
  "title": "Too Many Requests",
  "detail": "Too many failed verification attempts. Please try again later."
}
```

---

## POST /api/mfa/enrollments — Start MFA Enrollment

> **Flujo: Enrollment** — El usuario ya está completamente autenticado (tiene `access_token`) y quiere agregar un nuevo método MFA a su cuenta (ej. agregar SMS además del email que ya tenía). No interrumpe la sesión activa.

Begins enrollment of a new MFA method for an authenticated user.

**Headers**
```
Authorization: Bearer <access_token>
```

**Request**
```json
{
  "method": "sms",
  "contactValue": "+15551234567"
}
```

> For `email`: pass email address as `contactValue`.
> For `fido2`: use the FIDO2 API instead.

**Response 200**
```json
{
  "success": true,
  "message": "OTP sent. Verify to complete enrollment.",
  "data": {
    "enrollmentTransactionId": "a1b2c3d4-abcd-1234-ef56-7890abcdef12",
    "continuationToken": "tok_enroll_abc",
    "method": "sms",
    "status": "pending",
    "expiresAtUtc": "2026-07-16T12:10:00Z"
  }
}
```

**Response 409 — Method already active**
```json
{
  "status": 409,
  "title": "Conflict",
  "detail": "MFA method 'sms' is already configured. Use reconfigure to update it."
}
```

**Response 409 — Contact value in use by another account**
```json
{
  "status": 409,
  "title": "Conflict",
  "detail": "This contact value is already registered with another account."
}
```

**Response 429 — Rate limit (3 OTPs per 15 min)**
```json
{
  "status": 429,
  "title": "Too Many Requests",
  "detail": "Too many enrollment attempts. Please try again later."
}
```

---

## PATCH /api/mfa/enrollments/current — Verify MFA Enrollment

Verifies the OTP to complete enrollment of the new method.

**Headers**
```
Authorization: Bearer <access_token>
```

**Request**
```json
{
  "enrollmentTransactionId": "a1b2c3d4-abcd-1234-ef56-7890abcdef12",
  "continuationToken": "tok_enroll_abc",
  "code": "392847"
}
```

**Response 200**
```json
{
  "success": true,
  "message": "MFA method enrolled successfully.",
  "data": {
    "method": "sms",
    "status": "verified"
  }
}
```

---

## POST /api/mfa/login-enrollments — Start Login-Time Enrollment

> **Flujo: Login Enrollment** — El usuario nunca configuró MFA y el sistema le exige hacerlo durante el login antes de darle acceso. El usuario recibió un `enrollment_token` en lugar de un `access_token`. Hasta completar este flujo, no puede entrar a la aplicación.

Starts enrollment during login (when user has no MFA yet). Requires enrollment token.

**Headers**
```
Authorization: Bearer <enrollment_token>
```

**Request**
```json
{
  "continuationToken": "tok_abc123",
  "method": "email",
  "contactValue": "jdoe@example.com"
}
```

**Response 200**
```json
{
  "success": true,
  "message": "OTP sent. Verify to complete enrollment.",
  "data": {
    "enrollmentTransactionId": "b2c3d4e5-1234-5678-abcd-ef012345678a",
    "continuationToken": "tok_loginenroll_xyz",
    "method": "email",
    "status": "pending",
    "expiresAtUtc": "2026-07-16T12:15:00Z"
  }
}
```

---

## PATCH /api/mfa/login-enrollments/current — Verify Login-Time Enrollment

Verifies the OTP during login-time enrollment.

**Headers**
```
Authorization: Bearer <enrollment_token>
```

**Request**
```json
{
  "enrollmentTransactionId": "b2c3d4e5-1234-5678-abcd-ef012345678a",
  "continuationToken": "tok_loginenroll_xyz",
  "code": "193847"
}
```

**Response 200**
```json
{
  "success": true,
  "message": "Login enrollment verified.",
  "data": {
    "method": "email",
    "status": "verified",
    "continuationToken": "tok_complete_zyx"
  }
}
```

---

## POST /api/mfa/login-enrollment-sessions/complete — Complete Login Enrollment Session

Finalizes login-time enrollment and returns full access + refresh tokens.

**Headers**
```
Authorization: Bearer <enrollment_token>
```

**Request**
```json
{
  "enrollmentSessionId": "a1b2c3d4-...",
  "continuationToken": "tok_complete_zyx"
}
```

**Response 200**
```json
{
  "success": true,
  "message": "Login enrollment completed. You are now authenticated.",
  "data": {
    "status": "Authenticated",
    "mfaRequired": false,
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "bmV3UmVmcmVzaFRva2VuSGVyZQ...",
    "expiresIn": 900
  }
}
```

---

## POST /api/mfa/management-sessions — Start Management Session

> **Flujo: Management Session** — El usuario ya está autenticado y quiere **modificar** sus métodos existentes: eliminarlos (`DELETE /api/mfa/methods/{method}`) o cambiar el número/email de contacto (`POST /api/mfa/methods/{method}/reconfigure`). Para proteger contra sesiones robadas, el sistema exige un **step-up**: re-verificar su identidad con un método existente antes de permitir cualquier cambio destructivo. Una vez completado el step-up, tiene una ventana de ~10 minutos para ejecutar los cambios.

Initiates an MFA management session (step-up required to manage methods).

**Headers**
```
Authorization: Bearer <access_token>
```

**Response 200 — Step-up required**
```json
{
  "success": true,
  "message": "MFA step-up required to manage your methods.",
  "data": {
    "status": "step_up_required",
    "mfaTransactionId": "c3d4e5f6-5678-1234-abcd-012345678abc",
    "continuationToken": "tok_mgmt_abc",
    "availableMethods": ["sms", "email"],
    "expiresAtUtc": "2026-07-16T12:20:00Z"
  }
}
```

---

## POST /api/mfa/management-sessions/challenges/start — Start Management Challenge

Sends an OTP for the management step-up.

**Headers**
```
Authorization: Bearer <access_token>
```

**Request**
```json
{
  "mfaTransactionId": "c3d4e5f6-5678-1234-abcd-012345678abc",
  "continuationToken": "tok_mgmt_abc",
  "method": "email"
}
```

**Response 200**
```json
{
  "success": true,
  "message": "OTP sent via email.",
  "data": {
    "mfaTransactionId": "c3d4e5f6-5678-1234-abcd-012345678abc",
    "continuationToken": "tok_mgmt_challenge_xyz",
    "method": "email",
    "status": "pending",
    "expiresAtUtc": "2026-07-16T12:25:00Z"
  }
}
```

---

## POST /api/mfa/management-sessions/challenges/verify — Verify Management Challenge

Verifies the OTP to complete the management step-up.

**Headers**
```
Authorization: Bearer <access_token>
```

**Request**
```json
{
  "mfaTransactionId": "c3d4e5f6-5678-1234-abcd-012345678abc",
  "continuationToken": "tok_mgmt_challenge_xyz",
  "code": "573920"
}
```

**Response 200**
```json
{
  "success": true,
  "message": "Management step-up verified.",
  "data": {
    "status": "step_up_completed",
    "continuationToken": "tok_mgmt_complete_abc",
    "expiresAtUtc": "2026-07-16T12:35:00Z"
  }
}
```

---

## POST /api/mfa/management-sessions/complete — Complete Management Session

Commits the management session after changes are made.

**Headers**
```
Authorization: Bearer <access_token>
```

**Request**
```json
{
  "mfaTransactionId": "c3d4e5f6-5678-1234-abcd-012345678abc",
  "continuationToken": "tok_mgmt_complete_abc"
}
```

**Response 200**
```json
{
  "success": true,
  "message": "MFA management session completed."
}
```

---

## DELETE /api/mfa/management-sessions/{mfaTransactionId} — Cancel Management Session

Cancels an active management session.

**Headers**
```
Authorization: Bearer <access_token>
```

**Path param:** `mfaTransactionId` — GUID of the management session

**Response 200**
```json
{
  "success": true,
  "message": "MFA management session cancelled."
}
```

---

## DELETE /api/mfa/methods/{method} — Remove MFA Method

Removes an enrolled MFA method. Requires an active management session.

**Headers**
```
Authorization: Bearer <access_token>
```

**Path param:** `method` — e.g. `sms`, `email`, `recovery_code`

**Response 200**
```json
{
  "success": true,
  "message": "MFA method removed successfully.",
  "data": {
    "method": "sms",
    "removedAt": "2026-07-16T12:40:00Z"
  }
}
```

---

## POST /api/mfa/methods/{method}/reconfigure — Start Method Reconfiguration

Starts reconfiguring an existing MFA method (e.g. change phone number).

**Headers**
```
Authorization: Bearer <access_token>
```

**Path param:** `method` — e.g. `sms`

**Request**
```json
{
  "contactValue": "+15559876543"
}
```

**Response 200**
```json
{
  "success": true,
  "message": "OTP sent to new contact. Verify to complete reconfiguration.",
  "data": {
    "reconfigureTransactionId": "e5f6a7b8-abcd-1234-5678-90abcdef1234",
    "method": "sms",
    "status": "pending",
    "expiresAtUtc": "2026-07-16T12:45:00Z"
  }
}
```

**Response 409 — Contact value in use by another account**
```json
{
  "status": 409,
  "title": "Conflict",
  "detail": "This contact value is already registered with another account."
}
```

**Response 429 — Rate limit (3 OTPs per 15 min)**
```json
{
  "status": 429,
  "title": "Too Many Requests",
  "detail": "Too many reconfiguration attempts. Please try again later."
}
```

---

## PATCH /api/mfa/methods/{method}/reconfigure/current — Complete Method Reconfiguration

Verifies the OTP to finalize the reconfiguration.

**Headers**
```
Authorization: Bearer <access_token>
```

**Path param:** `method` — e.g. `sms`

**Request**
```json
{
  "reconfigureTransactionId": "e5f6a7b8-abcd-1234-5678-90abcdef1234",
  "code": "748201"
}
```

**Response 200**
```json
{
  "success": true,
  "message": "MFA method reconfigured successfully.",
  "data": {
    "method": "sms",
    "status": "verified"
  }
}
```
