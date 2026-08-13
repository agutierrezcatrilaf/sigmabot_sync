using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SigmabotConfig.Api.Configuration;
using SigmabotConfig.Api.Models;
using SigmabotConfig.Api.Services;
using SigmabotSync.Infrastructure.Services;
using SigmabotSync.Infrastructure.Services.ConfigurationEditor;

namespace SigmabotConfig.Api.Controllers;

[Route("api/trabajos/{idTrabajo:int}/ejecuciones")]
public sealed class TrabajosEjecucionController : ApiControllerBase
{
    private const int LimitDefault = 50;
    private const int LimitMax = 200;

    private readonly EjecucionLogFileService _logFiles;

    public TrabajosEjecucionController(
        IDatabaseConnectionProvider db,
        EjecucionLogFileService logFiles) : base(db)
    {
        _logFiles = logFiles;
    }

    /// <summary>Historial de ejecuciones del trabajo (solo lectura).</summary>
    [HttpGet]
    public IActionResult Listar(int idTrabajo, [FromQuery] int limit = LimitDefault)
    {
        if (!Db.IsConfigured)
            return NotConfigured();

        if (limit <= 0)
            limit = LimitDefault;
        if (limit > LimitMax)
            limit = LimitMax;

        try
        {
            var trabajosSvc = new TrabajosEditorService(ConnectionString);
            if (trabajosSvc.ListarTodos().All(t => t.Id != idTrabajo))
                return NotFound(new ApiProblem { Message = "Trabajo no encontrado." });

            var svc = new TrabajosEjecucionService(ConnectionString);
            return Ok(svc.ListarPorIdTrabajo(idTrabajo, limit));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiProblem { Message = ex.Message });
        }
    }

    /// <summary>Descarga el archivo de log de una ejecución (job-*-ejec-*.log).</summary>
    [HttpGet("{idEjecucion:int}/log")]
    public IActionResult DescargarLog(int idTrabajo, int idEjecucion)
    {
        if (!Db.IsConfigured)
            return NotConfigured();

        try
        {
            var trabajosSvc = new TrabajosEditorService(ConnectionString);
            if (trabajosSvc.ListarTodos().All(t => t.Id != idTrabajo))
                return NotFound(new ApiProblem { Message = "Trabajo no encontrado." });

            var svc = new TrabajosEjecucionService(ConnectionString);
            var ejecucion = svc.ObtenerPorIdTrabajoYId(idTrabajo, idEjecucion);
            if (ejecucion == null)
                return NotFound(new ApiProblem { Message = "Ejecución no encontrada." });

            if (!_logFiles.TryResolveReadableLog(ejecucion.RutaLog, out string fullPath, out string error))
                return NotFound(new ApiProblem { Message = error });

            string downloadName = Path.GetFileName(fullPath);
            if (string.IsNullOrWhiteSpace(downloadName))
                downloadName = $"job-{idTrabajo}-ejec-{idEjecucion}.log";

            return PhysicalFile(fullPath, "text/plain", downloadName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiProblem { Message = ex.Message });
        }
    }
}
