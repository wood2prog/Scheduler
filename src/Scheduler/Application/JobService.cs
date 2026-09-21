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

    public IReadOnlyList<Job> GetJobs(JobSortOrder sortOrder)
    {
        var jobs = _repository.GetAll();
        return sortOrder switch
        {
            JobSortOrder.EndDate => jobs.OrderBy(j => j.EndDate).ToList(),
            JobSortOrder.Name => jobs.OrderBy(j => j.Name).ToList(),
            _ => jobs.OrderBy(j => j.StartDate).ToList()
        };
    }

    public void AddJob(Job job) => _repository.Add(job);

    public void UpdateJob(Job job) => _repository.Update(job);

    public void DeleteJob(int id) => _repository.Delete(id);

    public void SetCompleted(int id, bool completed) => _repository.SetCompleted(id, completed);
}
