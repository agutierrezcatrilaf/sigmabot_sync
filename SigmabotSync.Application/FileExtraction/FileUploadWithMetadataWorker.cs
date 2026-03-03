using SigmabotSync.Application.Common;
using SigmabotSync.Domain.Config;
using SigmabotSync.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace SigmabotSync.Application.FileExtraction
{
    /// <summary>
    /// Worker para el tipo de trabajo FileUploadWithMetadata: lee la tabla de metadata desde la BD (CredencialBD),
    /// enlaza archivos en BasePath por docno (número de documento) y envía archivo + metadata a Aconex.
    /// </summary>
    public class FileUploadWithMetadataWorker
    {
        private readonly TrabajoConfiguracion _trabajoConfig;
        private readonly Credencial _credAconex;
        private readonly Credencial _credBd;
        private readonly FileExtractionConfig _aconexConfig;

        public event Action<int, int> OnProgress;
        public event Action<string> OnStatus;

        public FileUploadWithMetadataWorker(
            TrabajoConfiguracion trabajoConfig,
            Credencial credAconex,
            Credencial credBd)
        {
            _trabajoConfig = trabajoConfig ?? throw new ArgumentNullException(nameof(trabajoConfig));
            _credAconex = credAconex ?? throw new ArgumentNullException(nameof(credAconex));
            _credBd = credBd ?? throw new ArgumentNullException(nameof(credBd));
            _aconexConfig = FileExtractionConfig.FromCredencial(credAconex, trabajoConfig.IdProyecto ?? "", null);
        }

        /// <summary>
        /// Ejecuta el proceso: lee metadata de la tabla, enlaza archivos por docno y envía a Aconex.
        /// </summary>
        public async Task RunAsync()
        {
            string connectionStringBd = _credBd.GetConnectionString();
            if (string.IsNullOrWhiteSpace(connectionStringBd))
            {
                throw new InvalidOperationException("FileUploadWithMetadata requiere CredencialBD con Servidor y BaseDatos configurados.");
            }

            string tablaMetadata = ( _trabajoConfig.TablaMetadata ?? "" ).Trim();
            if (string.IsNullOrEmpty(tablaMetadata))
            {
                throw new InvalidOperationException("FileUploadWithMetadata requiere TablaMetadata en TrabajosConfiguracion.");
            }

            string basePath = ( _trabajoConfig.BasePath ?? "" ).Trim();
            if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
            {
                throw new InvalidOperationException("FileUploadWithMetadata requiere BasePath válido en TrabajosConfiguracion. Ruta: " + ( basePath ?? "" ));
            }

            OnStatus?.Invoke("Leyendo tabla de metadata...");
            DataTable metadata = LeerTablaMetadata(connectionStringBd, tablaMetadata);
            if (metadata == null || metadata.Rows.Count == 0)
            {
                OnStatus?.Invoke("No hay registros en la tabla de metadata.");
                return;
            }

            string columnaDocno = ResolverColumnaDocno(metadata);
            if (columnaDocno == null)
            {
                throw new InvalidOperationException("La tabla de metadata debe tener una columna docno o DocumentNumber. Columnas encontradas: " + string.Join(", ", metadata.Columns.Cast<DataColumn>().Select(c => c.ColumnName)));
            }

            int total = metadata.Rows.Count;
            int procesados = 0;
            int enviados = 0;
            int errores = 0;

            OnStatus?.Invoke($"Procesando {total} registro(s) de metadata...");

            for (int i = 0; i < metadata.Rows.Count; i++)
            {
                DataRow row = metadata.Rows[i];
                object docnoObj = row[columnaDocno];
                string docno = docnoObj?.ToString().Trim();
                if (string.IsNullOrEmpty(docno))
                {
                    Utilities.Wlog($"FileUploadWithMetadata: Fila {i + 1} sin docno, se omite.", 1);
                    procesados++;
                    OnProgress?.Invoke(procesados, total);
                    continue;
                }

                string filePath = ResolverRutaArchivo(basePath, docno);
                if (string.IsNullOrEmpty(filePath))
                {
                    Utilities.Wlog($"FileUploadWithMetadata: No se encontró archivo para docno={docno} en BasePath.", 1);
                    errores++;
                    procesados++;
                    OnProgress?.Invoke(procesados, total);
                    continue;
                }

                try
                {
                    await EnviarDocumentoAconexAsync(filePath, row, metadata.Columns);
                    enviados++;
                    OnStatus?.Invoke($"Enviado: {docno}");
                }
                catch (Exception ex)
                {
                    Utilities.Wlog($"FileUploadWithMetadata: Error enviando docno={docno}: {ex.Message}", 0);
                    errores++;
                }

                procesados++;
                OnProgress?.Invoke(procesados, total);
            }

            OnStatus?.Invoke($"Completado: {enviados} enviados, {errores} errores, {total} procesados.");
        }

        /// <summary>
        /// Lee la tabla de metadata desde la BD indicada por la credencial.
        /// </summary>
        private DataTable LeerTablaMetadata(string connectionString, string nombreTabla)
        {
            // Nombre de tabla con identificador entre corchetes para SQL Server
            string tablaEscapada = "[" + nombreTabla.Replace("]", "]]") + "]";
            string sql = "SELECT * FROM " + tablaEscapada;

            var dt = new DataTable();
            using (var cn = new SqlConnection(connectionString))
            {
                cn.Open();
                using (var cmd = new SqlCommand(sql, cn))
                using (var adapter = new SqlDataAdapter(cmd))
                {
                    adapter.Fill(dt);
                }
            }
            return dt;
        }

        /// <summary>
        /// Obtiene el nombre de la columna que contiene el número de documento (docno), case-insensitive.
        /// </summary>
        private static string ResolverColumnaDocno(DataTable metadata)
        {
            foreach (DataColumn c in metadata.Columns)
            {
                if (string.Equals(c.ColumnName, "docno", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.ColumnName, "DocumentNumber", StringComparison.OrdinalIgnoreCase))
                    return c.ColumnName;
            }
            return null;
        }

        /// <summary>
        /// Busca en basePath un archivo cuyo nombre (sin extensión) o nombre completo coincida con docno.
        /// </summary>
        private static string ResolverRutaArchivo(string basePath, string docno)
        {
            if (!Directory.Exists(basePath)) return null;
            string pathExacto = Path.Combine(basePath, docno);
            if (File.Exists(pathExacto)) return pathExacto;
            var files = Directory.GetFiles(basePath);
            string docnoLower = docno.ToLowerInvariant();
            foreach (string f in files)
            {
                string fileName = Path.GetFileName(f);
                string sinExtension = Path.GetFileNameWithoutExtension(f);
                if (string.Equals(fileName, docno, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(sinExtension, docno, StringComparison.OrdinalIgnoreCase))
                    return f;
            }
            return null;
        }

        /// <summary>
        /// Construye el body con la metadata (columnas de la fila) y el archivo en base64. Listo para serializar a JSON y enviar.
        /// </summary>
        /// <param name="filePath">Ruta física del archivo.</param>
        /// <param name="metadataRow">Fila de metadata.</param>
        /// <param name="columnas">Columnas de la tabla de metadata.</param>
        /// <returns>Objeto con Metadata (diccionario nombre columna -> valor) y FileBase64, FileName.</returns>
        private static FileUploadWithMetadataBody BuildBodyWithMetadataAndFileBase64(string filePath, DataRow metadataRow, DataColumnCollection columnas)
        {
            var metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DataColumn col in columnas)
            {
                object val = metadataRow[col];
                if (val == null || val == DBNull.Value)
                {
                    metadata[col.ColumnName] = null;
                    continue;
                }
                if (val is DateTime dt)
                {
                    metadata[col.ColumnName] = dt.ToString("o");
                    continue;
                }
                if (val is byte[] bytes)
                {
                    metadata[col.ColumnName] = Convert.ToBase64String(bytes);
                    continue;
                }
                metadata[col.ColumnName] = val;
            }

            byte[] fileBytes = File.ReadAllBytes(filePath);
            string fileBase64 = Convert.ToBase64String(fileBytes);
            string fileName = Path.GetFileName(filePath);

            return new FileUploadWithMetadataBody
            {
                Metadata = metadata,
                FileBase64 = fileBase64,
                FileName = fileName
            };
        }

        /// <summary>
        /// Envía el archivo y la metadata a Aconex mediante el API Register Document (multipart/mixed: XML + archivo base64).
        /// Ver: https://help.aconex.com/apis/api-guide-documents/#Register-Document
        /// </summary>
        private async Task EnviarDocumentoAconexAsync(string filePath, DataRow metadataRow, DataColumnCollection columnas)
        {
            FileUploadWithMetadataBody body = BuildBodyWithMetadataAndFileBase64(filePath, metadataRow, columnas);
            string projectId = _trabajoConfig.IdProyecto ?? _aconexConfig.ProjectId ?? "";
            if (string.IsNullOrWhiteSpace(projectId))
                throw new InvalidOperationException("IdProyecto es requerido para Register Document.");

            string xmlDocument = BuildAconexRegisterXml(metadataRow, columnas);
            const string boundary = "myboundary";
            string multipartBody = BuildMultipartRegisterBody(xmlDocument, body.FileName, body.FileBase64, boundary);

            string baseUrl = string.IsNullOrWhiteSpace(_aconexConfig.AconexBaseUrl) ? "https://us1.aconex.com" : _aconexConfig.AconexBaseUrl.TrimEnd('/');
            string registerUrl = baseUrl + "/api/projects/" + projectId + "/register";

            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromMinutes(10);
                client.DefaultRequestHeaders.Add("Authorization", "Basic " + _aconexConfig.AuthorizationHeader);
                if (!string.IsNullOrEmpty(_aconexConfig.IntegrationId))
                    client.DefaultRequestHeaders.Add("X-Application-Key", _aconexConfig.IntegrationId);
                var content = new StringContent(multipartBody, Encoding.UTF8);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("multipart/mixed");
                content.Headers.ContentType.Parameters.Add(new System.Net.Http.Headers.NameValueHeaderValue("boundary", "\"" + boundary + "\""));

                HttpResponseMessage response = await client.PostAsync(registerUrl, content);
                string responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Utilities.Wlog($"FileUploadWithMetadata: Register Document falló. Status={response.StatusCode}, Response={responseText}", 0);
                    throw new InvalidOperationException($"Aconex Register Document falló: {response.StatusCode}. {responseText}");
                }

                string documentId = ParseRegisterDocumentResponse(responseText);
                Utilities.Wlog($"FileUploadWithMetadata: Documento registrado. DocNo={GetValueFromRow(metadataRow, columnas, "docno", "DocumentNumber")}, DocumentId={documentId}", 1);
                OnStatus?.Invoke($"Registrado en Aconex: {body.FileName} (Id={documentId})");
            }
        }

        /// <summary>
        /// Construye el XML Document para el API Register Document. Mapea columnas de la fila a los identificadores Aconex (DocumentNumber, Title, Revision, DocumentTypeId, DocumentStatusId, etc.).
        /// </summary>
        private string BuildAconexRegisterXml(DataRow row, DataColumnCollection columnas)
        {
            string docNumber = GetValueFromRow(row, columnas, "docno", "DocumentNumber") ?? "";
            string title = GetValueFromRow(row, columnas, "title", "Title") ?? "";
            string revision = GetValueFromRow(row, columnas, "revision", "Revision") ?? "A";
            string docTypeId = GetValueFromRow(row, columnas, "DocumentTypeId", "doctype") ?? _trabajoConfig.DocumentTypeIdDefault ?? "";
            string docStatusId = GetValueFromRow(row, columnas, "DocumentStatusId", "statusid") ?? _trabajoConfig.DocumentStatusIdDefault ?? "";
            if (string.IsNullOrWhiteSpace(docTypeId))
                throw new InvalidOperationException("DocumentTypeId es obligatorio para Register Document. Configure la columna DocumentTypeId en la tabla de metadata o DocumentTypeIdDefault en TrabajosConfiguracion (ej. 1207960435).");

            var sb = new StringBuilder();
            sb.Append("<Document>");
            sb.Append("<DocumentNumber>").Append(EscapeXml(docNumber)).Append("</DocumentNumber>");
            sb.Append("<DocumentTypeId>").Append(EscapeXml(docTypeId)).Append("</DocumentTypeId>");
            sb.Append("<Revision>").Append(EscapeXml(revision)).Append("</Revision>");
            sb.Append("<Title>").Append(EscapeXml(title)).Append("</Title>");
            if (!string.IsNullOrEmpty(docStatusId))
                sb.Append("<DocumentStatusId>").Append(EscapeXml(docStatusId)).Append("</DocumentStatusId>");
            sb.Append("<HasFile>true</HasFile>");

            // Campos opcionales según documentación Aconex
            AppendOptionalElement(sb, row, columnas, "Author", "author");
            AppendOptionalElement(sb, row, columnas, "AuthorisedBy", "authorisedBy");
            AppendOptionalElement(sb, row, columnas, "Discipline", "discipline");
            AppendOptionalElement(sb, row, columnas, "Comments", "comments");
            AppendOptionalElement(sb, row, columnas, "Comments2", "comments2");
            AppendOptionalElement(sb, row, columnas, "Reference", "reference");
            AppendOptionalElement(sb, row, columnas, "Category", "category");
            AppendOptionalElement(sb, row, columnas, "PackageNumber", "packagenumber");
            AppendOptionalElement(sb, row, columnas, "ContractNumber", "contractnumber");
            AppendOptionalElement(sb, row, columnas, "VendorDocumentNumber", "vendordocumentnumber");
            AppendOptionalElement(sb, row, columnas, "VendorRev", "vendorrev");
            AppendOptionalElement(sb, row, columnas, "ContractorDocumentNumber", "contractordocumentnumber");
            AppendOptionalElement(sb, row, columnas, "ContractorRev", "contractorrev");
            AppendOptionalElement(sb, row, columnas, "Vdrcode", "vdrcode");
            AppendOptionalElement(sb, row, columnas, "PrintSize", "printSize");
            AppendOptionalElement(sb, row, columnas, "TagNumber", "tagNumber");

            sb.Append("</Document>");
            return sb.ToString();
        }

        private static void AppendOptionalElement(StringBuilder sb, DataRow row, DataColumnCollection columnas, string aconexTag, params string[] columnNames)
        {
            string val = GetValueFromRow(row, columnas, columnNames);
            if (string.IsNullOrEmpty(val)) return;
            sb.Append("<").Append(aconexTag).Append(">").Append(EscapeXml(val)).Append("</").Append(aconexTag).Append(">");
        }

        private static string GetValueFromRow(DataRow row, DataColumnCollection columnas, params string[] columnNames)
        {
            foreach (string name in columnNames)
            {
                foreach (DataColumn c in columnas)
                {
                    if (!string.Equals(c.ColumnName, name, StringComparison.OrdinalIgnoreCase)) continue;
                    object o = row[c];
                    if (o == null || o == DBNull.Value) break;
                    string s = o.ToString().Trim();
                    if (s.Length > 0) return s;
                    break;
                }
            }
            return null;
        }

        private static string EscapeXml(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        /// <summary>
        /// Construye el body multipart/mixed según la documentación Aconex: parte 1 = XML Document, parte 2 = X-Filename + base64.
        /// </summary>
        private static string BuildMultipartRegisterBody(string xmlDocument, string fileName, string fileBase64, string boundary)
        {
            var sb = new StringBuilder();
            sb.Append("--").Append(boundary).Append("\r\n\r\n");
            sb.Append(xmlDocument).Append("\r\n");
            sb.Append("--").Append(boundary).Append("\r\n");
            sb.Append("X-Filename: ").Append(fileName ?? "document").Append("\r\n\r\n");
            sb.Append(fileBase64 ?? "").Append("\r\n\r\n");
            sb.Append("--").Append(boundary).Append("--");
            return sb.ToString();
        }

        /// <summary>
        /// Parsea la respuesta XML del Register Document y devuelve el documentId (RegisterDocumentResult).
        /// </summary>
        private static string ParseRegisterDocumentResponse(string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText)) return null;
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(responseText);
                XmlNode node = doc.SelectSingleNode("//RegisterDocumentResult") ?? doc.SelectSingleNode("/*[local-name()='RegisterDocumentResult']");
                if (node != null)
                    return node.InnerText.Trim();
                return null;
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Body para envío: metadata del documento + archivo en base64. Serializable a JSON.
    /// </summary>
    public class FileUploadWithMetadataBody
    {
        /// <summary>Metadata del documento (nombre de columna -> valor). Fechas en ISO8601, bytes en base64.</summary>
        public Dictionary<string, object> Metadata { get; set; }

        /// <summary>Contenido del archivo codificado en base64.</summary>
        public string FileBase64 { get; set; }

        /// <summary>Nombre del archivo (ej. documento.pdf).</summary>
        public string FileName { get; set; }
    }
}
