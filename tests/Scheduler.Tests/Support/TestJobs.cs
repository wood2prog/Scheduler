using Scheduler.Domain;

namespace Scheduler.Tests.Support;

internal static class TestJobs
{
    /// <summary>A fixed reference date so tests never depend on the real clock.</summary>
    public static readonly DateTime Origin = new(2026, 10, 1);

    public static DateTime Day(int offset) => Origin.AddDays(offset);

    /// <summary>Builds a job with dates given as day offsets from <see cref="Origin"/>.</summary>
    public static Job Make(
        string name = "job",
        int start = 0,
        int end = 0,
        JobPhase? phase = null,
        bool completed = false,
        int? constructionStart = null,
        int? deliveryStart = null,
        int? target = null,
        bool pinStart = false,
        bool pinEnd = false,
        int id = 0) => new()
    {
        Id = id,
        Name = name,
        StartDate = Day(start),
        EndDate = Day(end),
        Phase = phase,
        Completed = completed,
        ConstructionStartDate = constructionStart is { } c ? Day(c) : null,
        DeliveryStartDate = deliveryStart is { } d ? Day(d) : null,
        DeliveryTargetDate = target is { } t ? Day(t) : null,
        PinStartToToday = pinStart,
        PinEndToToday = pinEnd
    };

    public static Job Clone(Job job) => new()
    {
        Id = job.Id,
        Name = job.Name,
        StartDate = job.StartDate,
        EndDate = job.EndDate,
        Completed = job.Completed,
        PinStartToToday = job.PinStartToToday,
        PinEndToToday = job.PinEndToToday,
        Phase = job.Phase,
        ConstructionStartDate = job.ConstructionStartDate,
        DeliveryStartDate = job.DeliveryStartDate,
        DeliveryTargetDate = job.DeliveryTargetDate
    };
}
