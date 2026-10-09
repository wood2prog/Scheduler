namespace Scheduler.UI;

/// <summary>
/// Persists the main window's position and size between runs as a plain
/// "X,Y,Width,Height" line in the per-user data folder.
/// </summary>
internal static class WindowSettings
{
    private static string FilePath => Path.Combine(AppPaths.DataDirectory, "window.settings");

    public static Rectangle? Load()
    {
        if (!File.Exists(FilePath))
        {
            return null;
        }

        var parts = File.ReadAllText(FilePath).Split(',');
        if (parts.Length != 4
            || !int.TryParse(parts[0], out var x)
            || !int.TryParse(parts[1], out var y)
            || !int.TryParse(parts[2], out var width)
            || !int.TryParse(parts[3], out var height))
        {
            return null;
        }

        // Ignore a saved position that is no longer on any screen (e.g. a disconnected monitor).
        var bounds = new Rectangle(x, y, width, height);
        return Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(bounds)) ? bounds : null;
    }

    public static void Save(Rectangle bounds)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, $"{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}");
    }
}
