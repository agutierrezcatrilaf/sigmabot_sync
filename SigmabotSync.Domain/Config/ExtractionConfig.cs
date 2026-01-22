using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>
        /// Convierte la configuración a Dictionary para compatibilidad con workers existentes
        /// </summary>
        public Dictionary<string, string> ToDictionary()
        {
            return new Dictionary<string, string>
            {
                { "ACXUser", ACXUser ?? "" },
                { "ACXPass", ACXPass ?? "" },
                { "IntegrationIdAconex", IntegrationIdAconex ?? "" },
                { "FieldIntegrationId", FieldIntegrationId ?? "" },
                { "NombrePrj", NombrePrj ?? "" },
                { "OrgId", OrgId ?? "" },
                { "userid", userid ?? "" }
            };
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
    }
}
