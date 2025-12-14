using TimeTrackerMcp.Services;
using Xunit;

namespace TimeTrackerMcp.Tests;

/// <summary>
/// Unit tests for TimeZoneResolver.
/// </summary>
public class TimeZoneResolverTests
{
    private readonly TimeZoneResolver _resolver = new();

    // M1-023: Test local timezone returns system timezone
    [Fact]
    public void Resolve_LocalTimezone_ReturnsSystemTimezone()
    {
        // Act
        var result = _resolver.Resolve("local");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.TimeZone);
        Assert.Equal(TimeZoneInfo.Local.Id, result.TimeZone!.Id);
    }

    // M1-024: Test UTC timezone returns UTC
    [Fact]
    public void Resolve_UtcTimezone_ReturnsUtc()
    {
        // Act
        var result = _resolver.Resolve("UTC");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.TimeZone);
        Assert.Equal(TimeZoneInfo.Utc.Id, result.TimeZone!.Id);
    }

    [Fact]
    public void Resolve_UtcTimezone_CaseInsensitive()
    {
        // Act
        var result = _resolver.Resolve("utc");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.TimeZone);
        Assert.Equal(TimeZoneInfo.Utc.Id, result.TimeZone!.Id);
    }

    // M1-025: Test IANA timezone converts correctly
    [Fact]
    public void Resolve_IanaTimezone_AmericaNewYork_Succeeds()
    {
        // Act
        var result = _resolver.Resolve("America/New_York");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.TimeZone);
        // On Windows, this maps to "Eastern Standard Time"
        Assert.Contains("Eastern", result.TimeZone!.Id, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_IanaTimezone_EuropeLondon_Succeeds()
    {
        // Act
        var result = _resolver.Resolve("Europe/London");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.TimeZone);
    }

    [Fact]
    public void Resolve_IanaTimezone_AsiaTokyo_Succeeds()
    {
        // Act
        var result = _resolver.Resolve("Asia/Tokyo");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.TimeZone);
    }

    // M1-026: Test unknown timezone returns structured error
    [Fact]
    public void Resolve_UnknownTimezone_ReturnsError()
    {
        // Act
        var result = _resolver.Resolve("Invalid/Timezone");

        // Assert
        Assert.False(result.Success);
        Assert.Null(result.TimeZone);
        Assert.NotNull(result.ErrorMessage);
        Assert.Equal("UNKNOWN_TIMEZONE", result.ErrorCode);
        Assert.Contains("Unknown timezone", result.ErrorMessage);
    }

    [Fact]
    public void Resolve_EmptyString_DefaultsToLocal()
    {
        // Act
        var result = _resolver.Resolve("");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.TimeZone);
        Assert.Equal(TimeZoneInfo.Local.Id, result.TimeZone!.Id);
    }

    [Fact]
    public void Resolve_NullString_DefaultsToLocal()
    {
        // Act
        var result = _resolver.Resolve(null!);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.TimeZone);
        Assert.Equal(TimeZoneInfo.Local.Id, result.TimeZone!.Id);
    }

    // M1-028: Test UTC offset formatting is correct
    [Fact]
    public void Resolve_TimezoneHasCorrectOffset()
    {
        // Act
        var result = _resolver.Resolve("UTC");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.TimeZone);
        Assert.Equal(TimeSpan.Zero, result.TimeZone!.BaseUtcOffset);
    }

    [Fact]
    public void Resolve_AmericaNewYork_HasNegativeOffset()
    {
        // Act
        var result = _resolver.Resolve("America/New_York");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.TimeZone);
        // Eastern Time is UTC-5 (standard) or UTC-4 (daylight)
        Assert.True(result.TimeZone!.BaseUtcOffset.Hours <= -4);
    }
}
