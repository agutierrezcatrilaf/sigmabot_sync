using SigmabotSync.Application.Common;
using SigmabotSync.Domain.Config;
using SigmabotSync.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace SigmabotSync.Application.Extraction
{
    /// <summary>
    /// Clase de prueba para ejecutar DocumentExtractionWorker
    /// Permite depurar con puntos de interrupción
    /// </summary>
    public static class DocumentExtractionWorkerTest
    {
        /// <summary>
        /// Método de prueba para ejecutar la extracción de documentos
        /// </summary>
        /// <param name="projectId">ID del proyecto de Aconex</param>
        /// <param name="connectionString">Cadena de conexión a la base de datos</param>
        public static async Task TestExtractDocumentsAsync(string projectId, string connectionString)
        {
            // Cargar configuración desde settings.json
            var settingsService = new SettingsService();
            var aconexSettings = settingsService.Load();

            // Validar que existan las credenciales
            if (string.IsNullOrWhiteSpace(aconexSettings.UserAconex) ||
                string.IsNullOrWhiteSpace(aconexSettings.PassAconex) ||
                string.IsNullOrWhiteSpace(aconexSettings.IntegrationIdAconex))
            {
                throw new Exception("Las credenciales de Aconex no están configuradas en settings.json");
            }

            // Crear configuración para el worker
            var config = new Dictionary<string, string>
            {
                { "ACXUser", aconexSettings.UserAconex },
                { "ACXPass", aconexSettings.PassAconex },
                { "IntegrationIdAconex", aconexSettings.IntegrationIdAconex },
                { "FieldIntegrationId", aconexSettings.IntegrationIdAconex }, // Por defecto mismo
                { "NombrePrj", "Proyecto de Prueba" },
                { "OrgId", "" }, // TODO: Obtener de BD
                { "userid", "" }  // TODO: Obtener de BD
            };

            // Configurar AppState para logging
            AppState.LogFile = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "extraction_test.log"
            );

            // Crear el worker
            var worker = new DocumentSyncWorker(config, connectionString);

            // Crear un BackgroundWorker simulado para el progreso
            var bgw = new BackgroundWorker
            {
                WorkerReportsProgress = true
            };

            bgw.ProgressChanged += (sender, e) =>
            {
                Console.WriteLine($"Progreso: {e.ProgressPercentage}% - {e.UserState}");
            };

            // PUNTO DE INTERRUPCIÓN AQUÍ - Puedes poner un breakpoint en la siguiente línea
            Console.WriteLine($"Iniciando extracción para proyecto: {projectId}");
            Console.WriteLine($"Connection String: {connectionString.Substring(0, Math.Min(50, connectionString.Length))}...");

            // Ejecutar el proceso completo
            worker.Documentos(bgw, projectId);

            Console.WriteLine("Extracción completada.");
        }

        /// <summary>
        /// Método de prueba rápido con credenciales hardcodeadas (solo para desarrollo)
        /// </summary>
        public static async Task TestGetFirstPageQuickAsync(string projectId)
        {
            // Credenciales de Aconex (temporal para pruebas)
            var config = new Dictionary<string, string>
            {
                { "ACXUser", "victorfidel" },
                { "ACXPass", "Galatea2025#" },
                { "IntegrationIdAconex", "00000198-fb8b-cf34-415b-30a5a5581000" },
                { "FieldIntegrationId", "00000198-fb8b-cf34-415b-30a5a5581000" },
                { "NombrePrj", "Proyecto de Prueba" },
                { "OrgId", "" },
                { "userid", "" }
            };

            // Connection string para BD local
            string connectionString = "Server=LAPTOP-0R7RVBU2;Database=SigmatecGlencore;User Id=sigmatec;Password=sigmatec2025;";

            // Configurar logging
            AppState.LogFile = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "extraction_test.log"
            );

            // Crear worker
            var worker = new DocumentSyncWorker(config, connectionString);

            // Crear BackgroundWorker
            var bgw = new BackgroundWorker
            {
                WorkerReportsProgress = true
            };

            bgw.ProgressChanged += (sender, e) =>
            {
                Console.WriteLine($"Progreso: {e.ProgressPercentage}% - {e.UserState}");
            };

            // PUNTO DE INTERRUPCIÓN AQUÍ - Pon un breakpoint en la siguiente línea
            Console.WriteLine($"Obteniendo primera página para proyecto: {projectId}");

            // Obtener código de autenticación
            string authcode = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes(
                    config["ACXUser"] + ":" + config["ACXPass"]
                )
            );

            // Llamar directamente al método que obtiene documentos
            // PUNTO DE INTERRUPCIÓN: Pon un breakpoint aquí para depurar la primera página
            var result = await worker.GetDocumentsAllAsync(projectId, authcode, bgw);

            Console.WriteLine($"Resultado: {(result ? "Éxito" : "Falló")}");
            Console.WriteLine($"Total documentos en Aconex: {AppState.TotDoctosAconex}");
        }

        /// <summary>
        /// Método de prueba para obtener solo la primera página de documentos
        /// </summary>
        public static async Task TestGetFirstPageAsync(string projectId, string connectionString)
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
            var config = new Dictionary<string, string>
            {
                { "ACXUser", aconexSettings.UserAconex },
                { "ACXPass", aconexSettings.PassAconex },
                { "IntegrationIdAconex", aconexSettings.IntegrationIdAconex },
                { "FieldIntegrationId", aconexSettings.IntegrationIdAconex },
                { "NombrePrj", "Proyecto de Prueba" },
                { "OrgId", "" },
                { "userid", "" }
            };

            // Configurar logging
            AppState.LogFile = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "extraction_test.log"
            );

            // Crear worker
            var worker = new DocumentSyncWorker(config, connectionString);

            // Crear BackgroundWorker
            var bgw = new BackgroundWorker
            {
                WorkerReportsProgress = true
            };

            bgw.ProgressChanged += (sender, e) =>
            {
                Console.WriteLine($"Progreso: {e.ProgressPercentage}% - {e.UserState}");
            };

            // PUNTO DE INTERRUPCIÓN AQUÍ - Pon un breakpoint en la siguiente línea
            Console.WriteLine($"Obteniendo primera página para proyecto: {projectId}");

            // Obtener código de autenticación
            string authcode = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes(
                    aconexSettings.UserAconex + ":" + aconexSettings.PassAconex
                )
            );

            // Llamar directamente al método que obtiene documentos
            // PUNTO DE INTERRUPCIÓN: Pon un breakpoint aquí para depurar la primera página
            var result = await worker.GetDocumentsAllAsync(projectId, authcode, bgw);

            Console.WriteLine($"Resultado: {(result ? "Éxito" : "Falló")}");
            Console.WriteLine($"Total documentos en Aconex: {AppState.TotDoctosAconex}");
        }
    }
}
