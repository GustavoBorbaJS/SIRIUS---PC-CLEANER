namespace SiriusCleaner.UI;

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(245, 247, 250);
    public static readonly Color Surface = Color.White;
    public static readonly Color Header = Color.FromArgb(17, 24, 39);
    public static readonly Color HeaderText = Color.FromArgb(243, 244, 246);
    public static readonly Color HeaderMuted = Color.FromArgb(156, 163, 175);
    public static readonly Color Text = Color.FromArgb(17, 24, 39);
    public static readonly Color Muted = Color.FromArgb(100, 116, 139);
    public static readonly Color Border = Color.FromArgb(226, 232, 240);
    public static readonly Color Accent = Color.FromArgb(37, 99, 235);
    public static readonly Color Success = Color.FromArgb(22, 163, 74);
    public static readonly Color Warning = Color.FromArgb(217, 119, 6);
    public static readonly Color Danger = Color.FromArgb(220, 38, 38);
    public static readonly Color Neutral = Color.FromArgb(71, 85, 105);

    public static readonly Color RowProtected = Color.FromArgb(241, 245, 249);
    public static readonly Color RowDotNet = Color.FromArgb(237, 233, 254);
    public static readonly Color RowHeavy = Color.FromArgb(254, 242, 242);

    public static readonly Font Regular = new("Segoe UI", 9.5f);
    public static readonly Font Small = new("Segoe UI", 8.5f);
    public static readonly Font Bold = new("Segoe UI Semibold", 9.5f);
    public static readonly Font Title = new("Segoe UI Semibold", 15f);
    public static readonly Font Mono = new("Consolas", 9f);

    public static Button Button(string text, Color color, int width = 130)
    {
        var button = new Button
        {
            Text = text,
            Width = width,
            Height = 34,
            BackColor = color,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = Bold,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 8, 0),
            UseVisualStyleBackColor = false,
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = ControlPaint.Light(color, 0.15f);
        button.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(color, 0.05f);
        return button;
    }

    public static Label Label(string text, Font font = null, Color? color = null) => new()
    {
        Text = text,
        AutoSize = true,
        Font = font ?? Regular,
        ForeColor = color ?? Text,
        Margin = new Padding(0, 8, 12, 0),
    };

    public static FlowLayoutPanel Toolbar() => new()
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        WrapContents = true,
        Padding = new Padding(0, 0, 0, 10),
        BackColor = Color.Transparent,
    };
}

/// <summary>ListView sem cintilação ao atualizar.</summary>
internal sealed class SmoothListView : ListView
{
    public SmoothListView()
    {
        DoubleBuffered = true;
        View = View.Details;
        FullRowSelect = true;
        BorderStyle = BorderStyle.FixedSingle;
        Font = Theme.Regular;
        HideSelection = false;
        Dock = DockStyle.Fill;
    }
}
