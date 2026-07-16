# Plan — Unicidad de Métodos MFA por Usuario

**Objetivo:** Un usuario solo puede tener un método MFA registrado por tipo (`sms`, `email`, `fido2`). No puede registrar el mismo email o teléfono en dos métodos del mismo tipo, ni registrar múltiples credenciales FIDO2.

---

## Estado Actual

### ¿Qué ya existe?

La tabla `UserMfaMethods` tiene un índice único compuesto `(UserId, Method)`:
```csharp
// UserMfaMethodConfiguration.cs
builder.HasIndex(x => new { x.UserId, x.Method }).IsUnique();
```

Esto **ya previene duplicar el tipo a nivel de base de datos** (un usuario no puede tener dos filas con `Method = "sms"`). Sin embargo:

| Escenario | Estado actual |
|-----------|:---:|
| Usuario intenta registrar `sms` cuando ya tiene uno activo | ⚠️ El service lo **actualiza** silenciosamente (no rechaza) |
| Usuario intenta registrar el mismo email con `sms` y `email` por separado | ⚠️ No validado — dos métodos distintos con mismo contacto |
| Usuario intenta registrar múltiples credenciales FIDO2 | ⚠️ `ExcludeCredentials` en las options previene hardware duplicado, pero no limita la cantidad de devices distintos |
| Email de otro usuario usado como contactValue en `sms`/`email` | ⚠️ No validado — un usuario podría enrollar el email/tel de otro |

### Código relevante — `StartEnrollmentCoreAsync` (MfaService.cs ~1383)

```csharp
// Envía OTP sin validar si el método ya existe
var providerSid = await _twilioOtpService.StartVerificationAsync(contact, normalizedMethod, ct);
```

```csharp
// En VerifyEnrollmentCoreAsync — upsert silencioso
if (existing is null) { await _mfaMethodRepository.AddAsync(new UserMfaMethod {...}); }
else { existing.ContactValue = contact; await _mfaMethodRepository.UpdateAsync(existing); }
```

---

## Reglas de Negocio a Implementar

| Regla | Descripción |
|-------|-------------|
| **R1** | Un usuario puede tener exactamente **1** método por tipo (`sms`, `email`, `fido2`) |
| **R2** | Si el usuario ya tiene el método activo → el flujo es **reconfiguración**, no enrollment nuevo |
| **R3** | El `contactValue` (email/teléfono) de un método debe ser **único globalmente** entre usuarios activos del mismo tipo |
| **R4** | Para FIDO2 → máximo **2 dispositivos** por usuario (un primario + un backup) |

---

## Plan de Implementación

### Fase 1 — Validación de método duplicado en enrollment (R1 y R2)

**Archivo:** `Services/Implementatons/MfaService.cs` → `StartEnrollmentCoreAsync`

**Cambio:** Antes de enviar el OTP, verificar si el método ya existe y está activo.

```csharp
// Agregar al inicio de StartEnrollmentCoreAsync, después de validar method/contact:
var existingMethod = await _mfaMethodRepository.GetByUserIdAndMethodAsync(
    userId, normalizedMethod, cancellationToken);

if (existingMethod is not null && existingMethod.IsEnabled)
{
    return Result<StartMfaEnrollmentResponse>.Failure(
        $"MFA method '{normalizedMethod}' is already configured. Use reconfigure to update it.",
        StatusCodes.Status409Conflict
    );
}
```

**Nota:** Si `existingMethod` existe pero `IsEnabled = false` (fue deshabilitado), se permite reactivar a través del enrollment.

---

### Fase 2 — Validación de contactValue único globalmente (R3)

**Requiere nuevo método en `IUserMfaMethodRepository`:**

```csharp
// IUserMfaMethodRepository.cs — nuevo método
Task<bool> IsContactValueInUseAsync(
    string contactValue,
    string method,
    long excludeUserId,
    CancellationToken cancellationToken
);
```

**Implementación en `UserMfaMethodRepository`:**

```csharp
public Task<bool> IsContactValueInUseAsync(
    string contactValue, string method, long excludeUserId, CancellationToken ct)
{
    return _context.UserMfaMethods.AnyAsync(
        x => x.ContactValue == contactValue
          && x.Method == method
          && x.IsEnabled
          && x.UserId != excludeUserId, ct);
}
```

**Uso en `StartEnrollmentCoreAsync`:**

```csharp
var contactInUse = await _mfaMethodRepository.IsContactValueInUseAsync(
    contact, normalizedMethod, userId, cancellationToken);

if (contactInUse)
{
    return Result<StartMfaEnrollmentResponse>.Failure(
        "This contact value is already registered with another account.",
        StatusCodes.Status409Conflict
    );
}
```

> ⚠️ La misma validación debe aplicarse en `StartReconfigureMethodAsync` y `StartManagementChallengeAsync` cuando se trate de reconfiguraciones.

---

### Fase 3 — Límite de 2 dispositivos FIDO2 por usuario (R4)

**Archivo:** `Services/Implementatons/Fido2MfaService.cs` → `CreateEnrollmentOptionsAsync`

**Sin cambio de configuración** — el límite es fijo en 2 (1 primario + 1 backup). Se define como constante en `Fido2MfaService`:

```csharp
// Fido2MfaService.cs — agregar constante
private const int MaxFido2CredentialsPerUser = 2;
```

**Validación en `CreateEnrollmentOptionsAsync`** (después de obtener existingCredentials):

```csharp
if (existingCredentials.Count >= MaxFido2CredentialsPerUser)
{
    return Result<Fido2OptionsResponse>.Failure(
        $"Maximum number of FIDO2 devices ({MaxFido2CredentialsPerUser}) reached. " +
        "Remove an existing device before adding a new one.",
        StatusCodes.Status409Conflict
    );
}
```

**Comportamiento:**
- Usuario con 0 devices → puede registrar (device primario)
- Usuario con 1 device → puede registrar (device backup)
- Usuario con 2 devices → **bloqueado** con `409 Conflict` hasta que elimine uno

---

### Fase 4 — Índice de base de datos en `ContactValue` (opcional, rendimiento)

Para hacer eficiente la consulta de unicidad global:

```csharp
// UserMfaMethodConfiguration.cs — agregar índice
builder.HasIndex(x => new { x.Method, x.ContactValue })
       .HasFilter("[IsEnabled] = 1"); // filtered index — solo activos
```

> Agregar en una nueva migración EF Core.

---

## Archivos a Modificar

| Archivo | Cambio |
|---------|--------|
| `Services/Implementatons/MfaService.cs` | Validación R1 en `StartEnrollmentCoreAsync`, validación R3 misma función |
| `Services/Implementatons/Fido2MfaService.cs` | Constante `MaxFido2CredentialsPerUser = 2`, validación en `CreateEnrollmentOptionsAsync` |
| `Data/Repositories/Interfaces/IUserMfaMethodRepository.cs` | Agregar `IsContactValueInUseAsync` |
| `Data/Repositories/Implementations/UserMfaMethodRepository.cs` | Implementar `IsContactValueInUseAsync` |
| `Options/Fido2Options.cs` | Sin cambios — límite fijo como constante en el service |
| `Data/Configurations/UserMfaMethodConfiguration.cs` | Agregar filtered index en `Method + ContactValue` |
| Nueva migración EF Core | Schema change para el nuevo índice |

---

## Respuestas de Error a Documentar en API_MFA.md

| Scenario | Status | Mensaje |
|----------|--------|---------|
| Método ya activo para ese usuario | `409 Conflict` | `"MFA method 'sms' is already configured. Use reconfigure to update it."` |
| ContactValue en uso por otro usuario | `409 Conflict` | `"This contact value is already registered with another account."` |
| Máximo de devices FIDO2 alcanzado (2) | `409 Conflict` | `"Maximum number of FIDO2 devices (2) reached. Remove an existing device before adding a new one."` |
