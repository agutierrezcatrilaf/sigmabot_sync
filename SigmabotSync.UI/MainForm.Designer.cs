namespace SigmabotDataSync
{
    partial class MainForm
    {
        /// <summary>
        /// Variable del diseñador necesaria.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Limpiar los recursos que se estén usando.
        /// </summary>
        /// <param name="disposing">true si los recursos administrados se deben desechar; false en caso contrario.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Código generado por el Diseñador de Windows Forms

        /// <summary>
        /// Método necesario para admitir el Diseñador. No se puede modificar
        /// el contenido de este método con el editor de código.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.LblOriginProject = new System.Windows.Forms.Label();
            this.cmbOriginProject = new System.Windows.Forms.ComboBox();
            this.btnSync = new System.Windows.Forms.Button();
            this.lblDestinationProject = new System.Windows.Forms.Label();
            this.cmbDestinationProject = new System.Windows.Forms.ComboBox();
            this.PictureBox1 = new System.Windows.Forms.PictureBox();
            this.label3 = new System.Windows.Forms.Label();
            this.btnConfig = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.PictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(189, 105);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(0, 23);
            this.label1.TabIndex = 1;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(126, 339);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(0, 23);
            this.label2.TabIndex = 2;
            // 
            // LblOriginProject
            // 
            this.LblOriginProject.BackColor = System.Drawing.Color.Tomato;
            this.LblOriginProject.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.LblOriginProject.ForeColor = System.Drawing.Color.White;
            this.LblOriginProject.Location = new System.Drawing.Point(43, 92);
            this.LblOriginProject.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.LblOriginProject.Name = "LblOriginProject";
            this.LblOriginProject.Size = new System.Drawing.Size(235, 31);
            this.LblOriginProject.TabIndex = 31;
            this.LblOriginProject.Text = "Proyecto Origen:";
            this.LblOriginProject.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // cmbOriginProject
            // 
            this.cmbOriginProject.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbOriginProject.FormattingEnabled = true;
            this.cmbOriginProject.ItemHeight = 23;
            this.cmbOriginProject.Location = new System.Drawing.Point(277, 92);
            this.cmbOriginProject.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.cmbOriginProject.Name = "cmbOriginProject";
            this.cmbOriginProject.Size = new System.Drawing.Size(324, 31);
            this.cmbOriginProject.TabIndex = 32;
            // 
            // btnSync
            // 
            this.btnSync.Location = new System.Drawing.Point(42, 278);
            this.btnSync.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.btnSync.Name = "btnSync";
            this.btnSync.Size = new System.Drawing.Size(273, 84);
            this.btnSync.TabIndex = 33;
            this.btnSync.Text = "Sincronizar";
            this.btnSync.UseVisualStyleBackColor = true;
            this.btnSync.Click += new System.EventHandler(this.btnSync_Click);
            // 
            // lblDestinationProject
            // 
            this.lblDestinationProject.BackColor = System.Drawing.Color.Tomato;
            this.lblDestinationProject.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblDestinationProject.ForeColor = System.Drawing.Color.White;
            this.lblDestinationProject.Location = new System.Drawing.Point(43, 178);
            this.lblDestinationProject.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblDestinationProject.Name = "lblDestinationProject";
            this.lblDestinationProject.Size = new System.Drawing.Size(235, 31);
            this.lblDestinationProject.TabIndex = 34;
            this.lblDestinationProject.Text = "Proyecto Destino:";
            this.lblDestinationProject.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // cmbDestinationProject
            // 
            this.cmbDestinationProject.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbDestinationProject.FormattingEnabled = true;
            this.cmbDestinationProject.ItemHeight = 23;
            this.cmbDestinationProject.Location = new System.Drawing.Point(277, 178);
            this.cmbDestinationProject.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.cmbDestinationProject.Name = "cmbDestinationProject";
            this.cmbDestinationProject.Size = new System.Drawing.Size(324, 31);
            this.cmbDestinationProject.TabIndex = 35;
            // 
            // PictureBox1
            // 
            this.PictureBox1.Image = ((System.Drawing.Image)(resources.GetObject("PictureBox1.Image")));
            this.PictureBox1.Location = new System.Drawing.Point(650, 141);
            this.PictureBox1.Margin = new System.Windows.Forms.Padding(4);
            this.PictureBox1.Name = "PictureBox1";
            this.PictureBox1.Size = new System.Drawing.Size(227, 221);
            this.PictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.PictureBox1.TabIndex = 72;
            this.PictureBox1.TabStop = false;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(128)))), ((int)(((byte)(0)))));
            this.label3.Location = new System.Drawing.Point(665, 91);
            this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(166, 36);
            this.label3.TabIndex = 71;
            this.label3.Text = "SigmaBOT";
            // 
            // btnConfig
            // 
            this.btnConfig.Location = new System.Drawing.Point(321, 278);
            this.btnConfig.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.btnConfig.Name = "btnConfig";
            this.btnConfig.Size = new System.Drawing.Size(280, 84);
            this.btnConfig.TabIndex = 73;
            this.btnConfig.Text = "Configuración";
            this.btnConfig.UseVisualStyleBackColor = true;
            this.btnConfig.Click += new System.EventHandler(this.btnConfig_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 23F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(900, 647);
            this.Controls.Add(this.btnConfig);
            this.Controls.Add(this.PictureBox1);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.cmbDestinationProject);
            this.Controls.Add(this.lblDestinationProject);
            this.Controls.Add(this.btnSync);
            this.Controls.Add(this.cmbOriginProject);
            this.Controls.Add(this.LblOriginProject);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Font = new System.Drawing.Font("Segoe UI", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
            this.Name = "MainForm";
            this.Text = "SigmaBot Sync";
            this.Load += new System.EventHandler(this.MainForm_Load);
            ((System.ComponentModel.ISupportInitialize)(this.PictureBox1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        internal System.Windows.Forms.Label LblOriginProject;
        private System.Windows.Forms.ComboBox cmbOriginProject;
        private System.Windows.Forms.Button btnSync;
        internal System.Windows.Forms.Label lblDestinationProject;
        private System.Windows.Forms.ComboBox cmbDestinationProject;
        internal System.Windows.Forms.PictureBox PictureBox1;
        internal System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button btnConfig;
    }
}

