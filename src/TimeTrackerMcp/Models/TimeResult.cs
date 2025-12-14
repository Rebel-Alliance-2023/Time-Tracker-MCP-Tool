using System.Text.Json.Serialization;

namespace TimeTrackerMcp.Models;

/// <summary>
/// Represents the result of a time query operation.
/// </summary>
public record TimeResult
{
    /// <summary>
    /// The current time in the requested format.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public required string Timestamp { get; init; }

    /// <summary>
    /// The timezone used for the response.
    /// </summary>
    [JsonPropertyName("timezone")]
    public required string Timezone { get; init; }

    /// <summary>
    /// UTC offset in ±HH:MM format.
    /// </summary>
    [JsonPropertyName("utc_offset")]
    public required string UtcOffset { get; init; }
}
