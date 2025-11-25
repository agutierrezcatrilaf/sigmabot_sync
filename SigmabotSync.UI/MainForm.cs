using SigmabotSync.Application.Services;
using SigmabotSync.Domain.Interfaces;
using SigmabotSync.Infrastructure.External;
using SigmabotSync.Infrastructure.Services;
using SigmabotSync.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SigmabotDataSync
{
    public partial class MainForm : Form
    {
        private readonly ProjectService _projectService;
        private readonly SettingsService _settingsService;

        public MainForm()
        {
            InitializeComponent();
            _settingsService = new SettingsService();
            _projectService = new ProjectService(_settingsService);

        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            try
            {
                var projects = await _projectService.GetProjectsAsync();
                cmbOriginProject.DataSource = projects;
                cmbOriginProject.DisplayMember = "Description";
                cmbOriginProject.ValueMember = "Id";

                cmbDestinationProject.DataSource = projects;
                cmbDestinationProject.DisplayMember = "Description";
                cmbDestinationProject.ValueMember = "Id";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error cargando proyectos: " + ex.Message);
            }

            var cfg = _settingsService.Load();

            bool hasCredentials =
                !string.IsNullOrWhiteSpace(cfg.UserAconex) &&
                !string.IsNullOrWhiteSpace(cfg.PassAconex) &&
                !string.IsNullOrWhiteSpace(cfg.IntegrationIdAconex);

            btnSync.Enabled = hasCredentials;
        }

        private void btnSync_Click(object sender, EventArgs e)
        {
            int origenId = (int)cmbOriginProject.SelectedValue;
            int destinoId = (int)cmbDestinationProject.SelectedValue;

            MessageBox.Show($"Origen ID: {origenId} | Destino ID: {destinoId}");

        }

        private void btnConfig_Click(object sender, EventArgs e)
        {
           var configForm = new ConfigForm();
            configForm.ShowDialog();
        }
    }
}
