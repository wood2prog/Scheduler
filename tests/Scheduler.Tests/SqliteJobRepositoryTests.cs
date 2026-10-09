using Microsoft.Data.Sqlite;
using Scheduler.Data;
using Scheduler.Domain;
using static Scheduler.Tests.Support.TestJobs;

namespace Scheduler.Tests;

/// <summary>Runs the real repository against a throwaway SQLite file.</summary>
public sealed class SqliteJobRepositoryTests : IDisposable
{
    private readonly string _directory;
    private readonly string _path;

    public SqliteJobRepositoryTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "SchedulerTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, "scheduler.db");
    }

    public void Dispose()
    {
        // Pooled connections keep the file open.
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp file is not worth failing a test run over.
        }
    }

    private SqliteJobRepository NewRepository() => new(_path);

    private void Execute(string sql)
    {
        using var connection = new SqliteConnection($"Data Source={_path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private long UserVersion()
    {
        using var connection = new SqliteConnection($"Data Source={_path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        return (long)command.ExecuteScalar()!;
    }

    private List<string> ColumnNames()
    {
        using var connection = new SqliteConnection($"Data Source={_path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(Jobs)";
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(1));
        }

        return names;
    }

    // ---- basics --------------------------------------------------------------------------

    [Fact]
    public void NewDatabase_IsEmpty() =>
        Assert.Empty(NewRepository().GetAll());

    [Fact]
    public void Add_AssignsIncreasingIds()
    {
        var repository = NewRepository();

        repository.Add(Make(name: "one", start: 0, end: 1));
        repository.Add(Make(name: "two", start: 0, end: 1));

        var jobs = repository.GetAll();
        Assert.Equal(2, jobs.Count);
        Assert.All(jobs, j => Assert.True(j.Id > 0));
        Assert.NotEqual(jobs[0].Id, jobs[1].Id);
    }

    [Fact]
    public void Add_PlainJob_RoundTripsEveryField()
    {
        var repository = NewRepository();
        repository.Add(Make(name: "Kitchen", start: 3, end: 17, completed: true, pinStart: true, pinEnd: true));

        var job = Assert.Single(repository.GetAll());

        Assert.Equal("Kitchen", job.Name);
        Assert.Equal(Day(3), job.StartDate);
        Assert.Equal(Day(17), job.EndDate);
        Assert.True(job.Completed);
        Assert.True(job.PinStartToToday);
        Assert.True(job.PinEndToToday);
    }

    [Fact]
    public void Add_PlainJob_LeavesPhaseFieldsNull()
    {
        var repository = NewRepository();
        repository.Add(Make(name: "plain", start: 1, end: 2));

        var job = Assert.Single(repository.GetAll());

        Assert.Null(job.Phase);
        Assert.Null(job.ConstructionStartDate);
        Assert.Null(job.DeliveryStartDate);
        Assert.Null(job.DeliveryTargetDate);
    }

    [Fact]
    public void Add_PhasedJob_RoundTripsPhaseFields()
    {
        var repository = NewRepository();
        repository.Add(Make(name: "phased", start: 1, end: 20, phase: JobPhase.Delivery, completed: true,
            constructionStart: 5, deliveryStart: 12, target: 18));

        var job = Assert.Single(repository.GetAll());

        Assert.Equal(JobPhase.Delivery, job.Phase);
        Assert.Equal(Day(5), job.ConstructionStartDate);
        Assert.Equal(Day(12), job.DeliveryStartDate);
        Assert.Equal(Day(18), job.DeliveryTargetDate);
    }

    [Theory]
    [InlineData(JobPhase.Prospect)]
    [InlineData(JobPhase.Design)]
    [InlineData(JobPhase.Construction)]
    [InlineData(JobPhase.Delivery)]
    public void EveryPhase_RoundTrips(JobPhase phase)
    {
        var repository = NewRepository();
        repository.Add(Make(phase: phase));

        Assert.Equal(phase, Assert.Single(repository.GetAll()).Phase);
    }

    [Fact]
    public void ProspectWithOnlyATarget_RoundTrips()
    {
        var repository = NewRepository();
        repository.Add(Make(name: "maybe", phase: JobPhase.Prospect, target: 40));

        var job = Assert.Single(repository.GetAll());

        Assert.Equal(JobPhase.Prospect, job.Phase);
        Assert.Equal(Day(40), job.DeliveryTargetDate);
        Assert.Null(job.ConstructionStartDate);
    }

    [Fact]
    public void Dates_KeepTheirDayAcrossTheRoundTrip()
    {
        var repository = NewRepository();
        repository.Add(Make(start: 0, end: 364));

        var job = Assert.Single(repository.GetAll());

        Assert.Equal(Day(0), job.StartDate);
        Assert.Equal(Day(364), job.EndDate);
    }

    [Theory]
    [InlineData("O'Brien's \"Deck\"")]
    [InlineData("x'); DROP TABLE Jobs;--")]
    [InlineData("Ünïcødé – 日本語 🚧")]
    [InlineData("")]
    public void AwkwardNames_AreStoredVerbatim(string name)
    {
        var repository = NewRepository();
        repository.Add(Make(name: name));

        Assert.Equal(name, Assert.Single(repository.GetAll()).Name);
    }

    // ---- update --------------------------------------------------------------------------

    [Fact]
    public void Update_ChangesEveryField()
    {
        var repository = NewRepository();
        repository.Add(Make(name: "before", start: 1, end: 2));
        var job = repository.GetAll().Single();

        job.Name = "after";
        job.StartDate = Day(10);
        job.EndDate = Day(20);
        job.Completed = true;
        job.PinStartToToday = true;
        job.PinEndToToday = true;
        job.Phase = JobPhase.Construction;
        job.ConstructionStartDate = Day(12);
        job.DeliveryStartDate = Day(15);
        job.DeliveryTargetDate = Day(30);
        repository.Update(job);

        var reloaded = repository.GetAll().Single();
        Assert.Equal("after", reloaded.Name);
        Assert.Equal(Day(10), reloaded.StartDate);
        Assert.Equal(Day(20), reloaded.EndDate);
        Assert.True(reloaded.Completed);
        Assert.True(reloaded.PinStartToToday);
        Assert.True(reloaded.PinEndToToday);
        Assert.Equal(JobPhase.Construction, reloaded.Phase);
        Assert.Equal(Day(12), reloaded.ConstructionStartDate);
        Assert.Equal(Day(15), reloaded.DeliveryStartDate);
        Assert.Equal(Day(30), reloaded.DeliveryTargetDate);
    }

    [Fact]
    public void Update_CanClearPhaseFieldsBackToNull()
    {
        var repository = NewRepository();
        repository.Add(Make(phase: JobPhase.Delivery, constructionStart: 5, deliveryStart: 8, target: 20));
        var job = repository.GetAll().Single();

        job.Phase = null;
        job.ConstructionStartDate = null;
        job.DeliveryStartDate = null;
        job.DeliveryTargetDate = null;
        repository.Update(job);

        var reloaded = repository.GetAll().Single();
        Assert.Null(reloaded.Phase);
        Assert.Null(reloaded.ConstructionStartDate);
        Assert.Null(reloaded.DeliveryStartDate);
        Assert.Null(reloaded.DeliveryTargetDate);
    }

    [Fact]
    public void Update_OnlyTouchesTheGivenJob()
    {
        var repository = NewRepository();
        repository.Add(Make(name: "first"));
        repository.Add(Make(name: "second"));
        var first = repository.GetAll().Single(j => j.Name == "first");

        first.Name = "renamed";
        repository.Update(first);

        Assert.Equal(["renamed", "second"], repository.GetAll().Select(j => j.Name).Order());
    }

    [Fact]
    public void Update_OfUnknownId_ChangesNothing()
    {
        var repository = NewRepository();
        repository.Add(Make(name: "only"));

        repository.Update(Make(name: "ghost", id: 999));

        Assert.Equal("only", Assert.Single(repository.GetAll()).Name);
    }

    // ---- delete -------------------------------------------------------------------------

    [Fact]
    public void Delete_RemovesOnlyThatJob()
    {
        var repository = NewRepository();
        repository.Add(Make(name: "keep"));
        repository.Add(Make(name: "drop"));
        var drop = repository.GetAll().Single(j => j.Name == "drop");

        repository.Delete(drop.Id);

        Assert.Equal("keep", Assert.Single(repository.GetAll()).Name);
    }

    [Fact]
    public void Delete_OfUnknownId_DoesNotThrow()
    {
        var repository = NewRepository();
        repository.Add(Make(name: "only"));

        repository.Delete(999);

        Assert.Single(repository.GetAll());
    }

    // ---- persistence and migration -------------------------------------------------------

    [Fact]
    public void Data_SurvivesReopeningTheDatabase()
    {
        NewRepository().Add(Make(name: "persisted", phase: JobPhase.Design, target: 9));

        var job = Assert.Single(NewRepository().GetAll());

        Assert.Equal("persisted", job.Name);
        Assert.Equal(JobPhase.Design, job.Phase);
        Assert.Equal(Day(9), job.DeliveryTargetDate);
    }

    [Fact]
    public void OpeningTheSameDatabaseRepeatedly_IsHarmless()
    {
        NewRepository().Add(Make(name: "a"));
        NewRepository();
        NewRepository();

        Assert.Single(NewRepository().GetAll());
        Assert.Equal(1, ColumnNames().Count(c => c == "Phase"));
    }

    [Fact]
    public void FreshDatabase_IsStampedWithTheSchemaVersion()
    {
        NewRepository();

        Assert.Equal(1, UserVersion());
    }

    [Fact]
    public void UnversionedLegacyDatabase_IsStampedAfterMigrating()
    {
        Execute("CREATE TABLE Jobs (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, StartDate TEXT NOT NULL, EndDate TEXT NOT NULL);");

        NewRepository();

        Assert.Equal(1, UserVersion());
        Assert.Contains("DeliveryTargetDate", ColumnNames());
    }

    [Fact]
    public void ReopeningAVersionedDatabase_ChangesNothing()
    {
        NewRepository().Add(Make(name: "kept"));

        var reopened = NewRepository();

        Assert.Equal(1, UserVersion());
        Assert.Equal("kept", Assert.Single(reopened.GetAll()).Name);
    }

    [Fact]
    public void FreshDatabase_HasAllColumns()
    {
        NewRepository();

        var columns = ColumnNames();

        foreach (var expected in new[]
        {
            "Id", "Name", "StartDate", "EndDate", "Completed", "PinStartToToday", "PinEndToToday",
            "Phase", "ConstructionStartDate", "DeliveryStartDate", "DeliveryTargetDate"
        })
        {
            Assert.Contains(expected, columns);
        }
    }

    [Fact]
    public void OriginalSchema_IsMigrated_AndExistingJobsKeepTheirData()
    {
        // The very first version of the app: no Completed, pin or phase columns.
        Execute("""
            CREATE TABLE Jobs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                StartDate TEXT NOT NULL,
                EndDate TEXT NOT NULL
            );
            INSERT INTO Jobs (Name, StartDate, EndDate)
            VALUES ('legacy', '2026-10-02T00:00:00.0000000', '2026-10-09T00:00:00.0000000');
            """);

        var repository = NewRepository();

        var job = Assert.Single(repository.GetAll());
        Assert.Equal("legacy", job.Name);
        Assert.Equal(Day(1), job.StartDate);
        Assert.Equal(Day(8), job.EndDate);
        Assert.False(job.Completed);
        Assert.False(job.PinStartToToday);
        Assert.False(job.PinEndToToday);
        Assert.Null(job.Phase);
        Assert.Null(job.ConstructionStartDate);
        Assert.Null(job.DeliveryStartDate);
        Assert.Null(job.DeliveryTargetDate);
        Assert.Contains("Phase", ColumnNames());
    }

    [Fact]
    public void SchemaBeforePhases_IsMigrated_AndCompletedFlagsAreKept()
    {
        // The schema as it was just before phases were added.
        Execute("""
            CREATE TABLE Jobs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                StartDate TEXT NOT NULL,
                EndDate TEXT NOT NULL,
                Completed INTEGER NOT NULL DEFAULT 0,
                PinStartToToday INTEGER NOT NULL DEFAULT 0,
                PinEndToToday INTEGER NOT NULL DEFAULT 0
            );
            INSERT INTO Jobs (Name, StartDate, EndDate, Completed, PinStartToToday, PinEndToToday)
            VALUES ('done', '2026-10-02T00:00:00.0000000', '2026-10-09T00:00:00.0000000', 1, 0, 0),
                   ('pinned', '2026-10-03T00:00:00.0000000', '2026-10-04T00:00:00.0000000', 0, 1, 1);
            """);

        var jobs = NewRepository().GetAll().OrderBy(j => j.Name).ToList();

        Assert.Equal(2, jobs.Count);
        Assert.True(jobs.Single(j => j.Name == "done").Completed);
        var pinned = jobs.Single(j => j.Name == "pinned");
        Assert.True(pinned.PinStartToToday);
        Assert.True(pinned.PinEndToToday);
        Assert.All(jobs, j => Assert.Null(j.Phase));
    }

    [Fact]
    public void MigratedDatabase_AcceptsNewPhasedJobsAndUpdates()
    {
        Execute("""
            CREATE TABLE Jobs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                StartDate TEXT NOT NULL,
                EndDate TEXT NOT NULL
            );
            INSERT INTO Jobs (Name, StartDate, EndDate)
            VALUES ('legacy', '2026-10-02T00:00:00.0000000', '2026-10-09T00:00:00.0000000');
            """);
        var repository = NewRepository();

        repository.Add(Make(name: "new", phase: JobPhase.Prospect, target: 30));
        var legacy = repository.GetAll().Single(j => j.Name == "legacy");
        legacy.Phase = JobPhase.Construction;
        legacy.ConstructionStartDate = Day(4);
        repository.Update(legacy);

        var jobs = repository.GetAll();
        Assert.Equal(2, jobs.Count);
        Assert.Equal(JobPhase.Construction, jobs.Single(j => j.Name == "legacy").Phase);
        Assert.Equal(Day(4), jobs.Single(j => j.Name == "legacy").ConstructionStartDate);
        Assert.Equal(JobPhase.Prospect, jobs.Single(j => j.Name == "new").Phase);
    }
}
