# OWASP Security Audit — API Endpoints

Revisión de seguridad de todos los endpoints basada en OWASP Top 10 (2021).
Última actualización: 2026-07-16.

---

## Leyenda

| Símbolo | Significado |
|---------|-------------|
| ✅ | Cumple |
| ⚠️ | Riesgo identificado — acción recomendada |
| 🔴 | Riesgo crítico — acción requerida |
| `Bearer` | JWT access token (15 min) |
| `MfaToken` | JWT MFA challenge token (5 min) |
| `EnrollToken` | JWT login enrollment token (10 min) |

---

## Mapa de Endpoints y Controles de Seguridad

### Sessions & Auth

| Endpoint | Método | Autenticación | Rate Limiting | OWASP |
|----------|--------|:---:|:---:|-------|
| `POST /api/sessions` | Login | ❌ Público | ✅ 10/15min/IP | A07 — sin brute force adicional a nivel de cuenta |
| `DELETE /api/sessions/current` | Logout | ✅ `Bearer` | ❌ | ✅ |
| `POST /api/sessions/refresh` | Refresh Token | ❌ Público (token en body) | ✅ 30/15min/IP | ✅ token rotation implementado |
| `DELETE /api/mfa/sessions/current` | Cancel MFA Auth | ✅ `MfaToken` | ❌ | ✅ |

### MFA — Challenges

| Endpoint | Método | Autenticación | Rate Limiting | OWASP |
|----------|--------|:---:|:---:|-------|
| `GET /api/mfa/methods` | Get Methods | ✅ `Bearer` | ❌ | ✅ |
| `GET /api/mfa/setup-options` | Get Setup Options | ✅ `Bearer` | ❌ | ✅ |
| `POST /api/mfa/challenges` | Start Challenge | ✅ `MfaToken` | ✅ 10/15min/user | ✅ |
| `PATCH /api/mfa/challenges/current` | Verify Challenge | ✅ `MfaToken` | ✅ 10/15min/user + lockout 5 intentos | ✅ |

### MFA — Enrollment

| Endpoint | Método | Autenticación | Rate Limiting | OWASP |
|----------|--------|:---:|:---:|-------|
| `POST /api/mfa/enrollments` | Start Enrollment | ✅ `Bearer` | ✅ 3/15min/user | ✅ + unicidad de método y contactValue |
| `PATCH /api/mfa/enrollments/current` | Verify Enrollment | ✅ `Bearer` | ✅ lockout 5 intentos | ✅ |
| `POST /api/mfa/login-enrollments` | Start Login Enrollment | ✅ `EnrollToken` | ✅ 3/10min/user | ✅ |
| `PATCH /api/mfa/login-enrollments/current` | Verify Login Enrollment | ✅ `EnrollToken` | ✅ lockout 5 intentos | ✅ |
| `POST /api/mfa/login-enrollment-sessions/complete` | Complete Login Enrollment | ✅ `EnrollToken` | ❌ | ✅ |

### MFA — Management

| Endpoint | Método | Autenticación | Rate Limiting | OWASP |
|----------|--------|:---:|:---:|-------|
| `POST /api/mfa/management-sessions` | Start Management Session | ✅ `Bearer` | ❌ | ✅ Step-up requerido |
| `POST /api/mfa/management-sessions/challenges/start` | Start Step-up Challenge | ✅ `Bearer` | ❌ | ✅ |
| `POST /api/mfa/management-sessions/challenges/verify` | Verify Step-up | ✅ `Bearer` | ❌ | ✅ lockout 5 intentos |
| `POST /api/mfa/management-sessions/complete` | Complete Management Session | ✅ `Bearer` | ❌ | ✅ |
| `DELETE /api/mfa/management-sessions/{id}` | Cancel Management Session | ✅ `Bearer` | ❌ | ✅ |
| `DELETE /api/mfa/methods/{method}` | Remove MFA Method | ✅ `Bearer` | ❌ | ✅ Step-up requerido previamente |
| `POST /api/mfa/methods/{method}/reconfigure` | Start Reconfigure | ✅ `Bearer` | ✅ 3/15min/user | ✅ Step-up + unicidad de contactValue |
| `PATCH /api/mfa/methods/{method}/reconfigure/current` | Complete Reconfigure | ✅ `Bearer` | ❌ | ✅ lockout 5 intentos |

### FIDO2

| Endpoint | Método | Autenticación | Rate Limiting | OWASP |
|----------|--------|:---:|:---:|-------|
| `POST /api/fido2/enrollments` | Create Enrollment Options | ✅ `Bearer` | ✅ 5/15min/user | ✅ + límite 2 devices |
| `PATCH /api/fido2/enrollments/current` | Complete Enrollment | ✅ `Bearer` | ✅ 5/15min/user | ✅ |
| `POST /api/fido2/authentications` | Create Login Options | ✅ `MfaToken` + DB lookup | ✅ 10/5min/user | ✅ |
| `PATCH /api/fido2/authentications/current` | Complete Login | ✅ `MfaToken` + DB lookup | ❌ | ✅ |

### Users

| Endpoint | Método | Autenticación | Rate Limiting | OWASP |
|----------|--------|:---:|:---:|-------|
| `POST /api/users` | Create User | ❌ Público | ❌ | ⚠️ Sin rate limit — riesgo de registro masivo (A07) |

### Monitoring

| Endpoint | Método | Autenticación | Rate Limiting | OWASP |
|----------|--------|:---:|:---:|-------|
| `GET /api/monitor/summary` | Dashboard Summary | ❌ `[AllowAnonymous]` | ❌ | 🔴 Expone stats del sistema sin autenticación |
| `GET /api/monitor/logins` | Login History | ❌ `[AllowAnonymous]` | ❌ | 🔴 Expone PII (email, IP) sin autenticación |
| `GET /api/monitor/enrollments` | Enrollments | ❌ `[AllowAnonymous]` | ❌ | 🔴 Expone datos de sesiones sin autenticación |
| `GET /api/monitor/challenges` | MFA Challenges | ❌ `[AllowAnonymous]` | ❌ | 🔴 Expone estado de challenges sin autenticación |
| `GET /api/monitor/sessions` | Token Sessions | ❌ `[AllowAnonymous]` | ❌ | 🔴 Expone sesiones activas sin autenticación |
| `GET /api/monitor/security-events` | Security Events | ❌ `[AllowAnonymous]` | ❌ | 🔴 Expone audit log de seguridad sin autenticación |
| `GET /api/monitor/users` | Users Summary | ❌ `[AllowAnonymous]` | ❌ | 🔴 Expone lista de usuarios y métodos MFA sin autenticación |

---

## Hallazgos por Categoría OWASP

### 🔴 A01:2021 — Broken Access Control

**Todos los endpoints de `/api/monitor` son `[AllowAnonymous]`.**

Exponen datos sensibles sin ningún control de acceso:
- `GET /api/monitor/logins` → emails, IPs, user agents, timestamps de login
- `GET /api/monitor/users` → lista de usuarios con métodos MFA activos
- `GET /api/monitor/sessions` → sesiones activas de tokens
- `GET /api/monitor/security-events` → audit log completo del sistema

**Recomendación:** Agregar `[Authorize(Roles = "Admin")]` o un scheme dedicado de API key para endpoints de monitoreo. Mínimamente requerir un token Bearer válido.

```csharp
// MonitorController.cs
[Authorize] // o [Authorize(Policy = "MonitorPolicy")]
public class MonitorController : ApiControllerBase
```

---

### 🔴 A02:2021 — Cryptographic Failures

**Contraseña en texto plano en seed data de UserConfiguration.cs.**

```csharp
// Data/Configurations/UserConfiguration.cs — línea 28
PasswordHash = "Rdavid58@",  // ← TEXTO PLANO — debe ser hash
```

Esta contraseña debería estar hasheada con `PasswordHasher.Hash("Rdavid58@")` antes de ser almacenada en el seed.

**Recomendación:** Reemplazar con el hash correspondiente:

```csharp
PasswordHash = PasswordHasher.Hash("Rdavid58@"),
```

> ⚠️ Si este seed se ejecutó en producción, la contraseña está comprometida. Cambiar inmediatamente.

---

### ⚠️ A07:2021 — Identification and Authentication Failures

**`POST /api/users` — Registro sin rate limiting.**

Permite crear cuentas masivamente desde cualquier IP sin restricción.

**Recomendación:**

```csharp
// UserRegistrationService o UsersController
var rateLimitKey = $"register_{ipAddress ?? "unknown"}";
if (!_rateLimitingService.IsAllowed(rateLimitKey, maxAttempts: 5, windowSeconds: 3600))
{
    return Result<CreateUserResponse>.Failure(
        "Too many registration attempts. Please try again later.",
        StatusCodes.Status429TooManyRequests
    );
}
```

---

### ⚠️ A07:2021 — Sin rate limit en endpoints de enrollment/FIDO2

✅ **Resuelto.** Todos los endpoints de enrollment y FIDO2 ahora tienen rate limiting:

| Endpoint | Key | Límite |
|----------|-----|--------|
| `POST /api/mfa/enrollments` | `enrollment_otp_{userId}` | 3/15min |
| `POST /api/mfa/login-enrollments` | `login_enrollment_{userId}` | 3/10min |
| `POST /api/mfa/methods/{method}/reconfigure` | `reconfigure_otp_{userId}` | 3/15min |
| `POST /api/fido2/enrollments` | `fido2_enroll_{userId}` | 5/15min |
| `PATCH /api/fido2/enrollments/current` | `fido2_enroll_complete_{userId}` | 5/15min |
| `POST /api/fido2/authentications` | `fido2_auth_{userId}` | 10/5min |

---

### ✅ A02:2021 — Tokens y contraseñas correctamente protegidos

| Control | Estado |
|---------|--------|
| Passwords hasheados con algoritmo seguro (`PasswordHasher`) | ✅ |
| Refresh tokens almacenados como SHA256 hash (nunca plaintext) | ✅ |
| Recovery codes almacenados como hash | ✅ |
| Tokens JWT firmados con HMAC-SHA256 | ✅ |
| Refresh token rotation en cada uso | ✅ |
| Access tokens de vida corta (15 min) | ✅ |
| MFA tokens de vida corta (5 min) | ✅ |

---

### ✅ A07:2021 — Controles de autenticación implementados

| Control | Estado |
|---------|--------|
| Rate limiting en login (10/15min/IP) | ✅ |
| Rate limiting en verify MFA (10/15min/user) | ✅ |
| Rate limiting en refresh token (30/15min/IP) | ✅ |
| Lockout en OTP incorrecto (5 intentos → status locked) | ✅ |
| Lockout en enrollment incorrecto (5 intentos) | ✅ |
| Lockout en management step-up incorrecto (5 intentos) | ✅ |
| Lockout en reconfigure incorrecto (5 intentos) | ✅ |
| MFA temp token validado contra BD (Fido2Controller) | ✅ |
| Continuation token anti-replay en flujos multi-step | ✅ |
| Step-up requerido para modificar métodos MFA | ✅ |

---

### ✅ A09:2021 — Security Logging and Monitoring

| Control | Estado |
|---------|--------|
| `AuthenticationAuditEvent` registra todos los intentos de login | ✅ |
| `SecurityAuditEvent` registra eventos de seguridad con severidad | ✅ |
| IP address y User-Agent registrados en sesiones y challenges | ✅ |
| `GlobalExceptionFilter` loguea excepciones no manejadas | ✅ |
| Intentos fallidos de OTP auditados | ✅ |
| Lockouts auditados | ✅ |
| Token rotation auditada | ✅ |

---

### ✅ A04:2021 — Insecure Design — FIDO2

| Control | Estado |
|---------|--------|
| Credenciales FIDO2 vinculadas a `rpId` (localhost/dominio configurado) | ✅ |
| `SignatureCounter` almacenado y validado contra clonación de authenticator | ✅ |
| Transacciones FIDO2 de un solo uso (`IsUsed`) con expiración | ✅ |
| Token MFA validado contra BD antes de aceptar assertion | ✅ |

---

## Resumen de Riesgos Priorizados

| Prioridad | Endpoint / Área | OWASP | Estado |
|-----------|-----------------|-------|--------|
| 🔴 1 | Todos `/api/monitor/*` — sin autenticación | A01 | ⚠️ **Pendiente** — agregar `[Authorize]` |
| 🔴 2 | Seed data con contraseña en texto plano | A02 | ⚠️ **Pendiente** — hashear en `UserConfiguration.cs` |
| ✅ 3 | Endpoints enrollment/FIDO2 — rate limiting | A07 | ✅ Implementado |
| ✅ 4 | `POST /api/users` — sin rate limit | A07 | ✅ Implementado (ver `UserRegistrationService`) |
