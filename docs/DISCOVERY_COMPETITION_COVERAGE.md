# Phase 3 discovery — competition and data coverage

**Fecha de verificación:** 2026-09-25. Este documento es una evaluación de factibilidad, no una promesa de cobertura ni autorización para ingerir. No se hicieron solicitudes autenticadas, no se leyeron secretos y no se modificó SQL.

## Estado local observado

El adaptador \`FootballDataProvider\` implementa \`IFootballDataProvider\`, pero fija la competición \`PL\` y el CLI sólo admite \`sync-fixtures PL\`. Normaliza competición, temporada, equipos, calendario, estado y marcador final; no deserializa estadísticas, árbitros ni alineaciones.

\`StatsBombOpenDataProvider\` recibe un par \`competition_id/season_id\`, descarga partidos y eventos públicos con concurrencia máxima 4. El calculador actual deriva goles, tiros, tiros a puerta, córners, offsides, faltas y amarillas/rojas. No persiste alineaciones ni árbitros. En la ingesta histórica, \`AvailableAtUtc\` se resuelve conservadoramente al instante de ingesta: no es evidencia de disponibilidad prepartido. El World Cup 2018 existente no fue tocado.

## Método y evidencia

Se consultaron de sólo lectura los [metadatos públicos de StatsBomb](https://raw.githubusercontent.com/statsbomb/open-data/master/data/competitions.json), el árbol público del repositorio y los ficheros de partidos indicados abajo. El [README de Open Data](https://github.com/statsbomb/open-data/blob/master/README.md) define: partidos por competición/temporada, y eventos y alineaciones por \`match_id\`.

Los recuentos **verificados** son \`partidos / eventos / alineaciones\` presentes en una temporada pública. Un recuento parcial es una muestra: jamás debe presentarse como toda la competición.

| Objetivo | Temporadas del catálogo Open Data | Ficheros verificados | Conclusión |
|---|---|---|---|
| Premier League | 2015/16 (2/27), 2003/04 (2/44) | 2015/16: **380 / 380 / 380** | Histórico; no evidencia de temporada reciente. |
| Serie A | 2015/16 (12/27), 1986/87 (12/86) | 2015/16: **380 / 380 / 380** | Histórico; no reciente. |
| La Liga | 1973/74; 2004/05–2020/21 (11, IDs de temporada variables) | 2020/21: **35 / 35 / 35** | Sólo muestra parcial. |
| Bundesliga | 2015/16 (9/27), 2023/24 (9/281) | 2023/24: **34 / 34 / 34** | Muestra reciente parcial. |
| Ligue 1 | 2015/16, 2021/22, 2022/23 (7, IDs variables) | 2022/23: **32 / 32 / 32** | Muestra parcial. |
| Champions League | 1970/71–1972/73, 1999/00, 2003/04–2018/19 (16, IDs variables) | 2018/19: **1 / 1 / 1** | Catálogo amplio, muestra mínima. |
| Europa League | 1988/89 (35/75) | **3 / 3 / 3** | Muestra histórica mínima. |
| Conference League | Sin entrada | — | No disponible en el catálogo actual. |
| UEFA selecciones | Euro 2020 (55/43), Euro 2024 (55/282) | Euro 2024: **51 / 51 / 51** | Euro disponible; no se hallaron Nations League ni clasificatorias UEFA. |
| Copa Libertadores | Sin entrada | — | No disponible. |
| Copa Sudamericana | Sin entrada | — | No disponible. |
| Copa América | 2024 (223/282) | **32 / 32 / 32** | Torneo disponible. |
| Eliminatorias CONMEBOL | Sin entrada | — | No disponible. |

La existencia de metadatos no prueba que haya todos los partidos ni datos actuales. Los campos \`match_available\` y \`match_updated\` no constituyen un SLA operativo.

## football-data.org: acceso real frente a documentación

La [cobertura pública](https://www.football-data.org/coverage) enumera gratuitamente Premier League, Bundesliga, Ligue 1, Serie A, La Liga, Champions League y Euro, y lista Europa League, Conference League, Nations League y clasificatorias UEFA en su catálogo.

Eso no identifica el plan del token ni prueba una respuesta autorizada. Por código y por validación previa del proyecto, el único soporte actual del adaptador es **Premier League**; cualquier competición adicional es **desconocida** hasta una sonda autenticada aprobada. Copa Libertadores, Sudamericana, América y eliminatorias CONMEBOL tampoco pueden atribuirse a esta fuente sin esa evidencia.

La [documentación de precios](https://www.football-data.org/pricing) anuncia line-ups y tarjetas en planes superiores, y el Statistic Add-On enumera córners, offsides, faltas, tiros y tarjetas. Son afirmaciones documentales: no están modeladas ni autorizadas para este entorno. El [manual](https://www.football-data.org/documentation/api) pide solicitudes inteligentes, indica 10 llamadas/minuto en Free y acepta nulos; Free anuncia resultados y horarios retrasados.

## Matriz por campo

| Campo | Fuente candidata | Evidencia / cobertura | Límites, nulos y frecuencia |
|---|---|---|---|
| Resultados recientes | football-data.org | Documentación para competiciones listadas; proyecto sólo PL validada | Plan desconocido; marcador puede ser nulo antes del final; Free retrasado. |
| Resultados históricos | StatsBomb Open Data | Verificado en los archivos de la tabla | Dataset curado, no feed reciente ni SLA. |
| Córners, tarjetas, offsides, tiros y tiros a puerta | StatsBomb eventos | Verificado donde existe evento; el calculador local ya los deriva | Eventos/campos pueden faltar; conservar semántica StatsBomb. |
| Mismas estadísticas | football-data.org | Sólo documentación del add-on | Autorización, cobertura y forma de respuesta desconocidas. |
| Identidad de árbitro | StatsBomb / football-data.org | Árbitro observado en metadatos públicos de World Cup 2018; la documentación de football-data muestra árbitros | No está mapeado; puede ser nulo o haber varios oficiales. |
| Promedio de tarjetas por árbitro | Cálculo interno futuro | Sin implementación | Requiere identidad estable, rol principal e histórico anterior al cutoff. |
| Alineación confirmada | StatsBomb / football-data.org | Archivos \`lineups\` verificados en muestras; football-data sólo documentado | StatsBomb es histórico/postpartido; no se verificó disponibilidad preinicio en football-data. |
| Alineación probable | Ninguna fuente verificada | Desconocido | No confundir con confirmada; requiere contrato y experimento explícitos. |

StatsBomb exige atribución y logo al publicar análisis derivados ([términos](https://github.com/statsbomb/open-data/blob/master/README.md)). Para football-data.org hay que revisar contrato y plan vigentes antes de almacenar o redistribuir.

## Experimento futuro de frescura de alineaciones (no implementado)

1. Seleccionar seis partidos de una o dos competiciones confirmadas por una respuesta autenticada; registrar ID externo y kickoff UTC.
2. Por respuesta conservar: \`RequestedAtUtc\`, \`ReceivedAtUtc\`, código HTTP, fingerprint, estado (\`absent/probable/confirmed/changed\`), y \`ProviderPublishedAtUtc\` si existe. \`FirstObservedAtUtc\` es propio, no publicación del proveedor.
3. Muestrear cada 12 h desde T-72 a T-24, cada 4 h hasta T-4, cada hora hasta T-1 y cada 15 min hasta kickoff. Detener tras confirmación y observar una vez postpartido para detectar correcciones. Preferir consulta agrupada por fecha/competición.
4. Peor caso: aproximadamente 18 observaciones por partido, 108 respuestas para seis partidos. Distribuido en el tiempo queda bajo el límite Free documentado de 10/minuto; respetar cabeceras y detener ante 429/Retry-After.
5. No hacer polling sin aprobar endpoint, plan, T&C, retención y un modelo inmutable que preserve cada cambio.

## Arquitectura y siguiente paso mínimo

Mantener adaptadores separados para calendario/resultados y detalle (estadísticas/alineaciones). Todos deben emitir contratos normalizados, mantener \`ProviderIdentifier\` separado del \`Guid\` interno y preservar proveedor, ID externo, fingerprint, \`ProviderPublishedAtUtc\` opcional, \`AvailableAtUtc\` e \`IngestedAtUtc\`. Las features operativas deben filtrar por ambos timestamps.

El siguiente paso más pequeño, sujeto a aprobación, es una **sonda autenticada de sólo lectura** del catálogo y de una respuesta PL, con presupuesto acordado y sin persistencia. Debe confirmar plan, campos reales, nulos y rate-limit. Si no hay lineups probables o cobertura CONMEBOL, habrá que evaluar otro proveedor licenciado antes de diseñar migraciones.
## Sonda autenticada de football-data.org — 2026-09-25

Se ejecutó una sonda de sólo lectura con el token ya configurado en el proceso. No se imprimió, almacenó ni registró el token, la cabecera de autenticación ni payloads completos. Cinco respuestas fueron HTTP 200. La quinta repitió sólo el detalle del mismo partido: un error local de variable ocurrió después de recibir la cuarta respuesta, antes de clasificar sus campos anidados. No hubo reintentos de errores HTTP, escrituras ni ingestas.

| # | Ruta | RequestedAtUtc | ReceivedAtUtc | HTTP | Cuota expuesta |
|---:|---|---|---|---:|---|
| 1 | competitions | 2026-09-25T07:46:25.8006776Z | 2026-09-25T07:46:26.5704964Z | 200 | API v4; disponibles/minuto 9; reset 60 s |
| 2 | competitions/PL | 2026-09-25T07:46:26.6131167Z | 2026-09-25T07:46:27.1518472Z | 200 | API v4; disponibles/minuto 9; reset 59 s |
| 3 | competitions/PL/matches?limit=1 | 2026-09-25T07:46:27.1550521Z | 2026-09-25T07:46:27.9862019Z | 200 | API v4; disponibles/minuto 8; reset 59 s |
| 4 | matches/560542 | 2026-09-25T07:46:28.0746765Z | 2026-09-25T07:46:28.5726964Z | 200 | API v4; disponibles/minuto 7; reset 58 s |
| 5 | matches/560542 (inventario correctivo) | 2026-09-25T07:47:10.7598592Z | 2026-09-25T07:47:11.5758624Z | 200 | API v4; disponibles/minuto 6; reset 15 s |

No se expuso Retry-After. Son valores puntuales, no una garantía de cuota futura.

### Competiciones devueltas por el catálogo autenticado

El catálogo incluyó: Premier League (PL, TIER_ONE, temporada actual 2502), UEFA Champions League (CL, TIER_ONE, 2557), European Championship (EC, TIER_ONE, 1537), Ligue 1 (FL1, TIER_ONE, 2497), Bundesliga (BL1, TIER_ONE, 2522), Serie A (SA, TIER_ONE, 2494), Primera Division/La Liga (PD, TIER_ONE, 2518) y Copa Libertadores (CLI, TIER_FOUR, 2466).

**Verificado por respuesta autenticada:** esas ocho entradas aparecen en el catálogo entregado al token. Esto no verifica cada endpoint de cada competición: sólo se probó individualmente PL. Europa League, Conference League, Nations League, Copa Sudamericana, Copa América y eliminatorias CONMEBOL no aparecieron como coincidencias objetivo; su disponibilidad es **desconocida**, no una prohibición definitiva.

El detalle de competitions/PL expuso area, code, currentSeason, emblem, id, lastUpdated, name, seasons y type, con 128 temporadas. El catálogo de temporadas PL queda verificado; no se consultaron temporadas de las otras competiciones.

### Inventario de una respuesta de partido PL

La ruta matches/560542 devolvió un partido FINISHED de temporada 2502. Campos de primer nivel: area, awayTeam, competition, group, homeTeam, id, lastUpdated, matchday, odds, referees, score, season, stage, status, utcDate y venue.

| Campo | Resultado de la muestra autenticada | Clasificación |
|---|---|---|
| Resultado final | score.fullTime presente (3–0) | Verificado, partido terminado |
| Árbitro | referees presente, 1 elemento | Verificado en una muestra |
| Estadísticas | Propiedad statistics ausente | No disponible en la muestra; no prueba ausencia global |
| Alineación confirmada | homeTeam y awayTeam contenían crest, id, name, shortName y tla; lineup y bench ausentes | No disponible en la muestra; no prueba ausencia global |
| Estado de lineup | No aparece entre los campos devueltos | No disponible en la muestra |
| Alineación probable | No observada | Desconocida |
| Nulos/faltantes | Faltan statistics, lineup, bench y estado de lineup; el resultado terminado no era nulo | Una muestra únicamente |

La integración local no deserializa árbitros, estadísticas o alineaciones, aun si una futura respuesta autorizada los ofreciera. No se cambia código.

### Conclusión revisada

Ahora está **verificado por respuesta autenticada** que el token puede leer el catálogo, temporadas de PL y un detalle de partido PL, y que expone cabeceras de cuota. Estadísticas y lineups de planes superiores siguen **documentados pero no verificados con el token**. Alineaciones probables siguen **desconocidas**.

El siguiente paso mínimo, sujeto a aprobación, es una única sonda autenticada de sólo lectura de CL o PD y, sólo si el contrato documenta un endpoint específico de lineups, una respuesta individual de ese endpoint. Debe detenerse ante 401, 403 o 429 y no persistir respuestas. No iniciar polling ni diseñar migraciones hasta confirmar contrato y cobertura.
## Verificación autenticada — Champions League

Se realizó exactamente una solicitud autenticada de sólo lectura a competitions/CL.

| Ruta | RequestedAtUtc | ReceivedAtUtc | HTTP | Cabeceras de cuota |
|---|---|---|---:|---|
| competitions/CL | 2026-09-25T07:50:52.5520803Z | 2026-09-25T07:50:53.1442991Z | 200 | API v4; disponibles/minuto 9; reset 60 s |

**Acceso CL verificado:** el token obtuvo el recurso de competición. La respuesta contenía area, code, currentSeason, emblem, id, lastUpdated, name, seasons y type. Identificó UEFA Champions League (CL), con 47 temporadas: la más antigua iniciaba 1980-09-16 y la más reciente/currentSeason es 2557, 2026-09-08 a 2027-01-27. El campo plan fue nulo en esta respuesta, así que no permite inferir una suscripción concreta. No hubo Retry-After ni otra restricción indicada en esta única respuesta.

### Lineups: endpoint oficial y restricción

La [referencia oficial de Match](https://www.football-data.org/documentation/api) documenta lineup y bench dentro de homeTeam y awayTeam de la representación de **detalle de partido**. No identifica un endpoint independiente de lineups. La [página de precios](https://www.football-data.org/pricing) anuncia Line-ups & Subs desde Free + Deep Data y en planes superiores; por tanto el recurso candidato autorizado es matches/{matchId}, pero su contenido de alineaciones para este token sigue sin verificarse.

No se llamó matches de Champions League, ni se hizo ninguna solicitud adicional. La disponibilidad temporal de alineaciones confirmadas o probables sigue siendo desconocida.
