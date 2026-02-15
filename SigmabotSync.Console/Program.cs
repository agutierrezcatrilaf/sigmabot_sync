using SigmabotSync.Application.Extraction;
using SigmabotSync.Application.FileExtraction;
using SigmabotSync.Domain.Config;
using SigmabotSync.Domain.Entities;
using SigmabotSync.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SigmabotSync.Console
{
    class Program
    {
        static async Task Main(string[] args)
        {
            System.Console.WriteLine("=== SigmaBot File Extraction Console ===");
            System.Console.WriteLine();

            string connectionString = ObtenerConnectionStringDesdeSettings();
            if (connectionString == null)
                return;

            int? idManual = ObtenerIdTrabajoManual(args);
            if (idManual.HasValue)
            {
                System.Console.WriteLine("Modo manual: ejecutando trabajo Id=" + idManual.Value);
                System.Console.WriteLine();
                await EjecutarUnTrabajoAsync(connectionString, idManual.Value, "Manual");
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
                System.Console.WriteLine("No hay trabajos pendientes para el scheduler. Para ejecutar un trabajo manualmente use: SigmabotSync.Console.exe --manual <IdTrabajo>");
            }
        }

        /// <summary>
        /// Parsea los argumentos y devuelve el IdTrabajo si se solicitó ejecución manual.
        /// Formas: --manual 2, -m 2, o solo 2 (un único número).
        /// </summary>
        static int? ObtenerIdTrabajoManual(string[] args)
        {
            if (args == null || args.Length == 0)
                return null;
            for (int i = 0; i < args.Length; i++)
            {
                var arg = (args[i] ?? "").Trim();
                if (arg == "--manual" || arg == "-m")
                {
                    if (i + 1 < args.Length && int.TryParse(args[i + 1].Trim(), out int id) && id > 0)
                        return id;
                    return null;
                }
                if (args.Length == 1 && int.TryParse(arg, out int idUnico) && idUnico > 0)
                    return idUnico;
            }
            return null;
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

                await EjecutarExtraccionArchivosAsync(trabajoConfig, credAconex, credBd, etapasEjecutadas);
                SincronizarMetadataDocumentos(trabajoConfig, credAconex, credBd, etapasEjecutadas);

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

                var docWorker = new DocumentSyncWorker(docConfig.ToDictionary(), connectionStringDocs);
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
