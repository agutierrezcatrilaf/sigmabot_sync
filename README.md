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

Requiere el front en `../SigmabotConfig` (repo Angular separado).

```bash
Scripts\Publish-Configurador.bat
```

Salida: `publish\configurador\api` y `publish\configurador\web`.

## Relación con otros repos

- **SigmabotConfig** — front Angular (consume esta API).
- **SigmabotSync.Worker** — consola batch + integración Aconex (copia propia de `Domain`; no compartir git).

Este repo se generó desde el monorepo histórico `SigmabotSync`; los cambios compartidos hay que replicarlos manualmente o portar commits entre repos hasta que definan un paquete común.
