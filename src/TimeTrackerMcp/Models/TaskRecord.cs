using System.Text.Json.Serialization;

namespace TimeTrackerMcp.Models;

/// <summary>
/// Represents a task timing record within a session.
/// </summary>
public class TaskRecord
{
    // M2-007: Task identification properties
    /// <summary>
    /// Task identifier (from the task list).
    /// </summary>
    [JsonPropertyName("task_id")]
    public required string TaskId { get; init; }

    /// <summary>
    /// Optional human-readable task name.
    /// </summary>
    [JsonPropertyName("task_name")]
    public string? TaskName { get; set; }

    /// <summary>
    /// Optional external task ID (e.g., from project management tool).
    /// </summary>
    [JsonPropertyName("external_task_id")]
    public string? ExternalTaskId { get; set; }

    /// <summary>
    /// Optional work item ID (e.g., Azure DevOps, Jira).
    /// </summary>
    [JsonPropertyName("work_item_id")]
    public string? WorkItemId { get; set; }

    // M2-008: Timing properties
    /// <summary>
    /// Wall-clock time when task started.
    /// </summary>
    [JsonPropertyName("start_time")]
    public DateTimeOffset? StartTime { get; set; }

    /// <summary>
    /// Monotonic tick count when task started.
    /// </summary>
    [JsonPropertyName("start_ticks")]
    public long? StartTicks { get; set; }

    /// <summary>
    /// Wall-clock time when task ended.
    /// </summary>
    [JsonPropertyName("end_time")]
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>
    /// Monotonic tick count when task ended.
    /// </summary>
    [JsonPropertyName("end_ticks")]
    public long? EndTicks { get; set; }

    /// <summary>
    /// Duration in milliseconds (computed from monotonic ticks).
    /// </summary>
    [JsonPropertyName("duration_ms")]
    public long? DurationMs { get; set; }

    // M2-009: Status and metadata properties
    /// <summary>
    /// Task status: not_started, in_progress, completed, skipped.
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = "not_started";

    /// <summary>
    /// Optional metadata key-value pairs.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// Flag indicating if task was already running when start was called (idempotent).
    /// </summary>
    [JsonPropertyName("already_running")]
    public bool AlreadyRunning { get; set; }

    /// <summary>
    /// Indicates if the task is currently in progress.
    /// </summary>
    [JsonIgnore]
    public bool IsInProgress => Status == "in_progress";

    /// <summary>
    /// Indicates if the task has been completed.
    /// </summary>
    [JsonIgnore]
    public bool IsCompleted => Status == "completed";

    /// <summary>
    /// Indicates if the task was skipped.
    /// </summary>
    [JsonIgnore]
    public bool IsSkipped => Status == "skipped";

    /// <summary>
    /// Calculates the duration in milliseconds using monotonic ticks.
    /// </summary>
    public long? CalculateDurationMs()
    {
        if (!StartTicks.HasValue || !EndTicks.HasValue)
            return null;

        var elapsedTicks = EndTicks.Value - StartTicks.Value;
        return elapsedTicks * 1000 / System.Diagnostics.Stopwatch.Frequency;
    }

    /// <summary>
    /// Gets the elapsed time since task start (for in-progress tasks).
    /// </summary>
    public long? GetElapsedMs()
    {
        if (!StartTicks.HasValue)
            return null;

        var currentTicks = System.Diagnostics.Stopwatch.GetTimestamp();
        var elapsedTicks = currentTicks - StartTicks.Value;
        return elapsedTicks * 1000 / System.Diagnostics.Stopwatch.Frequency;
    }
}

/// <summary>
/// Task status constants.
/// </summary>
public static class TaskStatus
{
    public const string NotStarted = "not_started";
    public const string InProgress = "in_progress";
    public const string Completed = "completed";
    public const string Skipped = "skipped";
}
