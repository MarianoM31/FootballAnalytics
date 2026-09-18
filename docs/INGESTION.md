# Ingestion core

Phase 1A define el flujo neutral `IFootballDataProvider` → modelos normalizados → `FixtureSyncService` → repositorios → Dapper → SQL Server. Ningún contrato contiene modelos ni detalles de un proveedor deportivo concreto.

Cada proveedor expone un `ProviderCode` estable. `NormalizedCompetition`, `NormalizedSeason`, `NormalizedTeam` y `NormalizedFixture` conservan IDs externos y los datos necesarios para sincronizar las entidades internas. Los timestamps de fixtures se validan en UTC.

Los repositorios resuelven `Provider`, `EntityType` y `ExternalId` mediante `ProviderIdentifiers`. Al no existir un mapping, crean la entidad con un `Guid` interno y su mapping dentro de la misma transacción. Cuando existe, actualizan la entidad asociada; por ello repetir una sincronización no crea duplicados y un fixture programado puede actualizarse a finalizado sin cambiar su ID interno.

Cada sincronización crea una fila `Running` en `IngestionRuns`, que termina como `Succeeded` con contadores o como `Failed` con un mensaje acotado y sin stack trace. La migración `002_ingestion.sql` introduce exclusivamente esta auditoría.

Para agregar un proveedor futuro, implementa `IFootballDataProvider` en `FootballAnalytics.Ingestion`, transforma sus respuestas a los modelos normalizados y compón el servicio desde un punto de ejecución futuro. No uses IDs externos como claves internas, ni agregues llamadas HTTP o secretos al núcleo de Application.

La constraint `CK_Seasons_DateRange` de la migración `001` exige solamente `StartDate <= EndDate`, por lo que temporadas que cruzan años como `2026/27` son válidas. Como `001` ya está aplicada, no se modifica.

## football-data.org

`FootballDataProvider` vive en Infrastructure y adapta las respuestas v4 de football-data.org para Premier League (`PL`). Usa los recursos de competición, equipos y partidos; sus DTOs externos no salen del adaptador. El flujo es `FootballDataProvider` → `Normalized*` → `FixtureSyncService` → Dapper → SQL Server.

El token solo se lee desde la variable local `FootballData__ApiToken`; nunca debe incluirse en código, configuración versionada, tests ni logs. La URL base puede sobrescribirse con `FootballData__BaseUrl` y por defecto es `https://api.football-data.org/v4/`.

Para una sincronización manual controlada, tras configurar el token en la sesión o perfil de Windows, ejecuta:

```powershell
dotnet run --project src/FootballAnalytics.Ingestion -- sync-fixtures PL
```

El CLI limita la prueba inicial a los próximos 14 días y no expone un endpoint HTTP. El adaptador envía `X-Auth-Token`, no registra ese header, reporta claramente errores de autorización, rate limit, timeout, servidor y JSON inválido, y no implementa reintentos agresivos. Ante HTTP 429 informa `Retry-After` cuando está presente.
