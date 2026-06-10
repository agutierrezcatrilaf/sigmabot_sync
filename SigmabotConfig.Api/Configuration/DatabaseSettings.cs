namespace SigmabotConfig.Api.Configuration;

/// <summary>
/// Cadena de conexión a SQL Server del configurador. Solo en appsettings del servidor (no en Angular).
/// </summary>
public sealed class DatabaseSettings
{
    public const string SectionName = "Database";

    public string ConnectionString { get; set; }
}
