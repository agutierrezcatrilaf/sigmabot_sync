using System;

namespace SigmabotSync.Domain.Entities
{
    /// <summary>
    /// Registro de la tabla Trabajos. Contiene el estado y resultado de la última ejecución del trabajo.
    /// </summary>
    public class Trabajo
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Tipo { get; set; }
        /// <summary>Periodicidad del trabajo (nombre de columna en BD: Perioricidad).</summary>
        public string Perioricidad { get; set; }
        /// <summary>Fecha y hora de la última ejecución (datetime).</summary>
        public DateTime? FechaUltimaEjecucion { get; set; }
        /// <summary>Fecha y hora de la próxima ejecución programada (datetime).</summary>
        public DateTime? FechaProximaEjecucion { get; set; }
        /// <summary>Resultado de la última ejecución: "Exitoso", "Error", etc.</summary>
        public string ResultadoUltimaEjecucion { get; set; }
        public string ControldeEjecucion { get; set; }
        public string Estado { get; set; }
        /// <summary>Última corrección o mensaje de error de la ejecución.</summary>
        public string UltCorrEjecucion { get; set; }
    }
}
