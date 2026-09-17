# Architecture

FootballAnalytics sigue una arquitectura por capas con dependencias dirigidas hacia el dominio.

`Api` compone dependencias y expone endpoints delgados. `Application` contiene casos de uso, DTOs e interfaces, incluido el núcleo neutral de ingestión. `Domain` contiene las entidades puras (`Competition`, `Season`, `Team`, `Player` y `Fixture`) y no depende de infraestructura. `Infrastructure` implementa los contratos de aplicación para SQL Server con Dapper. `Ingestion` queda reservado para adaptadores de proveedores concretos. `Analytics` concentrará features, ratings, predicciones, backtesting y calibración. `Web` queda reservado para el dashboard.

Los identificadores de proveedores están modelados por `ProviderIdentifier`: el `Guid` interno del dominio es estable y cada combinación de proveedor, tipo de entidad e identificador externo puede asociarse a él. Ningún identificador externo será clave primaria del dominio.

La API no accede directamente a SQL. La configuración de conexión se declara sin credenciales en `appsettings.json`; secretos y valores por entorno no se versionan.

`FixtureSyncService` pertenece a Application: recibe `IFootballDataProvider`, procesa datos normalizados en el orden competición, temporada, equipos y fixture, y delega persistencia y auditoría en interfaces. Infrastructure implementa esos contratos mediante Dapper y una factoría de conexiones SQL Server. No existen endpoints públicos de sincronización todavía.
