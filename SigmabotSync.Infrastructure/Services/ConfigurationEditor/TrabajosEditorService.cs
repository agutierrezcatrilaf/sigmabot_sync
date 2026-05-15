using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using SigmabotSync.Domain.Entities;
using SigmabotSync.Infrastructure.Data;

namespace SigmabotSync.Infrastructure.Services.ConfigurationEditor
{
    /// <summary>
    /// Alta, baja y modificación de la tabla Trabajos (herramienta de configuración).
    /// </summary>
    public class TrabajosEditorService
    {
        private readonly string _connectionString;

        public TrabajosEditorService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public IReadOnlyList<Trabajo> ListarTodos()
        {
            const string sql = @"
                SELECT id AS Id, Nombre, Tipo, Perioricidad,
                    FechaUltimaEjecucion, FechaProximaEjecucion, ResultadoUltimaEjecucion,
                    ControldeEjecucion, Estado, UltCorrEjecucion
                FROM Trabajos
                ORDER BY id";

            var lista = new List<Trabajo>();
            using (var cn = new SqlConnection(_connectionString))
            {
                cn.Open();
                using (var cmd = new SqlCommand(sql, cn))
                using (var adapter = new SqlDataAdapter(cmd))
                {
                    var dt = new DataTable();
                    adapter.Fill(dt);
                    foreach (DataRow row in dt.Rows)
                        lista.Add(row.MapTo<Trabajo>());
                }
            }

            return lista;
        }

        /// <summary>Inserta un trabajo. Devuelve el id generado (IDENTITY).</summary>
        public int Insertar(Trabajo t)
        {
            const string sql = @"
                INSERT INTO Trabajos (
                    Nombre, Tipo, Perioricidad,
                    FechaUltimaEjecucion, FechaProximaEjecucion, ResultadoUltimaEjecucion,
                    ControldeEjecucion, Estado, UltCorrEjecucion)
                OUTPUT INSERTED.id
                VALUES (
                    @Nombre, @Tipo, @Perioricidad,
                    @FechaUltimaEjecucion, @FechaProximaEjecucion, @ResultadoUltimaEjecucion,
                    @ControldeEjecucion, @Estado, @UltCorrEjecucion)";

            using (var cn = new SqlConnection(_connectionString))
            {
                cn.Open();
                using (var cmd = new SqlCommand(sql, cn))
                {
                    AddInsertParameters(cmd, t);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }

        public void Actualizar(Trabajo t)
        {
            if (t.Id <= 0)
                throw new ArgumentException("Id inválido.", nameof(t));

            const string sql = @"
                UPDATE Trabajos SET
                    Nombre = @Nombre,
                    Tipo = @Tipo,
                    Perioricidad = @Perioricidad,
                    FechaUltimaEjecucion = @FechaUltimaEjecucion,
                    FechaProximaEjecucion = @FechaProximaEjecucion,
                    ResultadoUltimaEjecucion = @ResultadoUltimaEjecucion,
                    ControldeEjecucion = @ControldeEjecucion,
                    Estado = @Estado,
                    UltCorrEjecucion = @UltCorrEjecucion
                WHERE id = @Id";

            using (var cn = new SqlConnection(_connectionString))
            {
                cn.Open();
                using (var cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@Id", t.Id);
                    AddUpdateBodyParameters(cmd, t);
                    if (cmd.ExecuteNonQuery() == 0)
                        throw new InvalidOperationException("No existe Trabajo id=" + t.Id);
                }
            }
        }

        private static void AddInsertParameters(SqlCommand cmd, Trabajo t)
        {
            cmd.Parameters.AddWithValue("@Nombre", (object)t.Nombre ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Tipo", (object)t.Tipo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Perioricidad", (object)t.Perioricidad ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FechaUltimaEjecucion", (object)t.FechaUltimaEjecucion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@FechaProximaEjecucion", (object)t.FechaProximaEjecucion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ResultadoUltimaEjecucion", (object)t.ResultadoUltimaEjecucion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ControldeEjecucion", (object)t.ControldeEjecucion ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Estado", (object)t.Estado ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@UltCorrEjecucion", (object)t.UltCorrEjecucion ?? DBNull.Value);
        }

        private static void AddUpdateBodyParameters(SqlCommand cmd, Trabajo t)
        {
            AddInsertParameters(cmd, t);
        }

        /// <summary>Elimina un trabajo por id. Puede fallar si existen filas en TrabajosProgramacion u otras FK.</summary>
        public void Eliminar(int id)
        {
            if (id <= 0)
                throw new ArgumentOutOfRangeException(nameof(id));

            const string sql = "DELETE FROM Trabajos WHERE id = @Id";
            using (var cn = new SqlConnection(_connectionString))
            {
                cn.Open();
                using (var cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    int n = cmd.ExecuteNonQuery();
                    if (n == 0)
                        throw new InvalidOperationException("No existe Trabajo id=" + id);
                }
            }
        }
    }
}
