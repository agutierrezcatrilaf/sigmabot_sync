using Microsoft.AspNetCore.Mvc;
using SigmabotConfig.Api.Models;
using SigmabotConfig.Api.Services;
using SigmabotSync.Domain.Configuration;
using SigmabotSync.Infrastructure.Services.ConfigurationEditor;

namespace SigmabotConfig.Api.Controllers;

[Route("api/trabajos/{idTrabajo:int}/project-sync")]
public sealed class ProjectSyncConfigController : ApiControllerBase
{
    public ProjectSyncConfigController(IDatabaseConnectionProvider db) : base(db) { }

    [HttpGet]
    public IActionResult Obtener(int idTrabajo, [FromQuery] bool invertir = false)
    {
        if (!Db.IsConfigured)
            return NotConfigured();

        try
        {
            var ctx = ResolverContexto(idTrabajo, invertir);
            if (ctx.Result != null)
                return ctx.Result;

            var svc = new ProjectSyncConfigEditorService(ConnectionString);
            return Ok(new ProjectSyncConfigResponse
            {
                IdTrabajo = idTrabajo,
                TipoTrabajo = TipoTrabajoIds.ProjectSync,
                AcxProjectIdLado1 = ctx.Lado1,
                AcxProjectIdLado2 = ctx.Lado2,
                SentidoInvertido = invertir,
                AcxProjectIdOrigen = ctx.Origen,
                AcxProjectIdDestino = ctx.Destino,
                CamposDestino = svc.ListarCamposDestino(idTrabajo, ctx.Origen, ctx.Destino).Select(ToDto).ToList(),
                Equivalencias = svc.ListarEquivalencias(idTrabajo, ctx.Origen, ctx.Destino).Select(ToDto).ToList()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiProblem { Message = ex.Message });
        }
    }

    [HttpPut("campos-destino")]
    public IActionResult GuardarCamposDestino(
        int idTrabajo,
        [FromBody] GuardarProjectSyncCamposDestinoRequest request,
        [FromQuery] bool invertir = false)
    {
        if (!Db.IsConfigured)
            return NotConfigured();

        try
        {
            var ctx = ResolverContexto(idTrabajo, invertir);
            if (ctx.Result != null)
                return ctx.Result;

            var campos = request?.CamposDestino ?? Array.Empty<ProjectSyncCampoDestinoDto>();
            var errores = ValidarCamposDestino(campos);
            if (errores.Count > 0)
                return ValidationProblem(errores);

            var svc = new ProjectSyncConfigEditorService(ConnectionString);
            svc.ReemplazarCamposDestino(idTrabajo, ctx.Origen, ctx.Destino, campos.Select(ToFila).ToList());
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiProblem { Message = ex.Message });
        }
    }

    [HttpPut("equivalencias")]
    public IActionResult GuardarEquivalencias(
        int idTrabajo,
        [FromBody] GuardarProjectSyncEquivalenciasRequest request,
        [FromQuery] bool invertir = false)
    {
        if (!Db.IsConfigured)
            return NotConfigured();

        try
        {
            var ctx = ResolverContexto(idTrabajo, invertir);
            if (ctx.Result != null)
                return ctx.Result;

            var equivalencias = request?.Equivalencias ?? Array.Empty<ProjectSyncEquivalenciaDto>();
            var errores = ValidarEquivalencias(equivalencias);
            if (errores.Count > 0)
                return ValidationProblem(errores);

            var svc = new ProjectSyncConfigEditorService(ConnectionString);
            svc.ReemplazarEquivalencias(idTrabajo, ctx.Origen, ctx.Destino, equivalencias.Select(ToFila).ToList());
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiProblem { Message = ex.Message });
        }
    }

    private (IActionResult Result, string Lado1, string Lado2, string Origen, string Destino) ResolverContexto(
        int idTrabajo,
        bool invertir)
    {
        var trabajosSvc = new TrabajosEditorService(ConnectionString);
        var trabajo = trabajosSvc.ListarTodos().FirstOrDefault(t => t.Id == idTrabajo);
        if (trabajo == null)
            return (NotFound(new ApiProblem { Message = "Trabajo no encontrado." }), null, null, null, null);

        if (!string.Equals((trabajo.Tipo ?? "").Trim(), TipoTrabajoIds.ProjectSync, StringComparison.OrdinalIgnoreCase))
        {
            return (BadRequest(new ApiProblem
            {
                Message = "La matriz destino y equivalencias solo aplican a trabajos ProjectSync."
            }), null, null, null, null);
        }

        var cfgSvc = new TrabajosConfiguracionEditorService(ConnectionString);
        var valores = cfgSvc.ListarPorIdTrabajo(idTrabajo)
            .Where(f => !string.IsNullOrWhiteSpace(f.Nombre))
            .ToDictionary(f => f.Nombre.Trim(), f => f.ValorTexto ?? "", StringComparer.OrdinalIgnoreCase);

        valores.TryGetValue(TrabajosConfiguracionKeyNames.IdProyecto, out var lado1);
        valores.TryGetValue(TrabajosConfiguracionKeyNames.IdProyecto2, out var lado2);

        if (string.IsNullOrWhiteSpace(lado1) || string.IsNullOrWhiteSpace(lado2))
        {
            return (BadRequest(new ApiProblem
            {
                Message = "Configure IdProyecto e IdProyecto2 antes de editar ProjectSync."
            }), null, null, null, null);
        }

        lado1 = lado1.Trim();
        lado2 = lado2.Trim();
        string origen = invertir ? lado2 : lado1;
        string destino = invertir ? lado1 : lado2;
        return (null, lado1, lado2, origen, destino);
    }

    private static IReadOnlyList<string> ValidarCamposDestino(IReadOnlyList<ProjectSyncCampoDestinoDto> campos)
    {
        var errors = new List<string>();
        if (campos == null)
            return errors;

        for (int i = 0; i < campos.Count; i++)
        {
            var fila = campos[i];
            if (string.IsNullOrWhiteSpace(fila?.CampoDestino))
                errors.Add($"Fila #{i + 1}: Campo destino es obligatorio.");
            if (string.IsNullOrWhiteSpace(fila?.TipoFuente))
                errors.Add($"Fila #{i + 1}: Tipo fuente es obligatorio.");
            else if (!EsTipoFuenteValido(fila.TipoFuente))
                errors.Add($"Fila #{i + 1}: Tipo fuente «{fila.TipoFuente}» no reconocido.");
        }
        return errors;
    }

    private static bool EsTipoFuenteValido(string tipo)
    {
        string t = (tipo ?? "").Trim();
        return string.Equals(t, ProjectSyncCampoDestinoTipoFuente.CampoOrigen, StringComparison.OrdinalIgnoreCase)
            || string.Equals(t, ProjectSyncCampoDestinoTipoFuente.ReglaDocumentTypeFromTipo, StringComparison.OrdinalIgnoreCase)
            || string.Equals(t, ProjectSyncCampoDestinoTipoFuente.ParametroIdEstatusDestino, StringComparison.OrdinalIgnoreCase)
            || string.Equals(t, ProjectSyncCampoDestinoTipoFuente.Adjunto, StringComparison.OrdinalIgnoreCase)
            || string.Equals(t, ProjectSyncCampoDestinoTipoFuente.Constante, StringComparison.OrdinalIgnoreCase)
            || string.Equals(t, ProjectSyncCampoDestinoTipoFuente.SoloPreservar, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ValidarEquivalencias(IReadOnlyList<ProjectSyncEquivalenciaDto> equivalencias)
    {
        var errors = new List<string>();
        if (equivalencias == null)
            return errors;

        for (int i = 0; i < equivalencias.Count; i++)
        {
            var fila = equivalencias[i];
            if (fila == null)
                continue;
            if (string.IsNullOrWhiteSpace(fila.Tipo))
                errors.Add($"Fila equivalencias #{i + 1}: Tipo es obligatorio.");
            if (string.IsNullOrWhiteSpace(fila.ValorOrigen))
                errors.Add($"Fila equivalencias #{i + 1}: Valor origen es obligatorio.");
            if (string.IsNullOrWhiteSpace(fila.ValorDestino))
                errors.Add($"Fila equivalencias #{i + 1}: Valor destino es obligatorio.");
            if (string.IsNullOrWhiteSpace(fila.CodigoDestino))
                errors.Add($"Fila equivalencias #{i + 1}: Código destino es obligatorio.");
        }
        return errors;
    }

    private static ProjectSyncCampoDestinoDto ToDto(ProjectSyncCampoDestinoFila fila) => new()
    {
        AcxProjectIdOrigen = fila.AcxProjectIdOrigen,
        AcxProjectIdDestino = fila.AcxProjectIdDestino,
        CampoDestino = fila.CampoDestino,
        TipoFuente = fila.TipoFuente,
        FuenteValor = fila.FuenteValor,
        EsObligatorio = fila.EsObligatorio,
        ValorDefault = fila.ValorDefault,
        Catalogo = fila.Catalogo,
        Orden = fila.Orden,
        Activo = fila.Activo
    };

    private static ProjectSyncCampoDestinoFila ToFila(ProjectSyncCampoDestinoDto dto) => new()
    {
        CampoDestino = dto.CampoDestino,
        TipoFuente = dto.TipoFuente,
        FuenteValor = dto.FuenteValor,
        EsObligatorio = dto.EsObligatorio,
        ValorDefault = dto.ValorDefault,
        Catalogo = dto.Catalogo,
        Orden = dto.Orden,
        Activo = dto.Activo
    };

    private static ProjectSyncEquivalenciaDto ToDto(ProjectSyncEquivalenciaFila fila) => new()
    {
        AcxProjectIdOrigen = fila.AcxProjectIdOrigen,
        AcxProjectIdDestino = fila.AcxProjectIdDestino,
        Tipo = fila.Tipo,
        ValorOrigen = fila.ValorOrigen,
        ValorDestino = fila.ValorDestino,
        CodigoDestino = fila.CodigoDestino,
        Activo = fila.Activo
    };

    private static ProjectSyncEquivalenciaFila ToFila(ProjectSyncEquivalenciaDto dto) => new()
    {
        Tipo = dto.Tipo,
        ValorOrigen = dto.ValorOrigen,
        ValorDestino = dto.ValorDestino,
        CodigoDestino = dto.CodigoDestino,
        Activo = dto.Activo
    };
}
