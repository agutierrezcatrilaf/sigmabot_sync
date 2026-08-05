using Microsoft.AspNetCore.Mvc;
using SigmabotConfig.Api.Models;
using SigmabotConfig.Api.Services;
using SigmabotSync.Domain.Entities;
using SigmabotSync.Infrastructure.Services.ConfigurationEditor;

namespace SigmabotConfig.Api.Controllers;

[Route("api/trabajos/{idTrabajo:int}/programacion")]
public sealed class TrabajosProgramacionController : ApiControllerBase
{
    public TrabajosProgramacionController(IDatabaseConnectionProvider db) : base(db) { }

    [HttpGet]
    public IActionResult Listar(int idTrabajo)
    {
        if (!Db.IsConfigured)
            return NotConfigured();
        try
        {
            var svc = new TrabajosProgramacionEditorService(ConnectionString);
            return Ok(svc.ListarPorIdTrabajo(idTrabajo));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiProblem { Message = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult Crear(int idTrabajo, [FromBody] TrabajoProgramacion fila)
    {
        if (!Db.IsConfigured)
            return NotConfigured();
        fila.IdTrabajo = idTrabajo;
        try
        {
            var svc = new TrabajosProgramacionEditorService(ConnectionString);
            fila.Id = svc.Insertar(fila);
            return CreatedAtAction(nameof(Listar), new { idTrabajo }, fila);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiProblem { Message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiProblem { Message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public IActionResult Actualizar(int idTrabajo, int id, [FromBody] TrabajoProgramacion fila)
    {
        if (!Db.IsConfigured)
            return NotConfigured();
        fila.Id = id;
        fila.IdTrabajo = idTrabajo;
        try
        {
            var svc = new TrabajosProgramacionEditorService(ConnectionString);
            svc.Actualizar(fila);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ApiProblem { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new ApiProblem { Message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiProblem { Message = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public IActionResult Eliminar(int idTrabajo, int id)
    {
        if (!Db.IsConfigured)
            return NotConfigured();
        try
        {
            var svc = new TrabajosProgramacionEditorService(ConnectionString);
            svc.Eliminar(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiProblem { Message = ex.Message });
        }
    }
}
