namespace SigmabotConfig.Api.Configuration;

/// <summary>
/// Ejecución a demanda del worker (solo servidores donde API y exe conviven, p. ej. pruebas).
/// Apagado por defecto: en producción dejar Enabled = false.
/// </summary>
public sealed class OnDemandExecutionSettings
{
    public const string SectionName = "OnDemandExecution";

    /// <summary>Si es false, el endpoint responde 403 y el front no muestra el botón.</summary>
    public bool Enabled { get; set; }

    /// <summary>Ruta absoluta a SigmabotSync.Console.exe.</summary>
    public string WorkerExePath { get; set; }
}
