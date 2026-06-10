namespace SigmabotConfig.Api.Configuration;

public sealed class CorsSettings
{
    public const string SectionName = "Cors";

    /// <summary>Orígenes permitidos para el front Angular (ej. http://localhost:4200).</summary>
    public string[] AllowedOrigins { get; set; } = { "http://localhost:4200" };
}
