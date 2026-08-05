# SigmabotSync

## Resumen del Proyecto

**SigmabotSync** es una solución desarrollada en C# sobre **.NET 8** que automatiza la interacción con la plataforma **Aconex** y la operación de "trabajos" programados sobre una base de datos SQL Server. Cubre tres casos de uso principales:

1. **Extracción** de documentos, incidentes, correos y workflows desde proyectos de Aconex.
2. **Sincronización** de documentos entre proyectos de Aconex.
3. **Carga de archivos con metadatos** hacia proyectos de Aconex.

El diseño sigue los principios de **Arquitectura Hexagonal** (Ports & Adapters): el dominio define los puertos (interfaces) y la infraestructura implementa los adaptadores concretos (clientes HTTP, acceso a BD, etc.).

> **Nota**: La configuración de credenciales y trabajos ya está migrada a **SQL Server**. El `settings.json` que aún existe sólo se usa para la cadena de conexión a la BD.

## Arquitectura Hexagonal

### 1. `SigmabotSync.Domain` (Núcleo - Puertos)

**Puertos (interfaces)** que definen los contratos que el dominio necesita del exterior:

- `IAconexHttpGetPort` - Operaciones HTTP GET genéricas contra Aconex.
- `IAconexRegisterSearchPort` - Búsquedas en el register de Aconex.
- `IAconexRegisterDocumentContentPort` - Descarga de contenido de documentos.
- `IAconexRegisterWritePort` - Escritura/creación de documentos en el register.
- `IDocumentSyncReadPort` - Lectura para el flujo de sincronización.

**Entidades del dominio**:

- `Project` - Proyecto de Aconex.
- `Credencial` - Credencial de acceso (Aconex u otros sistemas).
- `Trabajo`, `TrabajoConfiguracion`, `TrabajoProgramacion`, `TrabajoEjecucion` - Modelo de "trabajos" del scheduler.

**Modelos**:

- `DocumentMetadata`, `DocumentIntegrityInfo`, `UserInfo`.
- `Models/Extraction/*` - DTOs específicos para los flujos de extracción.

**Configuration**:

- Validadores y catálogos: `CredencialRequisitosValidator`, `TrabajoRequisitosValidator`, `TrabajoConfiguracionParamValidator`, `TrabajoTipoConfigFieldCatalog`, `CredencialTipoIds`, `TipoTrabajoIds`, `TrabajoEstadoIds`.

### 2. `SigmabotSync.Application` (Casos de Uso)

#### `Synchronization/`

- `DocumentSyncWorker` - Orquesta la sincronización con eventos de progreso.
- `DocumentService` - Servicio de aplicación para operaciones con documentos.

#### `Extraction/`

- `DocumentExtractionWorker` - Extracción de documentos.
- `IncidentExtractionWorker` - Extracción de incidentes.
- `MailExtractionWorker` - Extracción de correos.
- `WorkflowExtractionWorker` - Extracción de workflows.

#### `FileExtraction/`

- `FileExtractionWorker` - Descarga de archivos.
- `FileUploadWithMetadataWorker` - Subida de archivos con metadatos asociados.

#### `Services/`

- `ProjectService` - Servicio de aplicación para operaciones con proyectos.

#### `Common/`

- `AconexRegisterMultipart` - Helper de construcción multipart para Aconex.
- `AppState` - Estado compartido entre workers.
- `Utilities` - Utilidades transversales.

### 3. `SigmabotSync.Infrastructure.Config` (SQL del configurador)

Capa usada por **SigmabotConfig.Api**: editores CRUD, `TrabajosEjecucionService` y utilidades SQL. **Sin** clientes HTTP Aconex.

### 4. `SigmabotSync.Infrastructure` (Worker / Aconex)

#### `External/` (Adaptadores HTTP de Aconex)

- `AconexClientBase` - Clase base con autenticación Basic + header `X-Application-Key`.
- `AconexDocumentClient`, `AconexProjectClient`, `AconexUserClient` - Clientes legacy.
- `AconexHttpGetAdapter` - Implementa `IAconexHttpGetPort`.
- `AconexRegisterSearchAdapter` - Implementa `IAconexRegisterSearchPort`.
- `AconexRegisterDocumentContentAdapter` - Implementa `IAconexRegisterDocumentContentPort`.
- `AconexRegisterWriteAdapter` - Implementa `IAconexRegisterWritePort`.
- `AconexDocumentSyncAdapter`, `AconexExternalProjectAdapter` - Adaptadores adicionales para los flujos de sync.

#### `Services/` (Runtime del worker)

- `SettingsService` - Lectura del `settings.json` (sólo cadena de conexión).
- `CredencialesService` - Lectura de credenciales en BD (consola).
- `TrabajosService`, `TrabajosProgramacionService` - Consultas del scheduler.
- Servicios de estado ProjectSync / catálogo Aconex en BD.

> Editores CRUD, `ConnectionStringHelper`, `SqlDataReaderMapper` y `TrabajosEjecucionService` (compartido con la API) están en `SigmabotSync.Infrastructure.Config`.

### 5. Adaptadores de Entrada

| Proyecto | Tipo | Descripción |
|---|---|---|
| `SigmabotSync.Console` | Console app (.NET 8) | Runner principal. Ejecuta los trabajos programados (typically como Tarea Programada de Windows). |
| `SigmabotConfig.Api` | ASP.NET Core Web API (.NET 8) | API REST para la configuración (consumida por el front Angular `SigmabotConfig`). |
| `SigmabotSync.Tools.NetShareSmokeTest` | Console (.NET 8) | Utilidad de diagnóstico para validar accesos a network shares usados por los workers. |

## Casos de Uso

### Sincronización de Documentos

1. Consulta `/register/integrity` para obtener documentos modificados desde una fecha.
2. Obtiene metadatos completos de cada documento desde `/register/{documentId}/metadata`.
3. El worker emite eventos de progreso para logging y monitoreo.
4. Actualiza el documento en el proyecto destino.

### Extracción (Documentos / Incidentes / Mails / Workflows)

Cada worker recorre el proyecto origen, descarga los recursos pertinentes y los guarda en la ruta destino configurada (típicamente un network share).

### Carga de archivos con metadatos

`FileUploadWithMetadataWorker` toma archivos desde una ruta de entrada, construye los metadatos asociados (desde Excel/CSV/BD según configuración) y los sube al register de Aconex.

### Gestión de Trabajos

- Definición del tipo de trabajo + parámetros (`TrabajoConfiguracion`).
- Programación (`TrabajoProgramacion`).
- Registro histórico de ejecuciones (`TrabajoEjecucion`).
- Validación de requisitos al crear/editar trabajos.

## Tecnologías y Dependencias

- **.NET 8.0** (`net8.0`).
- **Microsoft.Data.SqlClient** `5.2.3` - Acceso a SQL Server.
- **Newtonsoft.Json** `13.0.4` - Serialización JSON (`settings.json`, payloads).
- **System.Net.Http** / **System.Xml.Serialization** (BCL) - Cliente HTTP y deserialización de respuestas XML de Aconex.

> Todas las versiones están **fijas** (sin rangos). Los `packages.lock.json` (uno por proyecto) garantizan restore reproducible. Ver sección [Seguridad y Builds Reproducibles](#seguridad-y-builds-reproducibles).

## Estructura de Directorios

```
SigmabotSync/
├── SigmabotSync.Domain/                    # Núcleo: puertos, entidades, modelos, validadores
│   ├── Ports/                              # Interfaces (puertos de salida)
│   ├── Interfaces/                         # Interfaces legacy
│   ├── Entities/                           # Entidades del dominio
│   ├── Models/                             # Modelos y DTOs (incluye Models/Extraction)
│   ├── Configuration/                      # Validadores y catálogos
│   └── Config/                             # Config legacy
│
├── SigmabotSync.Application/               # Casos de uso
│   ├── Synchronization/                    # Sincronización de documentos
│   ├── Extraction/                         # Extracción (docs, incidentes, mails, workflows)
│   ├── FileExtraction/                     # Descarga/Subida de archivos con metadatos
│   ├── Services/                           # Servicios de aplicación
│   └── Common/                             # Helpers compartidos
│
├── SigmabotSync.Infrastructure.Config/     # SQL del configurador (API)
│   ├── Services/ConfigurationEditor/       # CRUD para SigmabotConfig.Api
│   └── Data/                               # Helpers de mapeo SQL
│
├── SigmabotSync.Infrastructure/            # Worker: Aconex + runtime consola
│   ├── External/                           # Clientes/adaptadores HTTP de Aconex
│   └── Services/                           # Acceso a BD usado por la consola
│
├── SigmabotSync.Console/                   # Runner principal (Tarea Programada)
├── SigmabotConfig.Api/                     # API REST de configuración
├── SigmabotSync.Tools.NetShareSmokeTest/   # Utilidad de diagnóstico de shares
│
├── Scripts/                                # DDL de SQL Server (Credenciales, Trabajos, etc.)
├── postman/                                # Colecciones Postman de Aconex
├── deployment/                             # Scripts e instructivos de despliegue
├── .github/workflows/                      # CI/CD (a definir)
├── SigmabotSync.sln
└── README.md
```

## Configuración

### Conexión a base de datos

Cada ejecutable lee la cadena de conexión desde su `settings.json` local. Ejemplo:

```json
{
  "ConnectionString": "Server=...;Database=SigmabotSync;..."
}
```

### Credenciales y trabajos

Toda la configuración funcional (credenciales de Aconex, definición de trabajos, programaciones, parámetros) vive en **SQL Server**. Se edita desde `SigmabotConfig` (Angular, web) vía `SigmabotConfig.Api`.

El DDL de las tablas está en `Scripts/`:

- `CreateTable_Credenciales.sql`
- `CreateTable_Documentos.sql`
- `CreateTable_Trabajos.sql`
- `CreateTable_TrabajosConfiguracion.sql`
- `CreateTable_TrabajosEjecucion.sql`
- `CreateTable_TrabajosProgramacion.sql`
- `Alter_TrabajosEjecucion_FechaHoraFin_Nullable.sql`

## Seguridad y Builds Reproducibles

Para alinearnos con los estándares de seguridad del cliente (escaneo con Semgrep/Trivy y exigencia de instalación reproducible tipo `npm ci`), la solución tiene activada la siguiente configuración:

### `packages.lock.json` + `RestorePackagesWithLockFile`

Todos los `.csproj` declaran:

```xml
<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
```

Esto genera un archivo `packages.lock.json` al lado de cada `.csproj`, con las versiones exactas (directas y transitivas) y su `contentHash`. **Estos archivos están versionados en git** y no deben agregarse al `.gitignore`.

### Restore reproducible en CI

En cualquier pipeline (CI, build local de despliegue) se debe usar:

```bash
dotnet restore SigmabotSync.sln --locked-mode
```

`--locked-mode` falla el build si las versiones resueltas no coinciden con el `packages.lock.json`. Esto detecta:

- Cambios manuales de versión sin actualizar el lock.
- Paquetes alterados/republicados en NuGet.org.

### Flujo de trabajo al agregar/actualizar paquetes

1. Hacer el cambio en el `.csproj` o vía `dotnet add package <nombre> --version <X.Y.Z>`.
2. Correr `dotnet restore` (sin `--locked-mode`) → actualiza el `packages.lock.json` automáticamente.
3. **Commitear ambos archivos** (`.csproj` + `packages.lock.json`) juntos.

### Verificación de vulnerabilidades

Antes de pedir paso a QA/Producción conviene correr localmente:

```bash
dotnet list package --vulnerable --include-transitive
```

Y resolver cualquier paquete reportado antes de que lo marque Trivy en el pipeline del cliente.

## Notas Técnicas

- **Autenticación Aconex**: HTTP Basic (Base64) + header `X-Application-Key`.
- **Respuestas Aconex**: XML, deserializadas con `System.Xml.Serialization` a modelos del dominio.
- **Eventos de progreso**: los workers exponen eventos para logging/monitoreo (`DailyLog` en `SigmabotSync.Console`).
- **API Base**: `https://us1.aconex.com/api/`.
- **Endpoints principales**:
  - `/projects` - Lista de proyectos del usuario.
  - `/projects/{projectId}/register/integrity` - Documentos modificados.
  - `/projects/{projectId}/register/{documentId}/metadata` - Metadatos de documento.
  - `/projects/{projectId}/register/search` - Búsqueda en el register.

## Despliegue

La carpeta `deployment/` contiene scripts e instructivos:

- `install-task.bat` - Registra `SigmabotSync.Console` como Tarea Programada de Windows.
- `uninstall-task.bat` - La elimina.
- `run-sigmabot.bat` - Ejecución manual.
- `README-OPERACION.md` / `.html` / `.pdf` - Instructivo operativo.
