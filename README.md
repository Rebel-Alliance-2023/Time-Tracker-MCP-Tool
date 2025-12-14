# Time-Tracker-MCP-Tool

**Time Tracker MCP (Model Context Protocol) Tool** designed to provide AI coding assistants (such as GitHub Copilot) with the ability to query the current system time and track execution time for Milestone/Task workflows. This enables accurate execution reports with real wall-clock timestamps and durations.

---

## Problem Statement

AI coding assistants currently lack the ability to:

1. **Query the current system time** — Cannot determine when tasks start or end
2. **Track elapsed time** — Cannot measure how long operations take
3. **Generate accurate execution reports** — Must rely on user-provided timestamps or generate simulated/estimated durations

---

## Features

### V1: Basic Time Query (Stateless)
- `time_get_current` — Query current system time with format and timezone options

### V2: Session-Based Tracking (Stateful)
- `time_session_start` — Initialize milestone tracking session
- `time_task_start` — Mark task start (idempotent)
- `time_task_end` — Mark task completion with duration
- `time_session_end` — End session and return summary
- `time_session_summary` — Get status without ending session

---

## Technology Stack

- **.NET 10** — Latest LTS runtime
- **ModelContextProtocol.Server** — Official C# MCP SDK from Microsoft
- **In-memory storage** — ConcurrentDictionary for session state
- **Streamable HTTP Transport** — Standard MCP server hosting

---

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Build and Run

```bash
# Clone the repository
git clone https://github.com/Rebel-Alliance-2023/Time-Tracker-MCP-Tool.git
cd Time-Tracker-MCP-Tool

# Build
dotnet build

# Run tests
dotnet test

# Run the MCP server
dotnet run --project src/TimeTrackerMcp/TimeTrackerMcp.csproj
```

---

## Registration

### Visual Studio 2026

1. Open **Tools** ? **Options** ? **GitHub Copilot** ? **MCP Servers**
2. Add a new server pointing to the running Time Tracker MCP endpoint
3. Restart Visual Studio to apply changes

### VS Code

Add to `.vscode/mcp.json` in your workspace:

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

---

## Usage Example

```
AI: [calls time_session_start]
    milestone_id: "M2"
    task_ids: ["M2-001", "M2-002", "M2-003"]
    ? session_id: "abc123..."

    [calls time_task_start] task_id: "M2-001"
    ... implements task ...
    [calls time_task_end] task_id: "M2-001"
    ? Duration: 1 minute 13 seconds

    [calls time_session_end]
    ? Total Duration: 5 minutes 42 seconds
    ? Tasks Completed: 3/3
```

---

## Project Structure

```
Time-Tracker-MCP-Tool/
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
??? docs/
?   ??? Time Tracker MCP Tool Specification.md
?   ??? Implementation Plan - Time Tracker MCP Tool.md
?   ??? Time Tracker MCP Tool - Task List.md
??? .vscode/
?   ??? mcp.json
??? .github/
?   ??? workflows/
?       ??? build.yml
??? README.md
??? LICENSE
```

---

## Documentation

- [Specification](docs/Time%20Tracker%20MCP%20Tool%20Specification.md) — Detailed requirements and API schemas
- [Implementation Plan](docs/Implementation%20Plan%20-%20Time%20Tracker%20MCP%20Tool.md) — Architecture and milestones
- [Task List](docs/Time%20Tracker%20MCP%20Tool%20-%20Task%20List.md) — Granular work breakdown

---

## References

- [MCP Specification](https://modelcontextprotocol.io/specification/2025-06-18/basic/transports)
- [Official C# MCP SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- [VS Code MCP Servers](https://code.visualstudio.com/docs/copilot/customization/mcp-servers)

---

## License

MIT License — See [LICENSE](LICENSE) for details.
