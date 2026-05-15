using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SigmabotSync.Domain.Configuration;

namespace SigmabotSync.ConfigTool.ViewModels
{
    /// <summary>Un campo del formulario guiado de TrabajosConfiguracion (una clave del catálogo).</summary>
    public partial class ParametroTrabajoCampoViewModel : ObservableObject
    {
        public ParametroTrabajoCampoViewModel(
            TrabajoConfiguracionCampoDefinicion def,
            string tipoTrabajo,
            string valorInicial,
            IReadOnlyList<CredencialComboItem> credencialesParaCombo = null)
        {
            Clave = def.Clave;
            Etiqueta = def.Etiqueta;
            Ayuda = def.Ayuda ?? string.Empty;
            TituloCampo = def.EsObligatorioPara(tipoTrabajo) ? def.Etiqueta + " (*)" : def.Etiqueta;

            if (credencialesParaCombo != null)
            {
                foreach (var c in credencialesParaCombo)
                    OpcionesCredencial.Add(c);
            }

            UsaSelectorCredencial =
                OpcionesCredencial.Count > 0
                && (Clave == TrabajosConfiguracionKeyNames.CredencialAconex || Clave == TrabajosConfiguracionKeyNames.CredencialBD);

            var inicial = valorInicial ?? string.Empty;
            if (!UsaSelectorCredencial)
            {
                Valor = inicial;
                return;
            }

            if (int.TryParse(inicial.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var idCred))
            {
                var item = OpcionesCredencial.FirstOrDefault(c => c.Id == idCred);
                if (item != null)
                    CredencialSeleccionada = item;
                else
                    Valor = string.Empty;
            }
            else
                Valor = string.Empty;
        }

        public string Clave { get; }

        public string Etiqueta { get; }

        public string Ayuda { get; }

        /// <summary>Texto de etiqueta con marca de obligatorio si aplica.</summary>
        public string TituloCampo { get; }

        /// <summary>Si es true, se muestra combo de credenciales en lugar de caja de texto.</summary>
        public bool UsaSelectorCredencial { get; }

        public ObservableCollection<CredencialComboItem> OpcionesCredencial { get; } = new ObservableCollection<CredencialComboItem>();

        [ObservableProperty] private CredencialComboItem _credencialSeleccionada;

        [ObservableProperty] private string _valor = string.Empty;

        partial void OnCredencialSeleccionadaChanged(CredencialComboItem value)
        {
            Valor = value == null ? string.Empty : value.Id.ToString(CultureInfo.InvariantCulture);
        }
    }
}
