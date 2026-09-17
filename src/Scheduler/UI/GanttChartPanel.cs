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

    private static readonly Color HeaderBackColor = Color.FromArgb(240, 240, 240);
    private static readonly Color WeekendColor = Color.FromArgb(248, 248, 248);
    private static readonly Color TodayColor = Color.FromArgb(255, 250, 205);
    private static readonly Color GridLineColor = Color.FromArgb(225, 225, 225);
    private static readonly Color RowAltColor = Color.FromArgb(250, 250, 250);

    private IReadOnlyList<Job> _jobs = [];
    private DateTime _rangeStart = DateTime.Today;
    private int _totalDays = 30;

    public GanttChartPanel()
    {
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        AutoScroll = true;
        BackColor = Color.White;
        RecalculateScrollSize();
    }

    public void SetJobs(IReadOnlyList<Job> jobs)
    {
        _jobs = jobs;

        if (_jobs.Count == 0)
        {
            _rangeStart = DateTime.Today;
            _totalDays = 30;
        }
        else
        {
            var earliestStart = _jobs.Min(j => j.StartDate.Date);
            var latestEnd = _jobs.Max(j => j.EndDate.Date);
            _rangeStart = earliestStart.AddDays(-1);
            _totalDays = Math.Max(1, (latestEnd.Date - _rangeStart).Days + 2);
        }

        RecalculateScrollSize();
        Invalidate();
    }

    private void RecalculateScrollSize()
    {
        AutoScrollMinSize = new Size(
            NameColumnWidth + _totalDays * DayWidth,
            HeaderHeight + _jobs.Count * RowHeight);
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

        var offset = AutoScrollPosition;
        int bodyLeft = NameColumnWidth;
        int bodyTop = HeaderHeight;
        int bodyWidth = Math.Max(0, ClientSize.Width - bodyLeft);
        int bodyHeight = Math.Max(0, ClientSize.Height - bodyTop);

        int dx = bodyLeft + offset.X;
        int dy = bodyTop + offset.Y;

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
            int startOffset = (job.StartDate.Date - _rangeStart).Days;
            int endOffset = (job.EndDate.Date - _rangeStart).Days;
            int barX = dx + startOffset * DayWidth;
            int barWidth = Math.Max(DayWidth, (endOffset - startOffset + 1) * DayWidth);

            var barRect = new Rectangle(barX, y + BarMargin, barWidth, RowHeight - BarMargin * 2);
            var color = BarPalette[Math.Abs(job.Id) % BarPalette.Length];
            using var barBrush = new SolidBrush(color);
            g.FillRectangle(barBrush, barRect);
            g.DrawRectangle(Pens.White, barRect.X, barRect.Y, barRect.Width - 1, barRect.Height - 1);

            var textRect = Rectangle.Inflate(barRect, -4, 0);
            TextRenderer.DrawText(g, job.Name, Font, textRect, Color.White,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
    }

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
