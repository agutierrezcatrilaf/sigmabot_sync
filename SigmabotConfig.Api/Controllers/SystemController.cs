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

    /// <summary>Estado de la BD configurada en appsettings (servidor/BD sin password).</summary>
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

        TryParseEndpoint(_db.GetConnectionString(), out var server, out var database);

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
                DatabaseServer = server,
                DatabaseName = database,
                Message = "Conexión a SQL Server correcta."
            });
        }
        catch (Exception ex)
        {
            return Ok(new SystemStatusResponse
            {
                DatabaseConfigured = true,
                DatabaseReachable = false,
                DatabaseServer = server,
                DatabaseName = database,
                Message = "Error al conectar: " + ex.Message
            });
        }
    }

    private static void TryParseEndpoint(string connectionString, out string server, out string database)
    {
        server = null;
        database = null;
        if (string.IsNullOrWhiteSpace(connectionString))
            return;
        try
        {
            var b = new SqlConnectionStringBuilder(connectionString);
            server = string.IsNullOrWhiteSpace(b.DataSource) ? null : b.DataSource.Trim();
            database = string.IsNullOrWhiteSpace(b.InitialCatalog) ? null : b.InitialCatalog.Trim();
        }
        catch
        {
            // No exponer la cadena cruda si no parsea.
        }
    }
}
