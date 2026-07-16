# Performance Review — Escala 1,000,000 usuarios

Última actualización: 2026-07-16.

---

## Resumen Ejecutivo

| # | Problema | Impacto | Severidad | Estado |
|---|---------|---------|-----------|-------|
| 1 | `RateLimitingService` — memory leak sin cleanup | Crash OOM | 🔴 Crítico | ✅ Implementado |
| 2 | `GetSessionsAsync` — paginación en memoria | OOM + timeout | 🔴 Crítico | ✅ Implementado |
| 3 | `GetUsersAsync` — `IN (1M ids)` en SQL | Query SQL inviable | 🔴 Crítico | ✅ Implementado |
| 4 | `GetSummaryAsync` — 12 queries secuenciales | Latencia >5s | 🔴 Crítico | ⚠️ **Pendiente** |
| 5 | `RevokeAllActiveByUserAsync` — N UPDATE individuales | Login lento | 🟡 Medio | ✅ Implementado |
| 6 | `SessionFactory` — 2 SaveChangesAsync sin transacción | Inconsistencia posible | 🟡 Medio | ✅ Implementado |
| 7 | Índices faltantes en tablas de audit y sesiones | Queries lentas en Monitor | 🟡 Medio | ✅ Implementado |
| 8 | `MfaChallengeRepository.GetByIdAsync` sin `AsNoTracking` | Presión en EF tracker | 🟢 Bajo | ⚠️ Pendiente |
| 9 | `DistributedLockService` — busy-wait 10ms | Thread pool pressure | 🟢 Bajo | ⚠️ Pendiente |

---

## 🔴 Hallazgo 1 — `RateLimitingService` Memory Leak

### Problema

`RateLimitingService` es un `Singleton` que usa un `ConcurrentDictionary` que **nunca limpia entradas expiradas**.

```csharp
// RateLimitingService.cs
private readonly ConcurrentDictionary<string, RateLimitBucket> _buckets;
// No hay ningún mecanismo de limpieza de entradas antiguas
```

Con 1M usuarios y múltiples rate limit keys por usuario:

| Key | Cantidad |
|-----|---------|
| `login_{IP}` | Por IP única |
| `mfa_verify_{userId}` | 1M entradas |
| `mfa_start_{userId}` | 1M entradas |
| `enrollment_otp_{userId}` | 1M entradas |
| `fido2_enroll_{userId}` | 1M entradas |
| `fido2_auth_{userId}` | 1M entradas |

→ **Millones de entradas en memoria acumuladas permanentemente → OOM crash.**

### Fix Corto Plazo — Cleanup periódico

```csharp
// RateLimitingService.cs — agregar timer de limpieza
private readonly Timer _cleanupTimer;
private const int MaxWindowSeconds = 900; // ventana más larga usada

public RateLimitingService(ILogger<RateLimitingService> logger)
{
    _logger = logger;
    _buckets = new ConcurrentDictionary<string, RateLimitBucket>();
    // Limpiar entradas expiradas cada 5 minutos
    _cleanupTimer = new Timer(_ => CleanupExpiredBuckets(),
        null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
}

private void CleanupExpiredBuckets()
{
    var cutoff = DateTime.UtcNow.AddSeconds(-MaxWindowSeconds);
    var removed = 0;
    foreach (var key in _buckets.Keys)
        if (_buckets.TryGetValue(key, out var b) && b.FirstAttemptAt < cutoff)
            if (_buckets.TryRemove(key, out _)) removed++;

    if (removed > 0)
        _logger.LogDebug("Rate limiter: removed {Count} expired buckets.", removed);
}
```

### Fix Largo Plazo — Redis (recomendado para producción)

```csharp
// Reemplazar registro en DI con implementación Redis
services.AddSingleton<IRateLimitingService, RedisRateLimitingService>();
// IRateLimitingService ya tiene el contrato correcto — solo cambiar la implementación
```

Redis maneja TTL automático y es compartido entre múltiples instancias del servidor.

---

## 🔴 Hallazgo 2 — `GetSessionsAsync` — Paginación en Memoria

### Problema

```csharp
// MonitorService.cs
// Trae TODOS los registros a memoria, luego pagina en C#
var accessItems = await accessQuery.Select(...).ToListAsync(ct); // potencialmente millones
var refreshItems = await refreshQuery.Select(...).ToListAsync(ct); // potencialmente millones

var allItems = accessItems.Concat(refreshItems)
    .OrderByDescending(x => x.IssuedAtUtc)
    .ToList(); // ordenación en memoria

var pagedItems = allItems.Skip((page - 1) * pageSize).Take(pageSize).ToList(); // paginación en C#
```

Con 1M usuarios activos y sesiones de 5 días: **potencialmente decenas de millones de filas traídas a memoria por un solo request.**

### Fix — Paginación separada por tipo en SQL

```csharp
// Separar en dos queries con paginación real en la DB
// Si type = "access" o null:
var accessItems = await accessQuery
    .OrderByDescending(x => x.IssuedAtUtc)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .Select(...)
    .ToListAsync(ct);

// Si type = "refresh" o null — misma lógica
// El totalCount también va directo a la DB, no en memoria
```

Si se necesita mostrar access + refresh mezclados y ordenados, separar en dos endpoints distintos: `GET /api/monitor/sessions?type=access` y `GET /api/monitor/sessions?type=refresh`.

---

## 🔴 Hallazgo 3 — `GetUsersAsync` — `IN (1M ids)` en SQL

### Problema

```csharp
// MonitorService.cs
var userIds = await userQuery.Select(x => x.Id).ToListAsync(ct);
// ↑ Carga hasta 1M IDs en memoria (List<long> = ~8MB solo de IDs)

var mfaMethods = await _context.UserMfaMethods
    .Where(x => x.IsEnabled && userIds.Contains(x.UserId))
    // ↑ Genera: WHERE UserId IN (1, 2, 3, ..., 1000000)
    // SQL Server no puede procesar IN con 1M valores — timeout o error
    .GroupBy(x => x.UserId)
    .ToListAsync(ct);
```

### Fix — JOIN directo en EF

```csharp
// Reemplazar el bloque de userIds + Contains con un JOIN real en SQL
var query =
    from u in _context.Users.AsNoTracking()
    where (!isActive.HasValue || u.IsActive == isActive.Value)
    select new
    {
        User = u,
        Methods = _context.UserMfaMethods
            .Where(m => m.UserId == u.Id && m.IsEnabled)
            .Select(m => m.Method)
            .ToList(),
    };

// Para hasMfa filter — usar subquery EXISTS
if (hasMfa == true)
    query = query.Where(x => _context.UserMfaMethods.Any(m => m.UserId == x.User.Id && m.IsEnabled));
else if (hasMfa == false)
    query = query.Where(x => !_context.UserMfaMethods.Any(m => m.UserId == x.User.Id && m.IsEnabled));
```

---

## 🔴 Hallazgo 4 — `GetSummaryAsync` — 12 Queries Secuenciales

### Problema

```csharp
// MonitorService.cs — cada await es un round-trip a la BD en serie
var loginsToday          = await _context.AuthenticationAuditEvents.CountAsync(...);  // trip 1
var loginFailuresToday   = await _context.AuthenticationAuditEvents.CountAsync(...);  // trip 2
var activeAccessSessions = await _context.AccessTokenSessions.CountAsync(...);         // trip 3
var activeRefreshSessions= await _context.RefreshTokenSessions.CountAsync(...);        // trip 4
var pendingChallenges    = await _context.MfaChallenges.CountAsync(...);               // trip 5
var lockedChallenges     = await _context.MfaChallenges.CountAsync(...);               // trip 6
// ... 6 más
```

Con tablas de millones de filas, cada COUNT puede tardar 100ms–500ms.
**Total: 12 × 300ms promedio = ~3.6 segundos por request de summary.**

### Fix — `Task.WhenAll` para queries paralelas

```csharp
// Ejecutar los 12 CountAsync en paralelo
var t1  = _context.AuthenticationAuditEvents.CountAsync(x => x.OccurredAtUtc >= startOfDay, ct);
var t2  = _context.AuthenticationAuditEvents.CountAsync(x => x.OccurredAtUtc >= startOfDay && x.Outcome == "failure", ct);
var t3  = _context.AccessTokenSessions.CountAsync(x => x.RevokedAtUtc == null && x.ExpiresAtUtc > now, ct);
var t4  = _context.RefreshTokenSessions.CountAsync(x => x.RevokedAtUtc == null && x.ExpiresAtUtc > now, ct);
var t5  = _context.MfaChallenges.CountAsync(x => x.Status == "pending" && x.ExpiresAtUtc > now, ct);
var t6  = _context.MfaChallenges.CountAsync(x => x.Status == "locked", ct);
var t7  = _context.MfaLoginEnrollmentSessions.CountAsync(x => x.Status != "completed" && ..., ct);
var t8  = _context.MfaLoginEnrollmentSessions.CountAsync(x => x.Status == "completed", ct);
var t9  = _context.SecurityAuditEvents.CountAsync(x => x.OccurredAtUtc >= startOfDay && x.Severity == "Warning", ct);
var t10 = _context.SecurityAuditEvents.CountAsync(x => x.OccurredAtUtc >= startOfDay && (x.Severity == "Error" || x.Severity == "Critical"), ct);
var t11 = _context.Users.CountAsync(ct);
var t12 = _context.Users.CountAsync(x => x.IsActive, ct);
var t13 = _context.UserMfaMethods.Where(x => x.IsEnabled).Select(x => x.UserId).Distinct().CountAsync(ct);

await Task.WhenAll(t1, t2, t3, t4, t5, t6, t7, t8, t9, t10, t11, t12, t13);
```

**Reducción estimada:** De ~3.6s → ~300ms (la query más lenta).

> ⚠️ Nota: EF Core comparte el `DbContext` entre queries paralelas. Para queries `COUNT` simples en el mismo contexto está bien, pero si se hacen transformaciones complejas puede requerir contextos separados o usar `IDbContextFactory`.

---

## 🟡 Hallazgo 5 — `RevokeAllActiveByUserAsync` — N UPDATE Individuales

### Problema

```csharp
// AccessTokenSessionRepository.cs y RefreshTokenSessionRepository.cs
var sessions = await _context.AccessTokenSessions
    .Where(x => x.UserId == userId && x.RevokedAtUtc == null && x.ExpiresAtUtc > now)
    .ToListAsync(ct);   // SELECT → trae todos a memoria

foreach (var session in sessions)   // N UPDATE statements individuales
{
    session.RevokedAtUtc = now;
    session.RevokeReason = reason;
}

await _context.SaveChangesAsync(ct);
```

EF Core genera `UPDATE AccessTokenSessions SET ... WHERE Id = @id` por cada fila. Si un usuario tiene 10 sesiones activas = 10 queries individuales.

### Fix — `ExecuteUpdateAsync` (single SQL UPDATE)

```csharp
await _context.AccessTokenSessions
    .Where(x => x.UserId == userId && x.RevokedAtUtc == null && x.ExpiresAtUtc > now)
    .ExecuteUpdateAsync(s => s
        .SetProperty(x => x.RevokedAtUtc, now)
        .SetProperty(x => x.RevokeReason, reason), ct);
```

Genera: `UPDATE AccessTokenSessions SET RevokedAtUtc = @now, RevokeReason = @reason WHERE UserId = @userId AND ...`
**Un solo round-trip, sin cargar datos en memoria.**

El mismo fix aplica a `RefreshTokenSessionRepository.RevokeAllByUserAsync`.

---

## 🟡 Hallazgo 6 — `SessionFactory` — 2 Transacciones Separadas

### Problema

```csharp
// SessionFactory.cs
await _accessTokenSessionRepository.AddAsync(accessSession, ct);   // SaveChangesAsync #1
await _refreshTokenSessionRepository.AddAsync(refreshSession, ct);  // SaveChangesAsync #2
// Si el segundo falla → AccessTokenSession existe sin RefreshTokenSession
```

Dos round-trips separados y sin transacción → inconsistencia posible si falla entre los dos.

### Fix — Una sola transacción

```csharp
await using var transaction = await _context.Database.BeginTransactionAsync(ct);
_context.AccessTokenSessions.Add(accessSession);
_context.RefreshTokenSessions.Add(refreshSession);
await _context.SaveChangesAsync(ct);
await transaction.CommitAsync(ct);
// 1 round-trip en lugar de 2, atómico
```

---

## 🟡 Hallazgo 7 — Índices Faltantes

### Tablas de Audit — Severity sin índice

```csharp
// GetSummaryAsync usa:
// WHERE OccurredAtUtc >= startOfDay AND Severity = 'Warning'
// Índice actual: (Severity, OccurredAtUtc) NO existe
// Solo existe: (OccurredAtUtc), (Category, OccurredAtUtc), (Outcome, OccurredAtUtc)
```

**Agregar a `SecurityAuditEventConfiguration`:**
```csharp
builder.HasIndex(x => new { x.Severity, x.OccurredAtUtc });
```

**Agregar a `AuthenticationAuditEventConfiguration`:**
```csharp
builder.HasIndex(x => new { x.Stage, x.OccurredAtUtc });
```

### `AccessTokenSessions` / `RefreshTokenSessions` — Sin filtered index para sesiones activas

El monitor y el login consultan frecuentemente `WHERE RevokedAtUtc IS NULL AND ExpiresAtUtc > now`. Un filtered index sobre solo las sesiones activas sería más eficiente:

```csharp
// AccessTokenSessionConfiguration.cs
builder.HasIndex(x => new { x.UserId, x.ExpiresAtUtc })
       .HasFilter("[RevokedAtUtc] IS NULL")
       .HasDatabaseName("IX_AccessTokenSessions_Active");

// RefreshTokenSessionConfiguration.cs
builder.HasIndex(x => new { x.UserId, x.ExpiresAtUtc })
       .HasFilter("[RevokedAtUtc] IS NULL")
       .HasDatabaseName("IX_RefreshTokenSessions_Active");
```

### `MfaTempTokenSessions` — Sin índice en `TokenJti`

`GetActiveByJtiAsync` es llamado en **cada autenticación FIDO2** sin un índice en `TokenJti`:
```csharp
// Agregar a la configuración de MfaTempTokenSessions
builder.HasIndex(x => x.TokenJti).IsUnique();
```

### `MfaChallenges` — Sin índice en `(Status, CreatedAtUtc)` para cleanup

```csharp
// MfaChallengeConfiguration.cs — para el CleanupService
builder.HasIndex(x => new { x.Status, x.CreatedAtUtc });
```

---

## 🟢 Hallazgo 8 — `MfaChallengeRepository.GetByIdAsync` Sin `AsNoTracking`

### Problema

```csharp
// MfaChallengeRepository.cs
public Task<MfaChallenge?> GetByIdAsync(Guid id, CancellationToken ct)
    => _context.MfaChallenges.FirstOrDefaultAsync(x => x.Id == id, ct);
    // Sin AsNoTracking → EF registra en change tracker aunque la entidad se va a modificar de inmediato
```

Llamado en cada `VerifyChallengeAsync` — el endpoint de mayor frecuencia. El change tracker acumula objetos rastreados si el contexto vive mucho tiempo.

### Fix

```csharp
// Para lectura + modificación inmediata: tracking es correcto → dejar como está
// Para lectura + solo validación (sin UpdateAsync después): usar AsNoTracking
// Agregar overload o flag:
public Task<MfaChallenge?> GetByIdAsync(Guid id, bool trackChanges, CancellationToken ct)
{
    var query = _context.MfaChallenges.AsQueryable();
    if (!trackChanges) query = query.AsNoTracking();
    return query.FirstOrDefaultAsync(x => x.Id == id, ct);
}
```

---

## 🟢 Hallazgo 9 — `DistributedLockService` — Busy-Wait en Hot Path

### Problema

```csharp
// DistributedLockService.cs
while (DateTime.UtcNow < deadline)   // hasta 500 iteraciones
    await Task.Delay(10, cancellationToken);  // 10ms × 500 = 5s de busy-wait
```

Con 1M usuarios concurrentes en pico de autenticación, múltiples threads en busy-wait de 10ms consumen thread pool innecesariamente. El timeout de 5s también bloquea requests legítimos.

### Fix Largo Plazo — `SemaphoreSlim` (single-instance) o Redis Redlock (multi-instance)

```csharp
// Para single-instance: SemaphoreSlim por key
private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();

public async Task<string?> AcquireLockAsync(string key, int timeoutSeconds, CancellationToken ct)
{
    var semaphore = _semaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
    var acquired = await semaphore.WaitAsync(TimeSpan.FromSeconds(timeoutSeconds), ct);
    return acquired ? key : null; // lockId = key para simplificar
}
```

---

## Plan de Implementación Recomendado

### Sprint 1 — Implementado ✅
1. **Hallazgo 1** — `RateLimitingService`: cleanup `Timer` cada 5 min + `IDisposable`
2. **Hallazgo 5** — `RevokeAllActiveByUserAsync`: `ExecuteUpdateAsync` en `AccessTokenSessionRepository` y `RefreshTokenSessionRepository`
3. **Hallazgo 6** — `SessionFactory`: transacción única + un solo `SaveChangesAsync`

### Sprint 2 — Implementado ✅
4. **Hallazgo 2** — `GetSessionsAsync`: paginación SQL por tipo con métodos privados `GetAccessSessionsPagedAsync` / `GetRefreshSessionsPagedAsync`
5. **Hallazgo 3** — `GetUsersAsync`: `EXISTS` subquery + MFA methods cargados solo para la página actual (max 100 rows)

### Sprint 3 — Implementado ✅
6. **Hallazgo 7** — 6 índices nuevos + migración `AddPerformanceIndexes`:
   - `(Severity, OccurredAtUtc)` en `SecurityAuditEvents`
   - `(Stage, OccurredAtUtc)` en `AuthenticationAuditEvents`
   - `IX_AccessTokenSessions_Active` filtered `WHERE RevokedAtUtc IS NULL`
   - `IX_RefreshTokenSessions_Active` filtered `WHERE RevokedAtUtc IS NULL`
   - `(Status, CreatedAtUtc)` en `MfaChallenges` para cleanup

### Pendiente ⚠️
7. **Hallazgo 4** — `GetSummaryAsync`: `Task.WhenAll` para 12 queries paralelas
8. **Hallazgo 8** — `MfaChallengeRepository`: el tracking es requerido para `UpdateAsync` inmediato; requiere refactor de contrato
9. **Hallazgo 9** — `DistributedLockService`: `SemaphoreSlim` (single-instance) o Redis Redlock (multi-instance)

### Backlog — Infraestructura
10. Reemplazar `RateLimitingService` con Redis para multi-instancia y TTL automático
11. Reemplazar `DistributedLockService` con Redis Redlock
