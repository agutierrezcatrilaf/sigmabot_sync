namespace SigmabotSync.Domain.Configuration
{
    /// <summary>
    /// Valores de la columna <c>Nombre</c> en <c>TrabajosConfiguracion</c> (clave-valor).
    /// Deben coincidir con los nombres leídos por TrabajosService en Infrastructure.
    /// </summary>
    public static class TrabajosConfiguracionKeyNames
    {
        public const string Proyecto = "Proyecto";
        public const string IdProyecto = "IdProyecto";
        public const string IdProyecto2 = "IdProyecto2";
        public const string Proyecto2 = "Proyecto2";
        public const string DiasLookbackTransmittal = "DiasLookbackTransmittal";
        public const string CredencialAconex = "CredencialAconex";
        public const string CredencialBD = "CredencialBD";
        public const string CamposConsulta = "CamposConsulta";
        public const string CamposResponse = "CamposResponse";
        public const string CamposBD = "CamposBD";
        public const string BasePath = "BasePath";
        public const string TablaMetadata = "TablaMetadata";
        public const string TablaPaths = "TablaPaths";
    }
}
