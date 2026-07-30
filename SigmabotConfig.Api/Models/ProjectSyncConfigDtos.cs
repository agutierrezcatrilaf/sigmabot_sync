namespace SigmabotConfig.Api.Models;

public sealed class ProjectSyncCampoDto
{
    public string AcxProjectIdOrigen { get; set; }
    public string AcxProjectIdDestino { get; set; }
    public string Campo { get; set; }
    public string CampoOrigen { get; set; }
    public bool EsObligatorio { get; set; }
    public string ValorDefault { get; set; }
    public string Catalogo { get; set; }
    public int Orden { get; set; }
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
    /// <summary>IdProyecto del trabajo (lado 1).</summary>
    public string AcxProjectIdLado1 { get; set; }
    /// <summary>IdProyecto2 del trabajo (lado 2).</summary>
    public string AcxProjectIdLado2 { get; set; }
    /// <summary>true = se edita lado2→lado1; false = lado1→lado2.</summary>
    public bool SentidoInvertido { get; set; }
    public string AcxProjectIdOrigen { get; set; }
    public string AcxProjectIdDestino { get; set; }
    public IReadOnlyList<ProjectSyncCampoDto> Campos { get; set; } = Array.Empty<ProjectSyncCampoDto>();
    public IReadOnlyList<ProjectSyncEquivalenciaDto> Equivalencias { get; set; } = Array.Empty<ProjectSyncEquivalenciaDto>();
}

public sealed class GuardarProjectSyncCamposRequest
{
    public IReadOnlyList<ProjectSyncCampoDto> Campos { get; set; } = Array.Empty<ProjectSyncCampoDto>();
}

public sealed class GuardarProjectSyncEquivalenciasRequest
{
    public IReadOnlyList<ProjectSyncEquivalenciaDto> Equivalencias { get; set; } = Array.Empty<ProjectSyncEquivalenciaDto>();
}
