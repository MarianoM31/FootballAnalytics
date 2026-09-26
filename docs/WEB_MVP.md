# Explorador gráfico de estadísticas

Interfaz en español para competiciones/temporadas almacenadas, partidos paginados, resultados y estadísticas por equipo de un partido. Es una vista de lectura del archivo existente; no consulta proveedores ni ejecuta ingesta o migraciones.

## Ejecutar en desarrollo

Requiere .NET 10 y una base FootballAnalytics existente con el esquema ya aplicado. Desde la raíz del repositorio:

```powershell
dotnet build FootballAnalytics.sln
```

En una terminal, configurar la conexión a la base existente y arrancar la API. El ejemplo corresponde a la instancia indicada en la configuración local del migrador; adaptarlo si la instancia es otra. No ejecutar el migrador para abrir esta interfaz.

```powershell
$env:ConnectionStrings__FootballAnalyticsDb = 'Server=MARIANOM31\SQLEXPRESS04;Database=FootballAnalytics;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=5'
dotnet run --project src/FootballAnalytics.Api --no-build --no-launch-profile --urls http://localhost:5181
```

En otra terminal:

```powershell
dotnet run --project src/FootballAnalytics.Web --no-build --no-launch-profile --urls http://localhost:5182
```

Abrir [el explorador local](http://localhost:5182). Si cambia el puerto de la API, definir `Api__BaseUrl` (con barra final) en la terminal de Web. Web no necesita conexión SQL ni token de proveedor. Ambos servidores se detienen con Ctrl+C. La interfaz local no incorpora autenticación; no se configura para exposición pública.

## Recorrido y límites

- Elegir competición y temporada; los recuentos representan el archivo local, que puede ser parcial.
- El catálogo conserva el nombre original y expone `isSelectable` e `identityWarning` desde Application. Nombres vacíos y el placeholder confirmado `x` (ignorando espacios y mayúsculas) se excluyen del selector de Web. La API conserva esos registros y su estado de identidad para diagnóstico; la vista muestra el número de competiciones pendientes de verificación. No se renombran ni borran filas. La regla es una comprobación acotada de calidad, no una lista de competiciones autorizadas ni una verificación de identidad del proveedor.
- Recorrer páginas de 24 partidos, ordenados por fecha descendente e ID como desempate. Las fechas se muestran en UTC.
- Los recuentos, listados y detalles sólo incluyen fixtures cuya competición coincide con la de su temporada. Los registros inconsistentes se conservan sin cambios; su detalle devuelve 404. Si falla una petición de página, se conserva la página visible y se ofrece «Reintentar página». Al cambiar de temporada se limpia la lista anterior para no atribuirle partidos ajenos.
- Abrir un partido para comparar métricas de ambos equipos. `—` significa desconocido; cero se conserva como cero. Los estados vacíos y fallos del servicio se muestran sin datos de demostración.
- Cada fuente conserva su propia última revisión por instante de ingesta e ID como desempate. Nunca se mezclan proveedores ni revisiones para rellenar campos ausentes. Se muestran disponibilidad registrada e ingesta; no se infiere publicación prepartido.
- Calendario y marcador reflejan la proyección actual del fixture. Esta vista no reconstruye un corte histórico, no calcula features prepartido ni ofrece alineaciones.
- La cobertura y los listados se leen por separado; si cambia la base durante la navegación, actualizar la vista. Este MVP no añade procesos de escritura.

## Límites de arquitectura y rutas

`Web (HTML/CSS/JS y proxy GET) → API → ExplorerService (Application) → IExplorerRepository → DapperExplorerRepository (Infrastructure) → SQL Server`.

Web sólo reenvía las tres rutas de lectura a la API configurada. No tiene referencias a Infrastructure, SQL Server ni Application. Los contratos y validación de paginación viven en Application; las consultas parametrizadas de sólo lectura viven en Infrastructure.

| API | Contenido |
|---|---|
| `GET /api/explorer/competitions` | Filas de competición/temporada y recuentos locales |
| `GET /api/explorer/matches?seasonId={guid}&page=1` | Hasta 24 partidos y `hasMore`; páginas 1–10000 |
| `GET /api/explorer/matches/{guid}` | Partido y última observación estadística por proveedor, o 404 |

Las rutas equivalentes de Web empiezan en `/data`. Errores de base de datos devuelven 503 sin detalles de conexión; parámetros inválidos devuelven 400. No se incorpora acceso a proveedores externos.

## Validación y prueba SQL aislada

```powershell
dotnet test tests/FootballAnalytics.UnitTests --no-restore
dotnet test tests/FootballAnalytics.IntegrationTests --no-restore --filter 'FullyQualifiedName~ExplorerEndpointTests|FullyQualifiedName~HealthEndpointTests'
```

Los tests de endpoints sustituyen el repositorio por uno en memoria: prueban paginación, validación, procedencia, nulos, ceros, 404 y errores 503. No ejecutar toda la suite SQL como comprobación de este MVP: contiene pruebas que escriben datos y aplican migraciones.

La regresión SQL tiene ejecución explícita y aislada. Requiere autenticación integrada y permiso para crear una base temporal en la instancia indicada; no carga la conexión de la aplicación. Crea una base `FootballAnalytics_ExplorerTest_<guid>`, prepara tablas mínimas de prueba sin ejecutar migraciones y elimina esa base al terminar. Comprueba pertenencia competición/temporada (incluido un fixture inconsistente), paginación, detalle y mapeo de snapshots. Sin la variable indicada, esta prueba se omite. Si se interrumpe el proceso, puede quedar la base temporal pendiente de limpieza.

```powershell
$env:EXPLORER_TEST_SQL_SERVER = 'MARIANOM31\SQLEXPRESS04'
dotnet test tests/FootballAnalytics.IntegrationTests --no-restore --filter 'FullyQualifiedName~ExplorerRepositorySqlTests'
Remove-Item Env:EXPLORER_TEST_SQL_SERVER
```

Las pruebas enfocadas del selector y de recuperación de paginación usan Node.js (sin dependencias adicionales). Las de paginación ejecutan el cargador real con respuestas HTTP controladas y un DOM mínimo de prueba:

```powershell
node --test tests/FootballAnalytics.WebTests/*.test.mjs
```
