using Newtonsoft.Json;
using SigmabotSync.Application.Common;
using SigmabotSync.Domain.Config;
using SigmabotSync.Domain.Models.Extraction;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SigmabotSync.Application.FileExtraction
{
    /// <summary>
    /// Worker para extracción de archivos de documentos desde Aconex
    /// </summary>
    public class FileExtractionWorker
    {
        private readonly FileExtractionConfig _config;
        private readonly HttpClient _httpClient;

        public event Action<int, int> OnProgress;
        public event Action<string> OnStatus;

        public FileExtractionWorker(FileExtractionConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(10)
            };

            // Configurar headers
            _httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + _config.AuthorizationHeader);
            _httpClient.DefaultRequestHeaders.Add("X-Application-Key", _config.IntegrationId);
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        }

        /// <summary>
        /// Procesa todas las páginas de documentos
        /// </summary>
        public async Task ProcessAllPagesAsync(BackgroundWorker bgw = null)
        {
            try
            {
                OnStatus?.Invoke("Obteniendo información de páginas...");

                // Obtener primera página para conocer el total
                var firstPage = await GetPageAsync(1);
                
                if (firstPage == null)
                {
                    OnStatus?.Invoke("No se pudo obtener la primera página");
                    return;
                }

                int totalPages = firstPage.totalNumberOfPages;
                long totalDocuments = firstPage.totalResultsCount;

                OnStatus?.Invoke($"Total de documentos: {totalDocuments} en {totalPages} páginas");

                if (totalPages == 0)
                {
                    OnStatus?.Invoke("No hay documentos para procesar");
                    return;
                }

                // Procesar todas las páginas
                int processedPages = 0;
                long processedDocuments = 0;

                for (int page = 1; page <= totalPages; page++)
                {
                    OnStatus?.Invoke($"Procesando página {page} de {totalPages}...");

                    Rootobject pageData = page == 1 
                        ? firstPage 
                        : await GetPageAsync(page);

                    if (pageData != null && pageData.searchResults != null)
                    {
                        processedDocuments += pageData.searchResults.Count;
                        
                        // TODO: Aquí se procesará cada documento para descargar archivos
                        // Por ahora solo contamos los documentos
                        foreach (var doc in pageData.searchResults)
                        {
                            // Procesar documento (descarga de archivo quedará para siguiente etapa)
                            await ProcessDocumentAsync(doc);
                        }

                        processedPages++;
                    }

                    // Reportar progreso
                    int progress = (int)((page * 100) / totalPages);
                    OnProgress?.Invoke(page, totalPages);
                    bgw?.ReportProgress(progress, $"Procesando página {page} de {totalPages} ({processedDocuments} documentos)");
                }

                OnStatus?.Invoke($"Proceso completado: {processedPages} páginas, {processedDocuments} documentos procesados");
            }
            catch (Exception ex)
            {
                Utilities.Wlog($"FileExtraction: ERROR en ProcessAllPagesAsync: {ex.Message}", 0);
                OnStatus?.Invoke($"Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Obtiene una página específica de documentos
        /// </summary>
        private async Task<Rootobject> GetPageAsync(int pageNumber)
        {
            string uri = $"https://us1.aconex.com/api/projects/{_config.ProjectId}/register/search";

            var requestBody = new
            {
                orgId = _config.OrgId,
                userId = _config.UserId,
                returnFields = _config.ReturnFields,
                resultSize = _config.ResultSize.ToString(),
                showDocHistory = "true",
                pageNumber = pageNumber.ToString()
            };

            string jsonBody = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            try
            {
                var response = await Utilities.EjecutarConReintentosAsync(
                    async () => await _httpClient.PostAsync(uri, content),
                    $"FileExtraction: Error al obtener página {pageNumber}"
                );

                response.EnsureSuccessStatusCode();

                string responseString = await response.Content.ReadAsStringAsync();
                responseString = responseString.Replace("\u0003", ""); // Limpiar caracteres especiales

                return JsonConvert.DeserializeObject<Rootobject>(responseString);
            }
            catch (Exception ex)
            {
                Utilities.Wlog($"FileExtraction: ERROR en GetPageAsync página {pageNumber}: {ex.Message}", 0);
                throw;
            }
        }

        /// <summary>
        /// Procesa un documento individual y descarga su archivo
        /// </summary>
        private async Task ProcessDocumentAsync(Searchresult document)
        {
            try
            {
                Utilities.Wlog($"FileExtraction: Procesando documento ID={document.Id}, DocNo={document.DocumentNumber}, Title={document.Title}, Version={document.VersionNumber}", 1);
                
                // Descargar el archivo del documento
                await DownloadDocumentFileAsync(document);
            }
            catch (Exception ex)
            {
                Utilities.Wlog($"FileExtraction: ERROR procesando documento {document.Id}: {ex.Message}", 0);
            }
        }

        /// <summary>
        /// Descarga el archivo de un documento desde Aconex
        /// </summary>
        private async Task DownloadDocumentFileAsync(Searchresult document)
        {
            try
            {
                string documentId = document.Id.ToString();
                string version = document.VersionNumber.ToString();
                string documentNumber = document.DocumentNumber.ToString();


                // Construir ruta de destino: origen/{projectID}/{documentID}/{version}/
                string documentPath = Path.Combine(
                    _config.BasePath,
                    _config.ProjectId,
                    documentNumber,
                    version
                );

                // Crear directorio si no existe
                Directory.CreateDirectory(documentPath);

                // Construir URL del endpoint
                string downloadUrl = $"https://us1.aconex.com/api/projects/{_config.ProjectId}/register/{documentId}";

                // Crear un HttpClient separado para la descarga (sin X-Application-Key)
                // El endpoint de descarga no requiere ese header según el curl
                using (var downloadClient = new HttpClient())
                {
                    downloadClient.Timeout = TimeSpan.FromMinutes(10);
                    
                    // Crear request para descargar
                    var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
                    request.Headers.Add("Authorization", "Basic " + _config.AuthorizationHeader);

                    // Ejecutar descarga con reintentos
                    var response = await Utilities.EjecutarConReintentosAsync(
                        async () => await downloadClient.SendAsync(request),
                        $"FileExtraction: Error al descargar documento {documentId}"
                    );

                    response.EnsureSuccessStatusCode();

                    // Obtener nombre del archivo desde el documento o usar un nombre por defecto
                    string fileName = !string.IsNullOrWhiteSpace(document.Filename) 
                        ? document.Filename 
                        : $"document_{documentId}_v{version}.pdf";

                    // Limpiar nombre de archivo de caracteres inválidos
                    fileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));

                    string filePath = Path.Combine(documentPath, fileName);

                    // Guardar archivo
                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var contentStream = await response.Content.ReadAsStreamAsync();
                        await contentStream.CopyToAsync(fileStream);
                    }

                    Utilities.Wlog($"FileExtraction: Archivo descargado: {filePath}", 1);
                    OnStatus?.Invoke($"Archivo descargado: {document.DocumentNumber} v{version}");
                }
            }
            catch (Exception ex)
            {
                Utilities.Wlog($"FileExtraction: ERROR descargando archivo del documento {document.Id}: {ex.Message}", 0);
                throw;
            }
        }

        /// <summary>
        /// Libera recursos
        /// </summary>
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
