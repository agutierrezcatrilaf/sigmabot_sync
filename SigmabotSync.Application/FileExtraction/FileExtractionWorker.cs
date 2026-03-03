using Newtonsoft.Json;
using SigmabotSync.Application.Common;
using SigmabotSync.Domain.Config;
using SigmabotSync.Domain.Models.Extraction;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SigmabotSync.Application.FileExtraction
{
    /// <summary>
    /// Worker para extracción de archivos de documentos desde Aconex
    /// </summary>
    public class FileExtractionWorker
    {
        private const int MaxConcurrentDownloads = 6;

        // Campos mínimos que FileExtraction necesita siempre para funcionar,
        // independientemente de lo que venga en TrabajosConfiguracion.CamposConsulta.
        private static readonly string[] RequiredReturnFields = new[]
        {
            "docno",
            "filename",
            "trackingid",
            "versionnumber"
        };

        private readonly FileExtractionConfig _config;
        private readonly HttpClient _httpClient;
        private readonly HttpClient _downloadClient;

        private int _countSaved;
        private int _countOmittedNoDocument;  // sin filename o documento vacio (CANNOT_DOWNLOAD_EMPTY_DOCUMENT)
        private int _countOmittedAlreadyExists;
        private int _countErrors;

        public event Action<int, int> OnProgress;
        public event Action<string> OnStatus;

        private enum FileDownloadResult { Saved, Omitted, Error }

        public FileExtractionWorker(FileExtractionConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(10)
            };

            // Mismo esquema que DocumentExtractionWorker (solo Authorization; sin X-Application-Key) para que el search no devuelva 401
            _httpClient.DefaultRequestHeaders.Add("Authorization", "Basic " + _config.AuthorizationHeader);
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            // Cliente compartido para descargas (reutilizable, evita agotar sockets)
            var downloadHandler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate };
            _downloadClient = new HttpClient(downloadHandler)
            {
                Timeout = TimeSpan.FromMinutes(10)
            };
            _downloadClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", _config.AuthorizationHeader);
            _downloadClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, sdch");
        }

        /// <summary>
        /// Procesa todas las páginas de documentos
        /// </summary>
        public async Task ProcessAllPagesAsync()
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

                _countSaved = 0;
                _countOmittedNoDocument = 0;
                _countOmittedAlreadyExists = 0;
                _countErrors = 0;

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

                        // Descargas en paralelo con límite de concurrencia para no saturar Aconex
                        var semaphore = new SemaphoreSlim(MaxConcurrentDownloads);
                        var downloadTasks = pageData.searchResults.Select(async doc =>
                        {
                            await semaphore.WaitAsync();
                            try
                            {
                                await ProcessDocumentAsync(doc);
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        });
                        await Task.WhenAll(downloadTasks);

                        processedPages++;
                    }

                    int progress = (int)((page * 100) / totalPages);
                    OnProgress?.Invoke(page, totalPages);
                }

                OnStatus?.Invoke($"Proceso completado: {processedPages} páginas, {processedDocuments} documentos procesados");
                int totalOmitted = _countOmittedNoDocument + _countOmittedAlreadyExists;
                Utilities.Wlog($"FileExtraction resumen: Total procesados={processedDocuments}, Guardados={_countSaved}, Omitidos={totalOmitted} (sin documento/archivo={_countOmittedNoDocument}, ya existían={_countOmittedAlreadyExists}), Errores={_countErrors}", 0);
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
            string baseUrl = string.IsNullOrWhiteSpace(_config.AconexBaseUrl) ? "https://us1.aconex.com" : _config.AconexBaseUrl.TrimEnd('/');
            string uri = $"{baseUrl}/api/projects/{_config.ProjectId}/register/search";

            // Respetar CamposConsulta de TrabajosConfiguracion (_config.ReturnFields),
            // pero garantizando siempre los mínimos que FileExtraction necesita.
            var fields = _config.ReturnFields ?? new List<string>();
            foreach (var campo in RequiredReturnFields)
            {
                if (!fields.Contains(campo, StringComparer.OrdinalIgnoreCase))
                    fields.Add(campo);
            }

            var requestBody = new
            {
                orgId = _config.OrgId,
                userId = _config.UserId,
                returnFields = fields,
                resultSize = _config.ResultSize.ToString(),
                showDocHistory = "true",
                pageNumber = pageNumber.ToString()
            };

            string jsonBody = JsonConvert.SerializeObject(requestBody);
            try
            {
                using (var content = new StringContent(jsonBody, Encoding.UTF8, "application/json"))
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
            }
            catch (Exception ex)
            {
                Utilities.Wlog($"FileExtraction: ERROR en GetPageAsync página {pageNumber}: {ex.Message}", 0);
                throw;
            }
        }

        /// <summary>
        /// Procesa un documento individual y descarga su archivo. Devuelve el resultado para el resumen.
        /// </summary>
        private async Task<FileDownloadResult> ProcessDocumentAsync(Searchresult document)
        {
            try
            {
                return await DownloadDocumentFileAsync(document);
            }
            catch (Exception ex)
            {
                Utilities.Wlog($"FileExtraction: ERROR procesando documento {document.Id}: {ex.Message}", 0);
                Interlocked.Increment(ref _countErrors);
                return FileDownloadResult.Error;
            }
        }

        /// <summary>
        /// Descarga el archivo de un documento desde Aconex. Devuelve Saved, Omitted o Error (errores se registran en log).
        /// </summary>
        private async Task<FileDownloadResult> DownloadDocumentFileAsync(Searchresult document)
        {
            try
            {
                string documentId = document.Id.ToString();
                string version = document.GetDynamicValue("versionNumber") ?? "0";
                string documentNumber = document.DocumentNumber ?? "";

                string filenameFromMeta = document.GetDynamicValue("filename")?.ToString();
                if (string.IsNullOrWhiteSpace(filenameFromMeta))
                {
                    Interlocked.Increment(ref _countOmittedNoDocument);
                    return FileDownloadResult.Omitted;
                }

                string folderName = documentNumber;
                if (!string.IsNullOrEmpty(folderName) && folderName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    folderName = folderName.Substring(0, folderName.Length - 4);

                string documentPath = Path.Combine(
                    _config.BasePath,
                    _config.ProjectId,
                    folderName,
                    version
                );

                string fileName = string.Join("_", filenameFromMeta.Split(Path.GetInvalidFileNameChars()));
                string filePath = Path.Combine(documentPath, fileName);

                if (File.Exists(filePath))
                {
                    Interlocked.Increment(ref _countOmittedAlreadyExists);
                    return FileDownloadResult.Omitted;
                }

                Directory.CreateDirectory(documentPath);

                string baseUrl = string.IsNullOrWhiteSpace(_config.AconexBaseUrl) ? "https://us1.aconex.com" : _config.AconexBaseUrl.TrimEnd('/');
                string downloadUrl = $"{baseUrl}/api/projects/{_config.ProjectId}/register/{documentId}";


                using (var response = await _downloadClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (response.StatusCode == HttpStatusCode.BadRequest)
                    {
                        var errorBody = await response.Content.ReadAsStringAsync();
                        if (errorBody != null && errorBody.IndexOf("CANNOT_DOWNLOAD_EMPTY_DOCUMENT", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Interlocked.Increment(ref _countOmittedNoDocument);
                            return FileDownloadResult.Omitted;
                        }
                    }

                    response.EnsureSuccessStatusCode();

                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var contentStream = await response.Content.ReadAsStreamAsync();
                        await contentStream.CopyToAsync(fileStream);
                    }
                }

                Interlocked.Increment(ref _countSaved);
                return FileDownloadResult.Saved;
            }
            catch (OperationCanceledException)
            {
                // Incluye TaskCanceledException (timeout de HttpClient o CancellationToken).
                Utilities.Wlog($"FileExtraction: Timeout o cancelación al descargar documento {document.Id} (DocNo={document.DocumentNumber}). Se omite y se continúa.", 0);
                Interlocked.Increment(ref _countErrors);
                return FileDownloadResult.Error;
            }
            catch (Exception ex)
            {
                Utilities.Wlog($"FileExtraction: ERROR descargando archivo del documento {document.Id}: {ex.Message}", 0);
                Interlocked.Increment(ref _countErrors);
                return FileDownloadResult.Error;
            }
        }

        /// <summary>
        /// Libera recursos
        /// </summary>
        public void Dispose()
        {
            _httpClient?.Dispose();
            _downloadClient?.Dispose();
        }
    }
}
