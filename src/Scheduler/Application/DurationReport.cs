using Scheduler.Domain;

namespace Scheduler.Application;

/// <summary>A completed job and its length in days (start and end days both count).</summary>
public sealed record DurationPoint(Job Job, int Days);

/// <summary>
/// Job-length statistics behind the bell-curve report. The curve is a normal distribution
/// centered on <see cref="Median"/> with spread <see cref="StdDev"/>.
/// </summary>
public sealed record DurationReport(IReadOnlyList<DurationPoint> Points, double Median, double StdDev)
{
    public const int MinimumJobs = 3;

    // Never let the spread collapse to zero (e.g. every job the same length), or the curve
    // would be an infinitely thin spike.
    private const double MinimumStdDev = 1.0;

    public static DurationReport? Build(IEnumerable<Job> completedJobs)
    {
        var points = completedJobs
            .Select(j => new DurationPoint(j, (j.EndDate.Date - j.StartDate.Date).Days + 1))
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

        return new DurationReport(points, median, Math.Max(Math.Sqrt(variance), MinimumStdDev));
    }
}
