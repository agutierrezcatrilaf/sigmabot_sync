using Microsoft.AspNetCore.Mvc;
using SigmabotConfig.Api.Models;
using SigmabotConfig.Api.Services;
using SigmabotSync.Domain.Configuration;
using SigmabotSync.Domain.Entities;
using SigmabotSync.Infrastructure.Services;
using SigmabotSync.Infrastructure.Services.ConfigurationEditor;

namespace SigmabotConfig.Api.Controllers;

[Route("api/[controller]")]
public sealed class TrabajosController : ApiControllerBase
{
    private readonly OnDemandExecutionService _onDemand;

    public TrabajosController(IDatabaseConnectionProvider db, OnDemandExecutionService onDemand) : base(db)
    {
        _onDemand = onDemand;
    }

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
        var errores = ValidarTrabajo(trabajo);
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
        var errores = ValidarTrabajo(trabajo);
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

    /// <summary>Lanza el worker con --manual {id}. Solo si OnDemandExecution está habilitado.</summary>
    [HttpPost("{id:int}/ejecutar")]
    public IActionResult Ejecutar(int id)
    {
        if (!_onDemand.IsEnabled)
            return StatusCode(403, new ApiProblem { Message = "La ejecución a demanda no está habilitada en este servidor." });
        if (!Db.IsConfigured)
            return NotConfigured();

        try
        {
            var trabajosSvc = new TrabajosEditorService(ConnectionString);
            var trabajo = trabajosSvc.ListarTodos().FirstOrDefault(x => x.Id == id);
            if (trabajo == null)
                return NotFound(new ApiProblem { Message = "Trabajo no encontrado." });

            if (!string.Equals(trabajo.Estado, TrabajoEstadoIds.Activo, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new ApiProblem
                {
                    Message = "El trabajo debe estar en estado Activo para ejecutarlo a demanda."
                });
            }

            var ejecSvc = new TrabajosEjecucionService(ConnectionString);
            if (ejecSvc.ExisteEjecucionEnCurso(id))
            {
                return Conflict(new ApiProblem
                {
                    Message = "Este trabajo ya tiene una ejecución en curso."
                });
            }

            if (!_onDemand.TryStart(id, out var error))
            {
                var code = _onDemand.IsEnabled ? 503 : 403;
                return StatusCode(code, new ApiProblem { Message = error });
            }

            return Accepted(new ApiProblem
            {
                Message = "Ejecución iniciada (Manual). Consulte el historial en unos segundos."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiProblem { Message = ex.Message });
        }
    }

    private IReadOnlyList<string> ValidarTrabajo(Trabajo trabajo)
    {
        try
        {
            var tiposSvc = new TiposTrabajoEditorService(ConnectionString);
            var codigos = tiposSvc.ListarActivos()
                .Select(t => t.Codigo)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();
            if (codigos.Count > 0)
                return TrabajoRequisitosValidator.Validar(trabajo, codigos);
        }
        catch
        {
            // Tabla TiposTrabajo aún no creada: validación legacy por constantes.
        }

        return TrabajoRequisitosValidator.Validar(trabajo);
    }
}
