using Scheduler.Domain;

namespace Scheduler.Application;

public enum JobSortOrder
{
    StartDate,
    EndDate,
    Name
}

public sealed class JobService
{
    private readonly IJobRepository _repository;

    public JobService(IJobRepository repository)
    {
        _repository = repository;
    }

    public IReadOnlyList<Job> GetJobs(JobSortOrder sortOrder, bool includeCompleted = true)
    {
        var jobs = _repository.GetAll();
        if (!includeCompleted)
        {
            jobs = jobs.Where(j => !j.Completed).ToList();
        }

        var today = DateTime.Today;
        foreach (var job in jobs)
        {
            ApplyPins(job, today);
        }

        return sortOrder switch
        {
            JobSortOrder.EndDate => jobs.OrderBy(j => j.EndDate).ToList(),
            JobSortOrder.Name => jobs.OrderBy(j => j.Name).ToList(),
            _ => jobs.OrderBy(j => j.StartDate).ToList()
        };
    }

    // Pinned dates track the current day until the job is completed; completing it freezes
    // whatever dates it had at that point. A pinned start can overtake a fixed end as days
    // pass, so the end is pushed forward to keep the job at least one day long.
    private static void ApplyPins(Job job, DateTime today)
    {
        if (job.Completed)
        {
            return;
        }

        if (job.PinStartToToday)
        {
            job.StartDate = today;
        }

        if (job.PinEndToToday)
        {
            job.EndDate = today;
        }

        // An unfinished phased job is still running, so it always extends to today.
        if (job.Phase is not null)
        {
            job.EndDate = today;
        }

        if (job.EndDate < job.StartDate)
        {
            job.EndDate = job.StartDate;
        }
    }

    // Returns null when there are too few completed jobs for a meaningful curve. Pins never
    // apply to completed jobs, so their stored dates are final.
    public DurationReport? GetDurationReport() =>
        DurationReport.Build("Completed Job Durations",
            _repository.GetAll().Where(j => j.Completed).Select(DurationPoint.ForWholeJob));

    // One phase's lengths, over the jobs that have finished that phase (they needn't be finished
    // overall). Null when there are too few of them.
    public DurationReport? GetPhaseDurationReport(JobPhase phase)
    {
        var today = DateTime.Today;
        var points = new List<DurationPoint>();
        foreach (var job in _repository.GetAll())
        {
            if (JobPhases.GetCompletedPhaseDays(job, phase, today) is { } days)
            {
                points.Add(new DurationPoint(job, days));
            }
        }

        return DurationReport.Build($"{phase} Phase Durations", points);
    }

    public static JobStage GetStage(Job job) => JobPhases.GetStage(job);

    public void ChangeStage(Job job, JobStage stage) => JobPhases.ChangeStage(job, stage, DateTime.Today);

    public IReadOnlyList<PhaseSegment> GetSegments(Job job) => JobPhases.GetSegments(job, DateTime.Today);

    public void MovePhaseStart(Job job, JobPhase phase, DateTime date)
    {
        JobPhases.SetPhaseStart(job, phase, date);
        _repository.Update(job);
    }

    public void AddJob(Job job) => _repository.Add(job);

    public void UpdateJob(Job job) => _repository.Update(job);

    public void DeleteJob(int id) => _repository.Delete(id);

    public void SetCompleted(int id, bool completed) => _repository.SetCompleted(id, completed);
}
