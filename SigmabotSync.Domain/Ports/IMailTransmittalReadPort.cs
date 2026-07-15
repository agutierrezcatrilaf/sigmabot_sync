using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SigmabotSync.Domain.Models.Synchronization;

namespace SigmabotSync.Domain.Ports
{
    /// <summary>Lectura de transmitals (List Mail + View Mail Metadata).</summary>
    public interface IMailTransmittalReadPort
    {
        /// <param name="mailbox">inbox o sentbox.</param>
        Task<IReadOnlyList<TransmittalMailSummary>> ListTransmittalsAsync(
            string baseUrl,
            string projectId,
            string authorizationHeaderBase64,
            DateTime desdeUtc,
            DateTime hastaUtc,
            string mailbox,
            CancellationToken cancellationToken = default);

        Task<TransmittalMailDetail> GetTransmittalDetailAsync(
            string baseUrl,
            string projectId,
            string mailId,
            string authorizationHeaderBase64,
            CancellationToken cancellationToken = default);
    }
}
