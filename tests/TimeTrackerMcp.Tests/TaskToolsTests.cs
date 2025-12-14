using System.Text.Json;
using TimeTrackerMcp.Models;
using TimeTrackerMcp.Services;
using TimeTrackerMcp.Tools;
using Xunit;

// Alias to avoid ambiguity with System.Threading.Tasks.TaskStatus
using TaskStatus = TimeTrackerMcp.Models.TaskStatus;

namespace TimeTrackerMcp.Tests;

/// <summary>
/// Unit tests for Task Tools and related functionality.
/// </summary>
public class TaskToolsTests
{
    private readonly ITimeZoneResolver _timeZoneResolver;
    private readonly InMemorySessionService _sessionService;

    public TaskToolsTests()
    {
        _timeZoneResolver = new TimeZoneResolver();
        _sessionService = new InMemorySessionService(_timeZoneResolver);
    }

    private string CreateTestSession(List<string>? taskIds = null)
    {
        taskIds ??= new List<string> { "task-1", "task-2", "task-3" };
        var result = _sessionService.StartSession("test-milestone", taskIds);
        return result.Session!.SessionId;
    }

    #region M3-027: Task start returns correct data

    [Fact]
    public void TaskStart_ReturnsCorrectData()
    {
        // Arrange
        var sessionId = CreateTestSession();

        // Act
        var result = _sessionService.StartTask(sessionId, "task-1", "First Task");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Task);
        Assert.Equal("task-1", result.Task.TaskId);
        Assert.Equal("First Task", result.Task.TaskName);
        Assert.Equal(TaskStatus.InProgress, result.Task.Status);
        Assert.NotNull(result.Task.StartTime);
        Assert.True(result.Task.StartTicks > 0);
    }

    [Fact]
    public void TimeTaskStart_ReturnsCorrectJsonData()
    {
        // Arrange
        var sessionId = CreateTestSession();

        // Act
        var jsonResult = TimeTools.time_task_start(
            _sessionService,
            session_id: sessionId,
            task_id: "task-1",
            task_name: "First Task");

        // Assert
        var result = JsonSerializer.Deserialize<JsonElement>(jsonResult);
        Assert.Equal("task-1", result.GetProperty("task_id").GetString());
        Assert.Equal("First Task", result.GetProperty("task_name").GetString());
        Assert.Equal(sessionId, result.GetProperty("session_id").GetString());
        Assert.True(result.TryGetProperty("start_time", out _));
        Assert.True(result.TryGetProperty("start_time_friendly", out _));
        Assert.True(result.TryGetProperty("session_elapsed_ms", out _));
        Assert.True(result.TryGetProperty("tasks_completed", out _));
        Assert.True(result.TryGetProperty("tasks_remaining", out _));
    }

    [Fact]
    public void TaskStart_WithOptionalParameters_StoresThem()
    {
        // Arrange
        var sessionId = CreateTestSession();
        var metadata = new Dictionary<string, string> { ["key"] = "value" };

        // Act
        var result = _sessionService.StartTask(
            sessionId: sessionId,
            taskId: "task-1",
            taskName: "First Task",
            externalTaskId: "EXT-123",
            workItemId: "WI-456",
            metadata: metadata);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("EXT-123", result.Task!.ExternalTaskId);
        Assert.Equal("WI-456", result.Task.WorkItemId);
        Assert.NotNull(result.Task.Metadata);
        Assert.Equal("value", result.Task.Metadata["key"]);
    }

    #endregion

    #region M3-028: Task start is idempotent

    [Fact]
    public void TaskStart_IsIdempotent_ReturnsAlreadyRunning()
    {
        // Arrange
        var sessionId = CreateTestSession();

        // Act
        var firstStart = _sessionService.StartTask(sessionId, "task-1");
        var firstAlreadyRunning = firstStart.Task!.AlreadyRunning; // Capture immediately
        
        var secondStart = _sessionService.StartTask(sessionId, "task-1");
        var secondAlreadyRunning = secondStart.Task!.AlreadyRunning;

        // Assert
        Assert.True(firstStart.Success);
        Assert.True(secondStart.Success);
        Assert.False(firstAlreadyRunning); // First call should NOT be already running
        Assert.True(secondAlreadyRunning); // Second call SHOULD be already running
        Assert.Equal(firstStart.Task.StartTime, secondStart.Task.StartTime);
    }

    [Fact]
    public void TimeTaskStart_IsIdempotent_ReturnsAlreadyRunningFlag()
    {
        // Arrange
        var sessionId = CreateTestSession();

        // Act
        var firstResult = TimeTools.time_task_start(_sessionService, sessionId, "task-1");
        var secondResult = TimeTools.time_task_start(_sessionService, sessionId, "task-1");

        // Assert
        var first = JsonSerializer.Deserialize<JsonElement>(firstResult);
        var second = JsonSerializer.Deserialize<JsonElement>(secondResult);

        Assert.False(first.GetProperty("already_running").GetBoolean());
        Assert.True(second.GetProperty("already_running").GetBoolean());
    }

    #endregion

    #region M3-029: Task end without start returns error

    [Fact]
    public void TaskEnd_WithoutStart_ReturnsError()
    {
        // Arrange
        var sessionId = CreateTestSession();

        // Act - Try to end task that was never started
        var result = _sessionService.EndTask(sessionId, "task-1");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("TASK_NOT_STARTED", result.ErrorCode);
    }

    [Fact]
    public void TimeTaskEnd_WithoutStart_ReturnsStructuredError()
    {
        // Arrange
        var sessionId = CreateTestSession();

        // Act
        var jsonResult = TimeTools.time_task_end(_sessionService, sessionId, "task-1");

        // Assert
        var result = JsonSerializer.Deserialize<JsonElement>(jsonResult);
        Assert.True(result.GetProperty("error").GetBoolean());
        Assert.Equal("TASK_NOT_STARTED", result.GetProperty("error_code").GetString());
    }

    [Fact]
    public void TaskEnd_TaskNotInSession_ReturnsError()
    {
        // Arrange
        var sessionId = CreateTestSession(new List<string> { "task-1", "task-2" });

        // Act - Try to end task that doesn't exist in session
        var result = _sessionService.EndTask(sessionId, "task-99");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("TASK_NOT_FOUND", result.ErrorCode);
    }

    #endregion

    #region M3-030: Task end computes correct duration

    [Fact]
    public void TaskEnd_ComputesCorrectDuration()
    {
        // Arrange
        var sessionId = CreateTestSession();
        _sessionService.StartTask(sessionId, "task-1");

        // Wait a bit
        Thread.Sleep(50);

        // Act
        var result = _sessionService.EndTask(sessionId, "task-1");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Task!.DurationMs);
        Assert.True(result.Task.DurationMs >= 40, $"Duration should be at least 40ms, was {result.Task.DurationMs}ms");
    }

    [Fact]
    public void TimeTaskEnd_ReturnsCorrectDurationFields()
    {
        // Arrange
        var sessionId = CreateTestSession();
        TimeTools.time_task_start(_sessionService, sessionId, "task-1");
        Thread.Sleep(50);

        // Act
        var jsonResult = TimeTools.time_task_end(_sessionService, sessionId, "task-1");

        // Assert
        var result = JsonSerializer.Deserialize<JsonElement>(jsonResult);
        Assert.True(result.GetProperty("duration_ms").GetInt64() >= 40);
        Assert.NotNull(result.GetProperty("duration").GetString());
        Assert.NotNull(result.GetProperty("end_time").GetString());
    }

    #endregion

    #region M3-031: Skipped status works correctly

    [Fact]
    public void TaskEnd_WithSkippedStatus_SetsCorrectStatus()
    {
        // Arrange
        var sessionId = CreateTestSession();
        _sessionService.StartTask(sessionId, "task-1");

        // Act
        var result = _sessionService.EndTask(sessionId, "task-1", status: "skipped");

        // Assert
        Assert.True(result.Success);
        Assert.Equal(TaskStatus.Skipped, result.Task!.Status);
    }

    [Fact]
    public void TimeTaskEnd_WithSkippedStatus_ReturnsSkippedStatus()
    {
        // Arrange
        var sessionId = CreateTestSession();
        TimeTools.time_task_start(_sessionService, sessionId, "task-1");

        // Act
        var jsonResult = TimeTools.time_task_end(_sessionService, sessionId, "task-1", status: "skipped");

        // Assert
        var result = JsonSerializer.Deserialize<JsonElement>(jsonResult);
        Assert.Equal("skipped", result.GetProperty("status").GetString());
    }

    [Fact]
    public void SkippedTasks_CountedCorrectly()
    {
        // Arrange
        var sessionId = CreateTestSession();
        
        // Start and complete task-1
        _sessionService.StartTask(sessionId, "task-1");
        _sessionService.EndTask(sessionId, "task-1", status: "completed");
        
        // Start and skip task-2
        _sessionService.StartTask(sessionId, "task-2");
        _sessionService.EndTask(sessionId, "task-2", status: "skipped");

        // Act
        var summaryResult = _sessionService.GetSessionSummary(sessionId);

        // Assert
        var completedCount = summaryResult.Session!.Tasks.Count(t => t.Status == TaskStatus.Completed);
        var skippedCount = summaryResult.Session.Tasks.Count(t => t.Status == TaskStatus.Skipped);
        Assert.Equal(1, completedCount);
        Assert.Equal(1, skippedCount);
    }

    #endregion

    #region M3-032: Overlapping/parallel tasks compute independently

    [Fact]
    public void ParallelTasks_ComputeIndependently()
    {
        // Arrange
        var sessionId = CreateTestSession();

        // Act - Start task-1, wait, start task-2, wait, end both
        _sessionService.StartTask(sessionId, "task-1");
        Thread.Sleep(50);
        _sessionService.StartTask(sessionId, "task-2");
        Thread.Sleep(50);

        var endTask1 = _sessionService.EndTask(sessionId, "task-1");
        var endTask2 = _sessionService.EndTask(sessionId, "task-2");

        // Assert
        Assert.True(endTask1.Success);
        Assert.True(endTask2.Success);

        // Task 1 ran for ~100ms (50 + 50), Task 2 ran for ~50ms
        Assert.True(endTask1.Task!.DurationMs > endTask2.Task!.DurationMs,
            $"Task 1 ({endTask1.Task.DurationMs}ms) should have longer duration than Task 2 ({endTask2.Task.DurationMs}ms)");
    }

    [Fact]
    public void ParallelTasks_DontInterfereWithEachOther()
    {
        // Arrange
        var sessionId = CreateTestSession();

        // Act - Start multiple tasks simultaneously
        _sessionService.StartTask(sessionId, "task-1");
        _sessionService.StartTask(sessionId, "task-2");
        _sessionService.StartTask(sessionId, "task-3");

        Thread.Sleep(30);

        // End in different order
        var end2 = _sessionService.EndTask(sessionId, "task-2");
        Thread.Sleep(20);
        var end1 = _sessionService.EndTask(sessionId, "task-1");
        Thread.Sleep(20);
        var end3 = _sessionService.EndTask(sessionId, "task-3");

        // Assert - Each task should have its own duration
        Assert.True(end2.Success);
        Assert.True(end1.Success);
        Assert.True(end3.Success);

        // Task 3 should have longest duration, task 2 shortest
        Assert.True(end3.Task!.DurationMs > end1.Task!.DurationMs);
        Assert.True(end1.Task.DurationMs > end2.Task!.DurationMs);
    }

    #endregion

    #region M3-033: Duration formatting is human-readable

    [Fact]
    public void DurationFormatter_FormatsDurationsCorrectly()
    {
        // Test various durations
        Assert.Equal("less than 1 second", DurationFormatter.Format(500));
        Assert.Equal("1 second", DurationFormatter.Format(1000));
        Assert.Equal("5 seconds", DurationFormatter.Format(5000));
        Assert.Equal("1 minute", DurationFormatter.Format(60000));
        Assert.Equal("1 minute 30 seconds", DurationFormatter.Format(90000));
        Assert.Equal("2 minutes 34 seconds", DurationFormatter.Format(154000));
        Assert.Equal("1 hour", DurationFormatter.Format(3600000));
        Assert.Equal("1 hour 30 minutes", DurationFormatter.Format(5400000));
        Assert.Equal("2 hours 15 minutes 30 seconds", DurationFormatter.Format(8130000));
    }

    [Fact]
    public void DurationFormatter_HandlesDays()
    {
        // Test day formatting
        var oneDayMs = 24 * 60 * 60 * 1000L;
        Assert.Contains("1 day", DurationFormatter.Format(oneDayMs));
        Assert.Contains("2 days", DurationFormatter.Format(2 * oneDayMs));
    }

    [Fact]
    public void DurationFormatter_HandlesZero()
    {
        Assert.Equal("less than 1 second", DurationFormatter.Format(0));
    }

    [Fact]
    public void DurationFormatter_CompactFormat()
    {
        Assert.Equal("<1s", DurationFormatter.FormatCompact(500));
        Assert.Equal("1m 30s", DurationFormatter.FormatCompact(90000));
        Assert.Equal("1h 30m", DurationFormatter.FormatCompact(5400000));
    }

    #endregion

    #region M3-034: Summary counts are accurate

    [Fact]
    public void SummaryCounts_AreAccurate()
    {
        // Arrange
        var sessionId = CreateTestSession(new List<string> { "task-1", "task-2", "task-3", "task-4", "task-5" });

        // Complete task-1
        _sessionService.StartTask(sessionId, "task-1");
        _sessionService.EndTask(sessionId, "task-1", status: "completed");

        // Skip task-2
        _sessionService.StartTask(sessionId, "task-2");
        _sessionService.EndTask(sessionId, "task-2", status: "skipped");

        // Start task-3 but don't end (in progress)
        _sessionService.StartTask(sessionId, "task-3");

        // Leave task-4 and task-5 not started

        // Act
        var summary = _sessionService.GetSessionSummary(sessionId);

        // Assert
        var completed = summary.Session!.Tasks.Count(t => t.Status == TaskStatus.Completed);
        var skipped = summary.Session.Tasks.Count(t => t.Status == TaskStatus.Skipped);
        var inProgress = summary.Session.Tasks.Count(t => t.Status == TaskStatus.InProgress);
        var notStarted = summary.Session.Tasks.Count(t => t.Status == TaskStatus.NotStarted);

        Assert.Equal(1, completed);
        Assert.Equal(1, skipped);
        Assert.Equal(1, inProgress);
        Assert.Equal(2, notStarted);
    }

    [Fact]
    public void TimeSessionSummary_HasAccurateCounts()
    {
        // Arrange
        var startResult = TimeTools.time_session_start(
            _sessionService,
            milestone_id: "test",
            task_ids: "task-1,task-2,task-3,task-4");
        var startData = JsonSerializer.Deserialize<JsonElement>(startResult);
        var sessionId = startData.GetProperty("session_id").GetString()!;

        // Complete one, skip one, start one (in progress)
        TimeTools.time_task_start(_sessionService, sessionId, "task-1");
        TimeTools.time_task_end(_sessionService, sessionId, "task-1", status: "completed");

        TimeTools.time_task_start(_sessionService, sessionId, "task-2");
        TimeTools.time_task_end(_sessionService, sessionId, "task-2", status: "skipped");

        TimeTools.time_task_start(_sessionService, sessionId, "task-3");
        // task-3 left in progress, task-4 not started

        // Act
        var summaryJson = TimeTools.time_session_summary(_sessionService, sessionId);
        var summary = JsonSerializer.Deserialize<JsonElement>(summaryJson);

        // Assert
        Assert.Equal(1, summary.GetProperty("tasks_completed").GetInt32());
        Assert.Equal(1, summary.GetProperty("tasks_skipped").GetInt32());
        Assert.Equal(1, summary.GetProperty("tasks_in_progress").GetInt32());
        Assert.Equal(1, summary.GetProperty("tasks_not_started").GetInt32());
        Assert.Equal(2, summary.GetProperty("tasks_remaining").GetInt32()); // in_progress + not_started
    }

    #endregion

    #region M3-035: Duration remains accurate if system clock changes

    [Fact]
    public void Duration_UsesMonotonicTime_NotWallClock()
    {
        // This test verifies that we're using Stopwatch.GetTimestamp() 
        // (monotonic clock) rather than DateTime.Now (wall clock)
        
        // Arrange
        var sessionId = CreateTestSession();
        
        // Act
        var startResult = _sessionService.StartTask(sessionId, "task-1");
        
        // The task uses StartTicks from Stopwatch.GetTimestamp()
        Assert.True(startResult.Task!.StartTicks > 0);
        
        Thread.Sleep(50);
        
        var endResult = _sessionService.EndTask(sessionId, "task-1");
        
        // Assert
        // Duration is calculated from monotonic ticks, not wall clock
        Assert.NotNull(endResult.Task!.EndTicks);
        Assert.True(endResult.Task.EndTicks > startResult.Task.StartTicks);
        
        // Duration should be computed from ticks difference
        var expectedDurationMs = (endResult.Task.EndTicks!.Value - startResult.Task.StartTicks) 
            * 1000 / System.Diagnostics.Stopwatch.Frequency;
        Assert.Equal(expectedDurationMs, endResult.Task.DurationMs);
    }

    [Fact]
    public void Session_UsesMonotonicTime_ForElapsed()
    {
        // Arrange
        var sessionId = CreateTestSession();
        var session = _sessionService.GetSession(sessionId);
        
        // Act
        Thread.Sleep(50);
        var elapsed1 = session!.GetElapsedMs();
        Thread.Sleep(50);
        var elapsed2 = session.GetElapsedMs();

        // Assert
        Assert.True(elapsed1 >= 40);
        Assert.True(elapsed2 > elapsed1);
        Assert.True(elapsed2 - elapsed1 >= 40);
    }

    [Fact]
    public void TaskDuration_IsConsistentWithMonotonicCalculation()
    {
        // Arrange
        var sessionId = CreateTestSession();

        // Act
        _sessionService.StartTask(sessionId, "task-1");
        Thread.Sleep(100);
        var endResult = _sessionService.EndTask(sessionId, "task-1");

        // Assert - Duration should be at least 90ms (allowing for timing variance)
        Assert.NotNull(endResult.Task!.DurationMs);
        Assert.True(endResult.Task.DurationMs >= 90, 
            $"Duration was {endResult.Task.DurationMs}ms, expected >= 90ms");

        // Verify the calculation method
        var task = endResult.Task;
        var calculatedDuration = (task.EndTicks!.Value - task.StartTicks) * 1000 / System.Diagnostics.Stopwatch.Frequency;
        Assert.Equal(calculatedDuration, task.DurationMs);
    }

    #endregion

    #region Additional Edge Cases

    [Fact]
    public void TaskEnd_AlreadyCompleted_ReturnsError()
    {
        // Arrange
        var sessionId = CreateTestSession();
        _sessionService.StartTask(sessionId, "task-1");
        _sessionService.EndTask(sessionId, "task-1");

        // Act - Try to end again
        var result = _sessionService.EndTask(sessionId, "task-1");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("TASK_NOT_STARTED", result.ErrorCode);
    }

    [Fact]
    public void TaskStart_SessionNotFound_ReturnsError()
    {
        // Act
        var result = _sessionService.StartTask("invalid-session", "task-1");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("SESSION_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public void TaskEnd_SessionNotFound_ReturnsError()
    {
        // Act
        var result = _sessionService.EndTask("invalid-session", "task-1");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("SESSION_NOT_FOUND", result.ErrorCode);
    }

    #endregion
}
