namespace SigmabotConfig.Api.Models;

public sealed class SystemStatusResponse
{
    public bool DatabaseConfigured { get; set; }
    public bool DatabaseReachable { get; set; }
    public string Message { get; set; }
}
