using System.Diagnostics;
using SiriusCleaner.Core;

namespace SiriusCleaner.UI;

/// <summary>Aba "Configurações".</summary>
internal sealed class SettingsPage : UserControl
{
    private readonly AppSettings _settings;
    private readonly NumericUpDown _minAge = Number(1, 24 * 90);
    private readonly CheckBox _autoClean = Check("Limpar automaticamente em segundo plano (só categorias recomendadas e marcadas)");
    private readonly NumericUpDown _interval = Number(15, 24 * 60 * 7);
    private readonly NumericUpDown _idle = Number(1, 240);
    private readonly CheckBox _onBattery = Check("Permitir limpeza automática usando bateria");
    private readonly CheckBox _startWithWindows = Check("Iniciar com o Windows (minimizado na bandeja)");
    private readonly CheckBox _minimizeToTray = Check("Fechar a janela mantém o Sirius na bandeja");
    private readonly TextBox _protectedProcesses = MultiLine();
    private readonly TextBox _protectedPaths = MultiLine();

    public event EventHandler Saved;

    public SettingsPage(AppSettings settings)
    {
        _settings = settings;
        Dock = DockStyle.Fill;
        Padding = new Padding(14);
        BackColor = Theme.Background;
        AutoScroll = true;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(0),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        void Section(string title)
        {
            var label = Theme.Label(title, Theme.Bold, Theme.Accent);
            label.Margin = new Padding(0, 16, 0, 6);
            layout.Controls.Add(label);
            layout.SetColumnSpan(label, 2);
        }

        void Row(string text, Control control, string hint = null)
        {
            layout.Controls.Add(Theme.Label(text));
            if (hint == null)
            {
                layout.Controls.Add(control);
                return;
            }
            var panel = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
            panel.Controls.Add(control);
            panel.Controls.Add(Theme.Label(hint, Theme.Small, Theme.Muted));
            layout.Controls.Add(panel);
        }

        void Full(Control control)
        {
            layout.Controls.Add(control);
            layout.SetColumnSpan(control, 2);
        }

        Section("Segurança da limpeza");
        Row("Idade mínima dos arquivos (horas)", _minAge, "arquivos mais novos que isso nunca são apagados");
        Full(Theme.Label("Processos protegidos (um por linha, sem .exe) — além de sistema, serviços e .NET:", Theme.Regular, Theme.Muted));
        Full(_protectedProcesses);
        Full(Theme.Label("Pastas protegidas (uma por linha, aceita %VARIAVEIS%) — nunca serão tocadas:", Theme.Regular, Theme.Muted));
        Full(_protectedPaths);

        Section("Limpeza automática");
        Full(_autoClean);
        Row("Intervalo entre limpezas (minutos)", _interval);
        Row("Só limpar após o PC ficar ocioso por (minutos)", _idle);
        Full(_onBattery);

        Section("Inicialização");
        Full(_startWithWindows);
        Full(_minimizeToTray);

        var buttons = Theme.Toolbar();
        buttons.Dock = DockStyle.None;
        buttons.Margin = new Padding(0, 18, 0, 0);
        var save = Theme.Button("Salvar", Theme.Success, 110);
        var openLogs = Theme.Button("Abrir pasta de logs", Theme.Neutral, 160);
        buttons.Controls.AddRange(new Control[] { save, openLogs });
        Full(buttons);

        Controls.Add(layout);
        LoadValues();

        save.Click += (_, _) => SaveValues();
        openLogs.Click += (_, _) =>
        {
            Directory.CreateDirectory(AppInfo.DataDir);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppInfo.DataDir}\"") { UseShellExecute = true });
        };
    }

    private void LoadValues()
    {
        _minAge.Value = _settings.MinFileAgeHours;
        _autoClean.Checked = _settings.AutoCleanEnabled;
        _interval.Value = _settings.AutoCleanIntervalMinutes;
        _idle.Value = _settings.IdleMinutesBeforeAutoClean;
        _onBattery.Checked = _settings.AutoCleanOnBattery;
        _startWithWindows.Checked = StartupManager.IsEnabled();
        _minimizeToTray.Checked = _settings.MinimizeToTray;
        _protectedProcesses.Lines = _settings.ExtraProtectedProcesses.ToArray();
        _protectedPaths.Lines = _settings.ExtraProtectedPaths.ToArray();
    }

    private void SaveValues()
    {
        _settings.MinFileAgeHours = (int)_minAge.Value;
        _settings.AutoCleanEnabled = _autoClean.Checked;
        _settings.AutoCleanIntervalMinutes = (int)_interval.Value;
        _settings.IdleMinutesBeforeAutoClean = (int)_idle.Value;
        _settings.AutoCleanOnBattery = _onBattery.Checked;
        _settings.StartWithWindows = _startWithWindows.Checked;
        _settings.MinimizeToTray = _minimizeToTray.Checked;
        _settings.ExtraProtectedProcesses = CleanLines(_protectedProcesses);
        _settings.ExtraProtectedPaths = CleanLines(_protectedPaths);
        _settings.Save();
        StartupManager.Apply(_settings.StartWithWindows);

        Logger.Info("Configurações salvas");
        Saved?.Invoke(this, EventArgs.Empty);
        MessageBox.Show(this, "Configurações salvas.", AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static List<string> CleanLines(TextBox box) =>
        box.Lines.Select(l => l.Trim()).Where(l => l.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static NumericUpDown Number(int min, int max) => new()
    {
        Minimum = min,
        Maximum = max,
        Width = 90,
        Font = Theme.Regular,
        Margin = new Padding(0, 4, 10, 0),
    };

    private static CheckBox Check(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = Theme.Regular,
        Margin = new Padding(0, 6, 0, 0),
    };

    private static TextBox MultiLine() => new()
    {
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Height = 80,
        Width = 720,
        Font = Theme.Mono,
        Margin = new Padding(0, 4, 0, 4),
    };
}
