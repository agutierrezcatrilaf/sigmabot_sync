using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SigmabotConfig.Api.Configuration;
using SigmabotSync.Infrastructure.Services;

namespace SigmabotConfig.Api.Services;

public sealed class DatabaseConnectionProvider : IDatabaseConnectionProvider
{
    private readonly DatabaseSettings _settings;

    public DatabaseConnectionProvider(IOptions<DatabaseSettings> options)
    {
        _settings = options?.Value ?? new DatabaseSettings();
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_settings.ConnectionString);

    public string GetConnectionString()
    {
        var raw = (_settings.ConnectionString ?? string.Empty).Trim();
        if (raw.Length == 0)
            throw new InvalidOperationException(
                "No hay cadena de conexión configurada. Defina Database:ConnectionString en appsettings del servidor.");
        return ConnectionStringHelper.AsegurarTrustServerCertificate(raw);
    }
}
