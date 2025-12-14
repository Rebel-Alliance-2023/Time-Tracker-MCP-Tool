# Time Tracker MCP Tool Specification

## Overview

This document specifies a **Time Tracker MCP (Model Context Protocol) Tool** designed to provide AI coding assistants (such as GitHub Copilot) with the ability to query the current system time and track execution time for Milestone/Task workflows. This enables accurate execution reports with real wall-clock timestamps and durations.

---

## Problem Statement

AI coding assistants currently lack the ability to:

1. **Query the current system time** - Cannot determine when tasks start or end
2. **Track elapsed time** - Cannot measure how long operations take
3. **Generate accurate execution reports** - Must rely on user-provided timestamps or generate simulated/estimated durations

This limitation affects:
- Milestone execution reports
- Task tracking accuracy
- Developer productivity metrics
- Audit trails for automated code generation sessions

---

## Goals

1. Provide AI assistants with access to current system time
2. Enable Milestone/Task execution tracking with accurate timestamps
3. Support session-based time tracking with named markers
4. Maintain simplicity - minimal API surface for core use cases
5. Produce audit-grade execution reports with trusted timestamps

---

## Phased Approach

| Version | Scope | Tools | State |
|---------|-------|-------|-------|
| **V1 (Basic)** | Just get the current time | `time_get_current` | Stateless |
| **V2 (Session)** | Milestone/Task tracking | `time_session_start`, `time_task_start`, `time_task_end`, `time_session_end`, `time_session_summary` | Stateful (in-memory) |

---

## Architectural Decision: Standalone Project

### Why Standalone?

The Time Tracker MCP Tool **must be implemented as a separate, standalone project** rather than integrated into an existing codebase. This is due to a fundamental chicken-and-egg problem:

| Approach | Problem |
|----------|---------|
| Integrate into existing project (e.g., `Cookbook.Platform.Mcp`) | Cannot use the tool to track time while building the project that contains it. Copilot cannot call tools in code that doesn't exist yet. |
| **Standalone project** | ? Can be built, deployed, and registered *before* using Copilot on other projects. |

### Requirements for Copilot Integration

For the Time Tracker MCP Tool to be usable by Copilot during task execution:

1. **Must already exist and be running** before Copilot starts executing tasks
2. **Must be registered with Copilot/VS** as an available MCP server
3. **Must be independent** of any codebase being actively modified by Copilot

### Recommended Project Structure

```
time-tracker-mcp/
??? src/
?   ??? TimeTrackerMcp/
?       ??? TimeTrackerMcp.csproj
?       ??? Program.cs
?       ??? Tools/
?       ?   ??? TimeTools.cs
?       ??? Services/
?       ?   ??? ISessionService.cs
?       ?   ??? InMemorySessionService.cs
?       ?   ??? ITimeProvider.cs
?       ??? Models/
?           ??? Session.cs
?           ??? TaskRecord.cs
?           ??? TimeResult.cs
??? tests/
?   ??? TimeTrackerMcp.Tests/
?       ??? TimeTrackerMcp.Tests.csproj
?       ??? TimeToolsTests.cs
??? .vscode/
?   ??? mcp.json                    ? VS Code MCP configuration example
??? README.md
??? LICENSE
??? .github/
    ??? workflows/
        ??? build.yml
```

### Technology Stack

- **.NET 10** - Consistent with modern .NET projects
- **ModelContextProtocol.Server** - Official C# MCP SDK from Microsoft ([GitHub](https://github.com/modelcontextprotocol/csharp-sdk))
- **In-memory storage** - ConcurrentDictionary for session state
- **HTTP Transport** - Standard MCP server hosting (aligned with MCP specification)

### Registration and Discovery

#### Visual Studio 2026 Configuration (Primary)

For Visual Studio 2026 with GitHub Copilot, MCP servers can be registered via:

- **Visual Studio Options** ? GitHub Copilot ? MCP Servers
- **`mcp.json`** in solution directory
- **Extension-based registration**

> **Note:** Visual Studio 2026 (GA) includes enhanced MCP server support. Consult the Visual Studio 2026 and GitHub Copilot documentation for the latest registration methods.

#### VS Code Configuration (Secondary)

For VS Code/Copilot Chat integration, provide an `mcp.json` configuration example:

```json
{
  "servers": {
    "time-tracker": {
      "command": "dotnet",
      "args": ["run", "--project", "path/to/TimeTrackerMcp.csproj"],
      "env": {}
    }
  }
}
```

This can be placed in:
- `.vscode/mcp.json` (workspace-level)
- User profile configuration (global)

> **Note:** Do not hardcode secrets in configuration files. Use environment variables for any sensitive values.

Reference: [VS Code MCP Servers Documentation](https://code.visualstudio.com/docs/copilot/customization/mcp-servers)

---

## V1: Basic Mode (Single Tool)

### Tool: `time_get_current`

Returns the current system date and time. **Stateless, no dependencies.**

#### Input Schema

```json
{
  "type": "object",
  "properties": {
    "format": {
      "type": "string",
      "description": "Output format. Options: 'iso8601' (default), 'unix', 'unix_ms', 'friendly'",
      "default": "iso8601"
    },
    "timezone": {
      "type": "string",
      "description": "IANA timezone name (e.g., 'America/New_York', 'UTC'). Defaults to system local timezone.",
      "default": "local"
    }
  },
  "required": []
}
```

#### Output Schema

```json
{
  "type": "object",
  "properties": {
    "timestamp": {
      "type": "string",
      "description": "The current time in the requested format"
    },
    "timezone": {
      "type": "string",
      "description": "The timezone used for the response"
    },
    "utc_offset": {
      "type": "string",
      "description": "UTC offset in ±HH:MM format"
    }
  }
}
```

#### Format Options

| Format | Example Output |
|--------|----------------|
| `iso8601` | `2025-12-14T09:45:32.123-05:00` |
| `unix` | `1734185132` |
| `unix_ms` | `1734185132123` |
| `friendly` | `December 14, 2025 9:45:32 AM` |

---

## V2: Session Mode (Milestone/Task Tracking)

### Overview

V2 provides structured tracking for Milestone/Task execution workflows. The user provides context when starting a session, and the AI uses task-specific tools to track individual task durations.

### MCP Session Alignment

The Time Tracker's `sessionId` is a **logical run identifier** (representing a milestone execution). It should be **bound to the MCP protocol session** (per connected client via `Mcp-Session-Id`) to avoid cross-talk when multiple agents/clients run concurrently.

Reference: [MCP Transports Specification](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports)

### Session Data Model

```csharp
public record Session
{
    public string SessionId { get; init; }
    public string? McpSessionId { get; init; }  // Bound to MCP protocol session
    public string MilestoneId { get; init; }
    public string? MilestoneName { get; init; }
    public List<string> TaskIds { get; init; }
    public DateTimeOffset StartTime { get; init; }      // Wall-clock (audit)
    public long StartTicks { get; init; }               // Monotonic (duration calc)
    public DateTimeOffset? EndTime { get; set; }
    public long? EndTicks { get; set; }
    public string Timezone { get; init; }
    public Dictionary<string, string> Metadata { get; init; }
    public List<TaskRecord> Tasks { get; init; } = new();
    public DateTimeOffset LastActivityTime { get; set; } // For expiration
}

public record TaskRecord
{
    public string TaskId { get; init; }
    public string? TaskName { get; init; }
    public string? ExternalTaskId { get; init; }        // Maps to platform TaskId
    public string? WorkItemId { get; init; }            // Jira/GitHub Issues
    public DateTimeOffset StartTime { get; init; }      // Wall-clock (audit)
    public long StartTicks { get; init; }               // Monotonic (duration calc)
    public DateTimeOffset? EndTime { get; set; }
    public long? EndTicks { get; set; }
    public long? DurationMs { get; set; }
    public string Status { get; set; } // "in_progress", "completed", "skipped"
    public Dictionary<string, string> Metadata { get; init; }
}
```

### Time Measurement Strategy

> **Critical:** Use monotonic time for durations, wall-clock for timestamps.

| Purpose | Source | Rationale |
|---------|--------|-----------|
| **Audit timestamps** | `DateTimeOffset.UtcNow` | Human-readable, timezone-aware |
| **Duration calculation** | `Stopwatch.GetTimestamp()` | Monotonic, unaffected by clock changes |

```csharp
// Correct duration calculation
var durationMs = (endTicks - startTicks) * 1000 / Stopwatch.Frequency;
```

This avoids issues if the system clock changes mid-session (e.g., NTP sync, DST).

### Session Storage

- **In-memory**: `ConcurrentDictionary<string, Session>`
- **Session ID**: Auto-generated GUID
- **MCP Session Binding**: Sessions are associated with MCP protocol session ID
- **Expiration**: Sessions expire after 24 hours of inactivity (configurable)
- **Cleanup**: Background task removes expired sessions

#### Retention Limits

| Limit | Default | Purpose |
|-------|---------|---------|
| Max sessions | 100 | Prevent memory exhaustion |
| Max tasks per session | 500 | Prevent runaway sessions |
| Max session age | 24 hours | Cleanup stale sessions |
| Max inactivity | 4 hours | Cleanup abandoned sessions |

---

### Tool 1: `time_session_start`

Initializes a new tracking session for a Milestone execution.

#### Input Schema

```json
{
  "type": "object",
  "properties": {
    "milestone_id": {
      "type": "string",
      "description": "Unique identifier for the milestone (e.g., 'M2')"
    },
    "milestone_name": {
      "type": "string",
      "description": "Human-readable name (e.g., 'Commit + Lifecycle')"
    },
    "task_ids": {
      "type": "array",
      "items": { "type": "string" },
      "description": "List of task IDs to be executed (e.g., ['M2-001', 'M2-002', ...])"
    },
    "timezone": {
      "type": "string",
      "description": "IANA timezone for all timestamps in this session",
      "default": "local"
    },
    "metadata": {
      "type": "object",
      "description": "Optional key-value metadata (e.g., branch, author)",
      "additionalProperties": { "type": "string" }
    },
    "tags": {
      "type": "array",
      "items": { "type": "string" },
      "description": "Optional tags for categorization (e.g., ['milestone:2', 'area:gateway'])"
    }
  },
  "required": ["milestone_id", "task_ids"]
}
```

#### Output Schema

```json
{
  "type": "object",
  "properties": {
    "session_id": {
      "type": "string",
      "description": "Unique session identifier for subsequent calls"
    },
    "milestone_id": {
      "type": "string"
    },
    "start_time": {
      "type": "string",
      "description": "ISO 8601 timestamp when session started"
    },
    "start_time_friendly": {
      "type": "string",
      "description": "Human-readable start time"
    },
    "task_count": {
      "type": "integer",
      "description": "Number of tasks registered for this session"
    },
    "timezone": {
      "type": "string"
    }
  }
}
```

#### Example

**Request:**
```json
{
  "milestone_id": "M2",
  "milestone_name": "Commit + Lifecycle",
  "task_ids": ["M2-001", "M2-002", "M2-003", "M2-004", "M2-005"],
  "timezone": "America/New_York",
  "metadata": {
    "branch": "Recipe-Ingest-Agent",
    "execution_date": "2025-12-14"
  },
  "tags": ["milestone:2", "area:gateway", "area:orchestrator"]
}
```

**Response:**
```json
{
  "session_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "milestone_id": "M2",
  "start_time": "2025-12-14T09:45:32.123-05:00",
  "start_time_friendly": "December 14, 2025 9:45:32 AM EST",
  "task_count": 5,
  "timezone": "America/New_York"
}
```

---

### Tool 2: `time_task_start`

Marks the start of a specific task within the session.

#### Idempotency Behavior

- If the same `(sessionId, taskId)` is called twice while task is already running:
  - Returns existing start time
  - Includes `"already_running": true` in response
  - Does NOT reset the start time

#### Input Schema

```json
{
  "type": "object",
  "properties": {
    "session_id": {
      "type": "string",
      "description": "Session ID from time_session_start"
    },
    "task_id": {
      "type": "string",
      "description": "Task ID being started (must be in session's task_ids list)"
    },
    "task_name": {
      "type": "string",
      "description": "Optional human-readable task name"
    },
    "external_task_id": {
      "type": "string",
      "description": "Optional mapping to platform TaskId (e.g., AgentTask.Id)"
    },
    "work_item_id": {
      "type": "string",
      "description": "Optional mapping to Jira/GitHub issue ID"
    },
    "metadata": {
      "type": "object",
      "description": "Optional task-specific metadata",
      "additionalProperties": { "type": "string" }
    }
  },
  "required": ["session_id", "task_id"]
}
```

#### Output Schema

```json
{
  "type": "object",
  "properties": {
    "task_id": {
      "type": "string"
    },
    "start_time": {
      "type": "string",
      "description": "ISO 8601 timestamp"
    },
    "start_time_friendly": {
      "type": "string"
    },
    "session_elapsed": {
      "type": "string",
      "description": "Time elapsed since session start (e.g., '5 minutes 23 seconds')"
    },
    "tasks_completed": {
      "type": "integer",
      "description": "Number of tasks already completed in this session"
    },
    "tasks_remaining": {
      "type": "integer",
      "description": "Number of tasks not yet started (excluding current)"
    },
    "already_running": {
      "type": "boolean",
      "description": "True if task was already in progress (idempotent call)"
    }
  }
}
```

#### Example

**Request:**
```json
{
  "session_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "task_id": "M2-003",
  "task_name": "Implement POST /api/recipes/import skeleton",
  "external_task_id": "task-guid-12345"
}
```

**Response:**
```json
{
  "task_id": "M2-003",
  "start_time": "2025-12-14T09:47:15.456-05:00",
  "start_time_friendly": "9:47:15 AM",
  "session_elapsed": "1 minute 43 seconds",
  "tasks_completed": 2,
  "tasks_remaining": 2,
  "already_running": false
}
```

---

### Tool 3: `time_task_end`

Marks the completion of a specific task within the session.

#### Error Handling

- If `time_task_end` is called for a task that was never started:
  - Returns error code `"TASK_NOT_STARTED"`
  - Includes `"error": true` in response

#### Parallel Task Support

- Tasks CAN overlap in one session (parallel timing)
- Each task's elapsed time is computed independently
- Session summary includes all tasks regardless of overlap

#### Input Schema

```json
{
  "type": "object",
  "properties": {
    "session_id": {
      "type": "string",
      "description": "Session ID from time_session_start"
    },
    "task_id": {
      "type": "string",
      "description": "Task ID being completed"
    },
    "status": {
      "type": "string",
      "description": "Completion status: 'completed' (default), 'skipped'",
      "default": "completed"
    },
    "metadata": {
      "type": "object",
      "description": "Optional completion metadata (e.g., files_created, files_modified)",
      "additionalProperties": { "type": "string" }
    }
  },
  "required": ["session_id", "task_id"]
}
```

#### Output Schema

```json
{
  "type": "object",
  "properties": {
    "task_id": {
      "type": "string"
    },
    "start_time": {
      "type": "string"
    },
    "end_time": {
      "type": "string"
    },
    "duration": {
      "type": "string",
      "description": "Human-readable duration (e.g., '2 minutes 34 seconds')"
    },
    "duration_ms": {
      "type": "integer",
      "description": "Duration in milliseconds (computed from monotonic time)"
    },
    "status": {
      "type": "string"
    },
    "tasks_completed": {
      "type": "integer"
    },
    "tasks_remaining": {
      "type": "integer"
    },
    "error": {
      "type": "boolean",
      "description": "True if an error occurred"
    },
    "error_code": {
      "type": "string",
      "description": "Error code if error=true (e.g., 'TASK_NOT_STARTED')"
    }
  }
}
```

#### Example

**Request:**
```json
{
  "session_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "task_id": "M2-003",
  "status": "completed",
  "metadata": {
    "files_created": "RecipeImportService.cs",
    "files_modified": "RecipeEndpoints.cs, Program.cs"
  }
}
```

**Response:**
```json
{
  "task_id": "M2-003",
  "start_time": "2025-12-14T09:47:15.456-05:00",
  "end_time": "2025-12-14T09:49:49.789-05:00",
  "duration": "2 minutes 34 seconds",
  "duration_ms": 154333,
  "status": "completed",
  "tasks_completed": 3,
  "tasks_remaining": 2,
  "error": false
}
```

---

### Tool 4: `time_session_end`

Ends the session and returns a complete summary.

#### Input Schema

```json
{
  "type": "object",
  "properties": {
    "session_id": {
      "type": "string",
      "description": "Session ID from time_session_start"
    },
    "include_task_details": {
      "type": "boolean",
      "description": "Include per-task timing breakdown",
      "default": true
    }
  },
  "required": ["session_id"]
}
```

#### Output Schema

```json
{
  "type": "object",
  "properties": {
    "session_id": {
      "type": "string"
    },
    "milestone_id": {
      "type": "string"
    },
    "milestone_name": {
      "type": "string"
    },
    "start_time": {
      "type": "string"
    },
    "end_time": {
      "type": "string"
    },
    "total_duration": {
      "type": "string",
      "description": "Human-readable total duration"
    },
    "total_duration_ms": {
      "type": "integer"
    },
    "tasks_completed": {
      "type": "integer"
    },
    "tasks_skipped": {
      "type": "integer"
    },
    "tasks_not_started": {
      "type": "integer"
    },
    "timezone": {
      "type": "string"
    },
    "metadata": {
      "type": "object"
    },
    "tags": {
      "type": "array",
      "items": { "type": "string" }
    },
    "tasks": {
      "type": "array",
      "description": "Per-task timing details (if include_task_details=true)",
      "items": {
        "type": "object",
        "properties": {
          "task_id": { "type": "string" },
          "task_name": { "type": "string" },
          "external_task_id": { "type": "string" },
          "start_time": { "type": "string" },
          "end_time": { "type": "string" },
          "duration": { "type": "string" },
          "duration_ms": { "type": "integer" },
          "status": { "type": "string" }
        }
      }
    }
  }
}
```

#### Example

**Request:**
```json
{
  "session_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "include_task_details": true
}
```

**Response:**
```json
{
  "session_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "milestone_id": "M2",
  "milestone_name": "Commit + Lifecycle",
  "start_time": "2025-12-14T09:45:32.123-05:00",
  "end_time": "2025-12-14T09:57:18.456-05:00",
  "total_duration": "11 minutes 46 seconds",
  "total_duration_ms": 706333,
  "tasks_completed": 5,
  "tasks_skipped": 0,
  "tasks_not_started": 0,
  "timezone": "America/New_York",
  "metadata": {
    "branch": "Recipe-Ingest-Agent",
    "execution_date": "2025-12-14"
  },
  "tags": ["milestone:2", "area:gateway", "area:orchestrator"],
  "tasks": [
    {
      "task_id": "M2-001",
      "task_name": "Create ImportRecipeRequest model",
      "external_task_id": null,
      "start_time": "2025-12-14T09:45:32.123-05:00",
      "end_time": "2025-12-14T09:46:45.789-05:00",
      "duration": "1 minute 13 seconds",
      "duration_ms": 73666,
      "status": "completed"
    },
    {
      "task_id": "M2-002",
      "task_name": "Create ImportRecipeResponse model",
      "external_task_id": null,
      "start_time": "2025-12-14T09:46:45.789-05:00",
      "end_time": "2025-12-14T09:47:15.456-05:00",
      "duration": "29 seconds",
      "duration_ms": 29667,
      "status": "completed"
    }
  ]
}
```

---

### Tool 5: `time_session_summary`

Gets the current status of an active session without ending it. **High-value tool** for generating execution reports cleanly without having to reconstruct state by replaying tool responses.

#### Input Schema

```json
{
  "type": "object",
  "properties": {
    "session_id": {
      "type": "string",
      "description": "Session ID from time_session_start"
    },
    "include_task_details": {
      "type": "boolean",
      "description": "Include per-task timing breakdown",
      "default": true
    }
  },
  "required": ["session_id"]
}
```

#### Output Schema

Same as `time_session_end` but:
- Session remains active
- `end_time` reflects current time (not session end)
- Tasks in progress show current elapsed time

---

## V2 Usage Patterns

### Pattern 1: Full Milestone Execution with Per-Task Tracking

```
User: Execute Milestone 2 tasks M2-001 through M2-005.

AI: [calls time_session_start]
    Request: {
      "milestone_id": "M2",
      "milestone_name": "Commit + Lifecycle",
      "task_ids": ["M2-001", "M2-002", "M2-003", "M2-004", "M2-005"],
      "timezone": "America/New_York",
      "metadata": { "branch": "Recipe-Ingest-Agent" },
      "tags": ["milestone:2", "area:gateway"]
    }
    ? session_id: "abc123..."
    ? Start Time: 9:45:32 AM EST

    [calls time_task_start] task_id: "M2-001"
    ... implements M2-001 ...
    [calls time_task_end] task_id: "M2-001"
    ? Duration: 1 minute 13 seconds

    [calls time_task_start] task_id: "M2-002"
    ... implements M2-002 ...
    [calls time_task_end] task_id: "M2-002"
    ? Duration: 29 seconds

    ... continues for all tasks ...

    [calls time_session_end]
    ? Total Duration: 11 minutes 46 seconds
    ? Tasks Completed: 5/5
```

### Pattern 2: Batch Execution (No Per-Task Tracking)

For batch execution where individual task timing isn't needed:

```
AI: [calls time_session_start]
    ? session_id, start_time

    ... executes all tasks as a batch ...

    [calls time_session_end]
    ? total_duration (tasks array will show tasks_not_started)
```

### Pattern 3: Context Injection via User Prompt

The user provides session context in their prompt:

```
User: Execute Milestone 2 tasks M2-001 through M2-018.
      
      Session Context:
      {
        "milestone_id": "M2",
        "milestone_name": "Commit + Lifecycle", 
        "task_ids": ["M2-001", "M2-002", ..., "M2-018"],
        "timezone": "America/New_York",
        "tags": ["milestone:2", "area:gateway", "area:orchestrator"]
      }

AI: I'll start a tracking session for Milestone 2.
    [calls time_session_start with the provided context]
    ...
```

### Pattern 4: Mid-Execution Report Check

```
AI: [calls time_session_summary]
    ? Current elapsed: 5 minutes 23 seconds
    ? Tasks completed: 3/10
    ? Tasks in progress: 1
    
    "Halfway through - on track to complete in approximately 10 minutes."
```

---

## Execution Report Integration

The session summary from `time_session_end` can be directly used to populate execution reports:

```markdown
# Milestone 2 Execution Report: Commit + Lifecycle

**Execution Date:** December 14, 2025  
**Start Time:** 9:45:32 AM EST          ? from session.start_time
**End Time:** 9:57:18 AM EST            ? from session.end_time
**Actual Duration:** 11 minutes 46 seconds  ? from session.total_duration
**Branch:** Recipe-Ingest-Agent         ? from session.metadata.branch
**Tags:** milestone:2, area:gateway     ? from session.tags

---

## Task Execution Log

| Task ID | Task Name | Start | End | Duration | Status |
|---------|-----------|-------|-----|----------|--------|
| M2-001 | Create ImportRecipeRequest model | 9:45:32 AM | 9:46:45 AM | 1m 13s | ? |
| M2-002 | Create ImportRecipeResponse model | 9:46:45 AM | 9:47:15 AM | 29s | ? |
| ... | ... | ... | ... | ... | ... |

---

## Summary

- **Tasks Completed:** 18/18       ? from session.tasks_completed
- **Tasks Skipped:** 0             ? from session.tasks_skipped
- **Total Duration:** 11 minutes 46 seconds
```

---

## Implementation Notes

### Session Service Interface

```csharp
public interface ISessionService
{
    Session StartSession(SessionStartRequest request, string? mcpSessionId = null);
    TaskRecord StartTask(string sessionId, TaskStartRequest request);
    TaskRecord EndTask(string sessionId, TaskEndRequest request);
    Session EndSession(string sessionId);
    Session? GetSession(string sessionId);
    Session? GetSessionSummary(string sessionId);
    void CleanupExpiredSessions();
}
```

### In-Memory Storage Implementation

```csharp
public class InMemorySessionService : ISessionService
{
    private readonly ConcurrentDictionary<string, Session> _sessions = new();
    private readonly TimeSpan _sessionTimeout = TimeSpan.FromHours(24);
    private readonly TimeSpan _inactivityTimeout = TimeSpan.FromHours(4);
    private readonly int _maxSessions = 100;
    private readonly int _maxTasksPerSession = 500;

    public Session StartSession(SessionStartRequest request, string? mcpSessionId = null)
    {
        if (_sessions.Count >= _maxSessions)
        {
            CleanupExpiredSessions();
            if (_sessions.Count >= _maxSessions)
                throw new InvalidOperationException("Maximum session limit reached");
        }

        var now = DateTimeOffset.UtcNow;
        var session = new Session
        {
            SessionId = Guid.NewGuid().ToString(),
            McpSessionId = mcpSessionId,
            MilestoneId = request.MilestoneId,
            MilestoneName = request.MilestoneName,
            TaskIds = request.TaskIds,
            StartTime = now,
            StartTicks = Stopwatch.GetTimestamp(),
            Timezone = request.Timezone ?? TimeZoneInfo.Local.Id,
            Metadata = request.Metadata ?? new(),
            Tags = request.Tags ?? new(),
            Tasks = new(),
            LastActivityTime = now
        };

        _sessions[session.SessionId] = session;
        return session;
    }

    public TaskRecord StartTask(string sessionId, TaskStartRequest request)
    {
        var session = GetSessionOrThrow(sessionId);
        
        if (session.Tasks.Count >= _maxTasksPerSession)
            throw new InvalidOperationException("Maximum tasks per session limit reached");

        // Check for already running task (idempotent)
        var existingTask = session.Tasks.FirstOrDefault(t => 
            t.TaskId == request.TaskId && t.Status == "in_progress");
        
        if (existingTask != null)
        {
            return existingTask with { AlreadyRunning = true };
        }

        var now = DateTimeOffset.UtcNow;
        var task = new TaskRecord
        {
            TaskId = request.TaskId,
            TaskName = request.TaskName,
            ExternalTaskId = request.ExternalTaskId,
            WorkItemId = request.WorkItemId,
            StartTime = now,
            StartTicks = Stopwatch.GetTimestamp(),
            Status = "in_progress",
            Metadata = request.Metadata ?? new()
        };

        session.Tasks.Add(task);
        session.LastActivityTime = now;
        return task;
    }

    public TaskRecord EndTask(string sessionId, TaskEndRequest request)
    {
        var session = GetSessionOrThrow(sessionId);
        var task = session.Tasks.FirstOrDefault(t => 
            t.TaskId == request.TaskId && t.Status == "in_progress");

        if (task == null)
        {
            return new TaskRecord 
            { 
                TaskId = request.TaskId, 
                Status = "error",
                Error = true,
                ErrorCode = "TASK_NOT_STARTED"
            };
        }

        var now = DateTimeOffset.UtcNow;
        var endTicks = Stopwatch.GetTimestamp();
        var durationMs = (endTicks - task.StartTicks) * 1000 / Stopwatch.Frequency;

        task.EndTime = now;
        task.EndTicks = endTicks;
        task.DurationMs = durationMs;
        task.Status = request.Status ?? "completed";
        session.LastActivityTime = now;

        return task;
    }

    // ... other methods
}
```

### Performance Requirements

| Tool | Target Latency |
|------|----------------|
| `time_get_current` | < 1ms |
| `time_session_start` | < 5ms |
| `time_task_start` | < 2ms |
| `time_task_end` | < 2ms |
| `time_session_end` | < 10ms |
| `time_session_summary` | < 5ms |

### Security Considerations

- No sensitive information exposed
- Read-only access to system time
- Sessions contain only user-provided identifiers and metadata
- No file system or network access required
- Session data is ephemeral (in-memory only)
- MCP session binding prevents cross-client data access

---

## Mapping to Recipe Ingest Platform

The Time Tracker tool aligns with the Recipe Ingest Platform's existing task model:

| Platform Concept | Time Tracker Mapping |
|------------------|---------------------|
| `AgentTask.Id` | `external_task_id` in TaskRecord |
| `AgentTask.ThreadId` | Can be stored in session `metadata` |
| `TaskState.Status` | `status` in TaskRecord |
| Milestone (M0, M1, M2, etc.) | `milestone_id` in Session |
| Task ID (M2-001, M2-002, etc.) | `task_id` in TaskRecord |

### Example: Recipe Ingest Milestone Execution

**Session = Milestone 1 (URL Import Vertical Slice)**

Tasks:
- M1-001: Create IngestTaskPayload model
- M1-002: Create CreateIngestTaskRequest model
- M1-004: Update POST /api/tasks for AgentType=Ingest
- ... (83 tasks total)

**Tags:**
- `milestone:1`
- `area:gateway`
- `area:orchestrator`
- `area:fetch`
- `area:extraction`

---

## Success Criteria

### V1 Success Criteria

1. ? AI assistant can query current system time
2. ? Timestamps are accurate (within 1 second of actual time)
3. ? Multiple format options work correctly
4. ? Timezone handling works correctly
5. ? Tool responds in < 1ms

### V2 Success Criteria

1. ? AI can start a session with milestone/task context
2. ? AI can track start/end times for individual tasks
3. ? Elapsed time calculations are accurate (monotonic time)
4. ? Session summary provides all data needed for execution reports
5. ? Session state persists across tool calls within a conversation
6. ? Multiple concurrent sessions are supported
7. ? Sessions expire after configured timeout
8. ? Idempotent task start behavior
9. ? Proper error handling for invalid operations
10. ? MCP session binding prevents cross-client issues

---

## Open Questions

1. **How will Copilot discover this MCP server?** 
   - Manual registration in VS settings
   - `.vscode/mcp.json` for VS Code
   - Part of a Copilot extension marketplace (future)

2. **Should session data be persisted to disk?**
   - Current spec: in-memory only
   - Could add optional file/SQLite persistence for crash recovery

3. **Should parallel task support include aggregation?**
   - Current spec: independent elapsed per task
   - Could add overlapping time detection/reporting

---

## References

- [MCP Transports Specification](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports)
- [Official C# MCP SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- [VS Code MCP Servers Documentation](https://code.visualstudio.com/docs/copilot/customization/mcp-servers)

---

## Version History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-12-14 | AI-Human Collaboration | Initial specification |
| 1.1 | 2025-12-14 | AI-Human Collaboration | Simplified to V1/V2 phased approach |
| 2.0 | 2025-12-14 | AI-Human Collaboration | Full V2 specification with Milestone/Task tracking, in-memory session storage |
| 2.1 | 2025-12-14 | AI-Human Collaboration | Incorporated feedback: MCP session binding, monotonic time for durations, idempotency/concurrency model, retention limits, summary tool, external ID mappings, VS Code config, references || 2.1 | 2025-12-14 | AI-Human Collaboration | Incorporated feedback: MCP session binding, monotonic time for durations, idempotency/concurrency model, retention limits, summary tool, external ID mappings, VS Code config, references |