# Ingestion core

Phase 1A define el flujo neutral `IFootballDataProvider` → modelos normalizados → `FixtureSyncService` → repositorios → Dapper → SQL Server. Ningún contrato contiene modelos ni detalles de un proveedor deportivo concreto.

Cada proveedor expone un `ProviderCode` estable. `NormalizedCompetition`, `NormalizedSeason`, `NormalizedTeam` y `NormalizedFixture` conservan IDs externos y los datos necesarios para sincronizar las entidades internas. Los timestamps de fixtures se validan en UTC.

Los repositorios resuelven `Provider`, `EntityType` y `ExternalId` mediante `ProviderIdentifiers`. Al no existir un mapping, crean la entidad con un `Guid` interno y su mapping dentro de la misma transacción. Cuando existe, actualizan la entidad asociada; por ello repetir una sincronización no crea duplicados y un fixture programado puede actualizarse a finalizado sin cambiar su ID interno.

Cada sincronización crea una fila `Running` en `IngestionRuns`, que termina como `Succeeded` con contadores o como `Failed` con un mensaje acotado y sin stack trace. La migración `002_ingestion.sql` introduce exclusivamente esta auditoría.

Para agregar un proveedor de calendario/resultados, implementa `IFootballDataProvider` en `FootballAnalytics.Infrastructure`, transforma sus respuestas a los modelos normalizados y compón el servicio desde un punto de ejecución futuro. No uses IDs externos como claves internas, ni agregues llamadas HTTP o secretos al núcleo de Application.

La constraint `CK_Seasons_DateRange` de la migración `001` exige solamente `StartDate <= EndDate`, por lo que temporadas que cruzan años como `2026/27` son válidas. Como `001` ya está aplicada, no se modifica.

## football-data.org

`FootballDataProvider` vive en Infrastructure y adapta las respuestas v4 de football-data.org para Premier League (`PL`). Usa los recursos de competición, equipos y partidos; sus DTOs externos no salen del adaptador. El flujo es `FootballDataProvider` → `Normalized*` → `FixtureSyncService` → Dapper → SQL Server.

El token solo se lee desde la variable local `FootballData__ApiToken`; nunca debe incluirse en código, configuración versionada, tests ni logs. La URL base puede sobrescribirse con `FootballData__BaseUrl` y por defecto es `https://api.football-data.org/v4/`.

Para una sincronización manual controlada, tras configurar el token en la sesión o perfil de Windows, ejecuta:

```powershell
dotnet run --project src/FootballAnalytics.Ingestion -- sync-fixtures PL
```

El CLI limita la prueba inicial a los próximos 14 días y no expone un endpoint HTTP. El adaptador envía `X-Auth-Token`, no registra ese header, reporta claramente errores de autorización, rate limit, timeout, servidor y JSON inválido, y no implementa reintentos agresivos. Ante HTTP 429 informa `Retry-After` cuando está presente.

## Observaciones históricas

La migración `003_historical_match_foundation.sql` incorpora observaciones inmutables de programación y snapshots de estadísticas por equipo. `AvailableAtUtc` representa cuándo el proveedor hizo disponible una observación e `IngestedAtUtc` cuándo FootballAnalytics la obtuvo; si el proveedor no conoce disponibilidad, se debe usar el instante de ingesta de forma conservadora. Un cálculo con corte `T` solo puede usar observaciones para las que ambos timestamps sean menores o iguales a `T`.

`ObservedAtUtc` es opcional y representa cuándo ocurrió el hecho subyacente cuando esa noción existe. No se almacenan payloads completos: se preservan datos normalizados, proveedor, referencia a la ejecución de ingesta y fingerprint. Un reintento de la misma observación no se duplica; una corrección del proveedor, al tener fingerprint diferente, crea un nuevo snapshot.

## StatsBomb Open Data

StatsBomb Open Data puede usarse como fuente histórica mediante `sync-statsbomb-history <competitionId> <seasonId>`. Los análisis o insights publicados derivados de estos datos deben acreditar a StatsBomb y emplear su logo conforme al [Media Pack oficial](https://statsbomb.com/media-pack/). La procedencia se preserva con `Provider = statsbomb-open`.

## Límites actuales y planificación Phase 3

El adaptador football-data.org fija PL, expone sólo la temporada actual y mapea marcadores finales nullable; no deserializa árbitros, estadísticas ni alineaciones. Acceso observado al catálogo o al recurso de competición CL no amplía el soporte local ni acredita acceso a partidos CL.

`StatsBombOpenDataProvider` implementa `IHistoricalMatchStatisticsProvider`: consulta competición/temporada, partidos y eventos, con hasta cuatro descargas de eventos concurrentes. No consulta `lineups` ni mapea árbitros; posesión queda nula. El calculador excluye tandas y valida goles de eventos contra el marcador cuando ambos marcadores están presentes. Los límites de temporada proceden de las fechas mínima/máxima de partidos devueltos y no prueban cobertura completa.

El CLI usa `https://raw.githubusercontent.com/hudl/open-data/master/data/`; discovery cita `statsbomb/open-data`. Su equivalencia o redirección queda pendiente de verificación. Los recuentos y muestras parciales de discovery son evidencia fechada, no garantías actuales.

El servicio histórico no recibe fecha de publicación: resuelve `AvailableAtUtc` al instante de ingesta. Los archivos históricos no demuestran disponibilidad antes del kickoff. Las propuestas de identidad de jugadores, estados y timestamps adicionales están en [PHASE3_EXPANSION_PLAN.md](PHASE3_EXPANSION_PLAN.md); no son contratos ni persistencia implementados. El plan mantiene separados los adaptadores de calendario/resultados y detalle y exige verificación y autorización antes del experimento limitado a **108 intentos de solicitud**, incluidos setup, fallos y timeouts sin respuesta HTTP, con asignación inicial **12 + (6 × 16) = 108**.
