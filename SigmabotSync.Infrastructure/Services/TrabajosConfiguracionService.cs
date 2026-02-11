using System;
using System.Data;
using System.Data.SqlClient;
using SigmabotSync.Domain.Entities;
using SigmabotSync.Infrastructure.Data;

namespace SigmabotSync.Infrastructure.Services
{
    /// <summary>
    /// Lee la configuración del trabajo desde la tabla TrabajosConfiguracion.
    /// Por defecto se usa IdTrabajo = 1.
    /// </summary>
    public class TrabajosConfiguracionService
    {
        private readonly string _connectionString;

        public TrabajosConfiguracionService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        /// <summary>
        /// Obtiene la configuración del trabajo por IdTrabajo. La tabla es clave-valor: cada parámetro es una fila con Nombre y ValorTexto.
        /// Devuelve null si no hay filas para el trabajo o falta IdProyecto.
        /// </summary>
        public TrabajoConfiguracion GetByIdTrabajo(int idTrabajo = 1)
        {
            const string sql = @"
                SELECT idTrabajo, Nombre, ValorTexto
                FROM [TrabajosConfiguracion]
                WHERE idTrabajo = @IdTrabajo";

            using (var cn = new SqlConnection(_connectionString))
            {
                cn.Open();
                using (var cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@IdTrabajo", idTrabajo);
                    using (var adapter = new SqlDataAdapter(cmd))
                    {
                        var dt = new DataTable();
                        adapter.Fill(dt);
                        if (dt.Rows.Count == 0)
                            return null;

                        var result = new TrabajoConfiguracion { IdTrabajo = idTrabajo };
                        foreach (DataRow row in dt.Rows)
                        {
                            var nombre = (row["Nombre"] as string)?.Trim();
                            var valor = (row["ValorTexto"] as string)?.Trim();
                            if (string.IsNullOrEmpty(nombre)) continue;

                            switch (nombre)
                            {
                                case "Proyecto":
                                    result.Proyecto = valor;
                                    break;
                                case "IdProyecto":
                                    result.IdProyecto = valor;
                                    break;
                                case "CamposConsulta":
                                    result.CamposConsulta = valor;
                                    break;
                                case "CamposResponse":
                                    result.CamposResponse = valor;
                                    break;
                                case "CamposBD":
                                    result.CamposBD = valor;
                                    break;
                                case "BasePath":
                                    result.BasePath = valor;
                                    break;
                                case "CredencialAconex":
                                    if (int.TryParse(valor, out int idAconex))
                                        result.CredencialAconexId = idAconex;
                                    break;
                                case "CredencialBD":
                                    if (int.TryParse(valor, out int idBd))
                                        result.CredencialBDId = idBd;
                                    break;
                            }
                        }

                        if (string.IsNullOrWhiteSpace(result.IdProyecto))
                            return null;
                        return result;
                    }
                }
            }
        }
    }
}
