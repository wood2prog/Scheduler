using Scheduler.Application;
using Scheduler.Domain;
using static Scheduler.Tests.Support.TestJobs;

namespace Scheduler.Tests;

public class JobPhasesOtherTests
{
    private static readonly DateTime Today = Day(20);

    // ---- GetStage ------------------------------------------------------------------------

    [Theory]
    [InlineData(null, false, JobStage.NoPhases)]
    [InlineData(null, true, JobStage.NoPhases)]
    [InlineData(JobPhase.Prospect, false, JobStage.Prospect)]
    [InlineData(JobPhase.Design, false, JobStage.Design)]
    [InlineData(JobPhase.Construction, false, JobStage.Construction)]
    [InlineData(JobPhase.Delivery, false, JobStage.Delivery)]
    [InlineData(JobPhase.Delivery, true, JobStage.Finished)]
    public void GetStage_ReflectsPhaseAndCompleted(JobPhase? phase, bool completed, JobStage expected) =>
        Assert.Equal(expected, JobPhases.GetStage(Make(phase: phase, completed: completed)));

    [Fact]
    public void GetStage_OnlyDeliveryCanBeFinished() =>
        Assert.Equal(JobStage.Design, JobPhases.GetStage(Make(phase: JobPhase.Design, completed: true)));

    // ---- GetCompletedPhaseDays -----------------------------------------------------------

    private static Job FinishedJob() => Make(phase: JobPhase.Delivery, completed: true, start: 2, end: 14,
        constructionStart: 5, deliveryStart: 10);

    [Theory]
    [InlineData(JobPhase.Design, 3)]
    [InlineData(JobPhase.Construction, 5)]
    [InlineData(JobPhase.Delivery, 5)]
    public void FinishedJob_ReportsEachPhasesLength(JobPhase phase, int expectedDays) =>
        Assert.Equal(expectedDays, JobPhases.GetCompletedPhaseDays(FinishedJob(), phase, Today));

    [Fact]
    public void FinishedJob_PhaseLengthsAddUpToTheWholeJob()
    {
        var job = FinishedJob();

        var total = new[] { JobPhase.Design, JobPhase.Construction, JobPhase.Delivery }
            .Sum(p => JobPhases.GetCompletedPhaseDays(job, p, Today) ?? 0);

        Assert.Equal((job.EndDate - job.StartDate).Days + 1, total);
    }

    [Fact]
    public void PhaseInProgress_IsNotReported()
    {
        var inDesign = Make(phase: JobPhase.Design, start: 2);
        var inConstruction = Make(phase: JobPhase.Construction, start: 2, constructionStart: 5);
        var inDelivery = Make(phase: JobPhase.Delivery, start: 2, constructionStart: 5, deliveryStart: 8);

        Assert.Null(JobPhases.GetCompletedPhaseDays(inDesign, JobPhase.Design, Today));
        Assert.Null(JobPhases.GetCompletedPhaseDays(inConstruction, JobPhase.Construction, Today));
        Assert.Null(JobPhases.GetCompletedPhaseDays(inDelivery, JobPhase.Delivery, Today));
    }

    [Fact]
    public void CompletedEarlierPhases_AreReportedWhileLaterOnesRun()
    {
        var inDelivery = Make(phase: JobPhase.Delivery, start: 2, constructionStart: 5, deliveryStart: 8);

        Assert.Equal(3, JobPhases.GetCompletedPhaseDays(inDelivery, JobPhase.Design, Today));
        Assert.Equal(3, JobPhases.GetCompletedPhaseDays(inDelivery, JobPhase.Construction, Today));
    }

    [Fact]
    public void LaterPhases_AreNotReportedForEarlierStages()
    {
        var inConstruction = Make(phase: JobPhase.Construction, start: 2, constructionStart: 5);

        Assert.Null(JobPhases.GetCompletedPhaseDays(inConstruction, JobPhase.Delivery, Today));
    }

    [Fact]
    public void SkippedPhase_SpentNoDays_IsNotReported()
    {
        var job = Make(phase: JobPhase.Delivery, completed: true, start: 2, end: 9,
            constructionStart: 5, deliveryStart: 5);

        Assert.Null(JobPhases.GetCompletedPhaseDays(job, JobPhase.Construction, Today));
        Assert.Equal(3, JobPhases.GetCompletedPhaseDays(job, JobPhase.Design, Today));
    }

    [Theory]
    [InlineData(JobPhase.Design)]
    [InlineData(JobPhase.Construction)]
    [InlineData(JobPhase.Delivery)]
    public void UnphasedJobs_AreNeverReported(JobPhase phase) =>
        Assert.Null(JobPhases.GetCompletedPhaseDays(Make(start: 1, end: 9, completed: true), phase, Today));

    [Theory]
    [InlineData(JobPhase.Design)]
    [InlineData(JobPhase.Construction)]
    [InlineData(JobPhase.Delivery)]
    public void Prospects_AreNeverReported(JobPhase phase) =>
        Assert.Null(JobPhases.GetCompletedPhaseDays(Make(phase: JobPhase.Prospect), phase, Today));

    [Fact]
    public void ProspectPhase_ItselfHasNoReportableLength() =>
        Assert.Null(JobPhases.GetCompletedPhaseDays(FinishedJob(), JobPhase.Prospect, Today));

    // ---- SetPhaseStart -------------------------------------------------------------------

    [Fact]
    public void SetPhaseStart_Construction_MovesOnlyTheConstructionSeam()
    {
        var job = Make(phase: JobPhase.Delivery, start: 2, constructionStart: 5, deliveryStart: 8);

        JobPhases.SetPhaseStart(job, JobPhase.Construction, Day(6));

        Assert.Equal(Day(6), job.ConstructionStartDate);
        Assert.Equal(Day(8), job.DeliveryStartDate);
    }

    [Fact]
    public void SetPhaseStart_Delivery_MovesOnlyTheDeliverySeam()
    {
        var job = Make(phase: JobPhase.Delivery, start: 2, constructionStart: 5, deliveryStart: 8);

        JobPhases.SetPhaseStart(job, JobPhase.Delivery, Day(9));

        Assert.Equal(Day(5), job.ConstructionStartDate);
        Assert.Equal(Day(9), job.DeliveryStartDate);
    }

    [Fact]
    public void SetPhaseStart_DropsTimeOfDay()
    {
        var job = Make(phase: JobPhase.Construction, start: 2);

        JobPhases.SetPhaseStart(job, JobPhase.Construction, Day(6).AddHours(13));

        Assert.Equal(Day(6), job.ConstructionStartDate);
    }

    [Theory]
    [InlineData(JobPhase.Prospect)]
    [InlineData(JobPhase.Design)]
    public void SetPhaseStart_PhasesWithoutASeam_ChangeNothing(JobPhase phase)
    {
        var job = Make(phase: JobPhase.Delivery, start: 2, constructionStart: 5, deliveryStart: 8);

        JobPhases.SetPhaseStart(job, phase, Day(15));

        Assert.Equal(Day(2), job.StartDate);
        Assert.Equal(Day(5), job.ConstructionStartDate);
        Assert.Equal(Day(8), job.DeliveryStartDate);
    }

    [Fact]
    public void SetPhaseStart_ChangesWhatTheSegmentsShow()
    {
        var job = Make(phase: JobPhase.Construction, start: 2, constructionStart: 6);

        JobPhases.SetPhaseStart(job, JobPhase.Construction, Day(9));

        var design = JobPhases.GetSegments(job, Today).Single(s => s.Phase == JobPhase.Design);
        Assert.Equal(7, design.Days);
    }
}
