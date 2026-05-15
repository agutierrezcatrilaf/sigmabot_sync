using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using SigmabotSync.Domain.Entities;

namespace SigmabotSync.Infrastructure.Services.ConfigurationEditor
{
    /// <summary>CRUD de <c>TrabajosProgramacion</c> para la herramienta de configuración.</summary>
    public class TrabajosProgramacionEditorService
    {
        private readonly string _connectionString;

        public TrabajosProgramacionEditorService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public IReadOnlyList<TrabajoProgramacion> ListarPorIdTrabajo(int idTrabajo)
        {
            var inner = new TrabajosProgramacionService(_connectionString);
            return inner.ObtenerPorIdTrabajo(idTrabajo);
        }

        public int Insertar(TrabajoProgramacion fila)
        {
            if (fila.IdTrabajo <= 0)
                throw new ArgumentException("IdTrabajo inválido.", nameof(fila));
            if (fila.DiaSemana < 0 || fila.DiaSemana > 6)
                throw new ArgumentException("DiaSemana debe estar entre 0 (domingo) y 6 (sábado).", nameof(fila));

            const string sql = @"
                INSERT INTO [dbo].[TrabajosProgramacion] (IdTrabajo, DiaSemana, Hora, Activo)
                OUTPUT INSERTED.Id
                VALUES (@IdTrabajo, @DiaSemana, @Hora, @Activo)";

            using (var cn = new SqlConnection(_connectionString))
            {
                cn.Open();
                using (var cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@IdTrabajo", fila.IdTrabajo);
                    cmd.Parameters.AddWithValue("@DiaSemana", fila.DiaSemana);
                    cmd.Parameters.Add("@Hora", SqlDbType.Time).Value = fila.Hora;
                    cmd.Parameters.AddWithValue("@Activo", fila.Activo);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public void Actualizar(TrabajoProgramacion fila)
        {
            if (fila.Id <= 0)
                throw new ArgumentException("Id inválido.", nameof(fila));
            if (fila.DiaSemana < 0 || fila.DiaSemana > 6)
                throw new ArgumentException("DiaSemana debe estar entre 0 y 6.", nameof(fila));

            const string sql = @"
                UPDATE [dbo].[TrabajosProgramacion]
                SET IdTrabajo = @IdTrabajo, DiaSemana = @DiaSemana, Hora = @Hora, Activo = @Activo
                WHERE Id = @Id";

            using (var cn = new SqlConnection(_connectionString))
            {
                cn.Open();
                using (var cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@Id", fila.Id);
                    cmd.Parameters.AddWithValue("@IdTrabajo", fila.IdTrabajo);
                    cmd.Parameters.AddWithValue("@DiaSemana", fila.DiaSemana);
                    cmd.Parameters.Add("@Hora", SqlDbType.Time).Value = fila.Hora;
                    cmd.Parameters.AddWithValue("@Activo", fila.Activo);
                    if (cmd.ExecuteNonQuery() == 0)
                        throw new InvalidOperationException("No existe programación id=" + fila.Id);
                }
            }
        }

        public void Eliminar(int id)
        {
            const string sql = "DELETE FROM [dbo].[TrabajosProgramacion] WHERE Id = @Id";
            using (var cn = new SqlConnection(_connectionString))
            {
                cn.Open();
                using (var cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
