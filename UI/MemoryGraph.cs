using System.Drawing.Drawing2D;
using SiriusCleaner.Core;

namespace SiriusCleaner.UI;

/// <summary>Gráfico de uso de RAM dos últimos 2 minutos (leitura via GlobalMemoryStatusEx, custo ~zero).</summary>
internal sealed class MemoryGraph : Control
{
    private const int MaxPoints = 120;
    private readonly Queue<float> _history = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 1000 };

    public MemorySnapshot Current { get; private set; }
    public event EventHandler Sampled;

    public MemoryGraph()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        BackColor = Theme.Surface;
        MinimumSize = new Size(200, 140);
        _timer.Tick += (_, _) => Sample();
        _timer.Start();
        Sample();
    }

    private void Sample()
    {
        Current = SystemState.GetMemory();
        if (Current.TotalBytes == 0) return;
        _history.Enqueue(Current.LoadPercent);
        while (_history.Count > MaxPoints) _history.Dequeue();
        Sampled?.Invoke(this, EventArgs.Empty);
        if (Visible) Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(BackColor);

        var plot = new RectangleF(40, 34, Width - 52, Height - 58);
        using (var border = new Pen(Theme.Border))
            g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);

        using (var title = new SolidBrush(Theme.Text))
            g.DrawString("Uso de memória RAM", Theme.Bold, title, 10, 8);

        if (Current.TotalBytes > 0)
        {
            var text = $"{Current.LoadPercent}% · {Format.Bytes((long)Current.UsedBytes)} de {Format.Bytes((long)Current.TotalBytes)}";
            var size = g.MeasureString(text, Theme.Small);
            using var brush = new SolidBrush(ColorFor(Current.LoadPercent));
            g.DrawString(text, Theme.Small, brush, Width - size.Width - 10, 10);
        }

        using (var grid = new Pen(Theme.Border) { DashStyle = DashStyle.Dot })
        using (var labels = new SolidBrush(Theme.Muted))
        {
            for (int i = 0; i <= 4; i++)
            {
                float y = plot.Bottom - plot.Height * i / 4f;
                g.DrawLine(grid, plot.Left, y, plot.Right, y);
                g.DrawString($"{i * 25}%", Theme.Small, labels, 4, y - 8);
            }
            g.DrawString("últimos 2 minutos", Theme.Small, labels, plot.Left, plot.Bottom + 4);
        }

        if (_history.Count < 2) return;

        var values = _history.ToArray();
        float step = plot.Width / (MaxPoints - 1);
        float startX = plot.Right - step * (values.Length - 1);
        var points = values.Select((v, i) => new PointF(startX + i * step, plot.Bottom - plot.Height * v / 100f)).ToArray();

        var color = ColorFor(values[^1]);
        using (var path = new GraphicsPath())
        {
            path.AddLines(points);
            path.AddLine(points[^1], new PointF(points[^1].X, plot.Bottom));
            path.AddLine(new PointF(points[^1].X, plot.Bottom), new PointF(points[0].X, plot.Bottom));
            path.CloseFigure();
            using var fill = new LinearGradientBrush(plot, Color.FromArgb(90, color), Color.FromArgb(10, color), LinearGradientMode.Vertical);
            g.FillPath(fill, path);
        }
        using (var line = new Pen(color, 2f))
            g.DrawLines(line, points);
    }

    private static Color ColorFor(float percent) =>
        percent >= 85 ? Theme.Danger : percent >= 70 ? Theme.Warning : Theme.Accent;

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
