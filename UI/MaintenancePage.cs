using SiriusCleaner.Core;
using SiriusCleaner.Maintenance;

namespace SiriusCleaner.UI;

/// <summary>Aba "Manutenção": ferramentas nativas do Windows com saída no log.</summary>
internal sealed class MaintenancePage : UserControl
{
    private readonly SmoothListView _list = new() { MultiSelect = false };
    private readonly Button _run = Theme.Button("▶  Executar", Theme.Accent);
    private readonly Label _status = Theme.Label("", Theme.Bold, Theme.Muted);
    private bool _running;

    public MaintenancePage()
    {
        Dock = DockStyle.Fill;
        Padding = new Padding(14);
        BackColor = Theme.Background;

        var intro = new Label
        {
            Dock = DockStyle.Top,
            Height = 40,
            ForeColor = Theme.Muted,
            Text = "Ferramentas oficiais do Windows. Nenhuma delas encerra programas ou serviços em uso; a saída aparece no painel de atividade.\n" +
                   "Ordem sugerida para um PC com problemas: ponto de restauração → DISM → SFC → limpar componentes.",
        };

        var toolbar = Theme.Toolbar();
        toolbar.Controls.AddRange(new Control[] { _run, _status });

        _list.Columns.Add("Tarefa", 290);
        _list.Columns.Add("Duração", 100);
        _list.Columns.Add("Admin", 70);
        _list.Columns.Add("O que faz", 620);
        foreach (var task in MaintenanceCatalog.All)
        {
            var item = new ListViewItem(new[]
            {
                task.Name,
                task.Duration,
                task.RequiresAdmin ? "Sim" : "Não",
                task.Description,
            })
            { Tag = task };
            if (task.RequiresAdmin && !AppInfo.IsAdministrator) item.ForeColor = Theme.Muted;
            _list.Items.Add(item);
        }

        Controls.Add(_list);
        Controls.Add(toolbar);
        Controls.Add(intro);

        _run.Click += async (_, _) => await RunSelectedAsync();
        _list.ItemActivate += async (_, _) => await RunSelectedAsync();
    }

    private async Task RunSelectedAsync()
    {
        if (_running) return;
        if (_list.SelectedItems.Count == 0 || _list.SelectedItems[0].Tag is not MaintenanceTask task)
        {
            MessageBox.Show(this, "Selecione uma tarefa.", AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (task.RequiresAdmin && !AppInfo.IsAdministrator)
        {
            MessageBox.Show(this, "Esta tarefa precisa de privilégios de administrador.\nUse o botão \"Executar como administrador\" no topo da janela.",
                AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _running = true;
        _run.Enabled = false;
        _status.Text = $"Executando: {task.Name}...";
        _status.ForeColor = Theme.Accent;
        Logger.Info($"Manutenção iniciada: {task.Name}");
        try
        {
            int exit = await MaintenanceRunner.RunAsync(task, line => Logger.Info($"  {line}"));
            if (exit == 0)
            {
                Logger.Info($"Manutenção concluída: {task.Name}");
                _status.Text = $"Concluído: {task.Name}";
                _status.ForeColor = Theme.Success;
            }
            else
            {
                Logger.Warn($"{task.Name} terminou com código {exit}");
                _status.Text = $"{task.Name} terminou com código {exit} (veja o log)";
                _status.ForeColor = Theme.Warning;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Falha ao executar {task.Name}", ex);
            _status.Text = $"Falha: {ex.Message}";
            _status.ForeColor = Theme.Danger;
        }
        finally
        {
            _running = false;
            _run.Enabled = true;
        }
    }
}
