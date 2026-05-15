using System;
using System.Collections.Generic;

namespace SigmabotSync.Domain.Configuration
{
    /// <summary>
    /// Comprueba que existan valores no vacíos para las claves obligatorias de <c>TrabajosConfiguracion</c> según <c>Trabajos.Tipo</c>.
    /// </summary>
    public static class TrabajoConfiguracionParamValidator
    {
        /// <param name="tipoTrabajo">Valor de <c>Trabajos.Tipo</c>.</param>
        /// <param name="valorPorNombre">Clave Nombre (trim) → ValorTexto.</param>
        public static IReadOnlyList<string> ValidarObligatoriosPorTipo(string tipoTrabajo, IDictionary<string, string> valorPorNombre)
        {
            var errores = new List<string>();
            if (valorPorNombre == null)
            {
                errores.Add("No hay parámetros cargados.");
                return errores;
            }

            foreach (var def in TrabajoTipoConfigFieldCatalog.ObtenerObligatoriosPara(tipoTrabajo ?? string.Empty))
            {
                if (!valorPorNombre.TryGetValue(def.Clave.Trim(), out var v) || string.IsNullOrWhiteSpace(v))
                    errores.Add("Falta o está vacío: " + def.Etiqueta + " (" + def.Clave + ").");
            }

            return errores;
        }
    }
}
