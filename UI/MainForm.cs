using System.ComponentModel;
using System.Diagnostics;
using SiriusCleaner.Cleaning;
using SiriusCleaner.Core;
using SiriusCleaner.Processes;
using SiriusCleaner.Safety;

namespace SiriusCleaner.UI;

internal sealed class MainForm : Form
{
    public const string ShowEventName = @"Local\SiriusCleaner.Show";
    private const int MaxLogChars = 200_000;

    private readonly AppSettings _settings;
    private readonly ProcessGuard _processGuard;
    private readonly bool _startHidden;
    private readonly NotifyIcon _tray = new();
    private readonly TextBox _log = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
        Font = Theme.Mono,
        BackColor = Theme.Surface,
        BorderStyle = BorderStyle.None,
        WordWrap = false,
    };
    private readonly ToolStripStatusLabel _memoryStatus = new();
    private readonly ToolStripStatusLabel _lastCleanStatus = new() { Spring = true, TextAlign = ContentAlignment.MiddleRight };
    private readonly System.Windows.Forms.Timer _autoCleanTimer = new() { Interval = 60_000 };
    private readonly MemoryGraph _graph = new();
    private readonly CleanupPage _cleanupPage;
    private readonly EventWaitHandle _showEvent;
    private readonly RegisteredWaitHandle _showWait;
    private bool _exiting;

    public MainForm(AppSettings settings, bool startHidden)
    {
        _settings = settings;
        _startHidden = startHidden;
        _processGuard = new ProcessGuard(settings.ExtraProtectedProcesses);
        var cleaner = new JunkCleaner(settings);

        Text = AppInfo.IsAdministrator ? $"{AppInfo.Name} (Administrador)" : AppInfo.Name;
        Icon = AppInfo.LoadIcon();
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1240, 800);
        MinimumSize = new Size(960, 620);
        BackColor = Theme.Background;
        Font = Theme.Regular;

        _cleanupPage = new CleanupPage(settings, cleaner);
        var settingsPage = new SettingsPage(settings);
        settingsPage.Saved += (_, _) => _processGuard.SetExtraProtected(_settings.ExtraProtectedProcesses);
        _cleanupPage.Cleaned += (_, report) => UpdateLastClean(report);

        var tabs = new TabControl { Dock = DockStyle.Fill, Font = Theme.Regular, Padding = new Point(16, 6) };
        tabs.TabPages.Add(Page("Limpeza", _cleanupPage));
        tabs.TabPages.Add(Page("Processos", new ProcessesPage(new ProcessMonitor(_processGuard), _graph)));
        tabs.TabPages.Add(Page("Manutenção", new MaintenancePage()));
        tabs.TabPages.Add(Page("Configurações", settingsPage));

        // Ordem de Add importa para o Dock: o último adicionado é posicionado primeiro.
        Controls.Add(tabs);
        Controls.Add(new Splitter { Dock = DockStyle.Bottom, Height = 5, BackColor = Theme.Border });
        Controls.Add(BuildLogPanel());
        Controls.Add(BuildStatusBar());
        Controls.Add(BuildHeader());

        BuildTray();
        Logger.MessageLogged += OnLogMessage;

        _graph.Sampled += (_, _) =>
            _memoryStatus.Text = $"RAM: {_graph.Current.LoadPercent}% em uso · {Format.Bytes((long)_graph.Current.AvailableBytes)} livres";
        _autoCleanTimer.Tick += async (_, _) => await AutoCleanTickAsync();
        _autoCleanTimer.Start();
        UpdateLastClean(null);

        // Uma segunda instância sinaliza este evento para trazer a janela à frente.
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        _showWait = ThreadPool.RegisterWaitForSingleObject(_showEvent, (_, _) =>
        {
            if (IsHandleCreated) BeginInvoke(ShowFromTray);
        }, null, Timeout.Infinite, executeOnlyOnce: false);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Logger.Info($"{AppInfo.Name} {AppInfo.Version} iniciado{(AppInfo.IsAdministrator ? " como administrador" : " (sem privilégios de administrador: itens com 🔒 serão pulados)")}");
    }

    private static TabPage Page(string title, Control content)
    {
        var page = new TabPage(title) { BackColor = Theme.Background };
        page.Controls.Add(content);
        return page;
    }

    private Control BuildHeader()
    {
        var header = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = Theme.Header, Padding = new Padding(18, 10, 18, 10) };
        var icon = new PictureBox { Image = new Icon(Icon, 40, 40).ToBitmap(), SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(40, 40), Location = new Point(18, 14) };
        var title = new Label { Text = AppInfo.Name, Font = Theme.Title, ForeColor = Theme.HeaderText, AutoSize = true, Location = new Point(68, 9) };
        var subtitle = new Label
        {
            Text = $"v{AppInfo.Version} · limpeza segura que preserva serviços e aplicações .NET em uso",
            Font = Theme.Small,
            ForeColor = Theme.HeaderMuted,
            AutoSize = true,
            Location = new Point(70, 40),
        };
        header.Controls.AddRange(new Control[] { icon, title, subtitle });

        Control right;
        if (AppInfo.IsAdministrator)
        {
            right = new Label { Text = "🛡  Modo administrador", ForeColor = Color.FromArgb(134, 239, 172), Font = Theme.Bold, AutoSize = true };
        }
        else
        {
            var elevate = Theme.Button("🛡  Executar como administrador", Theme.Accent, 250);
            elevate.Click += (_, _) => RestartElevated();
            new ToolTip().SetToolTip(elevate, "Necessário para limpar C:\\Windows\\Temp, logs do sistema, Windows Update e para as tarefas de manutenção.");
            right = elevate;
        }
        right.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        header.Controls.Add(right);
        header.Layout += (_, _) => right.Location = new Point(header.ClientSize.Width - right.Width - 18, (header.Height - right.Height) / 2);
        return header;
    }

    private Control BuildLogPanel()
    {
        var panel = new Panel { Dock = DockStyle.Bottom, Height = 170, Padding = new Padding(14, 6, 14, 8), BackColor = Theme.Background };
        var bar = new Panel { Dock = DockStyle.Top, Height = 30 };
        var title = Theme.Label("Atividade", Theme.Bold);
        title.Location = new Point(0, 6);
        var export = new LinkLabel { Text = "Exportar", AutoSize = true, LinkColor = Theme.Accent, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        var clear = new LinkLabel { Text = "Limpar", AutoSize = true, LinkColor = Theme.Accent, Anchor = AnchorStyles.Top | AnchorStyles.Right };
        bar.Controls.AddRange(new Control[] { title, export, clear });
        bar.Layout += (_, _) =>
        {
            clear.Location = new Point(bar.Width - clear.Width, 6);
            export.Location = new Point(clear.Left - export.Width - 12, 6);
        };
        export.LinkClicked += (_, _) => ExportLog();
        clear.LinkClicked += (_, _) => _log.Clear();

        var border = new Panel { Dock = DockStyle.Fill, Padding = new Padding(1), BackColor = Theme.Border };
        var inner = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 6, 4, 4), BackColor = Theme.Surface };
        inner.Controls.Add(_log);
        border.Controls.Add(inner);

        panel.Controls.Add(border);
        panel.Controls.Add(bar);
        return panel;
    }

    private Control BuildStatusBar()
    {
        var status = new StatusStrip { SizingGrip = true, BackColor = Theme.Surface, Font = Theme.Small };
        status.Items.Add(_memoryStatus);
        status.Items.Add(_lastCleanStatus);
        return status;
    }

    private void BuildTray()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir Sirius Cleaner", null, (_, _) => ShowFromTray());
        menu.Items.Add("Limpeza rápida (categorias marcadas)", null, async (_, _) => await QuickCleanAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => ExitApplication());
        menu.Items[0].Font = new Font(menu.Items[0].Font, FontStyle.Bold);

        _tray.Icon = Icon;
        _tray.Text = AppInfo.Name;
        _tray.ContextMenuStrip = menu;
        _tray.Visible = true;
        _tray.DoubleClick += (_, _) => ShowFromTray();
    }

    private async Task QuickCleanAsync()
    {
        var report = await _cleanupPage.RunAsync(_cleanupPage.CheckedCategories()
            .Where(c => c.ConfirmationMessage == null).ToList(), dryRun: false, origin: "Limpeza rápida");
        if (report != null)
            _tray.ShowBalloonTip(4000, AppInfo.Name, report.Summary(), ToolTipIcon.Info);
    }

    private async Task AutoCleanTickAsync()
    {
        if (!_settings.AutoCleanEnabled || _cleanupPage.IsRunning) return;
        if (_settings.LastAutoCleanUtc is { } last &&
            DateTime.UtcNow - last < TimeSpan.FromMinutes(_settings.AutoCleanIntervalMinutes)) return;
        if (SystemState.IdleTime < TimeSpan.FromMinutes(_settings.IdleMinutesBeforeAutoClean)) return;
        if (!_settings.AutoCleanOnBattery && SystemState.IsOnBattery) return;

        var categories = CleanupCatalog.All
            .Where(c => c.AllowInAutoClean && c.ConfirmationMessage == null && _settings.IsCategoryEnabled(c.Id, c.DefaultEnabled))
            .ToList();

        _settings.LastAutoCleanUtc = DateTime.UtcNow;
        _settings.Save();

        var report = await _cleanupPage.RunAsync(categories, dryRun: false, origin: "Limpeza automática");
        if (report != null && report.TotalBytes > 50L * 1024 * 1024 && !Visible)
            _tray.ShowBalloonTip(4000, AppInfo.Name, $"Limpeza automática: {report.Summary()}", ToolTipIcon.Info);
    }

    private void UpdateLastClean(CleanupReport report)
    {
        if (report != null)
            _lastCleanStatus.Text = $"Última limpeza: {DateTime.Now:dd/MM HH:mm} · {Format.Bytes(report.TotalBytes)} liberados";
        else if (_settings.LastAutoCleanUtc is { } last)
            _lastCleanStatus.Text = $"Última limpeza automática: {last.ToLocalTime():dd/MM HH:mm}";
        else
            _lastCleanStatus.Text = _settings.AutoCleanEnabled ? "Limpeza automática ativa" : "Limpeza automática desligada";
    }

    private void OnLogMessage(string line)
    {
        if (!IsHandleCreated || IsDisposed) return;
        try
        {
            BeginInvoke(() =>
            {
                if (_log.TextLength > MaxLogChars)
                    _log.Text = _log.Text[^(MaxLogChars / 2)..];
                _log.AppendText(line + Environment.NewLine);
            });
        }
        catch (InvalidOperationException)
        {
            // Janela sendo fechada.
        }
    }

    private void ExportLog()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "Arquivo de texto|*.txt",
            FileName = $"SiriusCleaner_{DateTime.Now:yyyyMMdd_HHmmss}.txt",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        File.WriteAllText(dialog.FileName, _log.Text);
        Logger.Info($"Log exportado para {dialog.FileName}");
    }

    private void RestartElevated()
    {
        try
        {
            Process.Start(new ProcessStartInfo(Environment.ProcessPath, "--wait-instance")
            {
                UseShellExecute = true,
                Verb = "runas",
            });
            ExitApplication();
        }
        catch (Win32Exception)
        {
            Logger.Warn("Elevação cancelada pelo usuário");
        }
    }

    private void ShowFromTray()
    {
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
    }

    private void ExitApplication()
    {
        _exiting = true;
        Close();
    }

    protected override void SetVisibleCore(bool value)
    {
        // Iniciado com --tray: cria a janela sem mostrá-la.
        if (_startHidden && !IsHandleCreated)
        {
            CreateHandle();
            value = false;
        }
        base.SetVisibleCore(value);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_exiting && e.CloseReason == CloseReason.UserClosing && _settings.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
            _tray.ShowBalloonTip(2000, AppInfo.Name, "Continua rodando na bandeja. Clique com o botão direito no ícone para sair.", ToolTipIcon.Info);
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        Logger.MessageLogged -= OnLogMessage;
        _showWait.Unregister(null);
        _showEvent.Dispose();
        _autoCleanTimer.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        base.OnFormClosed(e);
    }
}
