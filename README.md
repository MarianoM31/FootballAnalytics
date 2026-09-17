# FootballAnalytics

Plataforma escalable para recopilar datos de fútbol y, progresivamente, producir analítica estadística y predicciones explicables.

## Arquitectura

La solución usa una separación por responsabilidades: `Domain` conserva el modelo puro; `Application` define casos de uso y contratos; `Infrastructure` contendrá SQL Server, Dapper e integraciones; `Ingestion` normalizará proveedores; `Analytics` alojará cálculos y modelos; `Api` expone HTTP; y `Web` reserva el futuro dashboard.

## Proyectos

- `src/FootballAnalytics.Domain`: entidades y enumeraciones sin dependencias externas.
- `src/FootballAnalytics.Application`: contratos y orquestación de casos de uso.
- `src/FootballAnalytics.Infrastructure`: persistencia SQL Server con Dapper.
- `src/FootballAnalytics.Ingestion`: sincronización y normalización futuras.
- `src/FootballAnalytics.Analytics`: analítica y modelado futuros.
- `src/FootballAnalytics.Api`: API ASP.NET Core.
- `src/FootballAnalytics.Web`: estructura reservada para el dashboard.
- `tests`: pruebas unitarias y de integración.

## Requisitos

- .NET SDK 10.
- SQL Server será la base de datos objetivo; no se necesita para ejecutar la fundación actual.

## Comandos

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src/FootballAnalytics.Api
```

La API expone `GET /health`, que responde `{"status":"healthy"}`. En desarrollo también publica el documento OpenAPI.

## Estado actual

Phase 0 está implementada: estructura de solución, modelo de dominio inicial, configuración sin secretos, endpoint de salud, pruebas y principios de datos. No hay proveedores deportivos, persistencia implementada, modelos predictivos ni dashboard todavía.

Consulta [la arquitectura](docs/ARCHITECTURE.md), [la hoja de ruta](docs/ROADMAP.md) y [los principios de datos](docs/DATA_PRINCIPLES.md).
