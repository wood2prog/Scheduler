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

        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Jobs (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                StartDate TEXT NOT NULL,
                EndDate TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<Job> GetAll()
    {
        var jobs = new List<Job>();

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, StartDate, EndDate FROM Jobs";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            jobs.Add(new Job
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                StartDate = DateTime.Parse(reader.GetString(2)),
                EndDate = DateTime.Parse(reader.GetString(3))
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
            INSERT INTO Jobs (Name, StartDate, EndDate)
            VALUES ($name, $start, $end);
            """;
        command.Parameters.AddWithValue("$name", job.Name);
        command.Parameters.AddWithValue("$start", job.StartDate.ToString("O"));
        command.Parameters.AddWithValue("$end", job.EndDate.ToString("O"));
        command.ExecuteNonQuery();
    }
}
