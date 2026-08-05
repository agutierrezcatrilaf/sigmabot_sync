using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using SigmabotConfig.Api.Configuration;
using SigmabotConfig.Api.Models;

namespace SigmabotConfig.Api.Auth;

/// <summary>
/// Enfoque nuevo Salfa: exige Bearer y valida el recurso con /user/validarRecursoToken/{recurso}.
/// </summary>
public sealed class AuthFilter : IAsyncAuthorizationFilter
{
    private readonly IAutorizadorValidationService _validation;
    private readonly AutorizadorSettings _settings;

    public AuthFilter(IAutorizadorValidationService validation, IOptions<AutorizadorSettings> settings)
    {
        _validation = validation;
        _settings = settings.Value;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (!_settings.Enabled)
        {
            return;
        }

        var path = context.HttpContext.Request.Path;
        if (path.StartsWithSegments("/swagger") || path.StartsWithSegments("/health"))
        {
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue("Authorization", out var authHeaderValues))
        {
            context.Result = Unauthorized("No se encuentra autorizado para realizar acciones.");
            return;
        }

        var authHeader = authHeaderValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader))
        {
            context.Result = Unauthorized("No se encuentra autorizado para realizar acciones.");
            return;
        }

        var parts = authHeader.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !parts[0].Equals("Bearer", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(parts[1]))
        {
            context.Result = Unauthorized("Encabezado Authorization inválido.");
            return;
        }

        var accessToken = parts[1].Trim();
        var ok = await _validation.ValidarRecursoTokenAsync(accessToken, context.HttpContext.RequestAborted)
            .ConfigureAwait(false);

        if (!ok)
        {
            context.Result = Unauthorized("El token no está autorizado para este recurso.");
        }
    }

    private static ObjectResult Unauthorized(string message) =>
        new ObjectResult(new ApiProblem { Message = message }) { StatusCode = StatusCodes.Status401Unauthorized };
}
