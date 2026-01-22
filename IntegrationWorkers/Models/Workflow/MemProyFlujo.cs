using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntegrationWorkers.Models.Workflow
{
    public class MemProyFlujo
    {
        public string Numero { get; set; }
        public string Nombre { get; set; }
        public string Estado { get; set; }
        public string Plantilla { get; set; }
        public string NombreEmisor { get; set; }
        public string ACXIdEmisor { get; set; }
        public string OrganizacionEmisora { get; set; }
        public string MotivodeEmision { get; set; }
    }

}
