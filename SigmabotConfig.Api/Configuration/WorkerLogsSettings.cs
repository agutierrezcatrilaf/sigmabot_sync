namespace SigmabotConfig.Api.Configuration;

/// <summary>
/// Directorios donde la API puede leer logs de ejecución del worker (descarga segura).
/// </summary>
public sealed class WorkerLogsSettings
{
    public const string SectionName = "WorkerLogs";

    /// <summary>Rutas absolutas permitidas (el archivo RutaLog debe estar bajo una de ellas).</summary>
    public List<string> AllowedDirectories { get; set; } = new();
}
