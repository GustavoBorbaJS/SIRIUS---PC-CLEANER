using SiriusCleaner.Cleaning;
using SiriusCleaner.Core;

namespace SiriusCleaner.UI;

/// <summary>Aba "Limpeza": escolher categorias, analisar e limpar.</summary>
internal sealed class CleanupPage : UserControl
{
    private readonly AppSettings _settings;
    private readonly JunkCleaner _cleaner;
    private readonly SmoothListView _list = new() { CheckBoxes = true };
    private readonly Dictionary<string, ListViewItem> _items = new();
    private readonly Button _analyze = Theme.Button("🔍  &Analisar", Theme.Accent);
    private readonly Button _clean = Theme.Button("🧹  &Limpar", Theme.Success);
    private readonly Button _cancel = Theme.Button("Cancelar", Theme.Neutral, 100);
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Fill, Height = 8, Style = ProgressBarStyle.Continuous };
    private readonly Label _summary = new() { Dock = DockStyle.Fill, AutoEllipsis = true, Font = Theme.Bold, TextAlign = ContentAlignment.MiddleLeft };
    private CancellationTokenSource _cts;
    private bool _loading;

    public bool IsRunning { get; private set; }

    /// <summary>Disparado ao terminar uma limpeza real (não análise).</summary>
    public event EventHandler<CleanupReport> Cleaned;

    public CleanupPage(AppSettings settings, JunkCleaner cleaner)
    {
        _settings = settings;
        _cleaner = cleaner;
        Dock = DockStyle.Fill;
        Padding = new Padding(14);
        BackColor = Theme.Background;

        var intro = new Label
        {
            Dock = DockStyle.Top,
            Height = 40,
            ForeColor = Theme.Muted,
            Font = Theme.Regular,
            Text = "Arquivos em uso, criados/modificados recentemente e pastas de .NET, MSBuild, NuGet, ASP.NET e IIS são sempre preservados.\n" +
                   "Caches de programas abertos são pulados. Use \"Analisar\" para ver o que seria removido sem apagar nada.",
        };

        var toolbar = Theme.Toolbar();
        var selectRecommended = new LinkLabel { Text = "Marcar recomendados", AutoSize = true, Margin = new Padding(12, 9, 8, 0), LinkColor = Theme.Accent };
        var selectNone = new LinkLabel { Text = "Desmarcar todos", AutoSize = true, Margin = new Padding(4, 9, 8, 0), LinkColor = Theme.Accent };
        toolbar.Controls.AddRange(new Control[] { _analyze, _clean, _cancel, selectRecommended, selectNone });
        _cancel.Enabled = false;

        _list.Columns.Add("Categoria", 270);
        _list.Columns.Add("Itens", 80, HorizontalAlignment.Right);
        _list.Columns.Add("Tamanho", 90, HorizontalAlignment.Right);
        _list.Columns.Add("Situação", 330);
        _list.Columns.Add("Descrição", 520);
        _list.ShowItemToolTips = true;
        PopulateCategories();

        var footer = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 46, ColumnCount = 1, RowCount = 2, Padding = new Padding(0, 8, 0, 0) };
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 10));
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        footer.Controls.Add(_progress, 0, 0);
        footer.Controls.Add(_summary, 0, 1);
        _summary.Text = "Pronto. Escolha as categorias e clique em Analisar.";

        Controls.Add(_list);
        Controls.Add(footer);
        Controls.Add(toolbar);
        Controls.Add(intro);

        _analyze.Click += async (_, _) => await RunAsync(CheckedCategories(), dryRun: true);
        _clean.Click += async (_, _) => await CleanCheckedAsync();
        _cancel.Click += (_, _) => _cts?.Cancel();
        selectRecommended.LinkClicked += (_, _) => SetChecked(c => c.DefaultEnabled);
        selectNone.LinkClicked += (_, _) => SetChecked(_ => false);
        _list.ItemChecked += (_, _) => SaveSelection();

        // O ListView dispara ItemChecked para cada item ao criar o handle nativo; só reagimos depois disso.
        _loading = true;
        _list.HandleCreated += (_, _) => BeginInvoke(() => _loading = false);
        _list.HandleDestroyed += (_, _) => _loading = true;
    }

    private void PopulateCategories()
    {
        _loading = true;
        _list.BeginUpdate();
        var groups = new Dictionary<string, ListViewGroup>();
        foreach (var category in CleanupCatalog.All)
        {
            if (!groups.TryGetValue(category.Group, out var group))
            {
                group = new ListViewGroup(category.Group);
                groups[category.Group] = group;
                _list.Groups.Add(group);
            }

            var name = category.RequiresAdmin && !AppInfo.IsAdministrator ? $"{category.Name}  🔒" : category.Name;
            var item = new ListViewItem(new[] { name, "", "", "", category.Description })
            {
                Group = group,
                Tag = category,
                Checked = _settings.IsCategoryEnabled(category.Id, category.DefaultEnabled),
                ToolTipText = category.Description + (category.RequiresAdmin ? "\nRequer executar como administrador." : ""),
            };
            _items[category.Id] = item;
            _list.Items.Add(item);
        }
        _list.EndUpdate();
    }

    private void SetChecked(Func<CleanupCategory, bool> predicate)
    {
        bool wasLoading = _loading;
        _loading = true;
        foreach (ListViewItem item in _list.Items) item.Checked = predicate((CleanupCategory)item.Tag);
        _loading = wasLoading;
        SaveSelection();
    }

    private void SaveSelection()
    {
        if (_loading) return;
        _settings.EnabledCategories = _list.Items.OfType<ListViewItem>()
            .Where(i => i.Checked && i.Tag is CleanupCategory)
            .Select(i => ((CleanupCategory)i.Tag).Id)
            .ToList();
        _settings.Save();
    }

    public List<CleanupCategory> CheckedCategories() =>
        _list.Items.OfType<ListViewItem>().Where(i => i.Checked && i.Tag is CleanupCategory)
            .Select(i => (CleanupCategory)i.Tag).ToList();

    private async Task CleanCheckedAsync()
    {
        var categories = CheckedCategories();
        foreach (var confirm in categories.Where(c => c.ConfirmationMessage != null).ToList())
        {
            if (MessageBox.Show(this, confirm.ConfirmationMessage, confirm.Name, MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                categories.Remove(confirm);
        }
        await RunAsync(categories, dryRun: false);
    }

    /// <summary>Executa análise ou limpeza. Retorna nulo se já houver uma em andamento.</summary>
    public async Task<CleanupReport> RunAsync(IReadOnlyList<CleanupCategory> categories, bool dryRun, string origin = null)
    {
        if (IsRunning) return null;
        if (categories.Count == 0)
        {
            if (origin == null)
                MessageBox.Show(this, "Marque pelo menos uma categoria.", AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return null;
        }

        IsRunning = true;
        SetBusy(true);
        _cts = new CancellationTokenSource();
        _progress.Maximum = categories.Count;
        _progress.Value = 0;
        foreach (var category in categories)
        {
            var item = _items[category.Id];
            item.SubItems[1].Text = item.SubItems[2].Text = "";
            item.SubItems[3].Text = "Aguardando...";
            item.ForeColor = Theme.Muted;
        }

        var label = origin ?? (dryRun ? "Análise" : "Limpeza");
        Logger.Info($"{label} iniciada ({categories.Count} categorias)");
        _summary.ForeColor = Theme.Text;
        _summary.Text = dryRun ? "Analisando..." : "Limpando...";

        var progress = new Progress<CleanupProgress>(p => OnProgress(p, dryRun));
        try
        {
            var report = await _cleaner.RunAsync(categories, dryRun, progress, _cts.Token);
            _summary.Text = report.Summary();
            _summary.ForeColor = report.TotalBytes > 0 ? Theme.Success : Theme.Text;
            Logger.Info($"{label} concluída: {report.Summary()}");
            if (!dryRun) Cleaned?.Invoke(this, report);
            return report;
        }
        catch (InvalidOperationException ex)
        {
            _summary.Text = ex.Message;
            Logger.Warn(ex.Message);
            return null;
        }
        catch (Exception ex)
        {
            _summary.Text = "Falha na limpeza. Veja o log.";
            Logger.Error("Falha inesperada na limpeza", ex);
            return null;
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            IsRunning = false;
            SetBusy(false);
        }
    }

    private void OnProgress(CleanupProgress progress, bool dryRun)
    {
        var item = _items[progress.Category.Id];
        if (progress.Result == null)
        {
            item.SubItems[3].Text = dryRun ? "Analisando..." : "Limpando...";
            item.ForeColor = Theme.Accent;
            item.EnsureVisible();
            return;
        }

        var r = progress.Result;
        item.SubItems[1].Text = r.SkipReason == null ? Format.Number(r.Files) : "–";
        item.SubItems[2].Text = r.SkipReason == null ? Format.Bytes(r.Bytes) : "–";
        item.SubItems[3].Text = r.StatusText(dryRun);
        item.ForeColor = r.SkipReason != null ? Theme.Warning : r.Bytes > 0 ? Theme.Text : Theme.Muted;
        _progress.Value = Math.Min(_progress.Maximum, _progress.Value + 1);
    }

    private void SetBusy(bool busy)
    {
        _analyze.Enabled = _clean.Enabled = !busy;
        _cancel.Enabled = busy;
        _list.Enabled = !busy;
        UseWaitCursor = busy;
    }
}
