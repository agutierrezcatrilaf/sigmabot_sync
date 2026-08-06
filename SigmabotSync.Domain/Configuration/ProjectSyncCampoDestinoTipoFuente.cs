namespace SigmabotSync.Domain.Configuration
{
    /// <summary>Tipo de origen del valor en la matriz <c>TransmittalSyncCampoDestino</c>.</summary>
    public static class ProjectSyncCampoDestinoTipoFuente
    {
        /// <summary>Campo Aconex del proyecto origen (<c>FuenteValor</c> = nombre API; vacío = mismo que <c>CampoDestino</c>).</summary>
        public const string CampoOrigen = "CampoOrigen";

        /// <summary>Ida Codelco→SALFA: DocumentTypeId desde TipoDeDocumento (Plano/Documento Externo).</summary>
        public const string ReglaDocumentTypeFromTipo = "ReglaDocumentTypeFromTipo";

        /// <summary>Vuelta: statusid desde parámetro IdEstatusDocumentoDestino.</summary>
        public const string ParametroIdEstatusDestino = "ParametroIdEstatusDestino";

        /// <summary>Valor del adjunto transmittal (<c>FuenteValor</c>: Revision, MailNo, …).</summary>
        public const string Adjunto = "Adjunto";

        /// <summary>Solo <c>ValorDefault</c> en create; en supersede se preserva del destino.</summary>
        public const string Constante = "Constante";

        /// <summary>Solo lectura supersede (project fields destino sin mapeo create).</summary>
        public const string SoloPreservar = "SoloPreservar";
    }
}
