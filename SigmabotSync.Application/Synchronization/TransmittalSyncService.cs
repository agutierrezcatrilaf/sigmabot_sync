using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SigmabotSync.Application.Common;
using SigmabotSync.Domain.Entities;
using SigmabotSync.Domain.Models.Synchronization;
using SigmabotSync.Domain.Ports;

namespace SigmabotSync.Application.Synchronization
{
    public sealed class TransmittalSyncService
    {
        private readonly IMailTransmittalReadPort _mailRead;
        private readonly IAconexRegisterWritePort _registerWrite;
        private readonly IAconexRegisterDocumentContentPort _documentContent;
        private readonly ITransmittalSyncStatePort _state;

        public TransmittalSyncService(
            IMailTransmittalReadPort mailRead,
            IAconexRegisterWritePort registerWrite,
            IAconexRegisterDocumentContentPort documentContent,
            ITransmittalSyncStatePort state)
        {
            _mailRead = mailRead ?? throw new ArgumentNullException(nameof(mailRead));
            _registerWrite = registerWrite ?? throw new ArgumentNullException(nameof(registerWrite));
            _documentContent = documentContent ?? throw new ArgumentNullException(nameof(documentContent));
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public async Task<TransmittalSyncProjectResult> ProcessProjectInboxAsync(
            TransmittalSyncRunRequest request,
            ProyectoSyncItem proyecto,
            Action<string> log,
            CancellationToken cancellationToken = default)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));

            DateTime hastaUtc = DateTime.UtcNow;
            DateTime desdeUtc = hastaUtc.AddDays(-Math.Max(1, request.DiasLookback));

            log?.Invoke($"Buscando transmitals inbox ({proyecto.Label}, {desdeUtc:yyyy-MM-dd}–{hastaUtc:yyyy-MM-dd} UTC)...");

            var mails = await _mailRead.ListInboxTransmittalsAsync(
                request.BaseUrl,
                proyecto.ProjectId,
                request.AuthorizationHeaderBase64,
                desdeUtc,
                hastaUtc,
                cancellationToken).ConfigureAwait(false);

            var result = new TransmittalSyncProjectResult { ProjectId = proyecto.ProjectId, TotalMails = mails.Count };
            log?.Invoke($"Transmitals encontrados: {mails.Count}");

            foreach (var mail in mails)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(mail.MailId))
                    continue;

                if (await _state.IsMailProcessedAsync(request.IdTrabajo, proyecto.ProjectId, mail.MailId, cancellationToken).ConfigureAwait(false))
                {
                    result.SkippedAlreadyProcessed++;
                    continue;
                }

                try
                {
                    var detail = await _mailRead.GetTransmittalDetailAsync(
                        request.BaseUrl,
                        proyecto.ProjectId,
                        mail.MailId,
                        request.AuthorizationHeaderBase64,
                        cancellationToken).ConfigureAwait(false);

                    if (detail.Attachments == null || detail.Attachments.Count == 0)
                    {
                        log?.Invoke($"Mail {mail.MailNo} ({mail.MailId}): sin adjuntos registrados.");
                        await _state.MarkMailProcessedAsync(request.IdTrabajo, proyecto.ProjectId, mail.MailId, cancellationToken).ConfigureAwait(false);
                        result.ProcessedMails++;
                        continue;
                    }

                    foreach (var attachment in detail.Attachments)
                    {
                        if (attachment.IsPlaceholder)
                        {
                            bool ok = await TryRegisterPlaceholderAsync(request, proyecto, attachment, log, cancellationToken).ConfigureAwait(false);
                            if (ok) result.PlaceholdersCreated++;
                        }
                        else
                        {
                            bool ok = await TryApplyFileFromTransmittalAsync(request, proyecto, attachment, log, cancellationToken).ConfigureAwait(false);
                            if (ok) result.FilesApplied++;
                        }
                    }

                    await _state.MarkMailProcessedAsync(request.IdTrabajo, proyecto.ProjectId, mail.MailId, cancellationToken).ConfigureAwait(false);
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
            ProyectoSyncItem proyecto,
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
            string existingId = await _state.GetLocalDocumentIdAsync(
                request.IdTrabajo, proyecto.ProjectId, attachment.DocumentNo, revision, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(existingId))
            {
                log?.Invoke($"Marcador ya mapeado: {attachment.DocumentNo} rev {revision} → {existingId}");
                return false;
            }

            string xml = BuildPlaceholderRegisterXml(attachment, revision);
            string boundary = AconexRegisterMultipart.CreateBoundary();
            string body = AconexRegisterMultipart.BuildRegisterBodyXmlOnly(xml, boundary);

            var response = await _registerWrite.PostRegisterDocumentAsync(
                request.BaseUrl,
                proyecto.ProjectId,
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
                request.IdTrabajo, proyecto.ProjectId, attachment.DocumentNo, revision, localDocumentId, cancellationToken).ConfigureAwait(false);

            log?.Invoke($"Marcador creado: {attachment.DocumentNo} rev {revision} → {localDocumentId}");
            return true;
        }

        private async Task<bool> TryApplyFileFromTransmittalAsync(
            TransmittalSyncRunRequest request,
            ProyectoSyncItem proyecto,
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
            string sourceDocumentId = attachment.DocumentId;
            if (string.IsNullOrWhiteSpace(sourceDocumentId))
            {
                log?.Invoke($"Archivo omitido ({attachment.DocumentNo}): sin DocumentId en transmittal.");
                return false;
            }

            string localDocumentId = !string.IsNullOrWhiteSpace(attachment.RegisteredAs)
                ? attachment.RegisteredAs.Trim()
                : await _state.GetLocalDocumentIdAsync(request.IdTrabajo, proyecto.ProjectId, attachment.DocumentNo, revision, cancellationToken).ConfigureAwait(false);

            string tempFile = Path.Combine(Path.GetTempPath(), "sigmabot_sync_" + Guid.NewGuid().ToString("N") + Path.GetExtension(attachment.FileName ?? ".bin"));
            try
            {
                var download = await _documentContent.DownloadToFileAsync(
                    request.BaseUrl,
                    proyecto.ProjectId,
                    sourceDocumentId,
                    tempFile,
                    request.AuthorizationHeaderBase64,
                    cancellationToken).ConfigureAwait(false);

                if (download.Status == AconexRegisterDocumentDownloadStatus.OmittedEmptyDocument)
                {
                    log?.Invoke($"Descarga vacía ({attachment.DocumentNo}): documento sin archivo en registro.");
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
                    return await RegisterWithFileAsync(request, proyecto, attachment, revision, fileName, fileBase64, log, cancellationToken).ConfigureAwait(false);
                }

                return await SupersedeWithFileAsync(request, proyecto, attachment, revision, localDocumentId, fileName, fileBase64, log, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                TryDeleteFile(tempFile);
            }
        }

        private async Task<bool> RegisterWithFileAsync(
            TransmittalSyncRunRequest request,
            ProyectoSyncItem proyecto,
            TransmittalDocumentAttachment attachment,
            string revision,
            string fileName,
            string fileBase64,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            string xml = BuildFileRegisterXml(attachment, revision);
            string boundary = AconexRegisterMultipart.CreateBoundary();
            string body = AconexRegisterMultipart.BuildRegisterBody(xml, fileName, fileBase64, boundary);

            var response = await _registerWrite.PostRegisterDocumentAsync(
                request.BaseUrl,
                proyecto.ProjectId,
                request.AuthorizationHeaderBase64,
                request.IntegrationId,
                body,
                boundary,
                cancellationToken).ConfigureAwait(false);

            string responseText = response?.Body ?? "";
            if (response == null || !response.IsSuccessStatusCode)
            {
                log?.Invoke($"Register con archivo falló ({attachment.DocumentNo}): {Truncate(responseText, 300)}");
                return false;
            }

            string localDocumentId = AconexRegisterResponseParser.ParseRegisterDocumentId(responseText);
            if (!string.IsNullOrWhiteSpace(localDocumentId))
            {
                await _state.SaveLocalDocumentMappingAsync(
                    request.IdTrabajo, proyecto.ProjectId, attachment.DocumentNo, revision, localDocumentId, cancellationToken).ConfigureAwait(false);
            }

            log?.Invoke($"Documento registrado con archivo: {attachment.DocumentNo} rev {revision} → {localDocumentId ?? "?"}");
            return true;
        }

        private async Task<bool> SupersedeWithFileAsync(
            TransmittalSyncRunRequest request,
            ProyectoSyncItem proyecto,
            TransmittalDocumentAttachment attachment,
            string revision,
            string localDocumentId,
            string fileName,
            string fileBase64,
            Action<string> log,
            CancellationToken cancellationToken)
        {
            string xml = BuildFileRegisterXml(attachment, revision);
            string boundary = AconexRegisterMultipart.CreateBoundary();
            string body = AconexRegisterMultipart.BuildRegisterBody(xml, fileName, fileBase64, boundary);

            var response = await _registerWrite.PostSupersedeDocumentAsync(
                request.BaseUrl,
                proyecto.ProjectId,
                localDocumentId,
                request.AuthorizationHeaderBase64,
                request.IntegrationId,
                body,
                boundary,
                cancellationToken).ConfigureAwait(false);

            string responseText = response?.Body ?? "";
            if (response == null || !response.IsSuccessStatusCode)
            {
                log?.Invoke($"Supersede falló ({attachment.DocumentNo} → {localDocumentId}): {Truncate(responseText, 300)}");
                return false;
            }

            log?.Invoke($"Supersede OK: {attachment.DocumentNo} rev {revision} → {localDocumentId}");
            return true;
        }

        private static string BuildPlaceholderRegisterXml(TransmittalDocumentAttachment attachment, string revision)
        {
            var sb = new StringBuilder();
            sb.Append("<Document>");
            sb.Append("<DocumentNumber>").Append(EscapeXml(attachment.DocumentNo)).Append("</DocumentNumber>");
            sb.Append("<Title>").Append(EscapeXml(string.IsNullOrWhiteSpace(attachment.Title) ? attachment.DocumentNo : attachment.Title)).Append("</Title>");
            sb.Append("<Revision>").Append(EscapeXml(revision)).Append("</Revision>");
            sb.Append("<HasFile>false</HasFile>");
            sb.Append("</Document>");
            return sb.ToString();
        }

        private static string BuildFileRegisterXml(TransmittalDocumentAttachment attachment, string revision)
        {
            var sb = new StringBuilder();
            sb.Append("<Document>");
            sb.Append("<DocumentNumber>").Append(EscapeXml(attachment.DocumentNo)).Append("</DocumentNumber>");
            sb.Append("<Title>").Append(EscapeXml(string.IsNullOrWhiteSpace(attachment.Title) ? attachment.DocumentNo : attachment.Title)).Append("</Title>");
            sb.Append("<Revision>").Append(EscapeXml(revision)).Append("</Revision>");
            sb.Append("<HasFile>true</HasFile>");
            sb.Append("</Document>");
            return sb.ToString();
        }

        private static string EscapeXml(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
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
        public string ProjectId { get; set; }
        public int TotalMails { get; set; }
        public int ProcessedMails { get; set; }
        public int SkippedAlreadyProcessed { get; set; }
        public int PlaceholdersCreated { get; set; }
        public int FilesApplied { get; set; }
        public int Errors { get; set; }
    }
}
