namespace SigmabotConfig.Api.Models;

public sealed class TipoTrabajoDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; }
    public int Orden { get; set; }
}
