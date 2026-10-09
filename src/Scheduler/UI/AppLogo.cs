using System.Reflection;

namespace Scheduler.UI;

/// <summary>The app logo, embedded in the assembly (see Scheduler.csproj).</summary>
internal static class AppLogo
{
    /// <summary>A new bitmap of the logo scaled to a square of <paramref name="size"/> pixels; the caller disposes it.</summary>
    public static Bitmap Load(int size)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SchedulerLogo.png")
            ?? throw new InvalidOperationException("The embedded logo is missing.");
        using var original = new Bitmap(stream);
        return new Bitmap(original, new Size(size, size));
    }
}
