namespace TimeTrackerMcp.Services;

/// <summary>
/// M3-019: Utility class for formatting durations as human-readable strings.
/// </summary>
public static class DurationFormatter
{
    /// <summary>
    /// M3-020, M3-021: Formats a duration in milliseconds as a human-readable string.
    /// Handles edge cases including sub-second durations and hours.
    /// </summary>
    /// <param name="milliseconds">Duration in milliseconds.</param>
    /// <returns>Human-readable duration string (e.g., "2 minutes 34 seconds").</returns>
    public static string Format(long milliseconds)
    {
        var span = TimeSpan.FromMilliseconds(milliseconds);

        // M3-021: Handle edge case - less than 1 second
        if (span.TotalSeconds < 1)
            return "less than 1 second";

        var parts = new List<string>();

        // M3-021: Handle edge case - days (for very long durations)
        if (span.Days > 0)
            parts.Add($"{span.Days} day{(span.Days != 1 ? "s" : "")}");

        // M3-021: Handle hours
        if (span.Hours > 0)
            parts.Add($"{span.Hours} hour{(span.Hours != 1 ? "s" : "")}");

        // Minutes
        if (span.Minutes > 0)
            parts.Add($"{span.Minutes} minute{(span.Minutes != 1 ? "s" : "")}");

        // Seconds
        if (span.Seconds > 0)
            parts.Add($"{span.Seconds} second{(span.Seconds != 1 ? "s" : "")}");

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Formats a duration with milliseconds precision for detailed output.
    /// </summary>
    /// <param name="milliseconds">Duration in milliseconds.</param>
    /// <returns>Human-readable duration string with milliseconds for sub-second durations.</returns>
    public static string FormatDetailed(long milliseconds)
    {
        var span = TimeSpan.FromMilliseconds(milliseconds);

        // For very short durations, show milliseconds
        if (span.TotalSeconds < 1)
        {
            if (milliseconds == 0)
                return "0 milliseconds";
            return $"{milliseconds} millisecond{(milliseconds != 1 ? "s" : "")}";
        }

        // For longer durations, use standard format
        return Format(milliseconds);
    }

    /// <summary>
    /// Formats a duration as a compact string (e.g., "2m 34s").
    /// </summary>
    /// <param name="milliseconds">Duration in milliseconds.</param>
    /// <returns>Compact duration string.</returns>
    public static string FormatCompact(long milliseconds)
    {
        var span = TimeSpan.FromMilliseconds(milliseconds);

        if (span.TotalSeconds < 1)
            return "<1s";

        var parts = new List<string>();

        if (span.Days > 0)
            parts.Add($"{span.Days}d");

        if (span.Hours > 0)
            parts.Add($"{span.Hours}h");

        if (span.Minutes > 0)
            parts.Add($"{span.Minutes}m");

        if (span.Seconds > 0)
            parts.Add($"{span.Seconds}s");

        return string.Join(" ", parts);
    }
}
