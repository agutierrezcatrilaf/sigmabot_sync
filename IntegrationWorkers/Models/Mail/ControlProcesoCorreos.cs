using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntegrationWorkers.Models.Mail
{
    public class ControlProcesoCorreos
    {
        public string ProjId { get; set; }
        public string Mailbox { get; set; }   // inbox o sentbox
        public DateTime UltimaFechaProcesada { get; set; }
        public int RangoDias { get;set; }
        public int TotalDescargados { get; set; }
        public int TotalProcesados { get; set; }
    }
}
