using Microsoft.AspNetCore.Mvc;
using SigmabotConfig.Api.Models;
using SigmabotConfig.Api.Services;
using SigmabotSync.Domain.Configuration;
using SigmabotSync.Domain.Entities;
using SigmabotSync.Domain.Security;
using SigmabotSync.Infrastructure.Services.ConfigurationEditor;

namespace SigmabotConfig.Api.Controllers;

[Route("api/[controller]")]
public sealed class CatalogosController : ApiControllerBase
{
    private readonly ICredencialClaveProtector _claveProtector;

    public CatalogosController(IDatabaseConnectionProvider db, ICredencialClaveProtector claveProtector) : base(db)
    {
        _claveProtector = claveProtector;
    }

    [HttpGet("tipos-trabajo")]
    public IActionResult TiposTrabajo()
    {
        if (!Db.IsConfigured)
            return NotConfigured();

        try
        {
            var svc = new TiposTrabajoEditorService(ConnectionString);
            var list = svc.ListarActivos()
                .Select(t => new TipoTrabajoDto
                {
                    Codigo = t.Codigo ?? string.Empty,
                    Nombre = t.Nombre ?? t.Codigo ?? string.Empty,
                    Descripcion = t.Descripcion,
                    Orden = t.Orden
                })
                .ToList();
            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiProblem { Message = ex.Message });
        }
    }

    [HttpGet("estados-trabajo")]
    public ActionResult<IReadOnlyList<string>> EstadosTrabajo()
    {
        return Ok(new[]
        {
            TrabajoEstadoIds.Activo,
            TrabajoEstadoIds.Desactivado,
            TrabajoEstadoIds.Pendiente
        });
    }

    [HttpGet("tipos-credencial")]
    public ActionResult<IReadOnlyList<string>> TiposCredencial()
    {
        return Ok(new[] { CredencialTipoIds.Aconex, CredencialTipoIds.BD });
    }

    [HttpGet("campos-configuracion/{tipoTrabajo}")]
    public ActionResult<IReadOnlyList<CampoDefinicionDto>> CamposConfiguracion(string tipoTrabajo)
    {
        var tipo = (tipoTrabajo ?? string.Empty).Trim();
        var defs = TrabajoTipoConfigFieldCatalog.ObtenerCamposParaFormulario(tipo);
        var dtos = defs.Select(d => new CampoDefinicionDto
        {
            Clave = d.Clave,
            Etiqueta = d.Etiqueta,
            Ayuda = d.Ayuda,
            Obligatorio = d.EsObligatorioPara(tipo)
        }).ToList();
        return Ok(dtos);
    }

    [HttpGet("plantillas-mapeo/{tipoTrabajo}")]
    public ActionResult<IReadOnlyList<PlantillaMapeoResumenDto>> PlantillasMapeo(string tipoTrabajo)
    {
        var tipo = (tipoTrabajo ?? string.Empty).Trim();
        if (!MapeoCamposDocumentoHelper.TipoUsaMapeoGuiado(tipo))
            return Ok(Array.Empty<PlantillaMapeoResumenDto>());

        var list = PlantillasMapeoCamposCatalog.ListarParaTipo(tipo)
            .Select(p => new PlantillaMapeoResumenDto { Id = p.Id, Nombre = p.Nombre })
            .ToList();
        return Ok(list);
    }

    [HttpGet("plantillas-mapeo/{tipoTrabajo}/{plantillaId}")]
    public ActionResult<PlantillaMapeoDetalleDto> PlantillaMapeoDetalle(string tipoTrabajo, string plantillaId)
    {
        try
        {
            var p = PlantillasMapeoCamposCatalog.Obtener(plantillaId, tipoTrabajo);
            return Ok(new PlantillaMapeoDetalleDto
            {
                Id = p.Id,
                Nombre = p.Nombre,
                Filas = p.Filas.Select(f => new MapeoCampoFilaDto
                {
                    Api = f.Api,
                    Json = f.Json,
                    Bd = f.Bd
                }).ToList()
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new ApiProblem { Message = ex.Message });
        }
    }

    [HttpGet("credenciales-combo")]
    public IActionResult CredencialesCombo()
    {
        if (!Db.IsConfigured)
            return NotConfigured();

        try
        {
            var svc = new CredencialesEditorService(ConnectionString, _claveProtector);
            var aconex = new List<CredencialComboDto>();
            var bd = new List<CredencialComboDto>();
            foreach (var c in svc.ListarTodas().OrderBy(x => x.Id))
            {
                var item = new CredencialComboDto
                {
                    Id = c.Id,
                    Etiqueta = c.Id + " — " + (c.Nombre ?? string.Empty).Trim()
                };
                var tipo = (c.Tipo ?? string.Empty).Trim();
                if (tipo.Equals(CredencialTipoIds.Aconex, StringComparison.OrdinalIgnoreCase))
                    aconex.Add(item);
                else if (tipo.Equals(CredencialTipoIds.BD, StringComparison.OrdinalIgnoreCase))
                    bd.Add(item);
            }

            return Ok(new { aconex, bd });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiProblem { Message = ex.Message });
        }
    }
}
