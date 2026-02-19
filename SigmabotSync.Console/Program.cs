using SigmabotSync.Application.Extraction;
using SigmabotSync.Application.FileExtraction;
using SigmabotSync.Application.Synchronization;
using SigmabotSync.Domain.Config;
using SigmabotSync.Domain.Entities;
using SigmabotSync.Infrastructure.External;
using SigmabotSync.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SigmabotSync.Console
{
    /// <summary>Tipos de trabajo soportados (valor en TrabajosConfiguracion TipoTrabajo).</summary>
    internal static class TipoTrabajoConst
    {
        public const string FileExtraction = "FileExtraction";
        public const string ProjectSync = "ProjectSync";
        public const string FullExtraction = "FullExtraction";
    }

    class Program
    {
        /// <summary>
        /// Al depurar: pon aquí el Id del trabajo a ejecutar (ej. 1) y al dar F5 se ejecutará solo ese trabajo.
        /// Pon null para usar argumentos de línea de comandos o el scheduler.
        /// </summary>
#if DEBUG
        private static readonly int? DebugIdTrabajo = 3;
#else
        private static readonly int? DebugIdTrabajo = null;
#endif

        static async Task Main(string[] args)
        {
            System.Console.WriteLine("=== SigmaBot File Extraction Console ===");
            System.Console.WriteLine();

            string connectionString = ObtenerConnectionStringDesdeSettings();
            if (connectionString == null)
                return;

            // Al debuggear: si DebugIdTrabajo está definido, ejecutar solo ese trabajo (ignora args)
            if (DebugIdTrabajo.HasValue)
            {
                System.Console.WriteLine("[Debug] Ejecutando trabajo Id=" + DebugIdTrabajo.Value + " (DebugIdTrabajo en código)");
                System.Console.WriteLine();
                await EjecutarUnTrabajoAsync(connectionString, DebugIdTrabajo.Value, "Local");
                return;
            }

            // Modo local: --local <id> o -l <id> (para desarrollo; ejecuta solo ese trabajo)
            var (idLocal, esLocal) = ObtenerIdTrabajoLocal(args);
            if (idLocal.HasValue)
            {
                System.Console.WriteLine(esLocal ? "Modo local: ejecutando trabajo Id=" + idLocal.Value : "Modo manual: ejecutando trabajo Id=" + idLocal.Value);
                System.Console.WriteLine();
                await EjecutarUnTrabajoAsync(connectionString, idLocal.Value, esLocal ? "Local" : "Manual");
                return;
            }

            var pendientes = ObtenerTrabajosPendientesParaScheduler(connectionString);
            if (pendientes != null && pendientes.Count > 0)
            {
                System.Console.WriteLine("Modo scheduler: " + pendientes.Count + " trabajo(s) pendiente(s) según TrabajosProgramacion.");
                System.Console.WriteLine();
                foreach (var idTrabajo in pendientes)
                {
                    System.Console.WriteLine("--- Ejecutando trabajo Id=" + idTrabajo + " ---");
                    await EjecutarUnTrabajoAsync(connectionString, idTrabajo, "Scheduler");
                    System.Console.WriteLine();
                }
                System.Console.WriteLine("Scheduler: ejecución finalizada.");
            }
            else
            {
                System.Console.WriteLine("No hay trabajos pendientes. Para ejecutar un trabajo en local: SigmabotSync.Console.exe --local <IdTrabajo> (o -l <IdTrabajo>). Manual: --manual <IdTrabajo> o solo <IdTrabajo>.");
            }
        }

        /// <summary>
        /// Parsea los argumentos y devuelve el IdTrabajo si se solicitó ejecución manual o local.
        /// Formas: --local 2, -l 2 (modo local), --manual 2, -m 2, o solo 2 (un único número).
        /// En local se registra como tipo ejecución "Local" en el historial.
        /// </summary>
        /// <returns>Tupla (idTrabajo, esLocal). esLocal true solo para --local/-l.</returns>
        static (int? id, bool esLocal) ObtenerIdTrabajoLocal(string[] args)
        {
            if (args == null || args.Length == 0)
                return (null, false);
            for (int i = 0; i < args.Length; i++)
            {
                var arg = (args[i] ?? "").Trim();
                if (arg == "--local" || arg == "-l")
                {
                    if (i + 1 < args.Length && int.TryParse(args[i + 1].Trim(), out int id) && id > 0)
                        return (id, true);
                    return (null, false);
                }
                if (arg == "--manual" || arg == "-m")
                {
                    if (i + 1 < args.Length && int.TryParse(args[i + 1].Trim(), out int id) && id > 0)
                        return (id, false);
                    return (null, false);
                }
                if (args.Length == 1 && int.TryParse(arg, out int idUnico) && idUnico > 0)
                    return (idUnico, false);
            }
            return (null, false);
        }

        /// <summary>
        /// Ejecuta un solo trabajo: configuración, credenciales, extracción de archivos, sincronización, guardado de resultado e historial.
        /// </summary>
        /// <param name="tipoEjecucion">"Manual" o "Scheduler" (se guarda en TrabajosEjecucion.TipoEjecucion).</param>
        static async Task EjecutarUnTrabajoAsync(string connectionString, int idTrabajo, string tipoEjecucion = "Scheduler")
        {
            DateTime? fechaInicioEjecucion = null;
            var etapasEjecutadas = new List<string>();
            bool exito = false;
            string mensajeError = null;
            string detalleError = null;

            try
            {
                TrabajoConfiguracion trabajoConfig = ObtenerYValidarConfiguracionTrabajo(idTrabajo, connectionString);
                if (trabajoConfig == null)
                    return;

                if (!ObtenerYValidarCredenciales(trabajoConfig, connectionString, out var credAconex, out var credBd))
                    return;

                fechaInicioEjecucion = DateTime.Now;

                string tipoTrabajo = (trabajoConfig.TipoTrabajo ?? "").Trim();
                bool tipoValido = tipoTrabajo == TipoTrabajoConst.FileExtraction
                    || tipoTrabajo == TipoTrabajoConst.ProjectSync
                    || tipoTrabajo == TipoTrabajoConst.FullExtraction;

                if (!tipoValido)
                {
                    mensajeError = string.IsNullOrEmpty(tipoTrabajo)
                        ? "Tipo de trabajo no configurado (campo Tipo en tabla Trabajos). Use: FileExtraction, ProjectSync o FullExtraction."
                        : "Tipo de trabajo no reconocido: " + tipoTrabajo + ". Use: FileExtraction, ProjectSync o FullExtraction.";
                    System.Console.WriteLine("No se ejecuta: " + mensajeError);
                    GuardarResultadoTrabajo(connectionString, idTrabajo, exito: false, mensajeError);
                    return;
                }

                System.Console.WriteLine("Tipo de trabajo: " + tipoTrabajo);
                System.Console.WriteLine();

                switch (tipoTrabajo)
                {
                    case TipoTrabajoConst.FileExtraction:
                        await EjecutarExtraccionArchivosAsync(trabajoConfig, credAconex, credBd, etapasEjecutadas);
                        SincronizarMetadataDocumentos(trabajoConfig, credAconex, credBd, etapasEjecutadas);
                        break;
                    case TipoTrabajoConst.ProjectSync:
                        await EjecutarProjectSyncAsync(trabajoConfig, credAconex, credBd, etapasEjecutadas);
                        break;
                    case TipoTrabajoConst.FullExtraction:
                        await EjecutarFullExtractionAsync(trabajoConfig, credAconex, credBd, etapasEjecutadas);
                        break;
                }

                System.Console.WriteLine("=== Extracción completada exitosamente (IdTrabajo=" + idTrabajo + ") ===");
                exito = true;
                GuardarResultadoTrabajo(connectionString, idTrabajo, exito: true, null);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("ERROR: " + ex.Message);
                System.Console.WriteLine("Stack Trace: " + ex.StackTrace);

                mensajeError = ex.Message;
                detalleError = ex.StackTrace;
                GuardarResultadoTrabajo(connectionString, idTrabajo, exito: false, ex.Message);
            }
            finally
            {
                if (fechaInicioEjecucion.HasValue)
                {
                    GuardarHistorialEjecucion(
                        connectionString,
                        idTrabajo,
                        fechaInicioEjecucion.Value,
                        DateTime.Now,
                        exito,
                        mensajeError,
                        etapasEjecutadas,
                        exito ? null : detalleError,
                        tipoEjecucion);
                }
            }
        }

        /// <summary>
        /// Obtiene los IdTrabajo que deben ejecutarse ahora según TrabajosProgramacion
        /// y que aún no se han ejecutado hoy en su ventana horaria (evita repetir ejecución).
        /// Para usar desde un scheduler: llamar cada X minutos y por cada id ejecutar el flujo del trabajo.
        /// </summary>
        public static IReadOnlyList<int> ObtenerTrabajosPendientesParaScheduler(string connectionString, DateTime? ahora = null)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return new int[0];
            try
            {
                var servicio = new TrabajosProgramacionService(connectionString);
                return servicio.ObtenerTrabajosPendientesDeEjecucion(ahora ?? DateTime.Now);
            }
            catch
            {
                return new int[0];
            }
        }

        /// <summary>
        /// Guarda en la tabla Trabajos el resultado de la última ejecución (éxito o error).
        /// No lanza si falla la actualización (ej. tabla no existe) para no ocultar el error original.
        /// </summary>
        static void GuardarResultadoTrabajo(string connectionString, int idTrabajo, bool exito, string mensajeError)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return;
            try
            {
                var trabajosService = new TrabajosService(connectionString);
                trabajosService.ActualizarResultadoEjecucion(idTrabajo, exito, mensajeError);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[Aviso] No se pudo actualizar resultado en tabla Trabajos: {ex.Message}");
            }
        }

        /// <summary>
        /// Inserta un registro histórico en TrabajosEjecucion (detalle, error, etapas ejecutadas, tipo ejecución).
        /// No lanza si falla para no ocultar el error original.
        /// </summary>
        static void GuardarHistorialEjecucion(
            string connectionString,
            int idTrabajo,
            DateTime fechaHoraInicio,
            DateTime fechaHoraFin,
            bool exito,
            string mensajeError,
            List<string> etapasEjecutadas,
            string detalleEjecucion,
            string tipoEjecucion = "Scheduler")
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                return;
            try
            {
                var servicio = new TrabajosEjecucionService(connectionString);
                servicio.Insertar(idTrabajo, fechaHoraInicio, fechaHoraFin, exito, mensajeError, etapasEjecutadas, detalleEjecucion, tipoEjecucion);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[Aviso] No se pudo guardar historial en TrabajosEjecucion: {ex.Message}");
            }
        }

        /// <summary>
        /// Ejecuta la extracción de archivos desde Aconex usando FileExtractionWorker.
        /// Configura logging, eventos y registra la etapa "FileExtraction".
        /// </summary>
        private static async Task EjecutarExtraccionArchivosAsync(
            TrabajoConfiguracion trabajoConfig,
            Credencial credAconex,
            Credencial credBd,
            List<string> etapasEjecutadas)
        {
            string projectId = trabajoConfig.IdProyecto ?? string.Empty;
            string projectName = !string.IsNullOrWhiteSpace(trabajoConfig.Proyecto) ? trabajoConfig.Proyecto.Trim() : "Proyecto";
            string basePath = !string.IsNullOrWhiteSpace(trabajoConfig.BasePath) ? trabajoConfig.BasePath.Trim() : null;

            System.Console.WriteLine("Configuración desde TrabajosConfiguracion (IdTrabajo=" + trabajoConfig.IdTrabajo + "):");
            System.Console.WriteLine($"  Proyecto={projectName}, IdProyecto={projectId}, BasePath={basePath ?? "(default)"}");
            System.Console.WriteLine($"  Credencial Aconex: {credAconex.Nombre} ({credAconex.Aconex_Instancia})");
            System.Console.WriteLine($"  Credencial BD: {credBd.Nombre}");

            var config = FileExtractionConfig.FromCredencial(credAconex, projectId, basePath);
            var returnFields = trabajoConfig.ToReturnFields();
            if (returnFields != null && returnFields.Count > 0)
                config.ReturnFields = returnFields;

            // Configurar logging
            SigmabotSync.Application.Common.AppState.LogFile = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                $"file_extraction_{DateTime.Now:yyyyMMdd_HHmmss}.log"
            );

            System.Console.WriteLine($"Log file: {SigmabotSync.Application.Common.AppState.LogFile}");
            System.Console.WriteLine();

            // Crear worker
            var worker = new FileExtractionWorker(config);

            // Configurar eventos
            worker.OnProgress += (current, total) =>
            {
                System.Console.WriteLine($"[Progreso] Página {current} de {total} ({(current * 100 / total)}%)");
            };

            worker.OnStatus += (status) =>
            {
                System.Console.WriteLine($"[Estado] {status}");
            };

            System.Console.WriteLine("Iniciando extracción de archivos...");
            System.Console.WriteLine("Presiona Ctrl+C para cancelar");
            System.Console.WriteLine();

            // Ejecutar extracción de archivos (Aconex) — descarga de documentos
            await worker.ProcessAllPagesAsync();
            etapasEjecutadas.Add("FileExtraction");
        }

        /// <summary>
        /// Sincroniza la metadata de documentos en la base de datos indicada por la credencial BD.
        /// Registra la etapa "DocumentExtraction" si se ejecuta.
        /// </summary>
        private static void SincronizarMetadataDocumentos(
            TrabajoConfiguracion trabajoConfig,
            Credencial credAconex,
            Credencial credBd,
            List<string> etapasEjecutadas)
        {
            string projectId = trabajoConfig.IdProyecto ?? string.Empty;
            string projectName = !string.IsNullOrWhiteSpace(trabajoConfig.Proyecto) ? trabajoConfig.Proyecto.Trim() : "Proyecto";
            var documentFieldMappings = trabajoConfig.ToDocumentFieldMappings();

            // Tras descargar archivos, sincronizar metadata de documentos en la BD indicada por la credencial BD
            var connectionStringDocs = credBd.GetConnectionString();
            if (!string.IsNullOrWhiteSpace(connectionStringDocs))
            {
                System.Console.WriteLine();
                System.Console.WriteLine("Sincronizando metadata de documentos en base de datos...");

                var docConfig = ExtractionConfig.FromCredenciales(
                    credAconex,
                    credBd,
                    projectName,
                    documentFieldMappings
                );

                var docWorker = new DocumentExtractionWorker(docConfig.ToDictionary(), connectionStringDocs);
                docWorker.Documentos(projectId);

                System.Console.WriteLine("Sincronización de documentos completada.");
                etapasEjecutadas.Add("DocumentExtraction");
            }
            else
            {
                System.Console.WriteLine("(Credencial BD sin Servidor/BaseDatos: no se ejecuta sincronización de documentos)");
            }
        }

        /// <summary>
        /// Ejecuta ProjectSync: sincronización de documentos modificados (RunAsync de DocumentSyncWorker en Synchronization).
        /// </summary>
        private static async Task EjecutarProjectSyncAsync(
            TrabajoConfiguracion trabajoConfig,
            Credencial credAconex,
            Credencial credBd,
            List<string> etapasEjecutadas)
        {
            string projectId = trabajoConfig.IdProyecto ?? string.Empty;
            // Sincronizar documentos modificados desde hace 1 día (se puede parametrizar después en TrabajosConfiguracion)
            DateTime since = DateTime.UtcNow.AddDays(-1);

            System.Console.WriteLine("Configuración ProjectSync (IdTrabajo=" + trabajoConfig.IdTrabajo + "):");
            System.Console.WriteLine($"  IdProyecto={projectId}, Since={since:yyyy-MM-dd HH:mm:ss} UTC");
            System.Console.WriteLine();

            var client = new AconexDocumentClient(
                credAconex.Aconex_Usuario ?? "",
                credAconex.Aconex_Clave ?? "",
                credAconex.Aconex_IntegrationId ?? "");
            var documentService = new DocumentService(client);
            var syncWorker = new DocumentSyncWorker(documentService);

            syncWorker.OnProgress += (current, total) =>
            {
                System.Console.WriteLine($"[Progreso] Documento {current} de {total}");
            };
            syncWorker.OnStatus += (status) =>
            {
                System.Console.WriteLine($"[Estado] {status}");
            };

            await syncWorker.RunAsync(projectId, since);
            etapasEjecutadas.Add("ProjectSync");
        }

        /// <summary>
        /// Ejecuta FullExtraction: Documentos, ProcessIncidents, Correos y FlujosdeTrabajo (workers de Extraction).
        /// </summary>
        private static async Task EjecutarFullExtractionAsync(
            TrabajoConfiguracion trabajoConfig,
            Credencial credAconex,
            Credencial credBd,
            List<string> etapasEjecutadas)
        {
            string projectId = trabajoConfig.IdProyecto ?? string.Empty;
            string projectName = !string.IsNullOrWhiteSpace(trabajoConfig.Proyecto) ? trabajoConfig.Proyecto.Trim() : "Proyecto";
            var documentFieldMappings = trabajoConfig.ToDocumentFieldMappings();
            var connectionStringDocs = credBd.GetConnectionString();

            if (string.IsNullOrWhiteSpace(connectionStringDocs))
            {
                throw new InvalidOperationException("FullExtraction requiere credencial BD con Servidor/BaseDatos configurado.");
            }

            var docConfig = ExtractionConfig.FromCredenciales(
                credAconex,
                credBd,
                projectName,
                documentFieldMappings);
            var configDict = docConfig.ToDictionary();

            System.Console.WriteLine("FullExtraction: Documentos...");
            var docWorker = new DocumentExtractionWorker(configDict, connectionStringDocs);
            docWorker.Documentos(projectId);
            etapasEjecutadas.Add("Documentos");

            //System.Console.WriteLine("FullExtraction: ProcessIncidents...");
            //var incidentWorker = new IncidentExtractionWorker(configDict, connectionStringDocs);
            //incidentWorker.ProcessIncidents(projectId);
            //etapasEjecutadas.Add("ProcessIncidents");

            System.Console.WriteLine("FullExtraction: Correos...");
            var mailWorker = new MailExtractionWorker(configDict, connectionStringDocs);
            mailWorker.Correos(projectId);
            etapasEjecutadas.Add("Correos");

            System.Console.WriteLine("FullExtraction: FlujosdeTrabajo...");
            var workflowWorker = new WorkflowExtractionWorker(configDict, connectionStringDocs);
            workflowWorker.FlujosdeTrabajo(projectId);
            etapasEjecutadas.Add("FlujosdeTrabajo");

            await Task.CompletedTask;
        }

        /// <summary>
        /// Lee el archivo de configuración (settings.json) y valida que exista una DatabaseConnectionString.
        /// En caso de error, muestra el mensaje y espera una tecla. Devuelve null si no es posible continuar.
        /// </summary>
        private static string ObtenerConnectionStringDesdeSettings()
        {
            var settingsService = new SettingsService();
            var settings = settingsService.Load();

            if (string.IsNullOrWhiteSpace(settings?.DatabaseConnectionString))
            {
                System.Console.WriteLine("ERROR: DatabaseConnectionString no está configurado en settings.json");
                System.Console.WriteLine("Configura la conexión a la base de datos donde están las tablas Credenciales, Trabajos y TrabajosConfiguracion.");
                return null;
            }

            return settings.DatabaseConnectionString.Trim();
        }

        /// <summary>
        /// Obtiene la configuración del trabajo desde la base de datos y valida los datos mínimos requeridos.
        /// Devuelve null si no es posible continuar.
        /// </summary>
        private static TrabajoConfiguracion ObtenerYValidarConfiguracionTrabajo(int idTrabajo, string connectionString)
        {
            var trabajosService = new TrabajosService(connectionString);
            TrabajoConfiguracion trabajoConfig = trabajosService.GetConfiguracionByIdTrabajo(idTrabajo);

            if (trabajoConfig == null)
            {
                System.Console.WriteLine("ERROR: No hay configuración en TrabajosConfiguracion para IdTrabajo=" + idTrabajo + " o el trabajo no está en estado 'Activo' en la tabla Trabajos. Configure IdProyecto y el resto de parámetros en esas tablas.");
                return null;
            }
            if (!trabajoConfig.CredencialAconexId.HasValue)
            {
                System.Console.WriteLine("ERROR: Falta CredencialAconex en TrabajosConfiguracion (Id de la credencial en tabla Credenciales).");
                return null;
            }
            if (!trabajoConfig.CredencialBDId.HasValue)
            {
                System.Console.WriteLine("ERROR: Falta CredencialBD en TrabajosConfiguracion (Id de la credencial en tabla Credenciales).");
                return null;
            }

            return trabajoConfig;
        }

        /// <summary>
        /// Obtiene las credenciales de Aconex y de BD asociadas a la configuración del trabajo y valida que existan.
        /// Devuelve false si no es posible continuar.
        /// </summary>
        private static bool ObtenerYValidarCredenciales(
            TrabajoConfiguracion trabajoConfig,
            string connectionString,
            out Credencial credAconex,
            out Credencial credBd)
        {
            credAconex = null;
            credBd = null;

            var credService = new CredencialesService(connectionString);
            credAconex = credService.GetById(trabajoConfig.CredencialAconexId.Value);
            credBd = credService.GetById(trabajoConfig.CredencialBDId.Value);

            if (credAconex == null)
            {
                System.Console.WriteLine("ERROR: No se encontró Credencial Id=" + trabajoConfig.CredencialAconexId + " en la tabla Credenciales (CredencialAconex).");
                return false;
            }
            if (credBd == null)
            {
                System.Console.WriteLine("ERROR: No se encontró Credencial Id=" + trabajoConfig.CredencialBDId + " en la tabla Credenciales (CredencialBD).");
                return false;
            }

            return true;
        }
    }
}
