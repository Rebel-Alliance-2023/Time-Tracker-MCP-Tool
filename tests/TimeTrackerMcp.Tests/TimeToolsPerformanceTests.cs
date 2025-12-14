using System.Diagnostics;
using TimeTrackerMcp.Services;
using TimeTrackerMcp.Tools;
using Xunit;
using Xunit.Abstractions;

namespace TimeTrackerMcp.Tests;

/// <summary>
/// Performance tests for TimeTools.
/// </summary>
public class TimeToolsPerformanceTests
{
    private readonly ITestOutputHelper _output;
    private readonly TimeZoneResolver _resolver = new();

    public TimeToolsPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // M1-029: Verify tool latency < 1ms on dev machine
    [Fact]
    public void TimeGetCurrent_Latency_ShouldBeLessThan1Ms()
    {
        // Warm up - run once to JIT compile
        TimeTools.time_get_current(_resolver, "iso8601", "UTC");

        // Measure multiple iterations
        const int iterations = 1000;
        var stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
        {
            TimeTools.time_get_current(_resolver, "iso8601", "UTC");
        }

        stopwatch.Stop();

        var averageMs = stopwatch.Elapsed.TotalMilliseconds / iterations;
        var averageMicroseconds = averageMs * 1000;

        _output.WriteLine($"Total time for {iterations} calls: {stopwatch.Elapsed.TotalMilliseconds:F2} ms");
        _output.WriteLine($"Average latency per call: {averageMs:F4} ms ({averageMicroseconds:F1} µs)");

        // Assert average latency is less than 1ms
        Assert.True(averageMs < 1.0, 
            $"Average latency {averageMs:F4} ms exceeds 1ms threshold");
    }

    [Fact]
    public void TimeGetCurrent_AllFormats_ShouldBeFast()
    {
        // Warm up
        TimeTools.time_get_current(_resolver, "iso8601", "UTC");

        var formats = new[] { "iso8601", "unix", "unix_ms", "friendly" };
        const int iterations = 100;

        foreach (var format in formats)
        {
            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < iterations; i++)
            {
                TimeTools.time_get_current(_resolver, format, "UTC");
            }

            stopwatch.Stop();
            var averageMs = stopwatch.Elapsed.TotalMilliseconds / iterations;

            _output.WriteLine($"Format '{format}': {averageMs:F4} ms average");

            Assert.True(averageMs < 1.0,
                $"Format '{format}' average latency {averageMs:F4} ms exceeds 1ms threshold");
        }
    }

    [Fact]
    public void TimeGetCurrent_WithTimezoneConversion_ShouldBeFast()
    {
        // Warm up
        TimeTools.time_get_current(_resolver, "iso8601", "America/New_York");

        var timezones = new[] { "local", "UTC", "America/New_York", "Europe/London", "Asia/Tokyo" };
        const int iterations = 100;

        foreach (var tz in timezones)
        {
            var stopwatch = Stopwatch.StartNew();

            for (int i = 0; i < iterations; i++)
            {
                TimeTools.time_get_current(_resolver, "iso8601", tz);
            }

            stopwatch.Stop();
            var averageMs = stopwatch.Elapsed.TotalMilliseconds / iterations;

            _output.WriteLine($"Timezone '{tz}': {averageMs:F4} ms average");

            Assert.True(averageMs < 1.0,
                $"Timezone '{tz}' average latency {averageMs:F4} ms exceeds 1ms threshold");
        }
    }
}
