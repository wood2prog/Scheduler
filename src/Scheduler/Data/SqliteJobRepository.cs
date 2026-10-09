using Microsoft.Data.Sqlite;
using Scheduler.Application;
using Scheduler.Domain;

namespace Scheduler.Data;

public sealed class SqliteJobRepository : IJobRepository
{
    private readonly string _connectionString;

    public SqliteJobRepository(string databasePath)
    {
        _connectionString = $"Data Source={databasePath}";
        EnsureDatabaseCreated();
    }

    private void EnsureDatabaseCreated()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = """
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
                """;
            command.ExecuteNonQuery();
        }

        // Databases created before these columns existed need them added on.
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
                using var command = connection.CreateCommand();
                command.CommandText = $"ALTER TABLE Jobs ADD COLUMN {name} {definition}";
                command.ExecuteNonQuery();
            }
        }
    }

    public IReadOnlyList<Job> GetAll()
    {
        var jobs = new List<Job>();

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Name, StartDate, EndDate, Completed, PinStartToToday, PinEndToToday,
                   Phase, ConstructionStartDate, DeliveryStartDate, DeliveryTargetDate
            FROM Jobs
            """;

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            jobs.Add(new Job
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                StartDate = DateTime.Parse(reader.GetString(2)),
                EndDate = DateTime.Parse(reader.GetString(3)),
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
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Jobs (Name, StartDate, EndDate, Completed, PinStartToToday, PinEndToToday,
                Phase, ConstructionStartDate, DeliveryStartDate, DeliveryTargetDate)
            VALUES ($name, $start, $end, $completed, $pinStart, $pinEnd,
                $phase, $constructionStart, $deliveryStart, $deliveryTarget);
            """;
        command.Parameters.AddWithValue("$name", job.Name);
        command.Parameters.AddWithValue("$start", job.StartDate.ToString("O"));
        command.Parameters.AddWithValue("$end", job.EndDate.ToString("O"));
        command.Parameters.AddWithValue("$completed", job.Completed);
        command.Parameters.AddWithValue("$pinStart", job.PinStartToToday);
        command.Parameters.AddWithValue("$pinEnd", job.PinEndToToday);
        AddPhaseParameters(command, job);
        command.ExecuteNonQuery();
    }

    public void Update(Job job)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Jobs SET Name = $name, StartDate = $start, EndDate = $end, Completed = $completed,
                PinStartToToday = $pinStart, PinEndToToday = $pinEnd, Phase = $phase,
                ConstructionStartDate = $constructionStart, DeliveryStartDate = $deliveryStart,
                DeliveryTargetDate = $deliveryTarget
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$name", job.Name);
        command.Parameters.AddWithValue("$start", job.StartDate.ToString("O"));
        command.Parameters.AddWithValue("$end", job.EndDate.ToString("O"));
        command.Parameters.AddWithValue("$completed", job.Completed);
        command.Parameters.AddWithValue("$pinStart", job.PinStartToToday);
        command.Parameters.AddWithValue("$pinEnd", job.PinEndToToday);
        AddPhaseParameters(command, job);
        command.Parameters.AddWithValue("$id", job.Id);
        command.ExecuteNonQuery();
    }

    private static void AddPhaseParameters(SqliteCommand command, Job job)
    {
        command.Parameters.AddWithValue("$phase", job.Phase?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$constructionStart", ToDb(job.ConstructionStartDate));
        command.Parameters.AddWithValue("$deliveryStart", ToDb(job.DeliveryStartDate));
        command.Parameters.AddWithValue("$deliveryTarget", ToDb(job.DeliveryTargetDate));
    }

    private static object ToDb(DateTime? date) => date?.ToString("O") ?? (object)DBNull.Value;

    private static DateTime? ReadDate(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : DateTime.Parse(reader.GetString(ordinal));

    public void Delete(int id)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Jobs WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public void SetCompleted(int id, bool completed)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Jobs SET Completed = $completed WHERE Id = $id";
        command.Parameters.AddWithValue("$completed", completed ? 1 : 0);
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }
}
