namespace SigmabotSync.Domain.Configuration
{
    /// <summary>
    /// Valores especiales de <c>CampoOrigen</c> en homologación ProjectSync (no son campos Aconex).
    /// </summary>
    public static class ProjectSyncCampoOrigenTokens
    {
        /// <summary>
        /// Toma el valor de <see cref="TrabajosConfiguracionKeyNames.IdEstatusDocumentoDestino"/> (parámetros del trabajo).
        /// </summary>
        public const string IdEstatusDocumentoDestino = "@IdEstatusDocumentoDestino";

        /// <summary>
        /// Ida Codelco→SALFA: DocumentTypeId desde TipoDeDocumento_singleSelect (prefijo numérico/letras).
        /// </summary>
        public const string DocumentTypeFromTipoDocumento = "@DocumentTypeFromTipoDocumento";

        /// <summary>
        /// Ida Codelco→SALFA: DocumentNumber nomenclatura SALFA desde docno Codelco + equivalencias.
        /// Requiere <see cref="TrabajosConfiguracionKeyNames.CodigoProyectoSalfa"/>.
        /// </summary>
        public const string SalfaDocumentNumberFromCodelco = "@SalfaDocumentNumberFromCodelco";

        public static bool IsIdEstatusDocumentoDestino(string campoOrigen)
        {
            return string.Equals(campoOrigen?.Trim(), IdEstatusDocumentoDestino, System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsDocumentTypeFromTipoDocumento(string campoOrigen)
        {
            return string.Equals(campoOrigen?.Trim(), DocumentTypeFromTipoDocumento, System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSalfaDocumentNumberFromCodelco(string campoOrigen)
        {
            return string.Equals(campoOrigen?.Trim(), SalfaDocumentNumberFromCodelco, System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSyntheticToken(string campoOrigen)
        {
            return IsIdEstatusDocumentoDestino(campoOrigen)
                || IsDocumentTypeFromTipoDocumento(campoOrigen)
                || IsSalfaDocumentNumberFromCodelco(campoOrigen);
        }
    }
}
