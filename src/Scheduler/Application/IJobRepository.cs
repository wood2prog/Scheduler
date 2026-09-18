using Scheduler.Domain;

namespace Scheduler.Application;

public interface IJobRepository
{
    IReadOnlyList<Job> GetAll();
    void Add(Job job);
    void Update(Job job);
    void Delete(int id);
    void SetCompleted(int id, bool completed);
}
