using Scheduler.Application;

namespace Scheduler.Tests;

public class ViewSettingsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "SchedulerViewSettings-" + Guid.NewGuid());
    private string FilePath => Path.Combine(_dir, "ui.settings");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }

    [Theory]
    [InlineData(JobSortOrder.StartDate, false)]
    [InlineData(JobSortOrder.EndDate, true)]
    [InlineData(JobSortOrder.Name, true)]
    public void RoundTrips(JobSortOrder order, bool hide)
    {
        Assert.True(new ViewSettings(order, hide).TrySave(FilePath));
        Assert.Equal(new ViewSettings(order, hide), ViewSettings.Load(FilePath));
    }

    [Fact]
    public void MissingFileGivesDefaults() =>
        Assert.Equal(ViewSettings.Default, ViewSettings.Load(FilePath));

    [Fact]
    public void CorruptFileGivesDefaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllBytes(FilePath, [0xFF, 0x00, 0x3D, 0xFE, 0x0A, 0x3D]);
        Assert.Equal(ViewSettings.Default, ViewSettings.Load(FilePath));
    }

    [Theory]
    [InlineData("Bogus")]
    [InlineData("1")]
    [InlineData("99")]
    public void UnknownSortNameFallsBackToDefaultSort(string value)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, $"SortOrder={value}\nHideCompleted=True\n");
        Assert.Equal(new ViewSettings(ViewSettings.Default.SortOrder, true), ViewSettings.Load(FilePath));
    }

    [Fact]
    public void PartialFileKeepsDefaultsForMissingKeys()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "SortOrder=Name\n");
        Assert.Equal(new ViewSettings(JobSortOrder.Name, false), ViewSettings.Load(FilePath));
    }

    [Fact]
    public void GarbageHideCompletedFallsBackToFalseButKeepsSortOrder()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "SortOrder=EndDate\nHideCompleted=maybe\n");
        Assert.Equal(new ViewSettings(JobSortOrder.EndDate, false), ViewSettings.Load(FilePath));
    }

    [Fact]
    public void ExtraWhitespaceAroundKeysAndValuesIsTolerated()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "  SortOrder  =   Name   \n\t HideCompleted = True \t\n");
        Assert.Equal(new ViewSettings(JobSortOrder.Name, true), ViewSettings.Load(FilePath));
    }

    [Fact]
    public void UnknownKeysAreIgnoredAndValidKeysStillLoad()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "Theme=Dark\nSortOrder=EndDate\nSomethingElse=42\nHideCompleted=true\n");
        Assert.Equal(new ViewSettings(JobSortOrder.EndDate, true), ViewSettings.Load(FilePath));
    }

    [Fact]
    public void BoolValuesAreCaseInsensitive()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "HideCompleted=TRUE\n");
        Assert.Equal(new ViewSettings(JobSortOrder.StartDate, true), ViewSettings.Load(FilePath));
    }

    [Fact]
    public void SaveFailureReturnsFalse()
    {
        Directory.CreateDirectory(_dir);
        // A directory sits where the file should be, so the write fails.
        Directory.CreateDirectory(FilePath);
        Assert.False(ViewSettings.Default.TrySave(FilePath));
    }
}
