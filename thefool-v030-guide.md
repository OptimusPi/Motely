# TheFool v0.3.0 - Search Results Widget Implementation Guide

## Overview
Add a SearchResultsWidget that automatically loads and displays results from DuckDB files matching the current configuration name. This provides immediate visual feedback by loading existing search results from the prototype.

## New Features in v0.3.0
- **SearchResultsWidget**: Display search results from DuckDB files
- **Auto-loading**: Automatically loads `{configName}.duckdb` when config changes
- **Refresh functionality**: Re-query the database on demand
- **Sortable columns**: Click headers to sort results
- **Export capabilities**: Export filtered results to CSV

## Prerequisites
- Working TheFool v0.2.0 installation
- DuckDB.NET NuGet package
- Existing `.duckdb` files in `ouija_databases/` folder

## Implementation Steps

### Step 1: Install DuckDB.NET Package
```bash
cd TheFool
dotnet add package DuckDB.NET
dotnet add package DuckDB.NET.Data
```

### Step 2: Create DuckDB Service

**File: `Services/DuckDbService.cs`**

```csharp
using DuckDB.NET.Data;
using System.Data;

namespace TheFool.Services;

public interface IDuckDbService
{
    Task<List<SearchResult>> GetResultsAsync(string configName, int limit = 1000);
    Task<bool> DatabaseExistsAsync(string configName);
    Task<DatabaseStats> GetDatabaseStatsAsync(string configName);
}

public class DuckDbService : IDuckDbService, IDisposable
{
    private readonly string _databasePath;
    private readonly Dictionary<string, DuckDBConnection> _connections = new();

    public DuckDbService(IConfiguration configuration)
    {
        _databasePath = configuration["DuckDbPath"] ?? "ouija_databases";
        Directory.CreateDirectory(_databasePath);
    }

    public async Task<List<SearchResult>> GetResultsAsync(string configName, int limit = 1000)
    {
        var dbPath = GetDatabasePath(configName);
        if (!File.Exists(dbPath))
            return new List<SearchResult>();

        var connection = GetOrCreateConnection(dbPath);
        var results = new List<SearchResult>();

        // Adjust this query based on your actual DuckDB schema
        var query = @"
            SELECT 
                seed,
                score,
                natural_negative_jokers,
                desired_negative_jokers,
                found_items,
                timestamp
            FROM search_results
            ORDER BY score DESC, timestamp DESC
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
                FoundItemsJson = reader.IsDBNull(4) ? null : reader.GetString(4),
                FoundAt = reader.IsDBNull(5) ? DateTime.UtcNow : reader.GetDateTime(5)
            });
        }

        return results;
    }

    public async Task<bool> DatabaseExistsAsync(string configName)
    {
        var dbPath = GetDatabasePath(configName);
        return await Task.FromResult(File.Exists(dbPath));
    }

    public async Task<DatabaseStats> GetDatabaseStatsAsync(string configName)
    {
        var dbPath = GetDatabasePath(configName);
        if (!File.Exists(dbPath))
            return new DatabaseStats();

        var connection = GetOrCreateConnection(dbPath);
        var stats = new DatabaseStats();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                COUNT(*) as total_results,
                MAX(score) as best_score,
                AVG(score) as avg_score,
                MIN(timestamp) as first_result,
                MAX(timestamp) as last_result
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

    private DuckDBConnection GetOrCreateConnection(string dbPath)
    {
        if (!_connections.ContainsKey(dbPath))
        {
            var connection = new DuckDBConnection($"DataSource={dbPath}");
            connection.Open();
            _connections[dbPath] = connection;
        }
        return _connections[dbPath];
    }

    private string GetDatabasePath(string configName)
    {
        // Handle both .json and .ouija.json extensions
        var baseName = configName.Replace(".ouija.json", "").Replace(".json", "");
        return Path.Combine(_databasePath, $"{baseName}.ouija.duckdb");
    }

    public void Dispose()
    {
        foreach (var connection in _connections.Values)
        {
            connection?.Dispose();
        }
        _connections.Clear();
    }
}

public class DatabaseStats
{
    public int TotalResults { get; set; }
    public int BestScore { get; set; }
    public double AverageScore { get; set; }
    public DateTime? FirstResult { get; set; }
    public DateTime? LastResult { get; set; }
}
```

### Step 3: Update SearchResult Model

**File: `Models/SearchResult.cs`**

```csharp
namespace TheFool.Models;

public class SearchResult
{
    public string Seed { get; set; }
    public int Score { get; set; }
    public int NaturalNegativeJokers { get; set; }
    public int DesiredNegativeJokers { get; set; }
    public DateTime FoundAt { get; set; }
    public string FoundItemsJson { get; set; }
    
    // Computed properties for display
    public string ScoreClass => Score switch
    {
        >= 5 => "high-score",
        >= 3 => "medium-score",
        _ => "low-score"
    };
    
    public string TimeAgo
    {
        get
        {
            var diff = DateTime.UtcNow - FoundAt;
            return diff switch
            {
                { TotalMinutes: < 1 } => "just now",
                { TotalMinutes: < 60 } => $"{(int)diff.TotalMinutes}m ago",
                { TotalHours: < 24 } => $"{(int)diff.TotalHours}h ago",
                { TotalDays: < 7 } => $"{(int)diff.TotalDays}d ago",
                _ => FoundAt.ToString("yyyy-MM-dd")
            };
        }
    }
}
```

### Step 4: Create SearchResultsWidget

**File: `Widgets/SearchResultsWidget.razor`**

```razor
@using TheFool.Models
@using TheFool.Services
@using System.Text.Json
@inject IDuckDbService DuckDbService
@inject IJSRuntime JS
@inject ISnackbar Snackbar

<MudPaper Class="pa-4" Elevation="2">
    <MudGrid AlignItems="Center" Class="mb-3">
        <MudItem xs="12" sm="6">
            <MudText Typo="Typo.h6">
                <MudIcon Icon="@Icons.Material.Filled.Search" Class="mr-2" />
                Search Results
                @if (IsLoading)
                {
                    <MudProgressCircular Size="Size.Small" Indeterminate="true" Class="ml-2" />
                }
            </MudText>
            @if (DatabaseStats != null && DatabaseStats.TotalResults > 0)
            {
                <MudText Typo="Typo.caption" Color="Color.Tertiary">
                    @DatabaseStats.TotalResults total results | 
                    Best score: @DatabaseStats.BestScore | 
                    Last updated: @DatabaseStats.LastResult?.ToString("g")
                </MudText>
            }
        </MudItem>
        <MudItem xs="12" sm="6">
            <MudStack Row="true" Justify="Justify.FlexEnd" Spacing="2">
                <MudTextField @bind-Value="SearchFilter" 
                             Placeholder="Filter seeds..." 
                             Variant="Variant.Outlined"
                             Immediate="true"
                             Adornment="Adornment.Start"
                             AdornmentIcon="@Icons.Material.Filled.FilterList"
                             Class="mt-0"
                             Style="width: 200px" />
                
                <MudButton Variant="Variant.Outlined" 
                          Color="Color.Primary"
                          StartIcon="@Icons.Material.Filled.Refresh"
                          OnClick="RefreshItems"
                          Disabled="@IsLoading">
                    Refresh
                </MudButton>
                
                <MudButton Variant="Variant.Outlined" 
                          Color="Color.Secondary"
                          StartIcon="@Icons.Material.Filled.Download"
                          OnClick="ExportResults"
                          Disabled="@(!Results.Any())">
                    Export CSV
                </MudButton>
            </MudStack>
        </MudItem>
    </MudGrid>

    @if (!DatabaseExists)
    {
        <MudAlert Severity="Severity.Info" Class="mb-3">
            <MudText>
                No results database found for <strong>@ConfigName</strong>
            </MudText>
            <MudText Typo="Typo.caption">
                Looking for: @GetExpectedDatabasePath()
            </MudText>
        </MudAlert>
    }
    else if (!Results.Any() && !IsLoading)
    {
        <MudAlert Severity="Severity.Warning" Class="mb-3">
            No results found in the database. Try running a search first!
        </MudAlert>
    }
    else
    {
        <MudDataGrid Items="@FilteredResults" 
                     SortMode="SortMode.Multiple" 
                     Filterable="false"
                     Dense="true"
                     Hover="true"
                     RowsPerPage="25"
                     Class="results-grid">
            <Columns>
                <PropertyColumn Property="x => x.Seed" 
                               Title="Seed"
                               Sortable="true">
                    <CellTemplate>
                        <MudTooltip Text="Click to copy">
                            <MudChip Size="Size.Small" 
                                    OnClick="@(() => CopySeed(context.Item.Seed))"
                                    Style="cursor: pointer;">
                                @context.Item.Seed
                            </MudChip>
                        </MudTooltip>
                    </CellTemplate>
                </PropertyColumn>
                
                <PropertyColumn Property="x => x.Score" 
                               Title="Score"
                               Sortable="true">
                    <CellTemplate>
                        <MudChip Size="Size.Small" 
                                Color="@GetScoreColor(context.Item.Score)"
                                Variant="Variant.Filled">
                            @context.Item.Score
                        </MudChip>
                    </CellTemplate>
                </PropertyColumn>
                
                <PropertyColumn Property="x => x.NaturalNegativeJokers" 
                               Title="Natural Negatives"
                               Sortable="true" />
                
                <PropertyColumn Property="x => x.DesiredNegativeJokers" 
                               Title="Desired Negatives"
                               Sortable="true" />
                
                <PropertyColumn Property="x => x.TimeAgo" 
                               Title="Found"
                               Sortable="false" />
                
                <TemplateColumn Title="Actions" Sortable="false">
                    <CellTemplate>
                        <MudIconButton Icon="@Icons.Material.Filled.ContentCopy" 
                                      Size="Size.Small"
                                      OnClick="@(() => CopyResultDetails(context.Item))" />
                    </CellTemplate>
                </TemplateColumn>
            </Columns>
            
            <PagerContent>
                <MudDataGridPager T="SearchResult" />
            </PagerContent>
        </MudDataGrid>
    }
</MudPaper>

<style>
    .results-grid {
        max-height: 600px;
    }
    
    .high-score {
        background-color: var(--mud-palette-success) !important;
    }
    
    .medium-score {
        background-color: var(--mud-palette-warning) !important;
    }
    
    .low-score {
        background-color: var(--mud-palette-info) !important;
    }
</style>

@code {
    [Parameter] public string ConfigName { get; set; } = "";
    [Parameter] public EventCallback OnResultsLoaded { get; set; }

    private List<SearchResult> Results = new();
    private DatabaseStats DatabaseStats;
    private bool IsLoading = false;
    private bool DatabaseExists = false;
    private string SearchFilter = "";

    private IEnumerable<SearchResult> FilteredResults => 
        string.IsNullOrWhiteSpace(SearchFilter) 
            ? Results 
            : Results.Where(r => r.Seed.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase));

    protected override async Task OnParametersSetAsync()
    {
        if (!string.IsNullOrEmpty(ConfigName))
        {
            await RefreshItems();
        }
    }

    public async Task RefreshItems()
    {
        if (string.IsNullOrEmpty(ConfigName))
            return;

        IsLoading = true;
        StateHasChanged();

        try
        {
            // Check if database exists
            DatabaseExists = await DuckDbService.DatabaseExistsAsync(ConfigName);
            
            if (DatabaseExists)
            {
                // Load results
                Results = await DuckDbService.GetResultsAsync(ConfigName);
                
                // Get stats
                DatabaseStats = await DuckDbService.GetDatabaseStatsAsync(ConfigName);
                
                await OnResultsLoaded.InvokeAsync();
                
                var message = Results.Any() 
                    ? $"Loaded {Results.Count} results from {ConfigName}.duckdb"
                    : "Database is empty";
                    
                Snackbar.Add(message, Results.Any() ? Severity.Success : Severity.Warning);
            }
            else
            {
                Results.Clear();
                DatabaseStats = null;
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error loading database: {ex.Message}", Severity.Error);
            Results.Clear();
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    private async Task CopySeed(string seed)
    {
        await JS.InvokeVoidAsync("navigator.clipboard.writeText", seed);
        Snackbar.Add($"Copied: {seed}", Severity.Info);
    }

    private async Task CopyResultDetails(SearchResult result)
    {
        var details = $"Seed: {result.Seed}\n" +
                     $"Score: {result.Score}\n" +
                     $"Natural Negatives: {result.NaturalNegativeJokers}\n" +
                     $"Desired Negatives: {result.DesiredNegativeJokers}\n" +
                     $"Found: {result.FoundAt:yyyy-MM-dd HH:mm:ss}";
                     
        await JS.InvokeVoidAsync("navigator.clipboard.writeText", details);
        Snackbar.Add("Result details copied!", Severity.Info);
    }

    private async Task ExportResults()
    {
        try
        {
            var csv = "Seed,Score,Natural Negatives,Desired Negatives,Found At\n";
            foreach (var result in FilteredResults)
            {
                csv += $"{result.Seed},{result.Score},{result.NaturalNegativeJokers}," +
                      $"{result.DesiredNegativeJokers},{result.FoundAt:yyyy-MM-dd HH:mm:ss}\n";
            }
            
            await JS.InvokeVoidAsync("downloadFile", 
                $"{ConfigName}_results_{DateTime.Now:yyyyMMdd_HHmmss}.csv", 
                csv);
                
            Snackbar.Add("Results exported!", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Export failed: {ex.Message}", Severity.Error);
        }
    }

    private Color GetScoreColor(int score) => score switch
    {
        >= 5 => Color.Success,
        >= 3 => Color.Warning,
        _ => Color.Info
    };

    private string GetExpectedDatabasePath()
    {
        var baseName = ConfigName.Replace(".ouija.json", "").Replace(".json", "");
        return $"ouija_databases/{baseName}.ouija.duckdb";
    }
}
```

### Step 5: Update Index.razor to Include Results Widget

**File: `Pages/Index.razor`** (Updated)

```razor
@page "/"
@using TheFool.Models
@using TheFool.Widgets
@inject ISnackbar Snackbar

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4" Align="Align.Center">
        🃏 TheFool - Balatro Seed Finder
    </MudText>
    
    <!-- File Management Widget -->
    <FileLoaderSaverWidget ConfigName="@CurrentConfig.name"
                          ConfigNameChanged="HandleConfigNameChange"
                          OnConfigLoaded="LoadConfiguration"
                          OnSaveRequested="HandleSave"
                          GetCurrentConfig="@(() => CurrentConfig)" />
    
    <MudGrid>
        <!-- Left Column - Config Display & Editor -->
        <MudItem xs="12" lg="5">
            <MudStack Spacing="3">
                <!-- Config Display -->
                <FilterConfigViewWidget Config="@CurrentConfig"
                                       ShowLaunchButton="true"
                                       OnLaunchSearch="StartSearch" />
                
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

@code {
    private FilterConfig CurrentConfig = CreateDefaultConfig();
    private SearchResultsWidget ResultsWidget;

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
        // Additional save logic if needed
    }

    private void HandleResultsLoaded()
    {
        // Optional: Update UI based on loaded results
    }

    private void StartSearch()
    {
        Snackbar.Add("🔥 JIMBO IS COOKING! 🔥", Severity.Info);
        
        // TODO: Wire up to actual search process
        var json = System.Text.Json.JsonSerializer.Serialize(CurrentConfig, new System.Text.Json.JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        
        // Write to temp file and launch searcher
        var tempFile = Path.Combine(Path.GetTempPath(), "thefool_search.json");
        File.WriteAllText(tempFile, json);
        
        // Process.Start("MotelySearch.exe", $"--filter \"{tempFile}\"");
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

### Step 6: Update Program.cs to Register DuckDB Service

**File: `Program.cs`** (Updated)

```csharp
using MudBlazor.Services;
using TheFool.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();

// Add DuckDB service
builder.Services.AddSingleton<IDuckDbService, DuckDbService>();

// Configure DuckDB path
builder.Configuration["DuckDbPath"] = "ouija_databases";

var app = builder.Build();

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

app.Run();
```

### Step 7: Update CSS for Better Visuals

**File: `wwwroot/css/site.css`** (Add these styles)

```css
/* Search Results Grid Enhancements */
.mud-data-grid {
    font-size: 0.875rem;
}

.mud-data-grid-cell {
    padding: 8px 16px !important;
}

/* Score chip animations */
.mud-chip {
    transition: transform 0.2s ease-in-out;
}

.mud-chip:hover {
    transform: scale(1.05);
}

/* Results widget specific */
.results-grid .mud-table-container {
    border-radius: 8px;
    overflow: hidden;
}

/* Smooth loading transitions */
.mud-progress-circular {
    animation: fade-in 0.3s ease-in-out;
}

@keyframes fade-in {
    from { opacity: 0; }
    to { opacity: 1; }
}
```

## Testing Instructions for AI Agents

### 1. Setup Test Environment
```bash
# Create test database directory
mkdir -p ouija_databases

# Ensure DuckDB package is installed
dotnet restore
```

### 2. Test Database Detection
1. Launch the app without any `.duckdb` files
2. Verify the info message appears: "No results database found"
3. Add a test `.duckdb` file matching config name
4. Click "Refresh" - should load results

### 3. Test Auto-Loading
1. Load a config file (e.g., `chadperkeo.ouija.json`)
2. Results should auto-load from `chadperkeo.ouija.duckdb`
3. Change config name in FileLoaderSaverWidget
4. Results should refresh automatically

### 4. Test Features
- **Sorting**: Click column headers
- **Filtering**: Type in filter box
- **Copy Seed**: Click on seed chip
- **Export**: Click Export CSV button
- **Pagination**: Navigate through pages

### 5. Performance Test
```bash
# Test with large database (1000+ results)
# Should load within 1-2 seconds
# UI should remain responsive during loading
```

## Troubleshooting Guide

### Common Issues

1. **"No results database found"**
   - Check database file exists in `ouija_databases/`
   - Verify naming convention: `{configName}.ouija.duckdb`
   - Check file permissions

2. **"Error loading database"**
   - Verify DuckDB schema matches expected columns
   - Check DuckDB file isn't corrupted
   - Ensure DuckDB.NET package is installed

3. **Results not refreshing**
   - Check browser console for errors
   - Verify `RefreshItems()` is being called
   - Check database connection isn't locked

### Database Schema Reference
Expected DuckDB table structure:
```sql
CREATE TABLE search_results (
    seed VARCHAR PRIMARY KEY,
    score INTEGER,
    natural_negative_jokers INTEGER,
    desired_negative_jokers INTEGER,
    found_items TEXT, -- JSON string
    timestamp TIMESTAMP
);
```

## Success Criteria
- ✅ Auto-loads results from matching DuckDB files
- ✅ Refresh button re-queries database
- ✅ Sortable, filterable results grid
- ✅ Export functionality works
- ✅ Smooth transitions and loading states
- ✅ Handles missing databases gracefully
- ✅ Responsive layout on all screen sizes

## Next Steps for v0.4.0
With search results now visible, v0.4.0 could add:
- **LiveSearchWidget**: Real-time search progress
- **SearchControlWidget**: Start/stop/pause controls
- **SearchHistoryWidget**: Track all searches
- **ResultDetailsWidget**: Expanded view of a single result

The widget architecture continues to shine! 🚀