# Roadmap

## Phase 0 - Foundation

Solución, límites de capas, modelo base, endpoint de salud, pruebas y documentación.

## Phase 1 - Fixtures

Persistencia de competiciones, temporadas, equipos y próximos partidos; sincronización desacoplada de proveedores.

### Phase 1A - Ingestion Core

Completada: contratos neutrales de proveedor, modelos normalizados, sincronización idempotente con Dapper, auditoría de ejecuciones y proveedor falso exclusivamente para pruebas. La integración con un proveedor deportivo real sigue pendiente.

### Phase 1B - football-data.org provider

En progreso: adaptador HTTP para Premier League (`PL`), configuración exclusiva mediante variable de entorno, CLI de sincronización manual y pruebas sin llamadas reales. La primera sincronización real requiere un token local configurado y autorización explícita.

## Phase 2 - Match Statistics

Resultados y estadísticas de partido, con procedencia y timestamps de ingesta.

## Phase 3 - Lineups

Alineaciones probables y confirmadas, XI, suplentes, formaciones y recálculos dependientes de su estado.

## Phase 4 - Analytics

Feature engineering, ratings, modelos de goles, probabilidades, backtesting y calibración temporal.

## Phase 5 - Insights

Explicaciones e insights automáticos basados en datos y predicciones versionadas.

## Phase 6 - Dashboard / Product

Dashboard web y experiencia de producto para explorar datos e insights.
