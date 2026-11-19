using SiriusCleaner.Core;
using SiriusCleaner.Processes;
using SiriusCleaner.Safety;

namespace SiriusCleaner.UI;

/// <summary>Aba "Processos": monitor de processos e RAM. Nada é encerrado automaticamente.</summary>
internal sealed class ProcessesPage : UserControl
{
    private readonly ProcessMonitor _monitor;
    private readonly SmoothListView _list = new() { MultiSelect = false };
    private readonly TextBox _filter = new() { Width = 220, Font = Theme.Regular, PlaceholderText = "Filtrar por nome...", Margin = new Padding(0, 5, 12, 0) };
    private readonly CheckBox _showSystem = new() { Text = "Mostrar sistema e serviços", AutoSize = true, Font = Theme.Regular, Margin = new Padding(0, 8, 12, 0) };
    private readonly Label _count = Theme.Label("", Theme.Small, Theme.Muted);
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 3000 };
    private List<ProcessEntry> _entries = new();
    private int _sortColumn = 3;
    private bool _sortDescending = true;
    private bool _refreshing;

    public ProcessesPage(ProcessMonitor monitor, MemoryGraph graph)
    {
        _monitor = monitor;
        Dock = DockStyle.Fill;
        Padding = new Padding(14);
        BackColor = Theme.Background;

        var toolbar = Theme.Toolbar();
        var refresh = Theme.Button("🔄  Atualizar", Theme.Accent);
        var kill = Theme.Button("Encerrar processo", Theme.Danger, 150);
        toolbar.Controls.AddRange(new Control[] { refresh, kill, _filter, _showSystem, _count });

        var note = new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            ForeColor = Theme.Muted,
            Text = "O Sirius não encerra processos sozinho. Sistema, serviços (incluindo IIS e apps .NET hospedados como serviço) e processos protegidos não podem ser encerrados.",
        };

        _list.Columns.Add("Processo", 220);
        _list.Columns.Add("PID", 70, HorizontalAlignment.Right);
        _list.Columns.Add("Tipo", 110);
        _list.Columns.Add("Memória", 100, HorizontalAlignment.Right);
        _list.Columns.Add("CPU", 70, HorizontalAlignment.Right);
        _list.Columns.Add("Serviços / caminho", 520);
        _list.ShowItemToolTips = true;

        graph.Dock = DockStyle.Right;
        graph.Width = 340;
        var spacer = new Panel { Dock = DockStyle.Right, Width = 12 };

        Controls.Add(_list);
        Controls.Add(spacer);
        Controls.Add(graph);
        Controls.Add(note);
        Controls.Add(toolbar);

        refresh.Click += async (_, _) => await RefreshAsync();
        kill.Click += (_, _) => KillSelected();
        _filter.TextChanged += (_, _) => Render();
        _showSystem.CheckedChanged += (_, _) => Render();
        _list.ColumnClick += (_, e) =>
        {
            _sortDescending = e.Column == _sortColumn ? !_sortDescending : e.Column is 3 or 4;
            _sortColumn = e.Column;
            Render();
        };
        _timer.Tick += async (_, _) => await RefreshAsync();
        VisibleChanged += async (_, _) =>
        {
            _timer.Enabled = Visible;
            if (Visible) await RefreshAsync();
        };
    }

    private async Task RefreshAsync()
    {
        if (_refreshing || !Visible || ParentForm is not { Visible: true, WindowState: not FormWindowState.Minimized }) return;
        _refreshing = true;
        try
        {
            _entries = await Task.Run(_monitor.Snapshot);
            Render();
        }
        catch (Exception ex)
        {
            Logger.Error("Falha ao listar processos", ex);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void Render()
    {
        var filter = _filter.Text.Trim();
        IEnumerable<ProcessEntry> rows = _entries;
        if (!_showSystem.Checked) rows = rows.Where(e => e.Kind is not (ProcessKind.Sistema or ProcessKind.Servico));
        if (filter.Length > 0) rows = rows.Where(e => e.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));

        Func<ProcessEntry, IComparable> key = _sortColumn switch
        {
            1 => e => e.Pid,
            2 => e => e.Kind,
            3 => e => e.MemoryBytes,
            4 => e => e.CpuPercent,
            5 => e => e.Path ?? "",
            _ => e => e.Name,
        };
        var list = (_sortDescending ? rows.OrderByDescending(key) : rows.OrderBy(key)).ToList();

        int? selectedPid = (_list.SelectedItems.Count > 0 ? _list.SelectedItems[0].Tag as ProcessEntry : null)?.Pid;
        int topIndex = _list.TopItem?.Index ?? 0;

        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var entry in list)
        {
            var detail = entry.Services != null ? $"Serviço: {entry.Services}" : entry.Path ?? "";
            var item = new ListViewItem(new[]
            {
                entry.Name,
                entry.Pid.ToString(),
                ProcessGuard.Describe(entry.Kind),
                Format.Bytes(entry.MemoryBytes),
                entry.CpuPercent < 0.05 ? "0" : $"{entry.CpuPercent:0.0}%",
                detail,
            })
            {
                Tag = entry,
                ToolTipText = $"{entry.Name} ({entry.Pid})\n{detail}",
            };

            if (ProcessGuard.IsBlocked(entry.Kind)) { item.BackColor = Theme.RowProtected; item.ForeColor = Theme.Muted; }
            else if (entry.Kind == ProcessKind.DotNet) item.BackColor = Theme.RowDotNet;
            else if (entry.MemoryBytes > 1024L * 1024 * 1024) item.BackColor = Theme.RowHeavy;

            _list.Items.Add(item);
            if (entry.Pid == selectedPid) item.Selected = true;
        }
        if (_list.Items.Count > 0) _list.TopItem = _list.Items[Math.Min(topIndex, _list.Items.Count - 1)];
        _list.EndUpdate();

        _count.Text = $"{list.Count} de {_entries.Count} processos";
    }

    private void KillSelected()
    {
        if (_list.SelectedItems.Count == 0 || _list.SelectedItems[0].Tag is not ProcessEntry entry)
        {
            MessageBox.Show(this, "Selecione um processo na lista.", AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (ProcessGuard.IsBlocked(entry.Kind))
        {
            MessageBox.Show(this,
                $"\"{entry.Name}\" é um processo do tipo {ProcessGuard.Describe(entry.Kind)} e é protegido pelo Sirius.\n\n" +
                "Para serviços, use services.msc ou o gerenciador do próprio serviço.",
                AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Stop);
            return;
        }

        var message = ProcessGuard.NeedsStrongConfirmation(entry.Kind)
            ? $"\"{entry.Name}\" (PID {entry.Pid}) é um processo {ProcessGuard.Describe(entry.Kind)} e pode estar servindo uma aplicação, um build ou uma sessão de depuração.\n\nEncerrar mesmo assim?"
            : $"Encerrar \"{entry.Name}\" (PID {entry.Pid})?\n\nDados não salvos nesse programa serão perdidos.";

        if (MessageBox.Show(this, message, "Encerrar processo", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        if (ProcessMonitor.TryTerminate(entry, out var error))
            Logger.Info($"Processo encerrado pelo usuário: {entry.Name} (PID {entry.Pid})");
        else
            Logger.Warn($"Não foi possível encerrar {entry.Name}: {error}");

        _ = RefreshAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
