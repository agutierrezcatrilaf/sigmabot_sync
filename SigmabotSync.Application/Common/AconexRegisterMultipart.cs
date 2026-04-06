using System;
using System.Text;

namespace SigmabotSync.Application.Common
{
    /// <summary>
    /// multipart/mixed para Register Document y Supersede (misma forma que la guía Aconex y ejemplos curl).
    /// El boundary es solo un nombre de separador: en el cuerpo aparece como <c>--{nombre}</c> entre partes y <c>--{nombre}--</c> al final
    /// (en el curl, si el nombre es <c>myboundary</c>, verás <c>--myboundary</c> y <c>--myboundary--</c>).
    /// </summary>
    public static class AconexRegisterMultipart
    {
        /// <summary>
        /// Mismo valor que en el curl de ejemplo: <c>boundary="myboundary"</c>. Es válido usar siempre este string.
        /// </summary>
        public const string ExampleBoundary = "myboundary";

        /// <summary>
        /// Opcional: otro nombre de boundary en cada petición (<c>sigmabot_</c> + GUID) para casi eliminar la posibilidad de que
        /// esa misma cadena aparezca dentro del XML o del PDF (colisión con el parser). Aconex no lo exige; <see cref="ExampleBoundary"/> suele bastar.
        /// </summary>
        public static string CreateBoundary()
        {
            return "sigmabot_" + Guid.NewGuid().ToString("N");
        }

        /// <summary>
        /// Construye el body multipart/mixed: parte 1 = XML Document, parte 2 = X-Filename + base64 del archivo.
        /// </summary>
        public static string BuildRegisterBody(string xmlDocument, string fileName, string fileBase64, string boundary)
        {
            if (string.IsNullOrEmpty(boundary))
                throw new ArgumentException("boundary requerido.", nameof(boundary));

            var sb = new StringBuilder();
            sb.Append("--").Append(boundary).Append("\r\n\r\n");
            sb.Append(xmlDocument ?? "").Append("\r\n");
            sb.Append("--").Append(boundary).Append("\r\n");
            sb.Append("X-Filename: ").Append(fileName ?? "document").Append("\r\n\r\n");
            sb.Append(fileBase64 ?? "").Append("\r\n\r\n");
            sb.Append("--").Append(boundary).Append("--");
            return sb.ToString();
        }
    }
}
