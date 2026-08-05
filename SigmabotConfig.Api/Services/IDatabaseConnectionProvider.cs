namespace SigmabotConfig.Api.Services;

public interface IDatabaseConnectionProvider
{
    /// <summary>Cadena normalizada (TrustServerCertificate). Lanza si no está configurada.</summary>
    string GetConnectionString();

    bool IsConfigured { get; }
}
