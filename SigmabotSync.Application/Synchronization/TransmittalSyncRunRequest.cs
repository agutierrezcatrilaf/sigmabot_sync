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

        /// <summary>idEstatus o nombre en EstatusDocumentos para forzar al registrar en <see cref="IdProyectoEstatusFijo"/>.</summary>
        public string IdEstatusDocumentoDestino { get; set; }

        /// <summary>Proyecto Aconex donde aplica el estatus fijo (default: lado 1 / IdProyecto).</summary>
        public string IdProyectoEstatusFijo { get; set; }

        /// <summary>Vuelta SALFA→Codelco: Subject debe contener este texto (vacío = sin filtro).</summary>
        public string SubjectFiltroTransmittalVuelta { get; set; }

        /// <summary>Primer segmento del docno SALFA (ida Codelco→SALFA). TrabajosConfiguracion CodigoProyectoSalfa.</summary>
        public string CodigoProyectoSalfa { get; set; }

        /// <summary>returnFields extra para register/search del destino antes de supersede.</summary>
        public IReadOnlyList<string> CamposConsultaRegistroDestino { get; set; }
    }
}
