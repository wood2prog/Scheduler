using System.Drawing.Drawing2D;
using Scheduler.Application;

namespace Scheduler.UI;

/// <summary>
/// Draws the job-duration bell curve: a normal curve centered on the median job length, with
/// one dot per completed job placed on the curve at that job's length and its name written
/// above the dot in tiny vertical text. Jobs of identical length are spread side by side.
/// </summary>
public sealed class DurationReportPanel : Panel
{
    private const int MarginLeft = 50;
    private const int MarginRight = 30;
    private const int MarginTop = 50;
    private const int MarginBottom = 50;
    private const int DotRadius = 4;
    private const int DotSpacing = 10;
    private const int LabelGap = 6;
    private const int MaxLabelLength = 150;
    private const float CurvePeakFraction = 0.40f;

    private static readonly Color CurveColor = Color.SteelBlue;
    private static readonly Color CurveFillColor = Color.FromArgb(40, 70, 130, 180);

    private readonly DurationReport _report;
    private readonly Font _labelFont = new("Segoe UI", 6.5f);
    private readonly Font _axisFont = new("Segoe UI", 9f);
    private readonly Font _titleFont = new("Segoe UI", 12f, FontStyle.Bold);

    public DurationReportPanel(DurationReport report)
    {
        _report = report;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        BackColor = Color.White;
        ResizeRedraw = true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _labelFont.Dispose();
            _axisFont.Dispose();
            _titleFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private double Pdf(double x)
    {
        double z = (x - _report.Median) / _report.StdDev;
        return Math.Exp(-0.5 * z * z);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var plot = new Rectangle(MarginLeft, MarginTop,
            Math.Max(1, ClientSize.Width - MarginLeft - MarginRight),
            Math.Max(1, ClientSize.Height - MarginTop - MarginBottom));

        // X range covers every job and at least three standard deviations either side of the
        // median, so the curve's tails are visible. Lengths below zero days make no sense.
        double minX = Math.Max(0, Math.Min(_report.Points.Min(p => p.Days) - 1, _report.Median - 3 * _report.StdDev));
        double maxX = Math.Max(_report.Points.Max(p => p.Days) + 1, _report.Median + 3 * _report.StdDev);

        float ToX(double days) => plot.Left + (float)((days - minX) / (maxX - minX) * plot.Width);
        float ToY(double density) => plot.Bottom - (float)(density * plot.Height * CurvePeakFraction);

        TextRenderer.DrawText(g, "Completed Job Durations", _titleFont, new Rectangle(0, 8, ClientSize.Width, 28),
            Color.Black, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        DrawAxis(g, plot, minX, maxX, ToX);
        DrawCurve(g, plot, minX, maxX, ToX, ToY);
        DrawMedian(g, plot, ToX);
        DrawJobs(g, ToX, ToY);
    }

    private void DrawAxis(Graphics g, Rectangle plot, double minX, double maxX, Func<double, float> toX)
    {
        g.DrawLine(Pens.Gray, plot.Left, plot.Bottom, plot.Right, plot.Bottom);

        double step = NiceStep((maxX - minX) / 10);
        for (double d = Math.Ceiling(minX / step) * step; d <= maxX; d += step)
        {
            float x = toX(d);
            g.DrawLine(Pens.Gray, x, plot.Bottom, x, plot.Bottom + 4);
            TextRenderer.DrawText(g, d.ToString("0.#"), _axisFont,
                new Rectangle((int)x - 25, plot.Bottom + 6, 50, 18), Color.Black,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
        }

        TextRenderer.DrawText(g, "Job length (days)", _axisFont,
            new Rectangle(plot.Left, plot.Bottom + 26, plot.Width, 18), Color.DimGray,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
    }

    private static double NiceStep(double rough)
    {
        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(Math.Max(rough, 0.1))));
        double normalized = rough / magnitude;
        double nice = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10;
        return Math.Max(1, nice * magnitude);
    }

    private void DrawCurve(Graphics g, Rectangle plot, double minX, double maxX,
        Func<double, float> toX, Func<double, float> toY)
    {
        const int segments = 240;
        var curve = new PointF[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            double days = minX + (maxX - minX) * i / segments;
            curve[i] = new PointF(toX(days), toY(Pdf(days)));
        }

        var area = new PointF[segments + 3];
        area[0] = new PointF(curve[0].X, plot.Bottom);
        curve.CopyTo(area, 1);
        area[^1] = new PointF(curve[^1].X, plot.Bottom);

        using var fill = new SolidBrush(CurveFillColor);
        g.FillPolygon(fill, area);
        using var pen = new Pen(CurveColor, 2f);
        g.DrawLines(pen, curve);
    }

    private void DrawMedian(Graphics g, Rectangle plot, Func<double, float> toX)
    {
        float x = toX(_report.Median);
        using var pen = new Pen(Color.Firebrick, 1f) { DashStyle = DashStyle.Dash };
        g.DrawLine(pen, x, plot.Top, x, plot.Bottom);
        TextRenderer.DrawText(g, $"Median: {_report.Median:0.#} days", _axisFont,
            new Rectangle((int)x - 70, plot.Top - 22, 140, 18), Color.Firebrick,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.Top);
    }

    private void DrawJobs(Graphics g, Func<double, float> toX, Func<double, float> toY)
    {
        using var labelFormat = new StringFormat(StringFormatFlags.NoWrap)
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter
        };

        foreach (var group in _report.Points.GroupBy(p => p.Days))
        {
            var members = group.ToList();
            float curveX = toX(group.Key);
            float dotY = toY(Pdf(group.Key));

            for (int i = 0; i < members.Count; i++)
            {
                // Same-length jobs would sit on the exact same spot, so fan them out
                // horizontally around the curve position.
                float x = curveX + (i - (members.Count - 1) / 2f) * DotSpacing;
                var job = members[i].Job;

                using var dotBrush = new SolidBrush(CurveColor);
                g.FillEllipse(dotBrush, x - DotRadius, dotY - DotRadius, DotRadius * 2, DotRadius * 2);
                g.DrawEllipse(Pens.White, x - DotRadius, dotY - DotRadius, DotRadius * 2, DotRadius * 2);

                // Rotate -90 degrees about the dot so the text reads bottom-to-top, starting
                // just above the dot.
                var state = g.Save();
                g.TranslateTransform(x, dotY - DotRadius - LabelGap);
                g.RotateTransform(-90);
                var labelRect = new RectangleF(0, -_labelFont.Height / 2f, MaxLabelLength, _labelFont.Height);
                g.DrawString(job.Name, _labelFont, Brushes.Black, labelRect, labelFormat);
                g.Restore(state);
            }
        }
    }
}
