namespace Scheduler.Application;

/// <summary>
/// The chart view choices that survive between runs (sort order, hide completed), kept in a small
/// "Key=Value" text file. Loading never throws: anything missing or unreadable falls back to defaults.
/// </summary>
public sealed record ViewSettings(JobSortOrder SortOrder, bool HideCompleted)
{
    public static ViewSettings Default { get; } = new(JobSortOrder.StartDate, false);

    public static ViewSettings Load(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return Default;
            }

            return Parse(File.ReadAllLines(path));
        }
        catch (Exception)
        {
            return Default;
        }
    }

    /// <summary>Writes the settings; returns false (instead of throwing) if the file can't be written.</summary>
    public bool TrySave(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, $"SortOrder={SortOrder}{Environment.NewLine}HideCompleted={HideCompleted}{Environment.NewLine}");
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static ViewSettings Parse(IEnumerable<string> lines)
    {
        var result = Default;
        foreach (var line in lines)
        {
            var parts = line.Split('=', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            var key = parts[0].Trim();
            var value = parts[1].Trim();
            if (key == "SortOrder" && Enum.TryParse<JobSortOrder>(value, out var order)
                && Enum.IsDefined(order) && !int.TryParse(value, out _))
            {
                result = result with { SortOrder = order };
            }
            else if (key == "HideCompleted" && bool.TryParse(value, out var hide))
            {
                result = result with { HideCompleted = hide };
            }
        }

        return result;
    }
}
