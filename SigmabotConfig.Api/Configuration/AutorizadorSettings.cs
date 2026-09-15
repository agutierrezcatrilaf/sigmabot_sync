namespace SigmabotConfig.Api.Configuration;

public sealed class AutorizadorSettings
{
    public const string SectionName = "Autorizador";

    /// <summary>
    /// Si es false, no se exige Bearer ni validación de recurso (solo desarrollo local).
    /// Por defecto true (fail-secure): en QA/Prod debe quedar habilitado.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Base API Autorizador, ej. https://apiautorizadorqa.salfagestion.cl/api/v1.0</summary>
    public string UrlApi { get; set; } = "https://autorizadorapi.salfagestion.cl/api/v1.0";

    /// <summary>Recurso declarado en Autorizador (scope/audience), ej. configsigmabot-api</summary>
    public string Recurso { get; set; } = "configsigmabot-api";
}
