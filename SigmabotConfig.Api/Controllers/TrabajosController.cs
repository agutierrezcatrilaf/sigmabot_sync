using Microsoft.AspNetCore.Mvc;
using SigmabotConfig.Api.Models;
using SigmabotConfig.Api.Services;
using SigmabotSync.Domain.Configuration;
using SigmabotSync.Domain.Entities;
using SigmabotSync.Infrastructure.Services.ConfigurationEditor;

namespace SigmabotConfig.Api.Controllers;

[Route("api/[controller]")]
public sealed class TrabajosController : ApiControllerBase
{
    public TrabajosController(IDatabaseConnectionProvider db) : base(db) { }

    [HttpGet]
    public IActionResult Listar()
    {
        if (!Db.IsConfigured)
            return NotConfigured();
        try
        {
            var svc = new TrabajosEditorService(ConnectionString);
            return Ok(svc.ListarTodos());
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiProblem { Message = ex.Message });
        }
    }

    [HttpGet("{id:int}")]
    public IActionResult Obtener(int id)
    {
        if (!Db.IsConfigured)
            return NotConfigured();
        try
        {
            var svc = new TrabajosEditorService(ConnectionString);
            var t = svc.ListarTodos().FirstOrDefault(x => x.Id == id);
            if (t == null)
                return NotFound(new ApiProblem { Message = "Trabajo no encontrado." });
            return Ok(t);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiProblem { Message = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult Crear([FromBody] Trabajo trabajo)
    {
        if (!Db.IsConfigured)
            return NotConfigured();
        var errores = TrabajoRequisitosValidator.Validar(trabajo);
        if (errores.Count > 0)
            return ValidationProblem(errores);

        try
        {
            trabajo.Id = 0;
            var svc = new TrabajosEditorService(ConnectionString);
            trabajo.Id = svc.Insertar(trabajo);
            return CreatedAtAction(nameof(Obtener), new { id = trabajo.Id }, trabajo);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiProblem { Message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public IActionResult Actualizar(int id, [FromBody] Trabajo trabajo)
    {
        if (!Db.IsConfigured)
            return NotConfigured();
        trabajo.Id = id;
        var errores = TrabajoRequisitosValidator.Validar(trabajo);
        if (errores.Count > 0)
            return ValidationProblem(errores);

        try
        {
            var svc = new TrabajosEditorService(ConnectionString);
            svc.Actualizar(trabajo);
            return NoContent();
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
    public IActionResult Eliminar(int id)
    {
        if (!Db.IsConfigured)
            return NotConfigured();
        try
        {
            var svc = new TrabajosEditorService(ConnectionString);
            svc.Eliminar(id);
            return NoContent();
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
}
