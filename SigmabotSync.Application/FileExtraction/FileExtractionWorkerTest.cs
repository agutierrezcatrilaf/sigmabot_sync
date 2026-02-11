using SigmabotSync.Application.Common;
using SigmabotSync.Domain.Config;
using SigmabotSync.Infrastructure.Services;
using System;
using System.Threading.Tasks;

namespace SigmabotSync.Application.FileExtraction
{
    /// <summary>
    /// Clase de prueba para FileExtractionWorker
    /// </summary>
    public static class FileExtractionWorkerTest
    {
        /// <summary>
        /// Método de prueba rápido con credenciales hardcodeadas
        /// </summary>
        public static async Task TestFileExtractionAsync(
            string projectId,
            string orgId,
            string userId)
        {
            // Cargar configuración desde settings.json
            var settingsService = new SettingsService();
            var aconexSettings = settingsService.Load();

            // Validar credenciales
            if (string.IsNullOrWhiteSpace(aconexSettings.UserAconex) ||
                string.IsNullOrWhiteSpace(aconexSettings.PassAconex) ||
                string.IsNullOrWhiteSpace(aconexSettings.IntegrationIdAconex))
            {
                throw new Exception("Las credenciales de Aconex no están configuradas en settings.json");
            }

            // Crear configuración
            var config = FileExtractionConfig.FromAconexSettings(
                aconexSettings,
                projectId,
                orgId,
                userId
            );

            // Configurar ruta base (puedes cambiarla si quieres)
            config.BasePath = @"C:\Users\Andres\AppData\Local\Temp\SigmaBotFileExtractionSalfa\";

            // Configurar logging
            AppState.LogFile = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "file_extraction_test.log"
            );

            // Crear worker
            var worker = new FileExtractionWorker(config);

            // Configurar eventos
            worker.OnProgress += (current, total) =>
            {
                Console.WriteLine($"Progreso: Página {current} de {total}");
            };

            worker.OnStatus += (status) =>
            {
                Console.WriteLine($"Estado: {status}");
            };

            Console.WriteLine($"Iniciando extracción de archivos para proyecto: {projectId}");
            Console.WriteLine($"OrgId: {orgId}, UserId: {userId}");

            try
            {
                await worker.ProcessAllPagesAsync();

                Console.WriteLine("Extracción de archivos completada.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                throw;
            }
            finally
            {
                worker.Dispose();
            }
        }
    }
}
