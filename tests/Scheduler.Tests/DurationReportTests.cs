using Scheduler.Application;
using Scheduler.Domain;
using static Scheduler.Tests.Support.TestJobs;

namespace Scheduler.Tests;

public class DurationReportTests
{
    private static List<DurationPoint> Points(params int[] days) =>
        days.Select((d, i) => new DurationPoint(Make(name: $"job{i}"), d)).ToList();

    // ---- DurationPoint.ForWholeJob -------------------------------------------------------

    [Fact]
    public void ForWholeJob_SingleDayJob_IsOneDay() =>
        Assert.Equal(1, DurationPoint.ForWholeJob(Make(start: 4, end: 4)).Days);

    [Fact]
    public void ForWholeJob_CountsBothEndDays() =>
        Assert.Equal(10, DurationPoint.ForWholeJob(Make(start: 0, end: 9)).Days);

    [Fact]
    public void ForWholeJob_IgnoresTimeOfDay()
    {
        var job = Make(start: 0, end: 2);
        job.StartDate = job.StartDate.AddHours(22);
        job.EndDate = job.EndDate.AddHours(1);

        Assert.Equal(3, DurationPoint.ForWholeJob(job).Days);
    }

    [Fact]
    public void ForWholeJob_KeepsTheJob()
    {
        var job = Make(name: "kept", start: 0, end: 1);

        Assert.Same(job, DurationPoint.ForWholeJob(job).Job);
    }

    // ---- minimum size --------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void FewerThanThreeJobs_GivesNoReport(int count) =>
        Assert.Null(DurationReport.Build("t", Points(Enumerable.Repeat(5, count).ToArray())));

    [Fact]
    public void ExactlyThreeJobs_GivesAReport()
    {
        Assert.Equal(3, DurationReport.MinimumJobs);
        Assert.NotNull(DurationReport.Build("t", Points(3, 5, 9)));
    }

    // ---- median --------------------------------------------------------------------------

    [Fact]
    public void Median_OddCount_IsTheMiddleValue() =>
        Assert.Equal(5, DurationReport.Build("t", Points(9, 3, 5))!.Median);

    [Fact]
    public void Median_EvenCount_IsTheAverageOfTheMiddleTwo() =>
        Assert.Equal(5, DurationReport.Build("t", Points(10, 2, 6, 4))!.Median);

    [Fact]
    public void Median_EvenCountWithOddSum_HasAHalf() =>
        Assert.Equal(4.5, DurationReport.Build("t", Points(1, 4, 5, 20))!.Median);

    [Fact]
    public void Median_IsNotPulledByOutliers() =>
        Assert.Equal(6, DurationReport.Build("t", Points(5, 6, 7, 400, 1))!.Median);

    // ---- standard deviation --------------------------------------------------------------

    [Fact]
    public void StdDev_IsTheSampleStandardDeviation() =>
        Assert.Equal(2.0, DurationReport.Build("t", Points(2, 4, 6))!.StdDev, 10);

    [Fact]
    public void StdDev_NeverCollapsesBelowOne_WhenAllJobsAreTheSameLength() =>
        Assert.Equal(1.0, DurationReport.Build("t", Points(7, 7, 7))!.StdDev);

    [Fact]
    public void StdDev_NeverCollapsesBelowOne_WhenJobsAreAlmostTheSame() =>
        Assert.Equal(1.0, DurationReport.Build("t", Points(5, 5, 6))!.StdDev);

    [Fact]
    public void StdDev_LargeSpreadIsKept()
    {
        var report = DurationReport.Build("t", Points(1, 50, 100))!;

        Assert.True(report.StdDev > 40);
    }

    // ---- shape of the result -------------------------------------------------------------

    [Fact]
    public void Points_AreOrderedByDaysThenName()
    {
        var jobs = new[]
        {
            new DurationPoint(Make(name: "b"), 5),
            new DurationPoint(Make(name: "z"), 2),
            new DurationPoint(Make(name: "a"), 5),
        };

        var report = DurationReport.Build("t", jobs)!;

        Assert.Equal(["z", "a", "b"], report.Points.Select(p => p.Job.Name));
    }

    [Fact]
    public void Title_IsCarriedThrough() =>
        Assert.Equal("My Title", DurationReport.Build("My Title", Points(1, 2, 3))!.Title);

    [Fact]
    public void AllPointsAreKept() =>
        Assert.Equal(5, DurationReport.Build("t", Points(1, 2, 3, 4, 5))!.Points.Count);

    [Fact]
    public void LazilyEnumeratedInput_IsAccepted()
    {
        IEnumerable<DurationPoint> Lazy()
        {
            yield return new DurationPoint(Make(name: "a"), 1);
            yield return new DurationPoint(Make(name: "b"), 2);
            yield return new DurationPoint(Make(name: "c"), 3);
        }

        Assert.Equal(2, DurationReport.Build("t", Lazy())!.Median);
    }
}
