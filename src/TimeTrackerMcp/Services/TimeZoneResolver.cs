using TimeZoneConverter;

namespace TimeTrackerMcp.Services;

/// <summary>
/// Resolves timezone identifiers to TimeZoneInfo objects.
/// Supports 'local', 'UTC', and IANA timezone names.
/// </summary>
public class TimeZoneResolver : ITimeZoneResolver
{
    /// <summary>
    /// Resolves a timezone identifier to a TimeZoneInfo object.
    /// </summary>
    /// <param name="timezoneId">The timezone identifier: 'local', 'UTC', or IANA name</param>
    /// <returns>A result containing either the TimeZoneInfo or an error message.</returns>
    public TimeZoneResolverResult Resolve(string timezoneId)
    {
        if (string.IsNullOrWhiteSpace(timezoneId))
        {
            timezoneId = "local";
        }

        try
        {
            // M1-005: Handle 'local' timezone
            if (timezoneId.Equals("local", StringComparison.OrdinalIgnoreCase))
            {
                return TimeZoneResolverResult.Ok(TimeZoneInfo.Local);
            }

            // M1-006: Handle 'UTC' timezone
            if (timezoneId.Equals("UTC", StringComparison.OrdinalIgnoreCase))
            {
                return TimeZoneResolverResult.Ok(TimeZoneInfo.Utc);
            }

            // M1-007: IANA to Windows timezone mapping using TimeZoneConverter
            // Try to convert IANA timezone to Windows timezone
            if (TZConvert.TryGetTimeZoneInfo(timezoneId, out var timeZoneInfo))
            {
                return TimeZoneResolverResult.Ok(timeZoneInfo);
            }

            // M1-008: Return structured error for unknown timezone
            return TimeZoneResolverResult.Error(
                $"Unknown timezone: '{timezoneId}'. Use 'local', 'UTC', or a valid IANA timezone name (e.g., 'America/New_York').",
                "UNKNOWN_TIMEZONE"
            );
        }
        catch (Exception ex)
        {
            // M1-008: Return structured error for any exception
            return TimeZoneResolverResult.Error(
                $"Failed to resolve timezone '{timezoneId}': {ex.Message}",
                "TIMEZONE_RESOLUTION_ERROR"
            );
        }
    }
}
