# Roadmap

## Phase 0 - Foundation

Solución, límites de capas, modelo base, endpoint de salud, pruebas y documentación.

## Phase 1 - Fixtures

Persistencia de competiciones, temporadas, equipos y próximos partidos; sincronización desacoplada de proveedores.

### Phase 1A - Ingestion Core

Completada: contratos neutrales de proveedor, modelos normalizados, sincronización idempotente con Dapper, auditoría de ejecuciones y proveedor falso exclusivamente para pruebas. La integración con un proveedor deportivo real sigue pendiente.

### Phase 1B - football-data.org provider

Completada: adaptador HTTP para Premier League (`PL`), configuración exclusiva mediante variable de entorno, CLI de sincronización manual, validación real idempotente y pruebas sin llamadas reales.

## Phase 2 - Match Statistics

Resultados y estadísticas de partido, con procedencia y timestamps de ingesta.

### Phase 2A - Historical & Temporal Match Foundation

En progreso: observaciones inmutables de programación, snapshots de estadísticas por equipo, temporalidad as-of e integridad de persistencia. No incluye todavía proveedores estadísticos, features ni modelos predictivos.

## Phase 3 - Lineups

Alineaciones probables y confirmadas, XI, suplentes, formaciones y recálculos dependientes de su estado.

## Phase 4 - Analytics

Feature engineering, ratings, modelos de goles, probabilidades, backtesting y calibración temporal.

## Phase 5 - Insights

Explicaciones e insights automáticos basados en datos y predicciones versionadas.

## Phase 6 - Dashboard / Product

Dashboard web y experiencia de producto para explorar datos e insights.
