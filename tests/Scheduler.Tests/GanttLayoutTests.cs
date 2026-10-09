using System.Drawing;
using Scheduler.Application;
using Scheduler.Domain;
using Scheduler.UI;
using static Scheduler.Tests.Support.TestJobs;

namespace Scheduler.Tests;

public class GanttLayoutTests
{
    // 800x400 control with 17px scrollbars; the first day column is Day(1).
    private static GanttLayout Make(int scrollX = 0, int scrollY = 0, int rows = 10) =>
        new(Day(1), scrollX, scrollY, new Size(800, 400), 17, 17, rows);

    // ---- days ----------------------------------------------------------------------------

    [Fact]
    public void DayToX_FirstDayStartsAtTheNameColumnEdge() =>
        Assert.Equal(GanttLayout.NameColumnWidth, Make().DayToX(Day(1)));

    [Fact]
    public void DayToX_AdvancesOneDayWidthPerDay() =>
        Assert.Equal(GanttLayout.NameColumnWidth + 3 * GanttLayout.DayWidth, Make().DayToX(Day(4)));

    [Fact]
    public void DayToX_MovesLeftAsTheGridScrollsRight() =>
        Assert.Equal(GanttLayout.NameColumnWidth - 50 + GanttLayout.DayWidth, Make(scrollX: 50).DayToX(Day(2)));

    [Fact]
    public void SnapToDay_RoundsToTheNearestBoundary()
    {
        var layout = Make();
        int boundary = layout.DayToX(Day(5));

        Assert.Equal(Day(5), layout.SnapToDay(boundary));
        Assert.Equal(Day(5), layout.SnapToDay(boundary + GanttLayout.DayWidth / 2 - 1));
        Assert.Equal(Day(6), layout.SnapToDay(boundary + GanttLayout.DayWidth / 2 + 1));
        Assert.Equal(Day(5), layout.SnapToDay(boundary - GanttLayout.DayWidth / 2 + 1));
    }

    [Fact]
    public void SnapToDay_IsTheInverseOfDayToX_WhenScrolled()
    {
        var layout = Make(scrollX: 123);

        Assert.Equal(Day(9), layout.SnapToDay(layout.DayToX(Day(9))));
    }

    // ---- rows ----------------------------------------------------------------------------

    private static Point InRow(int row, int x = 300, int scrollY = 0) =>
        new(x, GanttLayout.HeaderHeight - scrollY + row * GanttLayout.RowHeight + 2);

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(9)]
    public void RowAt_FindsTheRowUnderThePointer(int row) =>
        Assert.Equal(row, Make().RowAt(InRow(row)));

    [Fact]
    public void RowAt_AccountsForVerticalScroll() =>
        Assert.Equal(4, Make(scrollY: 56).RowAt(InRow(4, scrollY: 56)));

    [Fact]
    public void RowAt_IsNullBelowTheLastRow() =>
        Assert.Null(Make(rows: 3).RowAt(InRow(3)));

    [Fact]
    public void RowAt_IsNullInTheHeader() =>
        Assert.Null(Make().RowAt(new Point(300, GanttLayout.HeaderHeight - 1)));

    [Fact]
    public void RowAt_IsNullOverTheScrollbars()
    {
        var layout = Make();

        Assert.Null(layout.RowAt(new Point(layout.BodyRight, 100)));
        Assert.Null(layout.RowAt(new Point(300, layout.BodyBottom)));
    }

    [Fact]
    public void RowAt_IncludesTheNameColumn_UnlessGridOnly()
    {
        var layout = Make();
        var inNameColumn = InRow(2, x: 10);

        Assert.Equal(2, layout.RowAt(inNameColumn));
        Assert.Null(layout.RowAt(inNameColumn, gridOnly: true));
        Assert.Equal(2, layout.RowAt(InRow(2, x: GanttLayout.NameColumnWidth), gridOnly: true));
    }

    // ---- seams ---------------------------------------------------------------------------

    private static List<PhaseSegment> Segments() =>
    [
        new(JobPhase.Design, Day(1), Day(5)),
        new(JobPhase.Construction, Day(5), Day(9)),
        new(JobPhase.Delivery, Day(9), Day(12))
    ];

    [Fact]
    public void NearestSeam_FindsTheSeamUnderThePointer()
    {
        var layout = Make();

        Assert.Equal(0, layout.NearestSeam(Segments(), layout.DayToX(Day(5))));
        Assert.Equal(1, layout.NearestSeam(Segments(), layout.DayToX(Day(9))));
    }

    [Fact]
    public void NearestSeam_AllowsAFewPixelsEitherSide()
    {
        var layout = Make();
        int seamX = layout.DayToX(Day(5));

        Assert.Equal(0, layout.NearestSeam(Segments(), seamX - GanttLayout.SeamGrabWidth));
        Assert.Equal(0, layout.NearestSeam(Segments(), seamX + GanttLayout.SeamGrabWidth));
        Assert.Null(layout.NearestSeam(Segments(), seamX + GanttLayout.SeamGrabWidth + 1));
    }

    [Fact]
    public void NearestSeam_IsNullAwayFromAnySeam()
    {
        var layout = Make();

        Assert.Null(layout.NearestSeam(Segments(), layout.DayToX(Day(7))));
    }

    [Fact]
    public void NearestSeam_IsNullWithASingleSegment()
    {
        var layout = Make();

        Assert.Null(layout.NearestSeam([new PhaseSegment(JobPhase.Design, Day(1), Day(5))], layout.DayToX(Day(1))));
    }

    [Fact]
    public void NearestSeam_PicksTheClosestWhenSeamsAreClose()
    {
        // A one-day Construction phase puts the two seams a day (32px) apart; the closer one wins.
        var layout = Make();
        var segments = new List<PhaseSegment>
        {
            new(JobPhase.Design, Day(1), Day(5)),
            new(JobPhase.Construction, Day(5), Day(6)),
            new(JobPhase.Delivery, Day(6), Day(9))
        };

        Assert.Equal(0, layout.NearestSeam(segments, layout.DayToX(Day(5)) + 1));
        Assert.Equal(1, layout.NearestSeam(segments, layout.DayToX(Day(6)) - 1));
    }
}
