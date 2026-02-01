---
name: C# Async and Code Quality Fixes
overview: Fix Task.Run fire-and-forget patterns, async anti-patterns, FancyConsole threading, MCP project compilation, and other code smells across the Motely codebase.
todos:
  - id: wasm_progress_loop
    content: Fix fire-and-forget progress loop in MotelyWasm.cs - add exception handling and cancellation
    status: completed
  - id: tui_copy_button_delay
    content: Fix fire-and-forget Task.Run for UI button reset - use proper async pattern
    status: completed
  - id: tui_tunnel_task
    content: Fix tunnel Task.Run - track task and handle exceptions properly
    status: completed
  - id: fancy_console_desktop
    content: Create FancyConsole.Desktop.cs with proper cursor positioning
    status: completed
  - id: fancy_console_threading
    content: Fix FancyConsole base class to not spam duplicate progress lines
    status: completed
  - id: orchestrator_csv_export
    content: Move CSV export from CLI to Orchestration layer (DRY, architecture)
    status: completed
  - id: mcp_search_manager
    content: Create SearchManager facade for MCP project compatibility
    status: completed
  - id: mcp_using_statements
    content: Add missing using Motely.Analysis to MCP files
    status: completed
isProject: false
---

## Issues Fixed

### 1. FancyConsole Duplicate Progress Lines (FIXED)
**Files:** `Motely/FancyConsole.cs`, `Motely/FancyConsole.Desktop.cs` (new)

**Problem:** Every `WriteLine` call re-printed the bottom line, causing duplicate progress spam.

**Solution:**
- Created `FancyConsole.Desktop.cs` with proper cursor positioning for terminal apps
- Base class now tracks `_lastPrintedBottomLine` to prevent duplicate prints
- `WriteLine` no longer re-prints the bottom line (that was the spam source)
- Desktop version: saves cursor, moves to bottom, writes progress, restores cursor
- Fallback: only prints progress when value actually changes

### 2. CSV Export Architecture (FIXED)
**Files:** `Motely.Orchestration/MotelySearchOrchestrator.cs`, `Motely.CLI/Program.cs`

**Problem:** CLI directly called `Motely.DB.ResultsExportHelper` - violated architecture.

**Solution:**
- Added `ExportResultsToCsv()` wrapper method to `MotelySearchOrchestrator`
- CLI now calls orchestrator method, not DB directly
- Architecture: CLI → Orchestration → DB (never CLI → DB directly)

### 3. WASM Progress Loop (FIXED)
**File:** `Motely.WASM/MotelyWasm.cs`

**Problem:** Fire-and-forget `_ = Task.Run(async () =>` with no exception handling.

**Solution:** Added try-catch with logging, respects cancellation token properly.

### 4. TUI Copy Button Delay (FIXED)
**File:** `Motely.TUI/ApiServerWindow.cs`

**Problem:** Fire-and-forget Task.Run for button text reset with no error handling.

**Solution:** Added try-catch wrapper to prevent unobserved exceptions.

### 5. TUI Tunnel Task (FIXED)
**File:** `Motely.TUI/ApiServerWindow.cs`

**Problem:** Task.Run for tunnel start not tracked, no completion handling.

**Solution:** Store task reference in field, add ContinueWith for error logging.

## Patterns to Follow

### Good: Background task with tracking
```csharp
var task = Task.Run(async () => { ... }, ct);
_runningTasks.TryAdd(id, task);
```

### Good: Fire-and-forget with exception handling
```csharp
_ = Task.Run(async () =>
{
    try { ... }
    catch (Exception ex) { _logger.LogError(ex, "..."); }
});
```

### Bad: Fire-and-forget with no handling
```csharp
_ = Task.Run(async () => { ... }); // EXCEPTIONS LOST!
```

### Bad: Blocking on async
```csharp
task.Result;      // DEADLOCK RISK
task.Wait();      // DEADLOCK RISK  
task.GetAwaiter().GetResult(); // DEADLOCK RISK
```
