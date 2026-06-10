using System.Threading;
using System.Threading.Tasks;

namespace SigmabotSync.Domain.Ports
{
    /// <summary>Estado de mails y mapeo DocumentNo+Revision → DocumentId local para ProjectSync.</summary>
    public interface ITransmittalSyncStatePort
    {
        Task<bool> IsMailProcessedAsync(
            int idTrabajo,
            string projectId,
            string mailId,
            CancellationToken cancellationToken = default);

        Task MarkMailProcessedAsync(
            int idTrabajo,
            string projectId,
            string mailId,
            CancellationToken cancellationToken = default);

        Task<string> GetLocalDocumentIdAsync(
            int idTrabajo,
            string projectId,
            string documentNo,
            string revision,
            CancellationToken cancellationToken = default);

        Task SaveLocalDocumentMappingAsync(
            int idTrabajo,
            string projectId,
            string documentNo,
            string revision,
            string localDocumentId,
            CancellationToken cancellationToken = default);
    }
}
