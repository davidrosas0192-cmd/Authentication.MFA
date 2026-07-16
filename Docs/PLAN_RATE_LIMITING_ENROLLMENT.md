# Plan — Rate Limiting en Enrollment y FIDO2

**Objetivo:** Prevenir spam de OTPs de Twilio y abuso de los endpoints de enrollment y FIDO2 que actualmente no tienen rate limiting.

---

## Estado Actual

### Endpoints sin rate limiting

| Endpoint | Riesgo |
|----------|--------|
| `POST /api/mfa/enrollments` | Envía OTP via Twilio en cada request → costo económico y spam SMS/email |
| `POST /api/mfa/login-enrollments` | Mismo riesgo → Twilio OTP por cada llamada |
| `PATCH /api/mfa/enrollments/current` | Tiene lockout de 5 intentos por challenge, pero se puede abrir nuevo challenge |
| `POST /api/mfa/methods/{method}/reconfigure` | Envía OTP via Twilio → mismo riesgo |
| `POST /api/fido2/enrollments` | Genera opciones + crea `Fido2Transaction` en BD en cada request |
| `PATCH /api/fido2/enrollments/current` | Procesamiento criptográfico en cada intento |
| `POST /api/fido2/authentications` | Genera opciones + consulta BD por credenciales |

### ¿Qué ya existe?

```csharp
// IRateLimitingService — disponible y configurado
_rateLimitingService.IsAllowed(key, maxAttempts, windowSeconds)

// Ejemplos ya implementados:
$"login_{ipAddress}"              // Login: 10/15min/IP
$"mfa_verify_{userId}"            // MFA verify: 10/15min/user
$"mfa_start_{userId}"             // MFA start: 10/15min/user
$"refresh_{ipAddress}"            // Refresh: 30/15min/IP
```

---

## Reglas de Rate Limiting Propuestas

| Endpoint / Flujo | Key | Límite | Ventana | Razón |
|-----------------|-----|--------|---------|-------|
| `StartEnrollmentCoreAsync` | `enrollment_otp_{userId}` | **3** | 15 min | OTP de Twilio — costo por SMS/email |
| `StartReconfigureMethodAsync` | `reconfigure_otp_{userId}` | **3** | 15 min | Mismo riesgo que enrollment |
| `StartLoginEnrollmentAsync` | `login_enrollment_{userId}` | **3** | 10 min | OTP durante login |
| `CreateEnrollmentOptionsAsync` (FIDO2) | `fido2_enroll_{userId}` | **5** | 15 min | Generación de credenciales WebAuthn |
| `CompleteEnrollmentAsync` (FIDO2) | `fido2_enroll_complete_{userId}` | **5** | 15 min | Verificación criptográfica |
| `CreateLoginOptionsAsync` (FIDO2) | `fido2_auth_{userId}` | **10** | 5 min | Más permisivo — flujo normal de login |

> Los límites de OTP son más estrictos (3/15min) porque cada request incurre en costo de Twilio.

---

## Plan de Implementación

### Fase 1 — Rate limiting en enrollment SMS/Email

**Archivo:** `Services/Implementatons/MfaService.cs` → `StartEnrollmentCoreAsync`

**Ubicación:** Al inicio del método, antes de llamar a `_twilioOtpService.StartVerificationAsync`.

```csharp
// StartEnrollmentCoreAsync — agregar al inicio
var enrollRateLimitKey = $"enrollment_otp_{userId}";
if (!_rateLimitingService.IsAllowed(enrollRateLimitKey, maxAttempts: 3, windowSeconds: 900))
{
    await _auditService.TrackSecurityEventAsync(
        "Authentication",
        "auth.mfa.enrollment.rate_limited",
        "Warning",
        false,
        userId,
        null,
        "Enrollment OTP rate limit exceeded",
        new { method = normalizedMethod },
        cancellationToken
    );

    return Result<StartMfaEnrollmentResponse>.Failure(
        "Too many enrollment attempts. Please try again later.",
        StatusCodes.Status429TooManyRequests
    );
}
```

---

### Fase 2 — Rate limiting en reconfigure SMS/Email

**Archivo:** `Services/Implementatons/MfaService.cs` → `StartReconfigureMethodAsync`

**Mismo patrón** con key `reconfigure_otp_{userId}`:

```csharp
var reconfigRateLimitKey = $"reconfigure_otp_{userId}";
if (!_rateLimitingService.IsAllowed(reconfigRateLimitKey, maxAttempts: 3, windowSeconds: 900))
{
    return Result<StartMfaReconfigureResponse>.Failure(
        "Too many reconfiguration attempts. Please try again later.",
        StatusCodes.Status429TooManyRequests
    );
}
```

---

### Fase 3 — Rate limiting en FIDO2 enrollment

**Archivo:** `Services/Implementatons/Fido2MfaService.cs` → `CreateEnrollmentOptionsAsync` y `CompleteEnrollmentAsync`

**`CreateEnrollmentOptionsAsync`** — antes de `RequestNewCredential`:

```csharp
var fido2EnrollKey = $"fido2_enroll_{userId}";
if (!_rateLimitingService.IsAllowed(fido2EnrollKey, maxAttempts: 5, windowSeconds: 900))
{
    return Result<Fido2OptionsResponse>.Failure(
        "Too many FIDO2 enrollment attempts. Please try again later.",
        StatusCodes.Status429TooManyRequests
    );
}
```

**`CompleteEnrollmentAsync`** — antes del procesamiento criptográfico:

```csharp
var fido2CompleteKey = $"fido2_enroll_complete_{userId}";
if (!_rateLimitingService.IsAllowed(fido2CompleteKey, maxAttempts: 5, windowSeconds: 900))
{
    return Result<CompleteFido2EnrollmentResponse>.Failure(
        "Too many FIDO2 enrollment attempts. Please try again later.",
        StatusCodes.Status429TooManyRequests
    );
}
```

---

### Fase 4 — Rate limiting en FIDO2 login options

**Archivo:** `Services/Implementatons/Fido2MfaService.cs` → `CreateLoginOptionsAsync`

```csharp
var fido2AuthKey = $"fido2_auth_{userId}";
if (!_rateLimitingService.IsAllowed(fido2AuthKey, maxAttempts: 10, windowSeconds: 300))
{
    return Result<Fido2OptionsResponse>.Failure(
        "Too many FIDO2 authentication attempts. Please try again later.",
        StatusCodes.Status429TooManyRequests
    );
}
```

---

### Fase 5 — Rate limiting en login-enrollment OTP

**Archivo:** `Services/Implementatons/MfaService.cs` → `StartLoginEnrollmentAsync`

**Ubicación:** Antes del `await StartEnrollmentCoreAsync(...)`:

```csharp
var loginEnrollKey = $"login_enrollment_{userId}";
if (!_rateLimitingService.IsAllowed(loginEnrollKey, maxAttempts: 3, windowSeconds: 600))
{
    return Result<StartLoginEnrollmentResponse>.Failure(
        "Too many enrollment attempts. Please try again later.",
        StatusCodes.Status429TooManyRequests
    );
}
```

---

## Resumen de Cambios

| Archivo | Método | Key de rate limit | Límite |
|---------|--------|-------------------|--------|
| `MfaService.cs` | `StartEnrollmentCoreAsync` | `enrollment_otp_{userId}` | 3/15min |
| `MfaService.cs` | `StartLoginEnrollmentAsync` | `login_enrollment_{userId}` | 3/10min |
| `MfaService.cs` | `StartReconfigureMethodAsync` | `reconfigure_otp_{userId}` | 3/15min |
| `Fido2MfaService.cs` | `CreateEnrollmentOptionsAsync` | `fido2_enroll_{userId}` | 5/15min |
| `Fido2MfaService.cs` | `CompleteEnrollmentAsync` | `fido2_enroll_complete_{userId}` | 5/15min |
| `Fido2MfaService.cs` | `CreateLoginOptionsAsync` | `fido2_auth_{userId}` | 10/5min |

**No requiere:**
- Nuevos servicios ni interfaces
- Cambios en DI
- Cambios en base de datos
- Cambios en controllers

**Solo requiere:** Agregar el bloque `if (!_rateLimitingService.IsAllowed(...))` al inicio de cada método de service.

---

## Nota sobre producción multi-instancia

El `RateLimitingService` actual es **in-memory**. En producción con múltiples instancias del servidor, el rate limiting no es compartido entre instancias.

**Para producción:** Reemplazar `RateLimitingService` por una implementación con Redis:

```csharp
// Opción recomendada
services.AddStackExchangeRedisCache(options => options.Configuration = "redis-connection");
services.AddSingleton<IRateLimitingService, RedisRateLimitingService>(); // implementar
```

El contrato de `IRateLimitingService` ya es el correcto — solo cambiar la implementación.
