# SigmabotConfig.Api

API REST para el configurador de SigmabotSync (credenciales, trabajos, parámetros, programación).

Depende de `SigmabotSync.Domain` y `SigmabotSync.Infrastructure.Config` (SQL del configurador, sin clientes Aconex). La integración Aconex vive en `SigmabotSync.Infrastructure` y solo la usa la consola.

## Configuración de base de datos

La cadena de conexión **solo** se define en el servidor, en `appsettings` o variables de entorno:

```json
{
  "Database": {
    "ConnectionString": "Server=...;Database=sigmabot;..."
  }
}
```

El front Angular **no** envía ni guarda la connection string.

Copie `appsettings.Development.json.example` → `appsettings.Development.json` y complete sus valores (ese archivo está en `.gitignore`).

## Ejecución local

```bash
cd SigmabotConfig.Api
dotnet run
```

- HTTP: http://localhost:5154
- Swagger: http://localhost:5154/swagger

## CORS

Por defecto permite `http://localhost:4200` (Angular). Ajuste `Cors:AllowedOrigins` en `appsettings` para QA/Prod.

## Endpoints principales

| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | `/api/system/status` | Estado de conexión BD + flag ejecución a demanda |
| POST | `/api/trabajos/{id}/ejecutar` | Lanza el worker (`--manual`). Solo si `OnDemandExecution:Enabled` |
| GET/POST/PUT/DELETE | `/api/credenciales` | CRUD credenciales |
| GET/POST/PUT/DELETE | `/api/trabajos` | CRUD trabajos |
| GET/PUT | `/api/trabajos/{id}/parametros` | Parámetros guiados por tipo |
| GET/POST/PUT/DELETE | `/api/trabajos/{id}/programacion` | Programación |
| GET | `/api/catalogos/*` | Tipos, estados, combos, definición de campos |

## Autorizador corporativo

La API exige `Authorization: Bearer {token}` y valida el recurso con el Autorizador Salfa (`/user/validarRecursoToken/{Recurso}`).

| Entorno | `Autorizador:Enabled` | Swagger |
|---------|----------------------|---------|
| Development (local) | `false` en `appsettings.Development.json` | Disponible |
| QA / Production | `true` (obligatorio) | **No** publicado |

Configuración relevante:

```json
{
  "Autorizador": {
    "Enabled": true,
    "UrlApi": "https://apiautorizadorqa.salfagestion.cl/api/v1.0",
    "Recurso": "configsigmabot-api"
  }
}
```

- En **QA** use `UrlApi` del Autorizador QA; en **Prod** el de producción.
- El front Angular debe enviar el Bearer del SSO/Autorizador en todas las llamadas.
- Sin token válido → `401 Unauthorized`.
- No desactive `Enabled` en servidores expuestos.
