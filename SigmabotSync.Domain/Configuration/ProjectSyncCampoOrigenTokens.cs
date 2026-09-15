namespace SigmabotSync.Domain.Configuration
{
    /// <summary>
    /// Valores especiales de <c>CampoOrigen</c> en homologación ProjectSync (no son campos Aconex).
    /// </summary>
    public static class ProjectSyncCampoOrigenTokens
    {
        /// <summary>
        /// Allowlist / legacy: valor de <see cref="TrabajosConfiguracionKeyNames.IdEstatusDocumentoDestino"/>.
        /// Preferir mapeo Adjunto/Status; el parámetro filtra en vuelta.
        /// </summary>
        public const string IdEstatusDocumentoDestino = "@IdEstatusDocumentoDestino";

        /// <summary>
        /// Ida Codelco→SALFA: DocumentTypeId desde TipoDeDocumento_singleSelect (prefijo numérico/letras).
        /// </summary>
        public const string DocumentTypeFromTipoDocumento = "@DocumentTypeFromTipoDocumento";

        public static bool IsIdEstatusDocumentoDestino(string campoOrigen)
        {
            return string.Equals(campoOrigen?.Trim(), IdEstatusDocumentoDestino, System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsDocumentTypeFromTipoDocumento(string campoOrigen)
        {
            return string.Equals(campoOrigen?.Trim(), DocumentTypeFromTipoDocumento, System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSyntheticToken(string campoOrigen)
        {
            return IsIdEstatusDocumentoDestino(campoOrigen)
                || IsDocumentTypeFromTipoDocumento(campoOrigen);
        }
    }
}
