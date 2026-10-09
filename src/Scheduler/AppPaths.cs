namespace Scheduler;

/// <summary>
/// Where the app keeps its per-user files (database, window settings): Documents/Scheduler, easy
/// to find and back up, and not next to the executable, which may be read-only once installed.
/// </summary>
internal static class AppPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Scheduler");
}
