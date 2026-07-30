using System;
using System.Collections.Generic;
using SigmabotSync.Domain.Models.Synchronization;

namespace SigmabotSync.Domain.Configuration
{
    /// <summary>
    /// Ida Codelco → SALFA: {CodProyecto}-EXT-{CwaCodigo}-{DisciplinaCodigo}-{TipoDocCodigo}-{Correlativo}.
    /// Segmentos CWA, disciplina y tipo documento usan CodigoDestino de equivalencias.
    /// </summary>
    public static class ProjectSyncSalfaDocumentNumberBuilder
    {
        public const string TipoSegmentExterno = "EXT";

        public static string Build(
            string codigoProyectoSalfa,
            AconexDocumentCatalog documentCatalog,
            string codelcoDocumentNo,
            IReadOnlyDictionary<string, string> sourceHints,
            out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(codigoProyectoSalfa))
            {
                error = "Falta parámetro CodigoProyectoSalfa en configuración del trabajo.";
                return null;
            }

            if (string.IsNullOrWhiteSpace(codelcoDocumentNo))
            {
                error = "DocumentNumber Codelco vacío.";
                return null;
            }

            if (!TryParseCodelcoDocumentNumber(codelcoDocumentNo, out string wbsSegment, out string tipoEspSegment, out string correlativo))
            {
                error = $"DocumentNumber Codelco no tiene formato esperado (4 segmentos): {codelcoDocumentNo}";
                return null;
            }

            string localizador = GetHint(sourceHints, "Localizador_singleSelect");
            string especialidad = GetHint(sourceHints, "Especialidad_singleSelect")
                ?? GetHint(sourceHints, "discipline");
            string tipoDeDocumento = GetHint(sourceHints, "TipoDeDocumento_singleSelect");

            string cwaSegment = documentCatalog?.ResolveEquivalenciaCwaCodigoDocno(localizador);
            if (string.IsNullOrWhiteSpace(cwaSegment) && !string.IsNullOrWhiteSpace(wbsSegment))
                cwaSegment = documentCatalog?.ResolveEquivalenciaCwaCodigoDocnoByWbsCode(wbsSegment);

            string disciplineSegment = documentCatalog?.ResolveEquivalenciaDisciplineCodigoDocno(especialidad);
            if (string.IsNullOrWhiteSpace(disciplineSegment) && string.IsNullOrWhiteSpace(especialidad))
                disciplineSegment = documentCatalog?.ResolveEquivalenciaDisciplineCodigoDocno("MD - MULTIDISCIPLINA");

            string tipoDocSource = !string.IsNullOrWhiteSpace(tipoDeDocumento)
                ? tipoDeDocumento
                : tipoEspSegment;
            string tipoDocCodigo = documentCatalog?.ResolveEquivalenciaTipoDocumentoCodigoDocno(tipoDocSource);

            if (string.IsNullOrWhiteSpace(cwaSegment))
            {
                error = $"Sin CodigoDestino CWA para Localizador='{localizador ?? ""}' / WBS='{wbsSegment ?? ""}'.";
                return null;
            }
            if (string.IsNullOrWhiteSpace(disciplineSegment))
            {
                error = $"Sin CodigoDestino Discipline para Especialidad='{especialidad ?? ""}'.";
                return null;
            }
            if (string.IsNullOrWhiteSpace(tipoDocCodigo))
            {
                error = $"Sin CodigoDestino TipoDocumento para origen='{tipoDocSource ?? ""}'.";
                return null;
            }

            return string.Join("-", new[]
            {
                codigoProyectoSalfa.Trim(),
                TipoSegmentExterno,
                cwaSegment.Trim(),
                disciplineSegment.Trim(),
                tipoDocCodigo.Trim(),
                correlativo.Trim()
            });
        }

        public static bool TryParseCodelcoDocumentNumber(
            string documentNumber,
            out string wbsSegment,
            out string tipoEspSegment,
            out string correlativo)
        {
            wbsSegment = null;
            tipoEspSegment = null;
            correlativo = null;
            if (string.IsNullOrWhiteSpace(documentNumber))
                return false;

            string[] parts = documentNumber.Trim().Split('-');
            if (parts.Length < 4)
                return false;

            wbsSegment = parts[1].Trim();
            tipoEspSegment = parts[2].Trim();
            correlativo = parts[parts.Length - 1].Trim();
            return !string.IsNullOrEmpty(wbsSegment)
                && !string.IsNullOrEmpty(tipoEspSegment)
                && !string.IsNullOrEmpty(correlativo);
        }

        private static string GetHint(IReadOnlyDictionary<string, string> hints, string key)
        {
            if (hints == null || string.IsNullOrWhiteSpace(key))
                return null;
            return hints.TryGetValue(key.Trim(), out string v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
        }
    }
}
