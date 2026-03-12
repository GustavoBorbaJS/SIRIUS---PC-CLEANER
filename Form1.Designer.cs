namespace ProcessCleaner
{
    partial class ProcessCleanerForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.listViewProcesses = new System.Windows.Forms.ListView();
            this.btnRefresh = new System.Windows.Forms.Button();
            this.btnCleanSelected = new System.Windows.Forms.Button();
            this.btnCleanInactive = new System.Windows.Forms.Button();
            this.btnCleanAllNonSystem = new System.Windows.Forms.Button();
            this.btnCleanFiles = new System.Windows.Forms.Button();
            this.btnExportLog = new System.Windows.Forms.Button();
            this.btnLicense = new System.Windows.Forms.Button();
            this.cbAutoCleanup = new System.Windows.Forms.CheckBox();
            this.cbAutoCleanFiles = new System.Windows.Forms.CheckBox();
            this.txtLog = new System.Windows.Forms.TextBox();
            this.lblAvailableMemory = new System.Windows.Forms.Label();
            this.lblUsedMemory = new System.Windows.Forms.Label();
            this.lblCleanupSummary = new System.Windows.Forms.Label();
            this.lblLicenseStatus = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // listViewProcesses
            // 
            this.listViewProcesses.Location = new System.Drawing.Point(12, 12);
            this.listViewProcesses.Name = "listViewProcesses";
            this.listViewProcesses.Size = new System.Drawing.Size(870, 400); // Reduzido para dar espaço ao gráfico
            this.listViewProcesses.TabIndex = 0;
            this.listViewProcesses.UseCompatibleStateImageBehavior = false;
            // 
            // btnRefresh
            // 
            this.btnRefresh.Location = new System.Drawing.Point(12, 420);
            this.btnRefresh.Name = "btnRefresh";
            this.btnRefresh.Size = new System.Drawing.Size(100, 35);
            this.btnRefresh.TabIndex = 1;
            this.btnRefresh.Text = "🔄 Atualizar";
            this.btnRefresh.UseVisualStyleBackColor = true;
            this.btnRefresh.Click += new System.EventHandler(this.BtnRefresh_Click);
            // 
            // btnCleanSelected
            // 
            this.btnCleanSelected.Location = new System.Drawing.Point(120, 420);
            this.btnCleanSelected.Name = "btnCleanSelected";
            this.btnCleanSelected.Size = new System.Drawing.Size(140, 35);
            this.btnCleanSelected.TabIndex = 2;
            this.btnCleanSelected.Text = "🧹 Limpar Selecionados";
            this.btnCleanSelected.UseVisualStyleBackColor = true;
            this.btnCleanSelected.Click += new System.EventHandler(this.BtnCleanSelected_Click);
            // 
            // btnCleanInactive
            // 
            this.btnCleanInactive.Location = new System.Drawing.Point(270, 420);
            this.btnCleanInactive.Name = "btnCleanInactive";
            this.btnCleanInactive.Size = new System.Drawing.Size(140, 35);
            this.btnCleanInactive.TabIndex = 3;
            this.btnCleanInactive.Text = "😴 Limpar Inativos";
            this.btnCleanInactive.UseVisualStyleBackColor = true;
            this.btnCleanInactive.Click += new System.EventHandler(this.BtnCleanInactive_Click);
            // 
            // btnCleanAllNonSystem
            // 
            this.btnCleanAllNonSystem.Location = new System.Drawing.Point(420, 420);
            this.btnCleanAllNonSystem.Name = "btnCleanAllNonSystem";
            this.btnCleanAllNonSystem.Size = new System.Drawing.Size(160, 35);
            this.btnCleanAllNonSystem.TabIndex = 4;
            this.btnCleanAllNonSystem.Text = "⚠️ Limpar Não-Sistema";
            this.btnCleanAllNonSystem.UseVisualStyleBackColor = true;
            this.btnCleanAllNonSystem.Click += new System.EventHandler(this.BtnCleanAllNonSystem_Click);
            // 
            // btnCleanFiles
            // 
            this.btnCleanFiles.Location = new System.Drawing.Point(590, 420);
            this.btnCleanFiles.Name = "btnCleanFiles";
            this.btnCleanFiles.Size = new System.Drawing.Size(140, 35);
            this.btnCleanFiles.TabIndex = 5;
            this.btnCleanFiles.Text = "🧹 Limpar Arquivos";
            this.btnCleanFiles.UseVisualStyleBackColor = true;
            this.btnCleanFiles.Click += new System.EventHandler(this.BtnCleanFiles_Click);
            // 
            // btnExportLog
            // 
            this.btnExportLog.Location = new System.Drawing.Point(740, 420);
            this.btnExportLog.Name = "btnExportLog";
            this.btnExportLog.Size = new System.Drawing.Size(120, 35);
            this.btnExportLog.TabIndex = 6;
            this.btnExportLog.Text = "📋 Exportar Log";
            this.btnExportLog.UseVisualStyleBackColor = true;
            this.btnExportLog.Click += new System.EventHandler(this.BtnExportLog_Click);
            // 
            // btnLicense
            // 
            this.btnLicense.Location = new System.Drawing.Point(870, 420);
            this.btnLicense.Name = "btnLicense";
            this.btnLicense.Size = new System.Drawing.Size(100, 35);
            this.btnLicense.TabIndex = 7;
            this.btnLicense.Text = "🔑 Licença";
            this.btnLicense.UseVisualStyleBackColor = true;
            this.btnLicense.Click += new System.EventHandler(this.BtnLicense_Click);
            // 
            // cbAutoCleanup
            // 
            this.cbAutoCleanup.AutoSize = true;
            this.cbAutoCleanup.Location = new System.Drawing.Point(12, 470);
            this.cbAutoCleanup.Name = "cbAutoCleanup";
            this.cbAutoCleanup.Size = new System.Drawing.Size(159, 19);
            this.cbAutoCleanup.TabIndex = 8;
            this.cbAutoCleanup.Text = "Auto-limpeza de processos";
            this.cbAutoCleanup.UseVisualStyleBackColor = true;
            // 
            // cbAutoCleanFiles
            // 
            this.cbAutoCleanFiles.AutoSize = true;
            this.cbAutoCleanFiles.Location = new System.Drawing.Point(12, 495);
            this.cbAutoCleanFiles.Name = "cbAutoCleanFiles";
            this.cbAutoCleanFiles.Size = new System.Drawing.Size(150, 19);
            this.cbAutoCleanFiles.TabIndex = 9;
            this.cbAutoCleanFiles.Text = "Auto-limpeza de arquivos";
            this.cbAutoCleanFiles.UseVisualStyleBackColor = true;
            // 
            // txtLog
            // 
            this.txtLog.Location = new System.Drawing.Point(12, 530);
            this.txtLog.Multiline = true;
            this.txtLog.Name = "txtLog";
            this.txtLog.ReadOnly = true;
            this.txtLog.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtLog.Size = new System.Drawing.Size(1060, 120);
            this.txtLog.TabIndex = 10;
            // 
            // lblAvailableMemory
            // 
            this.lblAvailableMemory.AutoSize = true;
            this.lblAvailableMemory.Location = new System.Drawing.Point(200, 470);
            this.lblAvailableMemory.Name = "lblAvailableMemory";
            this.lblAvailableMemory.Size = new System.Drawing.Size(127, 15);
            this.lblAvailableMemory.TabIndex = 11;
            this.lblAvailableMemory.Text = "Memória Disponível: --";
            // 
            // lblUsedMemory
            // 
            this.lblUsedMemory.AutoSize = true;
            this.lblUsedMemory.Location = new System.Drawing.Point(200, 495);
            this.lblUsedMemory.Name = "lblUsedMemory";
            this.lblUsedMemory.Size = new System.Drawing.Size(102, 15);
            this.lblUsedMemory.TabIndex = 12;
            this.lblUsedMemory.Text = "Memória em Uso: --";
            // 
            // lblCleanupSummary
            // 
            this.lblCleanupSummary.AutoSize = true;
            this.lblCleanupSummary.Location = new System.Drawing.Point(420, 480);
            this.lblCleanupSummary.Name = "lblCleanupSummary";
            this.lblCleanupSummary.Size = new System.Drawing.Size(42, 15);
            this.lblCleanupSummary.TabIndex = 13;
            this.lblCleanupSummary.Text = "Pronto";
            // 
            // lblLicenseStatus
            // 
            this.lblLicenseStatus.AutoSize = true;
            this.lblLicenseStatus.Location = new System.Drawing.Point(750, 480);
            this.lblLicenseStatus.Name = "lblLicenseStatus";
            this.lblLicenseStatus.Size = new System.Drawing.Size(58, 15);
            this.lblLicenseStatus.TabIndex = 14;
            this.lblLicenseStatus.Text = "Licenciado";
            this.lblLicenseStatus.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // ProcessCleanerForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1184, 661); // Aumentado para acomodar gráfico
            this.Controls.Add(this.lblLicenseStatus);
            this.Controls.Add(this.lblCleanupSummary);
            this.Controls.Add(this.lblUsedMemory);
            this.Controls.Add(this.lblAvailableMemory);
            this.Controls.Add(this.txtLog);
            this.Controls.Add(this.cbAutoCleanFiles);
            this.Controls.Add(this.cbAutoCleanup);
            this.Controls.Add(this.btnLicense);
            this.Controls.Add(this.btnExportLog);
            this.Controls.Add(this.btnCleanFiles);
            this.Controls.Add(this.btnCleanAllNonSystem);
            this.Controls.Add(this.btnCleanInactive);
            this.Controls.Add(this.btnCleanSelected);
            this.Controls.Add(this.btnRefresh);
            this.Controls.Add(this.listViewProcesses);
            this.Name = "ProcessCleanerForm";
            this.Text = "Sirus Cleaner";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.ListView listViewProcesses;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.Button btnCleanSelected;
        private System.Windows.Forms.Button btnCleanInactive;
        private System.Windows.Forms.Button btnCleanAllNonSystem;
        private System.Windows.Forms.Button btnCleanFiles;
        private System.Windows.Forms.Button btnExportLog;
        private System.Windows.Forms.Button btnLicense;
        private System.Windows.Forms.CheckBox cbAutoCleanup;
        private System.Windows.Forms.CheckBox cbAutoCleanFiles;
        private System.Windows.Forms.TextBox txtLog;
        private System.Windows.Forms.Label lblAvailableMemory;
        private System.Windows.Forms.Label lblUsedMemory;
        private System.Windows.Forms.Label lblCleanupSummary;
        private System.Windows.Forms.Label lblLicenseStatus;
    }
}