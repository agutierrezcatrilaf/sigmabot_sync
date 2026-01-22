using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IntegrationWorkers.Shared
{
    public class Utilities
    {
        public static long ParseLong(string s)
        {
            return long.TryParse(s, out var v) ? v : 0L;
        }

        public static string CleanXml(string xml)
        {
            if (string.IsNullOrEmpty(xml)) return xml;

            // Solo caracteres válidos en XML 1.0
            return new string(xml.Where(ch =>
                ch == 0x9 || ch == 0xA || ch == 0xD ||
                (ch >= 0x20 && ch <= 0xD7FF) ||
                (ch >= 0xE000 && ch <= 0xFFFD) ||
                (ch >= 0x10000 && ch <= 0x10FFFF)
            ).ToArray());
        }

        public static string EncodeTexto(string valor)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(valor);
            return Convert.ToBase64String(bytes);
        }

        public static void Wlog(string texto, int nivel)
        {
            try
            {
                File.AppendAllText(AppState.LogFile, texto + Environment.NewLine);
            }
            catch (Exception)
            {
                // Intencionalmente vacío, como en VB
            }
        }


        public static T EjecutarConReintentos<T>(Func<T> accion, string contexto, int maxRetries = 3)
        {
            int attempts = 0;

            while (true)
            {
                try
                {
                    return accion();
                }
                catch (Exception ex)
                {
                    attempts++;
                    Utilities.Wlog($"{contexto}: intento {attempts} fallido - {ex.Message}", 0);

                    if (attempts >= maxRetries)
                    {
                        Utilities.Wlog($"{contexto}: ERROR definitivo - {ex.Message}", 0);
                        throw; // lo dejamos propagar para que el caller decida qué hacer
                    }

                    Thread.Sleep(attempts * 1000); // espera incremental
                }
            }
        }

        public static async Task<T> EjecutarConReintentosAsync<T>(Func<Task<T>> accion, string contexto, int maxRetries = 3)
        {
            int attempts = 0;

            while (true)
            {
                try
                {
                    return await accion();
                }
                catch (Exception ex)
                {
                    attempts++;
                    Utilities.Wlog($"{contexto}: intento {attempts} fallido - {ex.Message}", 0);

                    if (attempts >= maxRetries)
                    {
                        Utilities.Wlog($"{contexto}: ERROR definitivo - {ex.Message}", 0);
                        throw;
                    }

                    await Task.Delay(attempts * 1000); // espera incremental (1s, 2s, 3s)
                }
            }
        }


    }
}
