# Configurador en servidor de pruebas (API + Angular)

Misma máquina donde ya está `SigmabotSync.Console`. Ejemplo de carpetas:

```
C:\Sigmabot\
  SigmabotSync\              ← consola (ya desplegada)
  SigmabotConfig.Api\        ← API
  SigmabotConfig.Web\        ← Angular (archivos estáticos)
```

## 1. Generar el release en su PC (antes de copiar)

Desde el repo `SigmabotSync`, con la **IP o nombre** del servidor de pruebas:

```powershell
cd "...\SigmabotSync"
.\scripts\Build-ConfiguradorRelease.ps1 -ServerHost "IP_O_NOMBRE_DEL_SERVIDOR" -ApiPort 5154 -WebPort 8080
```

El script genera:

- `web/config.json` → URL de la API (`http://SERVIDOR:5154`)
- `api/appsettings.Production.json` → ConnectionString (desde su config local) + CORS
- `web/web.config` → versión sin URL Rewrite (use `-UseUrlRewrite` si IIS tiene el módulo)

Salida: `publish\configurador\api` y `publish\configurador\web`.

> Si cambia el host o puerto, vuelva a ejecutar el script y regenere el ZIP.

## 2. Requisitos en el servidor

- **.NET 8 Runtime** (API)
- **IIS** (recomendado para Angular) con ASP.NET Core Hosting Bundle, **o** solo Kestrel + otro servidor estático
- SQL Server accesible (misma BD que la consola)
- Firewall: permitir puertos **5154** (API) y **8080** (web, si usa ese puerto)

## 3. Desplegar la API

1. Copie `publish\configurador\api` → carpeta del servidor (ej. `C:\ProgramData\Sigmatec\Salfa\api`)
2. Verifique **`appsettings.Production.json`** (generado automáticamente con BD + CORS)
3. Si el script no encontró cadena local, renombre `appsettings.Production.json.example` → `appsettings.Production.json` y complete la BD
4. En el servidor:

```cmd
run-api.bat
```

5. Compruebe: `http://SU_SERVIDOR:5154/api/system/status` → `databaseReachable: true`

Opcional: `install-api-task.bat` (como administrador) para arrancar la API al iniciar Windows.

## 4. Desplegar Angular (IIS)

1. Copie `publish\configurador\web` → carpeta del sitio IIS (incluye `web.config` y `config.json`)
2. En IIS → Sitio nuevo o aplicación:
   - Ruta física: carpeta `web`
   - Enlace: puerto **8080** (o el que usó en el script)
3. Abra `http://SU_SERVIDOR:8080`

## 5. Comprobar

| URL | Esperado |
|-----|----------|
| `http://SERVIDOR:5154/swagger` | Swagger API |
| `http://SERVIDOR:5154/api/system/status` | BD OK |
| `http://SERVIDOR:8080` | Configurador Angular |
| `http://SERVIDOR:8080/config.json` | `apiBaseUrl` apuntando a `:5154` |

## 6. Sin IIS (solo prueba rápida)

- API: `run-api.bat`
- Web: use IIS o un servidor estático. Para producción use IIS.

## Notas

- La consola **no** necesita la API; el configurador **sí** necesita API + BD.
- `publish/` contiene credenciales; no lo suba al repositorio.
- Origen de ConnectionString al generar: `-ConnectionString`, luego `SigmabotConfig.Api/appsettings.Development.json`, luego `SigmabotSync.Console/settings.json`.
