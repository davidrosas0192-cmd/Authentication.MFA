# Plan de evaluacion de cambios para implementar MFA Twilio + OWASP

## 1. Objetivo

Definir un proceso sistematico para evaluar todos los cambios necesarios en el sistema actual y llevarlo al comportamiento funcional y de seguridad definido en el documento plan_funcional_mfa_twilio_owasp.md.

Este plan no implementa cambios; establece como medir brechas, priorizarlas y convertirlas en backlog ejecutable con criterios de cierre verificables.

## 2. Alcance de evaluacion

La evaluacion cubre:

- Flujos de autenticacion: login sin MFA, login con enrollment obligatorio, login con challenge.
- Flujos de administracion MFA: alta, baja, reemplazo y reconfiguracion de metodos.
- Recuperacion de cuenta y recovery codes.
- Modelo de sesion/token parcial: token MFA, continuation token, expiracion y rotacion.
- Seguridad operacional: rate limits, anti replay, anti enumeracion, antifraude SMS, auditoria y notificaciones.
- Contratos HTTP y ProblemDetails.
- Pruebas funcionales, negativas, concurrencia y riesgo.

Fuera de alcance inicial:

- Rediseno completo de UI.
- Cambios de negocio no relacionados con MFA.

## 3. Linea base del estado actual (para iniciar el gap analysis)

Estado observado en el repositorio:

- Existe flujo de login con MFA token temporal (mfa token) y seleccion de metodo.
- Existen endpoints OTP para start/verify de challenge y enrollment.
- Existen recovery codes con almacenamiento hash y consumo de un solo uso.
- Existen endpoints para remover/reconfigurar metodos.
- Existen eventos de auditoria en AuthenticationAuditEvents y SecurityAuditEvents.

Brechas probables que deben evaluarse formalmente:

- Falta de flujo explicito manage_mfa con step-up obligatorio para cambios sensibles.
- Falta de continuidad rotatoria por paso (consume token y emite uno nuevo por cada avance).
- Falta de separacion estricta por proposito de flujo (login_enrollment, login_challenge, manage_mfa, account_recovery).
- Falta de estados extendidos (superseded, consumed, blocked, revoked) para challenges/enrollments.
- Falta de enforcement completo para un challenge activo por flujo/metodo y semantica anti replay 409 uniforme.
- Falta de endurecimiento completo de anti enumeracion y mapeo de errores de proveedor a ProblemDetails estable.
- Falta de evaluacion de riesgo adaptativo para step-up y operaciones criticas.

## 4. Metodologia de evaluacion

### Fase 1: Inventario funcional y tecnico

Objetivo: mapear que ya existe y donde.

Actividades:

- Inventariar endpoints y auth scheme por endpoint.
- Inventariar entidades, estados, restricciones e indices de datos MFA.
- Inventariar eventos de auditoria y payload permitido/prohibido.
- Inventariar cobertura de pruebas actuales por flujo.

Artefacto:

- Matriz InventarioActual (endpoint, capa, archivo, comportamiento, evidencia).

### Fase 2: Descomposicion de requisitos del documento funcional

Objetivo: convertir el documento funcional en requisitos verificables.

Actividades:

- Crear IDs de requisito por escenario (A..Q) y por regla transversal.
- Separar requisitos por tipo:
  - Flujo
  - Seguridad
  - Datos/estado
  - API/errores
  - Auditoria/Notificaciones
  - Testing

Artefacto:

- CatalogoRequisitosMfa (ID, descripcion, criterio de aceptacion, evidencia esperada).

### Fase 3: Gap analysis trazable

Objetivo: comparar estado actual vs requisito y cuantificar esfuerzo/riesgo.

Actividades:

- Evaluar cada requisito en estado:
  - Cumplido
  - Parcial
  - No implementado
  - Ambiguo (requiere decision)
- Registrar impacto tecnico:
  - Contrato API
  - Compatibilidad backward
  - Migracion de datos
  - Riesgo de seguridad
- Registrar dependencias entre cambios.

Artefacto:

- MatrizBrechasMfa (ID requisito, estado, impacto, esfuerzo, dependencia, prioridad).

### Fase 4: Diseno de remediacion por oleadas

Objetivo: agrupar cambios en releases de bajo riesgo.

Actividades:

- Definir oleadas por criticidad:
  - Oleada 1: controles criticos de seguridad y flujo
  - Oleada 2: UX/API y observabilidad
  - Oleada 3: hardening avanzado y riesgo adaptativo
- Definir feature flags y estrategia de rollout.

Artefacto:

- RoadmapImplementacionMfa con hitos, dependencias y criterios go/no-go.

### Fase 5: Validacion de cierre

Objetivo: demostrar cumplimiento real del documento funcional.

Actividades:

- Ejecutar pruebas por escenarios A..Q + pruebas de abuso y concurrencia.
- Ejecutar checklist OWASP y Twilio Verify best practices aplicables.
- Validar que no se registran secretos.
- Validar resultados de auditoria y notificaciones.

Artefacto:

- InformeCierreMfa con evidencia por requisito y riesgos residuales.

## 5. Matriz de evaluacion recomendada

Columnas minimas:

- ReqId
- Escenario
- Regla
- EstadoActual
- Evidencia (archivo/endpoint/test)
- Brecha
- Severidad (Alta/Media/Baja)
- Esfuerzo (S/M/L)
- Riesgo de regresion
- Dependencias
- Owner
- Fecha objetivo

Priorizacion sugerida:

- P0: riesgo de bypass MFA, replay, secuestro de sesion, eliminacion del ultimo factor, exposure de secretos.
- P1: brechas de step-up, rotacion de continuation token, anti enumeracion fuerte, errores HTTP estables.
- P2: mejoras de UX, telemetria adicional, optimizaciones antifraude.

## 6. Paquetes de cambio a evaluar por dominio

### 6.1 Dominio de flujo y estado

Evaluar necesidad de:

- Nuevo modelo de MFA flow/session con proposito inmutable.
- Estados enriquecidos para flow/challenge/enrollment/method.
- Regla de un challenge activo por proposito/usuario/metodo.
- Politica de expiracion, revocacion, bloqueo y consumo.

### 6.2 Dominio de autorizacion y tokens

Evaluar necesidad de:

- Introducir continuation token rotatorio por paso.
- Separar permisos de Access Token, Mfa Token, Management Token.
- Step-up obligatorio para operaciones sensibles de administracion.
- Prevencion de replay y concurrencia con respuesta 409 consistente.

### 6.3 Dominio de API y contratos

Evaluar necesidad de:

- Endpoints dedicados para management sessions y enrollment sessions complete.
- Contratos ProblemDetails estables con codigos funcionales.
- Respuestas anti enumeracion consistentes.
- Retry-After en 429 y mapeo de fallas temporales proveedor a 503.

### 6.4 Dominio de datos y persistencia

Evaluar necesidad de:

- Entidades nuevas para flow tokens y sesion administrativa.
- Campos adicionales para challenge/enrollment (superseded, consumed, blocked).
- Indices para enforcement de ultimo challenge valido.
- Estrategia de migracion sin downtime y sin perdida de trazabilidad.

### 6.5 Dominio de seguridad y fraude

Evaluar necesidad de:

- Rate limiting por cuenta, IP, flujo, destino y dispositivo.
- Cooldown de reenvio con estado observable.
- Controles contra SMS pumping y restricciones de pais.
- Politica de riesgo adaptativo (step-up fuerte, bloqueo temporal, reauth password).

### 6.6 Dominio de auditoria y notificaciones

Evaluar necesidad de:

- Eventos minimos obligatorios del documento funcional.
- Garantia de no loggear OTP, tokens o secretos.
- Plantillas de notificacion para eventos sensibles.
- Correlation ID uniforme entre auth, MFA y proveedor.

### 6.7 Dominio de pruebas

Evaluar necesidad de:

- Suite de escenarios A..Q.
- Pruebas negativas e IDOR.
- Pruebas de concurrencia y replay.
- Pruebas de riesgo (pais, dispositivo, cambio de password durante flujo).

## 7. Backlog inicial de evaluacion (2 semanas)

Semana 1

1. Completar inventario tecnico en controladores, servicios, repositorios y migraciones.
2. Crear catalogo de requisitos con IDs trazables.
3. Levantar matriz de brechas preliminar y clasificar P0/P1/P2.

Semana 2

1. Refinar esfuerzo y dependencias por brecha.
2. Disenar roadmap por oleadas con criterios de salida.
3. Definir plan de pruebas y evidencia minima por requisito.
4. Revisar con seguridad y arquitectura para aprobacion de implementacion.

## 8. Definition of Ready para empezar implementacion

Un cambio entra a implementacion solo si:

- Tiene ReqId y criterio de aceptacion.
- Tiene contrato API y reglas de error definidos.
- Tiene impacto de datos/migracion documentado.
- Tiene pruebas asociadas (positivas y negativas).
- Tiene eventos de auditoria y notificacion definidos.
- Tiene estrategia de rollback o feature flag.

## 9. Definition of Done de la evaluacion

La evaluacion se considera completa cuando:

- 100% de requisitos del documento funcional tienen estado y evidencia.
- Todas las brechas P0 y P1 tienen plan de implementacion aprobado.
- Existe roadmap por oleadas con dependencias y riesgos.
- Existe suite de pruebas planificada para escenarios A..Q.
- Existe criterio de cierre de seguridad alineado con OWASP y Twilio Verify.

## 10. Entregables finales esperados

- Matriz InventarioActual.
- CatalogoRequisitosMfa.
- MatrizBrechasMfa priorizada.
- RoadmapImplementacionMfa por oleadas.
- PlanPruebasMfa (funcional, seguridad, concurrencia, riesgo).
- InformeCierreMfa con riesgos residuales.
