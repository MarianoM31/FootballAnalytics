# Data Principles

## Identificadores internos y externos

Cada entidad del dominio usa un identificador interno propio. Los IDs de proveedores se almacenarán como asociaciones `InternalEntityId`, `Provider`, `ExternalId` y `EntityType`, por lo que un equipo, jugador o fixture puede tener varios IDs externos.

## Tiempo y datos históricos

Todos los timestamps se almacenarán y comunicarán en UTC. Los datos históricos observados deben preservarse de forma inmutable cuando representen un hecho conocido en un momento determinado. Las correcciones de proveedor deben conservar su momento de obtención, no sobrescribir silenciosamente el contexto histórico.

## Prevención de data leakage

Una predicción histórica solo puede usar información disponible cuando se calculó. Los futuros flujos distinguirán entre el hecho observado, `obtained_at_utc`, las features calculadas, la predicción, la versión del modelo y `calculated_at_utc`. Los backtests deben reconstruir este corte temporal y trabajar con snapshots, nunca con el estado más reciente de la base de datos.

## Alineaciones

Un fixture puede nacer sin alineación, luego recibir una probable y finalmente una confirmada. El flujo futuro será: fixture programado, analítica previa, alineación probable cuando exista, alineación confirmada, recálculo, partido y estadísticas finales. El estado de la alineación será parte explícita de cualquier snapshot analítico.
