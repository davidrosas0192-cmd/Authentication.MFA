# Matriz de brechas prellenada - MFA Twilio + OWASP

## 1. Objetivo

Traducir el plan funcional de MFA (Twilio + OWASP) en una matriz de brechas accionable sobre el estado actual del repositorio.

Esta version esta prellenada con evidencia tecnica inicial y sirve como backlog base para implementacion.

## 2. Escala usada

- Estado: Cumplido | Parcial | No implementado | Ambiguo
- Prioridad:
  - P0: riesgo directo de bypass, replay, secuestro de sesion o perdida de control MFA
  - P1: seguridad alta e integridad de flujo
  - P2: hardening, consistencia o mejoras operativas
- Esfuerzo: S | M | L

## 3. Resumen ejecutivo inicial

- Fortalezas actuales:
  - Login con MFA token temporal y seleccion de metodo.
  - Enrollment/verify para sms y email.
  - Recovery codes hash + consumo one-time.
  - Auditoria de eventos clave.
- Brechas mayores:
  - No existe modelo formal de continuation token rotatorio por paso.
  - No existe flow de manage_mfa con step-up obligatorio previo a cambios sensibles.
  - Falta estandar uniforme de estados avanzados (consumed/superseded/blocked/revoked) para challenge/enrollment/flow.
  - Falta contrato de errores ProblemDetails uniforme para 409/410/429/503 segun el plan funcional.

## 4. Matriz de brechas

| ReqId | Escenario | Regla evaluada | Estado | Evidencia actual | Brecha | Prioridad | Esfuerzo | Dependencias | Accion recomendada |
|---|---|---|---|---|---|---|---|---|---|
| MFA-CORE-001 | Principio 2.1 | No emitir acceso completo antes de completar MFA cuando es requerido | Cumplido | AuthService login retorna RequiresMfa + mfaToken | N/A | P0 | S | Ninguna | Mantener y cubrir con tests de no emision de access token |
| MFA-CORE-002 | Principio 2.2 | Proposito de flujo inmutable (login_enrollment/login_challenge/manage_mfa/account_recovery) | Parcial | MfaChallengePurposes: login, enrollment, reconfigure | Faltan manage_mfa y account_recovery como propositos formales de flujo | P1 | M | Modelo de flujo MFA | Introducir MFA flow session con Purpose estricto |
| MFA-CORE-003 | Principio 2.3 | Cambios sensibles requieren step-up | No implementado | RemoveMethod y Reconfigure usan solo access token | Falta step-up obligatorio para remove/reconfigure/add desde sesion autenticada | P0 | M | Nuevo management flow/token | Implementar management session + step-up |
| MFA-CORE-004 | Principio 2.4 | Continuation token rotatorio por paso | No implementado | Existe mfa token inicial, no rotacion por cada paso | Falta token por paso con invalidacion inmediata del previo | P1 | L | Modelo flow token + persistencia | Crear continuation token versionado y consumo atomico |
| MFA-CORE-005 | Principio 2.5 | Un challenge activo por flujo/metodo (ultimo gana) | Parcial | MfaChallenge permite pending/failed/verified | No hay semantica explicita superseded ni invalidacion fuerte del anterior | P1 | M | Cambios de estado e indices | Agregar superseded + constraint logica de ultimo activo |
| MFA-CORE-006 | Principio 2.6 | Respuestas anti enumeracion consistentes | Parcial | Mensajes genericos en varios endpoints | Aun hay mensajes especificos por causa en algunas rutas | P1 | M | Estandar de errores | Definir catalogo de errores neutros por dominio |
| MFA-A-001 | Escenario A | Login sin MFA requerido | Cumplido | AuthService emite access/refresh cuando no hay metodos MFA | N/A | P2 | S | Ninguna | Agregar pruebas de riesgo adaptativo para elevar a MFA cuando aplique |
| MFA-B-001 | Escenario B | Enrollment obligatorio durante login | Parcial | Hay enrollment start/verify con access token | Flujo actual no distingue enrollment obligatorio en etapa parcial de login | P0 | L | Continuation flow | Separar login_enrollment de enrollment gestionado con access token |
| MFA-B-002 | Escenario B | Finalizacion explicita de enrollment session | No implementado | No endpoint de complete de enrollment session | Falta cierre formal con validaciones minimas y revocacion de flujo parcial | P1 | M | Modelo de enrollment flow | Crear POST /api/mfa/enrollment-sessions/complete |
| MFA-B-003 | Escenario B | Permitir agregar mas de un metodo en el mismo flujo controlado | Parcial | Se puede enroll method via endpoint existente | No hay sesion de enrollment con requerimientos y cierre atomico | P1 | M | Enrollment flow | Diseñar loop crear/verificar con continuation rotatorio |
| MFA-C-001 | Escenario C | Login con challenge de metodo existente | Cumplido | challenges/start + challenges/verify con mfa scheme | N/A | P0 | S | Ninguna | Reforzar replay/concurrencia en DB y tests |
| MFA-C-002 | Escenario C | No devolver tokens finales en repeticion de verify | Parcial | Verify exige status pending | Falta estado consumed explicito y respuesta 409 consistente para replay | P0 | M | Estados challenge | Agregar consumed/superseded y control idempotente |
| MFA-D-001 | Escenario D | Iniciar administracion MFA requiere step-up | No implementado | No management-sessions endpoints | Cambios de metodos autorizados solo por access token | P0 | L | Management flow/token | Implementar POST management-sessions + verify step-up + complete |
| MFA-D-002 | Escenario D | Access token robado no debe bastar para takeover MFA | No implementado | Remove/reconfigure directos con access token | Riesgo de toma de control de factores | P0 | L | Step-up policy + risk | Gate estricto para operaciones sensibles |
| MFA-E-001 | Escenario E | Eliminar metodo requiere step-up reciente | No implementado | DELETE methods/{method} usa authorize normal | Falta requisito de step-up reciente | P0 | M | Management token | Exigir claim/session de step-up |
| MFA-E-002 | Escenario E | Bloquear eliminacion del ultimo factor requerido | Parcial | Validacion de alternativa o recovery codes | Falta politica por proposito y riesgo (ej. metodo usado en step-up) | P1 | M | Reglas de politica MFA | Extender regla con contexto de step-up |
| MFA-F-001 | Escenario F | Reemplazo de destino es alta+verificacion+switchover, no overwrite directo | Parcial | Reconfigure verifica OTP y actualiza contacto | Falta estado dual old/new y notificacion obligatoria al canal previo | P1 | M | Notificaciones + estado reemplazo | Implementar flujo replace con activacion diferida |
| MFA-G-001 | Escenario G | Marcar factor comprometido y revocar desafios/sesiones asociadas | No implementado | No endpoint/servicio explicito compromised | Falta respuesta operativa ante SIM swap o compromiso | P1 | M | Modelo de estado metodo | Agregar status compromised + acciones de revocacion |
| MFA-H-001 | Escenario H | Recuperacion de cuenta robusta cuando no hay acceso a MFA | Parcial | Recovery code como factor existe | Falta flujo completo account_recovery para casos sin recovery code | P1 | L | Proceso recovery | Diseñar recovery alternativo con controles antifraude |
| MFA-I-001 | Escenario I | Recovery codes: generar, mostrar una sola vez, hash, consumo one-time | Parcial | Hash + consume + emision una vez implementado | Falta endpoint formal de regenerate con step-up fuerte dedicado | P1 | M | Management flow | Agregar endpoints/status/regenerate bajo step-up |
| MFA-J-001 | Escenario J | Reenvio con cooldown visible y limites | Parcial | Existen rutas alias de resend | Falta contrato resendAvailableIn y enforcement estandar por destino/flujo | P1 | M | Rate limiter + DTO | Exponer cooldown y estado de reenvio |
| MFA-K-001 | Escenario K | Codigo incorrecto: mensaje neutro + throttling progresivo + bloqueo | Parcial | Se retorna invalid OTP y failed | Falta contador de intentos por challenge y bloqueo progresivo | P0 | M | Persistencia intentos | Agregar attempt_count, blocked_until y politicas |
| MFA-L-001 | Escenario L | Flujo vencido debe devolver 410 y cerrar permisos parciales | No implementado | Respuestas actuales mayormente 400 | Falta semantica HTTP 410 con code estable MFA_FLOW_EXPIRED | P1 | S | Estandar de errores | Introducir mapper de expiracion a 410 |
| MFA-M-001 | Escenario M | Solicitudes simultaneas y replay deben fallar con 409 | Parcial | Validacion de estado pending existe | Falta control atomico de avance de flujo/token y respuesta 409 uniforme | P0 | L | Transacciones/locks | Implementar compare-and-swap por flow step |
| MFA-N-001 | Escenario N | Cancelacion revoca flujo y tokens parciales | Parcial | auth/cancel-authentication revoca MFA temp token | Falta cancelacion explicita para enrollment/management sessions | P1 | M | Modelo sessions | Crear cancel endpoints por tipo de flujo |
| MFA-O-001 | Escenario O | Reevaluar usuario/estado en cada paso sensible | Parcial | Hay validaciones basicas en varios servicios | Falta reevaluacion completa de bloqueo, password changed, compromise flag | P1 | M | Servicio de policy/risk | Centralizar guardas pre-step |
| MFA-P-001 | Escenario P | Cambio de password revoca flujos MFA previos | No implementado | No evidencia de invalidacion por password version | Posible completion tardio con token previo | P0 | M | Version de credencial en token/session | Incluir passwordChangedAt/version check |
| MFA-Q-001 | Escenario Q | Riesgo elevado: exigir factor mas fuerte y controles adicionales | No implementado | No motor de riesgo adaptativo visible | Falta politica de riesgo por evento/contexto | P1 | L | Risk engine minimo | Implementar reglas iniciales por IP/dispositivo/geovelocidad |
| MFA-API-001 | HTTP/Errores | ProblemDetails estable para 400/401/403/404/409/410/429/503 | No implementado | Contrato actual usa envelope success/message | Falta capa de errores estandar segun plan funcional | P1 | M | Middleware de errores | Agregar mapping central a ProblemDetails |
| MFA-API-002 | HTTP/Errores | Retry-After en 429 | No implementado | No evidencia de header Retry-After | Falta estandar de rate-limit responses | P1 | S | Rate limiter | Agregar middleware/policy y header |
| MFA-SEC-001 | Twilio | Mapeo de fallas Twilio a 503 sin filtrar error interno | Parcial | Integracion TwilioOtpService existe | Falta contrato uniforme y fallback resiliente visible | P1 | M | Error mapper proveedor | Normalizar excepciones proveedor |
| MFA-SEC-002 | Twilio | Service rate limits + antifraude SMS pumping | No implementado | No evidencia de controles de costo/fraude por prefijo | Riesgo financiero y abuso | P1 | M | Integracion config + monitoreo | Agregar limites por destino/pais/prefijo |
| MFA-AUD-001 | Auditoria | Cobertura de eventos minimos obligatorios | Parcial | Eventos auth/mfa ya registrados en AuditService | Faltan eventos de management flow, flow expiry, replay blocked, risk-upgrade | P1 | M | Catalogo de eventos | Extender taxonomia audit |
| MFA-AUD-002 | Auditoria | Prohibicion total de secretos en logs | Parcial | No se guardan OTP en flujo principal | Requiere validacion automatizada en tests y filtros de logging | P0 | S | Pruebas de logging | Añadir test guard y scrubbers |
| MFA-TST-001 | Testing | Cobertura escenarios A..Q | No implementado | Hay tests de controllers puntuales | Falta suite estructurada por escenarios funcionales y seguridad | P1 | L | Plan de pruebas | Crear matriz de casos y suites por categoria |
| MFA-TST-002 | Testing | Casos de concurrencia/replay/idor | Parcial | Hay cobertura basica de controllers | Falta stress de doble verify/doble complete/token reuse | P0 | M | Infra tests integracion | Agregar tests transaccionales y paralelos |

## 5. Dependencias criticas entre brechas

1. Modelar MFA flow/session con proposito inmutable.
2. Implementar continuation token rotatorio + consumo atomico.
3. Implementar management session + step-up obligatorio.
4. Estandarizar estados de challenge/enrollment/flow (pending, verified, consumed, superseded, blocked, expired, revoked).
5. Estandarizar errores HTTP con ProblemDetails (incluyendo 409/410/429/503).

## 6. Backlog de implementacion recomendado por oleadas

### Oleada 1 (P0 seguridad de flujo)

- MFA-CORE-003
- MFA-D-001
- MFA-D-002
- MFA-E-001
- MFA-K-001
- MFA-M-001
- MFA-P-001
- MFA-AUD-002
- MFA-TST-002

### Oleada 2 (P1 integridad + contratos)

- MFA-CORE-002
- MFA-CORE-004
- MFA-CORE-005
- MFA-B-001
- MFA-B-002
- MFA-F-001
- MFA-J-001
- MFA-L-001
- MFA-API-001
- MFA-API-002
- MFA-SEC-001
- MFA-AUD-001
- MFA-TST-001

### Oleada 3 (Hardening y riesgo adaptativo)

- MFA-G-001
- MFA-H-001
- MFA-I-001
- MFA-Q-001
- MFA-SEC-002

## 7. Criterios de salida de la matriz

La matriz se considera lista para ejecucion cuando:

- Cada ReqId tiene owner y fecha objetivo.
- Cada fila P0/P1 tiene diseno tecnico asociado.
- Cada cambio tiene casos de prueba vinculados.
- Todas las dependencias criticas tienen secuencia aprobada por arquitectura y seguridad.

## 8. Proximo artefacto sugerido

Generar Docs/MFA_TWILIO_OWASP_IMPLEMENTATION_ROADMAP.md con:

- Sprint por sprint.
- Cambios por archivo/capa.
- Orden de migraciones.
- Estrategia de rollout con feature flags.
- Plan de pruebas de regresion por oleada.
