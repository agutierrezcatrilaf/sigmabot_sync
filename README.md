# SigmabotSync.Api

Backend del **configurador** (SigmabotConfig.Api). Repo independiente para publicar en GitLab sin código del worker ni clientes Aconex.

## Proyectos

| Proyecto | Rol |
|----------|-----|
| `SigmabotConfig.Api` | API REST + SSO |
| `SigmabotSync.Domain` | Entidades y reglas (copia; puede divergir del worker) |
| `SigmabotSync.Infrastructure.Config` | SQL / editores CRUD del configurador |

## Build

```bash
dotnet build SigmabotConfig.sln -c Release
```

## Publish (API + Angular)

Requiere el front en `../SigmabotConfig` (repo Angular).

**Desde la raíz del repo** (recomendado):

```bash
Publish-Configurador.bat
```

o:

```bash
Scripts\Publish-Configurador.bat
```

Salida: `publish\configurador\api`, `publish\configurador\web` y ZIP `publish\SigmabotConfig-Servidor.zip`.

La connection string para `appsettings.Production.json` se toma de (en orden): parámetro `-ConnectionString`, `appsettings.Development.json`, o `../SigmabotSync.Worker/SigmabotSync.Console/settings.json`.

## Relación con otros repos

- **SigmabotConfig** — front Angular (consume esta API).
- **SigmabotSync.Worker** — consola batch + integración Aconex (copia propia de `Domain`; no compartir git).

Este repo se generó desde el monorepo histórico `SigmabotSync`; los cambios compartidos hay que replicarlos manualmente o portar commits entre repos hasta que definan un paquete común.
