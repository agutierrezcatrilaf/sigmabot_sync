namespace SigmabotConfig.Api.Models;

public sealed class ProjectSyncCampoDestinoDto
{
    public string AcxProjectIdOrigen { get; set; }
    public string AcxProjectIdDestino { get; set; }
    public string CampoDestino { get; set; }
    public string TipoFuente { get; set; }
    public string FuenteValor { get; set; }
    public bool EsObligatorio { get; set; }
    public string ValorDefault { get; set; }
    public string Catalogo { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
}

public sealed class ProjectSyncEquivalenciaDto
{
    public string AcxProjectIdOrigen { get; set; }
    public string AcxProjectIdDestino { get; set; }
    public string Tipo { get; set; }
    public string ValorOrigen { get; set; }
    public string ValorDestino { get; set; }
    public string CodigoDestino { get; set; }
    public bool Activo { get; set; } = true;
}

public sealed class ProjectSyncConfigResponse
{
    public int IdTrabajo { get; set; }
    public string TipoTrabajo { get; set; }
    public string AcxProjectIdLado1 { get; set; }
    public string AcxProjectIdLado2 { get; set; }
    public bool SentidoInvertido { get; set; }
    public string AcxProjectIdOrigen { get; set; }
    public string AcxProjectIdDestino { get; set; }
    public IReadOnlyList<ProjectSyncCampoDestinoDto> CamposDestino { get; set; } = Array.Empty<ProjectSyncCampoDestinoDto>();
    public IReadOnlyList<ProjectSyncEquivalenciaDto> Equivalencias { get; set; } = Array.Empty<ProjectSyncEquivalenciaDto>();
}

public sealed class GuardarProjectSyncCamposDestinoRequest
{
    public IReadOnlyList<ProjectSyncCampoDestinoDto> CamposDestino { get; set; } = Array.Empty<ProjectSyncCampoDestinoDto>();
}

public sealed class GuardarProjectSyncEquivalenciasRequest
{
    public IReadOnlyList<ProjectSyncEquivalenciaDto> Equivalencias { get; set; } = Array.Empty<ProjectSyncEquivalenciaDto>();
}
