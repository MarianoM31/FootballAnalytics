# Database migrations

`FootballAnalytics.DatabaseMigrator` es el único ejecutable responsable de crear la base de datos configurada y aplicar su esquema versionado. Usa SQL Server, Dapper y Windows Authentication; no usa Entity Framework ni requiere scripts manuales en SSMS.

## Configuración local

La configuración de desarrollo está en `src/FootballAnalytics.DatabaseMigrator/appsettings.json` y usa una cadena sin usuario ni contraseña. Para otra instancia local o de desarrollo, establece `ConnectionStrings__FootballAnalyticsDb` como variable de entorno. El migrador deriva una conexión a `master` desde esa cadena solo para comprobar o crear la base configurada.

Ejecuta el migrador desde la raíz del repositorio:

```powershell
dotnet run --project src/FootballAnalytics.DatabaseMigrator
```

## Añadir una migración

Agrega un archivo con nombre ordenable en `database/migrations`, por ejemplo `002_add_match_statistics.sql`. El migrador carga los archivos en orden, crea `dbo.SchemaMigrations` cuando es necesario y registra cada script solo tras aplicarlo correctamente, dentro de la misma transacción que la migración.

Nunca modifiques una migración que ya haya sido aplicada en un entorno compartido. Cualquier cambio de esquema debe llegar en una nueva migración. Las migraciones versionan estructura, índices y constraints; datos locales, secretos y copias de seguridad no pertenecen al repositorio.
