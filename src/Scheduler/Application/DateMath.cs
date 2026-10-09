namespace Scheduler.Application;

/// <summary>Min/max/clamp for dates, which <see cref="Math"/> doesn't cover.</summary>
internal static class DateMath
{
    public static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;

    public static DateTime Clamp(DateTime value, DateTime min, DateTime max) =>
        value < min ? min : value > max ? max : value;
}
