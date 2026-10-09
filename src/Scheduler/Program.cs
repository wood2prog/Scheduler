using Scheduler.Application;
using Scheduler.Data;
using Scheduler.UI;

namespace Scheduler;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        Directory.CreateDirectory(AppPaths.DataDirectory);
        var databasePath = Path.Combine(AppPaths.DataDirectory, "scheduler.db");
        IJobRepository repository = new SqliteJobRepository(databasePath);
        var jobService = new JobService(repository);

        System.Windows.Forms.Application.Run(new MainForm(jobService));
    }
}
