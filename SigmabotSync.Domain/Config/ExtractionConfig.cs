using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.Text;
using System.Threading.Tasks;

namespace SigmabotSync.Domain.Config
{
    /// <summary>
    /// Configuración temporal para los workers de extracción.
    /// TODO: Migrar a base de datos en el futuro
    /// </summary>
    public class ExtractionConfig
    {
        // Credenciales Aconex
        public string ACXUser { get; set; }
        public string ACXPass { get; set; }
        public string IntegrationIdAconex { get; set; }
        public string FieldIntegrationId { get; set; }

        // Información del proyecto
        public string NombrePrj { get; set; }
        public string OrgId { get; set; }
        public string userid { get; set; }

        // Cadena de conexión a base de datos
        public string ConnectionString { get; set; }

        /// <summary>Mapeo de campos documento (ApiField, JsonProperty, DbColumn). Si es null/empty el worker usa valores por defecto.</summary>
        public List<DocumentFieldMapping> DocumentFieldMappings { get; set; }

        /// <summary>
        /// Convierte la configuración a Dictionary para compatibilidad con workers existentes
        /// </summary>
        public Dictionary<string, string> ToDictionary()
        {
            var d = new Dictionary<string, string>
            {
                { "ACXUser", ACXUser ?? "" },
                { "ACXPass", ACXPass ?? "" },
                { "IntegrationIdAconex", IntegrationIdAconex ?? "" },
                { "FieldIntegrationId", FieldIntegrationId ?? "" },
                { "NombrePrj", NombrePrj ?? "" },
                { "OrgId", OrgId ?? "" },
                { "userid", userid ?? "" }
            };
            if (DocumentFieldMappings != null && DocumentFieldMappings.Count > 0)
                d["DocumentFieldMappings"] = JsonConvert.SerializeObject(DocumentFieldMappings);
            return d;
        }

        /// <summary>
        /// Crea una configuración desde AconexSettings (temporal)
        /// </summary>
        public static ExtractionConfig FromAconexSettings(AconexSettings settings, string connectionString, string projectName = "")
        {
            return new ExtractionConfig
            {
                ACXUser = settings.UserAconex,
                ACXPass = settings.PassAconex,
                IntegrationIdAconex = settings.IntegrationIdAconex,
                FieldIntegrationId = settings.IntegrationIdAconex, // Por defecto mismo que IntegrationId
                NombrePrj = projectName,
                OrgId = "", // TODO: Obtener de BD
                userid = "", // TODO: Obtener de BD
                ConnectionString = connectionString
            };
        }

        /// <summary>
        /// Crea una configuración desde ExtractionFilesConfig (settings.json) para DocumentSyncWorker.
        /// </summary>
        public static ExtractionConfig FromExtractionFilesConfig(ExtractionFilesConfig extractionFiles)
        {
            if (extractionFiles == null)
                throw new ArgumentNullException(nameof(extractionFiles));

            return new ExtractionConfig
            {
                ACXUser = extractionFiles.UserAconex,
                ACXPass = extractionFiles.PassAconex,
                IntegrationIdAconex = extractionFiles.IntegrationIdAconex,
                FieldIntegrationId = extractionFiles.IntegrationIdAconex,
                NombrePrj = extractionFiles.ProjectName ?? "Proyecto",
                OrgId = extractionFiles.OrgId ?? "",
                userid = extractionFiles.UserId ?? "",
                ConnectionString = extractionFiles.GetConnectionString(),
                DocumentFieldMappings = extractionFiles.DocumentFieldMappings
            };
        }
    }
}
