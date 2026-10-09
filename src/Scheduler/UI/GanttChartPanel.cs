using System.Drawing.Drawing2D;
using Scheduler.Application;
using Scheduler.Domain;

namespace Scheduler.UI;

/// <summary>
/// Custom-drawn Gantt chart: one row per job (top to bottom), one column per day
/// (left to right, horizontally scrollable). A colored bar spans each job's row
/// from its start-day column to its end-day column. The date header row and the
/// job-name column stay pinned while the grid body scrolls underneath them.
/// </summary>
public sealed class GanttChartPanel : Panel
{
    private const int NameColumnWidth = 160;
    private const int DayWidth = 32;
    private const int RowHeight = 28;
    private const int HeaderHeight = 36;
    private const int BarMargin = 4;

    private static readonly Color[] BarPalette =
    [
        Color.SteelBlue,
        Color.MediumSeaGreen,
        Color.IndianRed,
        Color.Goldenrod,
        Color.MediumPurple,
        Color.DarkTurquoise,
        Color.Coral,
        Color.SlateGray
    ];

    private static readonly Color DesignColor = Color.RoyalBlue;
    private static readonly Color ConstructionColor = Color.DarkOrange;
    private static readonly Color DeliveryColor = Color.ForestGreen;
    private static readonly Color OverdueColor = Color.Firebrick;
    private static readonly Color TargetMarkerColor = Color.Gold;

    private static readonly Color HeaderBackColor = Color.FromArgb(240, 240, 240);
    private static readonly Color WeekendColor = Color.FromArgb(248, 248, 248);
    private static readonly Color TodayColor = Color.FromArgb(255, 250, 205);
    private static readonly Color GridLineColor = Color.FromArgb(225, 225, 225);
    private static readonly Color RowAltColor = Color.FromArgb(250, 250, 250);

    private readonly HScrollBar _hScrollBar;
    private readonly VScrollBar _vScrollBar;

    private IReadOnlyList<Job> _jobs = [];
    private Dictionary<int, IReadOnlyList<PhaseSegment>> _segments = [];
    private DateTime _rangeStart = DateTime.Today;
    private int _totalDays = 30;

    public event Action<Job>? JobClicked;

    public GanttChartPanel()
    {
        // A plain Panel's AutoScroll uses the OS's ScrollWindowEx to bit-shift the
        // existing pixels on every scroll step, which physically drags the "pinned"
        // header/name-column content along with everything else before we get a
        // chance to repaint it, showing up as jitter/streaks. Driving scrolling
        // manually with plain scrollbars avoids any pixel blitting: every scroll
        // step just changes a value and triggers a normal, full, double-buffered
        // repaint, so the pinned regions never visibly move.
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        BackColor = Color.White;

        _hScrollBar = new HScrollBar { Dock = DockStyle.Bottom, SmallChange = DayWidth };
        _vScrollBar = new VScrollBar { Dock = DockStyle.Right, SmallChange = RowHeight };
        _hScrollBar.ValueChanged += (_, _) => Invalidate();
        _vScrollBar.ValueChanged += (_, _) => Invalidate();

        Controls.Add(_hScrollBar);
        Controls.Add(_vScrollBar);

        UpdateScrollBars();
    }

    /// <param name="getSegments">The phase stretches of a phased job (empty for unphased jobs and Prospects).</param>
    public void SetJobs(IReadOnlyList<Job> jobs, Func<Job, IReadOnlyList<PhaseSegment>> getSegments)
    {
        _jobs = jobs;
        _segments = jobs.Where(j => j.Phase is not null).ToDictionary(j => j.Id, getSegments);

        // The visible range covers everything drawn: plain bars, phase stretches and delivery
        // targets. A Prospect without a target draws nothing, so it doesn't widen the range.
        var dates = new List<DateTime>();
        foreach (var job in _jobs)
        {
            if (job.Phase is null)
            {
                dates.Add(job.StartDate.Date);
                dates.Add(job.EndDate.Date);
            }
            else
            {
                foreach (var segment in _segments[job.Id])
                {
                    dates.Add(segment.Start);
                    dates.Add(segment.EndExclusive.AddDays(-1));
                }
            }

            if (job.DeliveryTargetDate is { } target)
            {
                dates.Add(target.Date);
            }
        }

        if (dates.Count == 0)
        {
            _rangeStart = DateTime.Today;
            _totalDays = 30;
        }
        else
        {
            _rangeStart = dates.Min().AddDays(-1);
            _totalDays = Math.Max(1, (dates.Max() - _rangeStart).Days + 2);
        }

        UpdateScrollBars();
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateScrollBars();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        var bar = ModifierKeys == Keys.Shift ? (ScrollBar)_hScrollBar : _vScrollBar;
        int step = ModifierKeys == Keys.Shift ? DayWidth : RowHeight;
        int maxValue = Math.Max(bar.Minimum, bar.Maximum - bar.LargeChange + 1);
        bar.Value = Math.Clamp(bar.Value - Math.Sign(e.Delta) * step, bar.Minimum, maxValue);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button == MouseButtons.Left && GetJobAt(e.Location) is { } job)
        {
            JobClicked?.Invoke(job);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = GetJobAt(e.Location) is null ? Cursors.Default : Cursors.Hand;
    }

    private Job? GetJobAt(Point location)
    {
        int bodyTop = HeaderHeight;
        int bodyBottom = ClientSize.Height - _hScrollBar.Height;
        int bodyRight = ClientSize.Width - _vScrollBar.Width;

        bool inNameColumn = location.X >= 0 && location.X < NameColumnWidth;
        bool inGridBody = location.X >= NameColumnWidth && location.X < bodyRight;
        if (location.Y < bodyTop || location.Y >= bodyBottom || (!inNameColumn && !inGridBody))
        {
            return null;
        }

        int rowIndex = (location.Y - bodyTop + _vScrollBar.Value) / RowHeight;
        return rowIndex >= 0 && rowIndex < _jobs.Count ? _jobs[rowIndex] : null;
    }

    private void UpdateScrollBars()
    {
        int contentWidth = _totalDays * DayWidth;
        int contentHeight = _jobs.Count * RowHeight;
        int viewportWidth = Math.Max(1, ClientSize.Width - NameColumnWidth - _vScrollBar.Width);
        int viewportHeight = Math.Max(1, ClientSize.Height - HeaderHeight - _hScrollBar.Height);

        _hScrollBar.Value = 0;
        _hScrollBar.Maximum = Math.Max(0, contentWidth - 1);
        _hScrollBar.LargeChange = Math.Max(1, Math.Min(viewportWidth, contentWidth));

        _vScrollBar.Value = 0;
        _vScrollBar.Maximum = Math.Max(0, contentHeight - 1);
        _vScrollBar.LargeChange = Math.Max(1, Math.Min(viewportHeight, contentHeight));
    }

    // NOTE: text is drawn with TextRenderer (GDI), which does not reliably honor a
    // GDI+ Graphics.TranslateTransform. So instead of transforming the Graphics and
    // drawing at local (0,0)-relative coordinates, every rectangle below is computed
    // in final absolute (client-area) pixel coordinates up front, for both the shape
    // drawing (FillRectangle/DrawLine) and the text drawing. This keeps the two in
    // lock-step no matter how scrolling offsets are applied.
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);

        int bodyLeft = NameColumnWidth;
        int bodyTop = HeaderHeight;
        int bodyWidth = Math.Max(0, ClientSize.Width - bodyLeft - _vScrollBar.Width);
        int bodyHeight = Math.Max(0, ClientSize.Height - bodyTop - _hScrollBar.Height);

        int dx = bodyLeft - _hScrollBar.Value;
        int dy = bodyTop - _vScrollBar.Value;

        var state = g.Save();
        g.SetClip(new Rectangle(bodyLeft, bodyTop, bodyWidth, bodyHeight));
        DrawGridAndBars(g, dx, dy);
        g.Restore(state);

        state = g.Save();
        g.SetClip(new Rectangle(bodyLeft, 0, bodyWidth, HeaderHeight));
        DrawHeader(g, dx);
        g.Restore(state);

        state = g.Save();
        g.SetClip(new Rectangle(0, bodyTop, NameColumnWidth, bodyHeight));
        DrawNameColumn(g, dy);
        g.Restore(state);

        using var cornerBrush = new SolidBrush(HeaderBackColor);
        g.FillRectangle(cornerBrush, 0, 0, NameColumnWidth, HeaderHeight);
        g.DrawRectangle(Pens.Gray, 0, 0, NameColumnWidth - 1, HeaderHeight - 1);
    }

    private void DrawGridAndBars(Graphics g, int dx, int dy)
    {
        int gridWidth = _totalDays * DayWidth;
        int gridHeight = _jobs.Count * RowHeight;
        var today = DateTime.Today;

        for (int d = 0; d < _totalDays; d++)
        {
            int x = dx + d * DayWidth;
            var date = _rangeStart.AddDays(d);
            Color? fill = date.Date == today ? TodayColor
                : date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? WeekendColor
                : null;
            if (fill is { } c)
            {
                using var brush = new SolidBrush(c);
                g.FillRectangle(brush, x, dy, DayWidth, gridHeight);
            }
            g.DrawLine(Pens.LightGray, x, dy, x, dy + gridHeight);
        }
        g.DrawLine(Pens.LightGray, dx + gridWidth, dy, dx + gridWidth, dy + gridHeight);

        for (int i = 0; i < _jobs.Count; i++)
        {
            int y = dy + i * RowHeight;
            if (i % 2 == 1)
            {
                using var rowBrush = new SolidBrush(RowAltColor);
                g.FillRectangle(rowBrush, dx, y, gridWidth, RowHeight);
            }
            g.DrawLine(new Pen(GridLineColor), dx, y + RowHeight, dx + gridWidth, y + RowHeight);

            var job = _jobs[i];
            if (job.Phase is null)
            {
                DrawPlainBar(g, job, dx, y);
            }
            else
            {
                DrawPhasedBar(g, job, dx, y);
            }

            if (job.DeliveryTargetDate is { } target)
            {
                DrawTargetMarker(g, target.Date, dx, y);
            }
        }
    }

    private void DrawPlainBar(Graphics g, Job job, int dx, int y)
    {
        int startOffset = (job.StartDate.Date - _rangeStart).Days;
        int endOffset = (job.EndDate.Date - _rangeStart).Days;
        int barX = dx + startOffset * DayWidth;
        int barWidth = Math.Max(DayWidth, (endOffset - startOffset + 1) * DayWidth);

        var barRect = new Rectangle(barX, y + BarMargin, barWidth, RowHeight - BarMargin * 2);
        if (job.Completed)
        {
            using var hatchBrush = new HatchBrush(HatchStyle.LightUpwardDiagonal, Color.Gray, Color.Gainsboro);
            g.FillRectangle(hatchBrush, barRect);
        }
        else
        {
            var color = BarPalette[Math.Abs(job.Id) % BarPalette.Length];
            using var barBrush = new SolidBrush(color);
            g.FillRectangle(barBrush, barRect);
        }
        g.DrawRectangle(Pens.White, barRect.X, barRect.Y, barRect.Width - 1, barRect.Height - 1);

        DrawBarText(g, job.Name, barRect, job.Completed ? Color.Black : Color.White);
    }

    // One colored stretch per phase. Finished jobs are drawn faded. The part of Delivery that
    // runs past the delivery target is drawn in the overdue color.
    private void DrawPhasedBar(Graphics g, Job job, int dx, int y)
    {
        var segments = _segments[job.Id];
        if (segments.Count == 0)
        {
            return;
        }

        foreach (var segment in segments)
        {
            var color = segment.Phase switch
            {
                JobPhase.Design => DesignColor,
                JobPhase.Construction => ConstructionColor,
                _ => DeliveryColor
            };
            if (job.Completed)
            {
                color = Fade(color);
            }

            var overdueStart = segment.EndExclusive;
            if (segment.Phase == JobPhase.Delivery && job.DeliveryTargetDate is { } target)
            {
                overdueStart = Max(segment.Start, target.Date.AddDays(1));
                overdueStart = overdueStart > segment.EndExclusive ? segment.EndExclusive : overdueStart;
            }

            FillSpan(g, segment.Start, overdueStart, dx, y, color);
            FillSpan(g, overdueStart, segment.EndExclusive, dx, y, job.Completed ? Fade(OverdueColor) : OverdueColor);
        }

        var first = segments[0].Start;
        var last = segments[^1].EndExclusive;
        var span = SpanRect(first, last, dx, y);
        DrawBarText(g, job.Name, span, job.Completed ? Color.Black : Color.White);
    }

    private Rectangle SpanRect(DateTime start, DateTime endExclusive, int dx, int y) =>
        new(dx + (start - _rangeStart).Days * DayWidth, y + BarMargin,
            (endExclusive - start).Days * DayWidth, RowHeight - BarMargin * 2);

    private void FillSpan(Graphics g, DateTime start, DateTime endExclusive, int dx, int y, Color color)
    {
        if (endExclusive <= start)
        {
            return;
        }

        var rect = SpanRect(start, endExclusive, dx, y);
        using var brush = new SolidBrush(color);
        g.FillRectangle(brush, rect);
        g.DrawRectangle(Pens.White, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
    }

    private void DrawBarText(Graphics g, string text, Rectangle barRect, Color color)
    {
        var textRect = Rectangle.Inflate(barRect, -4, 0);
        TextRenderer.DrawText(g, text, Font, textRect, color,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }

    // A diamond centered on the delivery target day.
    private void DrawTargetMarker(Graphics g, DateTime target, int dx, int y)
    {
        const int half = 8;
        int cx = dx + (target - _rangeStart).Days * DayWidth + DayWidth / 2;
        int cy = y + RowHeight / 2;
        var diamond = new[]
        {
            new Point(cx, cy - half),
            new Point(cx + half, cy),
            new Point(cx, cy + half),
            new Point(cx - half, cy)
        };

        var previous = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(TargetMarkerColor);
        g.FillPolygon(brush, diamond);
        using var pen = new Pen(Color.FromArgb(60, 60, 60), 1.5f);
        g.DrawPolygon(pen, diamond);
        g.SmoothingMode = previous;
    }

    private static Color Fade(Color color) => Color.FromArgb(
        color.R + (255 - color.R) * 55 / 100,
        color.G + (255 - color.G) * 55 / 100,
        color.B + (255 - color.B) * 55 / 100);

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    private void DrawHeader(Graphics g, int dx)
    {
        int gridWidth = _totalDays * DayWidth;

        using var headerBrush = new SolidBrush(HeaderBackColor);
        g.FillRectangle(headerBrush, dx, 0, gridWidth, HeaderHeight);

        var today = DateTime.Today;
        for (int d = 0; d < _totalDays; d++)
        {
            int x = dx + d * DayWidth;
            var date = _rangeStart.AddDays(d);

            if (date.Day == 1 || d == 0)
            {
                TextRenderer.DrawText(g, date.ToString("MMM"), Font, new Rectangle(x, 0, DayWidth * 3, 14),
                    Color.DimGray, TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            }

            var dayRect = new Rectangle(x, 14, DayWidth, HeaderHeight - 14);
            var textColor = date.Date == today ? Color.Firebrick : Color.Black;
            TextRenderer.DrawText(g, date.Day.ToString(), Font, dayRect, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            g.DrawLine(Pens.LightGray, x, 0, x, HeaderHeight);
        }

        g.DrawLine(Pens.Gray, dx, HeaderHeight - 1, dx + gridWidth, HeaderHeight - 1);
    }

    private void DrawNameColumn(Graphics g, int dy)
    {
        int columnHeight = _jobs.Count * RowHeight;

        using var backBrush = new SolidBrush(BackColor);
        g.FillRectangle(backBrush, 0, dy, NameColumnWidth, columnHeight);

        for (int i = 0; i < _jobs.Count; i++)
        {
            int y = dy + i * RowHeight;
            var job = _jobs[i];

            if (i % 2 == 1)
            {
                using var rowBrush = new SolidBrush(RowAltColor);
                g.FillRectangle(rowBrush, 0, y, NameColumnWidth, RowHeight);
            }

            var textRect = new Rectangle(6, y, NameColumnWidth - 10, RowHeight);
            TextRenderer.DrawText(g, job.Name, Font, textRect, Color.Black,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            g.DrawLine(Pens.LightGray, 0, y + RowHeight, NameColumnWidth, y + RowHeight);
        }

        g.DrawLine(Pens.Gray, NameColumnWidth - 1, dy, NameColumnWidth - 1, dy + columnHeight);
    }
}
