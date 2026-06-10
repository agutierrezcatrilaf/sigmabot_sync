using Microsoft.AspNetCore.Mvc;
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

    public TrabajosEjecucionController(IDatabaseConnectionProvider db) : base(db) { }

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
}
