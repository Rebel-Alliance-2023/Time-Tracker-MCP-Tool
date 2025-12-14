using System.Text.Json.Serialization;

namespace TimeTrackerMcp.Models;

/// <summary>
/// Represents a time tracking session for a milestone with associated tasks.
/// </summary>
public class Session
{
    // M2-002: Session identification properties
    /// <summary>
    /// Unique identifier for this session (GUID).
    /// </summary>
    [JsonPropertyName("session_id")]
    public required string SessionId { get; init; }

    /// <summary>
    /// MCP protocol session ID (if bound).
    /// </summary>
    [JsonPropertyName("mcp_session_id")]
    public string? McpSessionId { get; set; }

    /// <summary>
    /// Milestone identifier being tracked.
    /// </summary>
    [JsonPropertyName("milestone_id")]
    public required string MilestoneId { get; init; }

    /// <summary>
    /// Optional human-readable milestone name.
    /// </summary>
    [JsonPropertyName("milestone_name")]
    public string? MilestoneName { get; init; }

    // M2-003: Task and timing properties
    /// <summary>
    /// List of task IDs associated with this session.
    /// </summary>
    [JsonPropertyName("task_ids")]
    public required List<string> TaskIds { get; init; }

    /// <summary>
    /// Wall-clock time when session started.
    /// </summary>
    [JsonPropertyName("start_time")]
    public required DateTimeOffset StartTime { get; init; }

    /// <summary>
    /// Monotonic tick count when session started (for accurate duration).
    /// </summary>
    [JsonPropertyName("start_ticks")]
    public required long StartTicks { get; init; }

    /// <summary>
    /// Wall-clock time when session ended (null if still active).
    /// </summary>
    [JsonPropertyName("end_time")]
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// Monotonic tick count when session ended (null if still active).
    /// </summary>
    [JsonPropertyName("end_ticks")]
    public long? EndTicks { get; set; }

    // M2-004: Additional properties
    /// <summary>
    /// Timezone used for this session.
    /// </summary>
    [JsonPropertyName("timezone")]
    public required string Timezone { get; init; }

    /// <summary>
    /// Optional metadata key-value pairs.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// Optional tags for categorization.
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string>? Tags { get; set; }

    /// <summary>
    /// Last activity time (updated on task start/end).
    /// </summary>
    [JsonPropertyName("last_activity_time")]
    public DateTimeOffset LastActivityTime { get; set; }

    // M2-005: Tasks list
    /// <summary>
    /// List of task records for this session.
    /// </summary>
    [JsonPropertyName("tasks")]
    public List<TaskRecord> Tasks { get; init; } = new();

    /// <summary>
    /// Indicates if the session has ended.
    /// </summary>
    [JsonIgnore]
    public bool IsEnded => EndTime.HasValue;

    /// <summary>
    /// Calculates the duration in milliseconds using monotonic ticks.
    /// </summary>
    public long? GetDurationMs()
    {
        if (!EndTicks.HasValue)
            return null;

        var elapsedTicks = EndTicks.Value - StartTicks;
        return elapsedTicks * 1000 / System.Diagnostics.Stopwatch.Frequency;
    }

    /// <summary>
    /// Gets the elapsed time since session start (for active sessions).
    /// </summary>
    public long GetElapsedMs()
    {
        var currentTicks = System.Diagnostics.Stopwatch.GetTimestamp();
        var elapsedTicks = currentTicks - StartTicks;
        return elapsedTicks * 1000 / System.Diagnostics.Stopwatch.Frequency;
    }
}
