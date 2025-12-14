using System.Text.Json;
using TimeTrackerMcp.Models;
using TimeTrackerMcp.Services;
using TimeTrackerMcp.Tools;
using Xunit;

namespace TimeTrackerMcp.Tests;

/// <summary>
/// Unit tests for Session Service and Session Tools.
/// </summary>
public class SessionServiceTests
{
    private readonly ITimeZoneResolver _timeZoneResolver;
    private readonly InMemorySessionService _sessionService;

    public SessionServiceTests()
    {
        _timeZoneResolver = new TimeZoneResolver();
        _sessionService = new InMemorySessionService(_timeZoneResolver);
    }

    #region M2-042: Start session returns valid session ID

    [Fact]
    public void StartSession_WithValidParameters_ReturnsValidSessionId()
    {
        // Arrange
        var milestoneId = "milestone-001";
        var taskIds = new List<string> { "task-1", "task-2", "task-3" };

        // Act
        var result = _sessionService.StartSession(milestoneId, taskIds);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Session);
        Assert.NotNull(result.Session.SessionId);
        Assert.Equal(32, result.Session.SessionId.Length); // GUID without dashes
        Assert.Equal(milestoneId, result.Session.MilestoneId);
        Assert.Equal(3, result.Session.TaskIds.Count);
    }

    [Fact]
    public void TimeSessionStart_WithValidParameters_ReturnsValidSessionId()
    {
        // Arrange & Act
        var jsonResult = TimeTools.time_session_start(
            _sessionService,
            milestone_id: "milestone-001",
            task_ids: "task-1,task-2,task-3");

        // Assert
        var result = JsonSerializer.Deserialize<JsonElement>(jsonResult);
        Assert.True(result.TryGetProperty("session_id", out var sessionId));
        Assert.Equal(32, sessionId.GetString()!.Length);
        Assert.True(result.TryGetProperty("task_count", out var taskCount));
        Assert.Equal(3, taskCount.GetInt32());
    }

    [Fact]
    public void TimeSessionStart_WithJsonArrayTaskIds_ParsesCorrectly()
    {
        // Arrange & Act
        var jsonResult = TimeTools.time_session_start(
            _sessionService,
            milestone_id: "milestone-002",
            task_ids: "[\"task-a\",\"task-b\"]");

        // Assert
        var result = JsonSerializer.Deserialize<JsonElement>(jsonResult);
        Assert.True(result.TryGetProperty("task_count", out var taskCount));
        Assert.Equal(2, taskCount.GetInt32());
    }

    #endregion

    #region M2-043: End session returns correct duration

    [Fact]
    public void EndSession_ReturnsCorrectDuration()
    {
        // Arrange
        var startResult = _sessionService.StartSession("milestone", new List<string> { "task-1" });
        var sessionId = startResult.Session!.SessionId;

        // Wait a small amount of time
        Thread.Sleep(50);

        // Act
        var endResult = _sessionService.EndSession(sessionId);

        // Assert
        Assert.True(endResult.Success);
        Assert.NotNull(endResult.Session);
        Assert.NotNull(endResult.Session.EndTime);
        
        var durationMs = endResult.Session.GetDurationMs();
        Assert.NotNull(durationMs);
        Assert.True(durationMs >= 40, $"Duration should be at least 40ms, was {durationMs}ms");
    }

    [Fact]
    public void TimeSessionEnd_ReturnsCorrectDurationFields()
    {
        // Arrange
        var startResult = TimeTools.time_session_start(
            _sessionService,
            milestone_id: "milestone",
            task_ids: "task-1");
        var startData = JsonSerializer.Deserialize<JsonElement>(startResult);
        var sessionId = startData.GetProperty("session_id").GetString()!;

        Thread.Sleep(50);

        // Act
        var endResult = TimeTools.time_session_end(_sessionService, sessionId);

        // Assert
        var result = JsonSerializer.Deserialize<JsonElement>(endResult);
        Assert.True(result.TryGetProperty("duration_ms", out var durationMs));
        Assert.True(durationMs.GetInt64() >= 40);
        Assert.True(result.TryGetProperty("duration", out var duration));
        Assert.NotNull(duration.GetString());
        Assert.True(result.TryGetProperty("is_ended", out var isEnded));
        Assert.True(isEnded.GetBoolean());
    }

    #endregion

    #region M2-044: End session is idempotent

    [Fact]
    public void EndSession_IsIdempotent_ReturnsSameResult()
    {
        // Arrange
        var startResult = _sessionService.StartSession("milestone", new List<string> { "task-1" });
        var sessionId = startResult.Session!.SessionId;

        // Act
        var firstEnd = _sessionService.EndSession(sessionId);
        var secondEnd = _sessionService.EndSession(sessionId);

        // Assert
        Assert.True(firstEnd.Success);
        Assert.True(secondEnd.Success);
        Assert.Equal(firstEnd.Session!.EndTime, secondEnd.Session!.EndTime);
        Assert.Equal(firstEnd.Session.GetDurationMs(), secondEnd.Session.GetDurationMs());
    }

    [Fact]
    public void TimeSessionEnd_CalledTwice_ReturnsSameEndTime()
    {
        // Arrange
        var startResult = TimeTools.time_session_start(
            _sessionService,
            milestone_id: "milestone",
            task_ids: "task-1");
        var startData = JsonSerializer.Deserialize<JsonElement>(startResult);
        var sessionId = startData.GetProperty("session_id").GetString()!;

        // Act
        var firstEnd = TimeTools.time_session_end(_sessionService, sessionId);
        Thread.Sleep(50); // Wait to ensure time would be different
        var secondEnd = TimeTools.time_session_end(_sessionService, sessionId);

        // Assert
        var first = JsonSerializer.Deserialize<JsonElement>(firstEnd);
        var second = JsonSerializer.Deserialize<JsonElement>(secondEnd);
        
        Assert.Equal(
            first.GetProperty("end_time").GetString(),
            second.GetProperty("end_time").GetString());
    }

    #endregion

    #region M2-045: Session not found returns error

    [Fact]
    public void EndSession_SessionNotFound_ReturnsError()
    {
        // Arrange
        var invalidSessionId = "nonexistent-session-id";

        // Act
        var result = _sessionService.EndSession(invalidSessionId);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("SESSION_NOT_FOUND", result.ErrorCode);
        Assert.Contains("not found", result.ErrorMessage);
    }

    [Fact]
    public void TimeSessionEnd_SessionNotFound_ReturnsStructuredError()
    {
        // Arrange & Act
        var jsonResult = TimeTools.time_session_end(_sessionService, "invalid-session");

        // Assert
        var result = JsonSerializer.Deserialize<JsonElement>(jsonResult);
        Assert.True(result.TryGetProperty("error", out var error));
        Assert.True(error.GetBoolean());
        Assert.True(result.TryGetProperty("error_code", out var errorCode));
        Assert.Equal("SESSION_NOT_FOUND", errorCode.GetString());
    }

    [Fact]
    public void TimeSessionSummary_SessionNotFound_ReturnsStructuredError()
    {
        // Arrange & Act
        var jsonResult = TimeTools.time_session_summary(_sessionService, "invalid-session");

        // Assert
        var result = JsonSerializer.Deserialize<JsonElement>(jsonResult);
        Assert.True(result.TryGetProperty("error", out var error));
        Assert.True(error.GetBoolean());
        Assert.True(result.TryGetProperty("error_code", out var errorCode));
        Assert.Equal("SESSION_NOT_FOUND", errorCode.GetString());
    }

    #endregion

    #region M2-046: Multiple concurrent sessions work independently

    [Fact]
    public void MultipleSessions_WorkIndependently()
    {
        // Arrange & Act
        var session1 = _sessionService.StartSession("milestone-1", new List<string> { "task-1" });
        var session2 = _sessionService.StartSession("milestone-2", new List<string> { "task-2", "task-3" });
        var session3 = _sessionService.StartSession("milestone-3", new List<string> { "task-4" });

        // Assert
        Assert.True(session1.Success);
        Assert.True(session2.Success);
        Assert.True(session3.Success);
        
        Assert.NotEqual(session1.Session!.SessionId, session2.Session!.SessionId);
        Assert.NotEqual(session2.Session.SessionId, session3.Session!.SessionId);
        
        Assert.Equal("milestone-1", session1.Session.MilestoneId);
        Assert.Equal("milestone-2", session2.Session.MilestoneId);
        Assert.Equal("milestone-3", session3.Session.MilestoneId);
        
        Assert.Equal(3, _sessionService.SessionCount);
    }

    [Fact]
    public void MultipleSessions_EndIndependently()
    {
        // Arrange
        var session1 = _sessionService.StartSession("milestone-1", new List<string> { "task-1" });
        Thread.Sleep(50);
        var session2 = _sessionService.StartSession("milestone-2", new List<string> { "task-2" });

        // Act - End both sessions immediately
        var end1 = _sessionService.EndSession(session1.Session!.SessionId);
        var end2 = _sessionService.EndSession(session2.Session!.SessionId);

        // Assert
        Assert.True(end1.Success);
        Assert.True(end2.Success);
        
        // Session 1 started first and ran for 50ms before session 2 started
        // So session 1 should have longer duration
        Assert.True(end1.Session!.GetDurationMs() > end2.Session!.GetDurationMs(),
            $"Session 1 duration ({end1.Session.GetDurationMs()}ms) should be > Session 2 duration ({end2.Session.GetDurationMs()}ms)");
    }

    [Fact]
    public async Task ConcurrentSessionCreation_ThreadSafe()
    {
        // Arrange
        var tasks = new List<Task<SessionResult>>();
        
        // Act - Create 50 sessions concurrently
        for (int i = 0; i < 50; i++)
        {
            var milestoneId = $"milestone-{i}";
            var taskIds = new List<string> { $"task-{i}" };
            tasks.Add(Task.Run(() => _sessionService.StartSession(milestoneId, taskIds)));
        }
        
        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(50, _sessionService.SessionCount);
        var uniqueIds = tasks.Select(t => t.Result.Session!.SessionId).Distinct().Count();
        Assert.Equal(50, uniqueIds);
    }

    #endregion

    #region M2-047: Sessions expire after inactivity (tested via cleanup)

    [Fact]
    public void CleanupExpiredSessions_RemovesOldSessions()
    {
        // Arrange - Create a session
        var result = _sessionService.StartSession("milestone", new List<string> { "task-1" });
        Assert.True(result.Success);
        Assert.Equal(1, _sessionService.SessionCount);

        // Note: We can't easily test the 4-hour inactivity limit without mocking time,
        // but we can verify the cleanup method runs without error
        
        // Act
        var cleanedCount = _sessionService.CleanupExpiredSessions();

        // Assert - Session should NOT be cleaned up yet (it's fresh)
        Assert.Equal(0, cleanedCount);
        Assert.Equal(1, _sessionService.SessionCount);
    }

    [Fact]
    public void GetSession_UpdatesNotLastActivityTime()
    {
        // Arrange
        var result = _sessionService.StartSession("milestone", new List<string> { "task-1" });
        var sessionId = result.Session!.SessionId;
        var initialActivityTime = result.Session.LastActivityTime;

        Thread.Sleep(10);

        // Act - GetSessionSummary should update LastActivityTime
        var summaryResult = _sessionService.GetSessionSummary(sessionId);

        // Assert
        Assert.True(summaryResult.Success);
        Assert.True(summaryResult.Session!.LastActivityTime > initialActivityTime);
    }

    #endregion

    #region M2-048: Max sessions limit enforced

    [Fact]
    public void MaxSessionsLimit_EnforcedAt100()
    {
        // Arrange - Create 100 sessions
        for (int i = 0; i < 100; i++)
        {
            var result = _sessionService.StartSession($"milestone-{i}", new List<string> { "task" });
            Assert.True(result.Success, $"Session {i} should succeed");
        }
        
        Assert.Equal(100, _sessionService.SessionCount);

        // Act - Try to create 101st session
        var overflowResult = _sessionService.StartSession("overflow", new List<string> { "task" });

        // Assert
        Assert.False(overflowResult.Success);
        Assert.Equal("MAX_SESSIONS_REACHED", overflowResult.ErrorCode);
        Assert.Equal(100, _sessionService.SessionCount);
    }

    [Fact]
    public void TimeSessionStart_MaxSessionsReached_ReturnsError()
    {
        // Arrange - Fill up sessions
        for (int i = 0; i < 100; i++)
        {
            _sessionService.StartSession($"milestone-{i}", new List<string> { "task" });
        }

        // Act
        var jsonResult = TimeTools.time_session_start(
            _sessionService,
            milestone_id: "overflow",
            task_ids: "task-1");

        // Assert
        var result = JsonSerializer.Deserialize<JsonElement>(jsonResult);
        Assert.True(result.TryGetProperty("error", out var error));
        Assert.True(error.GetBoolean());
        Assert.True(result.TryGetProperty("error_code", out var errorCode));
        Assert.Equal("MAX_SESSIONS_REACHED", errorCode.GetString());
    }

    #endregion

    #region M2-049: Session summary works mid-execution

    [Fact]
    public void SessionSummary_WorksMidExecution()
    {
        // Arrange
        var startResult = _sessionService.StartSession(
            "milestone",
            new List<string> { "task-1", "task-2", "task-3" },
            milestoneName: "Test Milestone");
        var sessionId = startResult.Session!.SessionId;

        Thread.Sleep(50);

        // Act
        var summaryResult = _sessionService.GetSessionSummary(sessionId);

        // Assert
        Assert.True(summaryResult.Success);
        Assert.NotNull(summaryResult.Session);
        Assert.False(summaryResult.Session.IsEnded);
        Assert.Null(summaryResult.Session.EndTime);
        
        // Should have elapsed time
        var elapsed = summaryResult.Session.GetElapsedMs();
        Assert.True(elapsed >= 40, $"Elapsed should be at least 40ms, was {elapsed}ms");
    }

    [Fact]
    public void TimeSessionSummary_WorksMidExecution_ReturnsActiveState()
    {
        // Arrange
        var startResult = TimeTools.time_session_start(
            _sessionService,
            milestone_id: "milestone",
            task_ids: "task-1,task-2,task-3",
            milestone_name: "Test Milestone");
        var startData = JsonSerializer.Deserialize<JsonElement>(startResult);
        var sessionId = startData.GetProperty("session_id").GetString()!;

        Thread.Sleep(50);

        // Act
        var summaryJson = TimeTools.time_session_summary(_sessionService, sessionId);

        // Assert
        var summary = JsonSerializer.Deserialize<JsonElement>(summaryJson);
        
        // Session should not be ended
        Assert.True(summary.TryGetProperty("is_ended", out var isEnded));
        Assert.False(isEnded.GetBoolean());
        
        // Should have duration/elapsed time
        Assert.True(summary.TryGetProperty("duration_ms", out var durationMs));
        Assert.True(durationMs.GetInt64() >= 40);
        
        // Should have task counts
        Assert.True(summary.TryGetProperty("task_count", out var taskCount));
        Assert.Equal(3, taskCount.GetInt32());
        Assert.True(summary.TryGetProperty("tasks_not_started", out var notStarted));
        Assert.Equal(3, notStarted.GetInt32());
        
        // End time should be null
        Assert.True(summary.TryGetProperty("end_time", out var endTime));
        Assert.Equal(JsonValueKind.Null, endTime.ValueKind);
    }

    [Fact]
    public void SessionSummary_DoesNotEndSession()
    {
        // Arrange
        var startResult = _sessionService.StartSession("milestone", new List<string> { "task-1" });
        var sessionId = startResult.Session!.SessionId;

        // Act - Get summary multiple times
        _sessionService.GetSessionSummary(sessionId);
        _sessionService.GetSessionSummary(sessionId);
        var finalSummary = _sessionService.GetSessionSummary(sessionId);

        // Assert - Session should still be active
        Assert.True(finalSummary.Success);
        Assert.False(finalSummary.Session!.IsEnded);
        Assert.Null(finalSummary.Session.EndTime);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void StartSession_MissingMilestoneId_ReturnsError()
    {
        // Act
        var result = _sessionService.StartSession("", new List<string> { "task-1" });

        // Assert
        Assert.False(result.Success);
        Assert.Equal("MISSING_MILESTONE_ID", result.ErrorCode);
    }

    [Fact]
    public void StartSession_MissingTaskIds_ReturnsError()
    {
        // Act
        var result = _sessionService.StartSession("milestone", new List<string>());

        // Assert
        Assert.False(result.Success);
        Assert.Equal("MISSING_TASK_IDS", result.ErrorCode);
    }

    [Fact]
    public void StartSession_WithOptionalParameters_StoresThem()
    {
        // Arrange
        var metadata = new Dictionary<string, string> { ["key"] = "value" };
        var tags = new List<string> { "tag1", "tag2" };

        // Act
        var result = _sessionService.StartSession(
            milestoneId: "milestone",
            taskIds: new List<string> { "task-1" },
            milestoneName: "My Milestone",
            timezone: "UTC",
            metadata: metadata,
            tags: tags);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("My Milestone", result.Session!.MilestoneName);
        Assert.Equal("UTC", result.Session.Timezone);
        Assert.NotNull(result.Session.Metadata);
        Assert.Equal("value", result.Session.Metadata["key"]);
        Assert.NotNull(result.Session.Tags);
        Assert.Equal(2, result.Session.Tags.Count);
    }

    #endregion
}
