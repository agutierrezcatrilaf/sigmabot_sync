using SigmabotSync.Domain.Config;
using SigmabotSync.Infrastructure.External;
using SigmabotSync.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SigmabotSync.UI
{
    public partial class ConfigForm : Form
    {
        private readonly SettingsService _settingsService;

        public ConfigForm()
        {
            InitializeComponent();
            _settingsService = new SettingsService();
        }

        private void ConfigForm_Load(object sender, EventArgs e)
        {
            var cfg = _settingsService.Load();
            txtUserAconex.Text = cfg.UserAconex;
            txtPassAconex.Text = cfg.PassAconex;
            txtIntegrationIdAconex.Text = cfg.IntegrationIdAconex;
        }

        private async void btnSaveAconex_Click(object sender, EventArgs e)
        {
            string user = txtUserAconex.Text.Trim();
            string pass = txtPassAconex.Text.Trim();
            string integration = txtIntegrationIdAconex.Text.Trim();

            if (string.IsNullOrWhiteSpace(user) ||
                string.IsNullOrWhiteSpace(pass) ||
                string.IsNullOrWhiteSpace(integration))
            {
                MessageBox.Show("Debe ingresar Usuario, Contraseña y Application Key.");
                return;
            }

            // Crear cliente Aconex para validar
            var client = new AconexUserClient(user, pass, integration);

            try
            {

                bool ok = await client.ValidateConnectionAsync();

                if (!ok)
                {
                    MessageBox.Show(
                        "La conexión con Aconex falló.\nVerifique usuario, contraseña y Application Key.",
                        "Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                    return;
                }

                // Si la conexión es correcta → guardar configuración
                var cfg = new AconexSettings
                {
                    UserAconex = user,
                    PassAconex = pass,
                    IntegrationIdAconex = integration
                };

                _settingsService.Save(cfg);

                MessageBox.Show(
                    "Conexión validada correctamente.\nLa configuración fue guardada.",
                    "OK",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                this.DialogResult = DialogResult.OK; // opcional
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Ocurrió un error al validar la conexión:\n" + ex.Message,
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void btnCancelConfig_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
