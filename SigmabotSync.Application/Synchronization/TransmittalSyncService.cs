using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SigmabotSync.Application.Common;
using SigmabotSync.Application.FileExtraction;
using SigmabotSync.Domain.Entities;
using SigmabotSync.Domain.Models;
using SigmabotSync.Domain.Models.Extraction;
using SigmabotSync.Domain.Models.Synchronization;
using SigmabotSync.Domain.Ports;

namespace SigmabotSync.Application.Synchronization
{
    public sealed class TransmittalSyncService
    {
        /// <summary>Bandeja en el proyecto origen donde aparecen los transmitals a replicar.</summary>
        private const string SourceMailbox = "inbox";

        private readonly IMailTransmittalReadPort _mailRead;
        private readonly IAconexRegisterWritePort _registerWrite;
        private readonly IAconexRegisterDocumentContentPort _documentContent;
        private readonly IAconexRegisterSearchPort _registerSearch;
        private readonly IAconexRegisterMetadataPort _registerMetadata;
        private readonly ITransmittalSyncFieldMapPort _fieldMap;
        private readonly ITransmittalSyncStatePort _state;
        private readonly IAconexDocumentCatalogPort _documentCatalog;

        public TransmittalSyncService(
            IMailTransmittalReadPort mailRead,
            IAconexRegisterWritePort registerWrite,
            IAconexRegisterDocumentContentPort documentContent,
            IAconexRegisterSearchPort registerSearch,
            IAconexRegisterMetadataPort registerMetadata,
            ITransmittalSyncFieldMapPort fieldMap,
            ITransmittalSyncStatePort state,
            IAconexDocumentCatalogPort documentCatalog)
        {
            _mailRead = mailRead ?? throw new ArgumentNullException(nameof(mailRead));
            _registerWrite = registerWrite ?? throw new ArgumentNullException(nameof(registerWrite));
            _documentContent = documentContent ?? throw new ArgumentNullException(nameof(documentContent));
            _registerSearch = registerSearch ?? throw new ArgumentNullException(nameof(registerSearch));
            _registerMetadata = registerMetadata ?? throw new ArgumentNullException(nameof(registerMetadata));
            _fieldMap = fieldMap ?? throw new ArgumentNullException(nameof(fieldMap));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _documentCatalog = documentCatalog ?? throw new ArgumentNullException(nameof(documentCatalog));
        }

        /// <summary>
        /// Lee transmittals del proyecto origen (<see cref="SourceMailbox"/>) y crea/actualiza documentos en el registro del proyecto destino.
        /// Los archivos se descargan del registro del origen.
        /// </summary>
        public async Task<TransmittalSyncProjectResult> ProcessCrossProjectAsync(
            TransmittalSyncRunRequest request,
            ProyectoSyncItem sourceProject,
            ProyectoSyncItem targetProject,
            Action<string> log,
            CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (sourceProject == null) throw new ArgumentNullException(nameof(sourceProject));
            if (targetProject == null) throw new ArgumentNullException(nameof(targetProject));

            DateTime hastaUtc = DateTime.UtcNow;
            DateTime desdeUtc = hastaUtc.AddDays(-Math.Max(1, request.DiasLookback));

            string fechaInicio = desdeUtc.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            string fechaFin = hastaUtc.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

            log?.Invoke(
                $"Buscando transmitals {SourceMailbox} origen {sourceProject.Label} ({sourceProject.ProjectId}) " +
                $"→ registro destino {targetProject.Label} ({targetProject.ProjectId}), " +
                $"sentdate:[{fechaInicio} TO {fechaFin}] ({request.DiasLookback} días lookback)...");

            var mails = await _mailRead.ListTransmittalsAsync(
                request.BaseUrl,
                sourceProject.ProjectId,
                request.AuthorizationHeaderBase64,
                desdeUtc,
                hastaUtc,
                SourceMailbox,
                cancellationToken).ConfigureAwait(false);

            var result = new TransmittalSyncProjectResult
            {
                SourceProjectId = sourceProject.ProjectId,
                TargetProjectId = targetProject.ProjectId,
                Mailbox = SourceMailbox,
                TotalMails = mails.Count
            };
            log?.Invoke($"Transmitals encontrados en {SourceMailbox} origen: {mails.Count}");
            if (mails.Count == 0)
            {
                log?.Invoke(
                    $"Sin resultados. Verifique DiasLookbackTransmittal (actual={request.DiasLookback}) " +
                    $"y que el transmittal esté en {SourceMailbox} del proyecto {sourceProject.ProjectId} " +
                    $"con sentdate entre {fechaInicio} y {fechaFin}.");
                return result;
            }

            IReadOnlyList<TransmittalSyncCampoMapeoItem> fieldMappings;
            AconexRegisterSchemaSnapshot targetSchema = null;
            AconexDocumentCatalog documentCatalog = AconexDocumentCatalog.Empty;
            try
            {
                fieldMappings = await _fieldMap.GetMappingsAsync(
                    request.IdTrabajo, sourceProject.ProjectId, targetProject.ProjectId, cancellationToken).ConfigureAwait(false);

                if (fieldMappings.Count > 0)
                {
                    documentCatalog = await _documentCatalog.LoadCatalogAsync(
                        request.IdTrabajo, sourceProject.ProjectId, targetProject.ProjectId, cancellationToken).ConfigureAwait(false);
                    targetSchema = await LoadTargetRegisterSchemaAsync(request, targetProject, cancellationToken).ConfigureAwait(false);
                    log?.Invoke(
                        $"Homologación ({sourceProject.ProjectId} → {targetProject.ProjectId}): " +
                        $"{fieldMappings.Count} campos + schema destino ({targetSchema.Fields?.Count ?? 0} campos).");
                    log?.Invoke(
                        $"  Catálogos BD: TiposDocumentos={documentCatalog.IdTipoPorNombre.Count}, " +
                        $"EstatusDocumentos={documentCatalog.IdEstatusPorNombre.Count}, " +
                        $"EquivDiscipline={documentCatalog.EquivalenciaDiscipline.Count}, " +
                        $"EquivTipoDoc={documentCatalog.EquivalenciaTipoDocumento.Count}");
                    foreach (var m in fieldMappings)
                        log?.Invoke($"    {m.CampoOrigen} → {m.CampoDestino}{(m.EsObligatorio ? " [oblig]" : "")}{(string.IsNullOrWhiteSpace(m.Catalogo) ? "" : $" cat={m.Catalogo}")}{(string.IsNullOrWhiteSpace(m.ValorDefault) ? "" : $" default={m.ValorDefault}")}");
                }
                else
                {
                    log?.Invoke("  AVISO: sin filas en TransmittalSyncCampoProyecto; se usará GET register/schema (modo legacy).");
                    targetSchema = await LoadTargetRegisterSchemaAsync(request, targetProject, cancellationToken).ConfigureAwait(false);
                    log?.Invoke($"Schema registro destino ({targetProject.Label}): {targetSchema.Fields?.Count ?? 0} campos.");
                }
            }
            catch (Exception ex)
            {
                result.Errors++;
                log?.Invoke($"ERROR preparando registro destino {targetProject.ProjectId}: {ex.Message}");
                return result;
            }

            foreach (var mail in mails)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(mail.MailId))
                    continue;

                if (await _state.IsMailProcessedAsync(request.IdTrabajo, sourceProject.ProjectId, mail.MailId, cancellationToken).ConfigureAwait(false))
                {
                    result.SkippedAlreadyProcessed++;
                    continue;
                }

                try
                {
                    var detail = await _mailRead.GetTransmittalDetailAsync(
                        request.BaseUrl,
                        sourceProject.ProjectId,
                        mail.MailId,
                        request.AuthorizationHeaderBase64,
                        cancellationToken).ConfigureAwait(false);

                    if (detail.Attachments == null || detail.Attachments.Count == 0)
                    {
                        log?.Invoke($"Mail {mail.MailNo} ({mail.MailId}): sin adjuntos registrados.");
                        await _state.MarkMailProcessedAsync(request.IdTrabajo, sourceProject.ProjectId, mail.MailId, cancellationToken).ConfigureAwait(false);
                        result.ProcessedMails++;
                        continue;
                    }

                    foreach (var attachment in detail.Attachments)
                    {
                        if (attachment.IsPlaceholder)
                        {
                            bool ok = await TryRegisterPlaceholderAsync(
                                request, sourceProject, targetProject, targetSchema, documentCatalog, fieldMappings, attachment, log, cancellationToken).ConfigureAwait(false);
                            if (ok) result.PlaceholdersCreated++;
                        }
                        else
                        {
                            bool ok = await TryApplyFileFromTransmittalAsync(
                                request, sourceProject, targetProject, targetSchema, documentCatalog, fieldMappings, attachment, log, cancellationToken).ConfigureAwait(false);
                            if (ok) result.FilesApplied++;
                        }
                    }

                    await _state.MarkMailProcessedAsync(request.IdTrabajo, sourceProject.ProjectId, mail.MailId, cancellationToken).ConfigureAwait(false);
                    result.ProcessedMails++;
                }
                catch (Exception ex)
                {
                    result.Errors++;
                    log?.Invoke($"ERROR mail {mail.MailId}: {ex.Message}");
                }
            }

            return result;
        }

        private async Task<bool> TryRegisterPlaceholderAsync(
            TransmittalSyncRunRequest request,
            ProyectoSyncItem sourceProject,
            ProyectoSyncItem targetProject,
            AconexRegisterSchemaSnapshot targetSchema,
            AconexDocumentCatalog documentCatalog,
            IReadOnlyList<TransmittalSyncCampoMapeoItem> fieldMappings,
            TransmittalDocumentAttachment attachment,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(attachment.DocumentNo))
            {
                log?.Invoke("Marcador omitido: DocumentNo vacío.");
                return false;
            }

            string revision = string.IsNullOrWhiteSpace(attachment.Revision) ? "A" : attachment.Revision.Trim();
            string existingId = await ResolveTargetDocumentIdAsync(
                request, targetProject, attachment.DocumentNo, revision, log, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(existingId))
            {
                log?.Invoke($"Documento ya existe en {targetProject.Label}: {attachment.DocumentNo} rev {revision} → {existingId}");
                return false;
            }

            string xml = await BuildRegisterXmlAsync(
                request, sourceProject, targetSchema, documentCatalog, fieldMappings, attachment, revision, hasFile: false, log, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(xml))
                return false;

            string boundary = AconexRegisterMultipart.CreateBoundary();
            string body = AconexRegisterMultipart.BuildRegisterBodyXmlOnly(xml, boundary);

            var response = await _registerWrite.PostRegisterDocumentAsync(
                request.BaseUrl,
                targetProject.ProjectId,
                request.AuthorizationHeaderBase64,
                request.IntegrationId,
                body,
                boundary,
                cancellationToken).ConfigureAwait(false);

            string responseText = response?.Body ?? "";
            if (response == null || !response.IsSuccessStatusCode)
            {
                log?.Invoke($"Register marcador falló ({attachment.DocumentNo}): {Truncate(responseText, 300)}");
                return false;
            }

            string localDocumentId = AconexRegisterResponseParser.ParseRegisterDocumentId(responseText);
            if (string.IsNullOrWhiteSpace(localDocumentId))
            {
                log?.Invoke($"Register marcador sin DocumentId en respuesta ({attachment.DocumentNo}).");
                return false;
            }

            await _state.SaveLocalDocumentMappingAsync(
                request.IdTrabajo, targetProject.ProjectId, attachment.DocumentNo, revision, localDocumentId, cancellationToken).ConfigureAwait(false);

            log?.Invoke($"Marcador creado en destino: {attachment.DocumentNo} rev {revision} → {localDocumentId}");
            return true;
        }

        private async Task<bool> TryApplyFileFromTransmittalAsync(
            TransmittalSyncRunRequest request,
            ProyectoSyncItem sourceProject,
            ProyectoSyncItem targetProject,
            AconexRegisterSchemaSnapshot targetSchema,
            AconexDocumentCatalog documentCatalog,
            IReadOnlyList<TransmittalSyncCampoMapeoItem> fieldMappings,
            TransmittalDocumentAttachment attachment,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(attachment.DocumentNo))
            {
                log?.Invoke("Archivo omitido: DocumentNo vacío.");
                return false;
            }

            string revision = string.IsNullOrWhiteSpace(attachment.Revision) ? "A" : attachment.Revision.Trim();
            string sourceDocumentId = ResolveSourceDocumentId(attachment);
            if (string.IsNullOrWhiteSpace(sourceDocumentId))
            {
                log?.Invoke($"Archivo omitido ({attachment.DocumentNo}): sin DocumentId/RegisteredAs en transmittal.");
                return false;
            }

            string localDocumentId = await ResolveTargetDocumentIdAsync(
                request, targetProject, attachment.DocumentNo, revision, log, cancellationToken).ConfigureAwait(false);

            string tempFile = Path.Combine(Path.GetTempPath(), "sigmabot_sync_" + Guid.NewGuid().ToString("N") + Path.GetExtension(attachment.FileName ?? ".bin"));
            try
            {
                var download = await _documentContent.DownloadToFileAsync(
                    request.BaseUrl,
                    sourceProject.ProjectId,
                    sourceDocumentId,
                    tempFile,
                    request.AuthorizationHeaderBase64,
                    cancellationToken).ConfigureAwait(false);

                if (download.Status == AconexRegisterDocumentDownloadStatus.OmittedEmptyDocument)
                {
                    log?.Invoke($"Descarga vacía ({attachment.DocumentNo}): documento sin archivo en registro origen.");
                    return false;
                }

                if (download.Status != AconexRegisterDocumentDownloadStatus.Saved || !File.Exists(tempFile))
                {
                    log?.Invoke($"Descarga falló ({attachment.DocumentNo}): {download.Message ?? download.Status.ToString()}");
                    return false;
                }

                byte[] bytes = File.ReadAllBytes(tempFile);
                string fileBase64 = Convert.ToBase64String(bytes);
                string fileName = string.IsNullOrWhiteSpace(attachment.FileName) ? attachment.DocumentNo + ".bin" : attachment.FileName;

                if (string.IsNullOrWhiteSpace(localDocumentId))
                {
                    return await RegisterWithFileAsync(
                        request, sourceProject, targetProject, targetSchema, documentCatalog, fieldMappings, attachment, revision, fileName, fileBase64, log, cancellationToken).ConfigureAwait(false);
                }

                return await SupersedeWithFileAsync(
                    request, sourceProject, targetProject, targetSchema, documentCatalog, fieldMappings, attachment, revision, localDocumentId, fileName, fileBase64, log, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                TryDeleteFile(tempFile);
            }
        }

        private async Task<bool> RegisterWithFileAsync(
            TransmittalSyncRunRequest request,
            ProyectoSyncItem sourceProject,
            ProyectoSyncItem targetProject,
            AconexRegisterSchemaSnapshot targetSchema,
            AconexDocumentCatalog documentCatalog,
            IReadOnlyList<TransmittalSyncCampoMapeoItem> fieldMappings,
            TransmittalDocumentAttachment attachment,
            string revision,
            string fileName,
            string fileBase64,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            string xml = await BuildRegisterXmlAsync(
                request, sourceProject, targetSchema, documentCatalog, fieldMappings, attachment, revision, hasFile: true, log, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(xml))
                return false;

            string boundary = AconexRegisterMultipart.CreateBoundary();
            string body = AconexRegisterMultipart.BuildRegisterBody(xml, fileName, fileBase64, boundary);

            var response = await _registerWrite.PostRegisterDocumentAsync(
                request.BaseUrl,
                targetProject.ProjectId,
                request.AuthorizationHeaderBase64,
                request.IntegrationId,
                body,
                boundary,
                cancellationToken).ConfigureAwait(false);

            string responseText = response?.Body ?? "";
            if (response == null || !response.IsSuccessStatusCode)
            {
                log?.Invoke($"Register con archivo falló en destino ({attachment.DocumentNo}): {Truncate(responseText, 300)}");
                return false;
            }

            string localDocumentId = AconexRegisterResponseParser.ParseRegisterDocumentId(responseText);
            if (!string.IsNullOrWhiteSpace(localDocumentId))
            {
                await _state.SaveLocalDocumentMappingAsync(
                    request.IdTrabajo, targetProject.ProjectId, attachment.DocumentNo, revision, localDocumentId, cancellationToken).ConfigureAwait(false);
            }

            log?.Invoke($"Documento registrado en destino: {attachment.DocumentNo} rev {revision} → {localDocumentId ?? "?"}");
            return true;
        }

        private async Task<bool> SupersedeWithFileAsync(
            TransmittalSyncRunRequest request,
            ProyectoSyncItem sourceProject,
            ProyectoSyncItem targetProject,
            AconexRegisterSchemaSnapshot targetSchema,
            AconexDocumentCatalog documentCatalog,
            IReadOnlyList<TransmittalSyncCampoMapeoItem> fieldMappings,
            TransmittalDocumentAttachment attachment,
            string revision,
            string localDocumentId,
            string fileName,
            string fileBase64,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            string xml = await BuildRegisterXmlAsync(
                request, sourceProject, targetSchema, documentCatalog, fieldMappings, attachment, revision, hasFile: true, log, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(xml))
                return false;

            string boundary = AconexRegisterMultipart.CreateBoundary();
            string body = AconexRegisterMultipart.BuildRegisterBody(xml, fileName, fileBase64, boundary);

            var response = await _registerWrite.PostSupersedeDocumentAsync(
                request.BaseUrl,
                targetProject.ProjectId,
                localDocumentId,
                request.AuthorizationHeaderBase64,
                request.IntegrationId,
                body,
                boundary,
                cancellationToken).ConfigureAwait(false);

            string responseText = response?.Body ?? "";
            if (response == null || !response.IsSuccessStatusCode)
            {
                log?.Invoke($"Supersede falló en destino ({attachment.DocumentNo} → {localDocumentId}): {Truncate(responseText, 300)}");
                return false;
            }

            log?.Invoke($"Supersede OK en destino: {attachment.DocumentNo} rev {revision} → {localDocumentId}");
            return true;
        }

        /// <summary>
        /// Resuelve el DocumentId en el proyecto destino: primero mapeo local, luego búsqueda en el register Aconex.
        /// Si lo encuentra en el register, persiste el mapeo para futuros syncs.
        /// </summary>
        private async Task<string> ResolveTargetDocumentIdAsync(
            TransmittalSyncRunRequest request,
            ProyectoSyncItem targetProject,
            string documentNo,
            string revision,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            string mappedId = await _state.GetLocalDocumentIdAsync(
                request.IdTrabajo, targetProject.ProjectId, documentNo, revision, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(mappedId))
                return mappedId;

            string foundId = await FindDocumentInTargetRegisterAsync(
                request, targetProject.ProjectId, documentNo, revision, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(foundId))
                return null;

            await _state.SaveLocalDocumentMappingAsync(
                request.IdTrabajo, targetProject.ProjectId, documentNo, revision, foundId, cancellationToken).ConfigureAwait(false);

            log?.Invoke($"Mapeo recuperado en {targetProject.Label}: {documentNo} rev {revision} → {foundId}");
            return foundId;
        }

        private async Task<string> FindDocumentInTargetRegisterAsync(
            TransmittalSyncRunRequest request,
            string targetProjectId,
            string documentNo,
            string revision,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(documentNo))
                return null;

            var returnFields = new List<string> { "docno", "revision" };

            var searchResult = await _registerSearch.SearchRegisterPageAsync(
                request.BaseUrl,
                targetProjectId,
                request.OrgId ?? "",
                request.UserId ?? "",
                request.AuthorizationHeaderBase64,
                returnFields,
                25,
                1,
                throwIfNotSuccess: false,
                cancellationToken,
                filterDocumentNo: documentNo.Trim(),
                filterRevision: IsWildcardRevision(revision) ? null : NormalizeRevision(revision)).ConfigureAwait(false);

            var page = searchResult?.Page;

            if (page?.searchResults == null || page.searchResults.Count == 0)
                return null;

            Searchresult match = page.searchResults.FirstOrDefault(r =>
                string.Equals(r.DocumentNumber?.Trim(), documentNo.Trim(), StringComparison.OrdinalIgnoreCase) &&
                (IsWildcardRevision(revision) ||
                 string.Equals(NormalizeRevision(r.Revision), NormalizeRevision(revision), StringComparison.OrdinalIgnoreCase)));

            if (match == null)
            {
                match = page.searchResults.FirstOrDefault(r =>
                    string.Equals(r.DocumentNumber?.Trim(), documentNo.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            if (match == null || match.Id <= 0)
                return null;

            return match.Id.ToString();
        }

        private static string BuildDocumentSearchQuery(string documentNo, string revision)
        {
            string doc = EscapeLuceneQuoted(documentNo.Trim());
            if (IsWildcardRevision(revision))
                return $"docno:\"{doc}\"";
            string rev = EscapeLuceneQuoted(NormalizeRevision(revision));
            return $"docno:\"{doc}\" AND revision:\"{rev}\"";
        }

        private static bool IsWildcardRevision(string revision)
        {
            if (string.IsNullOrWhiteSpace(revision))
                return true;
            string trimmed = revision.Trim();
            return trimmed == "-" || trimmed == "—";
        }

        private static string NormalizeRevision(string revision)
        {
            if (IsWildcardRevision(revision))
                return "A";
            return revision.Trim();
        }

        private static string ResolveSourceDocumentId(TransmittalDocumentAttachment attachment)
        {
            if (attachment == null)
                return null;
            if (!string.IsNullOrWhiteSpace(attachment.DocumentId))
                return attachment.DocumentId.Trim();
            if (!string.IsNullOrWhiteSpace(attachment.RegisteredAs))
                return attachment.RegisteredAs.Trim();
            return null;
        }

        private static string EscapeLuceneQuoted(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private async Task<AconexRegisterSchemaSnapshot> LoadTargetRegisterSchemaAsync(
            TransmittalSyncRunRequest request,
            ProyectoSyncItem targetProject,
            CancellationToken cancellationToken)
        {
            string schemaXml = await _registerWrite.GetRegisterSchemaXmlAsync(
                request.BaseUrl,
                targetProject.ProjectId,
                request.AuthorizationHeaderBase64,
                request.IntegrationId,
                cancellationToken).ConfigureAwait(false);

            return AconexRegisterSchemaParser.ParseSnapshot(schemaXml);
        }

        private async Task<string> BuildRegisterXmlAsync(
            TransmittalSyncRunRequest request,
            ProyectoSyncItem sourceProject,
            AconexRegisterSchemaSnapshot targetSchema,
            AconexDocumentCatalog documentCatalog,
            IReadOnlyList<TransmittalSyncCampoMapeoItem> fieldMappings,
            TransmittalDocumentAttachment attachment,
            string revision,
            bool hasFile,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            IReadOnlyDictionary<string, string> sourceHints = await FetchSourceDocumentHintsAsync(
                request, sourceProject, attachment, revision, fieldMappings, log, cancellationToken).ConfigureAwait(false);

            LogCamposObtenidos(attachment, revision, sourceHints, log);

            string xml;
            string error;
            if (fieldMappings != null && fieldMappings.Count > 0)
            {
                xml = TransmittalRegisterXmlBuilder.BuildFromFieldMappings(
                    fieldMappings, targetSchema, documentCatalog, attachment, revision, hasFile, sourceHints, out error);
            }
            else
            {
                LogRegisterDiagnostics(targetSchema, attachment.DocumentNo, sourceHints, log);
                xml = TransmittalRegisterXmlBuilder.Build(
                    targetSchema, attachment, revision, hasFile, sourceHints, out error);
            }

            if (string.IsNullOrWhiteSpace(xml))
            {
                log?.Invoke($"No se pudo armar XML Register ({attachment.DocumentNo}): {error}");
                return null;
            }

            log?.Invoke(
                $"CAMPOS ENVIADOS A REGISTER destino ({attachment.DocumentNo}):{Environment.NewLine}" +
                TransmittalRegisterXmlBuilder.FormatXmlFieldLines(xml));

            return xml;
        }

        private static void LogCamposObtenidos(
            TransmittalDocumentAttachment attachment,
            string revision,
            IReadOnlyDictionary<string, string> sourceHints,
            Action<string> log)
        {
            if (log == null || attachment == null)
                return;

            string docNo = attachment.DocumentNo ?? "?";
            log.Invoke($"CAMPOS OBTENIDOS ({docNo}):");

            log.Invoke("  [Transmittal adjunto]");
            log.Invoke($"    DocumentNo={attachment.DocumentNo ?? ""}");
            log.Invoke($"    Title={attachment.Title ?? ""}");
            log.Invoke($"    Revision={attachment.Revision ?? ""} (usada: {revision})");
            log.Invoke($"    RevisionDate={attachment.RevisionDate ?? ""}");
            log.Invoke($"    Status={attachment.Status ?? ""}");
            log.Invoke($"    FileName={attachment.FileName ?? ""}");
            log.Invoke($"    FileSize={attachment.FileSize}");
            log.Invoke($"    DocumentId={attachment.DocumentId ?? ""}");
            log.Invoke($"    RegisteredAs={attachment.RegisteredAs ?? ""}");

            log.Invoke("  [Register documento origen]");
            if (sourceHints == null || sourceHints.Count == 0)
            {
                log.Invoke("    (vacío — no se encontró el doc en register/search del origen)");
                return;
            }

            foreach (var kv in sourceHints)
                log.Invoke($"    {kv.Key}={kv.Value}");
        }

        private static void LogRegisterDiagnostics(
            AconexRegisterSchemaSnapshot targetSchema,
            string documentNo,
            IReadOnlyDictionary<string, string> sourceHints,
            Action<string> log)
        {
            if (log == null)
                return;

            var missing = TransmittalRegisterXmlBuilder.ListMandatoryFieldsMissingInSource(targetSchema, sourceHints);
            if (missing.Count > 0)
            {
                log.Invoke(
                    $"  Nota ({documentNo}): obligatorios en destino sin dato en origen (mismo nombre): " +
                    string.Join(", ", missing));
            }
        }

        private async Task<IReadOnlyDictionary<string, string>> FetchSourceDocumentHintsAsync(
            TransmittalSyncRunRequest request,
            ProyectoSyncItem sourceProject,
            TransmittalDocumentAttachment attachment,
            string revision,
            IReadOnlyList<TransmittalSyncCampoMapeoItem> fieldMappings,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            var hints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(attachment?.Status))
            {
                hints["Status"] = attachment.Status.Trim();
                hints["statusid"] = attachment.Status.Trim();
            }
            if (!string.IsNullOrWhiteSpace(attachment?.RevisionDate))
                hints["RevisionDate"] = attachment.RevisionDate.Trim();

            string sourceDocumentId = ResolveSourceDocumentId(attachment);
            if (!string.IsNullOrWhiteSpace(sourceDocumentId))
            {
                var metadata = await _registerMetadata.GetRegisterMetadataAsync(
                    request.BaseUrl,
                    sourceProject.ProjectId,
                    sourceDocumentId,
                    request.AuthorizationHeaderBase64,
                    cancellationToken).ConfigureAwait(false);

                if (metadata != null)
                {
                    MergeRegisterMetadataHints(hints, metadata);
                    log?.Invoke(
                        $"  Register origen: metadata {sourceDocumentId} → " +
                        $"doctype={metadata.DocumentType ?? ""}, status={metadata.DocumentStatus ?? ""}");
                }
                else
                {
                    log?.Invoke(
                        $"  Register origen: sin metadata para DocumentId={sourceDocumentId} " +
                        $"(proyecto {sourceProject.ProjectId}).");
                }
            }

            if (string.IsNullOrWhiteSpace(attachment?.DocumentNo))
                return hints;

            IReadOnlyList<string> returnFields = BuildRegisterSearchReturnFields(fieldMappings);
            log?.Invoke($"  Register origen returnFields: {string.Join(", ", returnFields)}");

            var searchResult = await SearchSourceRegisterAsync(
                request, sourceProject, attachment, revision, returnFields, cancellationToken).ConfigureAwait(false);

            if (searchResult != null && (!searchResult.IsHttpSuccess || searchResult.HasAconexError))
            {
                IReadOnlyList<string> coreFields = BuildCoreRegisterSearchReturnFields(fieldMappings);
                if (coreFields.Count < returnFields.Count)
                {
                    log?.Invoke(
                        $"  Register origen: reintento search solo campos API estándar ({coreFields.Count} de {returnFields.Count})...");
                    searchResult = await SearchSourceRegisterAsync(
                        request, sourceProject, attachment, revision, coreFields, cancellationToken).ConfigureAwait(false);
                }
            }

            if (searchResult != null && (!searchResult.IsHttpSuccess || searchResult.HasAconexError))
            {
                string aconexMsg = !string.IsNullOrWhiteSpace(searchResult.AconexErrorDescription)
                    ? $"{searchResult.AconexErrorCode}: {searchResult.AconexErrorDescription}"
                    : Truncate(searchResult.ResponseBody, 400);
                log?.Invoke(
                    $"  Register origen: search error HTTP {searchResult.StatusCode} ({attachment.DocumentNo}): {aconexMsg}");
                if (!string.IsNullOrWhiteSpace(searchResult.RequestBody))
                    log?.Invoke($"  Register origen search request: {Truncate(searchResult.RequestBody, 600)}");
            }

            var page = searchResult?.Page;

            if (page?.searchResults == null || page.searchResults.Count == 0)
            {
                if (searchResult == null || (searchResult.IsHttpSuccess && !searchResult.HasAconexError))
                {
                    log?.Invoke(
                        $"  Register origen: sin resultados search para docno={attachment.DocumentNo} " +
                        $"(proyecto {sourceProject.ProjectId}, revisión {revision}).");
                }
                return hints;
            }

            Searchresult match = page.searchResults.FirstOrDefault(r =>
                string.Equals(r.DocumentNumber?.Trim(), attachment.DocumentNo.Trim(), StringComparison.OrdinalIgnoreCase) &&
                (IsWildcardRevision(revision) ||
                 string.Equals(NormalizeRevision(r.Revision), NormalizeRevision(revision), StringComparison.OrdinalIgnoreCase)));

            if (match == null && page.searchResults.Count == 1)
                match = page.searchResults[0];

            if (match == null)
                return hints;

            AddHint(hints, "DocumentNumber", match.DocumentNumber);
            AddHint(hints, "Title", match.Title);
            AddHint(hints, "Revision", match.Revision);
            AddHint(hints, "revisiondate", match.GetDynamicValue("revisiondate") ?? match.GetDynamicValue("revisionDate"));
            AddHint(hints, "RevisionDate", match.GetDynamicValue("revisionDate") ?? match.GetDynamicValue("revisiondate"));
            AddHint(hints, "doctype", match.GetDynamicValue("doctype") ?? match.GetDynamicValue("documentType"));
            AddHint(hints, "DocumentType", match.GetDynamicValue("documentType") ?? match.GetDynamicValue("doctype"));
            AddHint(hints, "statusid", match.GetDynamicValue("statusid") ?? match.GetDynamicValue("documentStatusId"));
            AddHint(hints, "DocumentStatus", match.GetDynamicValue("documentStatus") ?? match.GetDynamicValue("statusid"));
            AddHint(hints, "discipline", match.GetDynamicValue("discipline"));
            AddHint(hints, "author", match.GetDynamicValue("author"));
            AddHint(hints, "reviewstatus", match.GetDynamicValue("reviewstatus") ?? match.GetDynamicValue("reviewStatus"));
            AddHint(hints, "ReviewStatusId", match.GetDynamicValue("reviewStatus") ?? match.GetDynamicValue("reviewstatus"));

            if (match.ProjectFields != null)
            {
                foreach (var field in match.ProjectFields)
                {
                    if (field == null || string.IsNullOrWhiteSpace(field.Name) || string.IsNullOrWhiteSpace(field.Value))
                        continue;
                    AddHint(hints, field.Name.Trim(), field.Value.Trim());
                }
            }

            if (match.ExtensionData != null)
            {
                foreach (var kv in match.ExtensionData)
                {
                    if (kv.Value == null)
                        continue;
                    AddHint(hints, kv.Key, kv.Value.ToString());
                }
            }

            return hints;
        }

        private static void MergeRegisterMetadataHints(IDictionary<string, string> hints, DocumentMetadata metadata)
        {
            if (hints == null || metadata == null)
                return;

            AddHint(hints, "DocumentNumber", metadata.DocumentNumber);
            AddHint(hints, "Title", metadata.Title);
            AddHint(hints, "Revision", metadata.Revision);
            AddHint(hints, "RevisionDate", metadata.RevisionDate);
            AddHint(hints, "revisiondate", metadata.RevisionDate);
            AddHint(hints, "doctype", metadata.DocumentType);
            AddHint(hints, "DocumentType", metadata.DocumentType);
            AddHint(hints, "statusid", metadata.DocumentStatus);
            AddHint(hints, "Status", metadata.DocumentStatus);
            AddHint(hints, "DocumentStatus", metadata.DocumentStatus);
            AddHint(hints, "discipline", metadata.Discipline);
            AddHint(hints, "author", metadata.Author);
            AddHint(hints, "reviewstatus", metadata.ReviewStatus);
            AddHint(hints, "ReviewStatusId", metadata.ReviewStatus);
            AddHint(hints, "SelectList1", metadata.SelectList1);
            AddHint(hints, "SelectList2", metadata.SelectList2);
            AddHint(hints, "SelectList3", metadata.SelectList3);
            AddHint(hints, "ProjectField1", metadata.ProjectField1);
            AddHint(hints, "ProjectField2", metadata.ProjectField2);
        }

        private Task<AconexRegisterSearchResult> SearchSourceRegisterAsync(
            TransmittalSyncRunRequest request,
            ProyectoSyncItem sourceProject,
            TransmittalDocumentAttachment attachment,
            string revision,
            IReadOnlyList<string> returnFields,
            CancellationToken cancellationToken)
        {
            return _registerSearch.SearchRegisterPageAsync(
                request.BaseUrl,
                sourceProject.ProjectId,
                request.OrgId ?? "",
                request.UserId ?? "",
                request.AuthorizationHeaderBase64,
                returnFields,
                25,
                1,
                throwIfNotSuccess: false,
                cancellationToken,
                filterDocumentNo: attachment.DocumentNo.Trim(),
                filterRevision: IsWildcardRevision(revision) ? null : NormalizeRevision(revision));
        }

        /// <summary>Campos API estándar (sin *_singleSelect del destino que pueden no existir en origen).</summary>
        private static IReadOnlyList<string> BuildCoreRegisterSearchReturnFields(IReadOnlyList<TransmittalSyncCampoMapeoItem> fieldMappings)
        {
            var all = BuildRegisterSearchReturnFields(fieldMappings);
            return all
                .Where(f => !f.EndsWith("_singleSelect", StringComparison.OrdinalIgnoreCase)
                         && !f.EndsWith("_multiLineText", StringComparison.OrdinalIgnoreCase)
                         && !f.EndsWith("_singleLineText", StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Campos a pedir en register/search del origen. Mapea homologación → nombres API (docno, title, …).
        /// Omite destinos solo-transmittal (AutoNumber) y nombres XML inválidos (DocumentNumber).
        /// </summary>
        private static IReadOnlyList<string> BuildRegisterSearchReturnFields(IReadOnlyList<TransmittalSyncCampoMapeoItem> fieldMappings)
        {
            var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "docno",
                "revision"
            };

            if (fieldMappings != null)
            {
                foreach (TransmittalSyncCampoMapeoItem map in fieldMappings)
                {
                    if (map == null)
                        continue;
                    string apiField = ToRegisterSearchApiField(map.CampoOrigen, map.CampoDestino);
                    if (!string.IsNullOrWhiteSpace(apiField))
                        fields.Add(apiField);
                }
            }
            else
            {
                fields.Add("revisiondate");
                fields.Add("doctype");
                fields.Add("statusid");
                fields.Add("discipline");
                fields.Add("author");
                fields.Add("title");
                fields.Add("reviewstatus");
            }

            return fields.ToList();
        }

        /// <summary>Nombre en returnFields del POST register/search (consulta), no tag XML destino.</summary>
        private static string ToRegisterSearchApiField(string campoOrigen, string campoDestino)
        {
            string key = !string.IsNullOrWhiteSpace(campoOrigen) ? campoOrigen.Trim() : campoDestino?.Trim();
            if (string.IsNullOrWhiteSpace(key))
                return null;

            switch (key.ToLowerInvariant())
            {
                case "documentnumber":
                case "docno":
                    return "docno";
                case "title":
                    return "title";
                case "revision":
                    return "revision";
                case "documenttypeid":
                case "doctype":
                case "documenttype":
                    return "doctype";
                case "documentstatusid":
                case "statusid":
                case "documentstatus":
                    return "statusid";
                case "author":
                    return "author";
                case "revisiondate":
                    return "revisiondate";
                case "reviewstatusid":
                case "reviewstatus":
                    return "reviewstatus";
                case "discipline":
                    return "discipline";
                case "autonumber":
                case "hasfile":
                case "id":
                    return null;
                default:
                    if (key.EndsWith("_singleSelect", StringComparison.OrdinalIgnoreCase)
                        || key.EndsWith("_multiLineText", StringComparison.OrdinalIgnoreCase)
                        || key.EndsWith("_singleLineText", StringComparison.OrdinalIgnoreCase))
                        return key;
                    return null;
            }
        }

        private static void AddHint(IDictionary<string, string> hints, string key, string value)
        {
            if (hints == null || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                return;
            hints[key] = value.Trim();
        }

        private static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
                return text ?? "";
            return text.Substring(0, max) + "...";
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // ignorar limpieza temp
            }
        }
    }

    public sealed class TransmittalSyncProjectResult
    {
        public string SourceProjectId { get; set; }
        public string TargetProjectId { get; set; }
        public string Mailbox { get; set; }
        public int TotalMails { get; set; }
        public int ProcessedMails { get; set; }
        public int SkippedAlreadyProcessed { get; set; }
        public int PlaceholdersCreated { get; set; }
        public int FilesApplied { get; set; }
        public int Errors { get; set; }
    }
}
