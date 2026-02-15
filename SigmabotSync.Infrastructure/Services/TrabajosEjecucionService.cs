using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace SigmabotSync.Infrastructure.Services
{
    /// <summary>
    /// Inserta registros de historial de ejecución en la tabla TrabajosEjecucion.
    /// Un insert por cada ejecución (detalle, error, etapas ejecutadas).
    /// </summary>
    public class TrabajosEjecucionService
    {
        private readonly string _connectionString;

        public TrabajosEjecucionService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        /// <summary>
        /// Inserta un registro histórico de ejecución del trabajo.
        /// </summary>
        /// <param name="tipoEjecucion">"Manual" o "Scheduler".</param>
        public void Insertar(
            int idTrabajo,
            DateTime fechaHoraInicio,
            DateTime fechaHoraFin,
            bool exito,
            string mensajeError,
            IReadOnlyList<string> etapasEjecutadas,
            string detalleEjecucion = null,
            string tipoEjecucion = "Scheduler")
        {
            var etapas = etapasEjecutadas != null && etapasEjecutadas.Count > 0
                ? string.Join(",", etapasEjecutadas)
                : null;

            const string sql = @"
                INSERT INTO [dbo].[TrabajosEjecucion] (IdTrabajo, FechaHoraInicio, FechaHoraFin, Exito, MensajeError, EtapasEjecutadas, DetalleEjecucion, TipoEjecucion)
                VALUES (@IdTrabajo, @FechaHoraInicio, @FechaHoraFin, @Exito, @MensajeError, @EtapasEjecutadas, @DetalleEjecucion, @TipoEjecucion)";

            using (var cn = new SqlConnection(_connectionString))
            {
                cn.Open();
                using (var cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@IdTrabajo", idTrabajo);
                    cmd.Parameters.AddWithValue("@FechaHoraInicio", fechaHoraInicio);
                    cmd.Parameters.AddWithValue("@FechaHoraFin", fechaHoraFin);
                    cmd.Parameters.AddWithValue("@Exito", exito);
                    cmd.Parameters.AddWithValue("@MensajeError", (object)mensajeError ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@EtapasEjecutadas", (object)etapas ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@DetalleEjecucion", (object)detalleEjecucion ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@TipoEjecucion", (object)tipoEjecucion ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
