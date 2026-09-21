namespace Scheduler.UI;

/// <summary>
/// Persists the main window's position and size between runs as a plain
/// "X,Y,Width,Height" line in the per-user data folder (Program Files may be
/// read-only once installed).
/// </summary>
internal static class WindowSettings
{
    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Scheduler", "window.settings");

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

        return new Rectangle(x, y, width, height);
    }

    public static void Save(Rectangle bounds)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, $"{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}");
    }
}
