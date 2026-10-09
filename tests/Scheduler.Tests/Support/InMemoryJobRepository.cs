using Scheduler.Application;
using Scheduler.Domain;

namespace Scheduler.Tests.Support;

/// <summary>
/// A repository kept in memory. Like the SQLite one it hands out copies, so mutating a job
/// returned by <see cref="GetAll"/> never changes what is stored until Update is called.
/// </summary>
internal sealed class InMemoryJobRepository : IJobRepository
{
    private readonly List<Job> _jobs = [];
    private int _nextId = 1;

    public int UpdateCount { get; private set; }

    public IReadOnlyList<Job> GetAll() => _jobs.Select(TestJobs.Clone).ToList();

    public void Add(Job job)
    {
        var stored = TestJobs.Clone(job);
        stored.Id = _nextId++;
        _jobs.Add(stored);
    }

    public void Update(Job job)
    {
        UpdateCount++;
        var index = _jobs.FindIndex(j => j.Id == job.Id);
        if (index >= 0)
        {
            _jobs[index] = TestJobs.Clone(job);
        }
    }

    public void Delete(int id) => _jobs.RemoveAll(j => j.Id == id);

    public void SetCompleted(int id, bool completed)
    {
        var job = _jobs.FirstOrDefault(j => j.Id == id);
        if (job is not null)
        {
            job.Completed = completed;
        }
    }

    /// <summary>Adds a job and returns it as stored (with its assigned Id).</summary>
    public Job Seed(Job job)
    {
        Add(job);
        return _jobs[^1];
    }

    public Job Get(int id) => TestJobs.Clone(_jobs.Single(j => j.Id == id));
}
