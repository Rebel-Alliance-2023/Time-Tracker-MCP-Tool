using System.Text.Json;
using TimeTrackerMcp.Services;
using TimeTrackerMcp.Tools;
using Xunit;

namespace TimeTrackerMcp.Tests;

/// <summary>
/// Unit tests for TimeTools.time_get_current.
/// </summary>
public class TimeToolsTests
{
    private readonly TimeZoneResolver _resolver = new();

    // M1-019: Test iso8601 format renders correctly
    [Fact]
    public void TimeGetCurrent_Iso8601Format_ReturnsValidIso8601()
    {
        // Act
        var result = TimeTools.time_get_current(_resolver, "iso8601", "UTC");
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        // Assert
        Assert.True(root.TryGetProperty("timestamp", out var timestamp));
        Assert.Contains("T", timestamp.GetString()); // ISO 8601 contains 'T' separator
        Assert.Contains("+", timestamp.GetString()!); // Contains offset
    }

    [Fact]
    public void TimeGetCurrent_Iso8601Format_ContainsTimezoneOffset()
    {
        // Act
        var result = TimeTools.time_get_current(_resolver, "iso8601", "UTC");
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        // Assert
        var timestamp = root.GetProperty("timestamp").GetString()!;
        // ISO 8601 format should end with offset like +00:00 or -05:00
        Assert.Matches(@"[+-]\d{2}:\d{2}$", timestamp);
    }

    // M1-020: Test unix format renders correctly
    [Fact]
    public void TimeGetCurrent_UnixFormat_ReturnsNumericString()
    {
        // Act
        var result = TimeTools.time_get_current(_resolver, "unix", "UTC");
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        // Assert
        Assert.True(root.TryGetProperty("timestamp", out var timestamp));
        var timestampStr = timestamp.GetString()!;
        Assert.True(long.TryParse(timestampStr, out var unixTime));
        Assert.True(unixTime > 0); // Should be positive (after 1970)
    }

    [Fact]
    public void TimeGetCurrent_UnixFormat_ReturnsSecondsNotMilliseconds()
    {
        // Act
        var result = TimeTools.time_get_current(_resolver, "unix", "UTC");
        var json = JsonDocument.Parse(result);
        var timestamp = json.RootElement.GetProperty("timestamp").GetString()!;
        var unixTime = long.Parse(timestamp);

        // Assert - Unix seconds should be ~10 digits (as of 2025), milliseconds would be ~13 digits
        Assert.True(timestamp.Length <= 11, "Unix timestamp should be in seconds, not milliseconds");
    }

    // M1-021: Test unix_ms format renders correctly
    [Fact]
    public void TimeGetCurrent_UnixMsFormat_ReturnsNumericString()
    {
        // Act
        var result = TimeTools.time_get_current(_resolver, "unix_ms", "UTC");
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        // Assert
        Assert.True(root.TryGetProperty("timestamp", out var timestamp));
        var timestampStr = timestamp.GetString()!;
        Assert.True(long.TryParse(timestampStr, out var unixTimeMs));
        Assert.True(unixTimeMs > 0);
    }

    [Fact]
    public void TimeGetCurrent_UnixMsFormat_ReturnsMillisecondsNotSeconds()
    {
        // Act
        var result = TimeTools.time_get_current(_resolver, "unix_ms", "UTC");
        var json = JsonDocument.Parse(result);
        var timestamp = json.RootElement.GetProperty("timestamp").GetString()!;

        // Assert - Unix milliseconds should be ~13 digits (as of 2025)
        Assert.True(timestamp.Length >= 13, "Unix_ms timestamp should be in milliseconds");
    }

    // M1-022: Test friendly format renders correctly
    [Fact]
    public void TimeGetCurrent_FriendlyFormat_ReturnsHumanReadable()
    {
        // Act
        var result = TimeTools.time_get_current(_resolver, "friendly", "UTC");
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        // Assert
        Assert.True(root.TryGetProperty("timestamp", out var timestamp));
        var timestampStr = timestamp.GetString()!;
        // Friendly format should contain month name (e.g., "December")
        Assert.Matches(@"\w+ \d{1,2}, \d{4}", timestampStr); // e.g., "December 14, 2025"
    }

    [Fact]
    public void TimeGetCurrent_FriendlyFormat_ContainsAmPm()
    {
        // Act
        var result = TimeTools.time_get_current(_resolver, "friendly", "UTC");
        var json = JsonDocument.Parse(result);
        var timestamp = json.RootElement.GetProperty("timestamp").GetString()!;

        // Assert - Should contain AM or PM
        Assert.True(timestamp.Contains("AM") || timestamp.Contains("PM"),
            "Friendly format should contain AM or PM");
    }

    // M1-027: Test unknown format returns structured error
    [Fact]
    public void TimeGetCurrent_UnknownFormat_ReturnsError()
    {
        // Act
        var result = TimeTools.time_get_current(_resolver, "invalid_format", "UTC");
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        // Assert
        Assert.True(root.TryGetProperty("error", out var error));
        Assert.True(error.GetBoolean());
        Assert.True(root.TryGetProperty("error_code", out var errorCode));
        Assert.Equal("UNKNOWN_FORMAT", errorCode.GetString());
        Assert.True(root.TryGetProperty("error_message", out var errorMessage));
        Assert.Contains("invalid_format", errorMessage.GetString());
    }

    [Fact]
    public void TimeGetCurrent_UnknownFormat_ListsValidOptions()
    {
        // Act
        var result = TimeTools.time_get_current(_resolver, "bad", "UTC");
        var json = JsonDocument.Parse(result);
        var errorMessage = json.RootElement.GetProperty("error_message").GetString()!;

        // Assert - Error message should list valid options
        Assert.Contains("iso8601", errorMessage);
        Assert.Contains("unix", errorMessage);
        Assert.Contains("unix_ms", errorMessage);
        Assert.Contains("friendly", errorMessage);
    }

    // Additional tests for timezone in response
    [Fact]
    public void TimeGetCurrent_ReturnsTimezoneInResponse()
    {
        // Act
        var result = TimeTools.time_get_current(_resolver, "iso8601", "UTC");
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        // Assert
        Assert.True(root.TryGetProperty("timezone", out var timezone));
        Assert.NotNull(timezone.GetString());
    }

    [Fact]
    public void TimeGetCurrent_ReturnsUtcOffsetInResponse()
    {
        // Act
        var result = TimeTools.time_get_current(_resolver, "iso8601", "UTC");
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        // Assert
        Assert.True(root.TryGetProperty("utc_offset", out var utcOffset));
        var offsetStr = utcOffset.GetString()!;
        Assert.Matches(@"^[+-]\d{2}:\d{2}$", offsetStr);
    }

    [Fact]
    public void TimeGetCurrent_UtcTimezone_ReturnsZeroOffset()
    {
        // Act
        var result = TimeTools.time_get_current(_resolver, "iso8601", "UTC");
        var json = JsonDocument.Parse(result);
        var utcOffset = json.RootElement.GetProperty("utc_offset").GetString();

        // Assert
        Assert.Equal("+00:00", utcOffset);
    }

    // Test unknown timezone through tool
    [Fact]
    public void TimeGetCurrent_UnknownTimezone_ReturnsError()
    {
        // Act
        var result = TimeTools.time_get_current(_resolver, "iso8601", "Invalid/Timezone");
        var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        // Assert
        Assert.True(root.TryGetProperty("error", out var error));
        Assert.True(error.GetBoolean());
        Assert.True(root.TryGetProperty("error_code", out var errorCode));
        Assert.Equal("UNKNOWN_TIMEZONE", errorCode.GetString());
    }
}
