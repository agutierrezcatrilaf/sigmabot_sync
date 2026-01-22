using SigmabotSync.Application.Services;
using SigmabotSync.Application.Synchronization;
using SigmabotSync.Domain.Entities;
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
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

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
                cmbOriginProject.DataSource = new List<Project>(projects);
                cmbOriginProject.DisplayMember = "Description";
                cmbOriginProject.ValueMember = "Id";

                cmbDestinationProject.DataSource = new List<Project>(projects);
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

        private async void btnSync_Click(object sender, EventArgs e)
        {
            btnSync.Enabled = false;
            progressBarSync.Value = 0;
            lblStatusSync.Text = "Iniciando...";

            var cfg = _settingsService.Load();
            var client = new AconexDocumentClient(
                cfg.UserAconex,
                cfg.PassAconex,
                cfg.IntegrationIdAconex);

            var docService = new DocumentService(client);

            string projectId = cmbOriginProject.SelectedValue.ToString();
            DateTime since = new DateTime(2025, 11, 25, 18, 0, 0);

            // Crear el worker
            var worker = new DocumentSyncWorker(docService);

            worker.OnProgress += UpdateProgress;
            worker.OnStatus += UpdateStatus;

            // Ejecutar
            //await worker.RunAsync(projectId, since);
            await Task.Run(() => worker.RunAsync(projectId, since));

            btnSync.Enabled = true;
        }


        private void btnConfig_Click(object sender, EventArgs e)
        {
           var configForm = new ConfigForm();
            configForm.ShowDialog();
        }

        private void UpdateStatus(string msg)
        {
            if (InvokeRequired)
                Invoke(new Action(() => lblStatusSync.Text = msg));
            else
                lblStatusSync.Text = msg;
        }

        private void UpdateProgress(int current, int total)
        {
            if (InvokeRequired)
                Invoke(new Action(() =>
                {
                    progressBarSync.Maximum = total;
                    progressBarSync.Value = current;
                }));
            else
            {
                progressBarSync.Maximum = total;
                progressBarSync.Value = current;
            }
        }

    }
}
