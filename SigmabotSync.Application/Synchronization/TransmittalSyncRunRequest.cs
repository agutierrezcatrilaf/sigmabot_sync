using System.Collections.Generic;
using SigmabotSync.Domain.Entities;

namespace SigmabotSync.Application.Synchronization
{
    public sealed class TransmittalSyncRunRequest
    {
        public int IdTrabajo { get; set; }
        public string BaseUrl { get; set; }
        public string AuthorizationHeaderBase64 { get; set; }
        public string IntegrationId { get; set; }
        public string OrgId { get; set; }
        public string UserId { get; set; }
        public int DiasLookback { get; set; } = 30;
        public IReadOnlyList<ProyectoSyncItem> Proyectos { get; set; }
    }
}
