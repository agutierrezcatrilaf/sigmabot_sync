using System;
using System.Data;
using System.Data.SqlClient;
using SigmabotSync.Domain.Entities;
using SigmabotSync.Infrastructure.Data;

namespace SigmabotSync.Infrastructure.Services
{
    /// <summary>
    /// Operaciones sobre la tabla Trabajos y su configuración (TrabajosConfiguracion).
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
            var fechaHoraUltimaEjecucion = DateTime.Now;
            var resultado = exito ? "Exitoso" : "Error";
            var ultCorr = exito ? (string)null : (mensajeError ?? "Error en la ejecución");

            const string sql = @"
                UPDATE [Trabajos]
                SET
                    FechaUltimaEjecucion = @FechaUltimaEjecucion,
                    ResultadoUltimaEjecucion = @ResultadoUltimaEjecucion,
                    UltCorrEjecucion = @UltCorrEjecucion
                WHERE id = @Id";

            using (var cn = new SqlConnection(_connectionString))
            {
                cn.Open();
                using (var cmd = new SqlCommand(sql, cn))
                {
                    cmd.Parameters.AddWithValue("@Id", idTrabajo);
                    cmd.Parameters.AddWithValue("@FechaUltimaEjecucion", fechaHoraUltimaEjecucion);
                    cmd.Parameters.AddWithValue("@ResultadoUltimaEjecucion", resultado);
                    cmd.Parameters.AddWithValue("@UltCorrEjecucion", (object)ultCorr ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Obtiene la configuración del trabajo por IdTrabajo desde TrabajosConfiguracion.
        /// Solo considera trabajos cuyo Estado = 'Activo' en la tabla Trabajos.
        /// Devuelve null si no hay filas para el trabajo, si el trabajo no está Activo o si falta IdProyecto.
        /// </summary>
        public TrabajoConfiguracion GetConfiguracionByIdTrabajo(int idTrabajo)
        {
            const string sql = @"
                SELECT tc.idTrabajo, tc.Nombre, tc.ValorTexto, t.Tipo
                FROM [TrabajosConfiguracion] tc
                INNER JOIN [Trabajos] t ON t.id = tc.idTrabajo
                WHERE tc.idTrabajo = @IdTrabajo
                  AND t.Estado = 'Activo'";

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
                        // Tipo de trabajo desde la tabla Trabajos (campo Tipo)
                        result.TipoTrabajo = (dt.Rows[0]["Tipo"] as string)?.Trim();
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
                                case "TablaMetadata":
                                    result.TablaMetadata = valor;
                                    break;
                                case "DocumentTypeIdDefault":
                                    result.DocumentTypeIdDefault = valor;
                                    break;
                                case "DocumentStatusIdDefault":
                                    result.DocumentStatusIdDefault = valor;
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
