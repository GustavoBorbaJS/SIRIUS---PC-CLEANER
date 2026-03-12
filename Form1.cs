using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ProcessCleaner
{
    public partial class ProcessCleanerForm : Form
    {
        // Dados
        private List<ProcessInfo> processesList = new List<ProcessInfo>();
        private PerformanceCounter memoryCounter;
        private System.Windows.Forms.Timer cleanupTimer;
        private System.Windows.Forms.Timer refreshTimer;
        private System.Windows.Forms.Timer statusTimer;
        private MemoryGraph memoryGraph; // Novo gráfico

        // Tray Icon
        private NotifyIcon notifyIcon;
        private ContextMenuStrip trayMenu;
        private ToolStripMenuItem menuItemOpen;
        private ToolStripMenuItem menuItemExit;
        private ToolStripMenuItem menuItemLicense;
        private ToolStripMenuItem menuItemStatus;

        // Processos protegidos
        private static readonly HashSet<string> ProtectedProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "explorer.exe", "csrss.exe", "winlogon.exe", "services.exe", "lsass.exe",
            "smss.exe", "dwm.exe", "taskhostw.exe", "spoolsv.exe", "System",
            "System Idle Process", "svchost.exe", "wininit.exe", "fontdrvhost.exe", 
            "SecurityHealthService.exe"
        };

        public ProcessCleanerForm()
        {
            // Verifica licença
            string licenseMsg;
            bool isTrial;
            int daysLeft;
            DateTime expiryDate;
            
            if (!AppMonitor.ValidateLicense(out licenseMsg, out isTrial, out daysLeft, out expiryDate))
            {
                // Trial expirado - pede senha master para renovar
                var result = MessageBox.Show(
                    $"{licenseMsg}\n\nDeseja inserir a senha master para renovar por mais 7 dias?",
                    "Sirus Cleaner - Trial Expirado",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    if (!ShowMasterPasswordDialog())
                    {
                        MessageBox.Show("Renovação cancelada. O programa será encerrado.", 
                            "Sirus Cleaner", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        Environment.Exit(0);
                    }
                }
                else
                {
                    Environment.Exit(0);
                }
            }
            else if (isTrial)
            {
                // Mostra dias restantes do trial
                if (daysLeft <= 1)
                {
                    MessageBox.Show($"⚠️ {licenseMsg}\n\nLembre-se de renovar com a senha master!", 
                        "Sirus Cleaner - Atenção", 
                        MessageBoxButtons.OK, 
                        MessageBoxIcon.Warning);
                }
            }

            InitializeComponent();
            
            // Configurar colunas do ListView
            ConfigureListViewColumns();
            
            // Adicionar gráfico de memória
            AddMemoryGraph();
            
            InitializePerformanceCounter();
            InitializeTimers();
            InitializeTrayIcon();
            
            this.Text = isTrial ? "Sirus Cleaner [MODO TRIAL]" : "Sirus Cleaner [PROFISSIONAL]";
            if (lblLicenseStatus != null)
                lblLicenseStatus.Text = AppMonitor.GetLicenseStatus();

            _ = LoadProcessesAsync();
            AppMonitor.Log("Aplicação iniciada");
        }

        private void AddMemoryGraph()
        {
            memoryGraph = new MemoryGraph
            {
                Location = new Point(890, 12),
                Size = new Size(280, 200)
            };
            this.Controls.Add(memoryGraph);
        }

        private bool ShowMasterPasswordDialog()
        {
            using (var form = new Form())
            {
                form.Text = "Renovar Trial - Senha Master";
                form.Size = new Size(400, 200);
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.BackColor = AppStyles.BackgroundColor;

                var lbl = new Label
                {
                    Text = "Digite a senha master para renovar por mais 7 dias:",
                    Location = new Point(20, 30),
                    Size = new Size(360, 40),
                    Font = AppStyles.NormalFont
                };

                var txt = new TextBox
                {
                    Location = new Point(20, 70),
                    Size = new Size(360, 25),
                    Font = AppStyles.NormalFont,
                    PasswordChar = '●',
                    UseSystemPasswordChar = true
                };

                var btnRenew = new Button
                {
                    Text = "Renovar Trial",
                    Location = new Point(100, 110),
                    Size = new Size(120, 30),
                    DialogResult = DialogResult.OK
                };
                AppStyles.ApplyModernButton(btnRenew, AppStyles.SecondaryColor);

                var btnCancel = new Button
                {
                    Text = "Cancelar",
                    Location = new Point(230, 110),
                    Size = new Size(100, 30),
                    DialogResult = DialogResult.Cancel
                };
                AppStyles.ApplyModernButton(btnCancel, AppStyles.AccentColor);

                form.Controls.AddRange(new Control[] { lbl, txt, btnRenew, btnCancel });
                form.AcceptButton = btnRenew;
                form.CancelButton = btnCancel;

                while (true)
                {
                    if (form.ShowDialog() == DialogResult.OK)
                    {
                        if (AppMonitor.RenewTrialWithMasterPassword(txt.Text))
                        {
                            MessageBox.Show("✅ Trial renovado por mais 7 dias!", 
                                "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return true;
                        }
                        else
                        {
                            MessageBox.Show("❌ Senha master incorreta! Tente novamente.", 
                                "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            txt.Clear();
                            txt.Focus();
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
            }
        }

        private void ConfigureListViewColumns()
        {
            listViewProcesses.View = View.Details;
            listViewProcesses.FullRowSelect = true;
            listViewProcesses.GridLines = true;
            listViewProcesses.Font = AppStyles.NormalFont;
            
            listViewProcesses.Columns.Clear();
            listViewProcesses.Columns.Add("Processo", 200);
            listViewProcesses.Columns.Add("PID", 70);
            listViewProcesses.Columns.Add("Memória (MB)", 100);
            listViewProcesses.Columns.Add("CPU %", 80);
            listViewProcesses.Columns.Add("Prioridade", 80);
            listViewProcesses.Columns.Add("Caminho", 300);
        }

        private void InitializePerformanceCounter()
        {
            try
            {
                memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            }
            catch (Exception ex)
            {
                LogMessage($"Erro ao iniciar contador: {ex.Message}");
            }
        }

        private void InitializeTimers()
        {
            // Timer para limpeza automática (5 minutos)
            cleanupTimer = new System.Windows.Forms.Timer { Interval = 300000 };
            cleanupTimer.Tick += CleanupTimer_Tick;
            cleanupTimer.Start();

            // Timer para atualização de processos (5 segundos)
            refreshTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            refreshTimer.Tick += async (s, e) => await LoadProcessesAsync();
            refreshTimer.Start();

            // Timer para status (1 segundo)
            statusTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            statusTimer.Tick += StatusTimer_Tick;
            statusTimer.Start();
        }

        private void InitializeTrayIcon()
        {
            trayMenu = new ContextMenuStrip();
            menuItemOpen = new ToolStripMenuItem("Abrir Sirus");
            menuItemStatus = new ToolStripMenuItem("Status: Ativo");
            menuItemLicense = new ToolStripMenuItem("Licença");
            menuItemExit = new ToolStripMenuItem("Sair");

            menuItemOpen.Click += (s, e) => ShowFromTray();
            menuItemLicense.Click += (s, e) => ShowLicenseDialog();
            menuItemExit.Click += (s, e) => Application.Exit();

            menuItemStatus.Enabled = false;
            menuItemStatus.Font = new Font(menuItemStatus.Font, FontStyle.Bold);

            trayMenu.Items.AddRange(new ToolStripItem[] { 
                menuItemOpen, menuItemStatus, menuItemLicense, menuItemExit 
            });

            notifyIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                ContextMenuStrip = trayMenu,
                Text = "Sirus Cleaner",
                Visible = true
            };
            notifyIcon.DoubleClick += (s, e) => ShowFromTray();
        }

        private void ShowFromTray()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.Hide();
                notifyIcon.ShowBalloonTip(1000, "Sirus Cleaner", 
                    "Rodando em segundo plano.", ToolTipIcon.Info);
            }
        }

        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (memoryCounter != null)
                {
                    float availableMemory = memoryCounter.NextValue();
                    lblAvailableMemory.Text = $"💾 Memória Disponível: {availableMemory:0} MB";
                }

                if (processesList.Any())
                {
                    var totalUsedMemory = processesList.Sum(p => p.MemoryUsage);
                    lblUsedMemory.Text = $"📊 Memória em Uso: {totalUsedMemory:0} MB";
                }

                menuItemStatus.Text = $"Status: Monitorando ({processesList.Count} processos)";
            }
            catch { }
        }

        private void CleanupTimer_Tick(object sender, EventArgs e)
        {
            if (cbAutoCleanup != null && cbAutoCleanup.Checked)
            {
                _ = Task.Run(() => CleanInactiveProcesses());
            }

            if (cbAutoCleanFiles != null && cbAutoCleanFiles.Checked && IsComputerIdle())
            {
                _ = Task.Run(() => AutoCleanFiles());
            }
        }

        private bool IsComputerIdle()
        {
            var lastInput = GetLastInputTime();
            return lastInput.TotalSeconds > 120;
        }

        private TimeSpan GetLastInputTime()
        {
            var lastInputInfo = new LASTINPUTINFO();
            lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);
            
            if (GetLastInputInfo(ref lastInputInfo))
            {
                return TimeSpan.FromMilliseconds(Environment.TickCount - lastInputInfo.dwTime);
            }
            return TimeSpan.Zero;
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        private async Task LoadProcessesAsync()
        {
            try
            {
                var loadedProcesses = await PerformanceOptimizer.GetProcessesAsync();
                processesList = loadedProcesses;

                if (listViewProcesses.IsHandleCreated && !listViewProcesses.IsDisposed)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        listViewProcesses.BeginUpdate();
                        listViewProcesses.Items.Clear();

                        foreach (var processInfo in processesList)
                        {
                            var item = new ListViewItem(processInfo.Name);
                            item.SubItems.Add(processInfo.Id.ToString());
                            item.SubItems.Add(processInfo.MemoryUsage.ToString());
                            item.SubItems.Add($"{processInfo.CPUUsage:0.0}");
                            item.SubItems.Add(GetPriorityText(processInfo.Priority));
                            item.SubItems.Add(Path.GetFileName(processInfo.FilePath));
                            item.Tag = processInfo;

                            // Color coding
                            if (processInfo.MemoryUsage > 100)
                                item.BackColor = Color.LightCoral;
                            else if (processInfo.CPUUsage > 30)
                                item.BackColor = Color.LightYellow;

                            listViewProcesses.Items.Add(item);
                        }

                        listViewProcesses.EndUpdate();
                        
                        if (processesList.Count == 0)
                        {
                            LogMessage("Nenhum processo encontrado ou todos são do sistema.");
                        }
                        else
                        {
                            LogMessage($"✅ {processesList.Count} processos carregados");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Erro ao carregar processos: {ex.Message}");
            }
        }

        private string GetPriorityText(int priority)
        {
            switch (priority)
            {
                case 4: return "Alta";
                case 8: return "Normal";
                case 13: return "Baixa";
                default: return priority.ToString();
            }
        }

        private bool IsProtectedProcess(ProcessInfo processInfo)
        {
            if (processInfo?.Process == null) return true;

            string name = processInfo.Name.ToLower();
            if (!name.EndsWith(".exe")) name += ".exe";

            return ProtectedProcesses.Contains(name) || 
                   processInfo.SessionId == 0 ||
                   name.Contains("system");
        }

        private void CleanInactiveProcesses()
        {
            var processesToClean = processesList
                .Where(p => p.CPUUsage < 0.5 && p.MemoryUsage < 20 && !IsProtectedProcess(p))
                .ToList();

            int cleaned = 0;
            foreach (var processInfo in processesToClean)
            {
                try
                {
                    if (processInfo.Process?.HasExited == false)
                    {
                        processInfo.Process.Kill();
                        cleaned++;
                        LogMessage($"🧹 Processo inativo limpo: {processInfo.Name} (ID: {processInfo.Id})");
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"❌ Erro ao limpar {processInfo.Name}: {ex.Message}");
                }
            }

            if (cleaned > 0)
            {
                this.Invoke(() => lblCleanupSummary.Text = $"✅ {cleaned} processos inativos limpos");
                _ = LoadProcessesAsync();
            }
        }

        private async Task AutoCleanFiles()
        {
            var result = await PerformanceOptimizer.CleanupSystemFilesAsync();
            if (result.DeletedFiles > 0)
            {
                this.Invoke(() =>
                {
                    lblCleanupSummary.Text = $"✨ {result.Summary}";
                    LogMessage($"🧹 Limpeza automática: {result.Summary}");
                });
            }
        }

        private async void BtnCleanFiles_Click(object sender, EventArgs e)
        {
            btnCleanFiles.Enabled = false;
            btnCleanFiles.Text = "Limpando...";

            try
            {
                var result = await PerformanceOptimizer.CleanupSystemFilesAsync();
                lblCleanupSummary.Text = $"✅ {result.Summary}";
                LogMessage($"🧹 Limpeza manual: {result.Summary}");

                if (result.FreedSpace > 500 * 1024 * 1024)
                {
                    if (MessageBox.Show("Limpeza concluída! Reiniciar agora?", 
                        "Sirus Cleaner", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        Process.Start("shutdown", "/r /t 5");
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Erro: {ex.Message}");
            }
            finally
            {
                btnCleanFiles.Enabled = true;
                btnCleanFiles.Text = "🧹 Limpar Arquivos";
            }
        }

       private void ShowLicenseDialog()
{
    using (var form = new Form())
    {
        form.Text = "Gerenciar Licença";
        form.Size = new Size(450, 400);
        form.StartPosition = FormStartPosition.CenterParent;
        form.BackColor = AppStyles.BackgroundColor;

        string licenseMsg;
        bool isTrial;
        int daysLeft;
        DateTime expiryDate;
        AppMonitor.ValidateLicense(out licenseMsg, out isTrial, out daysLeft, out expiryDate);

        var lblStatus = new Label
        {
            Text = AppMonitor.GetLicenseStatus(),
            Location = new Point(20, 20),
            Size = new Size(400, 30),
            Font = AppStyles.BoldFont,
            ForeColor = isTrial && daysLeft <= 1 ? Color.Red : AppStyles.PrimaryColor
        };

        var lblExpiry = new Label
        {
            Text = isTrial ? $"Expira em: {expiryDate:dd/MM/yyyy} (restam {daysLeft} dias)" : "Licença perpétua",
            Location = new Point(20, 50),
            Size = new Size(400, 20),
            Font = AppStyles.NormalFont
        };

        // Seção de renovação do trial
        var lblRenew = new Label
        {
            Text = "RENOVAR TRIAL (7 DIAS)",
            Location = new Point(20, 90),
            Size = new Size(400, 20),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = AppStyles.SecondaryColor
        };

        var lblRenewInfo = new Label
        {
            Text = "Senha: An@lystadesystem",
            Location = new Point(20, 110),
            Size = new Size(400, 20),
            Font = new Font("Segoe UI", 8, FontStyle.Italic),
            ForeColor = Color.Gray
        };

        var txtRenew = new TextBox
        {
            Location = new Point(20, 135),
            Size = new Size(280, 25),
            Font = AppStyles.NormalFont,
            PasswordChar = '●',
            UseSystemPasswordChar = true
        };

        var btnRenew = new Button
        {
            Text = "Renovar Trial",
            Location = new Point(310, 134),
            Size = new Size(100, 28)
        };
        AppStyles.ApplyModernButton(btnRenew, AppStyles.SecondaryColor);
        btnRenew.Click += (s, e) =>
        {
            if (AppMonitor.RenewTrialWithMasterPassword(txtRenew.Text))
            {
                MessageBox.Show("✅ Trial renovado por mais 7 dias!", "Sucesso", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                lblLicenseStatus.Text = AppMonitor.GetLicenseStatus();
                this.Text = "Sirus Cleaner [MODO TRIAL]";
                form.Close();
            }
            else
            {
                MessageBox.Show("❌ Senha incorreta!", "Erro", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        // Seção de ativação permanente
        var lblPermanent = new Label
        {
            Text = "ATIVAR LICENÇA PERMANENTE",
            Location = new Point(20, 180),
            Size = new Size(400, 20),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = AppStyles.PrimaryColor
        };

        var lblPermanentInfo = new Label
        {
            Text = "Senha Master: Ayanokoji2026@",
            Location = new Point(20, 200),
            Size = new Size(400, 20),
            Font = new Font("Segoe UI", 8, FontStyle.Italic),
            ForeColor = Color.Gray
        };

        var txtPermanent = new TextBox
        {
            Location = new Point(20, 225),
            Size = new Size(280, 25),
            Font = AppStyles.NormalFont,
            PasswordChar = '●',
            UseSystemPasswordChar = true
        };

        var btnPermanent = new Button
        {
            Text = "Ativar Permanente",
            Location = new Point(310, 224),
            Size = new Size(100, 28)
        };
        AppStyles.ApplyModernButton(btnPermanent, AppStyles.PrimaryColor);
        btnPermanent.Click += (s, e) =>
        {
            if (AppMonitor.RenewTrialWithMasterPassword(txtPermanent.Text))
            {
                MessageBox.Show("💎 Licença permanente ativada com sucesso!", "Sucesso", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                lblLicenseStatus.Text = AppMonitor.GetLicenseStatus();
                this.Text = "Sirus Cleaner [PROFISSIONAL]";
                form.Close();
            }
            else
            {
                MessageBox.Show("❌ Senha master incorreta!", "Erro", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        // Opção de chave de licença (para vendas futuras)
        var lblKey = new Label
        {
            Text = "OU USE CHAVE DE LICENÇA:",
            Location = new Point(20, 270),
            Size = new Size(400, 20),
            Font = new Font("Segoe UI", 8, FontStyle.Bold),
            ForeColor = Color.Gray
        };

        var txtKey = new TextBox
        {
            Location = new Point(20, 295),
            Size = new Size(280, 25),
            Font = AppStyles.NormalFont
        };

        var btnKey = new Button
        {
            Text = "Ativar com Chave",
            Location = new Point(310, 294),
            Size = new Size(100, 28)
        };
        AppStyles.ApplyModernButton(btnKey, AppStyles.DarkColor);
        btnKey.Click += (s, e) =>
        {
            if (AppMonitor.RenewTrialWithMasterPassword(txtKey.Text.Trim()))
            {
                MessageBox.Show("💎 Licença ativada com sucesso!", "Sucesso", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                lblLicenseStatus.Text = AppMonitor.GetLicenseStatus();
                this.Text = "Sirus Cleaner [PROFISSIONAL]";
                form.Close();
            }
            else
            {
                MessageBox.Show("❌ Chave inválida!", "Erro", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        var btnClose = new Button
        {
            Text = "Fechar",
            Location = new Point(330, 340),
            Size = new Size(100, 30),
            DialogResult = DialogResult.Cancel
        };
        AppStyles.ApplyModernButton(btnClose, AppStyles.AccentColor);

        form.Controls.AddRange(new Control[] { 
            lblStatus, lblExpiry,
            lblRenew, lblRenewInfo, txtRenew, btnRenew,
            lblPermanent, lblPermanentInfo, txtPermanent, btnPermanent,
            lblKey, txtKey, btnKey,
            btnClose 
        });

        form.ShowDialog();
    }
}
        private void LogMessage(string message)
        {
            if (txtLog?.IsDisposed == false)
            {
                try
                {
                    this.Invoke(() =>
                    {
                        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
                        txtLog.ScrollToCaret();
                    });
                }
                catch { }
            }
            AppMonitor.Log(message);
        }

        // Event Handlers
        private void BtnRefresh_Click(object sender, EventArgs e) => _ = LoadProcessesAsync();
        
        private void BtnCleanSelected_Click(object sender, EventArgs e)
        {
            if (listViewProcesses.SelectedItems.Count == 0)
            {
                MessageBox.Show("Selecione processos para limpar.", "Aviso", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            int cleaned = 0;
            foreach (ListViewItem item in listViewProcesses.SelectedItems)
            {
                if (item.Tag is ProcessInfo processInfo && !IsProtectedProcess(processInfo))
                {
                    try
                    {
                        processInfo.Process?.Kill();
                        cleaned++;
                        LogMessage($"🧹 Processo limpo: {processInfo.Name}");
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"❌ Erro ao limpar {processInfo.Name}: {ex.Message}");
                    }
                }
            }
            
            if (cleaned > 0)
            {
                lblCleanupSummary.Text = $"✅ {cleaned} processos limpos";
                _ = LoadProcessesAsync();
            }
        }

        private void BtnCleanInactive_Click(object sender, EventArgs e)
        {
            _ = Task.Run(() => CleanInactiveProcesses());
        }

        private void BtnCleanAllNonSystem_Click(object sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Limpar TODOS os processos não críticos?",
                "Confirmação", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                var nonSystem = processesList.Where(p => !IsProtectedProcess(p)).ToList();
                int cleaned = 0;
                
                foreach (var p in nonSystem)
                {
                    try 
                    { 
                        p.Process?.Kill(); 
                        cleaned++;
                    } 
                    catch { }
                }
                
                LogMessage($"🧹 {cleaned} processos não-sistema limpos");
                lblCleanupSummary.Text = $"✅ {cleaned} processos limpos";
                _ = LoadProcessesAsync();
            }
        }

        private void BtnExportLog_Click(object sender, EventArgs e)
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "Arquivo de texto|*.txt";
                sfd.FileName = $"SirusLog_{DateTime.Now:yyyyMMdd_HHmmss}.txt";

                if (sfd.ShowDialog() == DialogResult.OK && txtLog != null)
                {
                    File.WriteAllText(sfd.FileName, txtLog.Text);
                    MessageBox.Show("Log exportado com sucesso!", "Sucesso", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void BtnLicense_Click(object sender, EventArgs e)
        {
            ShowLicenseDialog();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.WindowState = FormWindowState.Minimized;
            }
            else
            {
                notifyIcon?.Dispose();
                cleanupTimer?.Dispose();
                refreshTimer?.Dispose();
                statusTimer?.Dispose();
                memoryCounter?.Dispose();
                memoryGraph?.Dispose();
                base.OnFormClosing(e);
            }
        }
    }
}