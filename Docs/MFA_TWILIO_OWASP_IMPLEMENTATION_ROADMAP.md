# Roadmap de implementacion - MFA Twilio + OWASP

## 1. Objetivo

Ejecutar la implementacion de cambios MFA en oleadas controladas, minimizando riesgo de regresion y priorizando controles de seguridad P0/P1.

Este roadmap aterriza la matriz de brechas en un plan de sprints con:

- Alcance por sprint.
- Cambios por capa y archivos objetivo.
- Orden de migraciones.
- Feature flags y estrategia de rollout.
- Plan de pruebas de regresion por oleada.

## 2. Supuestos de planificacion

- Duracion sugerida por sprint: 1 semana.
- Equipo minimo: 1 backend + 1 QA/security + 1 reviewer de arquitectura.
- Release progresivo detras de flags para evitar cortes abruptos.

## 3. Mapa de oleadas

- Oleada 1: Seguridad critica de flujo (P0).
- Oleada 2: Integridad de contratos y estados (P1).
- Oleada 3: Hardening avanzado y riesgo adaptativo.

## 4. Feature flags propuestas

- MFA_MANAGEMENT_STEPUP_REQUIRED
- MFA_CONTINUATION_TOKEN_V2
- MFA_PROBLEMDETAILS_ERRORS
- MFA_CHALLENGE_STATE_MACHINE_V2
- MFA_RISK_ADAPTIVE_CONTROLS
- MFA_TWILIO_FRAUD_GUARD_ENFORCED

Regla de activacion:

1. Activar por entorno dev.
2. Activar en staging con smoke tests.
3. Activar canary en produccion (5%-10%).
4. Activacion total tras validacion de metricas y auditoria.

## 5. Orden de migraciones de base de datos

### Migracion M1 - MFA Flow Sessions

Objetivo:

- Persistir sesiones de flujo MFA con proposito inmutable y version de paso.

Cambios:

- Nueva tabla sugerida: MfaFlowSessions.
- Campos minimos:
  - Id (guid)
  - UserId
  - Purpose (login_enrollment, login_challenge, manage_mfa, account_recovery)
  - Status (pending, completed, revoked, expired, blocked)
  - CurrentStepVersion
  - ExpiresAtUtc
  - CreatedAtUtc
  - UpdatedAtUtc

### Migracion M2 - Continuation Tokens

Objetivo:

- Soportar rotacion y consumo atomico de continuation token.

Cambios:

- Nueva tabla sugerida: MfaFlowStepTokens.
- Campos minimos:
  - Id (guid)
  - FlowSessionId
  - StepVersion
  - TokenHash
  - IsConsumed
  - ConsumedAtUtc
  - ExpiresAtUtc

### Migracion M3 - Challenge/Enrollment State Machine V2

Objetivo:

- Extender semantica de estados para anti replay y concurrencia.

Cambios:

- Extender entidad de challenge/enrollment con estados:
  - consumed
  - superseded
  - blocked
  - revoked
- Campos sugeridos:
  - AttemptCount
  - BlockedUntilUtc
  - SupersededByChallengeId (nullable)

### Migracion M4 - Management Session + Step-Up

Objetivo:

- Requerir step-up para operaciones sensibles de administracion MFA.

Cambios:

- Nueva tabla sugerida: MfaManagementSessions.
- Campos minimos:
  - Id (guid)
  - UserId
  - Status (step_up_required, step_up_completed, completed, expired, revoked)
  - StepUpMethod
  - ExpiresAtUtc
  - CreatedAtUtc

### Migracion M5 - Soporte de riesgo y trazabilidad

Objetivo:

- Permitir evaluacion adaptativa y decisiones auditables.

Cambios:

- Campos sugeridos en sesiones/desafios:
  - RiskLevel
  - RiskSignalsJson
  - PolicyDecision

## 6. Plan por sprint

## Sprint 1 - Base de seguridad de flujo (P0)

Objetivos:

- Introducir flujo MFA formal y continuation token rotatorio v2.
- Bloquear replay de avance de flujo con control atomico.

Brechas objetivo:

- MFA-CORE-004
- MFA-M-001
- MFA-L-001

Cambios por capa:

- Entidades/EF:
  - MfaFlowSessions
  - MfaFlowStepTokens
- Repositorios:
  - Interfaces y implementaciones de flow session/token con CAS (compare-and-swap).
- Servicios:
  - Nuevo servicio de orquestacion de flujo MFA.
- Controladores:
  - Ajustar endpoints actuales para recibir/emitir continuation token v2.
- Middleware/Errores:
  - Introducir 409 para MFA_FLOW_ALREADY_ADVANCED.
  - Introducir 410 para MFA_FLOW_EXPIRED.

Archivos objetivo (referencia inicial):

- Services/Implementatons/MfaService.cs
- Controllers/MfaController.cs
- Data/ApplicationDbContext.cs
- Data/Configurations/*
- Data/Repositories/Interfaces/*
- Data/Repositories/Implementations/*
- Constants/MfaChallengeStatuses.cs

Flags:

- MFA_CONTINUATION_TOKEN_V2 = ON en dev/staging.
- MFA_CHALLENGE_STATE_MACHINE_V2 = OFF (aun no inicia).

Pruebas de regresion del sprint:

- Reuso de continuation token debe responder 409.
- Flujo vencido debe responder 410.
- Login con MFA sigue funcionando en camino feliz.

## Sprint 2 - Step-up obligatorio para administracion MFA (P0)

Objetivos:

- Proteger alta/baja/reemplazo con management session + step-up.
- Evitar takeover con access token robado.

Brechas objetivo:

- MFA-CORE-003
- MFA-D-001
- MFA-D-002
- MFA-E-001

Cambios por capa:

- Entidades/EF:
  - MfaManagementSessions.
- API:
  - POST /api/mfa/management-sessions
  - POST /api/mfa/management-sessions/step-up/challenges
  - POST /api/mfa/management-sessions/step-up/challenges/{id}/verify
  - POST /api/mfa/management-sessions/complete
- Servicios:
  - Guardas para remove/reconfigure/enroll con management token.
- Controladores:
  - Gatear endpoints sensibles detras de sesion de management validada.

Archivos objetivo (referencia inicial):

- Controllers/MfaController.cs
- Services/Interfaces/IMfaService.cs
- Services/Implementatons/MfaService.cs
- Constants/MfaChallengePurposes.cs

Flags:

- MFA_MANAGEMENT_STEPUP_REQUIRED = ON en dev/staging.

Pruebas de regresion del sprint:

- Access token sin step-up no puede modificar metodos.
- Step-up exitoso habilita ventana corta de gestion.
- Expiracion de management session invalida operaciones pendientes.

## Sprint 3 - Estados robustos, intentos y bloqueo (P0/P1)

Objetivos:

- Endurecer maquina de estados challenge/enrollment.
- Aplicar contador de intentos y bloqueo progresivo.

Brechas objetivo:

- MFA-CORE-005
- MFA-K-001
- MFA-C-002

Cambios por capa:

- Entidades/EF:
  - AttemptCount, BlockedUntilUtc, SupersededByChallengeId.
- Servicios:
  - Politica de intentos por challenge.
  - Estado consumed/superseded en verify/resend.
- API/Errores:
  - Mensaje neutro para codigo invalido o vencido.

Archivos objetivo (referencia inicial):

- Entities/MfaChallenge.cs
- Services/Implementatons/MfaService.cs
- Data/Repositories/Implementations/MfaChallengeRepository.cs

Flags:

- MFA_CHALLENGE_STATE_MACHINE_V2 = ON en dev/staging.

Pruebas de regresion del sprint:

- Doble verify concurrente: solo una solicitud avanza.
- Reintentos excedidos bloquean challenge.
- Challenge superseded no puede verificarse.

## Sprint 4 - Contratos HTTP estables y anti enumeracion (P1)

Objetivos:

- Estandarizar errores con ProblemDetails.
- Uniformar anti enumeracion y Retry-After en 429.

Brechas objetivo:

- MFA-API-001
- MFA-API-002
- MFA-CORE-006
- MFA-SEC-001

Cambios por capa:

- Middleware:
  - Mapper de excepciones a ProblemDetails.
- API:
  - Codigos internos estables: MFA_FLOW_EXPIRED, MFA_FLOW_ALREADY_ADVANCED, INVALID_OR_EXPIRED_CODE.
- Rate limiter:
  - Header Retry-After.
- Twilio mapping:
  - Falla temporal proveedor -> 503.

Archivos objetivo (referencia inicial):

- Program.cs
- Extensions/ServiceCollectionExtensions.cs
- Controllers/*
- Services/Implementatons/TwilioOtpService.cs

Flags:

- MFA_PROBLEMDETAILS_ERRORS = ON en dev/staging.

Pruebas de regresion del sprint:

- Validar codigos HTTP esperados por escenario.
- Validar ausencia de leak de mensajes internos de proveedor.

## Sprint 5 - Enrollment/login sessions completas y replace seguro (P1)

Objetivos:

- Formalizar completion de enrollment session.
- Mejorar flujo de reemplazo de telefono/email sin overwrite silencioso.

Brechas objetivo:

- MFA-B-001
- MFA-B-002
- MFA-B-003
- MFA-F-001
- MFA-N-001

Cambios por capa:

- API:
  - POST /api/mfa/enrollment-sessions/complete
  - Endpoints de cancelacion por tipo de flujo.
- Servicios:
  - Reglas de requisito minimo satisfecho.
  - Switchover old/new con notificacion al canal anterior.

Archivos objetivo (referencia inicial):

- Controllers/MfaController.cs
- Services/Implementatons/MfaService.cs
- DTOs/Mfa/*

Pruebas de regresion del sprint:

- Enrollment obligatorio no entrega acceso completo hasta complete.
- Reemplazo conserva canal anterior hasta verificacion del nuevo.
- Cancelacion revoca tokens y estados parciales.

## Sprint 6 - Recovery avanzado y riesgo adaptativo (P1/P2)

Objetivos:

- Completar flujo account_recovery.
- Introducir controles de riesgo adaptativo y fraude SMS.

Brechas objetivo:

- MFA-H-001
- MFA-I-001
- MFA-Q-001
- MFA-SEC-002
- MFA-G-001

Cambios por capa:

- API:
  - Recovery flow dedicado cuando no hay factores disponibles.
  - Recovery code regenerate bajo step-up.
- Servicios:
  - Evaluador de riesgo minimo (IP, dispositivo, geografia, velocity).
  - Politicas de factor fuerte en operaciones criticas.
- Seguridad operacional:
  - Restriccion de pais/prefijo, alertas de pumping.

Archivos objetivo (referencia inicial):

- Services/Implementatons/MfaService.cs
- Services/Implementatons/AuthService.cs
- Services/Implementatons/TwilioOtpService.cs
- Data/Repositories/Implementations/UserRecoveryCodeRepository.cs

Flags:

- MFA_RISK_ADAPTIVE_CONTROLS = ON en staging.
- MFA_TWILIO_FRAUD_GUARD_ENFORCED = ON progresivo.

Pruebas de regresion del sprint:

- Riesgo alto obliga factor mas fuerte o step-up adicional.
- Regeneracion de recovery codes invalida lote anterior.
- Eventos de fraude generan respuesta y auditoria esperada.

## 7. Plan de pruebas de regresion por oleada

## Suite Oleada 1 (critica)

- Login con MFA happy path.
- Replay de verify y replay de complete.
- Token anterior reutilizado.
- Flujo expirado.

## Suite Oleada 2 (integridad)

- Step-up obligatorio para cambios MFA.
- Eliminacion del ultimo factor bloqueada segun politica.
- Reconfigure con validacion de ownership y estado.
- 409/410/429/503 segun contrato.

## Suite Oleada 3 (hardening)

- Riesgo adaptativo por IP/dispositivo.
- Casos de SMS pumping simulados.
- Recuperacion sin factores disponibles.
- Validacion de notificaciones y auditoria integral.

## 8. Criterios Go/No-Go por release

Go:

- 0 bypasses P0 en pruebas de seguridad.
- 0 leaks de secretos en logs/responses.
- 100% de escenarios criticos del sprint en verde.

No-Go:

- Falla de control step-up en operaciones sensibles.
- Emision de token final en flujo inconsistente/replay.
- Errores HTTP fuera de contrato en escenarios criticos.

## 9. Observabilidad minima requerida

Metricas:

- mfa.challenge.started
- mfa.challenge.verified
- mfa.challenge.blocked
- mfa.flow.expired
- mfa.flow.replay_blocked
- mfa.management.stepup.required
- mfa.management.stepup.completed
- mfa.twilio.provider_errors

Alertas:

- Pico de verify fallidos por destino/prefijo.
- Incremento de 503 de proveedor.
- Aumento de 409 MFA_FLOW_ALREADY_ADVANCED por encima de umbral.

## 10. Checklist final de cierre

- Todas las brechas P0 y P1 de la matriz tienen estado Cerrado o Mitigado.
- Roadmap ejecutado con flags encendidos al 100% en produccion.
- Pruebas funcionales, seguridad y concurrencia completas.
- Auditoria y notificaciones alineadas con plan funcional.
- Documentacion de API y runbooks operativos actualizados.
