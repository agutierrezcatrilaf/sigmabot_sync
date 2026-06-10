using Microsoft.AspNetCore.Mvc;
using SigmabotConfig.Api.Models;
using SigmabotConfig.Api.Services;
using SigmabotSync.Domain.Configuration;
using SigmabotSync.Infrastructure.Services.ConfigurationEditor;

namespace SigmabotConfig.Api.Controllers;

[Route("api/trabajos/{idTrabajo:int}/parametros")]
public sealed class TrabajosConfiguracionController : ApiControllerBase
{
    public TrabajosConfiguracionController(IDatabaseConnectionProvider db) : base(db) { }

    [HttpGet]
    public IActionResult Obtener(int idTrabajo)
    {
        if (!Db.IsConfigured)
            return NotConfigured();

        try
        {
            var trabajosSvc = new TrabajosEditorService(ConnectionString);
            var trabajo = trabajosSvc.ListarTodos().FirstOrDefault(t => t.Id == idTrabajo);
            if (trabajo == null)
                return NotFound(new ApiProblem { Message = "Trabajo no encontrado." });

            var tipo = (trabajo.Tipo ?? string.Empty).Trim();
            var guiado = TrabajoTipoConfigFieldCatalog.TipoSoportaFormularioGuiado(tipo);
            var defs = TrabajoTipoConfigFieldCatalog.ObtenerCamposParaFormulario(tipo)
                .Select(d => new CampoDefinicionDto
                {
                    Clave = d.Clave,
                    Etiqueta = d.Etiqueta,
                    Ayuda = d.Ayuda,
                    Obligatorio = d.EsObligatorioPara(tipo)
                })
                .ToList();

            var porNombre = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (guiado)
            {
                var cfgSvc = new TrabajosConfiguracionEditorService(ConnectionString);
                foreach (var f in cfgSvc.ListarPorIdTrabajo(idTrabajo))
                {
                    var k = (f.Nombre ?? string.Empty).Trim();
                    if (k.Length > 0)
                        porNombre[k] = f.ValorTexto ?? string.Empty;
                }
            }

            var valores = new List<ParametroValorDto>();
            foreach (var def in defs)
            {
                porNombre.TryGetValue(def.Clave, out var v);
                var valor = v ?? string.Empty;
                if (string.IsNullOrWhiteSpace(valor)
                    && string.Equals(tipo, TipoTrabajoIds.FileUploadWithMetadata, StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(def.Clave, TrabajosConfiguracionKeyNames.TablaMetadata, StringComparison.OrdinalIgnoreCase))
                        valor = FileUploadWithMetadataDefaults.TablaMetadata;
                    else if (string.Equals(def.Clave, TrabajosConfiguracionKeyNames.TablaPaths, StringComparison.OrdinalIgnoreCase))
                        valor = FileUploadWithMetadataDefaults.TablaPaths;
                }
                valores.Add(new ParametroValorDto { Clave = def.Clave, Valor = valor });
            }

            return Ok(new ParametrosTrabajoResponse
            {
                IdTrabajo = idTrabajo,
                TipoTrabajo = tipo,
                FormularioGuiado = guiado,
                Definiciones = defs,
                Valores = valores
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiProblem { Message = ex.Message });
        }
    }

    [HttpPut]
    public IActionResult Guardar(int idTrabajo, [FromBody] GuardarParametrosRequest request)
    {
        if (!Db.IsConfigured)
            return NotConfigured();

        try
        {
            var trabajosSvc = new TrabajosEditorService(ConnectionString);
            var trabajo = trabajosSvc.ListarTodos().FirstOrDefault(t => t.Id == idTrabajo);
            if (trabajo == null)
                return NotFound(new ApiProblem { Message = "Trabajo no encontrado." });

            var tipo = (trabajo.Tipo ?? string.Empty).Trim();
            if (!TrabajoTipoConfigFieldCatalog.TipoSoportaFormularioGuiado(tipo))
            {
                return BadRequest(new ApiProblem
                {
                    Message = "Este tipo de trabajo no tiene formulario guiado; no se guardan parámetros desde aquí."
                });
            }

            var dict = request?.Valores ?? new Dictionary<string, string>();
            var valParams = TrabajoConfiguracionParamValidator.ValidarObligatoriosPorTipo(tipo, dict);
            if (valParams.Count > 0)
                return ValidationProblem(valParams);

            var cfgSvc = new TrabajosConfiguracionEditorService(ConnectionString);
            foreach (var campo in TrabajoTipoConfigFieldCatalog.ObtenerCamposParaFormulario(tipo))
            {
                dict.TryGetValue(campo.Clave, out var valor);
                cfgSvc.UpsertValorTexto(idTrabajo, campo.Clave, valor ?? string.Empty);
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiProblem { Message = ex.Message });
        }
    }
}
