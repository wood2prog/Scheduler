using Scheduler.Application;
using Scheduler.Domain;
using Scheduler.Tests.Support;

namespace Scheduler.Tests;

/// <summary>
/// JobService reads the real clock for pins and "today", so these tests build their dates
/// relative to <see cref="DateTime.Today"/> rather than the fixed origin the pure tests use.
/// </summary>
public class JobServiceTests
{
    private static readonly DateTime Today = DateTime.Today;
    private static DateTime Ago(int days) => Today.AddDays(-days);
    private static DateTime Ahead(int days) => Today.AddDays(days);

    private readonly InMemoryJobRepository _repository = new();
    private readonly JobService _service;

    public JobServiceTests()
    {
        _service = new JobService(_repository);
    }

    private Job Seed(string name = "job", DateTime? start = null, DateTime? end = null, bool completed = false,
        bool pinStart = false, bool pinEnd = false, JobPhase? phase = null,
        DateTime? constructionStart = null, DateTime? deliveryStart = null)
    {
        var s = start ?? Ago(5);
        return _repository.Seed(new Job
        {
            Name = name,
            StartDate = s,
            EndDate = end ?? s.AddDays(2),
            Completed = completed,
            PinStartToToday = pinStart,
            PinEndToToday = pinEnd,
            Phase = phase,
            ConstructionStartDate = constructionStart,
            DeliveryStartDate = deliveryStart
        });
    }

    // ---- sorting -------------------------------------------------------------------------

    [Fact]
    public void GetJobs_SortsByStartDate()
    {
        Seed("late", start: Ago(1));
        Seed("early", start: Ago(9));
        Seed("middle", start: Ago(4));

        var names = _service.GetJobs(JobSortOrder.StartDate).Select(j => j.Name);

        Assert.Equal(["early", "middle", "late"], names);
    }

    [Fact]
    public void GetJobs_SortsByEndDate()
    {
        Seed("a", start: Ago(9), end: Ago(1));
        Seed("b", start: Ago(8), end: Ago(6));
        Seed("c", start: Ago(7), end: Ago(3));

        var names = _service.GetJobs(JobSortOrder.EndDate).Select(j => j.Name);

        Assert.Equal(["b", "c", "a"], names);
    }

    [Fact]
    public void GetJobs_SortsByName()
    {
        Seed("charlie");
        Seed("alpha");
        Seed("bravo");

        var names = _service.GetJobs(JobSortOrder.Name).Select(j => j.Name);

        Assert.Equal(["alpha", "bravo", "charlie"], names);
    }

    [Fact]
    public void GetJobs_NoJobs_ReturnsEmpty() =>
        Assert.Empty(_service.GetJobs(JobSortOrder.StartDate));

    // ---- hiding completed jobs -----------------------------------------------------------

    [Fact]
    public void GetJobs_IncludesCompletedJobsByDefault()
    {
        Seed("open");
        Seed("done", completed: true);

        Assert.Equal(2, _service.GetJobs(JobSortOrder.Name).Count);
    }

    [Fact]
    public void GetJobs_CanLeaveOutCompletedJobs()
    {
        Seed("open");
        Seed("done", completed: true);

        var jobs = _service.GetJobs(JobSortOrder.Name, includeCompleted: false);

        Assert.Equal("open", Assert.Single(jobs).Name);
    }

    [Fact]
    public void GetJobs_HidingCompleted_StillHidesFinishedPhasedJobs()
    {
        Seed("running", phase: JobPhase.Design);
        Seed("finished", phase: JobPhase.Delivery, completed: true);

        var jobs = _service.GetJobs(JobSortOrder.Name, includeCompleted: false);

        Assert.Equal("running", Assert.Single(jobs).Name);
    }

    // ---- pins ----------------------------------------------------------------------------

    [Fact]
    public void PinnedStart_FollowsToday()
    {
        Seed(start: Ago(10), end: Ahead(5), pinStart: true);

        var job = _service.GetJobs(JobSortOrder.StartDate).Single();

        Assert.Equal(Today, job.StartDate);
        Assert.Equal(Ahead(5), job.EndDate);
    }

    [Fact]
    public void PinnedEnd_FollowsToday()
    {
        Seed(start: Ago(10), end: Ago(3), pinEnd: true);

        var job = _service.GetJobs(JobSortOrder.StartDate).Single();

        Assert.Equal(Ago(10), job.StartDate);
        Assert.Equal(Today, job.EndDate);
    }

    [Fact]
    public void PinnedStart_PastAFixedEnd_PushesTheEndForward()
    {
        Seed(start: Ago(10), end: Ago(2), pinStart: true);

        var job = _service.GetJobs(JobSortOrder.StartDate).Single();

        Assert.Equal(Today, job.StartDate);
        Assert.Equal(Today, job.EndDate);
    }

    [Fact]
    public void CompletedJobs_IgnoreTheirPins()
    {
        Seed(start: Ago(10), end: Ago(3), completed: true, pinStart: true, pinEnd: true);

        var job = _service.GetJobs(JobSortOrder.StartDate).Single();

        Assert.Equal(Ago(10), job.StartDate);
        Assert.Equal(Ago(3), job.EndDate);
    }

    [Fact]
    public void Pins_AreAppliedToReadsOnly_NotPersisted()
    {
        var stored = Seed(start: Ago(10), end: Ago(3), pinStart: true, pinEnd: true);

        _service.GetJobs(JobSortOrder.StartDate);

        var inRepository = _repository.Get(stored.Id);
        Assert.Equal(Ago(10), inRepository.StartDate);
        Assert.Equal(Ago(3), inRepository.EndDate);
    }

    [Fact]
    public void UnfinishedPhasedJob_AlwaysRunsToToday()
    {
        Seed(start: Ago(10), end: Ago(8), phase: JobPhase.Construction, constructionStart: Ago(6));

        var job = _service.GetJobs(JobSortOrder.StartDate).Single();

        Assert.Equal(Today, job.EndDate);
    }

    [Fact]
    public void FinishedPhasedJob_KeepsItsEndDate()
    {
        Seed(start: Ago(10), end: Ago(3), completed: true, phase: JobPhase.Delivery);

        var job = _service.GetJobs(JobSortOrder.StartDate).Single();

        Assert.Equal(Ago(3), job.EndDate);
    }

    [Fact]
    public void Prospect_RunsToToday_ButPhaseStaysProspect()
    {
        Seed(start: Ago(4), end: Ago(4), phase: JobPhase.Prospect);

        var job = _service.GetJobs(JobSortOrder.StartDate).Single();

        Assert.Equal(JobPhase.Prospect, job.Phase);
        Assert.Equal(Today, job.EndDate);
    }

    [Fact]
    public void UnphasedJob_WithoutPins_KeepsItsDates()
    {
        Seed(start: Ago(10), end: Ago(3));

        var job = _service.GetJobs(JobSortOrder.StartDate).Single();

        Assert.Equal(Ago(10), job.StartDate);
        Assert.Equal(Ago(3), job.EndDate);
    }

    [Fact]
    public void GetJobs_EndDateSort_UsesPinnedEndDates()
    {
        Seed("fixed", start: Ago(9), end: Ago(5));
        Seed("pinned", start: Ago(8), end: Ago(7), pinEnd: true);

        var names = _service.GetJobs(JobSortOrder.EndDate).Select(j => j.Name);

        Assert.Equal(["fixed", "pinned"], names);
    }

    // ---- whole-job report ----------------------------------------------------------------

    [Fact]
    public void DurationReport_CountsOnlyCompletedJobs()
    {
        Seed("a", start: Ago(10), end: Ago(7), completed: true);
        Seed("b", start: Ago(10), end: Ago(5), completed: true);
        Seed("c", start: Ago(10), end: Ago(3), completed: true);
        Seed("open", start: Ago(10), end: Ago(1));

        var report = _service.GetDurationReport();

        Assert.NotNull(report);
        Assert.Equal(["a", "b", "c"], report!.Points.Select(p => p.Job.Name).Order());
        Assert.Equal("Completed Job Durations", report.Title);
    }

    [Fact]
    public void DurationReport_UsesStoredDates_NotPins()
    {
        Seed("a", start: Ago(10), end: Ago(7), completed: true, pinStart: true, pinEnd: true);
        Seed("b", start: Ago(10), end: Ago(7), completed: true);
        Seed("c", start: Ago(10), end: Ago(7), completed: true);

        var report = _service.GetDurationReport();

        Assert.All(report!.Points, p => Assert.Equal(4, p.Days));
    }

    [Fact]
    public void DurationReport_WithTooFewCompletedJobs_IsNull()
    {
        Seed("a", completed: true);
        Seed("b", completed: true);
        Seed("open");
        Seed("open2");

        Assert.Null(_service.GetDurationReport());
    }

    // ---- phase reports -------------------------------------------------------------------

    private void SeedPhasedJobs()
    {
        // Three jobs through Design (3 days each) and Construction (4 days each).
        for (int i = 0; i < 3; i++)
        {
            Seed($"done{i}", start: Ago(30), end: Ago(10), completed: true, phase: JobPhase.Delivery,
                constructionStart: Ago(27), deliveryStart: Ago(23));
        }

        Seed("inDesign", start: Ago(5), phase: JobPhase.Design);
        Seed("inConstruction", start: Ago(9), phase: JobPhase.Construction, constructionStart: Ago(5));
        Seed("unphased", start: Ago(30), end: Ago(1), completed: true);
        Seed("prospect", start: Ago(30), phase: JobPhase.Prospect);
    }

    [Fact]
    public void PhaseReport_Design_CountsJobsThatFinishedDesign()
    {
        SeedPhasedJobs();

        var report = _service.GetPhaseDurationReport(JobPhase.Design);

        Assert.NotNull(report);
        Assert.Equal("Design Phase Durations", report!.Title);
        Assert.Equal(4, report.Points.Count); // 3 done + inConstruction
        Assert.DoesNotContain(report.Points, p => p.Job.Name is "inDesign" or "unphased" or "prospect");
    }

    [Fact]
    public void PhaseReport_Construction_NeedsJobsPastConstruction()
    {
        SeedPhasedJobs();

        var report = _service.GetPhaseDurationReport(JobPhase.Construction);

        Assert.NotNull(report);
        Assert.Equal(3, report!.Points.Count);
        Assert.All(report.Points, p => Assert.Equal(4, p.Days));
        Assert.Equal(4, report.Median);
    }

    [Fact]
    public void PhaseReport_Delivery_CountsOnlyFinishedJobs()
    {
        SeedPhasedJobs();

        var report = _service.GetPhaseDurationReport(JobPhase.Delivery);

        Assert.NotNull(report);
        Assert.Equal(3, report!.Points.Count);
        Assert.All(report.Points, p => Assert.StartsWith("done", p.Job.Name));
        Assert.All(report.Points, p => Assert.Equal(14, p.Days)); // Ago(23)..Ago(10)
    }

    [Fact]
    public void PhaseReport_WithTooFewQualifyingJobs_IsNull()
    {
        Seed("a", start: Ago(9), phase: JobPhase.Construction, constructionStart: Ago(5));
        Seed("b", start: Ago(9), phase: JobPhase.Construction, constructionStart: Ago(5));

        Assert.Null(_service.GetPhaseDurationReport(JobPhase.Design));
    }

    [Fact]
    public void PhaseReport_IgnoresUnphasedJobs()
    {
        for (int i = 0; i < 5; i++)
        {
            Seed($"legacy{i}", start: Ago(20), end: Ago(5), completed: true);
        }

        Assert.Null(_service.GetPhaseDurationReport(JobPhase.Design));
        Assert.Null(_service.GetPhaseDurationReport(JobPhase.Construction));
        Assert.Null(_service.GetPhaseDurationReport(JobPhase.Delivery));
    }

    // ---- phase operations ----------------------------------------------------------------

    [Fact]
    public void ChangeStage_UpdatesTheJobInMemory_ButDoesNotSaveIt()
    {
        var stored = Seed(phase: JobPhase.Design, start: Ago(5));
        var job = _repository.Get(stored.Id);

        _service.ChangeStage(job, JobStage.Construction);

        Assert.Equal(JobPhase.Construction, job.Phase);
        Assert.Equal(Today, job.ConstructionStartDate);
        Assert.Equal(JobPhase.Design, _repository.Get(stored.Id).Phase);
        Assert.Equal(0, _repository.UpdateCount);
    }

    [Fact]
    public void MovePhaseStart_SavesTheNewDate()
    {
        var stored = Seed(phase: JobPhase.Construction, start: Ago(9), constructionStart: Ago(5));
        var job = _repository.Get(stored.Id);

        _service.MovePhaseStart(job, JobPhase.Construction, Ago(7));

        Assert.Equal(Ago(7), _repository.Get(stored.Id).ConstructionStartDate);
        Assert.Equal(1, _repository.UpdateCount);
    }

    [Fact]
    public void GetSegments_UsesTheCurrentDay()
    {
        var job = Seed(phase: JobPhase.Design, start: Ago(3));

        var segment = Assert.Single(_service.GetSegments(job));

        Assert.Equal(JobPhase.Design, segment.Phase);
        Assert.Equal(4, segment.Days); // three days ago through today
    }

    [Fact]
    public void GetStage_IsAvailableStatically() =>
        Assert.Equal(JobStage.Design, JobService.GetStage(new Job { Phase = JobPhase.Design }));

    // ---- plain pass-throughs -------------------------------------------------------------

    [Fact]
    public void AddJob_Stores()
    {
        _service.AddJob(new Job { Name = "new", StartDate = Today, EndDate = Today });

        Assert.Equal("new", Assert.Single(_repository.GetAll()).Name);
    }

    [Fact]
    public void UpdateJob_Saves()
    {
        var stored = Seed("before");
        var job = _repository.Get(stored.Id);
        job.Name = "after";

        _service.UpdateJob(job);

        Assert.Equal("after", _repository.Get(stored.Id).Name);
    }

    [Fact]
    public void DeleteJob_Removes()
    {
        var stored = Seed("doomed");
        Seed("kept");

        _service.DeleteJob(stored.Id);

        Assert.Equal("kept", Assert.Single(_repository.GetAll()).Name);
    }

    [Fact]
    public void SetCompleted_Toggles()
    {
        var stored = Seed();

        _service.SetCompleted(stored.Id, true);
        Assert.True(_repository.Get(stored.Id).Completed);

        _service.SetCompleted(stored.Id, false);
        Assert.False(_repository.Get(stored.Id).Completed);
    }
}
