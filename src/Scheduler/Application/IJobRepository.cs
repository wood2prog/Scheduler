using Scheduler.Domain;

namespace Scheduler.Application;

public interface IJobRepository
{
    IReadOnlyList<Job> GetAll();
    void Add(Job job);
}
