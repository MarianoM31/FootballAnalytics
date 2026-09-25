# Roadmap

Estado revisado contra el código, pruebas versionadas e historial Git el 2026-09-25 (`ca8bb039d3d9db5b2360e72d081aee277856ba3b`). «Completada» describe el alcance implementado en el repositorio; esta revisión documental no ejecutó pruebas, ingesta ni validaciones de proveedor o base de datos.

## Phase 0 - Foundation

Solución, límites de capas, modelo base, endpoint de salud, pruebas y documentación.

## Phase 1 - Fixtures

Persistencia de competiciones, temporadas, equipos y próximos partidos; sincronización desacoplada de proveedores.

### Phase 1A - Ingestion Core

Completada: contratos neutrales de proveedor, modelos normalizados, sincronización idempotente con Dapper, auditoría de ejecuciones y proveedor falso exclusivamente para pruebas. La integración con football-data.org se añadió en Phase 1B.

### Phase 1B - football-data.org provider

Completada en el repositorio (`45a547a`): [adaptador HTTP PL](../src/FootballAnalytics.Infrastructure/FootballData/FootballDataProvider.cs), token mediante configuración local, CLI manual y [pruebas con HTTP simulado](../tests/FootballAnalytics.UnitTests/FootballDataProviderTests.cs). La idempotencia pertenece al flujo de sincronización; las sondas registradas en discovery no demuestran por sí solas una ingesta idempotente ni acceso actual a otros endpoints.

## Phase 2 - Match Statistics

Resultados y estadísticas de partido, con procedencia y timestamps de ingesta.

### Phase 2A - Historical & Temporal Match Foundation

Completada en el repositorio (`303f3e1`): observaciones de programación y snapshots estadísticos, fingerprints, validación temporal y repositorios Dapper con inserción idempotente y consultas as-of. Evidencia: [migración 003](../database/migrations/003_historical_match_foundation.sql), [repositorio de snapshots](../src/FootballAnalytics.Infrastructure/Historical/DapperFixtureStatisticsSnapshotRepository.cs), [pruebas unitarias](../tests/FootballAnalytics.UnitTests/HistoricalFoundationTests.cs) y [pruebas SQL](../tests/FootballAnalytics.IntegrationTests/HistoricalFoundationSqlTests.cs). La base temporal está implementada; su existencia no acredita el estado de una base desplegada.

### Phase 2B - StatsBomb Historical Ingestion

Completada en el repositorio (`0bd7897`): [adaptador StatsBomb](../src/FootballAnalytics.Infrastructure/StatsBomb/StatsBombOpenDataProvider.cs), cálculo de estadísticas desde eventos, [servicio histórico](../src/FootballAnalytics.Application/Historical/HistoricalStatisticsIngestionService.cs), fingerprints y CLI por competición/temporada. Las [pruebas versionadas](../tests/FootballAnalytics.UnitTests/StatsBombPhase2BTests.cs) cubren mapeo, semántica de eventos, discrepancias de marcador, concurrencia y errores. No incorpora lineups; no implica cobertura completa ni disponibilidad histórica prepartido.

### Phase 2C - Temporal Team Features

Completada en el repositorio (`4d1e03f`): [TeamFeatureService](../src/FootballAnalytics.Application/Features/TeamFeatureService.cs) calcula ventanas Last5 y SeasonToDate, resultados y promedios por equipo, tamaños de muestra por métrica, procedencia y fingerprint. El [repositorio temporal](../src/FootballAnalytics.Infrastructure/Features/DapperTemporalFeatureDataRepository.cs) distingue LiveOperational (snapshots filtrados por disponibilidad e ingesta al cutoff) de HistoricalResearch (primer snapshot ingerido; partidos finalizados con margen de tres horas por defecto). HistoricalResearch no demuestra qué datos se conocían antes del kickoff histórico.

Evidencia adicional: [pruebas del servicio](../tests/FootballAnalytics.UnitTests/TeamFeatureServiceTests.cs), [pruebas SQL de features](../tests/FootballAnalytics.IntegrationTests/FeatureEngineSqlTests.cs) y [pruebas de límites temporales y correcciones](../tests/FootballAnalytics.IntegrationTests/TemporalFeatureRepositoryHardeningSqlTests.cs). La implementación es una base de estadísticas derivadas, no una certificación general de reconstrucción histórica de todos los campos del fixture.

## Phase 3 - Competition Coverage & Lineups

En planificación documental: ampliar cobertura sólo con evidencia por competición, temporada, endpoint y campo. El adaptador football-data.org sigue limitado a PL; el histórico StatsBomb no acredita disponibilidad prepartido ni cobertura completa de las temporadas parciales.

[Discovery](DISCOVERY_COMPETITION_COVERAGE.md) conserva las observaciones; [el plan Phase 3](PHASE3_EXPANSION_PLAN.md) separa comportamiento local, afirmaciones documentales y desconocidos. XI, suplentes, formaciones y estados probable/confirmado dependen de verificar semántica, acceso y procedencia temporal; no se promete soporte de alineaciones probables.

El eventual experimento de seis partidos tiene un techo duro de **108 intentos de solicitud**, incluidos setup, fallos y timeouts sin respuesta HTTP. La asignación inicial es **12 + (6 × 16) = 108**; se reduce muestreo si falta presupuesto. Requiere autorización separada antes de consultar proveedores. Esta fase se limita a documentación de cobertura y alineaciones.

## Phase 4 - Analytics

Pendiente: ampliar el análisis descriptivo sobre las métricas de Phase 2C, comparar rendimiento de equipos y temporadas, estudiar tendencias y calidad de datos con procedencia y cortes temporales explícitos.

## Phase 5 - Insights

Pendiente: explicaciones de resultados, tendencias y comparaciones deportivas basadas en estadísticas versionadas, con tamaños de muestra y limitaciones visibles.

## Phase 6 - Dashboard / Product

Dashboard web y experiencia de producto para explorar datos e insights.
