using Scheduler.Application;
using Scheduler.Domain;
using static Scheduler.Tests.Support.TestJobs;

namespace Scheduler.Tests;

public class JobPhasesSegmentsTests
{
    private static readonly DateTime Today = Day(10);

    private static string Describe(Job job, DateTime? today = null) =>
        string.Join(" ", JobPhases.GetSegments(job, today ?? Today)
            .Select(s => $"{s.Phase}[{(s.Start - Origin).Days},{(s.EndExclusive - Origin).Days})"));

    // ---- jobs with nothing to draw -------------------------------------------------------

    [Fact]
    public void UnphasedJob_HasNoSegments() =>
        Assert.Empty(JobPhases.GetSegments(Make(start: 1, end: 5), Today));

    [Fact]
    public void Prospect_HasNoSegments_EvenWithATarget() =>
        Assert.Empty(JobPhases.GetSegments(Make(phase: JobPhase.Prospect, target: 20), Today));

    [Fact]
    public void FinishedJobWhoseEndPrecedesItsStart_HasNoSegments() =>
        Assert.Empty(JobPhases.GetSegments(
            Make(phase: JobPhase.Delivery, completed: true, start: 8, end: 3), Today));

    // ---- jobs in progress run through today ----------------------------------------------

    [Fact]
    public void Design_RunsFromStartThroughToday() =>
        Assert.Equal("Design[2,11)", Describe(Make(phase: JobPhase.Design, start: 2)));

    [Fact]
    public void Design_StartingToday_IsOneDay() =>
        Assert.Equal("Design[10,11)", Describe(Make(phase: JobPhase.Design, start: 10)));

    [Fact]
    public void Design_StartingInTheFuture_IsOneDayAtItsStart() =>
        Assert.Equal("Design[15,16)", Describe(Make(phase: JobPhase.Design, start: 15)));

    [Fact]
    public void Construction_SplitsAtItsSeam() =>
        Assert.Equal("Design[2,6) Construction[6,11)",
            Describe(Make(phase: JobPhase.Construction, start: 2, constructionStart: 6)));

    [Fact]
    public void Delivery_HasThreeStretches() =>
        Assert.Equal("Design[2,6) Construction[6,8) Delivery[8,11)",
            Describe(Make(phase: JobPhase.Delivery, start: 2, constructionStart: 6, deliveryStart: 8)));

    [Fact]
    public void InProgressJob_GrowsAsTheDaysPass()
    {
        var job = Make(phase: JobPhase.Construction, start: 2, constructionStart: 6);

        Assert.Equal("Design[2,6) Construction[6,11)", Describe(job, Day(10)));
        Assert.Equal("Design[2,6) Construction[6,16)", Describe(job, Day(15)));
    }

    // ---- finished jobs stop at their end date --------------------------------------------

    [Fact]
    public void FinishedJob_EndsOnItsEndDateInclusive() =>
        Assert.Equal("Design[2,6) Construction[6,8) Delivery[8,13)",
            Describe(Make(phase: JobPhase.Delivery, completed: true, start: 2, end: 12,
                constructionStart: 6, deliveryStart: 8)));

    [Fact]
    public void FinishedJob_DoesNotGrowAsDaysPass()
    {
        var job = Make(phase: JobPhase.Delivery, completed: true, start: 2, end: 12,
            constructionStart: 6, deliveryStart: 8);

        Assert.Equal(Describe(job, Day(12)), Describe(job, Day(400)));
    }

    // ---- empty phases are omitted --------------------------------------------------------

    [Fact]
    public void ZeroLengthDesign_IsOmitted() =>
        Assert.Equal("Construction[2,11)",
            Describe(Make(phase: JobPhase.Construction, start: 2, constructionStart: 2)));

    [Fact]
    public void ZeroLengthConstruction_IsOmitted() =>
        Assert.Equal("Design[2,6) Delivery[6,11)",
            Describe(Make(phase: JobPhase.Delivery, start: 2, constructionStart: 6, deliveryStart: 6)));

    [Fact]
    public void SkippedConstructionAndDesign_LeavesOnlyDelivery() =>
        Assert.Equal("Delivery[2,11)",
            Describe(Make(phase: JobPhase.Delivery, start: 2, constructionStart: 2, deliveryStart: 2)));

    // ---- missing seams -------------------------------------------------------------------

    [Fact]
    public void Construction_WithoutASeamDate_IsAllConstruction() =>
        Assert.Equal("Construction[2,11)", Describe(Make(phase: JobPhase.Construction, start: 2)));

    [Fact]
    public void Delivery_WithoutAnyDates_IsAllDelivery() =>
        Assert.Equal("Delivery[2,11)", Describe(Make(phase: JobPhase.Delivery, start: 2)));

    [Fact]
    public void Delivery_WithOnlyADeliverySeam_TreatsConstructionAsStartingAtJobStart() =>
        Assert.Equal("Construction[2,8) Delivery[8,11)",
            Describe(Make(phase: JobPhase.Delivery, start: 2, deliveryStart: 8)));

    [Fact]
    public void Delivery_WithOnlyAConstructionSeam_StartsDeliveryWhereConstructionStarts() =>
        Assert.Equal("Design[2,6) Delivery[6,11)",
            Describe(Make(phase: JobPhase.Delivery, start: 2, constructionStart: 6)));

    // ---- impossible dates are clamped, never thrown on -----------------------------------

    [Fact]
    public void ConstructionSeamBeforeStart_IsClampedToStart() =>
        Assert.Equal("Construction[2,11)",
            Describe(Make(phase: JobPhase.Construction, start: 2, constructionStart: -5)));

    [Fact]
    public void ConstructionSeamAfterToday_IsClampedToTheEnd() =>
        Assert.Equal("Design[2,11)",
            Describe(Make(phase: JobPhase.Construction, start: 2, constructionStart: 30)));

    [Fact]
    public void DeliverySeamBeforeConstructionSeam_IsClampedToIt() =>
        Assert.Equal("Design[2,6) Delivery[6,11)",
            Describe(Make(phase: JobPhase.Delivery, start: 2, constructionStart: 6, deliveryStart: 3)));

    [Fact]
    public void SeamsBeyondAFinishedJobsEnd_AreClamped() =>
        Assert.Equal("Design[2,13)",
            Describe(Make(phase: JobPhase.Delivery, completed: true, start: 2, end: 12,
                constructionStart: 50, deliveryStart: 60)));

    // ---- details -------------------------------------------------------------------------

    [Fact]
    public void TimeOfDayInDates_IsIgnored()
    {
        var job = Make(phase: JobPhase.Construction, start: 2, constructionStart: 6);
        job.StartDate = job.StartDate.AddHours(17);
        job.ConstructionStartDate = job.ConstructionStartDate!.Value.AddHours(3);

        Assert.Equal("Design[2,6) Construction[6,11)", Describe(job, Today.AddHours(22)));
    }

    [Fact]
    public void Days_IsTheLengthOfTheStretch()
    {
        var segment = new PhaseSegment(JobPhase.Design, Day(2), Day(6));

        Assert.Equal(4, segment.Days);
    }

    [Theory]
    [InlineData(JobPhase.Design, 2, null, null, false, 2)]
    [InlineData(JobPhase.Construction, 2, 5, null, false, 2)]
    [InlineData(JobPhase.Delivery, 2, 5, 8, false, 12)]
    [InlineData(JobPhase.Delivery, 2, 5, 8, true, 12)]
    [InlineData(JobPhase.Delivery, 2, 2, 2, true, 7)]
    [InlineData(JobPhase.Construction, 4, 40, null, false, 6)]
    public void StretchesAreContiguousAndAddUpToTheJobsLength(
        JobPhase phase, int start, int? constructionStart, int? deliveryStart, bool completed, int end)
    {
        var job = Make(phase: phase, start: start, end: end, completed: completed,
            constructionStart: constructionStart, deliveryStart: deliveryStart);

        var segments = JobPhases.GetSegments(job, Today);

        Assert.NotEmpty(segments);
        Assert.Equal(Day(start), segments[0].Start);
        for (int i = 1; i < segments.Count; i++)
        {
            Assert.Equal(segments[i - 1].EndExclusive, segments[i].Start);
        }

        var lastDay = completed ? Day(end) : Day(Math.Max(10, start));
        Assert.Equal(lastDay.AddDays(1), segments[^1].EndExclusive);
        Assert.All(segments, s => Assert.True(s.Days > 0));
    }
}
