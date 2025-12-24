# Motely.API Code Audit Report

**Date:** 2024-12-23  
**Auditor:** AI Code Review  
**Scope:** Motely.API project + JAML WebUI

---

## Summary

| Severity | Count |
|----------|-------|
| 🔴 Critical | 3 |
| 🟠 High | 5 |
| 🟡 Medium | 8 |
| 🟢 Low | 6 |

---

## 🔴 Critical Issues

### C1. Swallowed Exceptions in Hot Path (SearchManager.cs)
**Location:** Lines 515, 569  
**Pattern:** `try { ... } catch { }`

```csharp
try { search.Database?.Checkpoint(); } catch { }
try { search.Database?.SaveBatchPosition(search.CompletedBatches, searchParams.BatchSize); } catch { }
```

**Problem:** Silent exception swallowing in the search execution loop. If database writes fail, the user has no idea their results aren't being saved. Data loss risk.

**Impact:** User could run a multi-hour search and lose all results due to a silent disk full error.

---

### C2. Blocking Async Calls in Sync Endpoints (Program.cs)
**Location:** Lines 1049, 1080

```csharp
var req = request.ReadFromJsonAsync<FilterColumnsRequest>().Result;
var req = request.ReadFromJsonAsync<FilterUpdateColumnLabelRequest>().Result;
```

**Problem:** `.Result` blocks the thread pool. Under load, this can cause thread starvation and deadlocks.

**Impact:** API becomes unresponsive under concurrent requests.

---

### C3. Singleton Pattern with Race Condition (SearchManager.cs, FertilizerDatabase.cs)
**Location:** Lines 17-32 (SearchManager), Lines 11-27 (FertilizerDatabase)

```csharp
if (_instance == null)
{
    lock (_lock)
    {
        _instance ??= new SearchManager();
    }
}
```

**Problem:** Double-checked locking without `volatile` keyword. In theory, the JIT could reorder writes, causing a partially constructed object to be returned. Modern .NET largely handles this, but it's still a code smell.

**Better Pattern:** Use `Lazy<T>` or static constructor initialization.

---

## 🟠 High Severity Issues

### H1. God Classes
**Location:** 
- `Program.cs` - 1,549 lines
- `SearchManager.cs` - 1,382 lines  
- `McpServer.cs` - 1,066 lines

**Problem:** Massive files violate Single Responsibility Principle. Hard to test, maintain, and debug.

---

### H2. Missing Input Validation on API Endpoints
**Location:** Multiple `MapGet`/`MapPost` handlers in Program.cs

**Problem:** Many endpoints don't validate inputs before processing. Example:
- `/search` POST doesn't validate `filterJaml` is valid YAML before creating search
- Seed sources aren't sanitized for path traversal

---

### H3. CancellationToken Not Disposed (SearchManager.cs)
**Location:** Lines 506, 167, 1284

```csharp
search.CancellationToken = new CancellationTokenSource();
```

**Problem:** `CancellationTokenSource` implements `IDisposable` but many instances are never disposed, causing resource leaks.

---

### H4. Console.WriteLine for Error Logging (Multiple Files)
**Location:** 18 occurrences across codebase

**Problem:** Using `Console.WriteLine` for error logging instead of structured logging (`ILogger`). Logs are lost in production, no log levels, no correlation IDs.

---

### H5. Dead Code - initActiveSearchesGrabber (jaml.js)
**Location:** Lines 2373-2450

**Problem:** This function references `activeSearchesGrabber` and `activeSearchesPanel` which no longer exist in the HTML after the refactor to collapsible panels. The function is never called from `onMonacoReady` after changes.

---

## 🟡 Medium Severity Issues

### M1. No Request Timeout on HTTP Calls (McpServer.cs)
**Location:** Line 394

```csharp
var response = await _httpClient.PostAsJsonAsync(_workerUrl, requestBody);
```

**Problem:** No timeout configured. If Cloudflare Worker hangs, the request hangs indefinitely.

---

### M2. Potential Memory Leak - Event Listeners Not Cleaned Up (jaml.js)
**Location:** Multiple drag handlers (lines 615-623, 2428-2434)

**Problem:** Event listeners added to `document` are never removed. If elements are dynamically recreated, old handlers accumulate.

---

### M3. Race Condition in Scheduler Queue (SearchManager.cs)
**Location:** Lines 416-449

**Problem:** `_roundRobinQueue` and `_fastLaneQueue` use `lock(_queueLock)` but the lock is released between checking queue contents and acting on them. Another thread could modify the queue between these operations.

---

### M4. Missing Error Handling in SignalR Connection (jaml.js)
**Location:** `ensureWs()` function

**Problem:** SignalR connection errors aren't gracefully handled. If WebSocket fails, the UI shows no indication.

---

### M5. Hardcoded Magic Numbers (SearchManager.cs)
**Location:** Lines 45, 57, 60

```csharp
private const int ReservedThreads = 1;
public int BatchesPerTurn { get; set; } = 100;
public int DefaultBatchSize { get; set; } = 3;
```

**Problem:** Should be in configuration, not hardcoded.

---

### M6. String Concatenation in Loops (McpServer.cs)
**Location:** Lines 670-735

```csharp
catalogText.AppendLine("JOKERS (type: \"Joker\"):");
```

**Problem:** While `StringBuilder` is used, the catalog generation happens on every request. Should be cached.

---

### M7. No CSRF Protection
**Location:** All POST endpoints in Program.cs

**Problem:** API endpoints that modify state don't validate origin or use anti-forgery tokens. Mitigated by Cloudflare Tunnel but still a concern.

---

### M8. Duplicate McpServer Files
**Location:** 
- `Motely.API/McpServer.cs` (1,066 lines)
- `Motely.API/McpProtocol/McpServer.cs` (440 lines)

**Problem:** Two files with same class name in different namespaces. Confusing. One appears to be an older/alternate implementation.

---

## 🟢 Low Severity Issues

### L1. Inconsistent Null Checking
**Location:** Throughout jaml.js

```javascript
val === undefined || val === null || val === 'undefined'
```

Mixed use of `==`, `===`, and checking for string `'undefined'`. Should be consistent.

---

### L2. TODOs Left in Code
**Location:** 
- `SearchManager.cs:1390` - `// TODO: Get top 1000 from search DB...`
- `McpProtocol/McpServer.cs:421` - `// TODO: Implement resource reading`
- `McpProtocol/McpServer.cs:477` - `// TODO: Implement prompt generation`

---

### L3. Temp/Backup Files in Repository
**Location:** 
- `temp_program.cs`
- `temp_program_backup.cs`
- `McpServer.cs.tmp`

**Problem:** Development artifacts should be in `.gitignore`.

---

### L4. Missing JSDoc Comments (jaml.js)
**Location:** Entire file (2,457 lines)

**Problem:** No function documentation. 24 console.log statements suggest debug code left in.

---

### L5. Unused Variable Warning Already Present
**Location:** Previous audit noted `reachedEnd` variable - this was fixed.

---

### L6. CSS Class Name Collisions
**Location:** styles.css

**Problem:** Generic class names like `.panel-main`, `.button-primary` could conflict if other stylesheets are loaded.

---

## Questions for Clarification

1. **McpProtocol/McpServer.cs** - Is this an older MCP implementation that should be deleted, or is it used for a different purpose?

2. **GenieFeedbackService** - The `failures.jsonl` file grows unbounded. Is there a cleanup strategy?

3. **fertilizer.db** - Created in `Environment.CurrentDirectory`. Is this intentional, or should it be in a specific data directory?

4. **Multiple polling intervals** - Both `statusPollInterval` (2s) and `activeSearchesPollInterval` (2s) run simultaneously. Is this necessary given SignalR is also active?

---

## Metrics

| File | Lines | Endpoints | Catches | TODOs |
|------|-------|-----------|---------|-------|
| Program.cs | 1,549 | 28 | 23 | 1 |
| SearchManager.cs | 1,382 | 0 | 22 | 1 |
| McpServer.cs | 1,066 | 0 | 6 | 0 |
| jaml.js | 2,457 | 0 | 25 try | 0 |
| styles.css | 1,318 | N/A | N/A | 0 |

---

*End of Audit Report*


