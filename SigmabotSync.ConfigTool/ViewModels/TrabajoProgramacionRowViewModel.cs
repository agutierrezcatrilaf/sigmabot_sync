using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using SigmabotSync.Domain.Entities;

namespace SigmabotSync.ConfigTool.ViewModels
{
    public partial class TrabajoProgramacionRowViewModel : ObservableObject
    {
        public static TrabajoProgramacionRowViewModel FromEntity(TrabajoProgramacion p)
        {
            if (p == null)
                return new TrabajoProgramacionRowViewModel();
            var vm = new TrabajoProgramacionRowViewModel();
            vm.Id = p.Id;
            vm.IdTrabajo = p.IdTrabajo;
            vm.DiaSemana = p.DiaSemana;
            var match = HoraProgramacionCatalogo.Match(p.Hora);
            if (match != null)
                vm.HoraSeleccionada = match;
            else
                vm.HoraTexto = FormatearHora(p.Hora);
            vm.Activo = p.Activo;
            return vm;
        }

        public TrabajoProgramacion ToEntity()
        {
            TimeSpan hora;
            if (HoraSeleccionada != null)
                hora = HoraSeleccionada.ValorEnBd;
            else if (!TryParseHora(HoraTexto, out hora))
                hora = TimeSpan.Zero;
            return new TrabajoProgramacion
            {
                Id = Id,
                IdTrabajo = IdTrabajo,
                DiaSemana = DiaSemana,
                Hora = hora,
                Activo = Activo
            };
        }

        private static string FormatearHora(TimeSpan t)
        {
            if (t < TimeSpan.Zero || t >= TimeSpan.FromDays(1))
                t = TimeSpan.Zero;
            if (t.Seconds == 0 && t.Milliseconds == 0)
                return string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}", t.Hours, t.Minutes);
            return string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}:{2:D2}", t.Hours, t.Minutes, t.Seconds);
        }

        internal static bool TryParseHora(string texto, out TimeSpan hora)
        {
            texto = (texto ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(texto))
            {
                hora = TimeSpan.Zero;
                return false;
            }

            if (TimeSpan.TryParse(texto, CultureInfo.InvariantCulture, out hora) && hora >= TimeSpan.Zero && hora < TimeSpan.FromDays(1))
                return true;

            var formatos = new[] { "HH:mm", "H:mm", "HH:mm:ss", "H:mm:ss" };
            if (DateTime.TryParseExact(texto, formatos, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            {
                hora = dt.TimeOfDay;
                return true;
            }

            hora = TimeSpan.Zero;
            return false;
        }

        [ObservableProperty] private int _id;
        [ObservableProperty] private int _idTrabajo;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DiaSemanaDisplay))]
        private int _diaSemana;

        public string DiaSemanaDisplay =>
            DiaSemana switch
            {
                0 => "Domingo",
                1 => "Lunes",
                2 => "Martes",
                3 => "Miércoles",
                4 => "Jueves",
                5 => "Viernes",
                6 => "Sábado",
                _ => "Día " + DiaSemana
            };

        [ObservableProperty] private string _horaTexto = "09:00";

        [ObservableProperty] private HoraProgramacionOpcion _horaSeleccionada;

        [ObservableProperty] private bool _activo = true;

        partial void OnHoraSeleccionadaChanged(HoraProgramacionOpcion value)
        {
            if (value != null)
                HoraTexto = value.Display;
        }
    }
}
