using Microsoft.Extensions.Options;
using SigmabotConfig.Api.Configuration;

namespace SigmabotConfig.Api.Services;

/// <summary>Valida y abre archivos de log de ejecución del worker.</summary>
public sealed class EjecucionLogFileService
{
    private readonly WorkerLogsSettings _settings;

    public EjecucionLogFileService(IOptions<WorkerLogsSettings> settings)
    {
        _settings = settings?.Value ?? new WorkerLogsSettings();
    }

    public bool TryResolveReadableLog(string? rutaLog, out string fullPath, out string errorMessage)
    {
        fullPath = null;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(rutaLog))
        {
            errorMessage = "La ejecución no tiene ruta de log registrada.";
            return false;
        }

        try
        {
            fullPath = Path.GetFullPath(rutaLog.Trim());
        }
        catch
        {
            errorMessage = "Ruta de log inválida.";
            return false;
        }

        if (!IsUnderAllowedRoot(fullPath))
        {
            errorMessage = "La ruta del log no está en un directorio permitido para descarga.";
            return false;
        }

        if (!File.Exists(fullPath))
        {
            errorMessage = "El archivo de log no existe en el servidor.";
            return false;
        }

        return true;
    }

    private bool IsUnderAllowedRoot(string fullPath)
    {
        var allowed = _settings.AllowedDirectories;
        if (allowed == null || allowed.Count == 0)
            return false;

        foreach (string root in allowed)
        {
            if (string.IsNullOrWhiteSpace(root))
                continue;

            try
            {
                string fullRoot = Path.GetFullPath(root.Trim());
                if (fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch
            {
                // ignorar raíz mal configurada
            }
        }

        return false;
    }
}
