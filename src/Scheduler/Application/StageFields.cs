namespace Scheduler.Application;

/// <summary>
/// Which parts of a job apply at a given stage, so the editor shows, enables and reads the right
/// fields. Whether a pinned date is currently overriding a calendar is the editor's own state and
/// is not part of this.
/// </summary>
/// <param name="UsesPhases">The job has phases: it shows phase dates instead of an End date, Completed and an end pin.</param>
/// <param name="HasStartDate">A Prospect has no start yet; it is set when the job moves into Design.</param>
/// <param name="CanPinStart">A start can follow today only while the job is unphased, a Prospect or in Design.</param>
/// <param name="CanPinEnd">Only unphased jobs have an end pin (phased jobs run to today by themselves), and not once completed.</param>
public sealed record StageFields(
    bool UsesPhases,
    bool HasStartDate,
    bool CanPinStart,
    bool CanPinEnd,
    bool HasConstructionStart,
    bool HasDeliveryStart,
    bool HasFinishedDate)
{
    /// <param name="completed">Once an unphased job is completed its pins stop applying and are locked.</param>
    public static StageFields For(JobStage stage, bool completed)
    {
        var unphasedDone = stage == JobStage.NoPhases && completed;
        return new StageFields(
            UsesPhases: stage != JobStage.NoPhases,
            HasStartDate: stage != JobStage.Prospect,
            CanPinStart: !unphasedDone && stage <= JobStage.Design,
            CanPinEnd: stage == JobStage.NoPhases && !completed,
            HasConstructionStart: stage >= JobStage.Construction,
            HasDeliveryStart: stage >= JobStage.Delivery,
            HasFinishedDate: stage == JobStage.Finished);
    }
}
