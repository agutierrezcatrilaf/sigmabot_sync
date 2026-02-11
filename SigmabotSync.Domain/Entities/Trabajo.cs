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
        public DateTime? FechaUltimaEjecucion { get; set; }
        /// <summary>Hora de la última ejecución (ej. "14:30:00").</summary>
        public string HoraUltimaEjecucion { get; set; }
        public DateTime? FechaProximaEjecucion { get; set; }
        public string HoraProximaEjecucion { get; set; }
        /// <summary>Resultado de la última ejecución: "Exitoso", "Error", etc.</summary>
        public string ResultadoUltimaEjecucion { get; set; }
        public string ControldeEjecucion { get; set; }
        public string Estado { get; set; }
        /// <summary>Última corrección o mensaje de error de la ejecución.</summary>
        public string UltCorrEjecucion { get; set; }
    }
}
