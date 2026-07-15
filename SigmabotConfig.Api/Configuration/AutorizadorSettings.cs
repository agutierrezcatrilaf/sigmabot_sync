namespace SigmabotConfig.Api.Configuration;

public sealed class AutorizadorSettings
{
    public const string SectionName = "Autorizador";

    /// <summary>
    /// Si es false, no se exige Bearer ni validación de recurso (solo desarrollo local).
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Base API Autorizador, ej. https://apiautorizadorqa.salfagestion.cl/api/v1.0</summary>
    public string UrlApi { get; set; }

    /// <summary>Recurso declarado en Autorizador (scope/audience), ej. sigmabotconfig-api</summary>
    public string Recurso { get; set; } = "sigmabotconfig-api";
}
