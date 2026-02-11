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

            const int idTrabajo = 1;

            try
            {
                // Cargar configuración desde settings.json (solo la conexión a la BD donde está la tabla Credenciales)
                var settingsService = new SettingsService();
                var settings = settingsService.Load();

                if (string.IsNullOrWhiteSpace(settings?.DatabaseConnectionString))
                {
                    System.Console.WriteLine("ERROR: DatabaseConnectionString no está configurado en settings.json");
                    System.Console.WriteLine("Configura la conexión a la base de datos donde están las tablas Credenciales y TrabajosConfiguracion.");
                    System.Console.WriteLine("Presiona cualquier tecla para salir...");
                    System.Console.ReadKey();
                    return;
                }

                // Toda la configuración del trabajo viene de TrabajosConfiguracion (IdProyecto, BasePath, CredencialAconex, CredencialBD, CamposConsulta/Response/BD, Proyecto).
                var trabajoConfigService = new TrabajosConfiguracionService(settings.DatabaseConnectionString.Trim());
                TrabajoConfiguracion trabajoConfig = trabajoConfigService.GetByIdTrabajo(idTrabajo);

                if (trabajoConfig == null)
                {
                    System.Console.WriteLine("ERROR: No hay configuración en TrabajosConfiguracion para IdTrabajo=" + idTrabajo + ". Configure IdProyecto y el resto de parámetros en esa tabla.");
                    System.Console.WriteLine("Presiona cualquier tecla para salir...");
                    System.Console.ReadKey();
                    return;
                }
                if (!trabajoConfig.CredencialAconexId.HasValue)
                {
                    System.Console.WriteLine("ERROR: Falta CredencialAconex en TrabajosConfiguracion (Id de la credencial en tabla Credenciales).");
                    System.Console.WriteLine("Presiona cualquier tecla para salir...");
                    System.Console.ReadKey();
                    return;
                }
                if (!trabajoConfig.CredencialBDId.HasValue)
                {
                    System.Console.WriteLine("ERROR: Falta CredencialBD en TrabajosConfiguracion (Id de la credencial en tabla Credenciales).");
                    System.Console.WriteLine("Presiona cualquier tecla para salir...");
                    System.Console.ReadKey();
                    return;
                }

                var credService = new CredencialesService(settings.DatabaseConnectionString.Trim());
                Credencial credAconex = credService.GetById(trabajoConfig.CredencialAconexId.Value);
                Credencial credBd = credService.GetById(trabajoConfig.CredencialBDId.Value);

                if (credAconex == null)
                {
                    System.Console.WriteLine("ERROR: No se encontró Credencial Id=" + trabajoConfig.CredencialAconexId + " en la tabla Credenciales (CredencialAconex).");
                    System.Console.WriteLine("Presiona cualquier tecla para salir...");
                    System.Console.ReadKey();
                    return;
                }
                if (credBd == null)
                {
                    System.Console.WriteLine("ERROR: No se encontró Credencial Id=" + trabajoConfig.CredencialBDId + " en la tabla Credenciales (CredencialBD).");
                    System.Console.WriteLine("Presiona cualquier tecla para salir...");
                    System.Console.ReadKey();
                    return;
                }

                string projectId = trabajoConfig.IdProyecto ?? string.Empty;
                string projectName = !string.IsNullOrWhiteSpace(trabajoConfig.Proyecto) ? trabajoConfig.Proyecto.Trim() : "Proyecto";
                string basePath = !string.IsNullOrWhiteSpace(trabajoConfig.BasePath) ? trabajoConfig.BasePath.Trim() : null;
                List<DocumentFieldMapping> documentFieldMappings = trabajoConfig.ToDocumentFieldMappings();

                System.Console.WriteLine("Configuración desde TrabajosConfiguracion (IdTrabajo=" + idTrabajo + "):");
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
                //await worker.ProcessAllPagesAsync();

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
                }
                else
                {
                    System.Console.WriteLine("(Credencial BD sin Servidor/BaseDatos: no se ejecuta sincronización de documentos)");
                }

                System.Console.WriteLine();
                System.Console.WriteLine("=== Extracción completada exitosamente ===");

                // Registrar resultado exitoso en tabla Trabajos
                GuardarResultadoTrabajo(settings?.DatabaseConnectionString?.Trim(), idTrabajo, exito: true, null);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine();
                System.Console.WriteLine($"ERROR: {ex.Message}");
                System.Console.WriteLine();
                System.Console.WriteLine("Stack Trace:");
                System.Console.WriteLine(ex.StackTrace);

                // Registrar resultado fallido en tabla Trabajos (intenta cargar settings por si falló antes)
                var connStr = new SettingsService().Load()?.DatabaseConnectionString?.Trim();
                GuardarResultadoTrabajo(connStr, idTrabajo, exito: false, ex.Message);
            }
            finally
            {
                System.Console.WriteLine();
                System.Console.WriteLine("Presiona cualquier tecla para salir...");
                System.Console.ReadKey();
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
    }
}
