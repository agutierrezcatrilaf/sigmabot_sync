namespace SigmabotConfig.Api.Models;

public sealed class CampoDefinicionDto
{
    public string Clave { get; set; }
    public string Etiqueta { get; set; }
    public string Ayuda { get; set; }
    public bool Obligatorio { get; set; }
}
