using System;
using System.Collections.Generic;

namespace SigmabotSync.Domain.Models.Synchronization
{
    /// <summary>Catálogos TiposDocumentos / EstatusDocumentos + equivalencias project fields.</summary>
    public sealed class AconexDocumentCatalog
    {
        private static readonly IReadOnlyDictionary<string, string> EmptyMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static AconexDocumentCatalog Empty { get; } = new AconexDocumentCatalog(
            EmptyMap, EmptyMap, EmptyMap, EmptyMap);

        public AconexDocumentCatalog(
            IReadOnlyDictionary<string, string> idTipoPorNombre,
            IReadOnlyDictionary<string, string> idEstatusPorNombre,
            IReadOnlyDictionary<string, string> equivalenciaDiscipline = null,
            IReadOnlyDictionary<string, string> equivalenciaTipoDocumento = null)
        {
            IdTipoPorNombre = idTipoPorNombre ?? EmptyMap;
            IdEstatusPorNombre = idEstatusPorNombre ?? EmptyMap;
            EquivalenciaDiscipline = equivalenciaDiscipline ?? EmptyMap;
            EquivalenciaTipoDocumento = equivalenciaTipoDocumento ?? EmptyMap;
        }

        public IReadOnlyDictionary<string, string> IdTipoPorNombre { get; }
        public IReadOnlyDictionary<string, string> IdEstatusPorNombre { get; }
        /// <summary>ValorOrigen → ValorDestino (texto picklist SALFA).</summary>
        public IReadOnlyDictionary<string, string> EquivalenciaDiscipline { get; }
        /// <summary>ValorOrigen → ValorDestino (texto picklist SALFA).</summary>
        public IReadOnlyDictionary<string, string> EquivalenciaTipoDocumento { get; }

        public string ResolveByCatalog(string catalogo, string nameOrId)
        {
            if (string.IsNullOrWhiteSpace(catalogo) || string.IsNullOrWhiteSpace(nameOrId))
                return null;

            string trimmed = nameOrId.Trim();
            string table = catalogo.Trim();

            if (string.Equals(table, AconexDocumentCatalogNames.EquivalenciaDiscipline, StringComparison.OrdinalIgnoreCase))
                return ResolveByName(EquivalenciaDiscipline, trimmed);
            if (string.Equals(table, AconexDocumentCatalogNames.EquivalenciaTipoDocumento, StringComparison.OrdinalIgnoreCase))
                return ResolveByName(EquivalenciaTipoDocumento, trimmed);

            if (LooksLikeAconexId(trimmed))
                return trimmed;

            if (string.Equals(table, AconexDocumentCatalogNames.EstatusDocumentos, StringComparison.OrdinalIgnoreCase))
                return ResolveByName(IdEstatusPorNombre, trimmed);
            if (string.Equals(table, AconexDocumentCatalogNames.TiposDocumentos, StringComparison.OrdinalIgnoreCase))
                return ResolveByName(IdTipoPorNombre, trimmed);

            return null;
        }

        private static string ResolveByName(IReadOnlyDictionary<string, string> map, string name)
        {
            if (map == null || string.IsNullOrWhiteSpace(name))
                return null;

            if (map.TryGetValue(name.Trim(), out string id) && !string.IsNullOrWhiteSpace(id))
                return id.Trim();

            foreach (var kv in map)
            {
                if (string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(kv.Value))
                    return kv.Value.Trim();
            }

            return null;
        }

        private static bool LooksLikeAconexId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            foreach (char c in value)
            {
                if (!char.IsDigit(c))
                    return false;
            }
            return value.Length >= 8;
        }
    }
}
