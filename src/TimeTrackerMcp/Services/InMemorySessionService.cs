using System.Collections.Concurrent;
using System.Diagnostics;
using TimeTrackerMcp.Models;

namespace TimeTrackerMcp.Services;

/// <summary>
/// In-memory implementation of ISessionService with retention limits.
/// </summary>
public class InMemorySessionService : ISessionService
{
    // M2-016: ConcurrentDictionary storage
    private readonly ConcurrentDictionary<string, Session> _sessions = new();
    
    // M2-024, M2-025, M2-026, M2-027: Retention limits
    private const int MaxSessions = 100;
    private const int MaxTasksPerSession = 500;
    private static readonly TimeSpan MaxSessionAge = TimeSpan.FromHours(24);
    private static readonly TimeSpan MaxInactivityTime = TimeSpan.FromHours(4);

    private readonly ITimeZoneResolver _timeZoneResolver;

    public InMemorySessionService(ITimeZoneResolver timeZoneResolver)
    {
        _timeZoneResolver = timeZoneResolver;
    }

    public int SessionCount => _sessions.Count;

    // M2-017, M2-018, M2-024: StartSession implementation
    public SessionResult StartSession(
        string milestoneId,
        List<string> taskIds,
        string? milestoneName = null,
        string? timezone = null,
        Dictionary<string, string>? metadata = null,
        List<string>? tags = null,
        string? mcpSessionId = null)
    {
        // M2-024: Enforce max sessions limit
        if (_sessions.Count >= MaxSessions)
        {
            // Try cleanup first
            CleanupExpiredSessions();
            
            if (_sessions.Count >= MaxSessions)
            {
                return SessionResult.Error(
                    $"Maximum session limit ({MaxSessions}) reached. End existing sessions or wait for expiration.",
                    "MAX_SESSIONS_REACHED");
            }
        }

        // Validate required parameters
        if (string.IsNullOrWhiteSpace(milestoneId))
        {
            return SessionResult.Error("milestone_id is required.", "MISSING_MILESTONE_ID");
        }

        if (taskIds == null || taskIds.Count == 0)
        {
            return SessionResult.Error("task_ids is required and must not be empty.", "MISSING_TASK_IDS");
        }

        // Resolve timezone
        var tzResult = _timeZoneResolver.Resolve(timezone ?? "local");
        if (!tzResult.Success)
        {
            return SessionResult.Error(tzResult.ErrorMessage!, tzResult.ErrorCode!);
        }

        var now = DateTimeOffset.UtcNow;
        var ticks = Stopwatch.GetTimestamp();

        // M2-017: Create session with GUID, timestamp, ticks
        var session = new Session
        {
            SessionId = Guid.NewGuid().ToString("N"),
            McpSessionId = mcpSessionId, // M2-018: Bind MCP session ID
            MilestoneId = milestoneId,
            MilestoneName = milestoneName,
            TaskIds = taskIds,
            StartTime = TimeZoneInfo.ConvertTime(now, tzResult.TimeZone!),
            StartTicks = ticks,
            Timezone = tzResult.TimeZone!.Id,
            Metadata = metadata,
            Tags = tags,
            LastActivityTime = now
        };

        // Initialize task records as not_started
        foreach (var taskId in taskIds)
        {
            session.Tasks.Add(new TaskRecord
            {
                TaskId = taskId,
                Status = Models.TaskStatus.NotStarted
            });
        }

        if (!_sessions.TryAdd(session.SessionId, session))
        {
            return SessionResult.Error("Failed to create session. Please try again.", "SESSION_CREATION_FAILED");
        }

        return SessionResult.Ok(session);
    }

    // M2-019, M2-020: EndSession implementation
    public SessionResult EndSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return SessionResult.Error("session_id is required.", "MISSING_SESSION_ID");
        }

        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return SessionResult.Error($"Session '{sessionId}' not found.", "SESSION_NOT_FOUND");
        }

        // M2-020: Idempotent - return existing if already ended
        if (session.IsEnded)
        {
            return SessionResult.Ok(session);
        }

        var now = DateTimeOffset.UtcNow;
        var ticks = Stopwatch.GetTimestamp();

        // End any in-progress tasks
        foreach (var task in session.Tasks.Where(t => t.IsInProgress))
        {
            task.EndTime = now;
            task.EndTicks = ticks;
            task.DurationMs = task.CalculateDurationMs();
            task.Status = Models.TaskStatus.Completed;
        }

        // M2-019: Calculate duration from monotonic ticks
        session.EndTime = now;
        session.EndTicks = ticks;
        session.LastActivityTime = now;

        return SessionResult.Ok(session);
    }

    // M2-021: GetSession implementation
    public Session? GetSession(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        _sessions.TryGetValue(sessionId, out var session);
        return session;
    }

    // M2-022: GetSessionSummary implementation
    public SessionResult GetSessionSummary(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return SessionResult.Error("session_id is required.", "MISSING_SESSION_ID");
        }

        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return SessionResult.Error($"Session '{sessionId}' not found.", "SESSION_NOT_FOUND");
        }

        // Update last activity time
        session.LastActivityTime = DateTimeOffset.UtcNow;

        return SessionResult.Ok(session);
    }

    // M2-013: StartTask implementation
    public TaskResult StartTask(
        string sessionId,
        string taskId,
        string? taskName = null,
        string? externalTaskId = null,
        string? workItemId = null,
        Dictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return TaskResult.Error("session_id is required.", "MISSING_SESSION_ID");
        }

        if (string.IsNullOrWhiteSpace(taskId))
        {
            return TaskResult.Error("task_id is required.", "MISSING_TASK_ID");
        }

        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return TaskResult.Error($"Session '{sessionId}' not found.", "SESSION_NOT_FOUND");
        }

        if (session.IsEnded)
        {
            return TaskResult.Error($"Session '{sessionId}' has already ended.", "SESSION_ENDED");
        }

        // M2-025: Enforce max tasks per session
        var startedTasks = session.Tasks.Count(t => t.Status != Models.TaskStatus.NotStarted);
        if (startedTasks >= MaxTasksPerSession)
        {
            return TaskResult.Error(
                $"Maximum tasks per session limit ({MaxTasksPerSession}) reached.",
                "MAX_TASKS_REACHED");
        }

        var now = DateTimeOffset.UtcNow;
        var ticks = Stopwatch.GetTimestamp();

        // Find existing task or create new one
        var task = session.Tasks.FirstOrDefault(t => t.TaskId == taskId);
        
        if (task == null)
        {
            // Task not in predefined list, add it
            task = new TaskRecord { TaskId = taskId };
            session.Tasks.Add(task);
        }

        // Idempotent: if already running, return with flag
        if (task.IsInProgress)
        {
            task.AlreadyRunning = true;
            return TaskResult.Ok(task, session);
        }

        // Start the task
        task.StartTime = now;
        task.StartTicks = ticks;
        task.Status = Models.TaskStatus.InProgress;
        task.TaskName = taskName;
        task.ExternalTaskId = externalTaskId;
        task.WorkItemId = workItemId;
        task.Metadata = metadata;
        task.AlreadyRunning = false;

        session.LastActivityTime = now;

        return TaskResult.Ok(task, session);
    }

    // M2-013: EndTask implementation
    public TaskResult EndTask(
        string sessionId,
        string taskId,
        string status = "completed",
        Dictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return TaskResult.Error("session_id is required.", "MISSING_SESSION_ID");
        }

        if (string.IsNullOrWhiteSpace(taskId))
        {
            return TaskResult.Error("task_id is required.", "MISSING_TASK_ID");
        }

        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            return TaskResult.Error($"Session '{sessionId}' not found.", "SESSION_NOT_FOUND");
        }

        var task = session.Tasks.FirstOrDefault(t => t.TaskId == taskId);
        
        if (task == null)
        {
            return TaskResult.Error($"Task '{taskId}' not found in session.", "TASK_NOT_FOUND");
        }

        if (!task.IsInProgress)
        {
            return TaskResult.Error(
                $"Task '{taskId}' is not in progress (current status: {task.Status}).",
                "TASK_NOT_STARTED");
        }

        var now = DateTimeOffset.UtcNow;
        var ticks = Stopwatch.GetTimestamp();

        task.EndTime = now;
        task.EndTicks = ticks;
        task.DurationMs = task.CalculateDurationMs();
        task.Status = status == "skipped" ? Models.TaskStatus.Skipped : Models.TaskStatus.Completed;

        // Merge metadata
        if (metadata != null)
        {
            task.Metadata ??= new Dictionary<string, string>();
            foreach (var kvp in metadata)
            {
                task.Metadata[kvp.Key] = kvp.Value;
            }
        }

        session.LastActivityTime = now;

        return TaskResult.Ok(task, session);
    }

    // M2-028: CleanupExpiredSessions implementation
    public int CleanupExpiredSessions()
    {
        var now = DateTimeOffset.UtcNow;
        var expiredSessions = new List<string>();

        foreach (var kvp in _sessions)
        {
            var session = kvp.Value;
            
            // M2-026: Check max session age
            if (now - session.StartTime > MaxSessionAge)
            {
                expiredSessions.Add(kvp.Key);
                continue;
            }

            // M2-027: Check inactivity (only for non-ended sessions)
            if (!session.IsEnded && now - session.LastActivityTime > MaxInactivityTime)
            {
                expiredSessions.Add(kvp.Key);
            }
        }

        foreach (var sessionId in expiredSessions)
        {
            _sessions.TryRemove(sessionId, out _);
        }

        return expiredSessions.Count;
    }
}
