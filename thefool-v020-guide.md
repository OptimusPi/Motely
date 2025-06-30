# TheFool v0.2.0 - Widget Refactor Implementation Guide

## Overview
Transform TheFool's monolithic UI into modular, reusable Blazor components (widgets) while maintaining 100% existing functionality. This guide is designed for AI coding assistants with file system access.

## Prerequisites
- Working TheFool v0.1.0A installation
- .NET 8+ SDK installed
- MudBlazor package already configured
- Basic understanding of Blazor components

## Project Structure After v0.2.0
```
TheFool/
├── Pages/
│   └── Index.razor (simplified orchestrator)
├── Widgets/
│   ├── FileLoaderSaverWidget.razor
│   ├── FilterConfigViewWidget.razor
│   └── FilterConfigEditorWidget.razor
├── Models/
│   └── FilterConfig.cs (unchanged)
└── wwwroot/
    └── js/
        └── fileDownload.js (unchanged)
```

## Step-by-Step Implementation

### Step 1: Create Widgets Folder
```bash
mkdir Widgets
```

### Step 2: Extract FileLoaderSaverWidget

**File: `Widgets/FileLoaderSaverWidget.razor`**

```razor
@using TheFool.Models
@using System.Text.Json
@inject IJSRuntime JS
@inject ISnackbar Snackbar

<MudPaper Class="pa-3 mb-3" Elevation="2">
    <MudGrid AlignItems="Center">
        <MudItem xs="12" sm="6">
            <MudTextField @bind-Value="ConfigName" 
                         Label="Configuration Name" 
                         Variant="Variant.Outlined"
                         Immediate="true"
                         OnDebounceIntervalElapsed="HandleNameChange" />
        </MudItem>
        <MudItem xs="12" sm="6">
            <MudStack Row="true" Justify="Justify.FlexEnd" Spacing="2">
                <MudTooltip Text="Mark as Favorite">
                    <MudIconButton Icon="@GetFavoriteIcon()" 
                                  Color="@GetFavoriteColor()"
                                  OnClick="ToggleFavorite" />
                </MudTooltip>
                
                <MudFileUpload T="IBrowserFile" FilesChanged="LoadConfig" Accept=".json">
                        <MudButton HtmlTag="label"
                                  Variant="Variant.Outlined"
                                  Color="Color.Primary"
                                  StartIcon="@Icons.Material.Filled.Upload"
                                  Size="Size.Small"
                                  for="@context">
                            Load
                        </MudButton>
                </MudFileUpload>
                
                <MudButton Variant="Variant.Outlined" 
                          Color="Color.Secondary"
                          StartIcon="@Icons.Material.Filled.Save"
                          Size="Size.Small"
                          OnClick="SaveConfig"
                          Disabled="@(!HasChanges)">
                    Save
                </MudButton>
                
                <MudButton Variant="Variant.Outlined" 
                          Color="Color.Tertiary"
                          StartIcon="@Icons.Material.Filled.SaveAs"
                          Size="Size.Small"
                          OnClick="SaveAsConfig">
                    Save As
                </MudButton>
            </MudStack>
        </MudItem>
    </MudGrid>
    
    @if (IsFavorite)
    {
        <MudChip Icon="@Icons.Material.Filled.Star" 
                Color="Color.Warning" 
                Size="Size.Small"
                Class="mt-2">
            Favorite Configuration
        </MudChip>
    }
</MudPaper>

@code {
    [Parameter] public string ConfigName { get; set; } = "My Filter";
    [Parameter] public EventCallback<string> ConfigNameChanged { get; set; }
    [Parameter] public EventCallback<FilterConfig> OnConfigLoaded { get; set; }
    [Parameter] public EventCallback<string> OnSaveRequested { get; set; }
    [Parameter] public Func<FilterConfig> GetCurrentConfig { get; set; }
    
    private bool IsFavorite = false;
    private bool HasChanges = false;
    private string OriginalName;

    protected override void OnInitialized()
    {
        OriginalName = ConfigName;
        LoadFavoriteStatus();
    }

    private async Task HandleNameChange(string newName)
    {
        HasChanges = newName != OriginalName;
        await ConfigNameChanged.InvokeAsync(newName);
    }

    private async Task LoadConfig(IBrowserFile file)
    {
        try
        {
            using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            
            var options = new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            };
            var config = JsonSerializer.Deserialize<FilterConfig>(json, options);
            
            if (config != null)
            {
                ConfigName = config.name;
                OriginalName = config.name;
                HasChanges = false;
                await ConfigNameChanged.InvokeAsync(ConfigName);
                await OnConfigLoaded.InvokeAsync(config);
                
                Snackbar.Add($"Loaded: {config.name}", Severity.Success);
                LoadFavoriteStatus();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to load: {ex.Message}", Severity.Error);
        }
    }

    private async Task SaveConfig()
    {
        await SaveToFile(ConfigName);
        OriginalName = ConfigName;
        HasChanges = false;
    }

    private async Task SaveAsConfig()
    {
        // In a real app, show a dialog for new name
        var newName = $"{ConfigName}_copy";
        await SaveToFile(newName);
    }

    private async Task SaveToFile(string filename)
    {
        try
        {
            var config = GetCurrentConfig?.Invoke();
            if (config == null) return;

            // Update the name in config
            var updatedConfig = config with { name = filename };
            
            var json = JsonSerializer.Serialize(updatedConfig, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            await JS.InvokeVoidAsync("downloadFile", $"{filename}.json", json);
            await OnSaveRequested.InvokeAsync(filename);
            
            Snackbar.Add($"Saved: {filename}.json", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Save failed: {ex.Message}", Severity.Error);
        }
    }

    private void ToggleFavorite()
    {
        IsFavorite = !IsFavorite;
        SaveFavoriteStatus();
        var message = IsFavorite ? "Added to favorites" : "Removed from favorites";
        Snackbar.Add(message, Severity.Info);
    }

    private void LoadFavoriteStatus()
    {
        // In real app, load from localStorage or user preferences
        IsFavorite = ConfigName.Contains("favorite", StringComparison.OrdinalIgnoreCase);
    }

    private void SaveFavoriteStatus()
    {
        // In real app, save to localStorage or user preferences
    }

    private string GetFavoriteIcon() => IsFavorite ? Icons.Material.Filled.Star : Icons.Material.Outlined.StarBorder;
    private Color GetFavoriteColor() => IsFavorite ? Color.Warning : Color.Default;
}
```

### Step 3: Create FilterConfigViewWidget

**File: `Widgets/FilterConfigViewWidget.razor`**

```razor
@using TheFool.Models

<MudPaper Class="pa-4" Elevation="2">
    <MudText Typo="Typo.h6" Class="mb-3">
        <MudIcon Icon="@Icons.Material.Filled.Settings" Class="mr-2" />
        Current Configuration
    </MudText>
    
    <MudSimpleTable Hover="true" Bordered="true" Dense="true">
        <tbody>
            <tr>
                <td><strong>Author</strong></td>
                <td>@Config.author</td>
            </tr>
            <tr>
                <td><strong>Description</strong></td>
                <td>@Config.description</td>
            </tr>
            <tr>
                <td><strong>Deck</strong></td>
                <td>
                    <MudChip Size="Size.Small" Color="Color.Primary">
                        @Config.filter_config.deck
                    </MudChip>
                </td>
            </tr>
            <tr>
                <td><strong>Stake</strong></td>
                <td>
                    <MudChip Size="Size.Small" Color="Color.Secondary">
                        @Config.filter_config.stake
                    </MudChip>
                </td>
            </tr>
            <tr>
                <td><strong>Search Range</strong></td>
                <td>Antes @Config.filter_config.minSearchAnte - @Config.filter_config.maxSearchAnte</td>
            </tr>
            <tr>
                <td><strong>Scoring</strong></td>
                <td>
                    @if (Config.filter_config.scoreNaturalNegatives)
                    {
                        <MudChip Size="Size.Small" Color="Color.Success" Class="ma-1">
                            Natural Negatives
                        </MudChip>
                    }
                    @if (Config.filter_config.scoreDesiredNegatives)
                    {
                        <MudChip Size="Size.Small" Color="Color.Info" Class="ma-1">
                            Desired Negatives
                        </MudChip>
                    }
                </td>
            </tr>
        </tbody>
    </MudSimpleTable>
    
    <MudGrid Class="mt-3">
        <MudItem xs="6">
            <MudPaper Class="pa-2" Elevation="0" Style="background-color: var(--mud-palette-background-grey);">
                <MudText Typo="Typo.subtitle2" Color="Color.Primary">
                    <MudIcon Icon="@Icons.Material.Filled.PriorityHigh" Size="Size.Small" />
                    Needs: @Config.filter_config.numNeeds
                </MudText>
            </MudPaper>
        </MudItem>
        <MudItem xs="6">
            <MudPaper Class="pa-2" Elevation="0" Style="background-color: var(--mud-palette-background-grey);">
                <MudText Typo="Typo.subtitle2" Color="Color.Secondary">
                    <MudIcon Icon="@Icons.Material.Filled.FavoriteBorder" Size="Size.Small" />
                    Wants: @Config.filter_config.numWants
                </MudText>
            </MudPaper>
        </MudItem>
    </MudGrid>
    
    @if (ShowLaunchButton)
    {
        <MudButton Variant="Variant.Filled" 
                  Color="Color.Primary" 
                  Size="Size.Large"
                  FullWidth="true"
                  Class="mt-4"
                  Style="height: 60px; font-size: 1.5rem; font-weight: bold;"
                  OnClick="OnLaunchSearch">
            🔥 LET JIMBO COOK! 🔥
        </MudButton>
    }
</MudPaper>

@code {
    [Parameter] public FilterConfig Config { get; set; }
    [Parameter] public bool ShowLaunchButton { get; set; } = true;
    [Parameter] public EventCallback OnLaunchSearch { get; set; }
}
```

### Step 4: Create FilterConfigEditorWidget

**File: `Widgets/FilterConfigEditorWidget.razor`**

```razor
@using TheFool.Models

<MudPaper Class="pa-4" Elevation="2">
    <MudTabs Elevation="0" 
             ApplyEffectsToContainer="true" 
             Rounded="true" 
             PanelClass="pa-4"
             @bind-ActivePanelIndex="ActiveTabIndex">
        
        <!-- Basic Settings Tab -->
        <MudTabPanel Text="Basic Settings" Icon="@Icons.Material.Filled.Settings">
            <MudGrid>
                <MudItem xs="12">
                    <MudTextField @bind-Value="EditableConfig.description" 
                                 Label="Description" 
                                 Lines="2"
                                 Variant="Variant.Outlined"
                                 Class="mb-3" />
                </MudItem>
                
                <MudItem xs="12" sm="6">
                    <MudTextField @bind-Value="EditableConfig.author" 
                                 Label="Author" 
                                 Variant="Variant.Outlined"
                                 Class="mb-3" />
                </MudItem>
                
                <MudItem xs="12" sm="6">
                    <MudTextField @bind-Value="KeywordsText" 
                                 Label="Keywords (comma-separated)" 
                                 Variant="Variant.Outlined"
                                 Class="mb-3" />
                </MudItem>
                
                <MudItem xs="12" sm="6">
                    <MudSelect @bind-Value="EditableConfig.filter_config.deck" 
                              Label="Deck" 
                              Variant="Variant.Outlined"
                              Class="mb-3">
                        @foreach (var deck in GameData.AvailableDecks)
                        {
                            <MudSelectItem Value="@deck">@deck</MudSelectItem>
                        }
                    </MudSelect>
                </MudItem>
                
                <MudItem xs="12" sm="6">
                    <MudSelect @bind-Value="EditableConfig.filter_config.stake" 
                              Label="Stake" 
                              Variant="Variant.Outlined"
                              Class="mb-3">
                        @foreach (var stake in GameData.AvailableStakes)
                        {
                            <MudSelectItem Value="@stake">@stake</MudSelectItem>
                        }
                    </MudSelect>
                </MudItem>
                
                <MudItem xs="12">
                    <MudText Typo="Typo.subtitle2" Class="mb-2">Ante Range</MudText>
                    <MudSlider @bind-Value="AnteRange" 
                              Min="1" Max="8" 
                              Step="1"
                              TickMarks="true"
                              TickMarkLabels="@(new string[] {"1","2","3","4","5","6","7","8"})" />
                </MudItem>
                
                <MudItem xs="12" sm="6">
                    <MudCheckBox @bind-Checked="EditableConfig.filter_config.scoreNaturalNegatives" 
                                Label="Score Natural Negatives" 
                                Color="Color.Success" />
                </MudItem>
                
                <MudItem xs="12" sm="6">
                    <MudCheckBox @bind-Checked="EditableConfig.filter_config.scoreDesiredNegatives" 
                                Label="Score Desired Negatives" 
                                Color="Color.Info" />
                </MudItem>
            </MudGrid>
        </MudTabPanel>
        
        <!-- Needs Tab -->
        <MudTabPanel Text="@($"Needs ({EditableConfig.filter_config.Needs.Count})")" 
                     Icon="@Icons.Material.Filled.PriorityHigh"
                     BadgeData="@EditableConfig.filter_config.Needs.Count"
                     BadgeColor="Color.Primary">
            <MudButton Color="Color.Primary" 
                      Variant="Variant.Filled" 
                      StartIcon="@Icons.Material.Filled.Add"
                      Class="mb-3"
                      OnClick="() => OpenConditionDialog(true)">
                Add Need
            </MudButton>
            
            <MudStack Spacing="2">
                @foreach (var need in EditableConfig.filter_config.Needs)
                {
                    <ConditionCard Condition="need" 
                                 OnRemove="() => RemoveCondition(need, true)"
                                 OnEdit="() => EditCondition(need, true)" />
                }
            </MudStack>
            
            @if (!EditableConfig.filter_config.Needs.Any())
            {
                <MudText Typo="Typo.body2" Color="Color.Tertiary" Align="Align.Center" Class="mt-4">
                    No needs configured. Add conditions that MUST be met.
                </MudText>
            }
        </MudTabPanel>
        
        <!-- Wants Tab -->
        <MudTabPanel Text="@($"Wants ({EditableConfig.filter_config.Wants.Count})")" 
                     Icon="@Icons.Material.Filled.FavoriteBorder"
                     BadgeData="@EditableConfig.filter_config.Wants.Count"
                     BadgeColor="Color.Secondary">
            <MudButton Color="Color.Secondary" 
                      Variant="Variant.Filled" 
                      StartIcon="@Icons.Material.Filled.Add"
                      Class="mb-3"
                      OnClick="() => OpenConditionDialog(false)">
                Add Want
            </MudButton>
            
            <MudStack Spacing="2">
                @foreach (var want in EditableConfig.filter_config.Wants)
                {
                    <ConditionCard Condition="want" 
                                 OnRemove="() => RemoveCondition(want, false)"
                                 OnEdit="() => EditCondition(want, false)" />
                }
            </MudStack>
            
            @if (!EditableConfig.filter_config.Wants.Any())
            {
                <MudText Typo="Typo.body2" Color="Color.Tertiary" Align="Align.Center" Class="mt-4">
                    No wants configured. Add optional conditions for scoring.
                </MudText>
            }
        </MudTabPanel>
    </MudTabs>
</MudPaper>

@code {
    [Parameter] public FilterConfig Config { get; set; }
    [Parameter] public EventCallback<FilterConfig> ConfigChanged { get; set; }
    [Inject] IDialogService DialogService { get; set; }

    private FilterConfig EditableConfig;
    private int ActiveTabIndex = 0;
    private double[] AnteRange = new[] { 1.0, 8.0 };
    private string KeywordsText = "";

    protected override void OnParametersSet()
    {
        // Create a deep copy for editing
        EditableConfig = DeepCopy(Config);
        KeywordsText = string.Join(", ", EditableConfig.keywords);
        AnteRange = new[] { 
            (double)EditableConfig.filter_config.minSearchAnte, 
            (double)EditableConfig.filter_config.maxSearchAnte 
        };
    }

    private async Task NotifyConfigChanged()
    {
        // Update derived values
        EditableConfig.keywords = KeywordsText.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                              .Select(k => k.Trim())
                                              .ToList();
        EditableConfig.filter_config.minSearchAnte = (int)AnteRange[0];
        EditableConfig.filter_config.maxSearchAnte = (int)AnteRange[1];
        EditableConfig.filter_config.numNeeds = EditableConfig.filter_config.Needs.Count;
        EditableConfig.filter_config.numWants = EditableConfig.filter_config.Wants.Count;
        
        await ConfigChanged.InvokeAsync(EditableConfig);
    }

    private void RemoveCondition(FilterCondition condition, bool isNeed)
    {
        if (isNeed)
            EditableConfig.filter_config.Needs.Remove(condition);
        else
            EditableConfig.filter_config.Wants.Remove(condition);
        
        _ = NotifyConfigChanged();
    }

    private async Task OpenConditionDialog(bool isNeed)
    {
        var parameters = new DialogParameters
        {
            ["IsNeed"] = isNeed,
            ["ExistingCondition"] = null
        };
        
        var dialog = await DialogService.ShowAsync<ConditionEditorDialog>(
            isNeed ? "Add Need" : "Add Want", 
            parameters,
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });
        
        var result = await dialog.Result;
        if (!result.Cancelled && result.Data is FilterCondition newCondition)
        {
            if (isNeed)
                EditableConfig.filter_config.Needs.Add(newCondition);
            else
                EditableConfig.filter_config.Wants.Add(newCondition);
            
            await NotifyConfigChanged();
        }
    }

    private async Task EditCondition(FilterCondition condition, bool isNeed)
    {
        var parameters = new DialogParameters
        {
            ["IsNeed"] = isNeed,
            ["ExistingCondition"] = condition
        };
        
        var dialog = await DialogService.ShowAsync<ConditionEditorDialog>(
            "Edit Condition", 
            parameters,
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });
        
        var result = await dialog.Result;
        if (!result.Cancelled && result.Data is FilterCondition editedCondition)
        {
            var list = isNeed ? EditableConfig.filter_config.Needs : EditableConfig.filter_config.Wants;
            var index = list.IndexOf(condition);
            if (index >= 0)
            {
                list[index] = editedCondition;
                await NotifyConfigChanged();
            }
        }
    }

    private FilterConfig DeepCopy(FilterConfig original)
    {
        // Simple deep copy using JSON serialization
        var json = System.Text.Json.JsonSerializer.Serialize(original);
        return System.Text.Json.JsonSerializer.Deserialize<FilterConfig>(json);
    }

    // Static game data
    private static class GameData
    {
        public static readonly List<string> AvailableDecks = new()
        {
            "RedDeck", "BlueDeck", "YellowDeck", "GreenDeck", "BlackDeck",
            "MagicDeck", "NebuladDeck", "GhostDeck", "AbandonedDeck", 
            "CheckeredDeck", "ZodiacDeck", "PaintedDeck", "AnaglyphDeck",
            "PlasmaDeck", "ErraticdDeck", "ChallengeDeck"
        };

        public static readonly List<string> AvailableStakes = new()
        {
            "WhiteStake", "RedStake", "GreenStake", "BlackStake",
            "BlueStake", "PurpleStake", "OrangeStake", "GoldStake"
        };
    }
}
```

### Step 5: Update ConditionCard Component

**File: `Widgets/ConditionCard.razor`**

```razor
@using TheFool.Models

<MudCard Class="condition-card" Elevation="1">
    <MudCardContent Class="pb-2 pt-2">
        <MudGrid AlignItems="Center">
            <MudItem xs="10">
                <MudStack Spacing="0">
                    <MudText Typo="Typo.body2">
                        <MudIcon Icon="@GetConditionIcon()" Size="Size.Small" Class="mr-1" />
                        <strong>@Condition.type:</strong> @Condition.value
                        @if (!string.IsNullOrEmpty(Condition.edition))
                        {
                            <MudChip Size="Size.Small" Color="Color.Primary" Class="ml-2">
                                @Condition.edition
                            </MudChip>
                        }
                    </MudText>
                    
                    <MudStack Row="true" Spacing="1" Class="mt-1">
                        <MudText Typo="Typo.caption" Color="Color.Tertiary">
                            Antes: @string.Join(", ", Condition.searchAntes)
                        </MudText>
                        
                        @if (Condition.jokerstickers?.Any() == true)
                        {
                            <MudText Typo="Typo.caption" Color="Color.Info">
                                | Stickers: @string.Join(", ", Condition.jokerstickers)
                            </MudText>
                        }
                        
                        @if (!string.IsNullOrEmpty(Condition.rank) || !string.IsNullOrEmpty(Condition.suit))
                        {
                            <MudText Typo="Typo.caption" Color="Color.Warning">
                                | @Condition.rank @Condition.suit
                            </MudText>
                        }
                    </MudStack>
                </MudStack>
            </MudItem>
            
            <MudItem xs="2">
                <MudStack Row="true" Justify="Justify.FlexEnd" Spacing="1">
                    <MudIconButton Icon="@Icons.Material.Filled.Edit" 
                                  Color="Color.Info" 
                                  Size="Size.Small"
                                  OnClick="OnEdit" />
                    <MudIconButton Icon="@Icons.Material.Filled.Delete" 
                                  Color="Color.Error" 
                                  Size="Size.Small"
                                  OnClick="OnRemove" />
                </MudStack>
            </MudItem>
        </MudGrid>
    </MudCardContent>
</MudCard>

<style>
    .condition-card {
        transition: all 0.2s ease-in-out;
    }
    
    .condition-card:hover {
        transform: translateY(-2px);
        box-shadow: 0 4px 20px 0 rgba(0,0,0,0.12) !important;
    }
</style>

@code {
    [Parameter] public FilterCondition Condition { get; set; }
    [Parameter] public EventCallback OnRemove { get; set; }
    [Parameter] public EventCallback OnEdit { get; set; }

    private string GetConditionIcon()
    {
        return Condition.type switch
        {
            "Joker" => Icons.Material.Filled.Style,
            "Tarot" => Icons.Material.Filled.AutoAwesome,
            "Spectral" => Icons.Material.Filled.Stars,
            "Voucher" => Icons.Material.Filled.LocalOffer,
            "PlayingCard" => Icons.Material.Filled.PlayingCards,
            "Boss" => Icons.Material.Filled.Warning,
            "SmallBlindTag" or "BigBlindTag" => Icons.Material.Filled.Label,
            _ => Icons.Material.Filled.Help
        };
    }
}
```

### Step 6: Update Index.razor to Use Widgets

**File: `Pages/Index.razor`**

```razor
@page "/"
@using TheFool.Models
@using TheFool.Widgets
@inject ISnackbar Snackbar

<MudContainer MaxWidth="MaxWidth.Large" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4" Align="Align.Center">
        🃏 TheFool - Balatro Seed Finder
    </MudText>
    
    <!-- File Management Widget -->
    <FileLoaderSaverWidget ConfigName="@CurrentConfig.name"
                          ConfigNameChanged="@((name) => CurrentConfig = CurrentConfig with { name = name })"
                          OnConfigLoaded="LoadConfiguration"
                          OnSaveRequested="HandleSave"
                          GetCurrentConfig="@(() => CurrentConfig)" />
    
    <MudGrid>
        <!-- Left Panel - Config Display -->
        <MudItem xs="12" md="4">
            <FilterConfigViewWidget Config="@CurrentConfig"
                                   ShowLaunchButton="true"
                                   OnLaunchSearch="StartSearch" />
        </MudItem>
        
        <!-- Right Panel - Config Editor -->
        <MudItem xs="12" md="8">
            <FilterConfigEditorWidget Config="@CurrentConfig"
                                     ConfigChanged="UpdateConfiguration" />
        </MudItem>
    </MudGrid>
</MudContainer>

@code {
    private FilterConfig CurrentConfig = CreateDefaultConfig();

    private void LoadConfiguration(FilterConfig config)
    {
        CurrentConfig = config;
        StateHasChanged();
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

### Step 7: Create Condition Editor Dialog (Bonus Widget!)

**File: `Widgets/ConditionEditorDialog.razor`**

```razor
@using TheFool.Models

<MudDialog>
    <DialogContent>
        <MudGrid>
            <MudItem xs="12">
                <MudSelect @bind-Value="ConditionType" 
                          Label="Type" 
                          Variant="Variant.Outlined"
                          Class="mb-3">
                    <MudSelectItem Value="@("Joker")">Joker</MudSelectItem>
                    <MudSelectItem Value="@("Tarot")">Tarot</MudSelectItem>
                    <MudSelectItem Value="@("Spectral")">Spectral</MudSelectItem>
                    <MudSelectItem Value="@("Voucher")">Voucher</MudSelectItem>
                    <MudSelectItem Value="@("PlayingCard")">Playing Card</MudSelectItem>
                    <MudSelectItem Value="@("Boss")">Boss</MudSelectItem>
                    <MudSelectItem Value="@("SmallBlindTag")">Small Blind Tag</MudSelectItem>
                    <MudSelectItem Value="@("BigBlindTag")">Big Blind Tag</MudSelectItem>
                    <MudSelectItem Value="@("SoulJoker")">Soul Joker</MudSelectItem>
                </MudSelect>
            </MudItem>
            
            @* Dynamic fields based on type *@
            @switch (ConditionType)
            {
                case "PlayingCard":
                    <MudItem xs="6">
                        <MudSelect @bind-Value="Rank" Label="Rank" Variant="Variant.Outlined">
                            @foreach (var rank in new[] { "Ace", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Jack", "Queen", "King" })
                            {
                                <MudSelectItem Value="@rank">@rank</MudSelectItem>
                            }
                        </MudSelect>
                    </MudItem>
                    <MudItem xs="6">
                        <MudSelect @bind-Value="Suit" Label="Suit" Variant="Variant.Outlined">
                            @foreach (var suit in new[] { "Heart", "Diamond", "Club", "Spade" })
                            {
                                <MudSelectItem Value="@suit">@suit</MudSelectItem>
                            }
                        </MudSelect>
                    </MudItem>
                    break;
                    
                default:
                    <MudItem xs="12">
                        <MudTextField @bind-Value="Value" 
                                     Label="Value/Name" 
                                     Variant="Variant.Outlined"
                                     Class="mb-3" />
                    </MudItem>
                    break;
            }
            
            @if (ShowEditionField())
            {
                <MudItem xs="12">
                    <MudSelect @bind-Value="Edition" Label="Edition" Variant="Variant.Outlined" Class="mb-3">
                        <MudSelectItem Value="@("None")">None</MudSelectItem>
                        <MudSelectItem Value="@("Foil")">Foil</MudSelectItem>
                        <MudSelectItem Value="@("Holographic")">Holographic</MudSelectItem>
                        <MudSelectItem Value="@("Polychrome")">Polychrome</MudSelectItem>
                        <MudSelectItem Value="@("Negative")">Negative</MudSelectItem>
                    </MudSelect>
                </MudItem>
            }
            
            <MudItem xs="12">
                <MudText Typo="Typo.subtitle2" Class="mb-2">Search Antes</MudText>
                <MudChipSet @bind-SelectedChips="SelectedAntes" MultiSelection="true" Filter="true">
                    @for (int i = 1; i <= 8; i++)
                    {
                        var ante = i;
                        <MudChip Text="@ante.ToString()" Value="@ante" Color="Color.Primary" />
                    }
                </MudChipSet>
            </MudItem>
        </MudGrid>
    </DialogContent>
    
    <DialogActions>
        <MudButton OnClick="Cancel">Cancel</MudButton>
        <MudButton Color="Color.Primary" 
                  Variant="Variant.Filled" 
                  OnClick="Submit"
                  Disabled="@(!IsValid())">
            @(ExistingCondition == null ? "Add" : "Update")
        </MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] MudDialogInstance MudDialog { get; set; }
    [Parameter] public bool IsNeed { get; set; }
    [Parameter] public FilterCondition ExistingCondition { get; set; }

    private string ConditionType = "Joker";
    private string Value = "";
    private string Edition = "None";
    private string Rank = "Ace";
    private string Suit = "Heart";
    private MudChip[] SelectedAntes = Array.Empty<MudChip>();

    protected override void OnInitialized()
    {
        if (ExistingCondition != null)
        {
            ConditionType = ExistingCondition.type;
            Value = ExistingCondition.value ?? "";
            Edition = ExistingCondition.edition ?? "None";
            Rank = ExistingCondition.rank ?? "Ace";
            Suit = ExistingCondition.suit ?? "Heart";
            
            // Pre-select antes
            SelectedAntes = ExistingCondition.searchAntes
                .Select(a => new MudChip { Text = a.ToString(), Value = a })
                .ToArray();
        }
    }

    private bool ShowEditionField() => 
        ConditionType is "Joker" or "PlayingCard" or "SoulJoker";

    private bool IsValid()
    {
        if (SelectedAntes.Length == 0) return false;
        
        return ConditionType switch
        {
            "PlayingCard" => !string.IsNullOrEmpty(Rank) && !string.IsNullOrEmpty(Suit),
            _ => !string.IsNullOrEmpty(Value)
        };
    }

    private void Submit()
    {
        var searchAntes = SelectedAntes
            .Select(chip => (int)chip.Value)
            .OrderBy(a => a)
            .ToList();

        var condition = new FilterCondition(
            type: ConditionType,
            value: ConditionType == "PlayingCard" ? null : Value,
            searchAntes: searchAntes,
            edition: ShowEditionField() ? Edition : null,
            rank: ConditionType == "PlayingCard" ? Rank : null,
            suit: ConditionType == "PlayingCard" ? Suit : null
        );

        MudDialog.Close(DialogResult.Ok(condition));
    }

    private void Cancel() => MudDialog.Cancel();
}
```

## Testing Instructions for AI Agents

### 1. Verify File Structure
```bash
# Ensure all widget files exist
ls Widgets/
# Should show: FileLoaderSaverWidget.razor, FilterConfigViewWidget.razor, FilterConfigEditorWidget.razor, ConditionCard.razor, ConditionEditorDialog.razor
```

### 2. Build and Run
```bash
dotnet build
# Should complete with 0 errors

dotnet run
# Navigate to https://localhost:5001
```

### 3. Test Each Widget
1. **FileLoaderSaverWidget**: 
   - Change config name → should update everywhere
   - Click favorite → star should toggle
   - Save → should download JSON file
   
2. **FilterConfigViewWidget**:
   - Should display all current settings
   - Big button should be clickable
   
3. **FilterConfigEditorWidget**:
   - Switch between tabs
   - Add/remove conditions
   - Edit existing conditions

### 4. Verify JSON Output
Save a config and verify it matches the schema exactly:
```json
{
  "name": "Test Config",
  "description": "...",
  "author": "...",
  "keywords": ["..."],
  "filter_config": {
    "numNeeds": 0,
    "numWants": 0,
    "Needs": [],
    "Wants": [],
    // ... rest of schema
  }
}
```

## Success Criteria
- ✅ All existing v0.1.0A functionality preserved
- ✅ Code is now modular and widget-based
- ✅ Each widget is self-contained and reusable
- ✅ Easy to add new widgets in v0.3.0
- ✅ JSON save/load works exactly as before
- ✅ UI looks and feels the same (or better!)

## Next Steps for v0.3.0
With this widget architecture, adding new features is as simple as:
1. Create a new widget file
2. Drop it into the page
3. Connect the events

Examples:
- `SearchResultsWidget.razor` - Display search results
- `SearchProgressWidget.razor` - Show real-time progress
- `DeviceSelectionWidget.razor` - Choose GPU/CPU
- `FilterLibraryWidget.razor` - Browse saved filters

The foundation is now SOLID and SIMPLE! 🚀