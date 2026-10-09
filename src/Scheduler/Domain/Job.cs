namespace Scheduler.Domain;

public enum JobPhase
{
    // Placeholder for a job that might happen: no bar on the timeline, but it can carry a
    // delivery target date.
    Prospect,
    Design,
    Construction,
    Delivery
}

public sealed class Job
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool Completed { get; set; }
    public bool PinStartToToday { get; set; }
    public bool PinEndToToday { get; set; }

    // Null for jobs that don't use phases (drawn as a single bar). When set, StartDate is the
    // start of Design, EndDate is the end of Delivery, and Completed means the job is finished.
    public JobPhase? Phase { get; set; }

    // Seam between Design and Construction.
    public DateTime? ConstructionStartDate { get; set; }

    // Seam between Construction and Delivery.
    public DateTime? DeliveryStartDate { get; set; }

    public DateTime? DeliveryTargetDate { get; set; }

    /// <summary>An independent copy. Every field is a value type or string, so a shallow copy is complete, including fields added later.</summary>
    public Job Clone() => (Job)MemberwiseClone();
}
