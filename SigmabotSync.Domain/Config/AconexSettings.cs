using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SigmabotSync.Domain.Config
{
    public class AconexSettings
    {
        public string UserAconex { get; set; }
        public string PassAconex { get; set; }
        public string IntegrationIdAconex { get; set; }
        public ExtractionFilesConfig ExtractionFiles { get; set; }
    }

    public class ExtractionFilesConfig
    {
        public string UserAconex { get; set; }
        public string PassAconex { get; set; }
        public string IntegrationIdAconex { get; set; }
        public string ProjectId { get; set; }
        public string OrgId { get; set; }
        public string UserId { get; set; }
        public string BasePath { get; set; }
    }
}
