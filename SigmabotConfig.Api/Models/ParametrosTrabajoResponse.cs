namespace SigmabotConfig.Api.Models;

public sealed class ParametrosTrabajoResponse
{
    public int IdTrabajo { get; set; }
    public string TipoTrabajo { get; set; }
    public bool FormularioGuiado { get; set; }
    public IReadOnlyList<CampoDefinicionDto> Definiciones { get; set; }
    public IReadOnlyList<ParametroValorDto> Valores { get; set; }
}

public sealed class ParametroValorDto
{
    public string Clave { get; set; }
    public string Valor { get; set; }
}

public sealed class GuardarParametrosRequest
{
    public Dictionary<string, string> Valores { get; set; }
}
