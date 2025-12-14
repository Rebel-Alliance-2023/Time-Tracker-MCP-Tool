namespace TimeTrackerMcp.Services;

/// <summary>
/// Resolves timezone identifiers to TimeZoneInfo objects.
/// Supports 'local', 'UTC', and IANA timezone names.
/// </summary>
public interface ITimeZoneResolver
{
    /// <summary>
    /// Resolves a timezone identifier to a TimeZoneInfo object.
    /// </summary>
    /// <param name="timezoneId">The timezone identifier: 'local', 'UTC', or IANA name (e.g., 'America/New_York')</param>
    /// <returns>A result containing either the TimeZoneInfo or an error message.</returns>
    TimeZoneResolverResult Resolve(string timezoneId);
}

/// <summary>
/// Result of a timezone resolution operation.
/// </summary>
public record TimeZoneResolverResult
{
    public bool Success { get; init; }
    public TimeZoneInfo? TimeZone { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }

    public static TimeZoneResolverResult Ok(TimeZoneInfo timeZone) => new()
    {
        Success = true,
        TimeZone = timeZone
    };

    public static TimeZoneResolverResult Error(string message, string code = "UNKNOWN_TIMEZONE") => new()
    {
        Success = false,
        ErrorMessage = message,
        ErrorCode = code
    };
}
