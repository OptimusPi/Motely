using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Motely.Analysis;
using Motely.Executors;
using Motely.Filters;
using DuckDB.NET.Data;

namespace Motely.API;

public class SavedSearch
{
    public string Id { get; set; } = "";
    public string FilterJaml { get; set; } = "";
    public string Deck { get; set; } = "Red";
    public string Stake { get; set; } = "White";
    public long Timestamp { get; set; }
}

public class BackgroundSearchState
{
    public bool IsRunning { get; set; }
    public int SeedsAdded { get; set; }
    public int BatchSize { get; set; } // Batch size for this search
    public long StartBatch { get; set; } // Batch we started from (for resume)
    public long CurrentBatch { get; set; } // Updated during search via progress callback
    public long TotalBatches { get; set; } // Total batches for progress calculation
    public long SeedsSearched { get; set; } // Total seeds searched so far
    public double SeedsPerMs { get; set; } // Current search speed
    public int EffectiveCutoff { get; set; } // Cutoff used for this search (user override or smart)
    public JsonSearchExecutor? Search { get; set; }
    public MotelySearchDatabase? Database { get; set; }
    public string? FilterJamlHash { get; set; } // Track if JAML changed to invalidate DB

    /// <summary>
    /// Get top results from search database (clean delegation pattern).
    /// </summary>
    public List<SearchResult> GetTopResults(int limit = 1000)
    {
        return Database?.GetTopResults(limit) ?? new List<SearchResult>();
    }
}

/// <summary>
/// Simple HTTP API server for Motely seed searching
/// </summary>
public class MotelyApiServer
{
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly string _host;
    private readonly int _port;
    private readonly Action<string> _logCallback;

    // Fertilizer pile: ONLY stores seeds (strings), no results!
    // Motely is fast enough to re-search the pile each time with any filter
    // GLOBAL pile - top 1000 from EVERY search gets added, NEVER cleared!
    // NOW STORED IN DUCKDB - no more in-memory HashSet!
    private static DuckDBConnection? _fertilizerConnection;
    private static readonly object _fertilizerLock = new();
    private static readonly ConcurrentDictionary<string, SavedSearch> _savedSearches = new();

    // Single running search (only one can run at a time due to SIMD/CPU constraints)
    private static BackgroundSearchState? _currentSearch;
    private static string? _currentSearchId;

    // Paths for persistence
    private static readonly string _filtersDir = "JamlFilters";
    private static readonly string _searchResultsDir = "SearchResults";
    private static readonly string _fertilizerDbPath = Path.Combine("SearchResults", "fertilizer.db");

    public bool IsRunning => _listener?.IsListening ?? false;
    public string Url => $"http://{_host}:{_port}/";
    public int ThreadCount { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Stops THE running search (there can only be one due to SIMD/CPU constraints).
    /// Dumps seeds to fertilizer, saves batch position, closes connections cleanly.
    /// </summary>
    private async Task StopRunningSearchAsync()
    {
        if (_currentSearch == null || !_currentSearch.IsRunning) return;

        var searchId = _currentSearchId!;
        var bgState = _currentSearch;

        _logCallback($"[{DateTime.Now:HH:mm:ss}] Stopping search '{searchId}' (batch {bgState.CurrentBatch}, {bgState.SeedsAdded} seeds)...");

        // 1. Mark as stopped so callback stops processing
        bgState.IsRunning = false;

        // 2. Cancel the Motely executor (calls Pause internally - immediate stop)
        bgState.Search?.Cancel();

        // 4. Save batch position and checkpoint
        try
        {
            if (bgState.Database != null)
            {
                bgState.Database.SaveBatchPosition(bgState.CurrentBatch, bgState.BatchSize);
                bgState.Database.Checkpoint();
                _logCallback($"[{DateTime.Now:HH:mm:ss}] Saved batch position {bgState.CurrentBatch} to DB");
            }
        }
        catch (Exception ex)
        {
            _logCallback($"[{DateTime.Now:HH:mm:ss}] Warning: Failed to save batch position: {ex.Message}");
        }

        // 5. Get DB path for fertilizer dump
        var searchDbPath = bgState.Database?.DatabasePath;

        // 6. Dispose database (closes connections, checkpoints)
        try
        {
            bgState.Database?.Dispose();
            bgState.Database = null;
        }
        catch (Exception ex)
        {
            _logCallback($"[{DateTime.Now:HH:mm:ss}] Warning: Failed to dispose database: {ex.Message}");
        }

        // 7. Dump seeds to fertilizer AFTER closing search DB (avoids file lock conflict)
        if (!string.IsNullOrEmpty(searchDbPath))
        {
            try
            {
                DumpSearchSeedsToFertilizer(searchDbPath, 1000);
            }
            catch (Exception ex)
            {
                _logCallback($"[{DateTime.Now:HH:mm:ss}] Warning: Failed to dump seeds: {ex.Message}");
            }
        }

        // Update in-memory state for resume
        bgState.StartBatch = bgState.CurrentBatch;
        _logCallback($"[{DateTime.Now:HH:mm:ss}] Search '{searchId}' stopped at batch {bgState.CurrentBatch}");
    }

    public MotelyApiServer(
        string host = "localhost",
        int port = 3141,
        Action<string>? logCallback = null,
        int? threadCount = null
    )
    {
        _host = host;
        _port = port;
        _logCallback = logCallback ?? Console.WriteLine;
        ThreadCount = threadCount ?? Environment.ProcessorCount;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_listener != null)
            throw new InvalidOperationException("Server is already running");

        // Initialize data directories
        Directory.CreateDirectory(_filtersDir);
        Directory.CreateDirectory(_searchResultsDir);

        // Initialize fertilizer DuckDB (replaces old txt file)
        InitializeFertilizerDb();

        // Convert any JSON filters to JAML (one-time migration)
        ConvertJsonFiltersToJaml();

        // Load saved filters from disk
        LoadSavedFilters();

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _listener = new HttpListener();

        // Add localhost prefix for local access
        _listener.Prefixes.Add(Url);

        // ALSO add wildcard prefix for Cloudflare tunnels (accepts any hostname on this port)
        // Uses '+' which means "any hostname" - requires admin on Windows but works with tunnels!
        try
        {
            _listener.Prefixes.Add($"http://+:{_port}/");
            _logCallback($"[{DateTime.Now:HH:mm:ss}] Added wildcard prefix for Cloudflare tunnels: http://+:{_port}/");
        }
        catch (Exception ex)
        {
            // If wildcard fails (no admin), fallback to localhost only
            _logCallback($"[{DateTime.Now:HH:mm:ss}] Wildcard prefix failed (need admin): {ex.Message}");
            _logCallback($"[{DateTime.Now:HH:mm:ss}] Server will only accept localhost connections");
        }

        try
        {
            _listener.Start();
            _logCallback($"[{DateTime.Now:HH:mm:ss}] API Server started on {Url} (+ wildcard if admin)");

            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequestAsync(context), _cts.Token);
                }
                catch (HttpListenerException)
                {
                    // GetContextAsync throws when Stop() is called
                    if (_cts.Token.IsCancellationRequested || !_listener.IsListening)
                        break;
                    throw; // Re-throw if it's a real error
                }
            }
        }
        finally
        {
            _listener.Stop();
            _listener.Close();
            _logCallback($"[{DateTime.Now:HH:mm:ss}] API Server stopped");
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop(); // Force GetContextAsync() to throw and exit the loop

        // Checkpoint fertilizer DB to persist any pending changes
        CheckpointFertilizer();

        // Close fertilizer connection
        lock (_fertilizerLock)
        {
            _fertilizerConnection?.Close();
            _fertilizerConnection = null;
        }
    }

    /// <summary>
    /// Initialize the fertilizer DuckDB database (creates table if needed)
    /// </summary>
    private void InitializeFertilizerDb()
    {
        try
        {
            var fullPath = Path.GetFullPath(_fertilizerDbPath);
            _logCallback($"[{DateTime.Now:HH:mm:ss}] Initializing fertilizer DB at: {fullPath}");

            lock (_fertilizerLock)
            {
                _fertilizerConnection = new DuckDBConnection($"Data Source={_fertilizerDbPath}");
                _fertilizerConnection.Open();

                // Create seeds table with just seed string (no results - Motely re-searches!)
                using var createCmd = _fertilizerConnection.CreateCommand();
                createCmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS seeds (
                        seed VARCHAR PRIMARY KEY
                    )";
                createCmd.ExecuteNonQuery();

                // Get count
                using var countCmd = _fertilizerConnection.CreateCommand();
                countCmd.CommandText = "SELECT COUNT(*) FROM seeds";
                var count = Convert.ToInt64(countCmd.ExecuteScalar());

                _logCallback($"[{DateTime.Now:HH:mm:ss}] Fertilizer DB ready with {count} seeds");

                // Show preview if any seeds exist
                if (count > 0)
                {
                    using var previewCmd = _fertilizerConnection.CreateCommand();
                    previewCmd.CommandText = "SELECT seed FROM seeds LIMIT 5";
                    using var reader = previewCmd.ExecuteReader();
                    var preview = new List<string>();
                    while (reader.Read()) preview.Add(reader.GetString(0));
                    _logCallback($"[{DateTime.Now:HH:mm:ss}]   Preview: {string.Join(", ", preview)}{(count > 5 ? "..." : "")}");
                }
            }

            // Migrate from old fertilizer.txt if it exists (one-time migration)
            MigrateFertilizerTxtToDb();
        }
        catch (Exception ex)
        {
            _logCallback($"[{DateTime.Now:HH:mm:ss}] Failed to initialize fertilizer DB: {ex.Message}");
        }
    }

    /// <summary>
    /// One-time migration from fertilizer.txt to fertilizer.db
    /// </summary>
    private void MigrateFertilizerTxtToDb()
    {
        const string oldPath = "fertilizer.txt";
        if (!File.Exists(oldPath)) return;

        try
        {
            var seeds = File.ReadAllLines(oldPath)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .ToList();

            if (seeds.Count == 0) return;

            _logCallback($"[{DateTime.Now:HH:mm:ss}] Migrating {seeds.Count} seeds from fertilizer.txt using DuckDB COPY...");

            lock (_fertilizerLock)
            {
                if (_fertilizerConnection == null) return;

                // Use DuckDB's COPY FROM for instant bulk loading (4.4M seeds in seconds!)
                var escapedPath = oldPath.Replace("\\", "/").Replace("'", "''");
                using var copyCmd = _fertilizerConnection.CreateCommand();
                copyCmd.CommandText = $"COPY seeds FROM '{escapedPath}' (HEADER false)";
                copyCmd.ExecuteNonQuery();

                // Checkpoint to persist
                using var checkpointCmd = _fertilizerConnection.CreateCommand();
                checkpointCmd.CommandText = "CHECKPOINT";
                checkpointCmd.ExecuteNonQuery();
            }

            // Rename old file so we don't migrate again
            File.Move(oldPath, oldPath + ".migrated");
            _logCallback($"[{DateTime.Now:HH:mm:ss}] Migration complete, renamed old file to {oldPath}.migrated");
        }
        catch (Exception ex)
        {
            _logCallback($"[{DateTime.Now:HH:mm:ss}] Migration warning: {ex.Message}");
        }
    }

    /// <summary>
    /// Get count of seeds in fertilizer DB
    /// </summary>
    private long GetFertilizerCount()
    {
        lock (_fertilizerLock)
        {
            if (_fertilizerConnection == null) return 0;
            try
            {
                using var cmd = _fertilizerConnection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM seeds";
                return Convert.ToInt64(cmd.ExecuteScalar());
            }
            catch { return 0; }
        }
    }

    /// <summary>
    /// Add a single seed to fertilizer DB
    /// </summary>
    private void AddSeedToFertilizer(string seed)
    {
        lock (_fertilizerLock)
        {
            if (_fertilizerConnection == null) return;
            try
            {
                using var cmd = _fertilizerConnection.CreateCommand();
                cmd.CommandText = "INSERT OR IGNORE INTO seeds (seed) VALUES (?)";
                cmd.Parameters.Add(new DuckDBParameter(seed));
                cmd.ExecuteNonQuery();
            }
            catch { /* ignore duplicates */ }
        }
    }

    /// <summary>
    /// Dump top seeds from a search DB to fertilizer DB using INSERT INTO SELECT (no C# memory!)
    /// Attaches the SEARCH DB (must be closed!) to the fertilizer connection
    /// </summary>
    private void DumpSearchSeedsToFertilizer(string searchDbPath, int limit = 1000)
    {
        lock (_fertilizerLock)
        {
            if (_fertilizerConnection == null) return;

            try
            {
                var searchFullPath = Path.GetFullPath(searchDbPath);

                // Attach the SEARCH DB to the FERTILIZER connection (search DB must be closed!)
                using var attachCmd = _fertilizerConnection.CreateCommand();
                attachCmd.CommandText = $"ATTACH '{searchFullPath}' AS search_db (READ_ONLY)";
                attachCmd.ExecuteNonQuery();

                // INSERT INTO local seeds SELECT from search_db.results - NO C# MEMORY!
                using var insertCmd = _fertilizerConnection.CreateCommand();
                insertCmd.CommandText = $@"
                    INSERT OR IGNORE INTO seeds (seed)
                    SELECT seed FROM search_db.results ORDER BY score DESC LIMIT {limit}";
                var inserted = insertCmd.ExecuteNonQuery();

                // Detach when done
                using var detachCmd = _fertilizerConnection.CreateCommand();
                detachCmd.CommandText = "DETACH search_db";
                detachCmd.ExecuteNonQuery();

                _logCallback($"[{DateTime.Now:HH:mm:ss}] Dumped up to {limit} seeds to fertilizer DB (INSERT INTO SELECT - zero C# memory!)");
            }
            catch (Exception ex)
            {
                _logCallback($"[{DateTime.Now:HH:mm:ss}] Fertilizer dump warning: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Checkpoint fertilizer DB to persist changes
    /// </summary>
    private void CheckpointFertilizer()
    {
        lock (_fertilizerLock)
        {
            if (_fertilizerConnection == null) return;
            try
            {
                using var cmd = _fertilizerConnection.CreateCommand();
                cmd.CommandText = "CHECKPOINT";
                cmd.ExecuteNonQuery();
            }
            catch { /* ignore */ }
        }
    }

    private void LoadSavedFilters()
    {
        try
        {
            // Clear existing filters to prevent duplicates on API restart
            _savedSearches.Clear();

            var jamlFiles = Directory.GetFiles(_filtersDir, "*.jaml");
            foreach (var file in jamlFiles)
            {
                var jaml = File.ReadAllText(file);

                // Parse the JAML to extract name, deck, stake - same logic as POST /search
                if (!JamlConfigLoader.TryLoadFromJamlString(jaml, out var config, out var parseError))
                {
                    _logCallback($"[{DateTime.Now:HH:mm:ss}] ❌ Failed to parse {Path.GetFileName(file)}:\n    {parseError}");
                    continue;
                }

                // Use same searchId generation as POST /search for consistency
                var filterName = GetFilterName(config!);
                var deck = GetDeckFromConfig(config!);
                var stake = GetStakeFromConfig(config!);
                var searchId = SanitizeSearchId($"{filterName}_{deck}_{stake}");

                _savedSearches[searchId] = new SavedSearch
                {
                    Id = searchId,
                    FilterJaml = jaml,
                    Deck = deck,
                    Stake = stake,
                    Timestamp = File.GetLastWriteTimeUtc(file).Ticks
                };
            }

            _logCallback($"[{DateTime.Now:HH:mm:ss}] Loaded {jamlFiles.Length} saved filters");
        }
        catch (Exception ex)
        {
            _logCallback($"[{DateTime.Now:HH:mm:ss}] Failed to load saved filters: {ex.Message}");
        }
    }

    private void ConvertJsonFiltersToJaml()
    {
        var jsonFiltersDir = "JsonFilters";
        if (!Directory.Exists(jsonFiltersDir))
        {
            _logCallback($"[{DateTime.Now:HH:mm:ss}] No JsonFilters directory found, skipping conversion");
            return;
        }

        var jsonFiles = Directory.GetFiles(jsonFiltersDir, "*.json");
        var converted = 0;
        var skipped = 0;

        foreach (var jsonPath in jsonFiles)
        {
            try
            {
                var jsonContent = File.ReadAllText(jsonPath);
                var config = ConfigFormatConverter.LoadFromJsonString(jsonContent);

                if (config == null)
                {
                    skipped++;
                    continue;
                }

                var jaml = config.SaveAsJaml();
                var baseName = Path.GetFileNameWithoutExtension(jsonPath);
                var jamlPath = Path.Combine(_filtersDir, $"{baseName}.jaml");

                // Only write if JAML doesn't already exist (don't overwrite user edits)
                if (!File.Exists(jamlPath))
                {
                    File.WriteAllText(jamlPath, jaml);
                    converted++;
                }
            }
            catch (Exception ex)
            {
                _logCallback($"[{DateTime.Now:HH:mm:ss}] Failed to convert {Path.GetFileName(jsonPath)}: {ex.Message}");
                skipped++;
            }
        }

        if (converted > 0 || skipped > 0)
        {
            _logCallback($"[{DateTime.Now:HH:mm:ss}] JSON→JAML: {converted} converted, {skipped} skipped, {jsonFiles.Length} total");
        }
    }

    private void SaveFilter(string searchId, string jaml)
    {
        try
        {
            // Extract just the filter name (without deck/stake) for the filename
            if (!JamlConfigLoader.TryLoadFromJamlString(jaml, out var config, out var parseError))
            {
                _logCallback($"[{DateTime.Now:HH:mm:ss}] Failed to parse JAML for saving filter: {parseError}");
                return;
            }

            var filterName = GetFilterName(config!);
            var filePath = Path.Combine(_filtersDir, $"{filterName}.jaml");
            File.WriteAllText(filePath, jaml);
            _logCallback($"[{DateTime.Now:HH:mm:ss}] Saved filter: {filterName}");
        }
        catch (Exception ex)
        {
            _logCallback($"[{DateTime.Now:HH:mm:ss}] Failed to save filter: {ex.Message}");
        }
    }

    /// <summary>
    /// Get sanitized filter name from config for use in searchId/filenames
    /// </summary>
    private static string GetFilterName(MotelyJsonConfig config)
    {
        // Direct property access - this is how configs work in 2025!
        if (!string.IsNullOrWhiteSpace(config.Name))
            return SanitizeFilterName(config.Name);

        // Fallback: generate from first clause
        var firstClause = config.Must?.FirstOrDefault() ?? config.Should?.FirstOrDefault();
        if (firstClause != null)
            return SanitizeFilterName($"{firstClause.Type}_{firstClause.Value}");

        return "UnnamedFilter";
    }

    private static string SanitizeFilterName(string name)
    {
        // Remove invalid filename characters and limit length
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalid.Contains(c) && c != ' ').ToArray());
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }

    /// <summary>
    /// Get deck from parsed config (uses MotelyJsonConfig.Deck property)
    /// </summary>
    private static string GetDeckFromConfig(MotelyJsonConfig config)
        => config.Deck ?? "Red";

    /// <summary>
    /// Get stake from parsed config (uses MotelyJsonConfig.Stake property)
    /// </summary>
    private static string GetStakeFromConfig(MotelyJsonConfig config)
        => config.Stake ?? "White";

    private static string SanitizeSearchId(string id)
    {
        var invalid = Path.GetInvalidFileNameChars();
        foreach (var c in invalid)
        {
            id = id.Replace(c, '-');
        }
        return id.Replace(',', '-').Replace(' ', '-');
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        try
        {
            // Enable CORS with comprehensive headers
            // Allow specific trusted domains (Cloudflare tunnels, genie app, localhost, and wildcard for dev)
            var origin = request.Headers["Origin"];
            var allowedOrigins = new[] { "*.8pi.me", "*.trycloudflare.com", "balatrogenie.app", "www.balatrogenie.app", "localhost", "127.0.0.1" };
            var isAllowedOrigin = !string.IsNullOrEmpty(origin) && allowedOrigins.Any(allowed =>
                allowed.StartsWith('*') ? origin.EndsWith(allowed[1..]) : origin.Contains(allowed));

            response.AddHeader("Access-Control-Allow-Origin", isAllowedOrigin ? origin! : "*");
            response.AddHeader("Access-Control-Allow-Methods", "GET, POST, DELETE, OPTIONS");
            response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Requested-With");
            if (isAllowedOrigin)
            {
                response.AddHeader("Access-Control-Allow-Credentials", "true");
            }

            // Allow cross-origin resource loading for Monaco editor and other CDN resources
            response.AddHeader("Cross-Origin-Embedder-Policy", "unsafe-none");
            response.AddHeader("Cross-Origin-Opener-Policy", "unsafe-none");
            response.AddHeader("Cross-Origin-Resource-Policy", "cross-origin");

            // Content Security Policy - allow CDN for Monaco + Blueprint iframe!
            response.AddHeader("Content-Security-Policy",
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net blob:; " +
                "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
                "font-src 'self' https://cdn.jsdelivr.net data:; " +
                "img-src 'self' data: https:; " +
                "connect-src 'self' https://cdn.jsdelivr.net blob:; " +
                "frame-src https://miaklwalker.github.io; " +
                "child-src 'self' blob: https://cdn.jsdelivr.net; " +
                "worker-src 'self' blob: https://cdn.jsdelivr.net;");

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 204;
                response.Close();
                return;
            }

            var path = request.Url?.AbsolutePath ?? "/";
            _logCallback($"[{DateTime.Now:HH:mm:ss}] {request.HttpMethod} {path}");

            if (request.HttpMethod == "GET" && path == "/")
            {
                await HandleIndexAsync(response);
            }
            else if (request.HttpMethod == "GET" && path == "/styles.css")
            {
                await ServeFileAsync(response, "wwwroot/styles.css", "text/css");
            }
            else if (request.HttpMethod == "GET" && path == "/script.js")
            {
                await ServeFileAsync(response, "wwwroot/script.js", "application/javascript");
            }
            else if (request.HttpMethod == "GET" && path.StartsWith("/monaco-editor/"))
            {
                // Serve Monaco editor files (bundled locally)
                var monacoPath = "wwwroot" + path.Replace("/", Path.DirectorySeparatorChar.ToString());
                var contentType = path.EndsWith(".js") ? "application/javascript" :
                                 path.EndsWith(".css") ? "text/css" :
                                 path.EndsWith(".ttf") ? "font/ttf" :
                                 path.EndsWith(".json") ? "application/json" :
                                 "application/octet-stream";
                await ServeFileAsync(response, monacoPath, contentType);
            }
            else if (path.StartsWith("/.well-known/"))
            {
                response.StatusCode = 404;
                response.Close();
            }
            else if (request.HttpMethod == "POST" && path == "/search")
            {
                response.ContentType = "application/json";
                await HandleSearchAsync(request, response);
            }
            else if (request.HttpMethod == "GET" && path == "/search")
            {
                response.ContentType = "application/json";
                await HandleSearchGetAsync(request, response);
            }
            else if (request.HttpMethod == "POST" && path == "/search/continue")
            {
                response.ContentType = "application/json";
                await HandleSearchContinueAsync(request, response);
            }
            else if (request.HttpMethod == "POST" && path == "/search/stop")
            {
                response.ContentType = "application/json";
                await HandleSearchStopAsync(request, response);
            }
            else if (request.HttpMethod == "POST" && path == "/analyze")
            {
                response.ContentType = "application/json";
                await HandleAnalyzeAsync(request, response);
            }
            else if (request.HttpMethod == "POST" && path == "/convert")
            {
                response.ContentType = "application/json";
                await HandleConvertAsync(request, response);
            }
            else if (request.HttpMethod == "GET" && path == "/filters")
            {
                response.ContentType = "application/json";
                await HandleFiltersGetAsync(response);
            }
            else if (request.HttpMethod == "DELETE" && path == "/search")
            {
                response.ContentType = "application/json";
                await HandleSearchDeleteAsync(request, response);
            }
            else if (request.HttpMethod == "DELETE" && path.StartsWith("/filters/"))
            {
                response.ContentType = "application/json";
                await HandleFilterDeleteAsync(request, response, path);
            }
            else
            {
                response.ContentType = "application/json";
                response.StatusCode = 404;
                await WriteJsonAsync(response, new { error = "Not Found" });
            }
        }
        catch (Exception ex)
        {
            _logCallback($"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}");
            response.StatusCode = 500;
            await WriteJsonAsync(response, new { error = ex.Message });
        }
    }

    private async Task HandleIndexAsync(HttpListenerResponse response)
    {
        await ServeFileAsync(response, "wwwroot/index.html", "text/html");
    }

    private async Task HandleSearchAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        // CRITICAL: Only ONE search can run at a time (SIMD/CPU constraint)
        // Stop any running search first, dump seeds to fertilizer, save batch position
        await StopRunningSearchAsync();

        using var reader = new StreamReader(request.InputStream);
        var body = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(body))
        {
            response.StatusCode = 400;
            await WriteJsonAsync(response, new { error = "Request body cannot be empty" });
            return;
        }

        var searchRequest = JsonSerializer.Deserialize<SearchRequest>(body);
        var filterJaml = searchRequest?.FilterJaml;
        var seedCount = searchRequest?.SeedCount ?? 1000000;

        if (string.IsNullOrWhiteSpace(filterJaml))
        {
            response.StatusCode = 400;
            await WriteJsonAsync(response, new { error = "filterJaml is required" });
            return;
        }

        if (!JamlConfigLoader.TryLoadFromJamlString(filterJaml, out var config, out var loadError))
        {
            response.StatusCode = 400;
            await WriteJsonAsync(response, new { error = $"Invalid JAML: {loadError}" });
            return;
        }

        // Extract filter name, deck, stake from parsed config
        var filterName = GetFilterName(config!);
        var deck = GetDeckFromConfig(config!);
        var stake = GetStakeFromConfig(config!);
        var searchId = SanitizeSearchId($"{filterName}_{deck}_{stake}");

        var isUpdated = _savedSearches.TryGetValue(searchId, out var existingSearch)
            && existingSearch.FilterJaml.Trim() != filterJaml.Trim();

        // If filter changed, reset the background search state AND delete stale DB
        if (isUpdated)
        {
            if (_currentSearchId == searchId && _currentSearch != null)
            {
                _currentSearch.StartBatch = 0;
                _currentSearch.SeedsAdded = 0;
                _currentSearch.IsRunning = false;
            }

            // Delete stale DB file - filter changed so old results are invalid
            var staleDbPath = Path.Combine(_searchResultsDir, $"{searchId}.db");
            try
            {
                if (File.Exists(staleDbPath)) File.Delete(staleDbPath);
                if (File.Exists(staleDbPath + ".wal")) File.Delete(staleDbPath + ".wal");
            }
            catch (Exception ex)
            {
                _logCallback($"[{DateTime.Now:HH:mm:ss}] Warning: Could not delete stale DB: {ex.Message}");
            }

            _logCallback($"[{DateTime.Now:HH:mm:ss}] Filter updated - cleared stale results, starting fresh");
        }

        // Track if filter was updated so we skip loading stale batch position from DB
        var filterWasUpdated = isUpdated;

        _savedSearches[searchId] = new SavedSearch
        {
            Id = searchId,
            FilterJaml = filterJaml,
            Deck = deck,
            Stake = stake,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        SaveFilter(searchId, filterJaml);

        // NOTE: Running search already stopped at start of this handler via StopRunningSearchAsync()

        try
        {
            var bgConfig = config!;
            var requestedBatchSize = searchRequest?.BatchSize ?? 2; // Default batch size
            _logCallback($"[{DateTime.Now:HH:mm:ss}] 🔧 Search settings: batchSize={requestedBatchSize}, threads={ThreadCount}, cutoff={searchRequest?.Cutoff ?? 0}");

            // Create or reuse background state for this search
            var bgState = (_currentSearchId == searchId && _currentSearch != null)
                ? _currentSearch
                : new BackgroundSearchState { StartBatch = 0, SeedsAdded = 0 };

            _currentSearch = bgState;
            _currentSearchId = searchId;

            // Set up DuckDB connection FIRST (before fertilizer search so we can save results)
            var dbPath = Path.Combine(_searchResultsDir, $"{searchId}.db");
            _logCallback($"[{DateTime.Now:HH:mm:ss}] Creating DB at: {Path.GetFullPath(dbPath)}");

            // Calculate expected tally columns for schema check
            var columnNames = config!.GetColumnNames();
            var tallyColumns = columnNames.Skip(2).ToList(); // Skip 'seed' and 'score'
            var expectedColumnCount = 2 + tallyColumns.Count; // seed + score + tallies

            // If DB exists, salvage seeds before potentially recreating
            if (File.Exists(dbPath))
            {
                try
                {
                    using var checkConn = new DuckDBConnection($"Data Source={dbPath}");
                    checkConn.Open();

                    // Check if results table exists and get column count
                    using var checkCmd = checkConn.CreateCommand();
                    checkCmd.CommandText = "SELECT COUNT(*) FROM information_schema.columns WHERE table_name = 'results'";
                    var actualColumnCount = Convert.ToInt32(checkCmd.ExecuteScalar());

                    if (actualColumnCount > 0 && actualColumnCount != expectedColumnCount)
                    {
                        _logCallback($"[{DateTime.Now:HH:mm:ss}] Schema mismatch! DB has {actualColumnCount} columns, need {expectedColumnCount}. Salvaging seeds...");

                        // Salvage seeds from old table to fertilizer DB before dropping
                        // Use INSERT INTO SELECT via ATTACH for zero C# memory usage
                        var salvageCount = 0;
                        try
                        {
                            var fertilizerFullPath = Path.GetFullPath(_fertilizerDbPath);
                            using var attachCmd = checkConn.CreateCommand();
                            attachCmd.CommandText = $"ATTACH '{fertilizerFullPath}' AS fertilizer_db";
                            attachCmd.ExecuteNonQuery();

                            using var salvageCmd = checkConn.CreateCommand();
                            salvageCmd.CommandText = "INSERT OR IGNORE INTO fertilizer_db.seeds (seed) SELECT seed FROM results";
                            salvageCount = salvageCmd.ExecuteNonQuery();

                            using var detachCmd = checkConn.CreateCommand();
                            detachCmd.CommandText = "DETACH fertilizer_db";
                            detachCmd.ExecuteNonQuery();
                        }
                        catch (Exception salvageEx)
                        {
                            _logCallback($"[{DateTime.Now:HH:mm:ss}] Salvage warning: {salvageEx.Message}");
                        }
                        _logCallback($"[{DateTime.Now:HH:mm:ss}] Salvaged {salvageCount} seeds to fertilizer DB");

                        // Drop old table so it gets recreated with correct schema
                        using var dropCmd = checkConn.CreateCommand();
                        dropCmd.CommandText = "DROP TABLE IF EXISTS results";
                        dropCmd.ExecuteNonQuery();
                        _logCallback($"[{DateTime.Now:HH:mm:ss}] Dropped old results table");
                    }
                    checkConn.Close();
                }
                catch (Exception ex)
                {
                    _logCallback($"[{DateTime.Now:HH:mm:ss}] Warning: Could not check/salvage old DB: {ex.Message}");
                }
            }

            // Create database with clean abstraction (dual read/write connections!)
            bgState.Database = new MotelySearchDatabase(dbPath, columnNames);

            // Load persisted batch position (or reset if filter/batch size changed)
            if (!filterWasUpdated)
            {
                var (savedBatch, savedSize) = bgState.Database.GetLastBatchPosition();
                if (savedBatch.HasValue && savedSize.HasValue)
                {
                    if (savedSize.Value != requestedBatchSize)
                    {
                        _logCallback($"[{DateTime.Now:HH:mm:ss}] Batch size changed from {savedSize.Value} to {requestedBatchSize} - clearing search_state!");
                        bgState.StartBatch = 0;
                    }
                    else
                    {
                        bgState.StartBatch = savedBatch.Value;
                        _logCallback($"[{DateTime.Now:HH:mm:ss}] Restored batch position: {bgState.StartBatch}");
                    }
                }
            }
            else
            {
                bgState.StartBatch = 0;
                _logCallback($"[{DateTime.Now:HH:mm:ss}] Filter changed - starting from batch 0");
            }

            // Allow user override of start batch
            if (searchRequest?.StartBatch.HasValue == true)
            {
                bgState.StartBatch = searchRequest.StartBatch.Value;
                _logCallback($"[{DateTime.Now:HH:mm:ss}] USER OVERRIDE: Starting at batch {bgState.StartBatch}");
                bgState.Database.SaveBatchPosition(bgState.StartBatch, requestedBatchSize);
                _logCallback($"[{DateTime.Now:HH:mm:ss}] Saved override batch position {bgState.StartBatch} with batch_size={requestedBatchSize} to DB");
            }

            // Validate StartBatch is within range
            const long maxBatches = 1_838_265_625;
            if (bgState.StartBatch >= maxBatches)
            {
                _logCallback($"[{DateTime.Now:HH:mm:ss}] StartBatch {bgState.StartBatch} is beyond max {maxBatches} - resetting to 0");
                bgState.StartBatch = 0;
            }

            // ========== FERTILIZER SEARCH ==========
            // Run this on EVERY search (new or continue) to get instant results from known good seeds
            // Uses DbList param to read directly from fertilizer.db - no permanent in-memory storage!
            var fertilizerDbFullPath = Path.GetFullPath(_fertilizerDbPath);
            var fertilizerCount = GetFertilizerCount();

            var results = new List<SearchResult>();

            if (fertilizerCount > 0 && File.Exists(fertilizerDbFullPath))
            {
                _logCallback($"[{DateTime.Now:HH:mm:ss}] Starting fertilizer search with {fertilizerCount} seeds from {_fertilizerDbPath}...");

                var pileParams = new JsonSearchParams
                {
                    Threads = ThreadCount,
                    EnableDebug = false,
                    NoFancy = true,
                    Quiet = true,
                    DbList = fertilizerDbFullPath, // Use DuckDB directly instead of SeedList!
                    AutoCutoff = false,
                    Cutoff = 1,
                };

                Action<MotelySeedScoreTally> pileCallback = (tally) =>
                {
                    lock (results)
                    {
                        results.Add(new SearchResult
                        {
                            Seed = tally.Seed,
                            Score = tally.Score,
                            Tallies = tally.TallyColumns
                        });
                    }
                };

                var pileExecutor = new JsonSearchExecutor(config!, pileParams, pileCallback);
                pileExecutor.Execute();

                _logCallback($"[{DateTime.Now:HH:mm:ss}] Fertilizer search: {results.Count} matched from {fertilizerCount} in pile");
            }

            var topResults = results.OrderByDescending(r => r.Score).Take(1000).ToList();

            // SAVE FERTILIZER RESULTS TO DB using clean database API
            foreach (var result in topResults)
            {
                bgState.Database.InsertResult(result);
                bgState.SeedsAdded++;
            }
            _logCallback($"[{DateTime.Now:HH:mm:ss}] Inserted {topResults.Count} fertilizer results to DB");

            // Also add fertilizer results to the fertilizer DB (they're good seeds!)
            foreach (var result in topResults)
            {
                AddSeedToFertilizer(result.Seed);
            }

            // Get pile size from DB
            var pileSize = GetFertilizerCount();

            // Cutoff logic:
            // - User override takes priority (allows explicit 0 to accept everything)
            // - Otherwise smart cutoff from fertilizer results:
            //   - No results = rare filter, accept everything (cutoff = 0)
            //   - Has results = use 10th best score as cutoff threshold
            int effectiveCutoff;
            if (searchRequest?.Cutoff.HasValue == true)
            {
                effectiveCutoff = searchRequest.Cutoff.Value;
                _logCallback($"[{DateTime.Now:HH:mm:ss}] USER CUTOFF OVERRIDE: {effectiveCutoff}");
            }
            else
            {
                effectiveCutoff = 0;
                if (topResults.Count >= 10)
                {
                    effectiveCutoff = topResults[9].Score; // 10th best (0-indexed, already sorted desc)
                }
                else if (topResults.Count > 0)
                {
                    effectiveCutoff = topResults[topResults.Count - 1].Score; // Worst of what we found
                }
                _logCallback($"[{DateTime.Now:HH:mm:ss}] Smart cutoff: {effectiveCutoff} (from {topResults.Count} fertilizer results)");
            }

            // Store effective cutoff in state so GET /search can return it
            bgState.EffectiveCutoff = effectiveCutoff;

            // Mark as running BEFORE sending response
            bgState.IsRunning = true;

            response.StatusCode = 200;
            await WriteJsonAsync(response, new
            {
                searchId = searchId,
                results = topResults,
                total = results.Count,
                columns = config!.GetColumnNames(),
                pileSize = pileSize,
                isBackgroundRunning = true // We JUST started it!
            });

            _logCallback($"[{DateTime.Now:HH:mm:ss}] Immediate response sent with {topResults.Count} results");

            bgState.BatchSize = requestedBatchSize; // Store in state for later use

            _ = Task.Run(() =>
            {
                try
                {
                    var bgExecutor = new JsonSearchExecutor(bgConfig, new JsonSearchParams
                    {
                        Threads = ThreadCount,
                        EnableDebug = false,
                        NoFancy = true,
                        Quiet = true,
                        BatchSize = requestedBatchSize, // Use batch size from request or default (2)
                        StartBatch = (ulong)bgState.StartBatch,
                        EndBatch = searchRequest?.EndBatch ?? 0, // User-specified end batch or no limit
                        AutoCutoff = false,
                        Cutoff = effectiveCutoff, // User override or smart cutoff from fertilizer results
                        ProgressCallback = (completed, total, seedsSearched, seedsPerMs) =>
                        {
                            // Update progress state for GET /search to read
                            var newBatch = bgState.StartBatch + completed;
                            bgState.CurrentBatch = newBatch;
                            bgState.TotalBatches = bgState.StartBatch + total;
                            bgState.SeedsSearched = seedsSearched;
                            bgState.SeedsPerMs = seedsPerMs;

                            // POWER OUTAGE PROTECTION: Save batch position every callback
                            if (bgState.Database != null)
                            {
                                try
                                {
                                    bgState.Database.SaveBatchPosition(newBatch, bgState.BatchSize);
                                }
                                catch (Exception ex)
                                {
                                    _logCallback($"[{DateTime.Now:HH:mm:ss}] Progress save warning: {ex.Message}");
                                }
                            }
                        }
                    }, (tally) => {
                        // Track seeds found regardless of save success
                        bgState.SeedsAdded++;

                        // Add to fertilizer DB (persists across restarts!)
                        AddSeedToFertilizer(tally.Seed);

                        _logCallback($"[{DateTime.Now:HH:mm:ss}] Found seed: {tally.Seed} (score: {tally.Score})");

                        // Skip DB save if search stopped
                        if (!bgState.IsRunning || bgState.Database == null) return;

                        // Insert result using clean database API (thread-safe internally!)
                        try
                        {
                            bgState.Database.InsertResult(new SearchResult
                            {
                                Seed = tally.Seed,
                                Score = tally.Score,
                                Tallies = tally.TallyColumns?.ToList()
                            });
                        }
                        catch (Exception ex)
                        {
                            if (!ex.Message.Contains("closed") && !ex.Message.Contains("disposed"))
                            {
                                _logCallback($"[{DateTime.Now:HH:mm:ss}] DB save warning: {ex.Message}");
                            }
                        }
                    });
                    
                    bgState.Search = bgExecutor;
                    bgState.CurrentBatch = bgState.StartBatch;
                    bgState.SeedsAdded = 0; // Reset counter for this run

                    // Execute without awaiting completion - it will run in background
                    bgExecutor.Execute(awaitCompletion: false);

                    _logCallback($"[{DateTime.Now:HH:mm:ss}] Background search started for {searchId} from batch {bgState.StartBatch}");

                    // Background search will continue running until cancelled or completed
                }
                catch (Exception ex)
                {
                    _logCallback($"[{DateTime.Now:HH:mm:ss}] Background search error: {ex.Message}");
                    if (_currentSearch != null)
                    {
                        _currentSearch.IsRunning = false;
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logCallback($"[{DateTime.Now:HH:mm:ss}] Search failed: {ex.Message}");
            response.StatusCode = 500;
            await WriteJsonAsync(response, new { error = ex.Message });
        }
    }

    private async Task HandleSearchGetAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        try
        {
            var searchId = request.QueryString["id"];
            
            if (string.IsNullOrEmpty(searchId))
            {
                response.StatusCode = 400;
                await WriteJsonAsync(response, new { error = "search id required" });
                return;
            }

            if (!_savedSearches.TryGetValue(searchId, out var savedSearch))
            {
                response.StatusCode = 404;
                await WriteJsonAsync(response, new { error = "search not found" });
                return;
            }

            if (!JamlConfigLoader.TryLoadFromJamlString(savedSearch.FilterJaml, out var config, out var loadError))
            {
                response.StatusCode = 400;
                await WriteJsonAsync(response, new { error = $"Invalid JAML: {loadError}" });
                return;
            }

            // Get results from DuckDB database
            var results = new List<SearchResult>();
            var dbPath = Path.Combine(_searchResultsDir, $"{searchId}.db");

            // If THIS search is running, ask it to query its own connection (safe!)
            if (_currentSearchId == searchId && _currentSearch?.IsRunning == true)
            {
                results = _currentSearch.GetTopResults(1000);
            }

            // FALLBACK: If running search returned empty OR search not running, try the file
            if (results.Count == 0 && File.Exists(dbPath))
            {
                results = GetTopResultsFromDb(dbPath, 1000);
            }

            // Check if THIS search is running
            var isRunning = _currentSearchId == searchId && _currentSearch?.IsRunning == true;
            long currentBatch = 0;
            long totalBatches = 0;
            long seedsSearched = 0;
            double seedsPerMs = 0;
            long totalSeedsFound = results.Count; // Default to results count, but try to get actual total
            if (_currentSearchId == searchId && _currentSearch != null)
            {
                currentBatch = _currentSearch.CurrentBatch;
                totalBatches = _currentSearch.TotalBatches;
                seedsSearched = _currentSearch.SeedsSearched;
                seedsPerMs = _currentSearch.SeedsPerMs;
            }

            // Get actual total count from DB (not capped at 1000)
            if (_currentSearchId == searchId && _currentSearch?.Database != null)
            {
                try
                {
                    totalSeedsFound = _currentSearch.Database.GetResultCount();
                }
                catch (Exception ex)
                {
                    _logCallback($"[{DateTime.Now:HH:mm:ss}] Warning: Could not count results: {ex.Message}");
                }
            }

            // If no in-memory batch position, try to load from DuckDB (survives server restart)
            if (currentBatch == 0 && File.Exists(dbPath))
            {
                try
                {
                    using var batchConn = new DuckDBConnection($"Data Source={dbPath}");
                    batchConn.Open();
                    using var batchCmd = batchConn.CreateCommand();
                    batchCmd.CommandText = "SELECT last_completed_batch FROM search_state WHERE id = 1";
                    var savedBatch = batchCmd.ExecuteScalar();
                    if (savedBatch != null && savedBatch != DBNull.Value)
                    {
                        currentBatch = Convert.ToInt64(savedBatch);
                    }
                }
                catch (Exception ex)
                {
                    _logCallback($"[{DateTime.Now:HH:mm:ss}] Warning: Could not read batch from DB: {ex.Message}");
                }
            }

            // Determine status
            var status = isRunning ? "RUNNING" : "STOPPED";
            var columnNames = config!.GetColumnNames();

            // Log search status with useful info
            var speedStr = seedsPerMs >= 1000 ? $"{seedsPerMs / 1000:F1}M/s"
                : seedsPerMs > 0 ? $"{seedsPerMs * 1000:F0}/s" : "-";
            var searchedStr = seedsSearched >= 1000000 ? $"{seedsSearched / 1000000.0:F1}M"
                : seedsSearched > 0 ? $"{seedsSearched / 1000.0:F1}K" : "0";
            _logCallback($"[{DateTime.Now:HH:mm:ss}] GET /search: {status} | batch {currentBatch}/{456976} | {searchedStr} searched | {totalSeedsFound} found | {speedStr}");

            // Get effective cutoff from current search state
            var effectiveCutoff = (_currentSearchId == searchId && _currentSearch != null)
                ? _currentSearch.EffectiveCutoff
                : 0;

            response.StatusCode = 200;
            await WriteJsonAsync(response, new
            {
                searchId = searchId,
                filterJaml = savedSearch.FilterJaml,
                deck = savedSearch.Deck,
                stake = savedSearch.Stake,
                results = results,
                total = totalSeedsFound, // Actual count from DB (not capped)
                columns = columnNames,
                status = status,
                currentBatch = currentBatch,
                totalBatches = totalBatches,
                seedsSearched = seedsSearched,
                seedsPerSecond = seedsPerMs * 1000, // Convert to per-second for UI
                seedsFound = totalSeedsFound, // Actual count from DB (not capped)
                isBackgroundRunning = isRunning,
                cutoff = effectiveCutoff // Return cutoff so UI can populate the field
            });
        }
        catch (Exception ex)
        {
            _logCallback($"[{DateTime.Now:HH:mm:ss}] GET Search failed: {ex.Message}");
            response.StatusCode = 500;
            await WriteJsonAsync(response, new { error = ex.Message });
        }
    }

    private async Task HandleSearchContinueAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        try
        {
            using var reader = new StreamReader(request.InputStream);
            var body = await reader.ReadToEndAsync();
            var requestData = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
            
            if (!requestData!.TryGetValue("searchId", out var searchIdObj))
            {
                response.StatusCode = 400;
                await WriteJsonAsync(response, new { error = "searchId required" });
                return;
            }
            
            var searchId = searchIdObj.ToString()!;

            if (_currentSearchId != searchId || _currentSearch == null)
            {
                response.StatusCode = 404;
                await WriteJsonAsync(response, new { error = "background search not found" });
                return;
            }

            if (_currentSearch.IsRunning)
            {
                response.StatusCode = 400;
                await WriteJsonAsync(response, new { error = "search already running" });
                return;
            }

            // Restart the search
            _currentSearch.IsRunning = true;
            _currentSearch.Search?.Execute(awaitCompletion: false);

            _logCallback($"[{DateTime.Now:HH:mm:ss}] Continued search for {searchId}");

            response.StatusCode = 200;
            await WriteJsonAsync(response, new {
                message = "search continued",
                searchId = searchId,
                status = "running"
            });
        }
        catch (Exception ex)
        {
            _logCallback($"[{DateTime.Now:HH:mm:ss}] Continue search failed: {ex.Message}");
            response.StatusCode = 500;
            await WriteJsonAsync(response, new { error = ex.Message });
        }
    }

    private async Task HandleSearchStopAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        try
        {
            using var reader = new StreamReader(request.InputStream);
            var body = await reader.ReadToEndAsync();
            var requestData = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
            
            if (!requestData!.TryGetValue("searchId", out var searchIdObj))
            {
                response.StatusCode = 400;
                await WriteJsonAsync(response, new { error = "searchId required" });
                return;
            }
            
            var searchId = searchIdObj.ToString()!;

            if (_currentSearchId != searchId || _currentSearch == null)
            {
                response.StatusCode = 404;
                await WriteJsonAsync(response, new { error = "background search not found" });
                return;
            }

            if (!_currentSearch.IsRunning)
            {
                response.StatusCode = 400;
                await WriteJsonAsync(response, new { error = "search not running" });
                return;
            }

            // Stop the search
            _currentSearch.IsRunning = false;
            _currentSearch.Search?.Cancel();

            // Save batch position and checkpoint
            try
            {
                if (_currentSearch.Database != null)
                {
                    _currentSearch.Database.SaveBatchPosition(_currentSearch.CurrentBatch, _currentSearch.BatchSize);
                    _currentSearch.Database.Checkpoint();
                    _logCallback($"[{DateTime.Now:HH:mm:ss}] Saved batch position {_currentSearch.CurrentBatch} to DB");
                    _currentSearch.StartBatch = _currentSearch.CurrentBatch;
                }
            }
            catch (Exception ex)
            {
                _logCallback($"[{DateTime.Now:HH:mm:ss}] Warning: Failed to save batch position: {ex.Message}");
            }

            _logCallback($"[{DateTime.Now:HH:mm:ss}] Stopped search for {searchId} at batch {_currentSearch.CurrentBatch}");

            response.StatusCode = 200;
            await WriteJsonAsync(response, new {
                message = "search stopped",
                searchId = searchId,
                status = "stopped",
                currentBatch = _currentSearch.CurrentBatch
            });
        }
        catch (Exception ex)
        {
            _logCallback($"[{DateTime.Now:HH:mm:ss}] Stop search failed: {ex.Message}");
            response.StatusCode = 500;
            await WriteJsonAsync(response, new { error = ex.Message });
        }
    }

    private async Task HandleAnalyzeAsync(
        HttpListenerRequest request,
        HttpListenerResponse response
    )
    {
        using var reader = new StreamReader(request.InputStream);
        var body = await reader.ReadToEndAsync();

        var analyzeRequest = JsonSerializer.Deserialize<AnalyzeRequest>(
            body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        if (analyzeRequest == null || string.IsNullOrWhiteSpace(analyzeRequest.Seed))
        {
            response.StatusCode = 400;
            await WriteJsonAsync(response, new { error = "seed is required" });
            return;
        }

        try
        {
            var deck =
                string.IsNullOrEmpty(analyzeRequest.Deck)
                || !Enum.TryParse<MotelyDeck>(analyzeRequest.Deck, true, out var d)
                    ? MotelyDeck.Red
                    : d;

            var stake =
                string.IsNullOrEmpty(analyzeRequest.Stake)
                || !Enum.TryParse<MotelyStake>(analyzeRequest.Stake, true, out var s)
                    ? MotelyStake.White
                    : s;

            var config = new MotelySeedAnalysisConfig(analyzeRequest.Seed, deck, stake);
            var analysis = MotelySeedAnalyzer.Analyze(config);

            response.StatusCode = 200;
            await WriteJsonAsync(
                response,
                new
                {
                    seed = analyzeRequest.Seed,
                    deck = deck.ToString(),
                    stake = stake.ToString(),
                    analysis = analysis.ToString(),
                }
            );

            _logCallback($"[{DateTime.Now:HH:mm:ss}] Analyzed seed {analyzeRequest.Seed}");
        }
        catch (Exception ex)
        {
            _logCallback($"[{DateTime.Now:HH:mm:ss}] Analyze failed: {ex.Message}");
            response.StatusCode = 500;
            await WriteJsonAsync(response, new { error = ex.Message });
        }
    }


    private async Task HandleConvertAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        using var reader = new StreamReader(request.InputStream);
        var body = await reader.ReadToEndAsync();

        var convertRequest = JsonSerializer.Deserialize<ConvertRequest>(
            body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        if (convertRequest == null || string.IsNullOrWhiteSpace(convertRequest.JsonContent))
        {
            response.StatusCode = 400;
            await WriteJsonAsync(response, new { error = "jsonContent is required" });
            return;
        }

        try
        {
            // Convert JSON to JAML using ConfigFormatConverter
            var config = ConfigFormatConverter.LoadFromJsonString(convertRequest.JsonContent);
            if (config == null)
            {
                response.StatusCode = 400;
                await WriteJsonAsync(response, new { error = "Invalid JSON filter format" });
                return;
            }

            var jaml = config.SaveAsJaml();

            response.StatusCode = 200;
            await WriteJsonAsync(response, new { jaml });

            _logCallback($"[{DateTime.Now:HH:mm:ss}] Converted JSON filter to JAML: {config.Name ?? "unnamed"}");
        }
        catch (Exception ex)
        {
            _logCallback($"[{DateTime.Now:HH:mm:ss}] Convert failed: {ex.Message}");
            response.StatusCode = 500;
            await WriteJsonAsync(response, new { error = ex.Message });
        }
    }

    private async Task HandleFiltersGetAsync(HttpListenerResponse response)
    {
        try
        {
            var filters = new List<object>();

            if (Directory.Exists(_filtersDir))
            {
                // Load both .jaml and .json files
                var allFiles = Directory.GetFiles(_filtersDir, "*.jaml")
                    .Concat(Directory.GetFiles(_filtersDir, "*.json"));

                foreach (var filePath in allFiles)
                {
                    var fileName = Path.GetFileName(filePath);
                    var content = await File.ReadAllTextAsync(filePath);

                    // Parse the filter and extract metadata from the config object
                    string? displayName = null;
                    string? searchId = null;

                    if (JamlConfigLoader.TryLoadFromJamlString(content, out var config, out var parseError))
                    {
                        displayName = GetFilterName(config!);
                        var deck = GetDeckFromConfig(config!);
                        var stake = GetStakeFromConfig(config!);
                        searchId = SanitizeSearchId($"{displayName}_{deck}_{stake}");
                    }
                    else
                    {
                        _logCallback($"[{DateTime.Now:HH:mm:ss}] ⚠️ Failed to parse {fileName}: {parseError}");
                    }

                    // Fallback to filename if parsing failed
                    displayName ??= Path.GetFileNameWithoutExtension(fileName);

                    filters.Add(new
                    {
                        name = displayName,
                        filePath = fileName,
                        filterJaml = content,
                        searchId = searchId // Client uses this directly!
                    });
                }
            }

            response.StatusCode = 200;
            await WriteJsonAsync(response, new
            {
                filters = filters,
                runningSearchId = _currentSearchId,
                isSearchRunning = _currentSearch?.IsRunning ?? false
            });
            _logCallback($"[{DateTime.Now:HH:mm:ss}] Returned {filters.Count} filter files, running: {_currentSearchId ?? "none"}");
        }
        catch (Exception ex)
        {
            _logCallback($"[{DateTime.Now:HH:mm:ss}] Get filters failed: {ex.Message}");
            response.StatusCode = 500;
            await WriteJsonAsync(response, new { error = ex.Message });
        }
    }

    private async Task HandleSearchDeleteAsync(HttpListenerRequest request, HttpListenerResponse response)
    {
        try
        {
            var query = request.Url?.Query ?? "";
            var searchId = "";
            
            // Parse search ID from query string
            if (query.StartsWith("?id="))
            {
                searchId = Uri.UnescapeDataString(query.Substring(4));
            }
            
            if (string.IsNullOrEmpty(searchId))
            {
                response.StatusCode = 400;
                await WriteJsonAsync(response, new { error = "Search ID required" });
                return;
            }
            
            // Remove from saved searches (safe - only removes from memory)
            if (_savedSearches.TryRemove(searchId, out var removedSearch))
            {
                _logCallback($"[{DateTime.Now:HH:mm:ss}] Deleted search: {searchId}");
                await WriteJsonAsync(response, new { success = true, message = $"Search {searchId} deleted" });
            }
            else
            {
                response.StatusCode = 404;
                await WriteJsonAsync(response, new { error = "Search not found" });
            }
        }
        catch (Exception ex)
        {
            _logCallback($"[{DateTime.Now:HH:mm:ss}] Delete search failed: {ex.Message}");
            response.StatusCode = 500;
            await WriteJsonAsync(response, new { error = ex.Message });
        }
    }

    private async Task HandleFilterDeleteAsync(HttpListenerRequest request, HttpListenerResponse response, string path)
    {
        try
        {
            // Extract filename safely - just the name part
            var fileName = path.Substring("/filters/".Length);
            
            // Validate: must be .jaml or .json and no path chars
            if (string.IsNullOrEmpty(fileName) || 
                (!fileName.EndsWith(".jaml") && !fileName.EndsWith(".json")) ||
                fileName.Contains("/") || 
                fileName.Contains("\\") || 
                fileName.Contains(".."))
            {
                response.StatusCode = 400;
                await WriteJsonAsync(response, new { error = "Invalid filter name" });
                return;
            }
            
            var filePath = Path.Combine(_filtersDir, fileName);
            
            if (!File.Exists(filePath))
            {
                response.StatusCode = 404;
                await WriteJsonAsync(response, new { error = "Filter not found" });
                return;
            }
            
            File.Delete(filePath);
            _logCallback($"[{DateTime.Now:HH:mm:ss}] Deleted filter file: {fileName}");
            
            await WriteJsonAsync(response, new { success = true, message = $"Filter {fileName} deleted" });
        }
        catch (Exception ex)
        {
            _logCallback($"[{DateTime.Now:HH:mm:ss}] Delete filter failed: {ex.Message}");
            response.StatusCode = 500;
            await WriteJsonAsync(response, new { error = ex.Message });
        }
    }

    private List<SearchResult> GetTopResultsFromDb(string dbPath, int limit)
    {
        if (!File.Exists(dbPath)) return new List<SearchResult>();
        
        try
        {
            using var conn = new DuckDBConnection($"Data Source={dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT * FROM results ORDER BY score DESC LIMIT {limit}";
            
            var results = new List<SearchResult>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var seed = reader.GetString(0);
                var score = reader.GetInt32(1);

                var tallies = new List<int>();
                for (int i = 2; i < reader.FieldCount; i++)
                {
                    tallies.Add(reader.IsDBNull(i) ? 0 : reader.GetInt32(i));
                }

                results.Add(new SearchResult
                {
                    Seed = seed,
                    Score = score,
                    Tallies = tallies
                });
            }
            
            return results;
        }
        catch (Exception ex)
        {
            _logCallback($"[{DateTime.Now:HH:mm:ss}] Failed to read from DB {dbPath}: {ex.Message}");
            return new List<SearchResult>();
        }
    }

    private async Task ServeFileAsync(HttpListenerResponse response, string filePath, string contentType)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                response.StatusCode = 404;
                await response.OutputStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes($"File not found: {filePath}"));
                return;
            }

            var content = await File.ReadAllTextAsync(filePath);
            response.ContentType = contentType;
            response.StatusCode = 200;
            
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            response.ContentLength64 = bytes.Length;
            await response.OutputStream.WriteAsync(bytes);
            response.Close();
        }
        catch (Exception ex)
        {
            _logCallback($"[{DateTime.Now:HH:mm:ss}] Failed to serve {filePath}: {ex.Message}");
            response.StatusCode = 500;
            await response.OutputStream.WriteAsync(System.Text.Encoding.UTF8.GetBytes("Server error"));
            response.Close();
        }
    }

    private static async Task WriteJsonAsync(HttpListenerResponse response, object data)
    {
        var json = JsonSerializer.Serialize(
            data,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
            }
        );

        var buffer = System.Text.Encoding.UTF8.GetBytes(json);
        response.ContentLength64 = buffer.Length;
        await response.OutputStream.WriteAsync(buffer);
        response.Close();
    }

}

public class SearchRequest
{
    [JsonPropertyName("filterJaml")]
    public string? FilterJaml { get; set; }

    [JsonPropertyName("seedCount")]
    public long SeedCount { get; set; }

    [JsonPropertyName("startBatch")]
    public long? StartBatch { get; set; }

    [JsonPropertyName("endBatch")]
    public ulong? EndBatch { get; set; }

    [JsonPropertyName("cutoff")]
    public int? Cutoff { get; set; }

    [JsonPropertyName("batchSize")]
    public int? BatchSize { get; set; }
}

public class SearchResult
{
    [JsonPropertyName("seed")]
    public string Seed { get; set; } = "";

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("tallies")]
    public List<int> Tallies { get; set; } = new();
}

public class AnalyzeRequest
{
    [JsonPropertyName("seed")]
    public string Seed { get; set; } = "";

    [JsonPropertyName("deck")]
    public string? Deck { get; set; }

    [JsonPropertyName("stake")]
    public string? Stake { get; set; }
}


public class ConvertRequest
{
    [JsonPropertyName("jsonContent")]
    public string JsonContent { get; set; } = "";
}

/// <summary>
/// Generates JAML filters from natural language prompts using keyword matching
/// </summary>
public static class JamlGenie
{
    // Legendary jokers (soulJoker type)
    private static readonly HashSet<string> SoulJokers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Canio", "Triboulet", "Yorick", "Chicot", "Perkeo"
    };

    // Rare jokers
    private static readonly HashSet<string> RareJokers = new(StringComparer.OrdinalIgnoreCase)
    {
        "DNA", "Vagabond", "Baron", "Obelisk", "BaseballCard", "AncientJoker", "Campfire",
        "Blueprint", "WeeJoker", "HitTheRoad", "TheDuo", "TheTrio", "TheFamily", "TheOrder",
        "TheTribe", "Stuntman", "InvisibleJoker", "Brainstorm", "DriversLicense", "BurntJoker"
    };

    // Uncommon jokers
    private static readonly HashSet<string> UncommonJokers = new(StringComparer.OrdinalIgnoreCase)
    {
        "JokerStencil", "FourFingers", "Mime", "CeremonialDagger", "MarbleJoker", "LoyaltyCard",
        "Dusk", "Fibonacci", "SteelJoker", "Hack", "Pareidolia", "SpaceJoker", "Burglar",
        "Blackboard", "SixthSense", "Constellation", "Hiker", "CardSharp", "Madness", "Seance",
        "Vampire", "Shortcut", "Hologram", "Cloud9", "Rocket", "MidasMask", "Luchador",
        "GiftCard", "TurtleBean", "Erosion", "ToTheMoon", "StoneJoker", "LuckyCat", "Bull",
        "DietCola", "TradingCard", "FlashCard", "SpareTrousers", "Ramen", "Seltzer", "Castle",
        "MrBones", "Acrobat", "SockAndBuskin", "Troubadour", "Certificate", "SmearedJoker",
        "Throwback", "RoughGem", "Bloodstone", "Arrowhead", "OnyxAgate", "GlassJoker", "Showman",
        "FlowerPot", "MerryAndy", "OopsAll6s", "TheIdol", "SeeingDouble", "Matador", "Satellite",
        "Cartomancer", "Astronomer", "Bootstraps"
    };

    // Common jokers
    private static readonly HashSet<string> CommonJokers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Joker", "GreedyJoker", "LustyJoker", "WrathfulJoker", "GluttonousJoker", "JollyJoker",
        "ZanyJoker", "MadJoker", "CrazyJoker", "DrollJoker", "SlyJoker", "WilyJoker", "CleverJoker",
        "DeviousJoker", "CraftyJoker", "HalfJoker", "CreditCard", "Banner", "MysticSummit",
        "EightBall", "Misprint", "RaisedFist", "ChaostheClown", "ScaryFace", "AbstractJoker",
        "DelayedGratification", "GrosMichel", "EvenSteven", "OddTodd", "Scholar", "BusinessCard",
        "Supernova", "RideTheBus", "Egg", "Runner", "IceCream", "Splash", "BlueJoker",
        "FacelessJoker", "GreenJoker", "Superposition", "ToDoList", "Cavendish", "RedCard",
        "SquareJoker", "RiffRaff", "Photograph", "ReservedParking", "MailInRebate", "Hallucination",
        "FortuneTeller", "Juggler", "Drunkard", "GoldenJoker", "Popcorn", "WalkieTalkie",
        "SmileyFace", "GoldenTicket", "Swashbuckler", "HangingChad", "ShootTheMoon"
    };

    // All jokers combined for easy lookup
    private static readonly HashSet<string> AllJokers = SoulJokers
        .Concat(RareJokers)
        .Concat(UncommonJokers)
        .Concat(CommonJokers)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    // Vouchers
    private static readonly HashSet<string> Vouchers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Overstock", "OverstockPlus", "ClearanceSale", "Liquidation", "Hone", "GlowUp",
        "RerollSurplus", "RerollGlut", "CrystalBall", "OmenGlobe", "Telescope", "Observatory",
        "Grabber", "NachoTong", "Wasteful", "Recyclomancy", "TarotMerchant", "TarotTycoon",
        "PlanetMerchant", "PlanetTycoon", "SeedMoney", "MoneyTree", "Blank", "Antimatter",
        "MagicTrick", "Illusion", "Hieroglyph", "Petroglyph", "DirectorsCut", "Retcon",
        "PaintBrush", "Palette"
    };

    // Tags
    private static readonly HashSet<string> Tags = new(StringComparer.OrdinalIgnoreCase)
    {
        "UncommonTag", "RareTag", "NegativeTag", "FoilTag", "HolographicTag", "PolychromeTag",
        "InvestmentTag", "VoucherTag", "BossTag", "StandardTag", "CharmTag", "MeteorTag",
        "BuffoonTag", "HandyTag", "GarbageTag", "EtherealTag", "CouponTag", "DoubleTag",
        "JuggleTag", "D6Tag", "TopupTag", "SpeedTag", "OrbitalTag", "EconomyTag"
    };

    // Tarot cards
    private static readonly HashSet<string> Tarots = new(StringComparer.OrdinalIgnoreCase)
    {
        "TheFool", "TheMagician", "TheHighPriestess", "TheEmpress", "TheEmperor", "TheHierophant",
        "TheLovers", "TheChariot", "Justice", "TheHermit", "TheWheelOfFortune", "Strength",
        "TheHangedMan", "Death", "Temperance", "TheDevil", "TheTower", "TheStar", "TheMoon",
        "TheSun", "Judgement", "TheWorld"
    };

    // Spectral cards
    private static readonly HashSet<string> Spectrals = new(StringComparer.OrdinalIgnoreCase)
    {
        "Familiar", "Grim", "Incantation", "Talisman", "Aura", "Wraith", "Sigil", "Ouija",
        "Ectoplasm", "Immolate", "Ankh", "DejaVu", "Hex", "Trance", "Medium", "Cryptid",
        "Soul", "BlackHole"
    };

    // Planet cards
    private static readonly HashSet<string> Planets = new(StringComparer.OrdinalIgnoreCase)
    {
        "Mercury", "Venus", "Earth", "Mars", "Jupiter", "Saturn", "Uranus", "Neptune",
        "Pluto", "PlanetX", "Ceres", "Eris"
    };

    // Boss blinds
    private static readonly HashSet<string> Bosses = new(StringComparer.OrdinalIgnoreCase)
    {
        "AmberAcorn", "CeruleanBell", "CrimsonHeart", "VerdantLeaf", "VioletVessel", "TheArm",
        "TheClub", "TheEye", "TheFish", "TheFlint", "TheGoad", "TheHead", "TheHook", "TheHouse",
        "TheManacle", "TheMark", "TheMouth", "TheNeedle", "TheOx", "ThePillar", "ThePlant",
        "ThePsychic", "TheSerpent", "TheTooth", "TheWall", "TheWater", "TheWheel", "TheWindow"
    };

    // Decks
    private static readonly HashSet<string> Decks = new(StringComparer.OrdinalIgnoreCase)
    {
        "Red", "Blue", "Yellow", "Green", "Black", "Magic", "Nebula", "Ghost", "Abandoned",
        "Checkered", "Zodiac", "Painted", "Anaglyph", "Plasma", "Erratic", "Challenge"
    };

    // Stakes
    private static readonly HashSet<string> Stakes = new(StringComparer.OrdinalIgnoreCase)
    {
        "White", "Red", "Green", "Black", "Blue", "Purple", "Orange", "Gold"
    };

    // Editions
    private static readonly HashSet<string> Editions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Foil", "Holo", "Polychrome", "Negative"
    };

    private static string BuildJaml(List<JamlClause> clauses, string deck, string stake, string prompt)
    {
        var sb = new System.Text.StringBuilder();

        // Determine if should vs must based on prompt keywords
        var useMust = prompt.Contains("must") || prompt.Contains("require") || prompt.Contains("need");
        var useShould = prompt.Contains("should") || prompt.Contains("prefer") || prompt.Contains("want") || prompt.Contains("score");

        // If neither specified, default to must for primary items
        if (!useMust && !useShould)
        {
            useMust = true;
        }

        if (useMust && clauses.Count > 0)
        {
            sb.AppendLine("must:");
            foreach (var clause in clauses)
            {
                sb.AppendLine($"  - {clause.Type}: {clause.Value}");
                if (clause.Edition != null)
                {
                    sb.AppendLine($"    edition: {clause.Edition}");
                }
                sb.AppendLine($"    antes: [{string.Join(", ", clause.Antes)}]");
            }
        }

        if (useShould && !useMust && clauses.Count > 0)
        {
            sb.AppendLine("should:");
            var score = 100;
            foreach (var clause in clauses)
            {
                sb.AppendLine($"  - {clause.Type}: {clause.Value}");
                if (clause.Edition != null)
                {
                    sb.AppendLine($"    edition: {clause.Edition}");
                }
                sb.AppendLine($"    antes: [{string.Join(", ", clause.Antes)}]");
                sb.AppendLine($"    score: {score}");
                score = Math.Max(10, score - 20);
            }
        }

        sb.AppendLine($"deck: {deck}");
        sb.AppendLine($"stake: {stake}");

        return sb.ToString().TrimEnd();
    }

    private static string GenerateHelpfulDefault(string prompt, string deck, string stake)
    {
        // Provide a helpful template with comments
        return $@"# Genie couldn't find specific items in your request.
# Try mentioning specific item names like:
#   - Jokers: Blueprint, Perkeo, Baron, DNA
#   - Vouchers: Telescope, Observatory, Antimatter
#   - Tags: NegativeTag, RareTag, PolychromeTag
#   - Editions: Negative, Polychrome, Foil, Holo
#
# Example: ""Find negative Perkeo in early antes with Telescope""

must:
  - voucher: Telescope
    antes: [1, 2]
should:
  - joker: Blueprint
    antes: [1, 2, 3]
    score: 50
deck: {deck}
stake: {stake}";
    }

    private class JamlClause
    {
        public string Type { get; set; } = "";
        public string Value { get; set; } = "";
        public string? Edition { get; set; }
        public List<int> Antes { get; set; } = new();
    }
}
