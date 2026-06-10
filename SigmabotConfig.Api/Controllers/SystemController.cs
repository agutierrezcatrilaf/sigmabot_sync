using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using SigmabotConfig.Api.Models;
using SigmabotConfig.Api.Services;

namespace SigmabotConfig.Api.Controllers;

[Route("api/[controller]")]
public sealed class SystemController : ControllerBase
{
    private readonly IDatabaseConnectionProvider _db;

    public SystemController(IDatabaseConnectionProvider db)
    {
        _db = db;
    }

    /// <summary>Estado de la BD configurada en appsettings (sin exponer la cadena).</summary>
    [HttpGet("status")]
    public ActionResult<SystemStatusResponse> GetStatus()
    {
        if (!_db.IsConfigured)
        {
            return Ok(new SystemStatusResponse
            {
                DatabaseConfigured = false,
                DatabaseReachable = false,
                Message = "Configure Database:ConnectionString en appsettings del servidor API."
            });
        }

        try
        {
            using (var cn = new SqlConnection(_db.GetConnectionString()))
            {
                cn.Open();
            }

            return Ok(new SystemStatusResponse
            {
                DatabaseConfigured = true,
                DatabaseReachable = true,
                Message = "Conexión a SQL Server correcta."
            });
        }
        catch (Exception ex)
        {
            return Ok(new SystemStatusResponse
            {
                DatabaseConfigured = true,
                DatabaseReachable = false,
                Message = "Error al conectar: " + ex.Message
            });
        }
    }
}
