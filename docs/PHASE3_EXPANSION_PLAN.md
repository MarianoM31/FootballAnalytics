# Phase 3 — Competition coverage and lineup data

## Alcance y base

Plan documental para estadísticas deportivas generales, basado en la revisión local del 2026-09-25 sobre `develop`, HEAD `ca8bb039d3d9db5b2360e72d081aee277856ba3b`, y en [discovery](DISCOVERY_COMPETITION_COVERAGE.md). No adopta un plan Cloud provisional como evidencia. No implementa código, pruebas, SQL, migraciones, datasets, ingesta ni funciones de apuestas, cuotas o wagering. Ninguna consulta futura queda autorizada por este documento.

## Evidencia y límites

**Verificado en el repositorio:** [FootballDataProvider](../src/FootballAnalytics.Infrastructure/FootballData/FootballDataProvider.cs) fija PL, expone la temporada actual y mapea calendario/resultados; sus DTOs omiten estadísticas, árbitros y lineups. El CLI admite únicamente PL para ese flujo. [StatsBombOpenDataProvider](../src/FootballAnalytics.Infrastructure/StatsBomb/StatsBombOpenDataProvider.cs) admite IDs de competición/temporada y descarga metadatos, partidos y eventos con concurrencia máxima 4; no descarga lineups. El calculador deriva estadísticas por equipo, excluye tandas, reconcilia goles y deja posesión nula. El servicio histórico usa la ingesta como disponibilidad conservadora. Las pruebas inspeccionadas usan HTTP simulado; no acreditan acceso actual a proveedores.

**Observaciones de proveedor registradas el 2026-09-25, no revalidadas en esta revisión:** cinco respuestas football-data.org iniciales y una posterior de CL; inventario StatsBomb de discovery. Mantener separadas estas dimensiones:

| Fuente / objetivo | Catálogo o temporada observada | Acceso / contenido observado | Soporte local y límite |
|---|---|---|---|
| football-data.org PL | 2502 actual; 128 temporadas listadas | Competición, lista de partidos y detalle 560542; resultado 3–0, un referee; sin statistics, lineup ni bench en ese detalle | Sólo PL; listar temporadas no verifica partidos de todas ellas |
| football-data.org CL | 2557 actual; 47 temporadas listadas | Sólo recurso de competición | Sin soporte local; partidos, estadísticas y lineups desconocidos |
| football-data.org EC, FL1, BL1, SA, PD, CLI | Entradas del catálogo autenticado | Sin pruebas individuales de sus endpoints | Sin soporte local; catálogo no acredita acceso a partidos |
| football-data.org otros objetivos | Sin coincidencias objetivo para EL, Conference, Nations League, Sudamericana, Copa América o eliminatorias CONMEBOL | Sin prueba de acceso | Desconocidos; no declarar prohibición global |
| StatsBomb PL y Serie A 2015/16 | 380 / 380 / 380 por temporada | Partidos / eventos / archivos lineups registrados | Histórico; adaptador sólo consume partidos/eventos |
| StatsBomb La Liga 2020/21, Bundesliga 2023/24, Ligue 1 2022/23 | 35 / 35 / 35; 34 / 34 / 34; 32 / 32 / 32 | Muestras parciales | No representan temporadas completas |
| StatsBomb CL 2018/19 y Europa League 1988/89 | 1 / 1 / 1; 3 / 3 / 3 | Muestras mínimas | No representan torneos completos |
| StatsBomb Euro 2024 y Copa América 2024 | 51 / 51 / 51; 32 / 32 / 32 | Archivos registrados para esos torneos | Sin evidencia de disponibilidad prepartido |
| StatsBomb otros objetivos | Sin entrada observada para Conference, Libertadores, Sudamericana, Nations League o clasificatorias UEFA/CONMEBOL | Alcance limitado al catálogo inspeccionado | No extrapolar ausencia actual o global |

La tabla detallada de discovery conserva otras temporadas del catálogo y sus IDs; catálogo no equivale a recuento verificado. La existencia de archivos no acredita calidad, XI confirmado ni cobertura de cada campo. Las fechas mínima/máxima de los partidos que devuelve el adaptador no definen por sí solas una temporada completa.

**Afirmaciones documentales previamente registradas:** football-data.org describe `lineup` y `bench` dentro del detalle `matches/{matchId}`, planes con Line-ups & Subs, un add-on estadístico y cuota Free de 10/minuto. No se identificó un endpoint independiente de lineups. StatsBomb describe archivos por match ID y requisitos de atribución. Estas referencias no prueban entitlements del token, cuota actual, derechos vigentes ni latencia.

**Pendiente de verificación del proveedor:** acceso por competición/temporada/endpoint, campos efectivamente autorizados, diferencias entre lista y detalle, nulos, semántica probable/confirmado, formaciones, identificadores de jugadores, publicación y correcciones, cuotas y términos de almacenamiento/redistribución. También la equivalencia entre el `hudl/open-data` configurado en el CLI y el `statsbomb/open-data` enlazado en discovery. No se resolverán mediante llamadas durante esta fase documental.

## Contrato de alineación propuesto, no implementado

- Procedencia: proveedor, endpoint o ruta, competición/temporada, partido, equipo y jugador con IDs externos separados de GUID internos; versión de normalización, fingerprint y referencia de observación. Registrar revisión del dataset cuando esté disponible; no unir jugadores sólo por nombre.
- Contenido: titulares, suplentes, formación y posiciones sólo si el proveedor los entrega con semántica verificada. Distinguir propiedad ausente, nula, colección vacía y contenido parcial. No inferir XI confirmado de un archivo histórico ni alineación probable de una plantilla.
- Estado: `unknown/absent/probable/confirmed`, conservando el estado original y la regla de clasificación. `changed` es un indicador de revisión separado, no un sustituto del estado. Una respuesta fallida no demuestra ausencia ni revoca una confirmación.
- Tiempos UTC: `RequestedAtUtc`, `ReceivedAtUtc`, kickoff observado y sus revisiones, `FirstObservedAtUtc` por versión y `ProviderPublishedAtUtc` nullable sólo con significado verificado. `lastUpdated`, fecha de partido o modificación de archivo no equivalen automáticamente a publicación de la alineación.
- Disponibilidad: mantener `AvailableAtUtc` e `IngestedAtUtc` y filtrar ambos por el cutoff. Sin publicación fiable, usar ingesta conservadora; primera observación es evidencia local, no hora exacta de publicación. Un archivo histórico observado hoy no habilita features antes del kickoff histórico.
- Correcciones: conservar versiones inmutables y distinguir cambios de contenido de repetición idéntica; registrar los intentos incluso sin cambios, con código HTTP o error de transporte y cabeceras de cuota disponibles. Definir retención autorizada antes de persistir; no guardar secretos ni payloads completos por defecto.

## Experimento de seis partidos

**Puerta de entrada:** autorización separada, derechos/retención, endpoint y representación confirmados, cuota vigente y semántica de confirmación acordadas. Seleccionar seis partidos de una o dos competiciones con acceso demostrado, IDs externos y kickoff UTC. Si el setup no demuestra acceso a lineups útiles, detenerse y documentar el resultado; no gastar el resto sondeando campos inaccesibles.

**Techo duro: 108 intentos de solicitud para el experimento**, incluidos selección/setup, revalidaciones, errores HTTP, repeticiones por errores locales, reintentos, paginación y observaciones postpartido. Cada intento consume una unidad, incluso si termina en timeout sin respuesta HTTP. Sin reintentos automáticos; no lanzar una solicitud sin presupuesto. Las seis respuestas de la discovery anterior son evidencia histórica, no nuevas solicitudes del experimento; cualquier repetición sí cuenta.

Asignación inicial: **12 + (6 × 16) = 108 intentos de solicitud**: 12 para setup/contingencias y 96 para seguimiento. No se supone que 12 garanticen completar el setup. Una consulta agrupada sólo ahorra presupuesto si se verifica que conserva los mismos campos y semántica; no se presupone esa equivalencia.

| Intentos programados por partido | Instantes relativos al kickoff | Número |
|---|---|---:|
| Tempranas | T−72 h, T−48 h, T−24 h | 3 |
| Intermedias | T−20 h, T−16 h, T−12 h, T−8 h, T−4 h | 5 |
| Últimas horas | T−3 h, T−2 h, T−1 h | 3 |
| Última hora | T−45 min, T−30 min, T−15 min, T | 4 |
| Posterior | Una en T+3 h; si no terminó, registrar esa limitación sin añadir una consulta | 1 |
| Total máximo | Fronteras contadas una sola vez | 16 |

Se eliminan T−60 h y T−36 h de la cadencia original: los intervalos tempranos pasan de 12 a 24 horas. La primera detección acota cuándo el contenido pasó a ser observable en el endpoint entre muestras válidas, no necesariamente cuándo lo publicó el proveedor; con errores o sin una observación previa de ausencia, el intervalo puede ser mayor o indeterminado. Seis partidos no permiten prometer un SLA ni generalizar a otras competiciones.

Si setup/contingencias supera 12, reducir el seguimiento: con coste `S`, quedan como máximo `108 − S` intentos. Ejemplo: setup de 18 permite 90 intentos de seguimiento, 15 por partido, retirando también T−48 h; la resolución temprana se degrada a 48 horas. Registrar el calendario reducido y su limitación antes de continuar. Reservar las seis observaciones posteriores antes de distribuir las restantes; si no alcanza para un diseño útil de seis partidos, detenerse y reportar inviabilidad bajo el techo.

Detener polling prepartido al confirmar y conservar la única observación posterior presupuestada. Esto impide medir cambios posteriores a la confirmación antes del kickoff; comparar ambos puntos sólo detecta diferencias entre ellos. No rellenar retrospectivamente huecos ni añadir consultas por aplazamientos sin descontarlas del presupuesto. Detener ante 401, 403 o 429 y registrar `Retry-After` si existe; el techo total no sustituye límites por minuto ni considera otras aplicaciones que compartan cuota.

Revisar asignación si el setup, errores, cambios de kickoff o el objetivo de medir correcciones hacen insuficiente el calendario. Priorizar menos muestras y declarar pérdida de resolución. Aumentar el techo exigiría una propuesta distinta con evidencia y autorización expresa; **este plan mantiene 108 intentos de solicitud**.

## Entregables y criterio de cierre

1. Matriz fechada por competición/temporada/endpoint/campo, con evidencia local, observación registrada, claim documental y desconocido separados; preservar muestras parciales.
2. Diccionario de procedencia, estados, nulos y tiempos; lista de decisiones pendientes de proveedor, sin migraciones.
3. Protocolo presupuestado y, sólo tras ejecución futura autorizada, informe de consumo total, observaciones válidas/fallidas, primera detección, correcciones observadas y límites de inferencia.
4. Decisión documentada de avanzar o detener cada capacidad. No prometer ampliación de adaptadores, datos probables o disponibilidad prepartido hasta satisfacer acceso, semántica, cobertura y derechos. Mantener adaptadores de calendario/resultados separados del detalle estadístico y de alineaciones.
