using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Options;
using SigmabotConfig.Api.Configuration;

namespace SigmabotConfig.Api.Services;

/// <summary>Lanza SigmabotSync.Console.exe --manual {id} sin esperar a que termine el trabajo.</summary>
public sealed class OnDemandExecutionService
{
    private static readonly ConcurrentDictionary<int, long> InFlight = new();
    private static readonly long InFlightTicks = TimeSpan.FromSeconds(20).Ticks;

    private readonly OnDemandExecutionSettings _settings;
    private readonly ILogger<OnDemandExecutionService> _logger;

    public OnDemandExecutionService(
        IOptions<OnDemandExecutionSettings> settings,
        ILogger<OnDemandExecutionService> logger)
    {
        _settings = settings?.Value ?? new OnDemandExecutionSettings();
        _logger = logger;
    }

    /// <summary>True solo si el flag está activo y el exe existe (para mostrar el botón).</summary>
    public bool IsAvailable => _settings.Enabled && TryResolveExe(out _, out _);

    public bool IsEnabled => _settings.Enabled;

    public bool TryStart(int idTrabajo, out string error)
    {
        error = null;
        if (!_settings.Enabled)
        {
            error = "La ejecución a demanda no está habilitada en este servidor.";
            return false;
        }

        if (!TryResolveExe(out var exePath, out error))
            return false;

        var now = DateTime.UtcNow.Ticks;
        EvictStale(now);
        if (!InFlight.TryAdd(idTrabajo, now))
        {
            error = "Ya se está iniciando una ejecución de este trabajo. Espere unos segundos.";
            return false;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = "--manual " + idTrabajo.ToString(CultureInfo.InvariantCulture),
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? "",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                InFlight.TryRemove(idTrabajo, out _);
                error = "No se pudo iniciar el proceso del worker.";
                return false;
            }

            _logger.LogInformation(
                "Ejecución a demanda: IdTrabajo={IdTrabajo}, PID={Pid}",
                idTrabajo,
                process.Id);
            return true;
        }
        catch (Exception ex)
        {
            InFlight.TryRemove(idTrabajo, out _);
            _logger.LogError(ex, "No se pudo iniciar el worker para IdTrabajo={IdTrabajo}", idTrabajo);
            error = "No se pudo iniciar el worker: " + ex.Message;
            return false;
        }
    }

    private bool TryResolveExe(out string exePath, out string error)
    {
        exePath = null;
        error = null;
        var raw = _settings.WorkerExePath?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "OnDemandExecution:WorkerExePath no está configurado.";
            return false;
        }

        if (!Path.IsPathRooted(raw))
        {
            error = "OnDemandExecution:WorkerExePath debe ser una ruta absoluta.";
            return false;
        }

        string full;
        try
        {
            full = Path.GetFullPath(raw);
        }
        catch
        {
            error = "OnDemandExecution:WorkerExePath no es una ruta válida.";
            return false;
        }

        if (!string.Equals(Path.GetExtension(full), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            error = "OnDemandExecution:WorkerExePath debe apuntar a un .exe.";
            return false;
        }

        if (!System.IO.File.Exists(full))
        {
            error = "No se encontró el exe del worker. Verifique OnDemandExecution:WorkerExePath.";
            return false;
        }

        exePath = full;
        return true;
    }

    private static void EvictStale(long now)
    {
        foreach (var kv in InFlight)
        {
            if (now - kv.Value > InFlightTicks)
                InFlight.TryRemove(kv.Key, out _);
        }
    }
}
