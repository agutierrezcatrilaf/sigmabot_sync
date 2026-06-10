using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SigmabotSync.Domain.Models.Synchronization;

namespace SigmabotSync.Domain.Ports
{
    /// <summary>Lectura de transmitals (List Mail inbox + View Mail Metadata).</summary>
    public interface IMailTransmittalReadPort
    {
        Task<IReadOnlyList<TransmittalMailSummary>> ListInboxTransmittalsAsync(
            string baseUrl,
            string projectId,
            string authorizationHeaderBase64,
            DateTime desdeUtc,
            DateTime hastaUtc,
            CancellationToken cancellationToken = default);

        Task<TransmittalMailDetail> GetTransmittalDetailAsync(
            string baseUrl,
            string projectId,
            string mailId,
            string authorizationHeaderBase64,
            CancellationToken cancellationToken = default);
    }
}
