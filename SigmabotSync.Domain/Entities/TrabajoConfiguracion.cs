using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using SigmabotSync.Domain.Config;

namespace SigmabotSync.Domain.Entities
{
    /// <summary>
    /// Registro de la tabla TrabajosConfiguracion. Contiene IdProyecto y los mapeos de campos (CamposConsulta, CamposResponse, CamposBD)
    /// que reemplazan a DocumentFieldMappings del settings.
    /// </summary>
    public class TrabajoConfiguracion
    {
        /// <summary>Identificador del trabajo (por defecto 1).</summary>
        public int IdTrabajo { get; set; }

        /// <summary>Nombre del proyecto para logs (desde TrabajosConfiguracion Nombre=Proyecto, ej. SalfaDemo).</summary>
        public string Proyecto { get; set; }

        /// <summary>ID del proyecto Aconex (reemplaza ProjectId del settings).</summary>
        public string IdProyecto { get; set; }

        /// <summary>Nombres de campos para la consulta API (returnFields). Comma-separated o JSON array. Orden = CamposResponse y CamposBD.</summary>
        public string CamposConsulta { get; set; }

        /// <summary>Nombres de propiedades en el JSON de respuesta. Comma-separated o JSON array. Orden = CamposConsulta y CamposBD.</summary>
        public string CamposResponse { get; set; }

        /// <summary>Nombres de columnas en BD. Comma-separated o JSON array. Orden = CamposConsulta y CamposResponse.</summary>
        public string CamposBD { get; set; }

        /// <summary>Ruta base para extracción de archivos (desde TrabajosConfiguracion Nombre=BasePath).</summary>
        public string BasePath { get; set; }

        /// <summary>Id de la credencial Aconex en tabla Credenciales (desde TrabajosConfiguracion Nombre=CredencialAconex).</summary>
        public int? CredencialAconexId { get; set; }

        /// <summary>Id de la credencial BD en tabla Credenciales (desde TrabajosConfiguracion Nombre=CredencialBD).</summary>
        public int? CredencialBDId { get; set; }

        /// <summary>Tipo de trabajo: FileExtraction, ProjectSync, FullExtraction. Viene del campo Tipo de la tabla Trabajos.</summary>
        public string TipoTrabajo { get; set; }

        /// <summary>
        /// Construye la lista de DocumentFieldMapping a partir de CamposConsulta, CamposResponse y CamposBD.
        /// Acepta listas separadas por comas o JSON arrays; mismo número de elementos (por índice: ApiField, JsonProperty, DbColumn).
        /// </summary>
        public List<DocumentFieldMapping> ToDocumentFieldMappings()
        {
            var consulta = ParseStringArray(CamposConsulta);
            var response = ParseStringArray(CamposResponse);
            var bd = ParseStringArray(CamposBD);
            if (consulta == null || consulta.Count == 0)
                return null;
            int n = consulta.Count;
            var list = new List<DocumentFieldMapping>(n);
            for (int i = 0; i < n; i++)
            {
                list.Add(new DocumentFieldMapping
                {
                    ApiField = consulta[i],
                    JsonProperty = (response != null && i < response.Count) ? response[i] : consulta[i],
                    DbColumn = (bd != null && i < bd.Count) ? bd[i] : consulta[i]
                });
            }
            return list;
        }

        /// <summary>
        /// Devuelve la lista de nombres de campos para la consulta API (returnFields), desde CamposConsulta.
        /// </summary>
        public List<string> ToReturnFields()
        {
            var list = ParseStringArray(CamposConsulta);
            return list ?? new List<string>();
        }

        private static List<string> ParseStringArray(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;
            json = json.Trim();
            try
            {
                var arr = JsonConvert.DeserializeObject<List<string>>(json);
                if (arr != null)
                    return arr.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                // Intentar como array de tokens separados por comas
                if (json.StartsWith("[") && json.EndsWith("]"))
                    return null;
                return json.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            }
            catch
            {
                return json.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            }
        }
    }
}
