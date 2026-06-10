using Microsoft.AspNetCore.Mvc;
using SigmabotConfig.Api.Models;
using SigmabotConfig.Api.Services;

namespace SigmabotConfig.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected readonly IDatabaseConnectionProvider Db;

    protected ApiControllerBase(IDatabaseConnectionProvider db)
    {
        Db = db;
    }

    protected string ConnectionString => Db.GetConnectionString();

    protected IActionResult NotConfigured()
    {
        return StatusCode(503, new ApiProblem
        {
            Message = "Base de datos no configurada en el servidor (Database:ConnectionString en appsettings)."
        });
    }

    protected IActionResult ValidationProblem(IReadOnlyList<string> errors)
    {
        return BadRequest(new ApiProblem
        {
            Message = "Validación fallida.",
            Errors = errors
        });
    }
}
