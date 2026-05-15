using System;
using System.Collections.Generic;
using System.Linq;

namespace SigmabotSync.Domain.Configuration
{
    /// <summary>
    /// Catálogo de campos de <c>TrabajosConfiguracion</c> por tipo de trabajo.
    /// </summary>
    public static class TrabajoTipoConfigFieldCatalog
    {
        private static readonly IReadOnlyList<TrabajoConfiguracionCampoDefinicion> Campos = new[]
        {
            new TrabajoConfiguracionCampoDefinicion(
                TrabajosConfiguracionKeyNames.IdProyecto,
                "Id proyecto Aconex",
                tiposDondeVisible: new[] { TipoTrabajoIds.FileExtraction, TipoTrabajoIds.FullExtraction, TipoTrabajoIds.FileUploadWithMetadata },
                tiposDondeObligatorio: new[] { TipoTrabajoIds.FileExtraction, TipoTrabajoIds.FullExtraction, TipoTrabajoIds.FileUploadWithMetadata },
                ayuda: "ProjectId en Aconex (numérico)."),

            new TrabajoConfiguracionCampoDefinicion(
                TrabajosConfiguracionKeyNames.CredencialAconex,
                "Id credencial Aconex",
                tiposDondeVisible: new[] { TipoTrabajoIds.FileExtraction, TipoTrabajoIds.FullExtraction, TipoTrabajoIds.FileUploadWithMetadata },
                tiposDondeObligatorio: new[] { TipoTrabajoIds.FileExtraction, TipoTrabajoIds.FullExtraction, TipoTrabajoIds.FileUploadWithMetadata },
                ayuda: "Id en tabla Credenciales con Tipo = Aconex."),

            new TrabajoConfiguracionCampoDefinicion(
                TrabajosConfiguracionKeyNames.CredencialBD,
                "Id credencial BD",
                tiposDondeVisible: new[] { TipoTrabajoIds.FileExtraction, TipoTrabajoIds.FullExtraction, TipoTrabajoIds.FileUploadWithMetadata },
                tiposDondeObligatorio: new[] { TipoTrabajoIds.FileExtraction, TipoTrabajoIds.FullExtraction, TipoTrabajoIds.FileUploadWithMetadata },
                ayuda: "Id en tabla Credenciales con Tipo = BD."),

            new TrabajoConfiguracionCampoDefinicion(
                TrabajosConfiguracionKeyNames.CamposConsulta,
                "Campos consulta API (CSV)",
                tiposDondeVisible: new[] { TipoTrabajoIds.FileExtraction, TipoTrabajoIds.FullExtraction },
                tiposDondeObligatorio: new[] { TipoTrabajoIds.FileExtraction, TipoTrabajoIds.FullExtraction },
                ayuda: "Lista separada por comas; mismo orden que CamposResponse y CamposBD."),

            new TrabajoConfiguracionCampoDefinicion(
                TrabajosConfiguracionKeyNames.CamposResponse,
                "Campos respuesta JSON (CSV)",
                tiposDondeVisible: new[] { TipoTrabajoIds.FileExtraction, TipoTrabajoIds.FullExtraction },
                tiposDondeObligatorio: new[] { TipoTrabajoIds.FileExtraction, TipoTrabajoIds.FullExtraction },
                ayuda: "Propiedades JSON alineadas por índice con CamposConsulta."),

            new TrabajoConfiguracionCampoDefinicion(
                TrabajosConfiguracionKeyNames.CamposBD,
                "Campos columnas BD (CSV)",
                tiposDondeVisible: new[] { TipoTrabajoIds.FileExtraction, TipoTrabajoIds.FullExtraction },
                tiposDondeObligatorio: new[] { TipoTrabajoIds.FileExtraction, TipoTrabajoIds.FullExtraction },
                ayuda: "Columnas en BD alineadas por índice con CamposConsulta."),

            new TrabajoConfiguracionCampoDefinicion(
                TrabajosConfiguracionKeyNames.Proyecto,
                "Nombre proyecto (logs)",
                tiposDondeVisible: new[] { TipoTrabajoIds.FileExtraction, TipoTrabajoIds.FullExtraction, TipoTrabajoIds.FileUploadWithMetadata },
                tiposDondeObligatorio: Array.Empty<string>(),
                ayuda: "Opcional; mejora logs y carpetas."),

            new TrabajoConfiguracionCampoDefinicion(
                TrabajosConfiguracionKeyNames.BasePath,
                "Ruta base archivos",
                tiposDondeVisible: new[] { TipoTrabajoIds.FileExtraction, TipoTrabajoIds.FileUploadWithMetadata },
                tiposDondeObligatorio: new[] { TipoTrabajoIds.FileExtraction, TipoTrabajoIds.FileUploadWithMetadata },
                ayuda: "UNC o carpeta local. FileExtraction descarga aquí; FileUploadWithMetadata busca archivos por NombreArchivo en metadata."),

            new TrabajoConfiguracionCampoDefinicion(
                TrabajosConfiguracionKeyNames.TablaMetadata,
                "Tabla metadata (BD)",
                tiposDondeVisible: new[] { TipoTrabajoIds.FileUploadWithMetadata },
                tiposDondeObligatorio: new[] { TipoTrabajoIds.FileUploadWithMetadata },
                ayuda: "Nombre de tabla con columna NombreArchivo (y filas a subir a Aconex). Solo FileUploadWithMetadata."),
        };

        public static IReadOnlyList<TrabajoConfiguracionCampoDefinicion> ObtenerTodos()
        {
            return Campos;
        }

        public static IEnumerable<TrabajoConfiguracionCampoDefinicion> ObtenerObligatoriosPara(string tipoTrabajo)
        {
            foreach (var c in Campos)
            {
                if (c.EsObligatorioPara(tipoTrabajo))
                    yield return c;
            }
        }

        /// <summary>Campos que debe mostrar el formulario guiado para el tipo de trabajo indicado.</summary>
        public static IEnumerable<TrabajoConfiguracionCampoDefinicion> ObtenerCamposParaFormulario(string tipoTrabajo)
        {
            foreach (var c in Campos)
            {
                if (c.EsVisiblePara(tipoTrabajo))
                    yield return c;
            }
        }

        /// <summary>Indica si existe al menos un campo de catálogo para ese tipo (p. ej. ProjectSync no tiene plantilla).</summary>
        public static bool TipoSoportaFormularioGuiado(string tipoTrabajo)
        {
            return ObtenerCamposParaFormulario(tipoTrabajo ?? string.Empty).Any();
        }
    }
}
