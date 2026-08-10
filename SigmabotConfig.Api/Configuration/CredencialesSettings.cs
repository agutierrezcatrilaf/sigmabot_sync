namespace SigmabotConfig.Api.Configuration;

/// <summary>
/// Cifrado de Aconex_Clave y BD_Clave en tabla Credenciales. La clave no se guarda en la BD.
/// </summary>
public sealed class CredencialesSettings
{
    public const string SectionName = "Credenciales";

    /// <summary>32 bytes en Base64 (AES-256). Vacío = sin cifrado (solo desarrollo).</summary>
    public string EncryptionKey { get; set; }
}
