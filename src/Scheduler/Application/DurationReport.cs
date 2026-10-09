using Scheduler.Domain;

namespace Scheduler.Application;

/// <summary>A job and a length in days: the whole job, or one phase of it.</summary>
public sealed record DurationPoint(Job Job, int Days)
{
    /// <summary>Start to end, both days counting.</summary>
    public static DurationPoint ForWholeJob(Job job) =>
        new(job, (job.EndDate.Date - job.StartDate.Date).Days + 1);
}

/// <summary>
/// Length statistics behind a bell-curve report. The curve is a normal distribution
/// centered on <see cref="Median"/> with spread <see cref="StdDev"/>.
/// </summary>
public sealed record DurationReport(string Title, IReadOnlyList<DurationPoint> Points, double Median, double StdDev)
{
    public const int MinimumJobs = 3;

    // Never let the spread collapse to zero (e.g. every job the same length), or the curve
    // would be an infinitely thin spike.
    private const double MinimumStdDev = 1.0;

    public static DurationReport? Build(string title, IEnumerable<DurationPoint> durations)
    {
        var points = durations
            .OrderBy(p => p.Days)
            .ThenBy(p => p.Job.Name)
            .ToList();

        if (points.Count < MinimumJobs)
        {
            return null;
        }

        int mid = points.Count / 2;
        double median = points.Count % 2 == 1
            ? points[mid].Days
            : (points[mid - 1].Days + points[mid].Days) / 2.0;

        double mean = points.Average(p => p.Days);
        double variance = points.Sum(p => Math.Pow(p.Days - mean, 2)) / (points.Count - 1);

        return new DurationReport(title, points, median, Math.Max(Math.Sqrt(variance), MinimumStdDev));
    }
}
