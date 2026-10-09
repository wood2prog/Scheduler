using Scheduler.Application;

namespace Scheduler.Tests;

public class StageFieldsTests
{
    [Theory]
    [InlineData(JobStage.NoPhases, false)]
    [InlineData(JobStage.Prospect, true)]
    [InlineData(JobStage.Design, true)]
    [InlineData(JobStage.Construction, true)]
    [InlineData(JobStage.Delivery, true)]
    [InlineData(JobStage.Finished, true)]
    public void UsesPhases_IsTrueForEveryStageExceptNoPhases(JobStage stage, bool expected) =>
        Assert.Equal(expected, StageFields.For(stage, completed: false).UsesPhases);

    [Fact]
    public void OnlyProspectLacksAStartDate()
    {
        foreach (var stage in Enum.GetValues<JobStage>())
        {
            Assert.Equal(stage != JobStage.Prospect, StageFields.For(stage, completed: false).HasStartDate);
        }
    }

    [Theory]
    [InlineData(JobStage.NoPhases, true)]
    [InlineData(JobStage.Prospect, true)]
    [InlineData(JobStage.Design, true)]
    [InlineData(JobStage.Construction, false)]
    [InlineData(JobStage.Delivery, false)]
    [InlineData(JobStage.Finished, false)]
    public void StartCanBePinnedOnlyUpToDesign(JobStage stage, bool expected) =>
        Assert.Equal(expected, StageFields.For(stage, completed: false).CanPinStart);

    [Fact]
    public void CompletedUnphasedJob_LocksBothPins()
    {
        var fields = StageFields.For(JobStage.NoPhases, completed: true);

        Assert.False(fields.CanPinStart);
        Assert.False(fields.CanPinEnd);
    }

    [Fact]
    public void OpenUnphasedJob_CanPinBoth()
    {
        var fields = StageFields.For(JobStage.NoPhases, completed: false);

        Assert.True(fields.CanPinStart);
        Assert.True(fields.CanPinEnd);
    }

    [Theory]
    [InlineData(JobStage.Design)]
    [InlineData(JobStage.Delivery)]
    public void PhasedJobs_HaveNoEndPin(JobStage stage) =>
        Assert.False(StageFields.For(stage, completed: false).CanPinEnd);

    [Theory]
    [InlineData(JobStage.Prospect, false, false, false)]
    [InlineData(JobStage.Design, false, false, false)]
    [InlineData(JobStage.Construction, true, false, false)]
    [InlineData(JobStage.Delivery, true, true, false)]
    [InlineData(JobStage.Finished, true, true, true)]
    public void PhaseDatesApplyOnceTheirPhaseIsReached(JobStage stage, bool construction, bool delivery, bool finished)
    {
        var fields = StageFields.For(stage, completed: stage == JobStage.Finished);

        Assert.Equal(construction, fields.HasConstructionStart);
        Assert.Equal(delivery, fields.HasDeliveryStart);
        Assert.Equal(finished, fields.HasFinishedDate);
    }
}
