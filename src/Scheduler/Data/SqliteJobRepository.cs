using System.Globalization;
using Microsoft.Data.Sqlite;
using Scheduler.Application;
using Scheduler.Domain;

namespace Scheduler.Data;

public sealed class SqliteJobRepository : IJobRepository
{
    private const string SelectColumns = """
        Id, Name, StartDate, EndDate, Completed, PinStartToToday, PinEndToToday,
        Phase, ConstructionStartDate, DeliveryStartDate, DeliveryTargetDate
        """;

    // Schema changes, in order. The database's PRAGMA user_version records how many have been
    // applied; to change the schema, append a step here and never edit an earlier one.
    private static readonly Action<SqliteConnection>[] Migrations =
    [
        CreateOrCatchUpJobsTable
    ];

    private readonly string _connectionString;

    public SqliteJobRepository(string databasePath)
    {
        _connectionString = $"Data Source={databasePath}";
        Migrate();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void Migrate()
    {
        using var connection = Open();

        var applied = Convert.ToInt32(ExecuteScalar(connection, "PRAGMA user_version"));
        for (int version = applied; version < Migrations.Length; version++)
        {
            Migrations[version](connection);
            Execute(connection, $"PRAGMA user_version = {version + 1}");
        }
    }

    // Databases from before versioning have user_version 0 but may already have the table, with
    // or without the columns added since, so this step is safe to run on any of them.
    private static void CreateOrCatchUpJobsTable(SqliteConnection connection)
    {
        Execute(connection, """
            CREATE TABLE IF NOT EXISTS Jobs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                StartDate TEXT NOT NULL,
                EndDate TEXT NOT NULL,
                Completed INTEGER NOT NULL DEFAULT 0,
                PinStartToToday INTEGER NOT NULL DEFAULT 0,
                PinEndToToday INTEGER NOT NULL DEFAULT 0,
                Phase TEXT NULL,
                ConstructionStartDate TEXT NULL,
                DeliveryStartDate TEXT NULL,
                DeliveryTargetDate TEXT NULL
            );
            """);

        var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(Jobs)";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                existingColumns.Add(reader.GetString(1));
            }
        }

        var addedColumns = new (string Name, string Definition)[]
        {
            ("Completed", "INTEGER NOT NULL DEFAULT 0"),
            ("PinStartToToday", "INTEGER NOT NULL DEFAULT 0"),
            ("PinEndToToday", "INTEGER NOT NULL DEFAULT 0"),
            ("Phase", "TEXT NULL"),
            ("ConstructionStartDate", "TEXT NULL"),
            ("DeliveryStartDate", "TEXT NULL"),
            ("DeliveryTargetDate", "TEXT NULL")
        };

        foreach (var (name, definition) in addedColumns)
        {
            if (!existingColumns.Contains(name))
            {
                Execute(connection, $"ALTER TABLE Jobs ADD COLUMN {name} {definition}");
            }
        }
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static object? ExecuteScalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    public IReadOnlyList<Job> GetAll()
    {
        var jobs = new List<Job>();

        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {SelectColumns} FROM Jobs";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            jobs.Add(new Job
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                StartDate = ParseDate(reader.GetString(2)),
                EndDate = ParseDate(reader.GetString(3)),
                Completed = reader.GetBoolean(4),
                PinStartToToday = reader.GetBoolean(5),
                PinEndToToday = reader.GetBoolean(6),
                Phase = reader.IsDBNull(7) ? null : Enum.Parse<JobPhase>(reader.GetString(7)),
                ConstructionStartDate = ReadDate(reader, 8),
                DeliveryStartDate = ReadDate(reader, 9),
                DeliveryTargetDate = ReadDate(reader, 10)
            });
        }

        return jobs;
    }

    public void Add(Job job)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Jobs (Name, StartDate, EndDate, Completed, PinStartToToday, PinEndToToday,
                Phase, ConstructionStartDate, DeliveryStartDate, DeliveryTargetDate)
            VALUES ($name, $start, $end, $completed, $pinStart, $pinEnd,
                $phase, $constructionStart, $deliveryStart, $deliveryTarget);
            """;
        AddJobParameters(command, job);
        command.ExecuteNonQuery();
    }

    public void Update(Job job)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Jobs SET Name = $name, StartDate = $start, EndDate = $end, Completed = $completed,
                PinStartToToday = $pinStart, PinEndToToday = $pinEnd, Phase = $phase,
                ConstructionStartDate = $constructionStart, DeliveryStartDate = $deliveryStart,
                DeliveryTargetDate = $deliveryTarget
            WHERE Id = $id;
            """;
        AddJobParameters(command, job);
        command.Parameters.AddWithValue("$id", job.Id);
        command.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Jobs WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    // The parameters shared by Add and Update.
    private static void AddJobParameters(SqliteCommand command, Job job)
    {
        command.Parameters.AddWithValue("$name", job.Name);
        command.Parameters.AddWithValue("$start", FormatDate(job.StartDate));
        command.Parameters.AddWithValue("$end", FormatDate(job.EndDate));
        command.Parameters.AddWithValue("$completed", job.Completed);
        command.Parameters.AddWithValue("$pinStart", job.PinStartToToday);
        command.Parameters.AddWithValue("$pinEnd", job.PinEndToToday);
        command.Parameters.AddWithValue("$phase", job.Phase?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$constructionStart", ToDb(job.ConstructionStartDate));
        command.Parameters.AddWithValue("$deliveryStart", ToDb(job.DeliveryStartDate));
        command.Parameters.AddWithValue("$deliveryTarget", ToDb(job.DeliveryTargetDate));
    }

    // Dates are stored as ISO 8601 round-trip strings, always read and written culture-independently.
    private static string FormatDate(DateTime date) => date.ToString("O", CultureInfo.InvariantCulture);

    private static DateTime ParseDate(string text) =>
        DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static object ToDb(DateTime? date) => date is { } d ? FormatDate(d) : DBNull.Value;

    private static DateTime? ReadDate(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : ParseDate(reader.GetString(ordinal));
}
