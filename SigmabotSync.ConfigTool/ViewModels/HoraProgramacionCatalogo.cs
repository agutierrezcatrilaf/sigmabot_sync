using System;
using System.Collections.Generic;

namespace SigmabotSync.ConfigTool.ViewModels
{
    /// <summary>Opción de hora en pasos de 1 hora para <c>TrabajosProgramacion.Hora</c>.</summary>
    public sealed class HoraProgramacionOpcion : IEquatable<HoraProgramacionOpcion>
    {
        public HoraProgramacionOpcion(string display, TimeSpan valorEnBd)
        {
            Display = display ?? string.Empty;
            ValorEnBd = valorEnBd;
        }

        /// <summary>Texto mostrado (ej. 09:00, 24:00).</summary>
        public string Display { get; }

        /// <summary>Valor guardado en columna TIME (24:00 → 23:59:59 por límite de SQL Server).</summary>
        public TimeSpan ValorEnBd { get; }

        public bool Equals(HoraProgramacionOpcion other) => other != null && ValorEnBd == other.ValorEnBd && Display == other.Display;

        public override bool Equals(object obj) => obj is HoraProgramacionOpcion o && Equals(o);

        public override int GetHashCode() => HashCode.Combine(Display, ValorEnBd);
    }

    /// <summary>01:00 … 23:00 y 24:00 (este último se persiste como 23:59:59).</summary>
    public static class HoraProgramacionCatalogo
    {
        public static IReadOnlyList<HoraProgramacionOpcion> Todas { get; }

        static HoraProgramacionCatalogo()
        {
            var list = new List<HoraProgramacionOpcion>(24);
            for (int h = 1; h <= 23; h++)
                list.Add(new HoraProgramacionOpcion($"{h:D2}:00", new TimeSpan(h, 0, 0)));
            list.Add(new HoraProgramacionOpcion("24:00", new TimeSpan(23, 59, 59)));
            Todas = list;
        }

        /// <summary>Coincide con una hora leída de BD (tolerancia a segundos).</summary>
        public static HoraProgramacionOpcion Match(TimeSpan t)
        {
            if (t < TimeSpan.Zero || t >= TimeSpan.FromDays(1))
                return null;

            // Slot "24:00" persistido como 23:59:59
            if (t >= new TimeSpan(23, 59, 0))
                return Todas[^1];

            foreach (var o in Todas)
            {
                if (o == Todas[^1])
                    continue;
                if (Math.Abs((o.ValorEnBd - t).TotalSeconds) < 1.5)
                    return o;
            }

            return null;
        }

        public static HoraProgramacionOpcion PorDisplay(string display)
        {
            if (string.IsNullOrWhiteSpace(display))
                return null;
            var x = display.Trim();
            foreach (var o in Todas)
            {
                if (string.Equals(o.Display, x, StringComparison.OrdinalIgnoreCase))
                    return o;
            }

            return null;
        }
    }
}
