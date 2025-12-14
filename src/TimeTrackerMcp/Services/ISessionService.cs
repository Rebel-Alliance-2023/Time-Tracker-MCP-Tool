using TimeTrackerMcp.Models;

namespace TimeTrackerMcp.Services;

/// <summary>
/// Service for managing time tracking sessions.
/// </summary>
public interface ISessionService
{
    // M2-012: Session lifecycle methods
    
    /// <summary>
    /// Starts a new session for tracking a milestone.
    /// </summary>
    /// <param name="milestoneId">Required milestone identifier.</param>
    /// <param name="taskIds">Required list of task IDs to track.</param>
    /// <param name="milestoneName">Optional human-readable milestone name.</param>
    /// <param name="timezone">Optional timezone (defaults to local).</param>
    /// <param name="metadata">Optional metadata key-value pairs.</param>
    /// <param name="tags">Optional tags for categorization.</param>
    /// <param name="mcpSessionId">Optional MCP protocol session ID.</param>
    /// <returns>Result containing the created session or error.</returns>
    SessionResult StartSession(
        string milestoneId,
        List<string> taskIds,
        string? milestoneName = null,
        string? timezone = null,
        Dictionary<string, string>? metadata = null,
        List<string>? tags = null,
        string? mcpSessionId = null);

    /// <summary>
    /// Ends a session and calculates final duration.
    /// </summary>
    /// <param name="sessionId">The session ID to end.</param>
    /// <returns>Result containing the ended session or error.</returns>
    SessionResult EndSession(string sessionId);

    /// <summary>
    /// Gets a session by ID.
    /// </summary>
    /// <param name="sessionId">The session ID to retrieve.</param>
    /// <returns>The session if found, null otherwise.</returns>
    Session? GetSession(string sessionId);

    /// <summary>
    /// Gets a session summary without ending it.
    /// </summary>
    /// <param name="sessionId">The session ID to summarize.</param>
    /// <returns>Result containing the session summary or error.</returns>
    SessionResult GetSessionSummary(string sessionId);

    // M2-013: Task methods
    
    /// <summary>
    /// Starts a task within a session.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="taskId">The task ID to start.</param>
    /// <param name="taskName">Optional task name.</param>
    /// <param name="externalTaskId">Optional external task ID.</param>
    /// <param name="workItemId">Optional work item ID.</param>
    /// <param name="metadata">Optional metadata.</param>
    /// <returns>Result containing the task record or error.</returns>
    TaskResult StartTask(
        string sessionId,
        string taskId,
        string? taskName = null,
        string? externalTaskId = null,
        string? workItemId = null,
        Dictionary<string, string>? metadata = null);

    /// <summary>
    /// Ends a task within a session.
    /// </summary>
    /// <param name="sessionId">The session ID.</param>
    /// <param name="taskId">The task ID to end.</param>
    /// <param name="status">The final status (completed, skipped).</param>
    /// <param name="metadata">Optional metadata to merge.</param>
    /// <returns>Result containing the task record or error.</returns>
    TaskResult EndTask(
        string sessionId,
        string taskId,
        string status = "completed",
        Dictionary<string, string>? metadata = null);

    // M2-014: Cleanup method
    
    /// <summary>
    /// Cleans up expired sessions based on age and inactivity limits.
    /// </summary>
    /// <returns>Number of sessions cleaned up.</returns>
    int CleanupExpiredSessions();

    /// <summary>
    /// Gets the current number of active sessions.
    /// </summary>
    int SessionCount { get; }
}

/// <summary>
/// Result of a session operation.
/// </summary>
public record SessionResult
{
    public bool Success { get; init; }
    public Session? Session { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }

    public static SessionResult Ok(Session session) => new()
    {
        Success = true,
        Session = session
    };

    public static SessionResult Error(string message, string code) => new()
    {
        Success = false,
        ErrorMessage = message,
        ErrorCode = code
    };
}

/// <summary>
/// Result of a task operation.
/// </summary>
public record TaskResult
{
    public bool Success { get; init; }
    public TaskRecord? Task { get; init; }
    public Session? Session { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }

    public static TaskResult Ok(TaskRecord task, Session session) => new()
    {
        Success = true,
        Task = task,
        Session = session
    };

    public static TaskResult Error(string message, string code) => new()
    {
        Success = false,
        ErrorMessage = message,
        ErrorCode = code
    };
}
