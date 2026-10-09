using Scheduler.Domain;

namespace Scheduler.Application;

/// <summary>What the Phase dropdown offers. Finished is Delivery plus <see cref="Job.Completed"/>.</summary>
public enum JobStage
{
    NoPhases,
    Prospect,
    Design,
    Construction,
    Delivery,
    Finished
}

/// <summary>
/// A stretch of days a job spent (or is spending) in one phase. <see cref="EndExclusive"/> is the
/// first day after the phase, so adjacent segments share a seam date and lengths are simple
/// subtraction. The days of all segments add up to the job's length, start to finish.
/// </summary>
public sealed record PhaseSegment(JobPhase Phase, DateTime Start, DateTime EndExclusive)
{
    public int Days => (EndExclusive - Start).Days;
}

public static class JobPhases
{
    public static JobStage GetStage(Job job) => job.Phase switch
    {
        null => JobStage.NoPhases,
        JobPhase.Prospect => JobStage.Prospect,
        JobPhase.Design => JobStage.Design,
        JobPhase.Construction => JobStage.Construction,
        _ => job.Completed ? JobStage.Finished : JobStage.Delivery
    };

    /// <summary>
    /// Moves the job to another stage. Moving forward stamps a date on each seam crossed; moving
    /// back clears the dates of the phases being left, so the earlier phase simply runs on up to
    /// today again.
    /// </summary>
    public static void ChangeStage(Job job, JobStage stage, DateTime today)
    {
        today = today.Date;
        var current = GetStage(job);
        if (stage == current)
        {
            return;
        }

        if (stage == JobStage.NoPhases)
        {
            job.Phase = null;
            job.ConstructionStartDate = null;
            job.DeliveryStartDate = null;
            job.DeliveryTargetDate = null;
            return;
        }

        // A job without phases is treated as already past Prospect, so converting it keeps its
        // real start date.
        int from = current == JobStage.NoPhases ? (int)JobStage.Design : (int)current;
        int to = (int)stage;

        // Converting a job that is already finished must not stamp dates after its end.
        var stamp = job.Completed && today > job.EndDate.Date ? job.EndDate.Date : today;
        if (stamp < job.StartDate.Date)
        {
            stamp = job.StartDate.Date;
        }

        job.Phase = stage == JobStage.Finished ? JobPhase.Delivery : Enum.Parse<JobPhase>(stage.ToString());

        // Phased jobs run to today by themselves, and once construction starts the start date is
        // a fact, not something that should keep following today.
        job.PinEndToToday = false;
        if (to >= (int)JobStage.Construction)
        {
            job.PinStartToToday = false;
        }

        if (to > from)
        {
            if (from == (int)JobStage.Prospect)
            {
                job.StartDate = today;
                stamp = today;
            }

            if (from < (int)JobStage.Construction && to >= (int)JobStage.Construction)
            {
                job.ConstructionStartDate = stamp;
            }

            if (from < (int)JobStage.Delivery && to >= (int)JobStage.Delivery)
            {
                job.DeliveryStartDate = stamp;
            }
        }
        else
        {
            if (to < (int)JobStage.Delivery)
            {
                job.DeliveryStartDate = null;
            }

            if (to < (int)JobStage.Construction)
            {
                job.ConstructionStartDate = null;
            }
        }

        if (stage == JobStage.Finished)
        {
            // An already-finished job being converted keeps the end date it has.
            if (!job.Completed)
            {
                job.EndDate = today;
            }

            job.Completed = true;
        }
        else
        {
            job.Completed = false;
            job.EndDate = today;
        }
    }

    /// <summary>
    /// How many days the job spent in a phase, or null if it hasn't completed that phase yet (or
    /// skipped it, spending no days there) and so has no finished length to report.
    /// </summary>
    public static int? GetCompletedPhaseDays(Job job, JobPhase phase, DateTime today)
    {
        var stage = GetStage(job);
        var completed = phase switch
        {
            JobPhase.Design => stage >= JobStage.Construction,
            JobPhase.Construction => stage >= JobStage.Delivery,
            JobPhase.Delivery => stage == JobStage.Finished,
            _ => false
        };
        if (!completed)
        {
            return null;
        }

        var days = GetSegments(job, today).FirstOrDefault(s => s.Phase == phase)?.Days ?? 0;
        return days > 0 ? days : null;
    }

    /// <summary>Moves the seam that starts <paramref name="phase"/> (Construction or Delivery) to another day.</summary>
    public static void SetPhaseStart(Job job, JobPhase phase, DateTime date)
    {
        switch (phase)
        {
            case JobPhase.Construction:
                job.ConstructionStartDate = date.Date;
                break;
            case JobPhase.Delivery:
                job.DeliveryStartDate = date.Date;
                break;
        }
    }

    /// <summary>
    /// The colored stretches to draw for a phased job. Empty for jobs without phases and for
    /// Prospects (placeholders with nothing on the timeline). A job still in progress runs up to
    /// and including today. Dates that were edited into an impossible order are clamped rather
    /// than rejected, and zero-length phases are left out.
    /// </summary>
    public static IReadOnlyList<PhaseSegment> GetSegments(Job job, DateTime today)
    {
        if (job.Phase is null or JobPhase.Prospect)
        {
            return [];
        }

        var start = job.StartDate.Date;
        var end = (job.Completed ? job.EndDate.Date : Max(today.Date, start)).AddDays(1);

        var seam1 = end;
        var seam2 = end;
        if (job.Phase is JobPhase.Construction or JobPhase.Delivery)
        {
            seam1 = job.ConstructionStartDate?.Date ?? start;
            seam2 = job.Phase == JobPhase.Delivery ? job.DeliveryStartDate?.Date ?? seam1 : end;
        }

        seam1 = Clamp(seam1, start, end);
        seam2 = Clamp(seam2, seam1, end);

        var segments = new List<PhaseSegment>(3);
        AddIfNotEmpty(segments, JobPhase.Design, start, seam1);
        AddIfNotEmpty(segments, JobPhase.Construction, seam1, seam2);
        AddIfNotEmpty(segments, JobPhase.Delivery, seam2, end);
        return segments;
    }

    private static void AddIfNotEmpty(List<PhaseSegment> segments, JobPhase phase, DateTime start, DateTime endExclusive)
    {
        if (endExclusive > start)
        {
            segments.Add(new PhaseSegment(phase, start, endExclusive));
        }
    }

    private static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    private static DateTime Clamp(DateTime value, DateTime min, DateTime max) =>
        value < min ? min : value > max ? max : value;
}
