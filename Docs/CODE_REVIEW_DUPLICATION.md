# Code Duplication Review — COMPLETADO

Revisión de código duplicado. Última actualización: 2026-07-16.
**Estado: ✅ Todos los hallazgos resueltos. Build: 0 errores, 0 warnings.**

---

## Resumen Final

| Área | Severidad | Estado |
|------|-----------|--------|
| Controller helpers duplicados (`ToActionResult`, `TryGetUserId`, `GetProblemTitle`) | 🔴 Alta | ✅ Resuelto |
| `UnauthorizedProblem` inconsistente — 14 ocurrencias en `MfaController` | 🔴 Alta | ✅ Resuelto |
| Retry en código incorrecto (Enrollment / Management / Reconfigure) | 🔴 Alta | ✅ Resuelto |
| `ValidateMfaTokenContext` — revisión de seguridad | — | ✅ Documentado — NO fusionar (intencional) |
| Creación de `AccessTokenSession` / `RefreshTokenSession` duplicada | 🟡 Media | ✅ Resuelto + 2 bugs corregidos |
| `GenerateRecoveryCode` / `NormalizeRecoveryCode` duplicados | 🟡 Media | ✅ Resuelto |
| `DateTime.UtcNow.AddMinutes(5)` magic number × 11 | 🟡 Media | ✅ Resuelto |
| `MfaController.GetProblemTitle` residual | 🟢 Baja | ✅ Resuelto |
| 2 `Unauthorized(new { message })` residuales | 🟢 Baja | ✅ Resuelto |
| try/catch boilerplate en 24 endpoints | 🟢 Baja | ✅ Resuelto |

---

## ✅ Hallazgo 1 — Retry en código incorrecto

Todos los flujos de verificación soportan retry con lockout en 5 intentos.

| Flujo | Retry | Comportamiento en fallo |
|-------|:---:|------------------------|
| Login MFA — SMS/Email OTP | ✅ | `FailedAttempts++`, status `pending`, lockout con `429` en 5 |
| Login MFA — Recovery code | ✅ | Mismo patrón |
| Enrollment verification | ✅ | Mismo patrón |
| Login-time enrollment verification | ✅ | Mismo patrón |
| Management step-up verification | ✅ | Mismo patrón |
| Reconfigure method completion | ✅ | Mismo patrón |

---

## ✅ Hallazgo 2 — `ApiControllerBase`

**Creado:** `Controllers/ApiControllerBase.cs`

Centraliza todos los helpers compartidos entre controllers:

| Método | Visibilidad | Descripción |
|--------|-------------|-------------|
| `ToActionResult<T>` | `protected` | Mapea `Result<T>` → `IActionResult` con `ProblemDetails` y header `Retry-After` en 429 |
| `ToActionResult(Result)` | `protected` | Versión sin data |
| `TryGetUserId` | `protected static` | Extrae `userId` de los claims JWT |
| `UnauthorizedProblem` | `protected static` | Retorna `401 ProblemDetails` consistente |
| `GetProblemTitle` | `protected static` | Switch de status codes → títulos HTTP (todos los 4xx/5xx) |
| `RetryAfterSeconds` | `protected virtual int` | Default 45s — `MfaController` hace override con `_mfaApiPolicyOptions` |

**Todos los controllers heredan `ApiControllerBase`:**
- `AuthController : ApiControllerBase`
- `MfaController : ApiControllerBase` — override `RetryAfterSeconds`, `new ToActionResult<T>` con lógica MFA-específica
- `Fido2Controller : ApiControllerBase`
- `UsersController : ApiControllerBase`
- `MonitorController : ApiControllerBase`

---

## ✅ Hallazgo 3 — `ValidateMfaTokenContext` — NO fusionar

Documentado con comentarios en el código. Los métodos son **intencionalmente distintos**:

| Validación | `Fido2Controller` | `MfaController` |
|------------|:-----------------:|:---------------:|
| Verifica `token_type = "mfa"` | ✅ | ❌ (delegado al middleware) |
| Verifica `jti` no vacío | ✅ | ❌ |
| Consulta BD — `GetActiveByJtiAsync()` | ✅ previene token replay | ❌ |
| Audit event | `auth.fido2.mfa_token_validation` | `auth.mfa.token_validation` |

`Fido2Controller` emite el `access_token` final → requiere validación BD para prevenir replay attacks.

---

## ✅ Hallazgo 4 — `SessionFactory`

**Creados:** `Services/Interfaces/ISessionFactory.cs` + `Services/Implementations/SessionFactory.cs`

Un único método `CreateAuthenticatedSessionAsync(user, ipAddress, userAgent, ct)` reemplaza los 5 bloques duplicados en `AuthService`, `MfaService`, y `Fido2MfaService`.

**Bugs corregidos como parte del refactor:**

| Bug | Servicios afectados |
|-----|---------------------|
| `RefreshTokenSession` no persistida — refresh token devuelto sin guardarse en BD | `MfaService.VerifyChallengeAsync`, `CompleteLoginEnrollmentSessionAsync`, `Fido2MfaService.CompleteLoginAsync` |
| `IpAddress` / `UserAgent` nulos en `AccessTokenSession` | `MfaService.VerifyChallengeAsync`, `Fido2MfaService.CompleteLoginAsync` |

---

## ✅ Hallazgo 5 — `RecoveryCodeHelper`

**Creado:** `Common/RecoveryCodeHelper.cs`

Métodos `GenerateRecoveryCode` y `NormalizeRecoveryCode` en `MfaService` y `Fido2MfaService` ahora son wrappers de `RecoveryCodeHelper.Generate()` / `RecoveryCodeHelper.Normalize()`.

---

## ✅ Hallazgo 6 — Magic number `AddMinutes(5)` eliminado

11 ocurrencias reemplazadas por `MfaChallengeOptions.ChallengeExpirationMinutes` en `MfaService.cs` y `Fido2MfaService.cs`.

---

## ✅ Hallazgo 7 — `GlobalExceptionFilter`

**Creado:** `Filters/GlobalExceptionFilter.cs`  
**Registrado en:** `Program.cs` → `builder.Services.AddControllers(options => options.Filters.Add<GlobalExceptionFilter>())`

Elimina 22 bloques try/catch genéricos idénticos de todos los controllers.

**Detalle por controller:**

| Controller | Bloques eliminados |
|------------|:-----------------:|
| `AuthController` | 2 |
| `UsersController` | 1 |
| `MfaController` | 17 |
| `Fido2Controller` | 3 genéricos eliminados |

`Fido2Controller.CompleteLogin` conserva su `catch (UnauthorizedAccessException)` específico — las excepciones genéricas son ahora manejadas por el filtro.

El filtro loguea `{Method} {Path}` con nivel `Error` para trazabilidad centralizada.

---

## Archivos creados / modificados

| Archivo | Cambio |
|---------|--------|
| `Common/RecoveryCodeHelper.cs` | Creado |
| `Controllers/ApiControllerBase.cs` | Creado |
| `Filters/GlobalExceptionFilter.cs` | Creado |
| `Services/Interfaces/ISessionFactory.cs` | Creado |
| `Services/Implementations/SessionFactory.cs` | Creado |
| `Controllers/AuthController.cs` | Hereda base, 2 try/catch eliminados |
| `Controllers/MfaController.cs` | Hereda base, 17 try/catch eliminados, 14 `UnauthorizedProblem` unificados |
| `Controllers/Fido2Controller.cs` | Hereda base, 3 try/catch eliminados, comentario de seguridad |
| `Controllers/UsersController.cs` | Hereda base, 1 try/catch eliminado |
| `Controllers/MonitorController.cs` | Hereda base |
| `Services/Implementatons/AuthService.cs` | Usa `ISessionFactory` |
| `Services/Implementatons/MfaService.cs` | Usa `ISessionFactory`, `RecoveryCodeHelper`, constante de expiración, retry |
| `Services/Implementatons/Fido2MfaService.cs` | Usa `ISessionFactory`, `RecoveryCodeHelper`, constante de expiración |
| `Extensions/ServiceCollectionExtensions.cs` | Registra `ISessionFactory` |
| `Program.cs` | Registra `GlobalExceptionFilter` |
