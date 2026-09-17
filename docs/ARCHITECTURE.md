# Architecture

FootballAnalytics sigue una arquitectura por capas con dependencias dirigidas hacia el dominio.

`Api` compone dependencias y expone endpoints delgados. `Application` contendrá casos de uso, DTOs e interfaces. `Domain` contiene las entidades puras (`Competition`, `Season`, `Team`, `Player` y `Fixture`) y no depende de infraestructura. `Infrastructure` implementará los contratos de aplicación para SQL Server con Dapper. `Ingestion` consumirá proveedores mediante abstracciones. `Analytics` concentrará features, ratings, predicciones, backtesting y calibración. `Web` queda reservado para el dashboard.

Los identificadores de proveedores están modelados por `ProviderIdentifier`: el `Guid` interno del dominio es estable y cada combinación de proveedor, tipo de entidad e identificador externo puede asociarse a él. Ningún identificador externo será clave primaria del dominio.

La API no accede directamente a SQL. La configuración de conexión se declara sin credenciales en `appsettings.json`; secretos y valores por entorno no se versionan.
