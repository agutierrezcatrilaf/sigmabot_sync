namespace SigmabotConfig.Api.Models;

public sealed class ApiProblem
{
    public string Message { get; set; }
    public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();
}
