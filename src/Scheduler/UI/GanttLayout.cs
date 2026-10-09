using Scheduler.Application;

namespace Scheduler.UI;

/// <summary>
/// The geometry of the Gantt chart at one moment: where days and rows fall on the control for the
/// current scroll position and size, and which row or seam a mouse position is over. Pure maths
/// with no controls involved, so it can be tested. All x/y values are client-area pixels.
/// </summary>
/// <param name="RangeStart">The date of the first day column.</param>
/// <param name="ScrollX">How far the grid is scrolled horizontally.</param>
/// <param name="ScrollY">How far the grid is scrolled vertically.</param>
/// <param name="VScrollWidth">Width of the vertical scrollbar, which covers the right edge.</param>
/// <param name="HScrollHeight">Height of the horizontal scrollbar, which covers the bottom edge.</param>
public sealed record GanttLayout(
    DateTime RangeStart,
    int ScrollX,
    int ScrollY,
    Size ClientSize,
    int VScrollWidth,
    int HScrollHeight,
    int RowCount)
{
    public const int NameColumnWidth = 160;
    public const int DayWidth = 32;
    public const int RowHeight = 28;
    public const int HeaderHeight = 36;
    public const int BarMargin = 4;

    /// <summary>How close (in pixels) the pointer must be to a seam to grab it.</summary>
    public const int SeamGrabWidth = 4;

    /// <summary>Right edge of the grid and name column, where the vertical scrollbar starts.</summary>
    public int BodyRight => ClientSize.Width - VScrollWidth;

    /// <summary>Bottom edge of the rows, where the horizontal scrollbar starts.</summary>
    public int BodyBottom => ClientSize.Height - HScrollHeight;

    /// <summary>The visible area of the day grid: right of the name column, below the header.</summary>
    public Rectangle GridBody => new(NameColumnWidth, HeaderHeight,
        Math.Max(0, BodyRight - NameColumnWidth), Math.Max(0, BodyBottom - HeaderHeight));

    /// <summary>X of the left edge of the first day column (it moves left as the grid scrolls right).</summary>
    public int OriginX => NameColumnWidth - ScrollX;

    /// <summary>Y of the top edge of the first row.</summary>
    public int OriginY => HeaderHeight - ScrollY;

    /// <summary>X of the left edge of a day's column.</summary>
    public int DayToX(DateTime day) => OriginX + (day - RangeStart).Days * DayWidth;

    /// <summary>The day boundary nearest to <paramref name="x"/>, as a date.</summary>
    public DateTime SnapToDay(int x) =>
        RangeStart.AddDays((int)Math.Round((x - OriginX) / (double)DayWidth));

    /// <summary>
    /// The row under <paramref name="location"/>, or null if it is outside the rows or past the
    /// last one. The name column counts unless <paramref name="gridOnly"/> is set.
    /// </summary>
    public int? RowAt(Point location, bool gridOnly = false)
    {
        int left = gridOnly ? NameColumnWidth : 0;
        if (location.X < left || location.X >= BodyRight || location.Y < HeaderHeight || location.Y >= BodyBottom)
        {
            return null;
        }

        int row = (location.Y - HeaderHeight + ScrollY) / RowHeight;
        return row >= 0 && row < RowCount ? row : null;
    }

    /// <summary>
    /// The seam within grabbing distance of <paramref name="x"/> (the closest if several), as the
    /// index of the segment before it: the seam between segments[i] and segments[i + 1], which sits
    /// at the start of segments[i + 1].
    /// </summary>
    public int? NearestSeam(IReadOnlyList<PhaseSegment> segments, int x)
    {
        int? best = null;
        int bestDistance = SeamGrabWidth + 1;
        for (int i = 0; i + 1 < segments.Count; i++)
        {
            int distance = Math.Abs(x - DayToX(segments[i + 1].Start));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }
}
