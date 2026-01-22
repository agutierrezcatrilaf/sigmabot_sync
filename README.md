# SigmabotSync

## Resumen del Proyecto

**SigmabotSync** es una aplicación desarrollada en C# (.NET Framework 4.8) que implementa dos casos de uso principales:
1. **Extracción de documentos** desde proyectos de Aconex
2. **Sincronización de documentos** entre proyectos de Aconex

El proyecto está diseñado siguiendo los principios de **Arquitectura Hexagonal** (Ports & Adapters), donde el dominio es independiente de las implementaciones concretas de infraestructura y presentación.

> **Nota**: La capa UI (Windows Forms) está siendo deprecada. La configuración se leerá desde una base de datos en el futuro.

## Arquitectura Hexagonal

El proyecto sigue una **Arquitectura Hexagonal** (Ports & Adapters) con las siguientes capas:

### 1. **SigmabotSync.Domain** (Núcleo - Puertos)
**Puertos (Interfaces)** que definen los contratos que el dominio necesita:
- `IExternalApiClient` - Puerto para comunicación con APIs externas
- `IProjectService` - Puerto para servicios de proyectos

**Entidades y Modelos del Dominio**:
- `Project` - Entidad que representa un proyecto de Aconex
- `DocumentMetadata` - Modelo con metadatos completos de un documento (serialización XML)
- `DocumentIntegrityInfo` - Modelo con información de integridad (ID, fechas de modificación)
- `UserInfo` - Modelo con información de usuario
- `AconexSettings` - Configuración de credenciales (será migrada a BD)

### 2. **SigmabotSync.Application** (Casos de Uso)
**Casos de uso organizados por funcionalidad**:

#### **Extraction/** (Extracción de Documentos)
- *Pendiente de implementación* - Lógica para extraer documentos desde Aconex

#### **Synchronization/** (Sincronización de Documentos)
- `DocumentSyncWorker` - Worker que orquesta el proceso de sincronización con eventos de progreso
- `DocumentService` - Servicio de aplicación para operaciones con documentos
- `ProjectService` - Servicio de aplicación para operaciones con proyectos

#### **Common/** (Compartido)
- *Pendiente* - Utilidades y lógica compartida entre casos de uso

### 3. **SigmabotSync.Infrastructure** (Adaptadores de Salida)
**Adaptadores que implementan los puertos del dominio**:

- **Clientes Aconex** (Adaptadores de API Externa):
  - `AconexClientBase` - Clase base con autenticación HTTP Basic y header `X-Application-Key`
  - `AconexDocumentClient` - Implementa operaciones con documentos (obtener documentos modificados, metadatos)
  - `AconexProjectClient` - Implementa operaciones con proyectos
  - `AconexUserClient` - Implementa operaciones con usuarios

- **Servicios de Infraestructura**:
  - `SettingsService` - Adaptador para gestión de configuración (actualmente `settings.json`, migrar a BD)

### 4. **SigmabotSync.UI** (Adaptador de Entrada - ⚠️ Deprecado)
> **Estado**: Esta capa será deprecada. La configuración se leerá desde base de datos.

- `MainForm` - Formulario principal (deprecado)
- `ConfigForm` - Formulario de configuración (deprecado)

## Casos de Uso

### 1. Extracción de Documentos (Extraction)
**Estado**: Pendiente de implementación

Este caso de uso se encargará de:
- Extraer documentos desde proyectos de Aconex
- Procesar y almacenar documentos extraídos
- Gestionar el ciclo de vida de la extracción

### 2. Sincronización de Documentos (Synchronization)
**Estado**: Parcialmente implementado

Flujo actual de sincronización:
1. **Obtención de documentos modificados**: Consulta la API de Aconex para obtener documentos modificados desde una fecha específica usando el endpoint de integridad (`/register/integrity`)
2. **Obtención de metadatos**: Para cada documento modificado, obtiene sus metadatos completos desde el endpoint `/register/{documentId}/metadata`
3. **Procesamiento**: El worker procesa cada documento y emite eventos de progreso y estado
4. **Actualización en destino**: ⏳ Pendiente de implementación (`UpdateDocumentOnDestinationAsync`)

### Gestión de Proyectos
- Obtención de proyectos disponibles del usuario desde Aconex
- Validación de proyectos origen y destino

### Configuración
- **Actual**: Almacenamiento de credenciales en `settings.json` (temporal)
- **Futuro**: Configuración desde base de datos (migración pendiente)

## Tecnologías y Dependencias

- **.NET Framework 4.8**
- **Newtonsoft.Json** (v13.0.4) - Serialización JSON para configuración (temporal)
- **System.Net.Http** - Cliente HTTP para comunicación con API de Aconex
- **System.Xml.Serialization** - Deserialización de respuestas XML de Aconex
- **Windows Forms** - ⚠️ Deprecado (solo para desarrollo/tests)

## Estado del Proyecto

### Implementado ✅
- Arquitectura hexagonal con puertos y adaptadores
- Autenticación con API de Aconex
- Obtención de proyectos del usuario
- Obtención de documentos modificados desde una fecha
- Obtención de metadatos de documentos
- Worker de sincronización con eventos de progreso
- Gestión de configuración temporal (JSON)

### En Desarrollo / Pendiente ⏳

#### Sincronización
- Actualización de documentos en el proyecto destino (`UpdateDocumentOnDestinationAsync`)
- Descarga de archivos de documentos
- Subida de archivos al proyecto destino

#### Extracción
- Implementación completa del caso de uso de extracción
- Lógica de procesamiento de documentos extraídos

#### Infraestructura
- Migración de configuración desde JSON a base de datos
- Adaptador de base de datos para configuración
- Manejo de errores más robusto
- Logging estructurado
- Validaciones adicionales

#### UI
- Deprecación completa de la capa UI

## Estructura de Directorios

```
SigmabotSync/
├── SigmabotSync.Domain/              # Núcleo - Puertos (interfaces), Entidades, Modelos
│   ├── Interfaces/                   # Puertos (contratos)
│   ├── Entities/                     # Entidades del dominio
│   ├── Models/                       # Modelos de dominio
│   └── Config/                       # Configuración (temporal)
│
├── SigmabotSync.Application/         # Casos de Uso
│   ├── Extraction/                   # Caso de uso: Extracción de documentos
│   ├── Synchronization/              # Caso de uso: Sincronización de documentos
│   │   └── DocumentSyncWorker.cs     # Worker de sincronización
│   ├── Common/                       # Utilidades compartidas
│   └── Services/                     # Servicios de aplicación
│
├── SigmabotSync.Infrastructure/      # Adaptadores de Salida
│   ├── External/                     # Adaptadores de APIs externas (Aconex)
│   └── Services/                     # Adaptadores de servicios (configuración, BD futura)
│
├── SigmabotSync.UI/                  # ⚠️ Adaptador de Entrada (Deprecado)
│   └── [Formularios Windows Forms]
│
└── README.md                          # Este archivo
```

## Principios de Arquitectura Hexagonal

### Puertos (Domain)
- **Puertos de Salida**: Interfaces que el dominio necesita para interactuar con el exterior (ej: `IExternalApiClient`)
- **Puertos de Entrada**: Interfaces que definen cómo el exterior interactúa con el dominio (futuro)

### Adaptadores (Infrastructure/UI)
- **Adaptadores de Salida**: Implementaciones concretas de los puertos de salida (ej: `AconexDocumentClient`)
- **Adaptadores de Entrada**: Puntos de entrada al sistema (ej: UI, API REST futura, workers)

### Beneficios
- **Independencia**: El dominio no depende de implementaciones concretas
- **Testabilidad**: Fácil mockear adaptadores para tests
- **Flexibilidad**: Cambiar infraestructura sin afectar el dominio
- **Separación de responsabilidades**: Casos de uso claramente separados (Extraction vs Synchronization)

## Configuración

### Actual (Temporal)
- Configuración almacenada en `settings.json` en el directorio de ejecución
- Credenciales: Usuario, Contraseña, Integration ID de Aconex

### Futuro
- Configuración desde base de datos
- Múltiples configuraciones por proyecto/sesión
- Gestión centralizada de credenciales

## Notas Técnicas

- **Autenticación**: HTTP Basic Authentication con Base64 + header `X-Application-Key`
- **Formato de respuestas**: Aconex devuelve XML, se deserializa a modelos del dominio
- **Eventos**: El worker utiliza eventos para notificar progreso (útil para logging/monitoreo futuro)
- **API Base**: `https://us1.aconex.com/api/`
- **Endpoints principales**:
  - `/projects` - Lista de proyectos
  - `/projects/{projectId}/register/integrity` - Documentos modificados
  - `/projects/{projectId}/register/{documentId}/metadata` - Metadatos de documento
