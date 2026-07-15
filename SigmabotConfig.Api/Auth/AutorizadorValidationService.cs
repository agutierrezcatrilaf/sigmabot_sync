using Microsoft.Extensions.Options;
using SigmabotConfig.Api.Configuration;

namespace SigmabotConfig.Api.Auth;

public interface IAutorizadorValidationService
{
    Task<bool> ValidarRecursoTokenAsync(string accessToken, CancellationToken cancellationToken = default);
}

public sealed class AutorizadorValidationService : IAutorizadorValidationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AutorizadorSettings _settings;
    private readonly ILogger<AutorizadorValidationService> _logger;

    public AutorizadorValidationService(
        IHttpClientFactory httpClientFactory,
        IOptions<AutorizadorSettings> settings,
        ILogger<AutorizadorValidationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> ValidarRecursoTokenAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.UrlApi) || string.IsNullOrWhiteSpace(_settings.Recurso))
        {
            _logger.LogError("Autorizador:UrlApi o Autorizador:Recurso no configurados.");
            return false;
        }

        var client = _httpClientFactory.CreateClient("Autorizador");
        var recurso = Uri.EscapeDataString(_settings.Recurso.Trim());
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"user/validarRecursoToken/{recurso}");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        _logger.LogWarning(
            "validarRecursoToken falló: {StatusCode} para recurso {Recurso}",
            (int)response.StatusCode,
            _settings.Recurso);
        return false;
    }
}
