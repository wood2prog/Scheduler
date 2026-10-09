using Scheduler.Domain;

namespace Scheduler.Tests;

public class JobCloneTests
{
    [Fact]
    public void Clone_CopiesEveryProperty_AndIsIndependent()
    {
        var job = new Job
        {
            Id = 7,
            Name = "x",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 2, 1),
            Completed = true,
            PinStartToToday = true,
            PinEndToToday = true,
            Phase = JobPhase.Delivery,
            ConstructionStartDate = new DateTime(2026, 1, 10),
            DeliveryStartDate = new DateTime(2026, 1, 20),
            DeliveryTargetDate = new DateTime(2026, 1, 30)
        };

        var clone = job.Clone();

        foreach (var property in typeof(Job).GetProperties())
        {
            Assert.Equal(property.GetValue(job), property.GetValue(clone));
            Assert.NotEqual(property.PropertyType.IsValueType ? Activator.CreateInstance(property.PropertyType) : null,
                property.GetValue(clone)); // the fixture above sets every property to a non-default value
        }

        clone.Name = "changed";
        Assert.Equal("x", job.Name);
    }
}
