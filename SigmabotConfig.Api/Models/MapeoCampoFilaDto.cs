namespace SigmabotConfig.Api.Models;

public sealed class MapeoCampoFilaDto
{
    public string Api { get; set; } = string.Empty;
    public string Json { get; set; } = string.Empty;
    public string Bd { get; set; } = string.Empty;
}

public sealed class PlantillaMapeoResumenDto
{
    public string Id { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}

public sealed class PlantillaMapeoDetalleDto
{
    public string Id { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public List<MapeoCampoFilaDto> Filas { get; set; } = new();
}
