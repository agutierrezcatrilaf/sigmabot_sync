using System;
using CommunityToolkit.Mvvm.ComponentModel;
using SigmabotSync.Domain.Entities;

namespace SigmabotSync.ConfigTool.ViewModels
{
    public partial class TrabajoRowViewModel : ObservableObject
    {
        public string EtiquetaLista => (Id > 0 ? Id + " — " : "") + (Nombre ?? "(sin nombre)");

        public static TrabajoRowViewModel FromEntity(Trabajo t)
        {
            if (t == null)
                return new TrabajoRowViewModel();
            var vm = new TrabajoRowViewModel();
            vm.Id = t.Id;
            vm.Nombre = t.Nombre;
            vm.Tipo = t.Tipo;
            vm.Perioricidad = t.Perioricidad;
            vm.FechaUltimaEjecucion = t.FechaUltimaEjecucion;
            vm.FechaProximaEjecucion = t.FechaProximaEjecucion;
            vm.ResultadoUltimaEjecucion = t.ResultadoUltimaEjecucion;
            vm.ControldeEjecucion = t.ControldeEjecucion;
            vm.Estado = t.Estado;
            vm.UltCorrEjecucion = t.UltCorrEjecucion;
            return vm;
        }

        public Trabajo ToEntity()
        {
            return new Trabajo
            {
                Id = Id,
                Nombre = Nombre,
                Tipo = Tipo,
                Perioricidad = Perioricidad,
                FechaUltimaEjecucion = FechaUltimaEjecucion,
                FechaProximaEjecucion = FechaProximaEjecucion,
                ResultadoUltimaEjecucion = ResultadoUltimaEjecucion,
                ControldeEjecucion = ControldeEjecucion,
                Estado = Estado,
                UltCorrEjecucion = UltCorrEjecucion
            };
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(EtiquetaLista))]
        private int _id;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(EtiquetaLista))]
        private string _nombre;
        [ObservableProperty] private string _tipo;
        [ObservableProperty] private string _perioricidad;
        [ObservableProperty] private DateTime? _fechaUltimaEjecucion;
        [ObservableProperty] private DateTime? _fechaProximaEjecucion;
        [ObservableProperty] private string _resultadoUltimaEjecucion;
        [ObservableProperty] private string _controldeEjecucion;
        [ObservableProperty] private string _estado;
        [ObservableProperty] private string _ultCorrEjecucion;
    }
}
