using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml;
using SigmabotSync.Application.FileExtraction;
using SigmabotSync.Domain.Entities;
using SigmabotSync.Domain.Models.Synchronization;

namespace SigmabotSync.Application.Synchronization
{
    /// <summary>Construye XML de Register/Supersede según el schema del proyecto destino.</summary>
    public static class TransmittalRegisterXmlBuilder
    {
        private const string DefaultDocumentTypeName = "Documento Interno";
        private const string DefaultAuthorName = "SigmabotSync";

        public static string Build(
            AconexRegisterSchemaSnapshot schema,
            TransmittalDocumentAttachment attachment,
            string revision,
            bool hasFile,
            IReadOnlyDictionary<string, string> sourceValuesByIdentifier,
            out string error)
        {
            error = null;
            if (schema?.Fields == null || schema.Fields.Count == 0)
            {
                error = "Schema de registro vacío para el proyecto destino.";
                return null;
            }

            string documentNo = attachment?.DocumentNo?.Trim() ?? "";
            string title = string.IsNullOrWhiteSpace(attachment?.Title) ? documentNo : attachment.Title.Trim();
            var picklists = schema.PicklistsByIdentifier ?? EmptyPicklists();

            string docTypeId = ResolvePicklistId(
                picklists, "DocumentTypeId",
                GetHint(sourceValuesByIdentifier, "DocumentTypeId", "doctype", "DocumentType"),
                DefaultDocumentTypeName);

            string docStatusId = ResolvePicklistId(
                picklists, "DocumentStatusId",
                GetHint(sourceValuesByIdentifier, "DocumentStatusId", "statusid", "DocumentStatus", "Status"),
                attachment?.Status);

            if (string.IsNullOrWhiteSpace(docTypeId))
            {
                error = "No se pudo resolver DocumentTypeId obligatorio (revisar schema destino o metadata origen).";
                return null;
            }

            var sb = new StringBuilder();
            sb.Append("<Document>");

            foreach (AconexRegisterSchemaField field in schema.Fields)
            {
                if (field == null || string.IsNullOrWhiteSpace(field.Identifier))
                    continue;

                string id = field.Identifier.Trim();
                if (string.Equals(id, "id", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!ShouldEmitIdentifier(id))
                    continue;
                if (ShouldSkipStandardInFavorOfProjectSingleSelect(id, schema))
                    continue;

                bool isMandatory = string.Equals(field.MandatoryStatus, "MANDATORY", StringComparison.OrdinalIgnoreCase);
                string value = ResolveFieldValue(
                    id, field.DataType, documentNo, title, revision, hasFile,
                    docTypeId, docStatusId, sourceValuesByIdentifier, picklists);

                if (string.IsNullOrEmpty(value))
                {
                    if (isMandatory)
                    {
                        if (IsDateField(id, field.DataType))
                            value = FormatAconexUtcDate(DateTime.UtcNow);
                        else
                            value = ResolveMandatoryFallback(id, picklists);
                        if (string.IsNullOrEmpty(value))
                        {
                            error = $"Campo obligatorio sin valor en schema destino: {id}.";
                            return null;
                        }
                    }
                    else
                    {
                        continue;
                    }
                }

                sb.Append("<").Append(id).Append(">").Append(EscapeXml(value)).Append("</").Append(id).Append(">");
            }

            sb.Append("</Document>");
            return sb.ToString();
        }

        /// <summary>
        /// Arma el XML según filas de homologación. <paramref name="targetSchema"/> es opcional (solo picklists si existe).
        /// </summary>
        public static string BuildFromFieldMappings(
            IReadOnlyList<TransmittalSyncCampoMapeoItem> mappings,
            AconexRegisterSchemaSnapshot targetSchema,
            AconexDocumentCatalog documentCatalog,
            TransmittalDocumentAttachment attachment,
            string revision,
            bool hasFile,
            IReadOnlyDictionary<string, string> sourceHints,
            out string error)
        {
            error = null;
            if (mappings == null || mappings.Count == 0)
            {
                error = "No hay filas en TransmittalSyncCampoMapeo para este IdTrabajo.";
                return null;
            }

            var picklists = targetSchema?.PicklistsByIdentifier ?? EmptyPicklists();
            var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sb = new StringBuilder();
            sb.Append("<Document>");

            foreach (TransmittalSyncCampoMapeoItem map in mappings)
            {
                if (map == null || string.IsNullOrWhiteSpace(map.CampoDestino))
                    continue;

                string destino = map.CampoDestino.Trim();
                if (emitted.Contains(destino))
                    continue;

                string value = ResolveOrigenValue(map.CampoOrigen, attachment, revision, sourceHints);
                if (string.IsNullOrWhiteSpace(value))
                    value = ApplyValorDefault(map.ValorDefault, hasFile, attachment, revision);

                value = ResolveCustomFieldValueForRegister(destino, value, map.EsObligatorio, picklists);

                if (string.IsNullOrWhiteSpace(value))
                {
                    if (map.EsObligatorio)
                    {
                        error = $"Campo obligatorio destino '{destino}' sin valor (origen '{map.CampoOrigen}').";
                        return null;
                    }
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(map.Catalogo))
                {
                    string resolved = documentCatalog?.ResolveByCatalog(map.Catalogo, value);
                    if (!string.IsNullOrWhiteSpace(resolved))
                    {
                        value = resolved;
                    }
                    else if (IsEquivalenciaCatalog(map.Catalogo) && !string.IsNullOrWhiteSpace(map.ValorDefault))
                    {
                        // Origen sin fila de equivalencia → usar default destino (ej. General / IAD).
                        string fallback = ApplyValorDefault(map.ValorDefault, hasFile, attachment, revision);
                        if (!string.IsNullOrWhiteSpace(fallback))
                            value = fallback;
                    }
                }

                sb.Append("<").Append(destino).Append(">").Append(EscapeXml(value)).Append("</").Append(destino).Append(">");
                emitted.Add(destino);
            }

            if (!emitted.Contains("HasFile"))
            {
                sb.Append("<HasFile>").Append(hasFile ? "true" : "false").Append("</HasFile>");
                emitted.Add("HasFile");
            }

            AppendMandatorySchemaFieldsNotMapped(targetSchema, emitted, sb);

            sb.Append("</Document>");
            return sb.ToString();
        }

        /// <summary>
        /// Campos MANDATORY del schema destino que no están en homologación.
        /// Custom fields (*_singleSelect, etc.) → TBD; fechas → UTC now; picklists → primera opción.
        /// </summary>
        private static void AppendMandatorySchemaFieldsNotMapped(
            AconexRegisterSchemaSnapshot targetSchema,
            HashSet<string> emitted,
            StringBuilder sb)
        {
            if (targetSchema?.Fields == null)
                return;

            var picklists = targetSchema.PicklistsByIdentifier ?? EmptyPicklists();

            foreach (AconexRegisterSchemaField field in targetSchema.Fields)
            {
                if (field == null || string.IsNullOrWhiteSpace(field.Identifier))
                    continue;

                string destino = field.Identifier.Trim();
                if (emitted.Contains(destino))
                    continue;
                if (string.Equals(destino, "id", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!ShouldEmitIdentifier(destino))
                    continue;
                if (!string.Equals(field.MandatoryStatus, "MANDATORY", StringComparison.OrdinalIgnoreCase))
                    continue;

                string value = null;
                if (IsPlaceholderCustomField(destino))
                    value = ResolveCustomFieldValueForRegister(destino, "TBD", esObligatorio: true, picklists);
                else if (IsDateField(destino, field.DataType))
                    value = FormatAconexUtcDate(DateTime.UtcNow);
                else
                    value = ResolveMandatoryFallback(destino, picklists);

                if (string.IsNullOrWhiteSpace(value))
                    continue;

                sb.Append("<").Append(destino).Append(">").Append(EscapeXml(value)).Append("</").Append(destino).Append(">");
                emitted.Add(destino);
            }
        }

        private static bool IsPlaceholderCustomField(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return false;
            return identifier.EndsWith("_singleSelect", StringComparison.OrdinalIgnoreCase)
                || identifier.EndsWith("_multiLineText", StringComparison.OrdinalIgnoreCase)
                || identifier.EndsWith("_singleLineText", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsEquivalenciaCatalog(string catalogo)
        {
            if (string.IsNullOrWhiteSpace(catalogo))
                return false;
            return string.Equals(catalogo, AconexDocumentCatalogNames.EquivalenciaDiscipline, StringComparison.OrdinalIgnoreCase)
                || string.Equals(catalogo, AconexDocumentCatalogNames.EquivalenciaTipoDocumento, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Project fields: se envían como texto de picklist (incl. "TBD" si es opción válida en SALFA).
        /// Si el schema trae picklist con IDs y el valor matchea, preferimos Id; si no, el texto tal cual.
        /// </summary>
        private static string ResolveCustomFieldValueForRegister(
            string destino,
            string value,
            bool esObligatorio,
            IReadOnlyDictionary<string, IReadOnlyList<AconexSchemaValueOption>> picklists)
        {
            if (!IsPlaceholderCustomField(destino))
                return value;

            if (string.IsNullOrWhiteSpace(value))
                return null;

            string trimmed = value.Trim();

            if (picklists != null &&
                picklists.TryGetValue(destino, out var options) &&
                options != null &&
                options.Count > 0 &&
                TryResolvePicklistId(picklists, destino, trimmed, out string picklistId) &&
                !string.IsNullOrWhiteSpace(picklistId))
            {
                return picklistId;
            }

            // TBD / valores de texto son válidos en picklists SALFA configurados en Project Fields.
            return trimmed;
        }

        private static string ResolveOrigenValue(
            string campoOrigen,
            TransmittalDocumentAttachment attachment,
            string revision,
            IReadOnlyDictionary<string, string> sourceHints)
        {
            if (!string.IsNullOrWhiteSpace(campoOrigen))
            {
                string fromHints = GetHint(sourceHints, GetOrigenAliases(campoOrigen));
                if (!string.IsNullOrWhiteSpace(fromHints))
                    return fromHints;
            }

            if (attachment == null || string.IsNullOrWhiteSpace(campoOrigen))
                return null;

            switch (campoOrigen.Trim())
            {
                case "DocumentNumber":
                    return attachment.DocumentNo?.Trim();
                case "Title":
                    return string.IsNullOrWhiteSpace(attachment.Title) ? attachment.DocumentNo?.Trim() : attachment.Title.Trim();
                case "Revision":
                    return string.IsNullOrWhiteSpace(attachment.Revision) ? revision : attachment.Revision.Trim();
                case "RevisionDate":
                    return attachment.RevisionDate?.Trim();
                case "Status":
                case "statusid":
                    return attachment.Status?.Trim();
                case "author":
                case "Author":
                    return GetHint(sourceHints, "author", "Author");
                default:
                    return GetHint(sourceHints, campoOrigen.Trim());
            }
        }

        private static string[] GetOrigenAliases(string campoOrigen)
        {
            if (string.IsNullOrWhiteSpace(campoOrigen))
                return Array.Empty<string>();

            switch (campoOrigen.Trim())
            {
                case "doctype":
                    return new[] { "doctype", "DocumentType", "DocumentTypeId" };
                case "revisiondate":
                    return new[] { "revisiondate", "RevisionDate", "revisionDate" };
                case "Status":
                case "statusid":
                    return new[] { "statusid", "Status", "DocumentStatus", "DocumentStatusId" };
                case "author":
                    return new[] { "author", "Author" };
                case "Especialidad_singleSelect":
                    return new[] { "Especialidad_singleSelect", "Discipline_singleSelect", "discipline" };
                case "TipoDeDocumento_singleSelect":
                    return new[] { "TipoDeDocumento_singleSelect" };
                default:
                    return new[] { campoOrigen.Trim() };
            }
        }

        private static string ApplyValorDefault(
            string valorDefault,
            bool hasFile,
            TransmittalDocumentAttachment attachment,
            string revision)
        {
            if (string.IsNullOrWhiteSpace(valorDefault))
                return null;

            string token = valorDefault.Trim();
            if (string.Equals(token, "@UtcNow", StringComparison.OrdinalIgnoreCase))
                return FormatAconexUtcDate(DateTime.UtcNow);
            if (string.Equals(token, "@HasFileTrue", StringComparison.OrdinalIgnoreCase))
                return "true";
            if (string.Equals(token, "@HasFileFalse", StringComparison.OrdinalIgnoreCase))
                return "false";
            if (string.Equals(token, "@Revision", StringComparison.OrdinalIgnoreCase))
                return string.IsNullOrWhiteSpace(revision) ? "A" : revision.Trim();
            if (string.Equals(token, "@DocumentNo", StringComparison.OrdinalIgnoreCase))
                return attachment?.DocumentNo?.Trim();

            return token;
        }

        /// <summary>Lista identificadores MANDATORY del schema destino (para logs de prueba).</summary>
        public static IReadOnlyList<string> ListMandatoryFieldIdentifiers(AconexRegisterSchemaSnapshot schema)
        {
            var list = new List<string>();
            if (schema?.Fields == null)
                return list;

            foreach (AconexRegisterSchemaField field in schema.Fields)
            {
                if (field == null || string.IsNullOrWhiteSpace(field.Identifier))
                    continue;
                if (!string.Equals(field.MandatoryStatus, "MANDATORY", StringComparison.OrdinalIgnoreCase))
                    continue;
                list.Add(field.Identifier.Trim());
            }

            return list;
        }

        /// <summary>Convierte el XML &lt;Document&gt; en líneas legibles campo=valor.</summary>
        public static string FormatXmlFieldLines(string xmlDocument)
        {
            if (string.IsNullOrWhiteSpace(xmlDocument))
                return "(vacío)";

            var lines = new List<string>();
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(xmlDocument);
                XmlNode root = doc.SelectSingleNode("Document") ?? doc.DocumentElement;
                if (root?.ChildNodes == null)
                    return xmlDocument;

                foreach (XmlNode child in root.ChildNodes)
                {
                    if (child.NodeType != XmlNodeType.Element)
                        continue;
                    lines.Add($"  {child.Name}={child.InnerText}");
                }
            }
            catch
            {
                return xmlDocument;
            }

            return lines.Count == 0 ? xmlDocument : string.Join(Environment.NewLine, lines);
        }

        /// <summary>Campos obligatorios del destino que no aparecen en los hints del origen (mismo nombre).</summary>
        public static IReadOnlyList<string> ListMandatoryFieldsMissingInSource(
            AconexRegisterSchemaSnapshot schema,
            IReadOnlyDictionary<string, string> sourceHints)
        {
            var missing = new List<string>();
            foreach (string id in ListMandatoryFieldIdentifiers(schema))
            {
                if (sourceHints != null && sourceHints.TryGetValue(id, out string v) && !string.IsNullOrWhiteSpace(v))
                    continue;
                missing.Add(id);
            }

            return missing;
        }

        private static string ResolveFieldValue(
            string identifier,
            string dataType,
            string documentNo,
            string title,
            string revision,
            bool hasFile,
            string docTypeId,
            string docStatusId,
            IReadOnlyDictionary<string, string> sourceValues,
            IReadOnlyDictionary<string, IReadOnlyList<AconexSchemaValueOption>> picklists)
        {
            switch (identifier)
            {
                case "DocumentNumber":
                    return documentNo;
                case "Title":
                    return title;
                case "Revision":
                    return revision;
                case "DocumentTypeId":
                    return docTypeId;
                case "DocumentStatusId":
                    return docStatusId;
                case "Author":
                    return GetHint(sourceValues, "Author", "author") ?? DefaultAuthorName;
                case "HasFile":
                    return hasFile ? "true" : "false";
                case "RevisionDate":
                    return FormatDateForAconex(GetHint(sourceValues, "RevisionDate", "revisiondate", "revisionDate"));
                default:
                    break;
            }

            if (IsDateField(identifier, dataType))
            {
                string raw = GetHint(sourceValues, identifier);
                return FormatDateForAconex(raw);
            }

            if (IsProjectFieldSingleSelect(identifier))
            {
                string fromSource = GetHint(sourceValues, identifier);
                return ResolvePicklistId(picklists, identifier, fromSource, null);
            }

            if (picklists != null && picklists.ContainsKey(identifier))
            {
                string fromSource = GetHint(sourceValues, identifier);
                return ResolvePicklistId(picklists, identifier, fromSource, null);
            }

            return GetHint(sourceValues, identifier);
        }

        private static bool IsDateField(string identifier, string dataType)
        {
            if (string.Equals(dataType, "DATE", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.IsNullOrWhiteSpace(identifier))
                return false;
            switch (identifier)
            {
                case "RevisionDate":
                case "DateCreated":
                case "DateApproved":
                case "DateForReview":
                case "DateReviewed":
                case "ToClientDate":
                case "PlannedSubmissionDate":
                case "MilestoneDate":
                case "Date1":
                case "Date2":
                    return true;
                default:
                    return false;
            }
        }

        private static string FormatDateForAconex(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind | DateTimeStyles.AllowWhiteSpaces, out DateTime dt))
                return FormatAconexUtcDate(dt.ToUniversalTime());
            if (DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out dt))
                return FormatAconexUtcDate(dt.ToUniversalTime());
            return raw.Trim();
        }

        private static string FormatAconexUtcDate(DateTime utc)
        {
            return utc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        }

        private static string ResolveMandatoryFallback(
            string identifier,
            IReadOnlyDictionary<string, IReadOnlyList<AconexSchemaValueOption>> picklists)
        {
            if (picklists != null &&
                picklists.TryGetValue(identifier, out var options) &&
                options != null &&
                options.Count > 0)
            {
                foreach (AconexSchemaValueOption option in options)
                {
                    if (option != null && !string.IsNullOrWhiteSpace(option.Id))
                        return option.Id.Trim();
                }
            }

            return null;
        }

        private static string ResolvePicklistId(
            IReadOnlyDictionary<string, IReadOnlyList<AconexSchemaValueOption>> picklists,
            string fieldIdentifier,
            string primaryHint,
            string secondaryHint)
        {
            if (TryResolvePicklistId(picklists, fieldIdentifier, primaryHint, out string id))
                return id;
            if (TryResolvePicklistId(picklists, fieldIdentifier, secondaryHint, out id))
                return id;
            return null;
        }

        private static bool TryResolvePicklistId(
            IReadOnlyDictionary<string, IReadOnlyList<AconexSchemaValueOption>> picklists,
            string fieldIdentifier,
            string userInput,
            out string aconexId)
        {
            aconexId = null;
            if (picklists == null || string.IsNullOrWhiteSpace(fieldIdentifier) || string.IsNullOrWhiteSpace(userInput))
                return false;
            if (!picklists.TryGetValue(fieldIdentifier, out var options) || options == null || options.Count == 0)
                return false;

            string trimmed = userInput.Trim();
            foreach (AconexSchemaValueOption option in options)
            {
                if (option == null) continue;
                if (!string.IsNullOrWhiteSpace(option.Id) &&
                    string.Equals(option.Id.Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    aconexId = option.Id.Trim();
                    return true;
                }
            }

            foreach (AconexSchemaValueOption option in options)
            {
                if (option == null) continue;
                if (!string.IsNullOrWhiteSpace(option.Value) &&
                    string.Equals(option.Value.Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    aconexId = option.Id?.Trim();
                    return !string.IsNullOrWhiteSpace(aconexId);
                }
            }

            return false;
        }

        private static string GetHint(IReadOnlyDictionary<string, string> sourceValues, params string[] keys)
        {
            if (sourceValues == null || keys == null)
                return null;
            foreach (string key in keys)
            {
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                if (sourceValues.TryGetValue(key, out string value) && !string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }
            return null;
        }

        private static bool ShouldEmitIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return false;
            if (string.Equals(identifier, "id", StringComparison.OrdinalIgnoreCase))
                return false;
            return true;
        }

        private static bool IsProjectFieldSingleSelect(string identifier)
        {
            return !string.IsNullOrWhiteSpace(identifier) &&
                   identifier.EndsWith("_singleSelect", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ShouldSkipStandardInFavorOfProjectSingleSelect(string identifier, AconexRegisterSchemaSnapshot schema)
        {
            if (string.IsNullOrWhiteSpace(identifier) || IsProjectFieldSingleSelect(identifier))
                return false;

            string projectXml = identifier.Trim() + "_singleSelect";
            if (schema?.Fields == null)
                return false;

            foreach (AconexRegisterSchemaField field in schema.Fields)
            {
                if (field != null &&
                    string.Equals(field.Identifier, projectXml, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<AconexSchemaValueOption>> EmptyPicklists()
        {
            return new Dictionary<string, IReadOnlyList<AconexSchemaValueOption>>(StringComparer.OrdinalIgnoreCase);
        }

        private static string EscapeXml(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }
    }
}
