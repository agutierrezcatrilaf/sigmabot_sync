using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using SigmabotSync.Domain.Entities;

namespace SigmabotSync.Infrastructure.Services
{
    /// <summary>
    /// Operaciones sobre la tabla TrabajosProgramacion.
    /// Permite obtener los trabajos que deben ejecutarse ahora y que aún no se han ejecutado en su ventana horaria de hoy.
    /// </summary>
    public class TrabajosProgramacionService
    {
        private readonly string _connectionString;

        public TrabajosProgramacionService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        /// <summary>
        /// Obtiene los IdTrabajo que están programados para ejecutarse "ahora" (mismo día de la semana, hora ya pasada o actual),
        /// están activos, el trabajo está Activo en Trabajos, y no se ha ejecutado ya hoy para esa programación
        /// (evita repetir: si debía ejecutarse a las 01:30 y ya se ejecutó a las 01:34, no se vuelve a incluir).
        /// Usa la fecha de <paramref name="ahora"/> para detectar "ya ejecutado hoy", no GETDATE(), para evitar desfases por zona horaria.
        /// </summary>
        /// <param name="ahora">Fecha/hora de referencia (normalmente DateTime.Now).</param>
        /// <returns>Lista de IdTrabajo únicos a ejecutar.</returns>
        public IReadOnlyList<int> ObtenerTrabajosPendientesDeEjecucion(DateTime ahora)
        {
            // En .NET DayOfWeek: 0=Dom, 1=Lun, ..., 6=Sab. TrabajosProgramacion usa el mismo esquema.
            int diaSemana = (int)ahora.DayOfWeek;
            var horaActual = ahora.TimeOfDay;
            var fechaReferencia = ahora.Date;

            const string sql = @"
                SELECT DISTINCT tp.IdTrabajo
                FROM [dbo].[TrabajosProgramacion] tp
                INNER JOIN [dbo].[Trabajos] t ON t.id = tp.IdTrabajo AND t.Estado = 'Activo'
                WHERE tp.Activo = 1
                  AND tp.DiaSemana = @DiaSemana
                  AND tp.Hora <= @HoraActual
                  AND NOT EXISTS (
                      SELECT 1 FROM [dbo].[TrabajosEjecucion] e
                      WHERE e.IdTrabajo = tp.IdTrabajo
                        AND CAST(e.FechaHoraInicio AS DATE) = @FechaReferencia
                        AND CAST(e.FechaHoraInicio AS TIME) >= tp.Hora
                  )";

            var lista = new List<int>();
            using (var cn = new SqlConnection(_connectionString))
            {
                cn.Open();
                using (var cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@DiaSemana", diaSemana);
                    cmd.Parameters.Add("@HoraActual", SqlDbType.Time).Value = horaActual;
                    cmd.Parameters.Add("@FechaReferencia", SqlDbType.Date).Value = fechaReferencia;

                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                            lista.Add(rdr.GetInt32(0));
                    }
                }
            }
            return lista;
        }

        /// <summary>
        /// Lista todas las programaciones activas de un trabajo (para administración).
        /// </summary>
        public IReadOnlyList<TrabajoProgramacion> ObtenerPorIdTrabajo(int idTrabajo)
        {
            const string sql = @"
                SELECT Id, IdTrabajo, DiaSemana, Hora, Activo
                FROM [dbo].[TrabajosProgramacion]
                WHERE IdTrabajo = @IdTrabajo
                ORDER BY DiaSemana, Hora";

            var lista = new List<TrabajoProgramacion>();
            using (var cn = new SqlConnection(_connectionString))
            {
                cn.Open();
                using (var cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@IdTrabajo", idTrabajo);
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            lista.Add(new TrabajoProgramacion
                            {
                                Id = rdr.GetInt32(0),
                                IdTrabajo = rdr.GetInt32(1),
                                DiaSemana = rdr.GetInt32(2),
                                Hora = rdr.IsDBNull(3) ? TimeSpan.Zero : rdr.GetTimeSpan(3),
                                Activo = rdr.GetBoolean(4)
                            });
                        }
                    }
                }
            }
            return lista;
        }
    }
}
