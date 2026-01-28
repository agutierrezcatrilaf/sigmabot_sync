using SigmabotSync.Application.FileExtraction;
using SigmabotSync.Domain.Config;
using SigmabotSync.Infrastructure.Services;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace SigmabotSync.Console
{
    class Program
    {
        static async Task Main(string[] args)
        {
            System.Console.WriteLine("=== SigmaBot File Extraction Console ===");
            System.Console.WriteLine();

            try
            {
                // Cargar configuración desde settings.json
                var settingsService = new SettingsService();
                var aconexSettings = settingsService.Load();

                // Validar ExtractionFiles
                if (aconexSettings?.ExtractionFiles == null)
                {
                    System.Console.WriteLine("ERROR: ExtractionFiles no está configurado en settings.json");
                    System.Console.WriteLine("Por favor agrega la sección ExtractionFiles con todas las configuraciones necesarias");
                    System.Console.WriteLine("Presiona cualquier tecla para salir...");
                    System.Console.ReadKey();
                    return;
                }

                var extractionConfig = aconexSettings.ExtractionFiles;

                // Validar credenciales dentro de ExtractionFiles
                if (string.IsNullOrWhiteSpace(extractionConfig.UserAconex) ||
                    string.IsNullOrWhiteSpace(extractionConfig.PassAconex) ||
                    string.IsNullOrWhiteSpace(extractionConfig.IntegrationIdAconex))
                {
                    System.Console.WriteLine("ERROR: Las credenciales de Aconex no están configuradas en ExtractionFiles");
                    System.Console.WriteLine("Por favor configura UserAconex, PassAconex e IntegrationIdAconex dentro de ExtractionFiles");
                    System.Console.WriteLine("Presiona cualquier tecla para salir...");
                    System.Console.ReadKey();
                    return;
                }

                // Validar parámetros del proyecto
                if (string.IsNullOrWhiteSpace(extractionConfig.ProjectId) ||
                    string.IsNullOrWhiteSpace(extractionConfig.OrgId) ||
                    string.IsNullOrWhiteSpace(extractionConfig.UserId))
                {
                    System.Console.WriteLine("ERROR: ProjectId, OrgId y UserId son requeridos en ExtractionFiles");
                    System.Console.WriteLine("Presiona cualquier tecla para salir...");
                    System.Console.ReadKey();
                    return;
                }

                System.Console.WriteLine("Configuración cargada desde settings.json (ExtractionFiles):");
                System.Console.WriteLine($"  Project ID: {extractionConfig.ProjectId}");
                System.Console.WriteLine($"  Org ID: {extractionConfig.OrgId}");
                System.Console.WriteLine($"  User ID: {extractionConfig.UserId}");
                System.Console.WriteLine($"  Base Path: {extractionConfig.BasePath}");
                System.Console.WriteLine();

                // Crear configuración desde settings
                var config = FileExtractionConfig.FromAconexSettings(aconexSettings);

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

                // Crear BackgroundWorker para compatibilidad
                var bgw = new BackgroundWorker
                {
                    WorkerReportsProgress = true
                };

                bgw.ProgressChanged += (sender, e) =>
                {
                    // El progreso ya se maneja en OnProgress
                };

                System.Console.WriteLine("Iniciando extracción de archivos...");
                System.Console.WriteLine("Presiona Ctrl+C para cancelar");
                System.Console.WriteLine();

                // PUNTO DE INTERRUPCIÓN AQUÍ - Pon un breakpoint en la siguiente línea
                await worker.ProcessAllPagesAsync(bgw);

                System.Console.WriteLine();
                System.Console.WriteLine("=== Extracción completada exitosamente ===");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine();
                System.Console.WriteLine($"ERROR: {ex.Message}");
                System.Console.WriteLine();
                System.Console.WriteLine("Stack Trace:");
                System.Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                System.Console.WriteLine();
                System.Console.WriteLine("Presiona cualquier tecla para salir...");
                System.Console.ReadKey();
            }
        }
    }
}
