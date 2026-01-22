using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntegrationWorkers.Models.Workflow
{
    public class MemProyPaso
    {
        public string IdFlujodeTrabajo { get; set; }
        public string NumeroFlujodeTrabajo { get; set; }
        public string Nombre { get; set; }
        public string Estado { get; set; }
        public string Resultado { get; set; }
        public string Comentarios { get; set; }
        public DateTime FechaFinalizacion { get; set; }
        public DateTime FechaLimite { get; set; }
        public DateTime FechaInicio { get; set; }
        public int Duracion { get; set; }
        public DateTime FechaLimiteOriginal { get; set; }
        public int DiasAtraso { get; set; }
        public string RevisorOrganizacion { get; set; }
        public string RevisorNombre { get; set; }
        public string RevisorACXId { get; set; }
    }

}
