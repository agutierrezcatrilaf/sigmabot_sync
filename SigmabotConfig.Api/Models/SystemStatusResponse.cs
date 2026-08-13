namespace SigmabotConfig.Api.Models;

public sealed class SystemStatusResponse
{
    public bool DatabaseConfigured { get; set; }
    public bool DatabaseReachable { get; set; }
    /// <summary>Servidor SQL (sin credenciales).</summary>
    public string DatabaseServer { get; set; }
    /// <summary>Nombre de la base de datos.</summary>
    public string DatabaseName { get; set; }
    public string Message { get; set; }
}
