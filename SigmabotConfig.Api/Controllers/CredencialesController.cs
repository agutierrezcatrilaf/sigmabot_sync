using Microsoft.AspNetCore.Mvc;
using SigmabotConfig.Api.Models;
using SigmabotConfig.Api.Services;
using SigmabotSync.Domain.Configuration;
using SigmabotSync.Domain.Entities;
using SigmabotSync.Infrastructure.Services.ConfigurationEditor;

namespace SigmabotConfig.Api.Controllers;

[Route("api/[controller]")]
public sealed class CredencialesController : ApiControllerBase
{
    public CredencialesController(IDatabaseConnectionProvider db) : base(db) { }

    [HttpGet]
    public IActionResult Listar()
    {
        if (!Db.IsConfigured)
            return NotConfigured();
        try
        {
            var svc = new CredencialesEditorService(ConnectionString);
            return Ok(svc.ListarTodas());
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
            var svc = new CredencialesEditorService(ConnectionString);
            var c = svc.ListarTodas().FirstOrDefault(x => x.Id == id);
            if (c == null)
                return NotFound(new ApiProblem { Message = "Credencial no encontrada." });
            return Ok(c);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiProblem { Message = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult Crear([FromBody] Credencial credencial)
    {
        if (!Db.IsConfigured)
            return NotConfigured();
        var errores = CredencialRequisitosValidator.ValidarCamposObligatorios(credencial);
        if (errores.Count > 0)
            return ValidationProblem(errores);

        try
        {
            credencial.Id = 0;
            var svc = new CredencialesEditorService(ConnectionString);
            var id = svc.Insertar(credencial);
            credencial.Id = id;
            return CreatedAtAction(nameof(Obtener), new { id }, credencial);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiProblem { Message = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public IActionResult Actualizar(int id, [FromBody] Credencial credencial)
    {
        if (!Db.IsConfigured)
            return NotConfigured();
        credencial.Id = id;
        var errores = CredencialRequisitosValidator.ValidarCamposObligatorios(credencial);
        if (errores.Count > 0)
            return ValidationProblem(errores);

        try
        {
            var svc = new CredencialesEditorService(ConnectionString);
            svc.Actualizar(credencial);
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
            var svc = new CredencialesEditorService(ConnectionString);
            svc.Eliminar(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiProblem { Message = ex.Message });
        }
    }
}
