using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SigmabotSync.Domain.Ports;

namespace SigmabotSync.Infrastructure.Services
{
    /// <summary>Persistencia de mails procesados y mapeo DocumentNo+Revision → DocumentId local.</summary>
    public sealed class TransmittalSyncStateService : ITransmittalSyncStatePort
    {
        private readonly string _connectionString;

        public TransmittalSyncStateService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task<bool> IsMailProcessedAsync(
            int idTrabajo,
            string projectId,
            string mailId,
            CancellationToken cancellationToken = default)
        {
            const string sql = @"
                SELECT COUNT(1)
                FROM TransmittalSyncProcesados
                WHERE IdTrabajo = @IdTrabajo AND ACXProjectId = @ProjectId AND MailId = @MailId";

            using (var cn = new SqlConnection(_connectionString))
            {
                await cn.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@IdTrabajo", idTrabajo);
                    cmd.Parameters.AddWithValue("@ProjectId", projectId ?? "");
                    cmd.Parameters.AddWithValue("@MailId", mailId ?? "");
                    object scalar = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                    return scalar != null && Convert.ToInt32(scalar) > 0;
                }
            }
        }

        public async Task MarkMailProcessedAsync(
            int idTrabajo,
            string projectId,
            string mailId,
            CancellationToken cancellationToken = default)
        {
            const string sql = @"
                IF NOT EXISTS (
                    SELECT 1 FROM TransmittalSyncProcesados
                    WHERE IdTrabajo = @IdTrabajo AND ACXProjectId = @ProjectId AND MailId = @MailId)
                BEGIN
                    INSERT INTO TransmittalSyncProcesados (IdTrabajo, ACXProjectId, MailId, ProcessedAt)
                    VALUES (@IdTrabajo, @ProjectId, @MailId, SYSUTCDATETIME())
                END";

            using (var cn = new SqlConnection(_connectionString))
            {
                await cn.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@IdTrabajo", idTrabajo);
                    cmd.Parameters.AddWithValue("@ProjectId", projectId ?? "");
                    cmd.Parameters.AddWithValue("@MailId", mailId ?? "");
                    await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        public async Task<string> GetLocalDocumentIdAsync(
            int idTrabajo,
            string projectId,
            string documentNo,
            string revision,
            CancellationToken cancellationToken = default)
        {
            const string sql = @"
                SELECT TOP 1 LocalDocumentId
                FROM TransmittalSyncMapeo
                WHERE IdTrabajo = @IdTrabajo
                  AND ACXProjectId = @ProjectId
                  AND DocumentNo = @DocumentNo
                  AND Revision = @Revision";

            using (var cn = new SqlConnection(_connectionString))
            {
                await cn.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@IdTrabajo", idTrabajo);
                    cmd.Parameters.AddWithValue("@ProjectId", projectId ?? "");
                    cmd.Parameters.AddWithValue("@DocumentNo", documentNo ?? "");
                    cmd.Parameters.AddWithValue("@Revision", revision ?? "");
                    object scalar = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                    return scalar == null || scalar == DBNull.Value ? null : (scalar as string)?.Trim();
                }
            }
        }

        public async Task SaveLocalDocumentMappingAsync(
            int idTrabajo,
            string projectId,
            string documentNo,
            string revision,
            string localDocumentId,
            CancellationToken cancellationToken = default)
        {
            const string sql = @"
                IF EXISTS (
                    SELECT 1 FROM TransmittalSyncMapeo
                    WHERE IdTrabajo = @IdTrabajo AND ACXProjectId = @ProjectId
                      AND DocumentNo = @DocumentNo AND Revision = @Revision)
                BEGIN
                    UPDATE TransmittalSyncMapeo
                    SET LocalDocumentId = @LocalDocumentId, UpdatedAt = SYSUTCDATETIME()
                    WHERE IdTrabajo = @IdTrabajo AND ACXProjectId = @ProjectId
                      AND DocumentNo = @DocumentNo AND Revision = @Revision
                END
                ELSE
                BEGIN
                    INSERT INTO TransmittalSyncMapeo (IdTrabajo, ACXProjectId, DocumentNo, Revision, LocalDocumentId, UpdatedAt)
                    VALUES (@IdTrabajo, @ProjectId, @DocumentNo, @Revision, @LocalDocumentId, SYSUTCDATETIME())
                END";

            using (var cn = new SqlConnection(_connectionString))
            {
                await cn.OpenAsync(cancellationToken).ConfigureAwait(false);
                using (var cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@IdTrabajo", idTrabajo);
                    cmd.Parameters.AddWithValue("@ProjectId", projectId ?? "");
                    cmd.Parameters.AddWithValue("@DocumentNo", documentNo ?? "");
                    cmd.Parameters.AddWithValue("@Revision", revision ?? "");
                    cmd.Parameters.AddWithValue("@LocalDocumentId", localDocumentId ?? "");
                    await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
