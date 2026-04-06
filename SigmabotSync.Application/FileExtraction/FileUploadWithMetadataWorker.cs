using SigmabotSync.Application.Common;
using SigmabotSync.Domain.Config;
using SigmabotSync.Domain.Entities;
using SigmabotSync.Domain.Ports;
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace SigmabotSync.Application.FileExtraction
{
    /// <summary>
    /// Worker para el tipo de trabajo FileUploadWithMetadata: lee la tabla de metadata desde la BD (CredencialBD),
    /// enlaza archivos en BasePath por la columna <c>NombreArchivo</c> (ej. <c>DocumentoEjemplo.pdf</c>) y envía archivo + metadata a Aconex.
    /// </summary>
    public class FileUploadWithMetadataWorker
    {
        /// <summary>
        /// Si es true, no se envía <c>DocumentNumber</c> en el XML y se envía <c>AutoNumber</c>=true para que Aconex asigne el número.
        /// Más adelante puede enlazarse a <c>TrabajoConfiguracion</c>.
        /// </summary>
        private const bool RegisterDocumentUseAconexAutoNumber = true;

        /// <summary>Valor por defecto del campo de proyecto <see cref="XmlNameTipoDeDocumentoSingleSelect"/> (más adelante: TrabajoConfiguracion).</summary>
        private const string DefaultTipoDeDocumentoSingleSelectValue = "Certificado";

        private const string XmlNameTipoDeDocumentoSingleSelect = "TipoDeDocumento_singleSelect";

        private readonly TrabajoConfiguracion _trabajoConfig;
        private readonly Credencial _credAconex;
        private readonly Credencial _credBd;
        private readonly FileExtractionConfig _aconexConfig;
        private readonly IAconexRegisterWritePort _registerWritePort;

        public event Action<int, int> OnProgress;
        public event Action<string> OnStatus;

        public FileUploadWithMetadataWorker(
            TrabajoConfiguracion trabajoConfig,
            Credencial credAconex,
            Credencial credBd,
            IAconexRegisterWritePort registerWritePort)
        {
            _trabajoConfig = trabajoConfig ?? throw new ArgumentNullException(nameof(trabajoConfig));
            _credAconex = credAconex ?? throw new ArgumentNullException(nameof(credAconex));
            _credBd = credBd ?? throw new ArgumentNullException(nameof(credBd));
            _registerWritePort = registerWritePort ?? throw new ArgumentNullException(nameof(registerWritePort));
            _aconexConfig = FileExtractionConfig.FromCredencial(credAconex, trabajoConfig.IdProyecto ?? "", null);
        }

        /// <summary>
        /// Ejecuta el proceso: lee metadata de la tabla, enlaza archivos por <c>NombreArchivo</c> en <c>BasePath</c> y envía a Aconex.
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

            string columnaNombreArchivo = ResolverColumnaNombreArchivo(metadata);
            if (columnaNombreArchivo == null)
            {
                throw new InvalidOperationException(
                    "La tabla de metadata debe tener una columna NombreArchivo (nombre del archivo en BasePath, ej. DocumentoEjemplo.pdf). Columnas encontradas: "
                    + string.Join(", ", metadata.Columns.Cast<DataColumn>().Select(c => c.ColumnName)));
            }

            string columnaProcesado = ResolverColumnaProcesado(metadata);
            string columnaId = ResolverColumnaId(metadata);
            var idsProcesadosExitosamente = new List<long>();
            var nombresProcesadosExitosamente = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            int total = metadata.Rows.Count;
            int procesados = 0;
            int enviados = 0;
            int omitidosYaProcesados = 0;

            OnStatus?.Invoke($"Procesando {total} registro(s) de metadata...");

            OnStatus?.Invoke("Obteniendo schema Register Document desde Aconex...");
            AconexRegisterSchemaSnapshot registerSchema = await ObtenerSchemaRegistroAconexAsync();

            OnStatus?.Invoke("Cargando TiposDocumentos y EstatusDocumentos en memoria...");
            (IReadOnlyDictionary<string, string> mapTipos, IReadOnlyDictionary<string, string> mapEstatus) =
                CargarMapasTiposYEstatusDocumentos(connectionStringBd);

            for (int i = 0; i < metadata.Rows.Count; i++)
            {
                DataRow row = metadata.Rows[i];
                if (FilaYaProcesada(row, metadata.Columns, columnaProcesado))
                {
                    object nom = row[columnaNombreArchivo];
                    string nomArchivo = nom?.ToString()?.Trim() ?? "";
                    Utilities.Wlog($"FileUploadWithMetadata: Fila {i + 1} ya procesada (Procesado=1), se omite. NombreArchivo={nomArchivo}", 1);
                    omitidosYaProcesados++;
                    procesados++;
                    OnProgress?.Invoke(procesados, total);
                    continue;
                }

                object nombreObj = row[columnaNombreArchivo];
                string nombreArchivo = nombreObj?.ToString().Trim();
                if (string.IsNullOrEmpty(nombreArchivo))
                {
                    Utilities.Wlog($"FileUploadWithMetadata: Fila {i + 1} sin NombreArchivo, se omite.", 1);
                    procesados++;
                    OnProgress?.Invoke(procesados, total);
                    continue;
                }

                string filePath = ResolverRutaArchivoPorNombreArchivo(basePath, nombreArchivo);
                if (string.IsNullOrEmpty(filePath))
                {
                    string msgArchivo = $"Fila {i + 1}, NombreArchivo={nombreArchivo}: archivo no encontrado en BasePath.";
                    Utilities.Wlog($"FileUploadWithMetadata: {msgArchivo}", 1);
                    throw new InvalidOperationException(msgArchivo);
                }

                try
                {
                    await EnviarDocumentoAconexAsync(filePath, row, metadata.Columns, registerSchema, mapTipos, mapEstatus);
                    enviados++;
                    AcumularFilaProcesadaParaUpdate(row, metadata.Columns, columnaId, columnaNombreArchivo, idsProcesadosExitosamente, nombresProcesadosExitosamente);
                    OnStatus?.Invoke($"Enviado: {nombreArchivo}");
                }
                catch (Exception ex)
                {
                    Utilities.Wlog($"FileUploadWithMetadata: Error enviando NombreArchivo={nombreArchivo}: {ex.Message}", 0);
                    throw;
                }

                procesados++;
                OnProgress?.Invoke(procesados, total);
            }

            int marcados = MarcarFilasComoProcesadas(
                connectionStringBd,
                tablaMetadata,
                columnaProcesado,
                columnaId,
                idsProcesadosExitosamente,
                columnaNombreArchivo,
                nombresProcesadosExitosamente);

            OnStatus?.Invoke($"Completado: {enviados} enviado(s), {omitidosYaProcesados} omitido(s) ya procesado(s), {marcados} marcado(s) con Procesado=1.");
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
        /// Carga en memoria <c>Nombre</c> → <c>idTipo</c> / <c>idEstatus</c> (una sola lectura por ejecución del trabajo).
        /// </summary>
        private static (IReadOnlyDictionary<string, string> IdTipoPorNombre, IReadOnlyDictionary<string, string> IdEstatusPorNombre)
            CargarMapasTiposYEstatusDocumentos(string connectionString)
        {
            var tipos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var estatus = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(connectionString))
                return (tipos, estatus);

            using (var cn = new SqlConnection(connectionString))
            {
                cn.Open();
                using (var cmd = new SqlCommand("SELECT [Nombre], [idTipo] FROM [TiposDocumentos]", cn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        string nombre = r[0] == DBNull.Value ? null : r[0].ToString()?.Trim();
                        if (string.IsNullOrEmpty(nombre)) continue;
                        string id = r[1] == DBNull.Value ? null : r[1].ToString()?.Trim();
                        if (string.IsNullOrEmpty(id)) continue;
                        if (!tipos.ContainsKey(nombre))
                            tipos[nombre] = id;
                    }
                }

                using (var cmd = new SqlCommand("SELECT [Nombre], [idEstatus] FROM [EstatusDocumentos]", cn))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        string nombre = r[0] == DBNull.Value ? null : r[0].ToString()?.Trim();
                        if (string.IsNullOrEmpty(nombre)) continue;
                        string id = r[1] == DBNull.Value ? null : r[1].ToString()?.Trim();
                        if (string.IsNullOrEmpty(id)) continue;
                        if (!estatus.ContainsKey(nombre))
                            estatus[nombre] = id;
                    }
                }
            }

            return (tipos, estatus);
        }

        /// <summary>
        /// Resuelve <c>idTipo</c> desde el mapa precargado (equivalente a buscar por <c>Nombre</c> en <c>TiposDocumentos</c>).
        /// </summary>
        private static string ResolveIdTipoFromTiposDocumentos(IReadOnlyDictionary<string, string> idTipoPorNombre, string nombreTipo)
        {
            if (idTipoPorNombre == null || string.IsNullOrWhiteSpace(nombreTipo))
                return null;
            string key = nombreTipo.Trim();
            return idTipoPorNombre.TryGetValue(key, out string id) ? id : null;
        }

        /// <summary>
        /// Resuelve <c>idEstatus</c> desde el mapa precargado (equivalente a buscar por <c>Nombre</c> en <c>EstatusDocumentos</c>).
        /// </summary>
        private static string ResolveIdEstatusFromEstatusDocumentos(IReadOnlyDictionary<string, string> idEstatusPorNombre, string nombreEstatus)
        {
            if (idEstatusPorNombre == null || string.IsNullOrWhiteSpace(nombreEstatus))
                return null;
            string key = nombreEstatus.Trim();
            return idEstatusPorNombre.TryGetValue(key, out string id) ? id : null;
        }

        /// <summary>
        /// Obtiene el nombre de la columna <c>NombreArchivo</c> (nombre del archivo en <c>BasePath</c>), case-insensitive.
        /// </summary>
        private static string ResolverColumnaNombreArchivo(DataTable metadata)
        {
            foreach (DataColumn c in metadata.Columns)
            {
                if (string.Equals(c.ColumnName, "NombreArchivo", StringComparison.OrdinalIgnoreCase))
                    return c.ColumnName;
            }
            return null;
        }

        private static string ResolverColumnaProcesado(DataTable metadata)
        {
            foreach (DataColumn c in metadata.Columns)
            {
                if (string.Equals(c.ColumnName, "Procesado", StringComparison.OrdinalIgnoreCase))
                    return c.ColumnName;
            }
            return null;
        }

        private static string ResolverColumnaId(DataTable metadata)
        {
            foreach (DataColumn c in metadata.Columns)
            {
                if (string.Equals(c.ColumnName, "Id", StringComparison.OrdinalIgnoreCase))
                    return c.ColumnName;
            }
            return null;
        }

        private static bool FilaYaProcesada(DataRow row, DataColumnCollection columnas, string columnaProcesado)
        {
            if (string.IsNullOrWhiteSpace(columnaProcesado)) return false;
            DataColumn col = null;
            foreach (DataColumn c in columnas)
            {
                if (string.Equals(c.ColumnName, columnaProcesado, StringComparison.OrdinalIgnoreCase))
                {
                    col = c;
                    break;
                }
            }
            if (col == null) return false;

            object o = row[col];
            if (o == null || o == DBNull.Value) return false;
            if (o is bool b) return b;
            if (o is byte bt) return bt != 0;
            if (o is short s) return s != 0;
            if (o is int i) return i != 0;
            string t = o.ToString()?.Trim() ?? "";
            return t == "1" || string.Equals(t, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static void AcumularFilaProcesadaParaUpdate(
            DataRow row,
            DataColumnCollection columnas,
            string columnaId,
            string columnaNombreArchivo,
            List<long> idsProcesadosExitosamente,
            HashSet<string> nombresProcesadosExitosamente)
        {
            if (!string.IsNullOrWhiteSpace(columnaId))
            {
                object oid = row[columnaId];
                if (oid != null && oid != DBNull.Value && long.TryParse(oid.ToString(), out long idVal))
                {
                    idsProcesadosExitosamente.Add(idVal);
                    return;
                }
            }

            object on = row[columnaNombreArchivo];
            string nombre = on?.ToString()?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(nombre))
                nombresProcesadosExitosamente.Add(Path.GetFileName(nombre));
        }

        private static int MarcarFilasComoProcesadas(
            string connectionString,
            string nombreTabla,
            string columnaProcesado,
            string columnaId,
            IReadOnlyList<long> idsProcesadosExitosamente,
            string columnaNombreArchivo,
            IReadOnlyCollection<string> nombresProcesadosExitosamente)
        {
            if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(nombreTabla))
                return 0;
            if (string.IsNullOrWhiteSpace(columnaProcesado))
            {
                Utilities.Wlog("FileUploadWithMetadata: no existe columna Procesado en metadata, no se pudo marcar Procesado=1.", 1);
                return 0;
            }

            string tablaEsc = "[" + nombreTabla.Replace("]", "]]") + "]";
            string colProcesadoEsc = "[" + columnaProcesado.Replace("]", "]]") + "]";
            int totalActualizados = 0;

            using (var cn = new SqlConnection(connectionString))
            {
                cn.Open();

                if (!string.IsNullOrWhiteSpace(columnaId) && idsProcesadosExitosamente != null && idsProcesadosExitosamente.Count > 0)
                {
                    string colIdEsc = "[" + columnaId.Replace("]", "]]") + "]";
                    const int batchSize = 500;
                    for (int start = 0; start < idsProcesadosExitosamente.Count; start += batchSize)
                    {
                        int count = Math.Min(batchSize, idsProcesadosExitosamente.Count - start);
                        var paramNames = new List<string>(count);
                        using (var cmd = new SqlCommand())
                        {
                            cmd.Connection = cn;
                            for (int j = 0; j < count; j++)
                            {
                                string p = "@p" + j;
                                paramNames.Add(p);
                                cmd.Parameters.AddWithValue(p, idsProcesadosExitosamente[start + j]);
                            }

                            cmd.CommandText = "UPDATE " + tablaEsc + " SET " + colProcesadoEsc + " = 1 WHERE " + colIdEsc + " IN (" + string.Join(", ", paramNames) + ")";
                            totalActualizados += cmd.ExecuteNonQuery();
                        }
                    }

                    return totalActualizados;
                }

                if (!string.IsNullOrWhiteSpace(columnaNombreArchivo) && nombresProcesadosExitosamente != null && nombresProcesadosExitosamente.Count > 0)
                {
                    string colNombreEsc = "[" + columnaNombreArchivo.Replace("]", "]]") + "]";
                    int ix = 0;
                    foreach (string nombre in nombresProcesadosExitosamente)
                    {
                        using (var cmd = new SqlCommand(
                            "UPDATE " + tablaEsc + " SET " + colProcesadoEsc + " = 1 WHERE " + colNombreEsc + " = @nombre", cn))
                        {
                            cmd.Parameters.AddWithValue("@nombre", nombre);
                            totalActualizados += cmd.ExecuteNonQuery();
                        }
                        ix++;
                    }
                }
            }

            return totalActualizados;
        }

        /// <summary>
        /// Busca en <paramref name="basePath"/> un archivo cuyo nombre coincida con <paramref name="nombreArchivo"/>
        /// (ej. <c>DocumentoEjemplo.pdf</c>). Solo se usa el nombre de archivo (sin subcarpetas) por seguridad.
        /// </summary>
        private static string ResolverRutaArchivoPorNombreArchivo(string basePath, string nombreArchivo)
        {
            if (!Directory.Exists(basePath) || string.IsNullOrWhiteSpace(nombreArchivo))
                return null;

            string soloNombre = Path.GetFileName(nombreArchivo.Trim());
            if (string.IsNullOrEmpty(soloNombre))
                return null;

            string pathExacto = Path.Combine(basePath, soloNombre);
            if (File.Exists(pathExacto))
                return pathExacto;

            foreach (string f in Directory.GetFiles(basePath))
            {
                if (string.Equals(Path.GetFileName(f), soloNombre, StringComparison.OrdinalIgnoreCase))
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
        /// GET <c>/api/projects/{{projectId}}/register/schema</c>: campos de creación según configuración del proyecto.
        /// </summary>
        private async Task<AconexRegisterSchemaSnapshot> ObtenerSchemaRegistroAconexAsync()
        {
            string projectId = _trabajoConfig.IdProyecto ?? _aconexConfig.ProjectId ?? "";
            if (string.IsNullOrWhiteSpace(projectId))
                throw new InvalidOperationException("IdProyecto es requerido para Register Document.");

            string baseUrl = string.IsNullOrWhiteSpace(_aconexConfig.AconexBaseUrl) ? "https://us1.aconex.com" : _aconexConfig.AconexBaseUrl.TrimEnd('/');

            string responseText;
            try
            {
                responseText = await _registerWritePort.GetRegisterSchemaXmlAsync(
                    baseUrl,
                    projectId,
                    _aconexConfig.AuthorizationHeader,
                    _aconexConfig.IntegrationId,
                    default).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                Utilities.Wlog($"FileUploadWithMetadata: GET register/schema falló. {ex.Message}", 0);
                throw;
            }

            AconexRegisterSchemaSnapshot snapshot = AconexRegisterSchemaParser.ParseSnapshot(responseText);
            if (snapshot.Fields == null || snapshot.Fields.Count == 0)
            {
                throw new InvalidOperationException(
                    "El XML de register/schema no contiene campos en EntityCreationSchemaFields (o no se pudieron leer). Revise la respuesta del endpoint.");
            }

            return snapshot;
        }

        /// <summary>
        /// Envía el archivo y la metadata a Aconex mediante el API Register Document (multipart/mixed: XML + archivo base64).
        /// Ver: https://help.aconex.com/apis/api-guide-documents/#Register-Document
        /// </summary>
        private async Task EnviarDocumentoAconexAsync(
            string filePath,
            DataRow metadataRow,
            DataColumnCollection columnas,
            AconexRegisterSchemaSnapshot registerSchema,
            IReadOnlyDictionary<string, string> idTipoPorNombre,
            IReadOnlyDictionary<string, string> idEstatusPorNombre)
        {
            FileUploadWithMetadataBody body = BuildBodyWithMetadataAndFileBase64(filePath, metadataRow, columnas);
            string projectId = _trabajoConfig.IdProyecto ?? _aconexConfig.ProjectId ?? "";
            if (string.IsNullOrWhiteSpace(projectId))
                throw new InvalidOperationException("IdProyecto es requerido para Register Document.");

            string xmlDocument = BuildAconexRegisterXml(
                metadataRow, columnas, body.FileName, registerSchema, idTipoPorNombre, idEstatusPorNombre);
            Utilities.Wlog("FileUploadWithMetadata: XML Register Document (cuerpo multipart 1): " + xmlDocument, 1);

            string boundary = AconexRegisterMultipart.ExampleBoundary;
            string multipartBody = AconexRegisterMultipart.BuildRegisterBody(xmlDocument, body.FileName, body.FileBase64, boundary);

            string baseUrl = string.IsNullOrWhiteSpace(_aconexConfig.AconexBaseUrl) ? "https://us1.aconex.com" : _aconexConfig.AconexBaseUrl.TrimEnd('/');

            AconexRawHttpResponse raw = await _registerWritePort.PostRegisterDocumentAsync(
                baseUrl,
                projectId,
                _aconexConfig.AuthorizationHeader,
                _aconexConfig.IntegrationId,
                multipartBody,
                boundary,
                default).ConfigureAwait(false);

            string responseText = raw.Body ?? "";

            if (!raw.IsSuccessStatusCode)
            {
                Utilities.Wlog($"FileUploadWithMetadata: Register Document falló. Status={raw.StatusCode}, Response={responseText}", 0);
                if (ResponseIndicatesFieldValueAlreadyExists(responseText))
                {
                    string refArchivo = GetValueFromRow(metadataRow, columnas, "NombreArchivo") ?? Path.GetFileName(filePath) ?? "";
                    throw new InvalidOperationException(
                        "Aconex indica FIELD_VALUE_ALREADY_EXISTS (p. ej. documento o valor único ya existente). " +
                        "Register Document solo crea documentos nuevos. Opciones: excluir esa fila si ya se cargó, " +
                        "o usar en Aconex el flujo de nueva revisión / Supersede según su proceso. " +
                        $"NombreArchivo={refArchivo}. Respuesta: {responseText}");
                }

                throw new InvalidOperationException($"Aconex Register Document falló: {raw.StatusCode}. {responseText}");
            }

            string documentId = ParseRegisterDocumentResponse(responseText);
            string logArchivo = GetValueFromRow(metadataRow, columnas, "NombreArchivo") ?? Path.GetFileName(filePath) ?? "";
            Utilities.Wlog($"FileUploadWithMetadata: Documento registrado. NombreArchivo={logArchivo}, DocumentId={documentId}", 1);
            OnStatus?.Invoke($"Registrado en Aconex: {body.FileName} (Id={documentId})");
        }

        /// <summary>
        /// Obtiene el <c>DocumentStatusId</c> válido para el proyecto: primero contra el schema (nombre o Id de Aconex), luego <c>EstatusDocumentos</c>.
        /// Los IDs deben existir en el schema; un <c>idEstatus</c> local que no coincida con Aconex provoca <c>INVALID_FIELD_VALUE</c>.
        /// </summary>
        private string ResolveDocumentStatusIdForAconex(
            DataRow row,
            DataColumnCollection columnas,
            IReadOnlyDictionary<string, IReadOnlyList<AconexSchemaValueOption>> picklists,
            IReadOnlyDictionary<string, string> idEstatusPorNombre)
        {
            string raw = GetValueFromRow(row, columnas, "docstatus", "statusid", "DocumentStatusId");
            if (string.IsNullOrWhiteSpace(raw))
                throw new InvalidOperationException(
                    "El estado del documento es obligatorio para Register Document: indique docstatus, statusid o DocumentStatusId en la tabla de metadata.");

            string trimmed = raw.Trim();

            if (TryResolveIdFromAconexPicklist(picklists, "DocumentStatusId", trimmed, out string fromSchema))
                return fromSchema;

            string fromSql = ResolveIdEstatusFromEstatusDocumentos(idEstatusPorNombre, trimmed);
            if (string.IsNullOrWhiteSpace(fromSql))
                throw new InvalidOperationException(
                    $"No se encontró estado para '{trimmed}' ni en el schema de Aconex (SchemaValue) ni en EstatusDocumentos (Nombre).");

            if (PicklistDefinesOptions(picklists, "DocumentStatusId") &&
                !IsIdInPicklist(picklists, "DocumentStatusId", fromSql))
            {
                throw new InvalidOperationException(
                    $"El id de estado '{fromSql}' (tabla EstatusDocumentos) no es un DocumentStatusId válido para este proyecto en Aconex. " +
                    "Use el texto exacto del estado como en la interfaz o el Id que aparece en GET .../register/schema para ese estado.");
            }

            return fromSql;
        }

        /// <summary>
        /// Resuelve <c>DocumentTypeId</c> desde el schema (preferido) o <c>TiposDocumentos</c>.
        /// </summary>
        private string ResolveDocumentTypeIdForAconex(
            DataRow row,
            DataColumnCollection columnas,
            IReadOnlyDictionary<string, IReadOnlyList<AconexSchemaValueOption>> picklists,
            IReadOnlyDictionary<string, string> idTipoPorNombre)
        {
            string docTypeNombre = GetValueFromRow(row, columnas, "doctype");
            if (string.IsNullOrWhiteSpace(docTypeNombre))
                throw new InvalidOperationException("doctype es obligatorio para Register Document: debe indicar el nombre del tipo de documento (columna doctype en la tabla de metadata).");

            string trimmed = docTypeNombre.Trim();

            if (TryResolveIdFromAconexPicklist(picklists, "DocumentTypeId", trimmed, out string fromSchema))
                return fromSchema;

            string fromSql = ResolveIdTipoFromTiposDocumentos(idTipoPorNombre, trimmed);
            if (string.IsNullOrWhiteSpace(fromSql))
                throw new InvalidOperationException(
                    $"No se encontró tipo de documento para '{trimmed}' ni en el schema de Aconex ni en TiposDocumentos (Nombre).");

            if (PicklistDefinesOptions(picklists, "DocumentTypeId") &&
                !IsIdInPicklist(picklists, "DocumentTypeId", fromSql))
            {
                throw new InvalidOperationException(
                    $"El id de tipo '{fromSql}' (tabla TiposDocumentos) no es un DocumentTypeId válido para este proyecto en Aconex. " +
                    "Use el nombre del tipo como en Aconex o el Id del schema (GET .../register/schema).");
            }

            return fromSql;
        }

        private static bool PicklistDefinesOptions(
            IReadOnlyDictionary<string, IReadOnlyList<AconexSchemaValueOption>> picklists,
            string identifier)
        {
            if (picklists == null || string.IsNullOrEmpty(identifier)) return false;
            return picklists.TryGetValue(identifier, out var opts) && opts != null && opts.Count > 0;
        }

        private static bool TryResolveIdFromAconexPicklist(
            IReadOnlyDictionary<string, IReadOnlyList<AconexSchemaValueOption>> picklists,
            string fieldIdentifier,
            string userInput,
            out string aconexId)
        {
            aconexId = null;
            if (picklists == null || string.IsNullOrWhiteSpace(userInput) || string.IsNullOrWhiteSpace(fieldIdentifier))
                return false;
            if (!picklists.TryGetValue(fieldIdentifier, out var options) || options == null || options.Count == 0)
                return false;

            string t = userInput.Trim();
            foreach (AconexSchemaValueOption o in options)
            {
                if (o == null) continue;
                if (!string.IsNullOrWhiteSpace(o.Id) && string.Equals(o.Id.Trim(), t, StringComparison.OrdinalIgnoreCase))
                {
                    aconexId = o.Id.Trim();
                    return true;
                }
            }

            foreach (AconexSchemaValueOption o in options)
            {
                if (o?.Value == null) continue;
                if (string.Equals(o.Value.Trim(), t, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(o.Id))
                    {
                        aconexId = o.Id.Trim();
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsIdInPicklist(
            IReadOnlyDictionary<string, IReadOnlyList<AconexSchemaValueOption>> picklists,
            string fieldIdentifier,
            string id)
        {
            if (picklists == null || string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(fieldIdentifier))
                return false;
            if (!picklists.TryGetValue(fieldIdentifier, out var options) || options == null)
                return false;
            foreach (AconexSchemaValueOption o in options)
            {
                if (o != null && !string.IsNullOrWhiteSpace(o.Id) && string.Equals(o.Id.Trim(), id.Trim(), StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Construye el XML <c>Document</c> según <paramref name="registerSchema"/> (GET register/schema).
        /// Tipo y estado: <c>doctype</c> → <c>TiposDocumentos</c>; <c>docstatus</c>/<c>statusid</c>/<c>DocumentStatusId</c> → <c>EstatusDocumentos</c>.
        /// El resto de identificadores se toman de columnas cuyo nombre coincide con el identificador o alias (p. ej. <c>Discipline</c>/<c>discipline</c>).
        /// Con autonumeración (<see cref="RegisterDocumentUseAconexAutoNumber"/>), no se envía <c>DocumentNumber</c>; el archivo en disco se enlaza por la columna <c>NombreArchivo</c> (ver <see cref="RunAsync"/>).
        /// Campos de proyecto con sufijo <c>_singleSelect</c> en la metadata se envían como elementos hijos directos de <c>Document</c> (p. ej. <c>&lt;Cma_singleSelect&gt;…&lt;/Cma_singleSelect&gt;</c>). Si <c>TipoDeDocumento_singleSelect</c> falta o viene vacío, se usa <see cref="DefaultTipoDeDocumentoSingleSelectValue"/>.
        /// </summary>
        private string BuildAconexRegisterXml(
            DataRow row,
            DataColumnCollection columnas,
            string uploadFileName,
            AconexRegisterSchemaSnapshot registerSchema,
            IReadOnlyDictionary<string, string> idTipoPorNombre,
            IReadOnlyDictionary<string, string> idEstatusPorNombre)
        {
            if (registerSchema?.Fields == null || registerSchema.Fields.Count == 0)
                throw new ArgumentException("registerSchema no puede estar vacío.", nameof(registerSchema));

            bool useAutoNumber = RegisterDocumentUseAconexAutoNumber;
            string docNumber = GetValueFromRow(row, columnas, "docno", "DocumentNumber") ?? "";
            if (!useAutoNumber && string.IsNullOrWhiteSpace(docNumber))
                throw new InvalidOperationException("docno/DocumentNumber es obligatorio en la tabla de metadata para Register Document (o active autonumeración en Aconex y en este worker).");

            string title = GetValueFromRow(row, columnas, "title", "Title") ?? "";
            if (string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(uploadFileName))
                title = Path.GetFileNameWithoutExtension(uploadFileName);
            if (string.IsNullOrWhiteSpace(title))
            {
                if (!string.IsNullOrWhiteSpace(docNumber))
                    title = docNumber;
                else
                {
                    string na = GetValueFromRow(row, columnas, "NombreArchivo");
                    title = !string.IsNullOrWhiteSpace(na) ? Path.GetFileNameWithoutExtension(na) : "Documento";
                }
            }

            string revision = GetValueFromRow(row, columnas, "revision", "Revision") ?? "A";

            IReadOnlyDictionary<string, IReadOnlyList<AconexSchemaValueOption>> picklists = registerSchema.PicklistsByIdentifier;

            string docTypeId = ResolveDocumentTypeIdForAconex(row, columnas, picklists, idTipoPorNombre);
            string docStatusId = ResolveDocumentStatusIdForAconex(row, columnas, picklists, idEstatusPorNombre);

            var sb = new StringBuilder();
            sb.Append("<Document>");
            if (useAutoNumber)
                sb.Append("<AutoNumber>true</AutoNumber>");

            bool emittedHasFile = false;
            var emittedXmlIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (useAutoNumber)
                emittedXmlIdentifiers.Add("DocumentNumber");

            foreach (AconexRegisterSchemaField field in registerSchema.Fields)
            {
                if (field == null || string.IsNullOrWhiteSpace(field.Identifier))
                    continue;

                string id = field.Identifier.Trim();
                if (useAutoNumber && string.Equals(id, "DocumentNumber", StringComparison.OrdinalIgnoreCase))
                    continue;

                string mandatory = field.MandatoryStatus ?? "";
                bool isMandatory = string.Equals(mandatory, "MANDATORY", StringComparison.OrdinalIgnoreCase);

                string value = ResolveRegisterFieldValueForSchema(
                    row, columnas, field.DataType,
                    id, docNumber, title, revision, docTypeId, docStatusId);

                if (string.IsNullOrEmpty(value))
                {
                    if (isMandatory)
                        throw new InvalidOperationException(
                            $"Campo obligatorio según schema de Aconex sin valor: {id}. Añada la columna en la tabla de metadata o el dato requerido.");
                    continue;
                }

                sb.Append("<").Append(id).Append(">").Append(EscapeXml(value)).Append("</").Append(id).Append(">");
                emittedXmlIdentifiers.Add(id);
                if (string.Equals(id, "HasFile", StringComparison.OrdinalIgnoreCase))
                    emittedHasFile = true;
            }

            AppendRegisterXmlFromExtraMetadataColumns(sb, row, columnas, emittedXmlIdentifiers, registerSchema, useAutoNumber);

            AppendSingleSelectMetadataAsDocumentElements(sb, row, columnas);

            if (!emittedHasFile)
                sb.Append("<HasFile>true</HasFile>");

            sb.Append("</Document>");
            return sb.ToString();
        }

        /// <summary>
        /// Detecta columnas de metadata cuyo nombre termina en <c>_singleSelect</c> (convención acordada con Aconex).
        /// </summary>
        private static bool IsProjectFieldSingleSelectColumn(string columnName) =>
            !string.IsNullOrWhiteSpace(columnName)
            && columnName.EndsWith("_singleSelect", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Emite un elemento por cada columna <c>*_singleSelect</c> con valor como hijo directo de <c>Document</c>.
        /// <c>TipoDeDocumento_singleSelect</c> vacío o ausente usa <see cref="DefaultTipoDeDocumentoSingleSelectValue"/>.
        /// </summary>
        private static void AppendSingleSelectMetadataAsDocumentElements(
            StringBuilder sb,
            DataRow row,
            DataColumnCollection columnas)
        {
            bool wroteTipoDeDocumento = false;

            foreach (DataColumn c in columnas)
            {
                string colName = c.ColumnName;
                if (!IsProjectFieldSingleSelectColumn(colName)) continue;

                object o = row[c];
                string value = o == null || o == DBNull.Value ? "" : ( o.ToString()?.Trim() ?? "" );
                if (string.IsNullOrEmpty(value)
                    && string.Equals(colName, XmlNameTipoDeDocumentoSingleSelect, StringComparison.OrdinalIgnoreCase))
                    value = DefaultTipoDeDocumentoSingleSelectValue;

                if (string.IsNullOrEmpty(value)) continue;

                sb.Append("<").Append(colName).Append(">").Append(EscapeXml(value)).Append("</").Append(colName).Append(">");
                if (string.Equals(colName, XmlNameTipoDeDocumentoSingleSelect, StringComparison.OrdinalIgnoreCase))
                    wroteTipoDeDocumento = true;
            }

            if (!wroteTipoDeDocumento)
            {
                sb.Append("<").Append(XmlNameTipoDeDocumentoSingleSelect).Append(">")
                    .Append(EscapeXml(DefaultTipoDeDocumentoSingleSelectValue))
                    .Append("</").Append(XmlNameTipoDeDocumentoSingleSelect).Append(">");
            }
        }

        /// <summary>
        /// Register Document usa identificadores del schema del proyecto; los campos <c>*_singleSelect</c> se emiten aparte como hijos directos de <c>Document</c> (ver <see cref="AppendSingleSelectMetadataAsDocumentElements"/>). El prefijo <c>RegisterXml_</c> sigue limitado a identificadores conocidos de la guía.
        /// 1) Columnas <c>RegisterXml_ProjectField1</c> (prefijo <c>RegisterXml_</c> + identificador permitido): emiten <c>&lt;ProjectField1&gt;…&lt;/ProjectField1&gt;</c>.
        /// 2) Identificadores conocidos aún no emitidos: se rellenan por alias (p. ej. columna <c>ProjectField1</c>).
        /// Revise en GET <c>register/schema</c> qué <c>FieldName</c> corresponde a cada <c>ProjectField1</c>…<c>3</c> en su proyecto.
        /// </summary>
        private static void AppendRegisterXmlFromExtraMetadataColumns(
            StringBuilder sb,
            DataRow row,
            DataColumnCollection columnas,
            HashSet<string> emittedXmlIdentifiers,
            AconexRegisterSchemaSnapshot registerSchema,
            bool useAutoNumber)
        {
            foreach (DataColumn c in columnas)
            {
                string colName = c.ColumnName;
                if (string.IsNullOrWhiteSpace(colName)) continue;
                if (!TryParseRegisterXmlPrefixedColumn(colName, out string xmlId)) continue;
                if (!IsKnownRegisterDocumentIdentifier(xmlId)) continue;
                if (useAutoNumber && string.Equals(xmlId, "DocumentNumber", StringComparison.OrdinalIgnoreCase)) continue;
                if (emittedXmlIdentifiers.Contains(xmlId)) continue;

                object o = row[c];
                if (o == null || o == DBNull.Value) continue;

                string dt = GetDataTypeForRegisterIdentifier(xmlId, registerSchema);
                string value = FormatRegisterValue(o, dt, xmlId);
                if (string.IsNullOrEmpty(value)) continue;

                sb.Append("<").Append(xmlId).Append(">").Append(EscapeXml(value)).Append("</").Append(xmlId).Append(">");
                emittedXmlIdentifiers.Add(xmlId);
            }

            foreach (string id in KnownRegisterDocumentFieldIdentifiers)
            {
                if (emittedXmlIdentifiers.Contains(id)) continue;
                if (useAutoNumber && string.Equals(id, "DocumentNumber", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(id, "HasFile", StringComparison.OrdinalIgnoreCase)) continue;
                if (IsSpecialCasedRegisterResolveIdentifier(id)) continue;

                string dt = GetDataTypeForRegisterIdentifier(id, registerSchema);
                string value = GetGenericRegisterFieldValue(row, columnas, id, dt);
                if (string.IsNullOrEmpty(value)) continue;

                sb.Append("<").Append(id).Append(">").Append(EscapeXml(value)).Append("</").Append(id).Append(">");
                emittedXmlIdentifiers.Add(id);
            }
        }

        /// <summary>Identificadores de creación admitidos por la API Register Document (subset de la guía Oracle Aconex).</summary>
        private static readonly string[] KnownRegisterDocumentFieldIdentifiers =
        {
            "DocumentTypeId", "DocumentStatusId", "Discipline", "Attribute1", "Attribute2", "Attribute3", "Attribute4",
            "ReviewStatusId", "Vdrcode", "Category", "PackageNumber", "ContractNumber",
            "DocumentNumber", "Revision", "DateCreated", "Title", "AuthorisedBy", "Comments", "Comments2",
            "PrintSize", "PercentComplete", "Reference", "Author", "Scale", "AccessList", "DateApproved",
            "DateForReview", "DateReviewed", "ToClientDate", "RevisionDate", "PlannedSubmissionDate",
            "MilestoneDate", "TagNumber", "VendorDocumentNumber", "VendorRev", "ContractorDocumentNumber",
            "ContractorRev", "AsBuiltRequired", "ContractDeliverable", "ProjectField1", "ProjectField2",
            "ProjectField3", "Check1", "Check2", "Date1", "Date2", "HasFile"
        };

        private static readonly HashSet<string> KnownRegisterDocumentIdentifierSet =
            new HashSet<string>(KnownRegisterDocumentFieldIdentifiers, StringComparer.OrdinalIgnoreCase);

        private static bool IsKnownRegisterDocumentIdentifier(string id) =>
            !string.IsNullOrWhiteSpace(id) && KnownRegisterDocumentIdentifierSet.Contains(id);

        /// <summary>Identificadores resueltos en <see cref="ResolveRegisterFieldValueForSchema"/>; no rellenar con <see cref="GetGenericRegisterFieldValue"/> en la pasada extra.</summary>
        private static bool IsSpecialCasedRegisterResolveIdentifier(string id)
        {
            switch (id)
            {
                case "DocumentNumber":
                case "Title":
                case "Revision":
                case "DocumentTypeId":
                case "DocumentStatusId":
                case "HasFile":
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryParseRegisterXmlPrefixedColumn(string columnName, out string registerIdentifier)
        {
            registerIdentifier = null;
            if (string.IsNullOrWhiteSpace(columnName)) return false;
            const string prefix = "RegisterXml_";
            if (!columnName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
            registerIdentifier = columnName.Substring(prefix.Length).Trim();
            return registerIdentifier.Length > 0;
        }

        private static string GetDataTypeForRegisterIdentifier(string id, AconexRegisterSchemaSnapshot registerSchema)
        {
            if (registerSchema?.Fields != null)
            {
                foreach (AconexRegisterSchemaField f in registerSchema.Fields)
                {
                    if (f != null && string.Equals(f.Identifier, id, StringComparison.OrdinalIgnoreCase))
                        return string.IsNullOrWhiteSpace(f.DataType) ? "STRING" : f.DataType.Trim();
                }
            }

            switch (id)
            {
                case "Date1":
                case "Date2":
                case "DateCreated":
                case "DateApproved":
                case "DateForReview":
                case "DateReviewed":
                case "ToClientDate":
                case "RevisionDate":
                case "PlannedSubmissionDate":
                case "MilestoneDate":
                    return "DATE";
                case "Check1":
                case "Check2":
                case "AsBuiltRequired":
                case "ContractDeliverable":
                case "HasFile":
                    return "BOOLEAN";
                case "PercentComplete":
                case "AccessList":
                    return "INTEGER";
                default:
                    return "STRING";
            }
        }

        /// <summary>
        /// Resuelve el valor XML para un <see cref="AconexRegisterSchemaField.Identifier"/>.
        /// </summary>
        private static string ResolveRegisterFieldValueForSchema(
            DataRow row,
            DataColumnCollection columnas,
            string dataType,
            string identifier,
            string docNumber,
            string title,
            string revision,
            string docTypeId,
            string docStatusId)
        {
            switch (identifier)
            {
                case "DocumentNumber":
                    return docNumber;
                case "Title":
                    return title;
                case "Revision":
                    return revision;
                case "DocumentTypeId":
                    return docTypeId;
                case "DocumentStatusId":
                    return docStatusId;
                case "HasFile":
                    return "true";
                default:
                    return GetGenericRegisterFieldValue(row, columnas, identifier, dataType);
            }
        }

        private static string GetGenericRegisterFieldValue(DataRow row, DataColumnCollection columnas, string identifier, string dataType)
        {
            foreach (string alias in GetColumnAliasesForIdentifier(identifier))
            {
                foreach (DataColumn c in columnas)
                {
                    if (!string.Equals(c.ColumnName, alias, StringComparison.OrdinalIgnoreCase)) continue;
                    object o = row[c];
                    if (o == null || o == DBNull.Value) break;
                    return FormatRegisterValue(o, dataType, identifier);
                }
            }
            return null;
        }

        /// <summary>
        /// Campos de fecha en Register Document (DataType DATE en el schema). Mismo patrón que el search (ISO UTC con <c>Z</c>).
        /// </summary>
        private static bool IsAconexDateOnlyXmlField(string xmlFieldIdentifier)
        {
            if (string.IsNullOrEmpty(xmlFieldIdentifier)) return false;
            switch (xmlFieldIdentifier)
            {
                case "RevisionDate":
                case "DateCreated":
                case "DateApproved":
                case "DateForReview":
                case "DateReviewed":
                case "ToClientDate":
                case "PlannedSubmissionDate":
                case "MilestoneDate":
                case "Date1":
                case "Date2":
                    return true;
                default:
                    return false;
            }
        }

        private static DateTime? TryCoerceToDateTime(object o)
        {
            if (o == null || o == DBNull.Value) return null;
            if (o is DateTime d) return d;
            if (o is DateTimeOffset dto) return dto.UtcDateTime;
            if (o is string s && !string.IsNullOrWhiteSpace(s))
            {
                if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind | DateTimeStyles.AllowWhiteSpaces, out var t))
                    return t;
                if (DateTime.TryParse(s, CultureInfo.CurrentCulture, DateTimeStyles.None, out t))
                    return t;
            }
            try
            {
                return Convert.ToDateTime(o, CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }

        private static string[] GetColumnAliasesForIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
                return Array.Empty<string>();

            switch (identifier)
            {
                case "DocumentNumber":
                    return new[] { "docno", "DocumentNumber" };
                case "Title":
                    return new[] { "title", "Title" };
                case "DocumentTypeId":
                    return new[] { "doctype", "DocumentTypeId" };
                case "DocumentStatusId":
                    return new[] { "docstatus", "statusid", "DocumentStatusId" };
                case "Revision":
                    return new[] { "revision", "Revision" };
                case "RevisionDate":
                    return new[] { "RevisionDate", "revisionDate", "revisiondate" };
                case "PackageNumber":
                    return new[] { "PackageNumber", "packagenumber" };
                case "ContractNumber":
                    return new[] { "ContractNumber", "contractnumber" };
                case "VendorDocumentNumber":
                    return new[] { "VendorDocumentNumber", "vendordocumentnumber" };
                case "ContractorDocumentNumber":
                    return new[] { "ContractorDocumentNumber", "contractordocumentnumber" };
                case "TagNumber":
                    return new[] { "TagNumber", "tagNumber" };
                case "Discipline":
                    return new[] { "Discipline", "discipline" };
                case "Author":
                    return new[] { "Author", "author" };
                case "AuthorisedBy":
                    return new[] { "AuthorisedBy", "authorisedBy" };
                case "Comments":
                    return new[] { "Comments", "comments" };
                case "Comments2":
                    return new[] { "Comments2", "comments2" };
                case "Reference":
                    return new[] { "Reference", "reference" };
                case "Category":
                    return new[] { "Category", "category" };
                case "VendorRev":
                    return new[] { "VendorRev", "vendorrev" };
                case "ContractorRev":
                    return new[] { "ContractorRev", "contractorrev" };
                case "Vdrcode":
                    return new[] { "Vdrcode", "vdrcode" };
                case "PrintSize":
                    return new[] { "PrintSize", "printSize" };
                case "Attribute1":
                    return new[] { "Attribute1", "attribute1" };
                case "Attribute2":
                    return new[] { "Attribute2", "attribute2" };
                case "Attribute3":
                    return new[] { "Attribute3", "attribute3" };
                case "Attribute4":
                    return new[] { "Attribute4", "attribute4" };
                case "ProjectField1":
                    return new[] { "ProjectField1", "projectField1" };
                case "ProjectField2":
                    return new[] { "ProjectField2", "projectField2" };
                case "ProjectField3":
                    return new[] { "ProjectField3", "projectField3" };
                case "ReviewStatusId":
                    return new[] { "ReviewStatusId", "reviewstatus", "reviewStatus" };
                default:
                    return new[]
                    {
                        identifier,
                        identifier.Length > 1
                            ? char.ToLowerInvariant(identifier[0]) + identifier.Substring(1)
                            : identifier.ToLowerInvariant()
                    };
            }
        }

        /// <summary>
        /// Fechas para Register Document: mismo estilo que el search (ej. <c>"revisionDate": "2025-11-17T05:00:00.000Z"</c>).
        /// ISO 8601 con milisegundos y sufijo Z (UTC).
        /// </summary>
        private static string FormatAconexRegisterDateXml(DateTime date)
        {
            DateTime utc;
            switch (date.Kind)
            {
                case DateTimeKind.Utc:
                    utc = date;
                    break;
                case DateTimeKind.Local:
                    utc = date.ToUniversalTime();
                    break;
                default:
                    // Sin zona (típico de SQL Server): interpretar como instante UTC para alinear con Aconex.
                    utc = DateTime.SpecifyKind(date, DateTimeKind.Utc);
                    break;
            }

            return utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        }

        private static string FormatRegisterValue(object o, string dataType, string xmlFieldIdentifier = null)
        {
            if (o == null || o == DBNull.Value) return null;
            string dt = string.IsNullOrWhiteSpace(dataType) ? "STRING" : dataType.Trim();

            if (string.Equals(dt, "BOOLEAN", StringComparison.OrdinalIgnoreCase))
            {
                if (o is bool b) return b ? "true" : "false";
                string s = o.ToString().Trim();
                if (string.Equals(s, "1", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "true", StringComparison.OrdinalIgnoreCase))
                    return "true";
                if (string.Equals(s, "0", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "false", StringComparison.OrdinalIgnoreCase))
                    return "false";
                return s;
            }

            if (string.Equals(dt, "INTEGER", StringComparison.OrdinalIgnoreCase) || string.Equals(dt, "LONG", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return Convert.ToInt64(o, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                }
                catch
                {
                    return o.ToString().Trim();
                }
            }

            if (string.Equals(dt, "DOUBLE", StringComparison.OrdinalIgnoreCase) || string.Equals(dt, "RATIO", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return Convert.ToDouble(o, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                }
                catch
                {
                    return o.ToString().Trim();
                }
            }

            bool dateOnly =
                string.Equals(dt, "DATE", StringComparison.OrdinalIgnoreCase)
                || IsAconexDateOnlyXmlField(xmlFieldIdentifier);

            DateTime? maybeDate = TryCoerceToDateTime(o);
            if (maybeDate.HasValue)
            {
                if (dateOnly)
                    return FormatAconexRegisterDateXml(maybeDate.Value);
                return maybeDate.Value.ToString("o", CultureInfo.InvariantCulture);
            }

            return o.ToString().Trim();
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
        /// Register Document no permite dos documentos con el mismo <c>DocumentNumber</c> en el proyecto.
        /// </summary>
        private static bool ResponseIndicatesFieldValueAlreadyExists(string responseText)
        {
            if (string.IsNullOrWhiteSpace(responseText)) return false;
            return responseText.IndexOf("FIELD_VALUE_ALREADY_EXISTS", StringComparison.OrdinalIgnoreCase) >= 0;
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

    /// <summary>
    /// Un campo del schema <c>EntityCreationSchemaFields</c> del GET register/schema de Aconex.
    /// </summary>
    public sealed class AconexRegisterSchemaField
    {
        public string Identifier { get; set; }
        public string MandatoryStatus { get; set; }
        public string DataType { get; set; }
        public bool IsMultiValue { get; set; }
    }

    /// <summary>
    /// Par Id/Value de un campo tipo lista en el schema (p. ej. estados y tipos de documento del proyecto).
    /// </summary>
    public sealed class AconexSchemaValueOption
    {
        public string Id { get; set; }
        public string Value { get; set; }
    }

    /// <summary>
    /// Resultado del parseo de <c>register/schema</c>: campos de creación + listas de valores permitidos por identificador.
    /// </summary>
    public sealed class AconexRegisterSchemaSnapshot
    {
        /// <summary>Atributo <c>autoNumberingEnabled</c> del nodo <c>RegisterSchema</c> en GET register/schema.</summary>
        public bool AutoNumberingEnabled { get; set; }

        public IReadOnlyList<AconexRegisterSchemaField> Fields { get; set; }
        /// <summary>Clave = <see cref="AconexRegisterSchemaField.Identifier"/> (ej. DocumentStatusId, DocumentTypeId).</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<AconexSchemaValueOption>> PicklistsByIdentifier { get; set; }
    }

    /// <summary>
    /// Parsea el XML del endpoint <c>GET /api/projects/{{id}}/register/schema</c> y extrae los campos de creación de documento.
    /// </summary>
    public static class AconexRegisterSchemaParser
    {
        /// <summary>
        /// Parsea campos de creación y listas SchemaValue (Id/Value) por identificador.
        /// </summary>
        public static AconexRegisterSchemaSnapshot ParseSnapshot(string schemaXml)
        {
            var fields = ParseEntityCreationFields(schemaXml);
            var picklists = ParsePicklistValuesByIdentifier(schemaXml);
            return new AconexRegisterSchemaSnapshot
            {
                AutoNumberingEnabled = ParseAutoNumberingEnabled(schemaXml),
                Fields = fields,
                PicklistsByIdentifier = picklists
            };
        }

        private static bool ParseAutoNumberingEnabled(string schemaXml)
        {
            if (string.IsNullOrWhiteSpace(schemaXml)) return false;
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(schemaXml);
                XmlNode root = doc.SelectSingleNode("//*[local-name()='RegisterSchema']");
                if (root?.Attributes == null) return false;
                XmlNode a = root.Attributes.GetNamedItem("autoNumberingEnabled");
                return a != null && string.Equals(a.Value?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Por cada campo bajo EntityCreationSchemaFields, recoge los <c>SchemaValue</c> (Id + Value) agrupados por <c>Identifier</c>.
        /// </summary>
        public static IReadOnlyDictionary<string, IReadOnlyList<AconexSchemaValueOption>> ParsePicklistValuesByIdentifier(string schemaXml)
        {
            var lists = new Dictionary<string, List<AconexSchemaValueOption>>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(schemaXml))
                return EmptyPicklists();

            var doc = new XmlDocument();
            doc.LoadXml(schemaXml);

            XmlNode container = doc.SelectSingleNode("//*[local-name()='EntityCreationSchemaFields']");
            if (container == null)
                return EmptyPicklists();

            XmlNodeList nodes = container.SelectNodes(".//*[local-name()='SingleValueSchemaField' or local-name()='MultiValueSchemaField']");
            if (nodes == null || nodes.Count == 0)
                return EmptyPicklists();

            foreach (XmlNode n in nodes)
            {
                string id = GetChildText(n, "Identifier");
                if (string.IsNullOrWhiteSpace(id))
                    continue;
                id = id.Trim();

                if (!lists.TryGetValue(id, out List<AconexSchemaValueOption> list))
                {
                    list = new List<AconexSchemaValueOption>();
                    lists[id] = list;
                }

                XmlNodeList schemaValues = n.SelectNodes(".//*[local-name()='SchemaValue']");
                if (schemaValues == null) continue;

                foreach (XmlNode sv in schemaValues)
                {
                    string vid = GetChildText(sv, "Id");
                    string vval = GetChildText(sv, "Value");
                    if (string.IsNullOrWhiteSpace(vid) && string.IsNullOrWhiteSpace(vval))
                        continue;
                    list.Add(new AconexSchemaValueOption { Id = vid?.Trim(), Value = vval?.Trim() });
                }
            }

            var result = new Dictionary<string, IReadOnlyList<AconexSchemaValueOption>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in lists)
                result[kv.Key] = kv.Value;

            return result;
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<AconexSchemaValueOption>> EmptyPicklists()
        {
            return new Dictionary<string, IReadOnlyList<AconexSchemaValueOption>>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Extrae <see cref="SingleValueSchemaField"/> y <see cref="MultiValueSchemaField"/> bajo <c>EntityCreationSchemaFields</c>, en orden de aparición.
        /// </summary>
        public static IReadOnlyList<AconexRegisterSchemaField> ParseEntityCreationFields(string schemaXml)
        {
            if (string.IsNullOrWhiteSpace(schemaXml))
                return Array.Empty<AconexRegisterSchemaField>();

            var doc = new XmlDocument();
            doc.LoadXml(schemaXml);

            XmlNode container = doc.SelectSingleNode("//*[local-name()='EntityCreationSchemaFields']");
            if (container == null)
                return Array.Empty<AconexRegisterSchemaField>();

            XmlNodeList nodes = container.SelectNodes(".//*[local-name()='SingleValueSchemaField' or local-name()='MultiValueSchemaField']");
            if (nodes == null || nodes.Count == 0)
                return Array.Empty<AconexRegisterSchemaField>();

            var list = new List<AconexRegisterSchemaField>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (XmlNode n in nodes)
            {
                string id = GetChildText(n, "Identifier");
                if (string.IsNullOrWhiteSpace(id))
                    continue;
                id = id.Trim();
                if (seen.Contains(id))
                    continue;
                seen.Add(id);

                bool isMulti = string.Equals(n.LocalName, "MultiValueSchemaField", StringComparison.OrdinalIgnoreCase);
                string mandatory = GetMandatoryStatus(n);
                string dataType = GetChildText(n, "DataType") ?? "STRING";

                list.Add(new AconexRegisterSchemaField
                {
                    Identifier = id,
                    MandatoryStatus = mandatory ?? "NOT_MANDATORY",
                    DataType = dataType.Trim(),
                    IsMultiValue = isMulti
                });
            }

            return list;
        }

        private static string GetMandatoryStatus(XmlNode fieldNode)
        {
            XmlNode m = fieldNode.SelectSingleNode(".//*[local-name()='MandatoryStatus']");
            if (m != null && !string.IsNullOrWhiteSpace(m.InnerText))
                return m.InnerText.Trim();

            if (fieldNode.Attributes != null)
            {
                foreach (XmlAttribute a in fieldNode.Attributes)
                {
                    if (string.Equals(a.LocalName, "MandatoryStatus", StringComparison.OrdinalIgnoreCase))
                        return a.Value?.Trim();
                }
            }

            XmlNode attrs = fieldNode.SelectSingleNode(".//*[local-name()='Attributes']");
            if (attrs != null)
            {
                m = attrs.SelectSingleNode(".//*[local-name()='MandatoryStatus']");
                if (m != null && !string.IsNullOrWhiteSpace(m.InnerText))
                    return m.InnerText.Trim();
            }

            return null;
        }

        private static string GetChildText(XmlNode parent, string localName)
        {
            if (parent == null) return null;
            XmlNode n = parent.SelectSingleNode(".//*[local-name()='" + localName + "']");
            if (n == null || string.IsNullOrWhiteSpace(n.InnerText))
                return null;
            return n.InnerText.Trim();
        }
    }
}
