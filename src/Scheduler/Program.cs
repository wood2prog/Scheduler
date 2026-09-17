using Scheduler.Application;
using Scheduler.Data;
using Scheduler.UI;

namespace Scheduler;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        var databasePath = Path.Combine(AppContext.BaseDirectory, "scheduler.db");
        IJobRepository repository = new SqliteJobRepository(databasePath);
        var jobService = new JobService(repository);

        System.Windows.Forms.Application.Run(new MainForm(jobService));
    }
}
