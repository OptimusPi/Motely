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

### Good: Async I/O - Just await it!

```csharp
// For async I/O operations - just await directly
await SomeAsyncMethod(ct);

// If you need to track it:
var task = SomeAsyncMethod(ct);
_runningTasks.TryAdd(id, task);
await task; // or don't await if truly fire-and-forget
```

### Good: Blocking synchronous work on thread pool (rare - only when needed)

```csharp
// ONLY use Task.Run for blocking synchronous work that would deadlock the current thread
// Example: Test helper that needs to timeout a blocking call
var joinTask = Task.Run(() => search.AwaitCompletion()); // AwaitCompletion calls Thread.Join
if (!joinTask.Wait(timeout))
    throw new TimeoutException();
```

### Good: Fire-and-forget async with exception handling

```csharp
// If you MUST fire-and-forget async I/O (rare), handle exceptions
_ = SomeAsyncMethod(ct).ContinueWith(t =>
{
    if (t.IsFaulted)
        _logger.LogError(t.Exception, "Background task failed");
}, TaskContinuationOptions.OnlyOnFaulted);
```

### Bad: Task.Run wrapping async I/O (ANTI-PATTERN!)

```csharp
// DON'T DO THIS - wastes thread pool threads!
var task = Task.Run(async () => await SomeAsyncMethod(ct)); // WRONG!
_ = Task.Run(async () => { await SomeAsyncMethod(); }); // WRONG!
```

### Bad: Fire-and-forget with no handling

```csharp
_ = SomeAsyncMethod(); // EXCEPTIONS LOST!
```

### Bad: Blocking on async

```csharp
task.Result;      // DEADLOCK RISK
task.Wait();      // DEADLOCK RISK  
task.GetAwaiter().GetResult(); // DEADLOCK RISK
```

## Key Principle

**Task.Run is NEVER appropriate in modern C# apps.** 

- **Async I/O** → Just `await` it directly
- **Fire-and-forget async** → Call the async method and handle exceptions with `ContinueWith`
- **Synchronous work** → Just call it directly (or make it async if it blocks)

The ONLY exception: Test helpers that need to timeout blocking synchronous calls (like `Thread.Join`). Even then, prefer async alternatives if available.

**Rule: If you're writing `Task.Run`, you're probably doing it wrong.**

