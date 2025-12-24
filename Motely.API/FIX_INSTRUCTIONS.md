# Fix Instructions for Cursor AI

**Target:** Motely.API codebase  
**Priority:** Fix in order listed (Critical → High → Medium)

---

## Critical Fixes

### Fix C1: Add Logging to Swallowed Exceptions

**File:** `SearchManager.cs`  
**Lines:** 515, 569

**Current:**
```csharp
try { search.Database?.Checkpoint(); } catch { }
try { search.Database?.SaveBatchPosition(search.CompletedBatches, searchParams.BatchSize); } catch { }
```

**Change to:**
```csharp
try { search.Database?.Checkpoint(); } 
catch (Exception ex) { Console.WriteLine($"[SearchManager] Checkpoint failed for {search.SearchId}: {ex.Message}"); }

try { search.Database?.SaveBatchPosition(search.CompletedBatches, searchParams.BatchSize); } 
catch (Exception ex) { Console.WriteLine($"[SearchManager] SaveBatchPosition failed for {search.SearchId}: {ex.Message}"); }
```

---

### Fix C2: Convert Blocking Calls to Async

**File:** `Program.cs`  
**Lines:** 1045-1073 and 1076-1112

**Current:**
```csharp
app.MapPost("/filters/columns", (HttpRequest request) =>
{
    try
    {
        var req = request.ReadFromJsonAsync<FilterColumnsRequest>().Result;
```

**Change to:**
```csharp
app.MapPost("/filters/columns", async (HttpRequest request) =>
{
    try
    {
        var req = await request.ReadFromJsonAsync<FilterColumnsRequest>();
```

Apply same pattern to `/filters/update-column-label` endpoint.

---

### Fix C3: Simplify Singleton Pattern

**File:** `SearchManager.cs`  
**Lines:** 17-33

**Current:**
```csharp
private static SearchManager? _instance;
private static readonly object _lock = new();

public static SearchManager Instance
{
    get
    {
        if (_instance == null)
        {
            lock (_lock)
            {
                _instance ??= new SearchManager();
            }
        }
        return _instance;
    }
}
```

**Change to:**
```csharp
private static readonly Lazy<SearchManager> _instance = new(() => new SearchManager());

public static SearchManager Instance => _instance.Value;
```

Apply same pattern to `FertilizerDatabase.cs`.

---

## High Priority Fixes

### Fix H3: Dispose CancellationTokenSource

**File:** `SearchManager.cs`

Add disposal in `StopSearchInternalAsync` after line 870:

```csharp
try
{
    search.CancellationToken?.Cancel();
}
catch (Exception ex)
{
    Console.WriteLine($"Error canceling cancellation token for search {search.SearchId}: {ex.Message}");
}

// ADD THIS:
try
{
    search.CancellationToken?.Dispose();
    search.CancellationToken = null;
}
catch { }
```

---

### Fix H5: Remove Dead Code

**File:** `jaml.js`  
**Lines:** 2373-2457

Delete the entire `initActiveSearchesGrabber()` function - it references elements that no longer exist after the collapsible panel refactor.

Also delete the global variable on line 2260:
```javascript
let activeSearchesPanelHeight = 100; // Default height
```

---

## Medium Priority Fixes

### Fix M1: Add HTTP Timeout

**File:** `McpServer.cs`

In constructor, after line 28:

```csharp
_httpClient = httpClient;
_httpClient.Timeout = TimeSpan.FromSeconds(30); // Add timeout
```

---

### Fix M2: Cleanup Event Listeners

**File:** `jaml.js`

Create cleanup function and call on page unload:

```javascript
// Add near top of file
const cleanupFunctions = [];

function registerCleanup(fn) {
  cleanupFunctions.push(fn);
}

window.addEventListener('beforeunload', () => {
  cleanupFunctions.forEach(fn => fn());
});
```

Then in drag handlers, store references and register cleanup:

```javascript
// In initSplitter, initTopGrabber, initCollapsibleDrag
const onMouseMove = (e) => { ... };
document.addEventListener('mousemove', onMouseMove);
registerCleanup(() => document.removeEventListener('mousemove', onMouseMove));
```

---

### Fix M6: Cache Catalog Generation

**File:** `McpServer.cs`

Add static cached catalog:

```csharp
private static string? _cachedCatalog = null;
private static readonly object _catalogLock = new();

private string GetCatalogText()
{
    if (_cachedCatalog != null) return _cachedCatalog;
    
    lock (_catalogLock)
    {
        if (_cachedCatalog != null) return _cachedCatalog;
        
        // Existing catalog generation code...
        _cachedCatalog = catalogText.ToString();
        return _cachedCatalog;
    }
}
```

---

## Low Priority Fixes

### Fix L3: Delete Temp Files

Delete these files from the repository:
- `temp_program.cs`
- `temp_program_backup.cs`  
- `McpServer.cs.tmp`

Add to `.gitignore`:
```
*.tmp
temp_*.cs
```

---

### Fix L4: Remove Debug Console Logs

**File:** `jaml.js`

Search and evaluate each `console.log` and `console.warn` - remove debug statements, keep error logging.

Current count: 24 statements. Keep only those in error handlers.

---

## Validation Checklist

After applying fixes, verify:

- [ ] `dotnet build Motely.API` succeeds with no warnings
- [ ] All API endpoints respond correctly
- [ ] Search start/stop works
- [ ] Filter save/load works
- [ ] No JavaScript errors in browser console
- [ ] SignalR connection establishes
- [ ] Results stream to UI in real-time

---

## Do NOT Change

These patterns look unusual but are intentional:

1. **Dual polling + SignalR** - Redundancy for reliability
2. **35-character seed alphabet** - Balatro game requirement (no 0, no lowercase)
3. **DuckDB over SQLite** - Performance requirement for SIMD operations
4. **No compression/minification** - Cloudflare Tunnel handles this

---

*End of Fix Instructions*


