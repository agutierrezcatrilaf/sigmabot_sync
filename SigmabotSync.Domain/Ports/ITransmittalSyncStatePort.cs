using System.Threading;
using System.Threading.Tasks;

namespace SigmabotSync.Domain.Ports
{
    /// <summary>
    /// Estado de ProjectSync cross-project.
    /// Mails procesados: <paramref name="projectId"/> = proyecto origen (donde se leyó el transmittal).
    /// Mapeo documentos: <paramref name="projectId"/> = proyecto destino (donde está el DocumentId local en Aconex).
    /// </summary>
    public interface ITransmittalSyncStatePort
    {
        Task<bool> IsMailProcessedAsync(
            int idTrabajo,
            string sourceProjectId,
            string mailId,
            CancellationToken cancellationToken = default);

        Task MarkMailProcessedAsync(
            int idTrabajo,
            string sourceProjectId,
            string mailId,
            CancellationToken cancellationToken = default);

        Task<string> GetLocalDocumentIdAsync(
            int idTrabajo,
            string targetProjectId,
            string documentNo,
            string revision,
            CancellationToken cancellationToken = default);

        Task SaveLocalDocumentMappingAsync(
            int idTrabajo,
            string targetProjectId,
            string documentNo,
            string revision,
            string localDocumentId,
            CancellationToken cancellationToken = default);
    }
}
