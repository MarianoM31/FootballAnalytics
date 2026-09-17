# Ingestion core

Phase 1A define el flujo neutral `IFootballDataProvider` → modelos normalizados → `FixtureSyncService` → repositorios → Dapper → SQL Server. Ningún contrato contiene modelos ni detalles de un proveedor deportivo concreto.

Cada proveedor expone un `ProviderCode` estable. `NormalizedCompetition`, `NormalizedSeason`, `NormalizedTeam` y `NormalizedFixture` conservan IDs externos y los datos necesarios para sincronizar las entidades internas. Los timestamps de fixtures se validan en UTC.

Los repositorios resuelven `Provider`, `EntityType` y `ExternalId` mediante `ProviderIdentifiers`. Al no existir un mapping, crean la entidad con un `Guid` interno y su mapping dentro de la misma transacción. Cuando existe, actualizan la entidad asociada; por ello repetir una sincronización no crea duplicados y un fixture programado puede actualizarse a finalizado sin cambiar su ID interno.

Cada sincronización crea una fila `Running` en `IngestionRuns`, que termina como `Succeeded` con contadores o como `Failed` con un mensaje acotado y sin stack trace. La migración `002_ingestion.sql` introduce exclusivamente esta auditoría.

Para agregar un proveedor futuro, implementa `IFootballDataProvider` en `FootballAnalytics.Ingestion`, transforma sus respuestas a los modelos normalizados y compón el servicio desde un punto de ejecución futuro. No uses IDs externos como claves internas, ni agregues llamadas HTTP o secretos al núcleo de Application.

La constraint `CK_Seasons_DateRange` de la migración `001` exige solamente `StartDate <= EndDate`, por lo que temporadas que cruzan años como `2026/27` son válidas. Como `001` ya está aplicada, no se modifica.
