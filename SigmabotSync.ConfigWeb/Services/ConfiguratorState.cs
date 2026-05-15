using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Data.SqlClient;
using SigmabotSync.ConfigWeb.ViewModels;
using SigmabotSync.Domain.Configuration;
using SigmabotSync.Domain.Entities;
using SigmabotSync.Infrastructure.Services;
using SigmabotSync.Infrastructure.Services.ConfigurationEditor;

namespace SigmabotSync.ConfigWeb.Services
{
    public partial class ConfiguratorState : ObservableObject
    {
        private readonly SettingsService _settingsService;
        private readonly IConfiguratorDialogs _dialogs;

        public string SettingsFilePath => _settingsService.SettingsFilePath;

        public ConfiguratorState(IConfiguratorDialogs dialogs, SettingsService settingsService)
        {
            _dialogs = dialogs;
            _settingsService = settingsService;
            var settings = _settingsService.Load();
            ConnectionString = settings?.DatabaseConnectionString ?? string.Empty;
        }

        public void OnTabIndexChanged(int value)
        {
            SelectedTabIndex = value;
            OnSelectedTabIndexChanged(value);
        }

        [ObservableProperty] private string _connectionString = string.Empty;

        /// <summary>Cadena normalizada tras probar conexión (TrustServerCertificate).</summary>
        [ObservableProperty] private string _effectiveConnectionString = string.Empty;

        [ObservableProperty] private string _statusMessage = "Indique la cadena de conexión y pulse Probar conexión.";

        [ObservableProperty] private int _selectedTabIndex;

        private const int TabIndiceCredenciales = 1;

        private const int TabIndiceTrabajos = 2;

        private const int TabIndiceParametrosTrabajo = 3;

        private const int TabIndiceProgramacion = 4;

        partial void OnSelectedTabIndexChanged(int value)
        {
            try
            {
                if (value == TabIndiceCredenciales && Credenciales.Count == 0)
                    AutoCargarCredencialesSiHayConexion();

                if ((value == TabIndiceTrabajos || value == TabIndiceProgramacion) && Trabajos.Count == 0)
                    AutoCargarTrabajosSiHayConexion();

                if (value == TabIndiceParametrosTrabajo && Trabajos.Count == 0)
                    AutoCargarTrabajosSiHayConexion(limpiarSeleccionYParametros: true);
            }
            catch (Exception ex)
            {
                StatusMessage = "Error al cargar datos de la pestaña: " + ex.Message;
            }
        }

        private void AutoCargarCredencialesSiHayConexion()
        {
            var cs = ResolverCadenaParaServicios();
            if (cs == null)
                return;

            var svc = new CredencialesEditorService(cs);
            Credenciales.Clear();
            foreach (var c in svc.ListarTodas())
                Credenciales.Add(CredencialRowViewModel.FromEntity(c));
            if (Credenciales.Count > 0)
                StatusMessage = Credenciales.Count + " credencial(es): lista cargada al abrir esta pestaña.";
            else
                StatusMessage = "No hay filas en Credenciales o revise la conexión.";
        }

        private void AutoCargarTrabajosSiHayConexion(bool limpiarSeleccionYParametros = false)
        {
            var cs = ResolverCadenaParaServicios();
            if (cs == null)
                return;

            RecargarTrabajosDesdeBd(limpiarSeleccionYParametros);
            if (Trabajos.Count > 0)
                StatusMessage = Trabajos.Count + " trabajo(s): lista cargada al abrir esta pestaña.";
            else if (!string.IsNullOrWhiteSpace(EffectiveConnectionString) || !string.IsNullOrWhiteSpace(ConnectionString?.Trim()))
                StatusMessage = "No hay trabajos en la tabla o revise la conexión (Probar conexión).";
        }

        /// <summary>Vacía y vuelve a leer <c>Trabajos</c> desde la BD.</summary>
        private void RecargarTrabajosDesdeBd(bool limpiarSeleccionYParametros)
        {
            var cs = ResolverCadenaParaServicios();
            if (cs == null)
                return;

            int idParam = 0;
            int idProg = 0;
            if (!limpiarSeleccionYParametros)
            {
                idParam = TrabajoParaParametros?.Id ?? 0;
                idProg = TrabajoParaProgramacion?.Id ?? 0;
            }

            if (limpiarSeleccionYParametros)
            {
                TrabajoParaParametros = null;
                CamposParametrosTrabajo.Clear();
                FormularioParametrosActivo = false;
                MensajeFormularioParametros = string.Empty;
                TrabajoParaProgramacion = null;
                ProgramacionesTrabajo.Clear();
            }

            var svc = new TrabajosEditorService(cs);
            Trabajos.Clear();
            foreach (var t in svc.ListarTodos())
                Trabajos.Add(TrabajoRowViewModel.FromEntity(t));

            if (!limpiarSeleccionYParametros)
            {
                if (idParam > 0)
                    TrabajoParaParametros = Trabajos.FirstOrDefault(t => t.Id == idParam);
                if (idProg > 0)
                    TrabajoParaProgramacion = Trabajos.FirstOrDefault(t => t.Id == idProg);
            }
        }

        [ObservableProperty] private CredencialRowViewModel _credencialSeleccionada;

        public ObservableCollection<CredencialRowViewModel> Credenciales { get; } = new ObservableCollection<CredencialRowViewModel>();

        [ObservableProperty] private TrabajoRowViewModel _trabajoSeleccionado;

        public ObservableCollection<TrabajoRowViewModel> Trabajos { get; } = new ObservableCollection<TrabajoRowViewModel>();

        /// <summary>Trabajo cuyos parámetros (TrabajosConfiguracion) se editan en la pestaña correspondiente.</summary>
        [ObservableProperty] private TrabajoRowViewModel _trabajoParaParametros;

        /// <summary>Campos del formulario guiado según <see cref="TrabajoTipoConfigFieldCatalog"/> y el tipo del trabajo.</summary>
        public ObservableCollection<ParametroTrabajoCampoViewModel> CamposParametrosTrabajo { get; } = new ObservableCollection<ParametroTrabajoCampoViewModel>();

        [ObservableProperty] private bool _formularioParametrosActivo;

        [ObservableProperty] private string _mensajeFormularioParametros = string.Empty;

        /// <summary>Trabajo cuya programación (<c>TrabajosProgramacion</c>) se edita en la pestaña correspondiente.</summary>
        [ObservableProperty] private TrabajoRowViewModel _trabajoParaProgramacion;

        [ObservableProperty] private TrabajoProgramacionRowViewModel _programacionSeleccionada;

        public ObservableCollection<TrabajoProgramacionRowViewModel> ProgramacionesTrabajo { get; } = new ObservableCollection<TrabajoProgramacionRowViewModel>();

        /// <summary>Combo día de la semana (0=domingo … 6=sábado, <see cref="System.DayOfWeek"/>).</summary>
        public IReadOnlyList<DiaSemanaOpcion> DiasSemanaProgramacion { get; } = new DiaSemanaOpcion[]
        {
            new() { Valor = 0, Nombre = "Domingo" },
            new() { Valor = 1, Nombre = "Lunes" },
            new() { Valor = 2, Nombre = "Martes" },
            new() { Valor = 3, Nombre = "Miércoles" },
            new() { Valor = 4, Nombre = "Jueves" },
            new() { Valor = 5, Nombre = "Viernes" },
            new() { Valor = 6, Nombre = "Sábado" }
        };

        /// <summary>Horas en bloques de 1 h (01:00–23:00 y 24:00; 24:00 se guarda como 23:59:59 en TIME).</summary>
        public IReadOnlyList<HoraProgramacionOpcion> HorasProgramacionParaCombo => HoraProgramacionCatalogo.Todas;

        /// <summary>Opciones del combo Tipo en la grilla de trabajos.</summary>
        public IReadOnlyList<string> TiposTrabajoParaEditor { get; } = new[]
        {
            TipoTrabajoIds.FileExtraction,
            TipoTrabajoIds.ProjectSync,
            TipoTrabajoIds.FullExtraction,
            TipoTrabajoIds.FileUploadWithMetadata
        };

        /// <summary>Opciones del combo Estado (el motor solo considera operativo el valor Activo).</summary>
        public IReadOnlyList<string> EstadosTrabajoParaEditor { get; } = new[]
        {
            TrabajoEstadoIds.Activo,
            TrabajoEstadoIds.Desactivado,
            TrabajoEstadoIds.Pendiente
        };

        /// <summary>Opciones del combo Tipo en la grilla de credenciales.</summary>
        public IReadOnlyList<string> TiposCredencialParaEditor { get; } = new[]
        {
            CredencialTipoIds.Aconex,
            CredencialTipoIds.BD
        };

        public void ProbarConexion()
        {
            try
            {
                var raw = (ConnectionString ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(raw))
                {
                    StatusMessage = "La cadena de conexión está vacía.";
                    EffectiveConnectionString = string.Empty;
                    return;
                }

                var cs = ConnectionStringHelper.AsegurarTrustServerCertificate(raw);
                using (var cn = new SqlConnection(cs))
                {
                    cn.Open();
                }

                EffectiveConnectionString = cs;
                StatusMessage = "Conexión correcta.";
            }
            catch (Exception ex)
            {
                EffectiveConnectionString = string.Empty;
                StatusMessage = "Error: " + ex.Message;
            }
        }

        public void GuardarSettings()
        {
            try
            {
                var settings = _settingsService.Load() ?? new SigmabotSync.Domain.Config.AconexSettings();
                settings.DatabaseConnectionString = (ConnectionString ?? string.Empty).Trim();
                _settingsService.Save(settings);
                StatusMessage = "Guardado en: " + _settingsService.SettingsFilePath;
            }
            catch (Exception ex)
            {
                StatusMessage = "No se pudo guardar settings: " + ex.Message;
            }
        }

        public void CargarCredenciales()
        {
            var cs = ResolverCadenaParaServicios();
            if (cs == null)
                return;

            try
            {
                var svc = new CredencialesEditorService(cs);
                Credenciales.Clear();
                foreach (var c in svc.ListarTodas())
                    Credenciales.Add(CredencialRowViewModel.FromEntity(c));
                StatusMessage = Credenciales.Count + " credencial(es) cargada(s).";
            }
            catch (Exception ex)
            {
                StatusMessage = "Error al cargar credenciales: " + ex.Message;
            }
        }

        public void NuevaCredencial()
        {
            var cs = ResolverCadenaParaServicios();
            if (cs == null)
                return;

            Credenciales.Add(new CredencialRowViewModel
            {
                Id = 0,
                Tipo = CredencialTipoIds.Aconex,
                Nombre = "Nueva"
            });
            StatusMessage = "Fila nueva (sin guardar). Complete campos y pulse Guardar credenciales.";
        }

        public async Task GuardarCredencialesAsync()
        {
            var cs = ResolverCadenaParaServicios();
            if (cs == null)
                return;

            try
            {
                var svc = new CredencialesEditorService(cs);
                foreach (var row in Credenciales)
                {
                    var entity = row.ToEntity();
                    var etiquetaFila = string.IsNullOrWhiteSpace(entity.Nombre)
                        ? ("Id=" + (entity.Id > 0 ? entity.Id.ToString() : "(nueva)"))
                        : ("\"" + entity.Nombre.Trim() + "\"");

                    var validacion = CredencialRequisitosValidator.ValidarCamposObligatorios(entity);
                    if (validacion.Count > 0)
                    {
                        var detalle = string.Join(Environment.NewLine + "  • ", validacion);
                        await _dialogs.ShowWarningAsync(
                            "Credencial " + etiquetaFila + ":" + Environment.NewLine + "  • " + detalle,
                            "Campos obligatorios");
                        StatusMessage = "Revise la fila " + etiquetaFila + " (campos obligatorios por tipo).";
                        return;
                    }

                    if (entity.Id <= 0)
                    {
                        int newId = svc.Insertar(entity);
                        row.Id = newId;
                    }
                    else
                        svc.Actualizar(entity);
                }

                StatusMessage = "Credenciales guardadas.";
            }
            catch (Exception ex)
            {
                await _dialogs.ShowWarningAsync(ex.Message, "Error al guardar");
                StatusMessage = "Error al guardar: " + ex.Message;
            }
        }

        public async Task EliminarCredencialSeleccionadaAsync()
        {
            if (CredencialSeleccionada == null)
            {
                StatusMessage = "Seleccione una fila para eliminar.";
                return;
            }

            var cs = ResolverCadenaParaServicios();
            if (cs == null)
                return;

            var row = CredencialSeleccionada;
            if (row.Id <= 0)
            {
                Credenciales.Remove(row);
                StatusMessage = "Fila nueva eliminada.";
                return;
            }

            if (!await _dialogs.ConfirmAsync(
                    "¿Eliminar la credencial Id=" + row.Id + " (" + (row.Nombre ?? "") + ")?",
                    "Confirmar"))
                return;

            try
            {
                var svc = new CredencialesEditorService(cs);
                svc.Eliminar(row.Id);
                Credenciales.Remove(row);
                StatusMessage = "Credencial eliminada.";
            }
            catch (Exception ex)
            {
                await _dialogs.ShowWarningAsync(ex.Message, "Error al eliminar");
                StatusMessage = "Error al eliminar: " + ex.Message;
            }
        }

        public void CargarTrabajos()
        {
            try
            {
                RecargarTrabajosDesdeBd(true);
                StatusMessage = Trabajos.Count + " trabajo(s) cargado(s).";
            }
            catch (Exception ex)
            {
                StatusMessage = "Error al cargar trabajos: " + ex.Message;
            }
        }

        public void NuevoTrabajo()
        {
            var cs = ResolverCadenaParaServicios();
            if (cs == null)
                return;

            Trabajos.Add(new TrabajoRowViewModel
            {
                Id = 0,
                Nombre = "Nuevo trabajo",
                Tipo = TipoTrabajoIds.FileExtraction,
                Estado = TrabajoEstadoIds.Desactivado
            });
            StatusMessage = "Fila nueva de trabajo. Ajuste Tipo/Estado y guarde.";
        }

        public async Task GuardarTrabajosAsync()
        {
            var cs = ResolverCadenaParaServicios();
            if (cs == null)
                return;

            try
            {
                var svc = new TrabajosEditorService(cs);
                foreach (var row in Trabajos)
                {
                    var entity = row.ToEntity();
                    var etiqueta = entity.Id > 0 ? ("Id=" + entity.Id) : ("\"" + (entity.Nombre ?? "").Trim() + "\"");

                    var validacion = TrabajoRequisitosValidator.Validar(entity);
                    if (validacion.Count > 0)
                    {
                        var detalle = string.Join(Environment.NewLine + "  • ", validacion);
                        await _dialogs.ShowWarningAsync(
                            "Trabajo " + etiqueta + ":" + Environment.NewLine + "  • " + detalle,
                            "Validación");
                        StatusMessage = "Revise el trabajo " + etiqueta + ".";
                        return;
                    }

                    if (entity.Id <= 0)
                    {
                        int newId = svc.Insertar(entity);
                        row.Id = newId;
                    }
                    else
                        svc.Actualizar(entity);
                }

                StatusMessage = "Trabajos guardados.";
            }
            catch (Exception ex)
            {
                await _dialogs.ShowWarningAsync(ex.Message, "Error al guardar trabajos");
                StatusMessage = "Error al guardar trabajos: " + ex.Message;
            }
        }

        public async Task EliminarTrabajoSeleccionadoAsync()
        {
            if (TrabajoSeleccionado == null)
            {
                StatusMessage = "Seleccione un trabajo en la grilla.";
                return;
            }

            var cs = ResolverCadenaParaServicios();
            if (cs == null)
                return;

            var row = TrabajoSeleccionado;
            if (row.Id <= 0)
            {
                Trabajos.Remove(row);
                if (TrabajoParaParametros == row)
                {
                    TrabajoParaParametros = null;
                    CamposParametrosTrabajo.Clear();
                    FormularioParametrosActivo = false;
                    MensajeFormularioParametros = string.Empty;
                }

                if (TrabajoParaProgramacion == row)
                {
                    TrabajoParaProgramacion = null;
                    ProgramacionesTrabajo.Clear();
                }

                StatusMessage = "Fila nueva eliminada.";
                return;
            }

            if (!await _dialogs.ConfirmAsync(
                    "¿Eliminar el trabajo Id=" + row.Id + " (" + (row.Nombre ?? "") + ")? Puede fallar si hay programación (FK) u otras dependencias.",
                    "Confirmar"))
                return;

            try
            {
                var svc = new TrabajosEditorService(cs);
                svc.Eliminar(row.Id);
                Trabajos.Remove(row);
                if (TrabajoParaParametros == row)
                {
                    TrabajoParaParametros = null;
                    CamposParametrosTrabajo.Clear();
                    FormularioParametrosActivo = false;
                    MensajeFormularioParametros = string.Empty;
                }

                if (TrabajoParaProgramacion == row)
                {
                    TrabajoParaProgramacion = null;
                    ProgramacionesTrabajo.Clear();
                }

                StatusMessage = "Trabajo eliminado.";
            }
            catch (Exception ex)
            {
                await _dialogs.ShowWarningAsync(ex.Message, "Error al eliminar");
                StatusMessage = "Error al eliminar: " + ex.Message;
            }
        }

        partial void OnTrabajoParaParametrosChanged(TrabajoRowViewModel value)
        {
            ReconstruirFormularioParametrosDesdeBd();
        }

        partial void OnTrabajoParaProgramacionChanged(TrabajoRowViewModel value)
        {
            ReconstruirProgramacionesDesdeBd();
        }

        private void ReconstruirProgramacionesDesdeBd()
        {
            ProgramacionesTrabajo.Clear();
            if (TrabajoParaProgramacion == null || TrabajoParaProgramacion.Id <= 0)
                return;

            var cs = ResolverCadenaParaServicios();
            if (cs == null)
                return;

            try
            {
                var svc = new TrabajosProgramacionEditorService(cs);
                foreach (var p in svc.ListarPorIdTrabajo(TrabajoParaProgramacion.Id))
                    ProgramacionesTrabajo.Add(TrabajoProgramacionRowViewModel.FromEntity(p));
            }
            catch (Exception ex)
            {
                StatusMessage = "Programación: error al leer — " + ex.Message;
            }
        }

        public async Task CargarProgramacionTrabajoAsync()
        {
            if (TrabajoParaProgramacion == null || TrabajoParaProgramacion.Id <= 0)
            {
                await _dialogs.ShowInfoAsync("Seleccione un trabajo ya guardado (con Id) en el desplegable.", "Programación");
                return;
            }

            var cs = ResolverCadenaParaServicios();
            if (cs == null)
                return;

            ReconstruirProgramacionesDesdeBd();
            StatusMessage = ProgramacionesTrabajo.Count + " fila(s) en TrabajosProgramacion para IdTrabajo=" + TrabajoParaProgramacion.Id + ".";
        }

        public async Task NuevaFilaProgramacionAsync()
        {
            if (TrabajoParaProgramacion == null || TrabajoParaProgramacion.Id <= 0)
            {
                await _dialogs.ShowInfoAsync("Seleccione un trabajo con Id válido.", "Programación");
                return;
            }

            var nueva = new TrabajoProgramacionRowViewModel
            {
                Id = 0,
                IdTrabajo = TrabajoParaProgramacion.Id,
                DiaSemana = 1,
                Activo = true
            };
            nueva.HoraSeleccionada = HoraProgramacionCatalogo.PorDisplay("09:00");
            ProgramacionesTrabajo.Add(nueva);
            StatusMessage = "Nueva fila de programación (sin guardar).";
        }

        public async Task GuardarProgramacionTrabajoAsync()
        {
            var cs = ResolverCadenaParaServicios();
            if (cs == null)
                return;

            if (TrabajoParaProgramacion == null || TrabajoParaProgramacion.Id <= 0)
            {
                await _dialogs.ShowInfoAsync("Seleccione un trabajo con Id válido.", "Programación");
                return;
            }

            int idTrabajo = TrabajoParaProgramacion.Id;
            foreach (var row in ProgramacionesTrabajo)
            {
                row.IdTrabajo = idTrabajo;
                if (row.DiaSemana < 0 || row.DiaSemana > 6)
                {
                    await _dialogs.ShowWarningAsync("Día de la semana debe estar entre 0 (domingo) y 6 (sábado).", "Programación");
                    return;
                }

                if (row.HoraSeleccionada == null && !TrabajoProgramacionRowViewModel.TryParseHora(row.HoraTexto, out _))
                {
                    await _dialogs.ShowWarningAsync("Seleccione la hora en el desplegable o escriba HH:mm (ej. 14:30).", "Programación");
                    return;
                }
            }

            try
            {
                var svc = new TrabajosProgramacionEditorService(cs);
                foreach (var row in ProgramacionesTrabajo)
                {
                    var ent = row.ToEntity();
                    if (ent.Id <= 0)
                    {
                        int newId = svc.Insertar(ent);
                        row.Id = newId;
                    }
                    else
                        svc.Actualizar(ent);
                }

                StatusMessage = "Programación guardada (IdTrabajo=" + idTrabajo + ").";
            }
            catch (Exception ex)
            {
                await _dialogs.ShowWarningAsync(ex.Message, "Error al guardar programación");
                StatusMessage = "Error al guardar programación: " + ex.Message;
            }
        }

        public async Task EliminarProgramacionSeleccionadaAsync()
        {
            if (ProgramacionSeleccionada == null)
            {
                StatusMessage = "Seleccione una fila de programación.";
                return;
            }

            var cs = ResolverCadenaParaServicios();
            if (cs == null)
                return;

            var row = ProgramacionSeleccionada;
            if (row.Id <= 0)
            {
                ProgramacionesTrabajo.Remove(row);
                StatusMessage = "Fila nueva eliminada.";
                return;
            }

            if (!await _dialogs.ConfirmAsync("¿Eliminar la programación Id=" + row.Id + " (día " + row.DiaSemana + ", " + row.HoraTexto + ")?", "Confirmar"))
                return;

            try
            {
                var svc = new TrabajosProgramacionEditorService(cs);
                svc.Eliminar(row.Id);
                ProgramacionesTrabajo.Remove(row);
                StatusMessage = "Programación eliminada.";
            }
            catch (Exception ex)
            {
                await _dialogs.ShowWarningAsync(ex.Message, "Error al eliminar");
                StatusMessage = "Error al eliminar: " + ex.Message;
            }
        }

        public async Task CargarParametrosTrabajoAsync()
        {
            if (TrabajoParaParametros == null || TrabajoParaParametros.Id <= 0)
            {
                await _dialogs.ShowInfoAsync("Seleccione un trabajo ya guardado (con Id) en el desplegable.", "Parámetros");
                return;
            }

            var cs = ResolverCadenaParaServicios();
            if (cs == null)
                return;

            ReconstruirFormularioParametrosDesdeBd();
            if (FormularioParametrosActivo)
                StatusMessage = "Parámetros cargados (IdTrabajo=" + TrabajoParaParametros.Id + ", tipo " + (TrabajoParaParametros.Tipo ?? "").Trim() + ").";
        }

        public async Task GuardarParametrosTrabajoAsync()
        {
            var cs = ResolverCadenaParaServicios();
            if (cs == null)
                return;

            if (TrabajoParaParametros == null || TrabajoParaParametros.Id <= 0)
            {
                await _dialogs.ShowInfoAsync("Seleccione un trabajo con Id válido.", "Parámetros");
                return;
            }

            var tipoTrabajo = (TrabajoParaParametros.Tipo ?? string.Empty).Trim();
            if (!TrabajoTipoConfigFieldCatalog.TipoSoportaFormularioGuiado(tipoTrabajo))
            {
                await _dialogs.ShowInfoAsync("Este tipo de trabajo no tiene formulario guiado aquí. No se guardan cambios desde esta pestaña.", "Parámetros");
                return;
            }

            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var campo in CamposParametrosTrabajo)
                dict[campo.Clave] = campo.Valor ?? string.Empty;

            var valParams = TrabajoConfiguracionParamValidator.ValidarObligatoriosPorTipo(tipoTrabajo, dict);
            if (valParams.Count > 0)
            {
                var detalle = string.Join(Environment.NewLine + "  • ", valParams);
                await _dialogs.ShowWarningAsync(
                    "Parámetros incompletos para tipo \"" + tipoTrabajo + "\":" + Environment.NewLine + "  • " + detalle,
                    "Validación");
                StatusMessage = "Revise los campos marcados con (*).";
                return;
            }

            try
            {
                var svc = new TrabajosConfiguracionEditorService(cs);
                int idTrabajo = TrabajoParaParametros.Id;
                foreach (var campo in CamposParametrosTrabajo)
                {
                    var v = campo.Valor;
                    var guardar = string.IsNullOrWhiteSpace(v) ? null : v.Trim();
                    svc.UpsertValorTexto(idTrabajo, campo.Clave, guardar);
                }

                StatusMessage = "Parámetros guardados (IdTrabajo=" + idTrabajo + ").";
            }
            catch (Exception ex)
            {
                await _dialogs.ShowWarningAsync(ex.Message, "Error al guardar parámetros");
                StatusMessage = "Error al guardar parámetros: " + ex.Message;
            }
        }

        public async Task AbrirReferenciaCamposAsync()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Claves estándar (TrabajosConfiguracion.Nombre):");
            sb.AppendLine("- " + TrabajosConfiguracionKeyNames.IdProyecto);
            sb.AppendLine("- " + TrabajosConfiguracionKeyNames.CredencialAconex + " / " + TrabajosConfiguracionKeyNames.CredencialBD);
            sb.AppendLine("- " + TrabajosConfiguracionKeyNames.CamposConsulta + " / " + TrabajosConfiguracionKeyNames.CamposResponse + " / " + TrabajosConfiguracionKeyNames.CamposBD);
            sb.AppendLine("- " + TrabajosConfiguracionKeyNames.BasePath + " (FileExtraction, FileUploadWithMetadata)");
            sb.AppendLine("- " + TrabajosConfiguracionKeyNames.TablaMetadata + " (FileUploadWithMetadata)");
            sb.AppendLine();
            sb.AppendLine("Programación (tabla TrabajosProgramacion): IdTrabajo, DiaSemana 0–6 (domingo–sábado), Hora (TIME), Activo.");
            sb.AppendLine();
            sb.AppendLine("Tipos de trabajo (Trabajos.Tipo):");
            sb.AppendLine("- " + TipoTrabajoIds.FileExtraction);
            sb.AppendLine("- " + TipoTrabajoIds.FullExtraction);
            sb.AppendLine("- " + TipoTrabajoIds.FileUploadWithMetadata);
            sb.AppendLine("- " + TipoTrabajoIds.ProjectSync + " (sin formulario guiado en esta herramienta)");
            sb.AppendLine("Credenciales.Tipo = " + CredencialTipoIds.Aconex + " — obligatorios:");
            sb.AppendLine("Nombre, Tipo, Aconex instancia, usuario, clave, Integration Id, Organization Id, User Id.");
            sb.AppendLine();
            sb.AppendLine("Credenciales.Tipo = " + CredencialTipoIds.BD + " — obligatorios:");
            sb.AppendLine("Nombre, Tipo, BD servidor, tipo conexión, usuario, clave, base de datos.");
            await _dialogs.ShowInfoAsync(sb.ToString(), "Referencia rápida");
        }

        /// <summary>Arma el formulario según el tipo del trabajo y rellena valores desde TrabajosConfiguracion si hay conexión.</summary>
        private void ReconstruirFormularioParametrosDesdeBd()
        {
            CamposParametrosTrabajo.Clear();
            FormularioParametrosActivo = false;
            MensajeFormularioParametros = string.Empty;

            if (TrabajoParaParametros == null)
            {
                MensajeFormularioParametros = "Seleccione un trabajo en el desplegable.";
                return;
            }

            if (TrabajoParaParametros.Id <= 0)
            {
                MensajeFormularioParametros = "El trabajo debe estar guardado (con Id). Guárdelo en la pestaña Trabajos.";
                return;
            }

            var tipo = (TrabajoParaParametros.Tipo ?? string.Empty).Trim();
            if (!TrabajoTipoConfigFieldCatalog.TipoSoportaFormularioGuiado(tipo))
            {
                MensajeFormularioParametros = "El tipo «" + tipo + "» no tiene formulario guiado aquí (p. ej. ProjectSync). Los parámetros en SQL no se muestran; puede editarlos directamente en la base de datos.";
                return;
            }

            FormularioParametrosActivo = true;

            var porNombre = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var cs = ResolverCadenaParaServicios();

            var credencialesAconex = new List<CredencialComboItem>();
            var credencialesBd = new List<CredencialComboItem>();
            if (cs != null)
            {
                try
                {
                    var credSvc = new CredencialesEditorService(cs);
                    foreach (var c in credSvc.ListarTodas().OrderBy(x => x.Id))
                    {
                        var tipoC = (c.Tipo ?? string.Empty).Trim();
                        var etiqueta = c.Id + " — " + (c.Nombre ?? string.Empty).Trim();
                        if (string.Equals(tipoC, CredencialTipoIds.Aconex, StringComparison.OrdinalIgnoreCase))
                            credencialesAconex.Add(new CredencialComboItem(c.Id, etiqueta));
                        else if (string.Equals(tipoC, CredencialTipoIds.BD, StringComparison.OrdinalIgnoreCase))
                            credencialesBd.Add(new CredencialComboItem(c.Id, etiqueta));
                    }
                }
                catch (Exception exCred)
                {
                    StatusMessage = "Aviso: no se pudieron cargar credenciales para los desplegables — " + exCred.Message;
                }
            }

            if (cs != null)
            {
                try
                {
                    var svc = new TrabajosConfiguracionEditorService(cs);
                    foreach (var f in svc.ListarPorIdTrabajo(TrabajoParaParametros.Id))
                    {
                        var k = (f.Nombre ?? string.Empty).Trim();
                        if (k.Length > 0)
                            porNombre[k] = f.ValorTexto ?? string.Empty;
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = "Parámetros: no se pudieron leer valores desde la BD — " + ex.Message;
                }
            }

            foreach (var def in TrabajoTipoConfigFieldCatalog.ObtenerCamposParaFormulario(tipo))
            {
                porNombre.TryGetValue(def.Clave, out var v);
                IReadOnlyList<CredencialComboItem> credOps = null;
                if (def.Clave == TrabajosConfiguracionKeyNames.CredencialAconex)
                    credOps = credencialesAconex;
                else if (def.Clave == TrabajosConfiguracionKeyNames.CredencialBD)
                    credOps = credencialesBd;
                CamposParametrosTrabajo.Add(new ParametroTrabajoCampoViewModel(def, tipo, v ?? string.Empty, credOps));
            }
        }

        private string ResolverCadenaParaServicios()
        {
            if (!string.IsNullOrWhiteSpace(EffectiveConnectionString))
                return EffectiveConnectionString;

            var raw = (ConnectionString ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw))
            {
                StatusMessage = "Probar conexión primero o complete la cadena.";
                return null;
            }

            try
            {
                var cs = ConnectionStringHelper.AsegurarTrustServerCertificate(raw);
                using (var cn = new SqlConnection(cs))
                    cn.Open();
                EffectiveConnectionString = cs;
                return cs;
            }
            catch (Exception ex)
            {
                StatusMessage = "Conexión no válida: " + ex.Message;
                return null;
            }
        }
    }
}
