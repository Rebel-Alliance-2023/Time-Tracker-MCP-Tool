# Implementation Plan: Time Tracker MCP Tool (V1 → V2)

**Applies to:** *Time Tracker MCP Tool Specification* (v2.1)  
**Target stack:** .NET 10, official MCP C# SDK (`ModelContextProtocol.Server`), Streamable HTTP transport  
**Primary IDE:** Visual Studio 2026 (GA)  
**Primary use case:** enabling AI coding assistants (Copilot, etc.) to produce accurate execution reports with real timestamps and measured durations.

---

## Problem

AI coding assistants lack a trusted way to (1) query current time and (2) track elapsed durations for milestone/task workflows, resulting in simulated timestamps and unreliable execution reports.

## Solution

Build a **standalone** MCP server that exposes a minimal set of time tools:

- **V1:** `time_get_current` (stateless)
- **V2:** `time_session_start`, `time_task_start`, `time_task_end`, `time_session_end`, `time_session_summary` (stateful, in-memory)

The server runs independently of any project being modified by the assistant, so it can be registered and used immediately.

---

## 1. Scope

### 1.1 In scope
- Streamable HTTP MCP server (single `/mcp` endpoint) exposing time tools
- In-memory session storage (ConcurrentDictionary), with retention limits:
  - Max 100 sessions
  - Max 500 tasks per session
  - 24-hour max session age
  - 4-hour inactivity expiry
- Accurate timestamp formatting + timezone handling
- Accurate elapsed durations (monotonic clock)
- Test suite + CI build pipeline
- Registration docs for Visual Studio 2026 and VS Code

### 1.2 Out of scope (v1/v2)
- Durable persistence (SQLite/file) for sessions (deferred)
- Multi-tenant auth (optional future)
- Deep integrations into Cookbook platform services (must remain standalone)

### 1.3 Repository
- **Separate GitHub repository**: `copilot-mcp-tools` (or similar)
- This will be the first of potentially many Copilot MCP tools
- Independent of `cookbook-agent-platform`

---

## 2. External references (official docs)

- MCP specification & protocol schemas: `https://github.com/modelcontextprotocol/modelcontextprotocol`
- MCP Streamable HTTP transport and session header (`Mcp-Session-Id`):  
  `https://modelcontextprotocol.io/specification/2025-11-25/basic/transports`
- MCP C# SDK:
  - Repo: `https://github.com/modelcontextprotocol/csharp-sdk`
  - API docs: `https://modelcontextprotocol.github.io/csharp-sdk/`
- Build an MCP server in C# (.NET blog): `https://devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp/`
- VS Code MCP server configuration:
  - "Use MCP servers in VS Code": `https://code.visualstudio.com/docs/copilot/customization/mcp-servers`
  - MCP extension guide: `https://code.visualstudio.com/api/extension-guides/ai/mcp`
- Visual Studio MCP servers (Copilot): `https://learn.microsoft.com/en-us/visualstudio/ide/mcp-servers?view=visualstudio`

---

## 3. Architecture Overview

### 3.1 Process model
- A single standalone process: `TimeTrackerMcp`
- Transport: **Streamable HTTP** (preferred modern MCP transport)
- In-memory state only (no disk writes)

### 3.2 Core components
- `TimeTools` (tool handlers)
- `ISessionService` + `InMemorySessionService`
- `SessionCleanupService` (background cleanup)
- Models:
  - `Session` (session metadata, start/end, tasks, last activity)
  - `TaskRecord` (start/end, status, durationMs, metadata)
  - `TimeResult` (formatted time, timezone, utc offset)
- `ITimeZoneResolver` (timezone parsing + IANA to Windows mapping)

---

## 4. Tool surface (summary)

### V1
- `time_get_current({ format?, timezone? }) -> { timestamp, timezone, utc_offset }`

### V2
- `time_session_start({ milestone_id, task_ids, milestone_name?, timezone?, metadata?, tags? }) -> { session_id, start_time, ... }`
- `time_task_start({ session_id, task_id, task_name?, external_task_id?, work_item_id?, metadata? }) -> { task_id, start_time, ... }`
- `time_task_end({ session_id, task_id, status?, metadata? }) -> { task_id, end_time, duration, duration_ms, ... }`
- `time_session_end({ session_id, include_task_details? }) -> { session summary }`
- `time_session_summary({ session_id, include_task_details? }) -> { session summary without ending }`

---

## 5. Milestones

### Milestone 0 — Repo scaffolding and MCP server skeleton
**Objective:** stand up a buildable, runnable MCP server with correct transport and tool registration.

**Deliverables**
- New GitHub repo: `copilot-mcp-tools`
- Repo layout:
  - `src/TimeTrackerMcp/` (server)
  - `tests/TimeTrackerMcp.Tests/` (xUnit)
- MCP server bootstrap using the MCP C# SDK
- Health logging to stderr (avoid stdout pollution)
- Minimal README: how to run locally, how to register with Visual Studio 2026

**Acceptance criteria**
- `dotnet test` passes
- `dotnet run` starts the MCP server and exposes the MCP endpoint

---

### Milestone 1 — V1 tool: `time_get_current`
**Objective:** provide accurate current time with formatting and timezone support.

**Deliverables**
- Implement `time_get_current` with:
  - `format`: `iso8601` (default), `unix`, `unix_ms`, `friendly`
  - `timezone`: `local` default; accept `UTC` and IANA names (e.g., `America/New_York`)
- `ITimeZoneResolver` for IANA to Windows timezone mapping
- Validation:
  - unknown format → structured error
  - unknown timezone → structured error
- Unit tests for:
  - format rendering
  - timezone conversion
  - UTC offset formatting

**Acceptance criteria**
- Tool latency meets the spec's target (< 1ms typical, dev machine)
- Output is stable and correct across repeated calls

---

### Milestone 2 — V2 sessions: in-memory session tracking
**Objective:** introduce session lifecycle and in-memory state with retention limits.

**Deliverables**
- Implement `ISessionService` backed by `ConcurrentDictionary<string, Session>`
- `time_session_start`:
  - generates secure session id
  - records `milestone_id`, `milestone_name`, `task_ids`
  - records `start_time` (wall-clock) + `start_ticks` (monotonic)
  - stores `timezone`, `metadata`, `tags`
  - updates `last_activity_at`
  - binds to MCP protocol session ID (if available)
- `time_session_end`:
  - records end time
  - computes total duration (monotonic)
  - returns session summary + optional per-task breakdown
  - idempotent: if already ended, return existing summary + `already_ended=true`
- `time_session_summary`:
  - returns current session state without ending
  - tasks in progress show current elapsed time
- Retention limits:
  - Max 100 sessions
  - Max 500 tasks per session
  - Max 24-hour session age
  - Max 4-hour inactivity
- `SessionCleanupService`:
  - background timer (every 5 minutes)
  - evicts expired sessions

**Acceptance criteria**
- Multiple concurrent sessions supported without cross-talk
- Sessions are evicted after limits exceeded
- `time_session_summary` works mid-execution

---

### Milestone 3 — V2 tasks: task timing and summary
**Objective:** record task start/end, compute durations, and provide summary output suitable for execution reports.

**Deliverables**
- `time_task_start`:
  - idempotent: if already running, return existing start + `already_running=true`
  - records `task_id`, `task_name`, `external_task_id`, `work_item_id`
  - records `start_time` (wall-clock) + `start_ticks` (monotonic)
  - stores metadata
  - validates task count limit (500)
- `time_task_end`:
  - validates task exists and is in progress
  - if not started: return error `TASK_NOT_STARTED`
  - status: `completed` (default), `skipped`
  - computes duration from monotonic ticks
  - records `end_time`, `duration_ms`, `status`
  - merges completion metadata
- Summary fields in responses:
  - `tasks_completed`, `tasks_skipped`, `tasks_remaining`, `tasks_not_started`
  - human-readable durations + `duration_ms`
  - `session_elapsed` (time since session start)
- Unit tests:
  - idempotent start
  - end without start → error
  - overlapping/parallel tasks
  - skipped tasks
  - duration accuracy with clock changes

**Acceptance criteria**
- Session end response includes complete data for milestone execution report
- Task duration measurements remain accurate if system clock changes

---

### Milestone 4 — Packaging + registration workflows (Visual Studio 2026 + VS Code)
**Objective:** make the tool easy to install/register in common IDE hosts.

**Deliverables**
- Provide 2 run modes:
  1) `dotnet run` (dev)
  2) self-contained single-file publish (production)
- Registration examples:
  - **Visual Studio 2026**: 
    - Options → GitHub Copilot → MCP Servers registration steps
    - `mcp.json` in solution directory (if supported)
  - **VS Code**: 
    - `.vscode/mcp.json` sample for Streamable HTTP server
    - CLI-based `--add-mcp` example (user profile)
- Document tool permissions and minimal risk posture (no filesystem/network required)
- README with quick-start guide

**Acceptance criteria**
- Developer can register the server in Visual Studio 2026 and invoke `time_get_current`
- Developer can register the server in VS Code and invoke tools

---

### Milestone 5 — CI, quality gates, and release packaging
**Objective:** ensure repeatable builds and safe evolution.

**Deliverables**
- GitHub Actions workflow:
  - build (.NET 10)
  - test (xUnit)
  - publish artifact (self-contained single-file)
- Versioning:
  - Semantic versioning
  - Git tags/releases with changelog notes
- Structured logging:
  - tool name, duration, session/task ids
  - no PII, no file paths

**Acceptance criteria**
- Clean CI runs on every PR
- Release artifact can be downloaded and run locally
- Version available in tool responses or logs

---

## 6. Implementation details and decisions

### 6.1 Transport and sessions
- Use **Streamable HTTP** transport with MCP session header handling:
  - server MAY assign session id via `Mcp-Session-Id` header
  - client sends same header on subsequent requests
- Mapping:
  - MCP session header (protocol-level session) = connection continuity
  - Time Tracker `session_id` = logical "run id" (milestone execution run)
  - Store both in Session record for debugging

### 6.2 Timezone handling (IANA)
The spec expects IANA timezone names. On Windows, .NET's `TimeZoneInfo` uses Windows IDs by default:
- `ITimeZoneResolver` interface:
  - accepts `local` → `TimeZoneInfo.Local`
  - accepts `UTC` → `TimeZoneInfo.Utc`
  - accepts IANA names → maps to Windows IDs (use `TimeZoneConverter` NuGet or similar)
  - returns structured error for unknown IDs

### 6.3 Duration correctness
- Store timestamps with `DateTimeOffset.UtcNow` (audit)
- Compute elapsed time with `Stopwatch.GetTimestamp()` (monotonic, precision)
- Duration formula: `(endTicks - startTicks) * 1000 / Stopwatch.Frequency`

### 6.4 Idempotency and error semantics
| Tool | Scenario | Response |
|------|----------|----------|
| `time_task_start` | Task already running | Return existing start + `already_running=true` |
| `time_task_end` | Task not started | Return `error=true`, `error_code=TASK_NOT_STARTED` |
| `time_session_end` | Already ended | Return existing summary + `already_ended=true` |
| `time_session_*` | Session not found | Return `error=true`, `error_code=SESSION_NOT_FOUND` |

### 6.5 Retention limits
| Limit | Value | Enforcement |
|-------|-------|-------------|
| Max sessions | 100 | Reject new session if at limit (after cleanup attempt) |
| Max tasks per session | 500 | Reject new task if at limit |
| Max session age | 24 hours | Background cleanup |
| Max inactivity | 4 hours | Background cleanup |

### 6.6 Security posture
- No filesystem access
- No outbound network calls
- In-memory only + expiry
- Only user-provided identifiers and metadata stored

---

## 7. How this is used with Cookbook platform milestones (example)

A typical sequence while implementing "Recipe Ingest Agent" milestones:

1. `time_session_start` with:
   ```json
   {
     "milestone_id": "M1",
     "milestone_name": "URL Import Vertical Slice",
     "task_ids": ["M1-001", "M1-002", ..., "M1-083"],
     "timezone": "America/New_York",
     "metadata": { "branch": "Recipe-Ingest-Agent" },
     "tags": ["milestone:1", "area:gateway", "area:orchestrator"]
   }
   ```
2. For each task:
   - `time_task_start(session_id, "M1-001", { "task_name": "Create IngestTaskPayload model" })`
   - ... implement task ...
   - `time_task_end(session_id, "M1-001", { "files_created": "IngestTaskPayload.cs" })`
3. Mid-execution check (optional):
   - `time_session_summary(session_id)` → see progress without ending
4. `time_session_end(session_id, include_task_details=true)`
5. Use the returned summary to produce an execution report with real durations.

---

## 8. Open questions (from spec) and recommended answers

1) **How will Copilot discover this MCP server?**  
   - Visual Studio 2026: Options → GitHub Copilot → MCP Servers
   - VS Code: `.vscode/mcp.json` or user profile configuration

2) **Should this be proposed as native Copilot feature?**  
   - Not required for v1/v2; keep as standalone MVP first.

3) **Should session data persist to disk?**  
   - Keep in-memory for v2. Consider opt-in file/SQLite in v3.

4) **Should there be `time_task_skip`?**  
   - Not needed: use `time_task_end(status="skipped")`.

5) **Separate repo or integrated?**  
   - **Separate repo**: `copilot-mcp-tools` — first of potentially many MCP tools.

---

## 9. Deliverable checklist

- [ ] New GitHub repo: `copilot-mcp-tools`
- [ ] Standalone `.NET 10` MCP server project
- [ ] V1: `time_get_current`
- [ ] V2: sessions + task timing + summary (5 tools)
- [ ] Retention limits + background cleanup
- [ ] xUnit tests
- [ ] CI workflow (GitHub Actions)
- [ ] README + registration examples (Visual Studio 2026 + VS Code)

---

## 10. Priority

**Immediate implementation** — This tool is needed to produce accurate time metrics for the Recipe Ingest Agent project (and all future Copilot-assisted development).

---

## 11. Version History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-12-14 | AI-Human Collaboration | Initial implementation plan |
| 1.1 | 2025-12-14 | AI-Human Collaboration | Updated to spec v2.1: added `time_session_summary`, retention limits, VS 2026 as primary target, separate repo decision, immediate priority |
