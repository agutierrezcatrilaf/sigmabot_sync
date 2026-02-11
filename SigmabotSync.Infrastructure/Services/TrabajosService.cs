using System;
using System.Data.SqlClient;

namespace SigmabotSync.Infrastructure.Services
{
    /// <summary>
    /// Actualiza el estado y resultado de ejecución en la tabla Trabajos.
    /// </summary>
    public class TrabajosService
    {
        private readonly string _connectionString;

        public TrabajosService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        /// <summary>
        /// Registra el resultado de la última ejecución del trabajo (éxito o error).
        /// Actualiza el registro en Trabajos donde id = idTrabajo. El registro debe existir (id es IDENTITY).
        /// </summary>
        /// <param name="idTrabajo">Id del trabajo (coincide con la columna id en Trabajos).</param>
        /// <param name="exito">True si la ejecución fue exitosa, false si falló.</param>
        /// <param name="mensajeError">Mensaje de error o detalle cuando exito es false (se guarda en UltCorrEjecucion).</param>
        public void ActualizarResultadoEjecucion(int idTrabajo, bool exito, string mensajeError = null)
        {
            var ahora = DateTime.Now;
            var fecha = ahora.Date;
            var hora = ahora.ToString("HH:mm:ss");
            var resultado = exito ? "Exitoso" : "Error";
            var estado = exito ? "Completado" : "Error";
            var ultCorr = exito ? (string)null : (mensajeError ?? "Error en la ejecución");

            const string sql = @"
                UPDATE [Trabajos]
                SET
                    FechaUltimaEjecucion = @FechaUltimaEjecucion,
                    HoraUltimaEjecucion = @HoraUltimaEjecucion,
                    ResultadoUltimaEjecucion = @ResultadoUltimaEjecucion,
                    Estado = @Estado,
                    UltCorrEjecucion = @UltCorrEjecucion
                WHERE id = @Id";

            using (var cn = new SqlConnection(_connectionString))
            {
                cn.Open();
                using (var cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@Id", idTrabajo);
                    cmd.Parameters.AddWithValue("@FechaUltimaEjecucion", fecha);
                    cmd.Parameters.AddWithValue("@HoraUltimaEjecucion", hora);
                    cmd.Parameters.AddWithValue("@ResultadoUltimaEjecucion", resultado);
                    cmd.Parameters.AddWithValue("@Estado", estado);
                    cmd.Parameters.AddWithValue("@UltCorrEjecucion", (object)ultCorr ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
