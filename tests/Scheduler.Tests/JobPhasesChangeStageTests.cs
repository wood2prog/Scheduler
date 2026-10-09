using Scheduler.Application;
using Scheduler.Domain;
using Scheduler.Tests.Support;
using static Scheduler.Tests.Support.TestJobs;

namespace Scheduler.Tests;

public class JobPhasesChangeStageTests
{
    private static readonly DateTime Today = Day(10);

    // ---- forward moves -------------------------------------------------------------------

    [Fact]
    public void ProspectToDesign_SetsStartToToday()
    {
        var job = Make(phase: JobPhase.Prospect, start: 0, end: 0);

        JobPhases.ChangeStage(job, JobStage.Design, Today);

        Assert.Equal(JobPhase.Design, job.Phase);
        Assert.Equal(Today, job.StartDate);
        Assert.Equal(Today, job.EndDate);
        Assert.False(job.Completed);
        Assert.Null(job.ConstructionStartDate);
        Assert.Null(job.DeliveryStartDate);
    }

    [Fact]
    public void DesignToConstruction_StampsConstructionStart_KeepsStart()
    {
        var job = Make(phase: JobPhase.Design, start: 2, end: 5);

        JobPhases.ChangeStage(job, JobStage.Construction, Today);

        Assert.Equal(JobPhase.Construction, job.Phase);
        Assert.Equal(Day(2), job.StartDate);
        Assert.Equal(Today, job.ConstructionStartDate);
        Assert.Null(job.DeliveryStartDate);
        Assert.Equal(Today, job.EndDate);
    }

    [Fact]
    public void ConstructionToDelivery_StampsDeliveryStart_KeepsConstructionStart()
    {
        var job = Make(phase: JobPhase.Construction, start: 2, constructionStart: 6);

        JobPhases.ChangeStage(job, JobStage.Delivery, Today);

        Assert.Equal(JobPhase.Delivery, job.Phase);
        Assert.Equal(Day(6), job.ConstructionStartDate);
        Assert.Equal(Today, job.DeliveryStartDate);
        Assert.False(job.Completed);
    }

    [Fact]
    public void DeliveryToFinished_CompletesAndSetsEndToToday_KeepsSeams()
    {
        var job = Make(phase: JobPhase.Delivery, start: 2, constructionStart: 6, deliveryStart: 8);

        JobPhases.ChangeStage(job, JobStage.Finished, Today);

        Assert.Equal(JobPhase.Delivery, job.Phase);
        Assert.True(job.Completed);
        Assert.Equal(Today, job.EndDate);
        Assert.Equal(Day(6), job.ConstructionStartDate);
        Assert.Equal(Day(8), job.DeliveryStartDate);
        Assert.Equal(JobStage.Finished, JobPhases.GetStage(job));
    }

    [Fact]
    public void ProspectToConstruction_SkipsDesign_StampsStartAndConstructionOnly()
    {
        var job = Make(phase: JobPhase.Prospect);

        JobPhases.ChangeStage(job, JobStage.Construction, Today);

        Assert.Equal(Today, job.StartDate);
        Assert.Equal(Today, job.ConstructionStartDate);
        Assert.Null(job.DeliveryStartDate);
    }

    [Fact]
    public void ProspectToDelivery_StampsBothSeams()
    {
        var job = Make(phase: JobPhase.Prospect);

        JobPhases.ChangeStage(job, JobStage.Delivery, Today);

        Assert.Equal(Today, job.StartDate);
        Assert.Equal(Today, job.ConstructionStartDate);
        Assert.Equal(Today, job.DeliveryStartDate);
    }

    [Fact]
    public void ProspectToFinished_StampsEverythingAndCompletes()
    {
        var job = Make(phase: JobPhase.Prospect);

        JobPhases.ChangeStage(job, JobStage.Finished, Today);

        Assert.Equal(Today, job.StartDate);
        Assert.Equal(Today, job.ConstructionStartDate);
        Assert.Equal(Today, job.DeliveryStartDate);
        Assert.Equal(Today, job.EndDate);
        Assert.True(job.Completed);
    }

    [Fact]
    public void DesignToFinished_StampsBothSeamsAtToday()
    {
        var job = Make(phase: JobPhase.Design, start: 2);

        JobPhases.ChangeStage(job, JobStage.Finished, Today);

        Assert.Equal(Day(2), job.StartDate);
        Assert.Equal(Today, job.ConstructionStartDate);
        Assert.Equal(Today, job.DeliveryStartDate);
        Assert.Equal(Today, job.EndDate);
    }

    [Fact]
    public void LeavingProspect_OverridesAnyEarlierStartDate()
    {
        var job = Make(phase: JobPhase.Prospect, start: -30, end: -30);

        JobPhases.ChangeStage(job, JobStage.Design, Today);

        Assert.Equal(Today, job.StartDate);
    }

    // ---- backward moves ------------------------------------------------------------------

    [Fact]
    public void FinishedToDelivery_ReopensJob_KeepsSeams()
    {
        var job = Make(phase: JobPhase.Delivery, completed: true, start: 2, end: 12, constructionStart: 6, deliveryStart: 8);

        JobPhases.ChangeStage(job, JobStage.Delivery, Today);

        Assert.False(job.Completed);
        Assert.Equal(Today, job.EndDate);
        Assert.Equal(Day(6), job.ConstructionStartDate);
        Assert.Equal(Day(8), job.DeliveryStartDate);
        Assert.Equal(JobStage.Delivery, JobPhases.GetStage(job));
    }

    [Fact]
    public void FinishedToConstruction_ClearsDeliveryStart_KeepsConstructionStart()
    {
        var job = Make(phase: JobPhase.Delivery, completed: true, start: 2, end: 12, constructionStart: 6, deliveryStart: 8);

        JobPhases.ChangeStage(job, JobStage.Construction, Today);

        Assert.Equal(JobPhase.Construction, job.Phase);
        Assert.False(job.Completed);
        Assert.Null(job.DeliveryStartDate);
        Assert.Equal(Day(6), job.ConstructionStartDate);
        Assert.Equal(Today, job.EndDate);
    }

    [Fact]
    public void FinishedToDesign_ClearsBothSeams()
    {
        var job = Make(phase: JobPhase.Delivery, completed: true, start: 2, end: 12, constructionStart: 6, deliveryStart: 8);

        JobPhases.ChangeStage(job, JobStage.Design, Today);

        Assert.Equal(JobPhase.Design, job.Phase);
        Assert.Null(job.ConstructionStartDate);
        Assert.Null(job.DeliveryStartDate);
        Assert.Equal(Day(2), job.StartDate);
    }

    [Fact]
    public void DeliveryToProspect_ClearsSeams_KeepsStartDate()
    {
        var job = Make(phase: JobPhase.Delivery, start: 2, constructionStart: 6, deliveryStart: 8);

        JobPhases.ChangeStage(job, JobStage.Prospect, Today);

        Assert.Equal(JobPhase.Prospect, job.Phase);
        Assert.Null(job.ConstructionStartDate);
        Assert.Null(job.DeliveryStartDate);
        Assert.Equal(Day(2), job.StartDate);
    }

    [Fact]
    public void ConstructionToDesign_ThenForwardAgain_StampsAFreshDate()
    {
        var job = Make(phase: JobPhase.Construction, start: 2, constructionStart: 6);

        JobPhases.ChangeStage(job, JobStage.Design, Day(8));
        Assert.Null(job.ConstructionStartDate);

        JobPhases.ChangeStage(job, JobStage.Construction, Day(9));
        Assert.Equal(Day(9), job.ConstructionStartDate);
    }

    // ---- no-op, NoPhases, target, pins ---------------------------------------------------

    [Fact]
    public void SameStage_ChangesNothing()
    {
        var job = Make(phase: JobPhase.Construction, start: 2, end: 4, constructionStart: 3, target: 20);
        var before = Clone(job);

        JobPhases.ChangeStage(job, JobStage.Construction, Today);

        AssertSame(before, job);
    }

    [Fact]
    public void NoPhasesToNoPhases_ChangesNothing()
    {
        var job = Make(start: 2, end: 4, completed: true);
        var before = Clone(job);

        JobPhases.ChangeStage(job, JobStage.NoPhases, Today);

        AssertSame(before, job);
    }

    [Fact]
    public void ToNoPhases_ClearsPhaseSeamsAndTarget_KeepsPlainFields()
    {
        var job = Make(phase: JobPhase.Delivery, completed: true, start: 2, end: 12,
            constructionStart: 6, deliveryStart: 8, target: 11);

        JobPhases.ChangeStage(job, JobStage.NoPhases, Today);

        Assert.Null(job.Phase);
        Assert.Null(job.ConstructionStartDate);
        Assert.Null(job.DeliveryStartDate);
        Assert.Null(job.DeliveryTargetDate);
        Assert.Equal(Day(2), job.StartDate);
        Assert.Equal(Day(12), job.EndDate);
        Assert.True(job.Completed);
        Assert.Equal(JobStage.NoPhases, JobPhases.GetStage(job));
    }

    [Theory]
    [InlineData(JobStage.Prospect)]
    [InlineData(JobStage.Design)]
    [InlineData(JobStage.Construction)]
    [InlineData(JobStage.Delivery)]
    [InlineData(JobStage.Finished)]
    public void DeliveryTarget_SurvivesEveryStageChange(JobStage stage)
    {
        var job = Make(phase: JobPhase.Design, start: 2, target: 25);

        JobPhases.ChangeStage(job, stage, Today);

        Assert.Equal(Day(25), job.DeliveryTargetDate);
    }

    [Fact]
    public void PinEnd_IsAlwaysClearedWhenJobBecomesPhased()
    {
        var job = Make(start: 2, end: 4, pinEnd: true);

        JobPhases.ChangeStage(job, JobStage.Design, Today);

        Assert.False(job.PinEndToToday);
    }

    [Theory]
    [InlineData(JobStage.Prospect, true)]
    [InlineData(JobStage.Design, true)]
    [InlineData(JobStage.Construction, false)]
    [InlineData(JobStage.Delivery, false)]
    [InlineData(JobStage.Finished, false)]
    public void PinStart_SurvivesOnlyProspectAndDesign(JobStage stage, bool pinSurvives)
    {
        var job = Make(start: 2, end: 4, pinStart: true);

        JobPhases.ChangeStage(job, stage, Today);

        Assert.Equal(pinSurvives, job.PinStartToToday);
    }

    // ---- converting jobs that have no phases ---------------------------------------------

    [Fact]
    public void LegacyActiveJob_ToConstruction_KeepsItsRealStart()
    {
        var job = Make(start: 1, end: 3);

        JobPhases.ChangeStage(job, JobStage.Construction, Today);

        Assert.Equal(Day(1), job.StartDate);
        Assert.Equal(Today, job.ConstructionStartDate);
        Assert.Equal(Today, job.EndDate);
        Assert.Equal(JobPhase.Construction, job.Phase);
    }

    [Fact]
    public void LegacyActiveJob_ToDesign_KeepsItsRealStart()
    {
        var job = Make(start: 1, end: 3);

        JobPhases.ChangeStage(job, JobStage.Design, Today);

        Assert.Equal(JobPhase.Design, job.Phase);
        Assert.Equal(Day(1), job.StartDate);
        Assert.Null(job.ConstructionStartDate);
        Assert.Equal(Today, job.EndDate);
    }

    [Fact]
    public void LegacyActiveJob_ToProspect_KeepsStart()
    {
        var job = Make(start: 1, end: 3);

        JobPhases.ChangeStage(job, JobStage.Prospect, Today);

        Assert.Equal(JobPhase.Prospect, job.Phase);
        Assert.Equal(Day(1), job.StartDate);
    }

    [Fact]
    public void LegacyFinishedJob_ToFinished_KeepsEndAndPutsSeamsOnIt()
    {
        var job = Make(start: 0, end: 9, completed: true);

        JobPhases.ChangeStage(job, JobStage.Finished, Day(40));

        Assert.True(job.Completed);
        Assert.Equal(Day(9), job.EndDate);
        Assert.Equal(Day(9), job.ConstructionStartDate);
        Assert.Equal(Day(9), job.DeliveryStartDate);
        Assert.Equal(JobStage.Finished, JobPhases.GetStage(job));
    }

    [Fact]
    public void LegacyFinishedJob_ToConstruction_ReopensIt()
    {
        var job = Make(start: 0, end: 9, completed: true);

        JobPhases.ChangeStage(job, JobStage.Construction, Day(40));

        Assert.False(job.Completed);
        Assert.Equal(Day(40), job.EndDate);
        Assert.Equal(Day(9), job.ConstructionStartDate);
    }

    [Fact]
    public void LegacyFinishedJob_NeverGetsSeamsAfterItsEnd()
    {
        var job = Make(start: 0, end: 9, completed: true);

        JobPhases.ChangeStage(job, JobStage.Delivery, Day(40));

        Assert.True(job.ConstructionStartDate <= Day(9));
        Assert.True(job.DeliveryStartDate <= Day(9));
    }

    [Fact]
    public void StampNeverPrecedesTheStartDate()
    {
        // A job that starts in the future: stamping "today" would put the seam before the start.
        var job = Make(start: 20, end: 25);

        JobPhases.ChangeStage(job, JobStage.Construction, Today);

        Assert.Equal(Day(20), job.ConstructionStartDate);
    }

    // ---- misc ----------------------------------------------------------------------------

    [Fact]
    public void TimeOfDayInToday_IsIgnored()
    {
        var job = Make(phase: JobPhase.Prospect);

        JobPhases.ChangeStage(job, JobStage.Design, Today.AddHours(15).AddMinutes(30));

        Assert.Equal(Today, job.StartDate);
        Assert.Equal(Today, job.EndDate);
    }

    [Fact]
    public void FullJourney_ThereAndBackAgain()
    {
        var job = Make(phase: JobPhase.Prospect, target: 30);

        JobPhases.ChangeStage(job, JobStage.Design, Day(1));
        JobPhases.ChangeStage(job, JobStage.Construction, Day(4));
        JobPhases.ChangeStage(job, JobStage.Delivery, Day(9));
        JobPhases.ChangeStage(job, JobStage.Finished, Day(12));

        Assert.Equal(Day(1), job.StartDate);
        Assert.Equal(Day(4), job.ConstructionStartDate);
        Assert.Equal(Day(9), job.DeliveryStartDate);
        Assert.Equal(Day(12), job.EndDate);
        Assert.True(job.Completed);

        JobPhases.ChangeStage(job, JobStage.Construction, Day(14));
        JobPhases.ChangeStage(job, JobStage.Design, Day(15));

        Assert.Equal(JobStage.Design, JobPhases.GetStage(job));
        Assert.Equal(Day(1), job.StartDate);
        Assert.Null(job.ConstructionStartDate);
        Assert.Null(job.DeliveryStartDate);
        Assert.False(job.Completed);
        Assert.Equal(Day(30), job.DeliveryTargetDate);
    }

    // ---- every transition ----------------------------------------------------------------

    public static IEnumerable<object[]> AllTransitions()
    {
        var stages = Enum.GetValues<JobStage>();
        foreach (var from in stages)
        {
            foreach (var to in stages)
            {
                yield return [from, to];
            }
        }
    }

    // Builds a job that is consistently in the given stage, as it would be after real use.
    private static Job InStage(JobStage stage)
    {
        if (stage == JobStage.NoPhases)
        {
            return Make(start: 1, end: 3);
        }

        var job = Make(phase: JobPhase.Prospect, target: 30);
        foreach (var step in new[] { JobStage.Design, JobStage.Construction, JobStage.Delivery, JobStage.Finished })
        {
            if ((int)step > (int)stage)
            {
                break;
            }

            JobPhases.ChangeStage(job, step, Day(5));
        }

        return job;
    }

    [Theory]
    [MemberData(nameof(AllTransitions))]
    public void EveryTransition_LeavesAConsistentJob(JobStage from, JobStage to)
    {
        var job = InStage(from);
        Assert.Equal(from, JobPhases.GetStage(job));

        JobPhases.ChangeStage(job, to, Today);

        Assert.Equal(to, JobPhases.GetStage(job));
        if (to == JobStage.NoPhases)
        {
            Assert.Null(job.Phase);
            return;
        }

        if (from == to)
        {
            return; // a no-op: dates are deliberately left as they were
        }

        Assert.Equal(to == JobStage.Finished, job.Completed);
        Assert.False(job.PinEndToToday);

        // Seams exist exactly for the phases that have been reached.
        Assert.Equal(to >= JobStage.Construction, job.ConstructionStartDate is not null);
        Assert.Equal(to >= JobStage.Delivery, job.DeliveryStartDate is not null);

        // The bar always ends today for a job changed today, and its phases add up to its length.
        Assert.Equal(Today, job.EndDate);
        if (to >= JobStage.Design)
        {
            var segments = JobPhases.GetSegments(job, Today);
            var expectedDays = (Today - job.StartDate).Days + 1;
            Assert.Equal(expectedDays, segments.Sum(s => s.Days));
        }
    }

    private static void AssertSame(Job expected, Job actual)
    {
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.StartDate, actual.StartDate);
        Assert.Equal(expected.EndDate, actual.EndDate);
        Assert.Equal(expected.Completed, actual.Completed);
        Assert.Equal(expected.PinStartToToday, actual.PinStartToToday);
        Assert.Equal(expected.PinEndToToday, actual.PinEndToToday);
        Assert.Equal(expected.Phase, actual.Phase);
        Assert.Equal(expected.ConstructionStartDate, actual.ConstructionStartDate);
        Assert.Equal(expected.DeliveryStartDate, actual.DeliveryStartDate);
        Assert.Equal(expected.DeliveryTargetDate, actual.DeliveryTargetDate);
    }
}
