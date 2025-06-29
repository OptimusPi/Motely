# TheFool v0.4.0 - Console Runner & Status Bar Implementation Guide

## Overview
Add ConsoleRunnerWidget to execute MotelySearch and capture stdout, automatically creating/managing DuckDB databases based on CSV headers. Also adds a StatusBarWidget for real-time progress monitoring.

## New Features in v0.4.0
- **ConsoleRunnerWidget**: Execute search processes and parse stdout
- **Automatic DuckDB Management**: Create/backup databases based on CSV headers
- **Real-time Result Streaming**: Insert results as they arrive
- **StatusBarWidget**: Display search progress metrics
- **Integrated "Let Jimbo Cook!" button**: Now launches actual searches

## Implementation Steps

### Step 1: Create Process Runner Service

**File: `Services/ProcessRunnerService.cs`**

```csharp
using System.Diagnostics;
using System.Text;
using System.Collections.Concurrent;

namespace TheFool.Services;

public interface IProcessRunnerService
{
    event EventHandler<OutputReceivedEventArgs> OutputReceived;
    event EventHandler<ProcessStateEventArgs> ProcessStateChanged;
    
    Task<Guid> StartProcessAsync(string executable, string arguments, string workingDirectory = null);
    Task StopProcessAsync(Guid processId);
    bool IsProcessRunning(Guid processId);
    ProcessInfo GetProcessInfo(Guid processId);
}

public class ProcessRunnerService : IProcessRunnerService, IDisposable
{
    private readonly ConcurrentDictionary<Guid, ManagedProcess> _processes = new();
    
    public event EventHandler<OutputReceivedEventArgs> OutputReceived;
    public event EventHandler<ProcessStateEventArgs> ProcessStateChanged;

    public async Task<Guid> StartProcessAsync(string executable, string arguments, string workingDirectory = null)
    {
        var processId = Guid.NewGuid();
        var managedProcess = new ManagedProcess
        {
            Id = processId,
            StartTime = DateTime.UtcNow,
            Executable = executable,
            Arguments = arguments
        };

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        var process = new Process { StartInfo = startInfo };
        managedProcess.Process = process;

        // Wire up output handlers
        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                OutputReceived?.Invoke(this, new OutputReceivedEventArgs
                {
                    ProcessId = processId,
                    Data = e.Data,
                    IsError = false,
                    Timestamp = DateTime.UtcNow
                });
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                OutputReceived?.Invoke(this, new OutputReceivedEventArgs
                {
                    ProcessId = processId,
                    Data = e.Data,
                    IsError = true,
                    Timestamp = DateTime.UtcNow
                });
            }
        };

        process.EnableRaisingEvents = true;
        process.Exited += (sender, e) =>
        {
            managedProcess.EndTime = DateTime.UtcNow;
            managedProcess.ExitCode = process.ExitCode;
            ProcessStateChanged?.Invoke(this, new ProcessStateEventArgs
            {
                ProcessId = processId,
                State = ProcessState.Exited,
                ExitCode = process.ExitCode
            });
        };

        _processes[processId] = managedProcess;

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            ProcessStateChanged?.Invoke(this, new ProcessStateEventArgs
            {
                ProcessId = processId,
                State = ProcessState.Running
            });

            return processId;
        }
        catch (Exception ex)
        {
            _processes.TryRemove(processId, out _);
            throw new InvalidOperationException($"Failed to start process: {ex.Message}", ex);
        }
    }

    public async Task StopProcessAsync(Guid processId)
    {
        if (_processes.TryGetValue(processId, out var managedProcess))
        {
            try
            {
                if (!managedProcess.Process.HasExited)
                {
                    // Try graceful shutdown first
                    managedProcess.Process.CloseMainWindow();
                    
                    // Wait up to 5 seconds for graceful exit
                    if (!managedProcess.Process.WaitForExit(5000))
                    {
                        // Force kill if necessary
                        managedProcess.Process.Kill(true);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't throw - process might have already exited
                Debug.WriteLine($"Error stopping process: {ex.Message}");
            }
        }
    }

    public bool IsProcessRunning(Guid processId)
    {
        return _processes.TryGetValue(processId, out var managedProcess) 
               && managedProcess.Process != null 
               && !managedProcess.Process.HasExited;
    }

    public ProcessInfo GetProcessInfo(Guid processId)
    {
        if (_processes.TryGetValue(processId, out var managedProcess))
        {
            return new ProcessInfo
            {
                Id = processId,
                Executable = managedProcess.Executable,
                Arguments = managedProcess.Arguments,
                StartTime = managedProcess.StartTime,
                EndTime = managedProcess.EndTime,
                IsRunning = !managedProcess.Process.HasExited,
                ExitCode = managedProcess.ExitCode
            };
        }
        return null;
    }

    public void Dispose()
    {
        foreach (var process in _processes.Values)
        {
            try
            {
                if (!process.Process.HasExited)
                {
                    process.Process.Kill(true);
                }
                process.Process.Dispose();
            }
            catch { }
        }
        _processes.Clear();
    }

    private class ManagedProcess
    {
        public Guid Id { get; set; }
        public Process Process { get; set; }
        public string Executable { get; set; }
        public string Arguments { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int? ExitCode { get; set; }
    }
}

public class OutputReceivedEventArgs : EventArgs
{
    public Guid ProcessId { get; set; }
    public string Data { get; set; }
    public bool IsError { get; set; }
    public DateTime Timestamp { get; set; }
}

public class ProcessStateEventArgs : EventArgs
{
    public Guid ProcessId { get; set; }
    public ProcessState State { get; set; }
    public int? ExitCode { get; set; }
}

public enum ProcessState
{
    Starting,
    Running,
    Exited,
    Failed
}

public class ProcessInfo
{
    public Guid Id { get; set; }
    public string Executable { get; set; }
    public string Arguments { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool IsRunning { get; set; }
    public int? ExitCode { get; set; }
}
```

### Step 2: Enhanced DuckDB Service with Dynamic Schema

**File: `Services/DuckDbService.cs`** (Updated)

```csharp
using DuckDB.NET.Data;
using System.Data;
using System.Text;

namespace TheFool.Services;

public interface IDuckDbService
{
    Task<List<SearchResult>> GetResultsAsync(string configName, int limit = 1000);
    Task<bool> DatabaseExistsAsync(string configName);
    Task<DatabaseStats> GetDatabaseStatsAsync(string configName);
    Task<bool> CreateOrUpdateDatabaseAsync(string configName, string[] headers);
    Task InsertResultAsync(string configName, string[] values);
    Task<string[]> GetDatabaseHeadersAsync(string configName);
    Task BackupDatabaseAsync(string configName);
}

public class DuckDbService : IDuckDbService, IDisposable
{
    private readonly string _databasePath;
    private DuckDBConnection _currentConnection;
    private string _currentConfigName;
    private string[] _currentHeaders;

    public DuckDbService(IConfiguration configuration)
    {
        _databasePath = configuration["DuckDbPath"] ?? "ouija_databases";
        Directory.CreateDirectory(_databasePath);
    }

    public async Task<bool> CreateOrUpdateDatabaseAsync(string configName, string[] headers)
    {
        // Close any existing connection if switching configs
        if (_currentConfigName != configName && _currentConnection != null)
        {
            _currentConnection.Dispose();
            _currentConnection = null;
        }

        _currentConfigName = configName;
        var dbPath = GetDatabasePath(configName);
        
        // Check if database exists and headers match
        if (File.Exists(dbPath))
        {
            var existingHeaders = await GetDatabaseHeadersAsync(configName);
            if (!HeadersMatch(headers, existingHeaders))
            {
                // Backup existing database
                await BackupDatabaseAsync(configName);
                
                // Close connection before deleting
                if (_currentConnection != null)
                {
                    _currentConnection.Dispose();
                    _currentConnection = null;
                }
                
                // Delete old database
                File.Delete(dbPath);
            }
            else
            {
                // Headers match, we're good
                _currentHeaders = headers;
                return true;
            }
        }

        // Create new database with dynamic schema
        var connection = GetOrCreateConnection(configName);
        var createTableSql = BuildCreateTableSql(headers);
        
        using var command = connection.CreateCommand();
        command.CommandText = createTableSql;
        await command.ExecuteNonQueryAsync();
        
        _currentHeaders = headers;
        return true;
    }

    public async Task InsertResultAsync(string configName, string[] values)
    {
        if (_currentConfigName != configName || _currentConnection == null)
        {
            throw new InvalidOperationException($"Database not initialized for {configName}. Call CreateOrUpdateDatabaseAsync first.");
        }

        if (_currentHeaders == null)
        {
            throw new InvalidOperationException("Headers not set. Call CreateOrUpdateDatabaseAsync first.");
        }

        var insertSql = BuildInsertSql(_currentHeaders, values);
        
        using var command = _currentConnection.CreateCommand();
        command.CommandText = insertSql;
        await command.ExecuteNonQueryAsync();
    }

    public async Task<string[]> GetDatabaseHeadersAsync(string configName)
    {
        var dbPath = GetDatabasePath(configName);
        if (!File.Exists(dbPath))
            return Array.Empty<string>();

        var connection = GetOrCreateConnection(configName);
        
        // Get column information from the search_results table
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(search_results)";
        
        var headers = new List<string>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var columnName = reader.GetString(1);
            // Skip metadata columns
            if (columnName != "inserted_at")
            {
                headers.Add(columnName);
            }
        }
        
        return headers.ToArray();
    }

    public async Task BackupDatabaseAsync(string configName)
    {
        var dbPath = GetDatabasePath(configName);
        if (!File.Exists(dbPath))
            return;

        var backupPath = $"{dbPath}.BAK";
        
        // Close connection before backup
        if (_currentConnection != null && _currentConfigName == configName)
        {
            _currentConnection.Dispose();
            _currentConnection = null;
        }
        
        File.Copy(dbPath, backupPath, true);
    }

    public async Task<List<SearchResult>> GetResultsAsync(string configName, int limit = 1000)
    {
        var dbPath = GetDatabasePath(configName);
        if (!File.Exists(dbPath))
            return new List<SearchResult>();

        var connection = GetOrCreateConnection(configName);
        var results = new List<SearchResult>();

        // Build dynamic query based on known columns
        var query = @"
            SELECT 
                Seed,
                Score,
                COALESCE(NaturalNegatives, 0) as NaturalNegatives,
                COALESCE(DesiredNegatives, 0) as DesiredNegatives,
                inserted_at
            FROM search_results
            ORDER BY Score DESC, inserted_at DESC
            LIMIT ?";

        using var command = connection.CreateCommand();
        command.CommandText = query;
        command.Parameters.Add(new DuckDBParameter(limit));

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new SearchResult
            {
                Seed = reader.GetString(0),
                Score = reader.GetInt32(1),
                NaturalNegativeJokers = reader.GetInt32(2),
                DesiredNegativeJokers = reader.GetInt32(3),
                FoundAt = reader.GetDateTime(4)
            });
        }

        return results;
    }

    private string BuildCreateTableSql(string[] headers)
    {
        var sb = new StringBuilder();
        sb.AppendLine("CREATE TABLE search_results (");
        
        for (int i = 0; i < headers.Length; i++)
        {
            var header = headers[i];
            var columnDef = GetColumnDefinition(header);
            sb.Append($"    {header} {columnDef}");
            
            if (i < headers.Length - 1)
                sb.AppendLine(",");
        }
        
        // Add metadata columns
        sb.AppendLine(",");
        sb.AppendLine("    inserted_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,");
        sb.AppendLine("    PRIMARY KEY (Seed)");
        sb.AppendLine(");");
        
        return sb.ToString();
    }

    private string BuildInsertSql(string[] headers, string[] values)
    {
        var columns = string.Join(", ", headers);
        var placeholders = string.Join(", ", values.Select(v => $"'{v.Replace("'", "''")}'"));
        
        return $@"
            INSERT OR REPLACE INTO search_results ({columns})
            VALUES ({placeholders})";
    }

    private string GetColumnDefinition(string header)
    {
        // Determine column type based on header name
        return header.ToLower() switch
        {
            "seed" => "VARCHAR(16) NOT NULL",
            var h when h.Contains("score") => "INTEGER DEFAULT 0",
            var h when h.Contains("negative") => "INTEGER DEFAULT 0",
            var h when h.Contains("count") => "INTEGER DEFAULT 0",
            _ => "INTEGER DEFAULT 0"  // Default to integer for unknown columns
        };
    }

    private bool HeadersMatch(string[] headers1, string[] headers2)
    {
        if (headers1.Length != headers2.Length)
            return false;
            
        return headers1.SequenceEqual(headers2, StringComparer.OrdinalIgnoreCase);
    }

    private DuckDBConnection GetOrCreateConnection(string configName)
    {
        // Only maintain ONE connection - to the current config's database
        if (_currentConnection == null || _currentConfigName != configName)
        {
            // Close existing connection if switching configs
            if (_currentConnection != null)
            {
                _currentConnection.Dispose();
            }
            
            var dbPath = GetDatabasePath(configName);
            _currentConnection = new DuckDBConnection($"DataSource={dbPath}");
            _currentConnection.Open();
            _currentConfigName = configName;
        }
        return _currentConnection;
    }

    private string GetDatabasePath(string configName)
    {
        var baseName = configName.Replace(".ouija.json", "").Replace(".json", "");
        return Path.Combine(_databasePath, $"{baseName}.ouija.duckdb");
    }

    public async Task<DatabaseStats> GetDatabaseStatsAsync(string configName)
    {
        var dbPath = GetDatabasePath(configName);
        if (!File.Exists(dbPath))
            return new DatabaseStats();

        var connection = GetOrCreateConnection(configName);
        var stats = new DatabaseStats();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                COUNT(*) as total_results,
                MAX(Score) as best_score,
                AVG(Score) as avg_score,
                MIN(inserted_at) as first_result,
                MAX(inserted_at) as last_result
            FROM search_results";

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            stats.TotalResults = reader.GetInt32(0);
            stats.BestScore = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
            stats.AverageScore = reader.IsDBNull(2) ? 0 : reader.GetDouble(2);
            stats.FirstResult = reader.IsDBNull(3) ? null : reader.GetDateTime(3);
            stats.LastResult = reader.IsDBNull(4) ? null : reader.GetDateTime(4);
        }

        return stats;
    }

    public void Dispose()
    {
        if (_currentConnection != null)
        {
            _currentConnection.Dispose();
            _currentConnection = null;
        }
    }
}
```

### Step 3: Create ConsoleRunnerWidget

**File: `Widgets/ConsoleRunnerWidget.razor`**

```razor
@using TheFool.Models
@using TheFool.Services
@using System.Collections.Concurrent
@inject IProcessRunnerService ProcessRunner
@inject IDuckDbService DuckDbService
@inject ISnackbar Snackbar
@implements IDisposable

<MudPaper Class="pa-4" Elevation="2">
    <MudGrid AlignItems="Center" Class="mb-3">
        <MudItem xs="12" sm="6">
            <MudText Typo="Typo.h6">
                <MudIcon Icon="@Icons.Material.Filled.Terminal" Class="mr-2" />
                Search Process
            </MudText>
            @if (IsRunning)
            {
                <MudChip Size="Size.Small" Color="Color.Success" Icon="@Icons.Material.Filled.RadioButtonChecked">
                    Running
                </MudChip>
            }
            else
            {
                <MudChip Size="Size.Small" Color="Color.Default" Icon="@Icons.Material.Filled.RadioButtonUnchecked">
                    Idle
                </MudChip>
            }
        </MudItem>
        <MudItem xs="12" sm="6">
            <MudStack Row="true" Justify="Justify.FlexEnd" Spacing="2">
                @if (!IsRunning)
                {
                    <MudButton Variant="Variant.Filled" 
                              Color="Color.Primary" 
                              Size="Size.Large"
                              StartIcon="@Icons.Material.Filled.LocalFireDepartment"
                              OnClick="StartSearch"
                              Disabled="@string.IsNullOrEmpty(FilterConfig?.name)">
                        🔥 LET JIMBO COOK! 🔥
                    </MudButton>
                }
                else
                {
                    <MudButton Variant="Variant.Filled" 
                              Color="Color.Error" 
                              StartIcon="@Icons.Material.Filled.Stop"
                              OnClick="StopSearch">
                        Stop Cooking
                    </MudButton>
                }
                
                <MudIconButton Icon="@Icons.Material.Filled.ClearAll" 
                              Color="Color.Default"
                              OnClick="ClearConsole"
                              Title="Clear Console" />
            </MudStack>
        </MudItem>
    </MudGrid>

    <!-- Console Output -->
    <MudPaper Class="console-output pa-3" Elevation="0" 
              Style="background-color: #1e1e1e; font-family: 'Consolas', 'Courier New', monospace;">
        <div @ref="consoleElement" class="console-content">
            @foreach (var line in ConsoleLines.TakeLast(MaxConsoleLines))
            {
                <div class="@GetLineClass(line.Type)">
                    <span class="timestamp">[@line.Timestamp:HH:mm:ss]</span> 
                    <span class="content">@line.Content</span>
                </div>
            }
        </div>
    </MudPaper>

    <!-- Statistics -->
    @if (ResultsInserted > 0 || HeadersDetected)
    {
        <MudGrid Class="mt-3">
            <MudItem xs="6" sm="3">
                <MudPaper Class="pa-2 text-center" Elevation="0">
                    <MudText Typo="Typo.caption">Results Found</MudText>
                    <MudText Typo="Typo.h6" Color="Color.Success">@ResultsInserted</MudText>
                </MudPaper>
            </MudItem>
            <MudItem xs="6" sm="3">
                <MudPaper Class="pa-2 text-center" Elevation="0">
                    <MudText Typo="Typo.caption">Runtime</MudText>
                    <MudText Typo="Typo.h6">@Runtime</MudText>
                </MudPaper>
            </MudItem>
            <MudItem xs="6" sm="3">
                <MudPaper Class="pa-2 text-center" Elevation="0">
                    <MudText Typo="Typo.caption">Database</MudText>
                    <MudText Typo="Typo.body2" Color="@(DatabaseReady ? Color.Success : Color.Warning)">
                        @(DatabaseReady ? "Ready" : "Waiting...")
                    </MudText>
                </MudPaper>
            </MudItem>
            <MudItem xs="6" sm="3">
                <MudPaper Class="pa-2 text-center" Elevation="0">
                    <MudText Typo="Typo.caption">Headers</MudText>
                    <MudText Typo="Typo.body2">@(CurrentHeaders?.Length ?? 0) columns</MudText>
                </MudPaper>
            </MudItem>
        </MudGrid>
    }
</MudPaper>

<style>
    .console-output {
        height: 400px;
        overflow-y: auto;
        border-radius: 4px;
    }

    .console-content {
        font-size: 12px;
        line-height: 1.4;
    }

    .console-line {
        margin: 2px 0;
        padding: 2px 4px;
    }

    .console-line.header {
        color: #4FC3F7;
        font-weight: bold;
    }

    .console-line.result {
        color: #81C784;
    }

    .console-line.error {
        color: #EF5350;
    }

    .console-line.status {
        color: #FFB74D;
    }

    .console-line.normal {
        color: #E0E0E0;
    }

    .timestamp {
        color: #666;
        margin-right: 8px;
    }

    /* Auto-scroll to bottom */
    .console-output {
        display: flex;
        flex-direction: column-reverse;
    }
</style>

@code {
    [Parameter] public FilterConfig FilterConfig { get; set; }
    [Parameter] public EventCallback OnSearchStarted { get; set; }
    [Parameter] public EventCallback OnSearchStopped { get; set; }
    [Parameter] public EventCallback<string> OnStatusUpdate { get; set; }
    [Parameter] public EventCallback OnResultsInserted { get; set; }

    private ElementReference consoleElement;
    private Guid? _currentProcessId;
    private DateTime? _startTime;
    private System.Timers.Timer _runtimeTimer;
    private readonly ConcurrentBag<ConsoleLine> ConsoleLines = new();
    private const int MaxConsoleLines = 500;

    private bool IsRunning => _currentProcessId.HasValue && ProcessRunner.IsProcessRunning(_currentProcessId.Value);
    private string Runtime => _startTime.HasValue 
        ? (DateTime.UtcNow - _startTime.Value).ToString(@"hh\:mm\:ss") 
        : "00:00:00";

    private int ResultsInserted = 0;
    private bool HeadersDetected = false;
    private bool DatabaseReady = false;
    private string[] CurrentHeaders = null;

    protected override void OnInitialized()
    {
        // Subscribe to process events
        ProcessRunner.OutputReceived += OnOutputReceived;
        ProcessRunner.ProcessStateChanged += OnProcessStateChanged;

        // Setup runtime timer
        _runtimeTimer = new System.Timers.Timer(1000);
        _runtimeTimer.Elapsed += (s, e) => InvokeAsync(StateHasChanged);
    }

    private async Task StartSearch()
    {
        if (FilterConfig == null)
        {
            Snackbar.Add("No filter configuration loaded!", Severity.Error);
            return;
        }

        try
        {
            // Reset statistics
            ResultsInserted = 0;
            HeadersDetected = false;
            DatabaseReady = false;
            CurrentHeaders = null;
            _startTime = DateTime.UtcNow;

            // Save filter to temp file
            var tempFilterPath = Path.Combine(Path.GetTempPath(), $"{FilterConfig.name}.json");
            var json = System.Text.Json.JsonSerializer.Serialize(FilterConfig, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
            await File.WriteAllTextAsync(tempFilterPath, json);

            AddConsoleLine($"Starting search with filter: {FilterConfig.name}", ConsoleLineType.Normal);
            AddConsoleLine($"Filter saved to: {tempFilterPath}", ConsoleLineType.Normal);

            // Start the search process
            var executable = "dotnet";
            var arguments = $"run --project MotelySearch -- --filter \"{tempFilterPath}\"";
            
            _currentProcessId = await ProcessRunner.StartProcessAsync(executable, arguments);
            
            _runtimeTimer.Start();
            await OnSearchStarted.InvokeAsync();
            
            AddConsoleLine("🔥 JIMBO IS COOKING! 🔥", ConsoleLineType.Status);
            Snackbar.Add("Search started!", Severity.Success);
        }
        catch (Exception ex)
        {
            AddConsoleLine($"Failed to start search: {ex.Message}", ConsoleLineType.Error);
            Snackbar.Add($"Failed to start search: {ex.Message}", Severity.Error);
        }
    }

    private async Task StopSearch()
    {
        if (_currentProcessId.HasValue)
        {
            AddConsoleLine("Stopping search...", ConsoleLineType.Status);
            await ProcessRunner.StopProcessAsync(_currentProcessId.Value);
            _currentProcessId = null;
            _runtimeTimer.Stop();
            await OnSearchStopped.InvokeAsync();
            Snackbar.Add("Search stopped", Severity.Warning);
        }
    }

    private void OnOutputReceived(object sender, OutputReceivedEventArgs e)
    {
        if (_currentProcessId.HasValue && e.ProcessId == _currentProcessId.Value)
        {
            InvokeAsync(async () => await ProcessOutput(e.Data, e.IsError));
        }
    }

    private async Task ProcessOutput(string line, bool isError)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        // Parse based on first character
        var firstChar = line[0];
        var content = line.Length > 1 ? line.Substring(1) : "";

        switch (firstChar)
        {
            case '+': // CSV Header
                await ProcessCsvHeader(content);
                break;
                
            case '|': // CSV Data Row
                await ProcessCsvRow(content);
                break;
                
            case '$': // Status Update
                ProcessStatusUpdate(content);
                break;
                
            default: // Regular output
                AddConsoleLine(line, isError ? ConsoleLineType.Error : ConsoleLineType.Normal);
                break;
        }
    }

    private async Task ProcessCsvHeader(string headerLine)
    {
        try
        {
            CurrentHeaders = headerLine.Split(',');
            AddConsoleLine($"CSV Headers detected: {CurrentHeaders.Length} columns", ConsoleLineType.Header);
            
            // Create or update database
            var success = await DuckDbService.CreateOrUpdateDatabaseAsync(FilterConfig.name, CurrentHeaders);
            
            if (success)
            {
                HeadersDetected = true;
                DatabaseReady = true;
                AddConsoleLine($"Database ready: {FilterConfig.name}.ouija.duckdb", ConsoleLineType.Header);
            }
            else
            {
                AddConsoleLine("Failed to initialize database", ConsoleLineType.Error);
            }
        }
        catch (Exception ex)
        {
            AddConsoleLine($"Header processing error: {ex.Message}", ConsoleLineType.Error);
        }
    }

    private async Task ProcessCsvRow(string dataLine)
    {
        if (!DatabaseReady || CurrentHeaders == null)
        {
            AddConsoleLine("Received data before database ready - skipping", ConsoleLineType.Error);
            return;
        }

        try
        {
            var values = dataLine.Split(',');
            await DuckDbService.InsertResultAsync(FilterConfig.name, values);
            
            ResultsInserted++;
            
            // Show first few results in console
            if (ResultsInserted <= 5)
            {
                AddConsoleLine($"Result: {values[0]} (Score: {values[1]})", ConsoleLineType.Result);
            }
            else if (ResultsInserted == 6)
            {
                AddConsoleLine("... (subsequent results inserted silently)", ConsoleLineType.Normal);
            }

            // Notify every 100 results
            if (ResultsInserted % 100 == 0)
            {
                await OnResultsInserted.InvokeAsync();
            }
        }
        catch (Exception ex)
        {
            AddConsoleLine($"Insert error: {ex.Message}", ConsoleLineType.Error);
        }
    }

    private void ProcessStatusUpdate(string statusLine)
    {
        // Parse: "0.00% ~2:01:42:40 remaining (12583 seeds/ms)"
        var parts = statusLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length >= 4)
        {
            var percentComplete = parts[0];
            var timeRemaining = parts[1].TrimStart('~');
            var seedsPerMs = parts[3].Replace("(", "").Replace(")", "");
            
            _ = OnStatusUpdate.InvokeAsync($"{percentComplete} | ~{timeRemaining} | {seedsPerMs} seeds/ms");
        }
        
        AddConsoleLine($"Progress: {statusLine}", ConsoleLineType.Status);
    }

    private void OnProcessStateChanged(object sender, ProcessStateEventArgs e)
    {
        if (_currentProcessId.HasValue && e.ProcessId == _currentProcessId.Value)
        {
            InvokeAsync(() =>
            {
                if (e.State == ProcessState.Exited)
                {
                    _runtimeTimer.Stop();
                    var exitMessage = e.ExitCode == 0 
                        ? "Search completed successfully" 
                        : $"Search exited with code {e.ExitCode}";
                    AddConsoleLine(exitMessage, e.ExitCode == 0 ? ConsoleLineType.Status : ConsoleLineType.Error);
                    
                    _currentProcessId = null;
                    OnSearchStopped.InvokeAsync();
                }
            });
        }
    }

    private void AddConsoleLine(string content, ConsoleLineType type)
    {
        ConsoleLines.Add(new ConsoleLine
        {
            Content = content,
            Type = type,
            Timestamp = DateTime.Now
        });
        
        // Keep only last N lines
        while (ConsoleLines.Count > MaxConsoleLines)
        {
            ConsoleLines.TryTake(out _);
        }
        
        InvokeAsync(StateHasChanged);
    }

    private void ClearConsole()
    {
        ConsoleLines.Clear();
        AddConsoleLine("Console cleared", ConsoleLineType.Normal);
    }

    private string GetLineClass(ConsoleLineType type) => type switch
    {
        ConsoleLineType.Header => "console-line header",
        ConsoleLineType.Result => "console-line result",
        ConsoleLineType.Error => "console-line error",
        ConsoleLineType.Status => "console-line status",
        _ => "console-line normal"
    };

    public void Dispose()
    {
        ProcessRunner.OutputReceived -= OnOutputReceived;
        ProcessRunner.ProcessStateChanged -= OnProcessStateChanged;
        _runtimeTimer?.Dispose();
        
        // Stop any running process
        if (_currentProcessId.HasValue)
        {
            _ = StopSearch();
        }
    }

    private class ConsoleLine
    {
        public string Content { get; set; }
        public ConsoleLineType Type { get; set; }
        public DateTime Timestamp { get; set; }
    }

    private enum ConsoleLineType
    {
        Normal,
        Header,
        Result,
        Error,
        Status
    }
}
```

### Step 4: Create StatusBarWidget

**File: `Widgets/StatusBarWidget.razor`**

```razor
@using System.Timers

<MudPaper Class="status-bar" Elevation="4" Square="true">
    <MudGrid AlignItems="Center" Spacing="1">
        <MudItem xs="12" sm="3">
            <MudStack Row="true" AlignItems="Center" Spacing="1">
                <MudIcon Icon="@Icons.Material.Filled.Info" Size="Size.Small" />
                <MudText Typo="Typo.caption">@ConnectionStatus</MudText>
            </MudStack>
        </MudItem>
        
        @if (IsSearching)
        {
            <MudItem xs="12" sm="3">
                <MudProgressLinear Value="@ProgressPercent" 
                                  Color="Color.Primary" 
                                  Size="Size.Small"
                                  Class="mt-1 mb-1">
                    <MudText Typo="Typo.caption">@PercentCompleteMetric</MudText>
                </MudProgressLinear>
            </MudItem>
            
            <MudItem xs="12" sm="3">
                <MudStack Row="true" AlignItems="Center" Spacing="1">
                    <MudIcon Icon="@Icons.Material.Filled.Timer" Size="Size.Small" />
                    <MudText Typo="Typo.caption">@TimeRemainingDisplay</MudText>
                </MudStack>
            </MudItem>
            
            <MudItem xs="12" sm="3">
                <MudStack Row="true" AlignItems="Center" Spacing="1">
                    <MudIcon Icon="@Icons.Material.Filled.Speed" Size="Size.Small" />
                    <MudText Typo="Typo.caption">@SeedsPerSecondDisplay</MudText>
                </MudStack>
            </MudItem>
        }
        else
        {
            <MudItem xs="12" sm="9">
                <MudText Typo="Typo.caption" Align="Align.Center">
                    @StatusMessage
                </MudText>
            </MudItem>
        }
    </MudGrid>
</MudPaper>

<style>
    .status-bar {
        position: fixed;
        bottom: 0;
        left: 0;
        right: 0;
        padding: 8px 16px;
        background-color: var(--mud-palette-surface);
        border-top: 1px solid var(--mud-palette-divider);
        z-index: 1300;
    }
    
    /* Add padding to main content to account for status bar */
    :global(.mud-main-content) {
        padding-bottom: 60px !important;
    }
</style>

@code {
    [Parameter] public string ConnectionStatus { get; set; } = "Ready";
    [Parameter] public bool IsSearching { get; set; }
    [Parameter] public string StatusMessage { get; set; } = "TheFool v0.4.0 - Ready to search";
    
    // Metrics from search process
    public string PercentCompleteMetric { get; set; } = "0.00%";
    public string TimeRemainingMetric { get; set; } = "0:00:00";
    public string SeedsPerMillisecondMetric { get; set; } = "0";

    private double ProgressPercent => double.TryParse(PercentCompleteMetric.TrimEnd('%'), out var percent) ? percent : 0;
    private string TimeRemainingDisplay => string.IsNullOrEmpty(TimeRemainingMetric) ? "Calculating..." : $"~{TimeRemainingMetric} remaining";
    private string SeedsPerSecondDisplay => $"{ConvertToSeedsPerSecond()} seeds/s";

    public void UpdateSearchMetrics(string percentComplete, string timeRemaining, string seedsPerMs)
    {
        PercentCompleteMetric = percentComplete;
        TimeRemainingMetric = timeRemaining;
        SeedsPerMillisecondMetric = seedsPerMs;
        InvokeAsync(StateHasChanged);
    }

    public void UpdateStatus(string message)
    {
        StatusMessage = message;
        InvokeAsync(StateHasChanged);
    }

    public void SetSearching(bool searching)
    {
        IsSearching = searching;
        if (!searching)
        {
            // Reset metrics
            PercentCompleteMetric = "0.00%";
            TimeRemainingMetric = "0:00:00";
            SeedsPerMillisecondMetric = "0";
        }
        InvokeAsync(StateHasChanged);
    }

    private string ConvertToSeedsPerSecond()
    {
        if (int.TryParse(SeedsPerMillisecondMetric, out var seedsPerMs))
        {
            var seedsPerSecond = seedsPerMs * 1000;
            return seedsPerSecond > 1000000 
                ? $"{seedsPerSecond / 1000000.0:F1}M" 
                : seedsPerSecond > 1000 
                    ? $"{seedsPerSecond / 1000.0:F1}K" 
                    : seedsPerSecond.ToString();
        }
        return "0";
    }
}
```

### Step 5: Update Index.razor to Wire Everything Together

**File: `Pages/Index.razor`** (Updated for v0.4.0)

```razor
@page "/"
@using TheFool.Models
@using TheFool.Widgets
@inject ISnackbar Snackbar

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4 mb-5">
    <MudText Typo="Typo.h4" Class="mb-4" Align="Align.Center">
        🃏 TheFool - Balatro Seed Finder
    </MudText>
    
    <!-- File Management Widget -->
    <FileLoaderSaverWidget ConfigName="@CurrentConfig.name"
                          ConfigNameChanged="HandleConfigNameChange"
                          OnConfigLoaded="LoadConfiguration"
                          OnSaveRequested="HandleSave"
                          GetCurrentConfig="@(() => CurrentConfig)" />
    
    <!-- Console Runner Widget -->
    <ConsoleRunnerWidget @ref="ConsoleRunner"
                        FilterConfig="@CurrentConfig"
                        OnSearchStarted="HandleSearchStarted"
                        OnSearchStopped="HandleSearchStopped"
                        OnStatusUpdate="HandleStatusUpdate"
                        OnResultsInserted="RefreshResults" />
    
    <MudGrid Class="mt-3">
        <!-- Left Column - Config Display & Editor -->
        <MudItem xs="12" lg="5">
            <MudStack Spacing="3">
                <!-- Config Display (without button since it's in ConsoleRunner now) -->
                <FilterConfigViewWidget Config="@CurrentConfig"
                                       ShowLaunchButton="false" />
                
                <!-- Config Editor -->
                <FilterConfigEditorWidget Config="@CurrentConfig"
                                         ConfigChanged="UpdateConfiguration" />
            </MudStack>
        </MudItem>
        
        <!-- Right Column - Search Results -->
        <MudItem xs="12" lg="7">
            <SearchResultsWidget @ref="ResultsWidget"
                                ConfigName="@CurrentConfig.name"
                                OnResultsLoaded="HandleResultsLoaded" />
        </MudItem>
    </MudGrid>
</MudContainer>

<!-- Status Bar -->
<StatusBarWidget @ref="StatusBar"
                ConnectionStatus="@ConnectionStatus"
                IsSearching="@IsSearching" />

@code {
    private FilterConfig CurrentConfig = CreateDefaultConfig();
    private ConsoleRunnerWidget ConsoleRunner;
    private SearchResultsWidget ResultsWidget;
    private StatusBarWidget StatusBar;
    
    private string ConnectionStatus = "Ready";
    private bool IsSearching = false;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && ResultsWidget != null)
        {
            // Load any existing results on startup
            await ResultsWidget.RefreshItems();
        }
    }

    private async Task HandleConfigNameChange(string newName)
    {
        CurrentConfig = CurrentConfig with { name = newName };
        StateHasChanged();
        
        // Refresh results when config name changes
        if (ResultsWidget != null)
        {
            await ResultsWidget.RefreshItems();
        }
    }

    private async Task LoadConfiguration(FilterConfig config)
    {
        CurrentConfig = config;
        StateHasChanged();
        
        // Refresh results for newly loaded config
        if (ResultsWidget != null)
        {
            await ResultsWidget.RefreshItems();
        }
    }

    private void UpdateConfiguration(FilterConfig config)
    {
        CurrentConfig = config;
        StateHasChanged();
    }

    private void HandleSave(string filename)
    {
        StatusBar?.UpdateStatus($"Configuration saved: {filename}");
    }

    private void HandleResultsLoaded()
    {
        // Optional: Update UI based on loaded results
    }

    private async Task HandleSearchStarted()
    {
        IsSearching = true;
        ConnectionStatus = "Searching...";
        StatusBar?.SetSearching(true);
        StatusBar?.UpdateStatus("🔥 Jimbo is cooking! 🔥");
    }

    private async Task HandleSearchStopped()
    {
        IsSearching = false;
        ConnectionStatus = "Ready";
        StatusBar?.SetSearching(false);
        StatusBar?.UpdateStatus("Search completed");
        
        // Refresh results to show new findings
        if (ResultsWidget != null)
        {
            await ResultsWidget.RefreshItems();
        }
    }

    private async Task HandleStatusUpdate(string statusLine)
    {
        // Parse status update and forward to status bar
        // Expected format: "0.00% | ~2:01:42:40 | 12583 seeds/ms"
        var parts = statusLine.Split(" | ");
        if (parts.Length >= 3 && StatusBar != null)
        {
            StatusBar.UpdateSearchMetrics(
                parts[0], // percent
                parts[1].TrimStart('~'), // time remaining
                parts[2].Split(' ')[0] // seeds/ms number only
            );
        }
    }

    private async Task RefreshResults()
    {
        // Debounced refresh - called every 100 results
        if (ResultsWidget != null)
        {
            await ResultsWidget.RefreshItems();
        }
    }

    private static FilterConfig CreateDefaultConfig()
    {
        return new FilterConfig(
            name: "My Filter",
            description: "Custom filter configuration",
            author: Environment.UserName,
            keywords: new List<string> { "FOOL", "SEARCH" },
            filter_config: new FilterSettings(
                numNeeds: 0,
                numWants: 0,
                Needs: new List<FilterCondition>(),
                Wants: new List<FilterCondition>(),
                minSearchAnte: 1,
                maxSearchAnte: 8,
                deck: "GhostDeck",
                stake: "WhiteStake",
                scoreNaturalNegatives: true,
                scoreDesiredNegatives: true
            )
        );
    }
}
```

### Step 6: Update Program.cs for New Services

**File: `Program.cs`** (Updated for v0.4.0)

```csharp
using MudBlazor.Services;
using TheFool.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();

// Add application services
builder.Services.AddSingleton<IDuckDbService, DuckDbService>();
builder.Services.AddSingleton<IProcessRunnerService, ProcessRunnerService>();

// Configure paths
builder.Configuration["DuckDbPath"] = "ouija_databases";

var app = builder.Build();

// Ensure database directory exists
Directory.CreateDirectory("ouija_databases");

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// Cleanup on shutdown
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    var processRunner = app.Services.GetService<IProcessRunnerService>();
    (processRunner as IDisposable)?.Dispose();
    
    var duckDb = app.Services.GetService<IDuckDbService>();
    (duckDb as IDisposable)?.Dispose();
});

app.Run();
```

## Testing Instructions for AI Agents

### 1. Build and Verify Services
```bash
dotnet build
# Should complete with 0 errors
# Verify all services are registered in DI
```

### 2. Test Process Execution
1. Click "LET JIMBO COOK!" without a config loaded
   - Should show error message
2. Load a valid config
3. Click "LET JIMBO COOK!"
   - Console should show process starting
   - Status bar should update to "Searching..."

### 3. Test CSV Header Processing
Mock stdout with test data:
```
+Seed,Score,NaturalNegatives,DesiredNegatives,Joker1,Joker2
```
- Should create new database
- Should show "Database ready" message

### 4. Test Result Insertion
Mock CSV data rows:
```
|ABC123,5,1,2,0,1
|DEF456,3,0,1,1,0
```
- Results counter should increment
- Database should contain rows

### 5. Test Status Updates
Mock status line:
```
$25.50% ~1:30:00 remaining (5000 seeds/ms)
```
- Status bar should update all metrics
- Progress bar should show 25.5%

### 6. Test Database Backup
1. Create search with headers
2. Change a header and restart
3. Verify .BAK file created
4. Verify new database created

## Success Criteria
- ✅ "Let Jimbo Cook!" button launches actual search process
- ✅ CSV headers automatically create DuckDB schema
- ✅ Results stream into database in real-time
- ✅ Console shows filtered output (no +|$ prefixes)
- ✅ Status bar shows live progress metrics
- ✅ Database backup on schema mismatch
- ✅ Clean process management (start/stop)
- ✅ Results widget auto-refreshes during search

## Next Steps for v0.5.0
- **SearchQueueWidget**: Queue multiple searches
- **ResultAnalyticsWidget**: Charts and statistics
- **SearchTemplatesWidget**: Preset filter configurations
- **ExportSchedulerWidget**: Auto-export on conditions

The widget architecture continues to deliver! Each new feature is completely self-contained and reusable. 🚀