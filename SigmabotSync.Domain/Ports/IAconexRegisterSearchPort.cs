using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SigmabotSync.Domain.Models.Extraction;

namespace SigmabotSync.Domain.Ports
{
    /// <summary>
    /// Búsqueda paginada en el registro de documentos (POST .../register/search).
    /// Solo Authorization Basic; sin X-Application-Key (mismo criterio que los workers de extracción).
    /// </summary>
    public interface IAconexRegisterSearchPort
    {
        /// <param name="throwIfNotSuccess">Si es false, devuelve null ante respuesta HTTP no exitosa (comportamiento DocumentExtraction).</param>
        Task<Rootobject> SearchRegisterPageAsync(
            string baseUrl,
            string projectId,
            string orgId,
            string userId,
            string authorizationHeaderBase64,
            IReadOnlyList<string> returnFields,
            int resultSize,
            int pageNumber,
            bool throwIfNotSuccess = true,
            CancellationToken cancellationToken = default);
    }
}
