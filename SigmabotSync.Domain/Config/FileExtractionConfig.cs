using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SigmabotSync.Domain.Config
{
    /// <summary>
    /// Configuración para el caso de uso de extracción de archivos
    /// </summary>
    public class FileExtractionConfig
    {
        /// <summary>
        /// ID del proyecto de Aconex
        /// </summary>
        public string ProjectId { get; set; }

        /// <summary>
        /// ID de la organización en Aconex
        /// </summary>
        public string OrgId { get; set; }

        /// <summary>
        /// ID del usuario en Aconex
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// Header de autorización (Basic base64)
        /// </summary>
        public string AuthorizationHeader { get; set; }

        /// <summary>
        /// Integration ID de Aconex (para X-Application-Key)
        /// </summary>
        public string IntegrationId { get; set; }

        /// <summary>
        /// Tamaño de página para paginado (default: 300)
        /// </summary>
        public int ResultSize { get; set; } = 300;

        /// <summary>
        /// Campos a retornar en la búsqueda
        /// </summary>
        public List<string> ReturnFields { get; set; }

        /// <summary>
        /// Ruta base donde se guardarán los archivos descargados
        /// </summary>
        public string BasePath { get; set; } = @"C:\Users\Usuario\AppData\Local\Temp\SigmaBotFileExtractionSalfa\";

        /// <summary>
        /// Constructor por defecto con campos estándar
        /// </summary>
        public FileExtractionConfig()
        {
            ReturnFields = new List<string>
            {
                "approved", "asBuiltRequired", "attribute1", "attribute2", "attribute3", "attribute4",
                "author", "authorisedBy", "category", "check1", "check2", "comments", "comments2",
                "confidential", "contractDeliverable", "contractnumber", "contractordocumentnumber",
                "contractorrev", "current", "date1", "date2", "discipline", "docno", "doctype", "filename",
                "fileSize", "fileType", "forreview", "markupLastModifiedDate", "milestonedate",
                "numberOfMarkups", "packagenumber", "percentComplete", "plannedsubmissiondate",
                "printSize", "projectField1", "projectField2", "projectField3", "received", "reference",
                "registered", "reviewed", "reviewSource", "reviewstatus", "revision", "revisiondate",
                "selectlist1", "selectlist2", "selectlist3", "selectlist4", "selectlist5", "selectlist6",
                "selectlist7", "selectlist8", "selectlist9", "selectlist10", "scale", "statusid",
                "tagNumber", "title", "toclient", "trackingid", "versionnumber", "vdrcode",
                "vendordocumentnumber", "vendorrev", "versionnumber"
            };
        }

        /// <summary>
        /// Crea la configuración desde AconexSettings y parámetros adicionales
        /// </summary>
        public static FileExtractionConfig FromAconexSettings(
            AconexSettings aconexSettings,
            string projectId,
            string orgId,
            string userId)
        {
            string authHeader = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{aconexSettings.UserAconex}:{aconexSettings.PassAconex}")
            );

            return new FileExtractionConfig
            {
                ProjectId = projectId,
                OrgId = orgId,
                UserId = userId,
                AuthorizationHeader = authHeader,
                IntegrationId = aconexSettings.IntegrationIdAconex
            };
        }

        /// <summary>
        /// Crea la configuración completamente desde AconexSettings (incluyendo ExtractionFiles)
        /// </summary>
        public static FileExtractionConfig FromAconexSettings(AconexSettings aconexSettings)
        {
            if (aconexSettings?.ExtractionFiles == null)
            {
                throw new ArgumentException("ExtractionFiles no está configurado en settings.json");
            }

            var extractionConfig = aconexSettings.ExtractionFiles;

            // Validar credenciales
            if (string.IsNullOrWhiteSpace(extractionConfig.UserAconex) ||
                string.IsNullOrWhiteSpace(extractionConfig.PassAconex) ||
                string.IsNullOrWhiteSpace(extractionConfig.IntegrationIdAconex))
            {
                throw new ArgumentException("UserAconex, PassAconex e IntegrationIdAconex son requeridos en ExtractionFiles");
            }

            // Validar parámetros del proyecto
            if (string.IsNullOrWhiteSpace(extractionConfig.ProjectId) ||
                string.IsNullOrWhiteSpace(extractionConfig.OrgId) ||
                string.IsNullOrWhiteSpace(extractionConfig.UserId))
            {
                throw new ArgumentException("ProjectId, OrgId y UserId son requeridos en ExtractionFiles");
            }

            string authHeader = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{extractionConfig.UserAconex}:{extractionConfig.PassAconex}")
            );

            return new FileExtractionConfig
            {
                ProjectId = extractionConfig.ProjectId,
                OrgId = extractionConfig.OrgId,
                UserId = extractionConfig.UserId,
                AuthorizationHeader = authHeader,
                IntegrationId = extractionConfig.IntegrationIdAconex,
                BasePath = string.IsNullOrWhiteSpace(extractionConfig.BasePath) 
                    ? @"C:\Users\Usuario\AppData\Local\Temp\SigmaBotFileExtractionSalfa\" 
                    : extractionConfig.BasePath
            };
        }
    }
}
