# Database Schema

Base de datos: SQL Server. ORM: Entity Framework Core. Última actualización: 2026-07-16.

Connection string key: `connectionStrings.DefaultConnection`

---

## Diagrama de Relaciones

```
Users ─────────────────────────────────────────────────────────────┐
  │                                                                  │
  ├─── UserMfaMethods (1:N)                                          │
  ├─── UserFido2Credentials (1:N) ◄─── Fido2Transactions            │
  ├─── UserRecoveryCodeBatches (1:N)                                 │
  │      └─── UserRecoveryCodes (1:N)                                │
  ├─── AccessTokenSessions (1:N)                                     │
  │      └─── RefreshTokenSessions (1:N) ◄── PreviousTokenSession   │
  ├─── MfaChallenges (1:N)                                           │
  ├─── MfaTempTokenSessions (1:N)                                    │
  ├─── MfaLoginEnrollmentSessions (1:N)                              │
  ├─── MfaManagementSessions (1:N)                                   │
  ├─── AuthenticationAuditEvents (1:N)                               │
  └─── SecurityAuditEvents (1:N)                                     │
                                                                     │
MfaSessions ──────────────────────────────────────────────────────  │
(legacy/general session table — sin FK a Users)                     │
```

---

## Tablas

### `Users`

Tabla principal de usuarios del sistema.

| Columna | Tipo | Constraints | Descripción |
|---------|------|-------------|-------------|
| `Id` | `bigint` | PK, Identity | ID del usuario |
| `Username` | `nvarchar(100)` | NOT NULL, UNIQUE | Nombre de usuario |
| `Email` | `nvarchar(255)` | NOT NULL, UNIQUE | Email |
| `PasswordHash` | `nvarchar(max)` | NOT NULL | Hash de la contraseña (bcrypt/PBKDF2) |
| `IsActive` | `bit` | NOT NULL, default `1` | Si el usuario está activo |
| `IsFido2MfaEnabled` | `bit` | NOT NULL, default `0` | Si tiene FIDO2 habilitado |
| `CreatedAtUtc` | `datetime2` | NOT NULL | Fecha de creación |
| `LastLoginAtUtc` | `datetime2` | NULL | Último login exitoso |

**Índices:** `IX_Users_Email` (unique), `IX_Users_Username` (unique)

---

### `UserMfaMethods`

Métodos MFA habilitados por usuario (SMS, Email, FIDO2).

| Columna | Tipo | Constraints | Descripción |
|---------|------|-------------|-------------|
| `Id` | `bigint` | PK, Identity | ID del método |
| `UserId` | `bigint` | NOT NULL, FK → Users | Usuario |
| `Method` | `nvarchar(30)` | NOT NULL | `sms`, `email`, `fido2` |
| `IsEnabled` | `bit` | NOT NULL, default `1` | Si está activo |
| `IsPrimary` | `bit` | NOT NULL | Si es el método principal |
| `IsVerified` | `bit` | NOT NULL | Si fue verificado durante enrollment |
| `ContactValue` | `nvarchar(320)` | NULL | Teléfono o email de contacto |
| `CreatedAtUtc` | `datetime2` | NOT NULL | Fecha de creación |
| `UpdatedAtUtc` | `datetime2` | NOT NULL | Última actualización |

**Índices:** `IX_UserMfaMethods_UserId_Method` (unique), `IX_UserMfaMethods_UserId_IsEnabled`, `IX_UserMfaMethods_Method_ContactValue_Active` (filtered `WHERE IsEnabled = 1`)

---

### `UserFido2Credentials`

Credenciales FIDO2/WebAuthn registradas.

| Columna | Tipo | Constraints | Descripción |
|---------|------|-------------|-------------|
| `Id` | `bigint` | PK, Identity | ID de la credencial |
| `UserId` | `bigint` | NOT NULL, FK → Users | Usuario |
| `CredentialId` | `varbinary(max)` | NOT NULL, UNIQUE | ID de la credencial WebAuthn (base64url) |
| `PublicKey` | `varbinary(max)` | NOT NULL | Clave pública COSE del authenticator |
| `UserHandle` | `varbinary(max)` | NOT NULL | Handle del usuario para el authenticator |
| `SignatureCounter` | `int` (uint) | NOT NULL | Contador de firmas — detecta clonación |
| `AaGuid` | `nvarchar(max)` | NULL | GUID del modelo de authenticator |
| `CredType` | `nvarchar(max)` | NULL | Tipo de credencial (`public-key`) |
| `CreatedAtUtc` | `datetime2` | NOT NULL | Fecha de registro |
| `LastUsedAtUtc` | `datetime2` | NULL | Último uso |

**Índices:** `IX_UserFido2Credentials_CredentialId` (unique)

---

### `UserRecoveryCodeBatches`

Lote de códigos de recuperación (cada usuario tiene 1 batch activo a la vez).

| Columna | Tipo | Constraints | Descripción |
|---------|------|-------------|-------------|
| `Id` | `uniqueidentifier` | PK | ID del batch |
| `UserId` | `bigint` | NOT NULL | Usuario |
| `IssuedAtUtc` | `datetime2` | NOT NULL | Fecha de emisión |
| `ReplacedAtUtc` | `datetime2` | NULL | Fecha en que fue reemplazado |

---

### `UserRecoveryCodes`

Códigos de recuperación individuales (10 por batch).

| Columna | Tipo | Constraints | Descripción |
|---------|------|-------------|-------------|
| `Id` | `uniqueidentifier` | PK | ID del código |
| `BatchId` | `uniqueidentifier` | NOT NULL, FK → UserRecoveryCodeBatches | Batch al que pertenece |
| `UserId` | `bigint` | NOT NULL | Usuario |
| `CodeHash` | `nvarchar(max)` | NOT NULL | Hash del código (formato `XXXX-XXXX-XXXX`) |
| `CreatedAtUtc` | `datetime2` | NOT NULL | Fecha de creación |
| `UsedAtUtc` | `datetime2` | NULL | Fecha de uso — NULL si no usado |

> Los códigos se consumen al usarse y no pueden reutilizarse. El batch se reemplaza al generar nuevos.

---

### `AccessTokenSessions`

Sesiones de access tokens JWT activas/revocadas.

| Columna | Tipo | Constraints | Descripción |
|---------|------|-------------|-------------|
| `Id` | `uniqueidentifier` | PK | ID de la sesión |
| `UserId` | `bigint` | NOT NULL | Usuario |
| `TokenJti` | `nvarchar(100)` | NOT NULL, UNIQUE | JWT ID del token (claim `jti`) |
| `IssuedAtUtc` | `datetime2` | NOT NULL | Fecha de emisión |
| `ExpiresAtUtc` | `datetime2` | NOT NULL | Expiración (15 min desde emisión) |
| `RevokedAtUtc` | `datetime2` | NULL | Fecha de revocación — NULL si activo |
| `RevokeReason` | `nvarchar(100)` | NULL | Razón de revocación (`logout`, `new_login`, etc.) |
| `IpAddress` | `nvarchar(100)` | NULL | IP del cliente |
| `UserAgent` | `nvarchar(500)` | NULL | User-Agent del cliente |

**Índices:** `IX_AccessTokenSessions_TokenJti` (unique), `IX_AccessTokenSessions_UserId_ExpiresAtUtc`, `IX_AccessTokenSessions_Active` (filtered `WHERE RevokedAtUtc IS NULL`)

---

### `RefreshTokenSessions`

Sesiones de refresh tokens con historial de rotación.

| Columna | Tipo | Constraints | Descripción |
|---------|------|-------------|-------------|
| `Id` | `uniqueidentifier` | PK | ID de la sesión |
| `UserId` | `bigint` | NOT NULL | Usuario |
| `TokenHash` | `nvarchar(256)` | NOT NULL, UNIQUE | SHA256 hash del token (nunca texto plano) |
| `AccessTokenSessionId` | `uniqueidentifier` | NOT NULL | FK → AccessTokenSessions |
| `IssuedAtUtc` | `datetime2` | NOT NULL | Fecha de emisión |
| `ExpiresAtUtc` | `datetime2` | NOT NULL | Expiración (5 días desde emisión) |
| `RevokedAtUtc` | `datetime2` | NULL | Fecha de revocación |
| `RevokeReason` | `nvarchar(100)` | NULL | `rotated`, `logout`, `new_login` |
| `LastRotatedAtUtc` | `datetime2` | NULL | Última rotación |
| `PreviousTokenSessionId` | `uniqueidentifier` | NULL, FK self | Token anterior en la cadena de rotación |
| `IpAddress` | `nvarchar(100)` | NULL | IP del cliente |
| `UserAgent` | `nvarchar(500)` | NULL | User-Agent del cliente |

**Índices:** `IX_RefreshTokenSessions_TokenHash` (unique), `IX_RefreshTokenSessions_UserId_ExpiresAtUtc_RevokedAtUtc`, `IX_RefreshTokenSessions_AccessTokenSessionId`, `IX_RefreshTokenSessions_PreviousTokenSessionId`, `IX_RefreshTokenSessions_Active` (filtered `WHERE RevokedAtUtc IS NULL`)

> `PreviousTokenSessionId` forma una cadena de auditoría de rotaciones. Permite reconstruir el historial completo de refresh tokens de una sesión.

---

### `MfaChallenges`

Challenges MFA activos y completados (OTP, recovery code, FIDO2 selection).

| Columna | Tipo | Constraints | Descripción |
|---------|------|-------------|-------------|
| `Id` | `uniqueidentifier` | PK | ID del challenge (también usado como MFA transaction ID) |
| `UserId` | `bigint` | NOT NULL | Usuario |
| `Purpose` | `nvarchar(30)` | NOT NULL | `login`, `enrollment`, `manage_mfa`, `reconfigure` |
| `ContinuationToken` | `nvarchar(100)` | NOT NULL | Token anti-replay del paso actual |
| `StepVersion` | `int` | NOT NULL | Versión del paso — incrementa en cada avance |
| `Method` | `nvarchar(30)` | NULL | `sms`, `email`, `recovery_code`, `fido2` |
| `Provider` | `nvarchar(30)` | NULL | `twilio`, `internal` |
| `ProviderRequestId` | `nvarchar(120)` | NULL | SID de Twilio Verify |
| `Channel` | `nvarchar(30)` | NULL | Canal del proveedor |
| `ContactValue` | `nvarchar(320)` | NULL | Email o teléfono al que se envió el OTP |
| `Status` | `nvarchar(30)` | NOT NULL | Ver tabla de statuses |
| `FailedAttempts` | `int` | NOT NULL, default `0` | Intentos fallidos acumulados |
| `LastFailedAttemptAtUtc` | `datetime2` | NULL | Timestamp del último intento fallido |
| `ExpiresAtUtc` | `datetime2` | NOT NULL | Expiración (5 min configurables) |
| `VerifiedAtUtc` | `datetime2` | NULL | Fecha de verificación exitosa |
| `IpAddress` | `nvarchar(100)` | NULL | IP del cliente |
| `UserAgent` | `nvarchar(500)` | NULL | User-Agent del cliente |
| `CreatedAtUtc` | `datetime2` | NOT NULL | Fecha de creación |

**Statuses:** `pending` → `verified` → `consumed` / `locked` / `expired` / `revoked` / `failed`

**Índices:** `IX_MfaChallenges_UserId_Status_ExpiresAtUtc`, `IX_MfaChallenges_UserId_Purpose_Status_ExpiresAtUtc`, `IX_MfaChallenges_ContinuationToken`, `IX_MfaChallenges_ProviderRequestId`, `IX_MfaChallenges_Status_CreatedAtUtc` (para cleanup)

---

### `MfaTempTokenSessions`

Sesiones de tokens MFA temporales emitidos después del login exitoso con contraseña (flujo MFA).

| Columna | Tipo | Constraints | Descripción |
|---------|------|-------------|-------------|
| `Id` | `uniqueidentifier` | PK | ID de la sesión |
| `UserId` | `bigint` | NOT NULL | Usuario |
| `MfaTransactionId` | `uniqueidentifier` | NOT NULL | FK → MfaChallenges |
| `TokenJti` | `nvarchar(max)` | NOT NULL | JWT ID del MFA token |
| `IssuedAtUtc` | `datetime2` | NOT NULL | Fecha de emisión |
| `ExpiresAtUtc` | `datetime2` | NOT NULL | Expiración (5 min) |
| `ConsumedAtUtc` | `datetime2` | NULL | Fecha de consumo (verificación exitosa) |
| `RevokedAtUtc` | `datetime2` | NULL | Fecha de revocación |
| `IpAddress` | `nvarchar(max)` | NULL | IP del cliente |
| `UserAgent` | `nvarchar(max)` | NULL | User-Agent del cliente |

---

### `MfaLoginEnrollmentSessions`

Sesiones de enrollment MFA durante el login (usuarios sin MFA configurado).

| Columna | Tipo | Constraints | Descripción |
|---------|------|-------------|-------------|
| `Id` | `uniqueidentifier` | PK | ID de la sesión |
| `UserId` | `bigint` | NOT NULL | Usuario |
| `Status` | `nvarchar(max)` | NOT NULL | `pending_method`, `pending_verification`, `ready_to_complete`, `completed`, `expired`, `cancelled` |
| `ContinuationToken` | `nvarchar(max)` | NOT NULL | Token anti-replay |
| `StepVersion` | `int` | NOT NULL | Versión del paso |
| `TokenJti` | `nvarchar(max)` | NOT NULL | JTI del enrollment token |
| `ChallengeId` | `uniqueidentifier` | NULL | FK → MfaChallenges activo |
| `ExpiresAtUtc` | `datetime2` | NOT NULL | Expiración (10 min) |
| `CompletedAtUtc` | `datetime2` | NULL | Fecha de completación |
| `CreatedAtUtc` | `datetime2` | NOT NULL | Fecha de creación |
| `UpdatedAtUtc` | `datetime2` | NOT NULL | Última actualización |

---

### `MfaManagementSessions`

Sesiones de administración de métodos MFA (step-up requerido).

| Columna | Tipo | Constraints | Descripción |
|---------|------|-------------|-------------|
| `Id` | `uniqueidentifier` | PK | ID de la sesión |
| `UserId` | `bigint` | NOT NULL | Usuario |
| `Status` | `nvarchar(max)` | NOT NULL | `step_up_required`, `step_up_completed`, `completed`, `cancelled` |
| `ContinuationToken` | `nvarchar(max)` | NOT NULL | Token anti-replay |
| `StepVersion` | `int` | NOT NULL | Versión del paso |
| `ChallengeId` | `uniqueidentifier` | NULL | FK → MfaChallenges del step-up |
| `ExpiresAtUtc` | `datetime2` | NOT NULL | Expiración |
| `VerifiedAtUtc` | `datetime2` | NULL | Fecha en que completó el step-up |
| `CreatedAtUtc` | `datetime2` | NOT NULL | Fecha de creación |
| `UpdatedAtUtc` | `datetime2` | NOT NULL | Última actualización |

---

### `Fido2Transactions`

Transacciones FIDO2 de un solo uso para registration y assertion.

| Columna | Tipo | Constraints | Descripción |
|---------|------|-------------|-------------|
| `Id` | `uniqueidentifier` | PK | ID de la transacción |
| `UserId` | `bigint` | NOT NULL | Usuario |
| `Type` | `nvarchar(max)` | NOT NULL | `registration` o `assertion` |
| `OptionsJson` | `nvarchar(max)` | NOT NULL | JSON de `PublicKeyCredentialCreationOptions` o `RequestOptions` |
| `IsUsed` | `bit` | NOT NULL | Si ya fue consumida |
| `IpAddress` | `nvarchar(max)` | NOT NULL | IP del cliente |
| `UserAgent` | `nvarchar(max)` | NOT NULL | User-Agent del cliente |
| `CreatedAtUtc` | `datetime2` | NOT NULL | Fecha de creación |
| `ExpiresAtUtc` | `datetime2` | NOT NULL | Expiración |
| `ParentMfaTransactionId` | `uniqueidentifier` | NULL | FK → MfaChallenges del flujo de login |

---

### `AuthenticationAuditEvents`

Log de todos los intentos de autenticación (login, MFA verify, etc.).

| Columna | Tipo | Constraints | Descripción |
|---------|------|-------------|-------------|
| `Id` | `bigint` | PK, Identity | ID del evento |
| `OccurredAtUtc` | `datetime2` | NOT NULL | Timestamp del evento |
| `UserId` | `bigint` | NULL | Usuario (NULL si no fue identificado) |
| `UsernameOrEmail` | `nvarchar(max)` | NULL | Input del usuario |
| `Stage` | `nvarchar(max)` | NOT NULL | `password_login`, `mfa_challenge_verify`, etc. |
| `Method` | `nvarchar(max)` | NOT NULL | `password`, `sms`, `email`, `fido2`, etc. |
| `Outcome` | `nvarchar(max)` | NOT NULL | `success` o `failure` |
| `FailureReason` | `nvarchar(max)` | NULL | Descripción del fallo |
| `IpAddress` | `nvarchar(max)` | NULL | IP del cliente |
| `UserAgent` | `nvarchar(max)` | NULL | User-Agent del cliente |
| `CorrelationId` | `nvarchar(max)` | NULL | ID de correlación de la request |

---

### `SecurityAuditEvents`

Log de eventos de seguridad del sistema (token replay, rate limits, etc.).

| Columna | Tipo | Constraints | Descripción |
|---------|------|-------------|-------------|
| `Id` | `bigint` | PK, Identity | ID del evento |
| `OccurredAtUtc` | `datetime2` | NOT NULL | Timestamp del evento |
| `Category` | `nvarchar(max)` | NOT NULL | e.g. `Authentication` |
| `EventType` | `nvarchar(max)` | NOT NULL | e.g. `auth.fido2.mfa_token_validation` |
| `Severity` | `nvarchar(max)` | NOT NULL | `Information`, `Warning`, `Error`, `Critical` |
| `Outcome` | `nvarchar(max)` | NOT NULL | `success` o `failure` |
| `UserId` | `bigint` | NULL | Usuario |
| `UsernameOrEmail` | `nvarchar(max)` | NULL | Input del usuario |
| `IpAddress` | `nvarchar(max)` | NULL | IP del cliente |
| `UserAgent` | `nvarchar(max)` | NULL | User-Agent |
| `CorrelationId` | `nvarchar(max)` | NULL | ID de correlación |
| `RequestPath` | `nvarchar(max)` | NULL | Path de la request |
| `HttpMethod` | `nvarchar(max)` | NULL | Verbo HTTP |
| `FailureReason` | `nvarchar(max)` | NULL | Descripción del fallo |
| `DetailsJson` | `nvarchar(max)` | NULL | Detalles extra en JSON |

---

### `MfaSessions` *(legacy)*

Tabla de sesiones MFA de uso general. Contiene campos combinados de varios tipos de sesión.

| Columna | Tipo | Descripción |
|---------|------|-------------|
| `Id` | `uniqueidentifier` | PK |
| `UserId` | `bigint` | Usuario |
| `SessionType` | `nvarchar(max)` | `temp_token` o `login_enrollment` |
| `TokenJti` | `nvarchar(max)` | JTI del token asociado |
| `Status` | `nvarchar(max)` | Estado de la sesión |
| `ContinuationToken` | `nvarchar(max)` | Token anti-replay |
| `MfaTransactionId` | `uniqueidentifier` | FK → MfaChallenges |
| `ChallengeId` | `uniqueidentifier` | FK → MfaChallenges |
| `ExpiresAtUtc` | `datetime2` | Expiración |
| `CompletedAtUtc` | `datetime2` | Completación |
| `ConsumedAtUtc` | `datetime2` | Consumo |
| `RevokedAtUtc` | `datetime2` | Revocación |
| `IpAddress` / `UserAgent` | `nvarchar(max)` | Cliente |

> ⚠️ Esta tabla es una versión legacy. Los flujos activos usan `MfaTempTokenSessions` y `MfaLoginEnrollmentSessions` en lugar de esta tabla.

---

## Convenciones

| Convención | Descripción |
|------------|-------------|
| Todos los timestamps en UTC | Columnas `*AtUtc` |
| PKs de entidades de usuario | `bigint` identity |
| PKs de sesiones/transacciones | `uniqueidentifier` (GUID) |
| Tokens sensibles | Nunca texto plano — siempre hash SHA256 o bcrypt |
| Recovery codes | Almacenados como hash, nunca el valor original |
| Soft delete | No usado — registros se marcan como revocados/expirados |
| Cleanup automático | `CleanupService` ejecuta cada hora — elimina registros expirados/revocados old |
