# TheFool MVP 0.1.0A - MINIMAL BLAZOR UI

## Why Blazor?
- ONE codebase in C# for desktop AND web
- No JavaScript bullshit
- Works on Windows/Mac/Linux/Web
- Can compile to desktop app with Blazor Hybrid

## Project Setup (5 minutes)

```bash
# Create the project
dotnet new blazorserver -n TheFool
cd TheFool

# Add packages
dotnet add package System.Text.Json
dotnet add package MudBlazor
```

## 1. The ENTIRE Filter Model (Models/FilterConfig.cs)

```csharp
namespace TheFool.Models;

public record FilterConfig(
    string name,
    string description,
    string author,
    List<string> keywords,
    FilterSettings filter_config
);

public record FilterSettings(
    int numNeeds,
    int numWants,
    List<FilterCondition> Needs,
    List<FilterCondition> Wants,
    int minSearchAnte,
    int maxSearchAnte,
    string deck,
    string stake,
    bool scoreNaturalNegatives,
    bool scoreDesiredNegatives
);

public record FilterCondition(
    string type,
    string value,
    List<int> searchAntes,
    string? edition = null,
    List<string>? jokerstickers = null,
    string? rank = null,
    string? suit = null,
    string? enchantment = null,
    string? chip = null
);
```

## 2. The ENTIRE App Layout (Shared/MainLayout.razor)

```razor
@inherits LayoutComponentBase

<MudThemeProvider Theme="@_darkTheme" />
<MudDialogProvider />
<MudSnackbarProvider />

<MudLayout>
    <MudAppBar Elevation="1">
        <MudText Typo="Typo.h5">🃏 TheFool - Balatro Seed Finder</MudText>
        <MudSpacer />
        <MudText>MVP 0.1.0A</MudText>
    </MudAppBar>
    <MudMainContent>
        @Body
    </MudMainContent>
</MudLayout>

@code {
    private MudTheme _darkTheme = new()
    {
        Palette = new PaletteDark()
        {
            Primary = "#FF4444",
            Secondary = "#FF8844",
            Background = "#1a1a1a",
            Surface = "#2a2a2a",
            AppbarBackground = "#2a2a2a",
        }
    };
}
```

## 3. The ENTIRE UI (Pages/Index.razor)

```razor
@page "/"
@using TheFool.Models
@using System.Text.Json
@inject IJSRuntime JS
@inject ISnackbar Snackbar

<MudContainer MaxWidth="MaxWidth.Large" Class="mt-4">
    <MudGrid>
        <!-- Left Panel - Config Editor -->
        <MudItem xs="12" md="5">
            <MudPaper Class="pa-4" Elevation="3">
                <MudText Typo="Typo.h6" Class="mb-4">Filter Configuration</MudText>
                
                <!-- Load/Save Buttons -->
                <MudStack Row="true" Class="mb-4">
                    <MudFileUpload T="IBrowserFile" FilesChanged="LoadConfig" Accept=".json">
                            <MudButton HtmlTag="label"
                                      Variant="Variant.Outlined"
                                      Color="Color.Primary"
                                      StartIcon="@Icons.Material.Filled.Upload"
                                      for="@context">
                                Load JSON
                            </MudButton>
                    </MudFileUpload>
                    
                    <MudButton Variant="Variant.Outlined" 
                              Color="Color.Secondary"
                              StartIcon="@Icons.Material.Filled.Download"
                              OnClick="SaveConfig">
                        Save JSON
                    </MudButton>
                </MudStack>

                <!-- Basic Info -->
                <MudTextField @bind-Value="ConfigName" Label="Name" Class="mb-2" />
                <MudTextField @bind-Value="ConfigDescription" Label="Description" Class="mb-2" />
                <MudTextField @bind-Value="ConfigAuthor" Label="Author" Class="mb-4" />

                <!-- Deck Settings -->
                <MudSelect @bind-Value="SelectedDeck" Label="Deck" Class="mb-2">
                    @foreach (var deck in AvailableDecks)
                    {
                        <MudSelectItem Value="@deck">@deck</MudSelectItem>
                    }
                </MudSelect>

                <MudSelect @bind-Value="SelectedStake" Label="Stake" Class="mb-4">
                    @foreach (var stake in AvailableStakes)
                    {
                        <MudSelectItem Value="@stake">@stake</MudSelectItem>
                    }
                </MudSelect>

                <!-- Checkboxes -->
                <MudCheckBox @bind-Checked="ScoreNaturalNegatives" 
                            Label="Score Natural Negatives" 
                            Color="Color.Primary" />
                <MudCheckBox @bind-Checked="ScoreDesiredNegatives" 
                            Label="Score Desired Negatives" 
                            Color="Color.Primary" 
                            Class="mb-4" />

                <!-- THE BIG BUTTON -->
                <MudButton Variant="Variant.Filled" 
                          Color="Color.Primary" 
                          Size="Size.Large"
                          FullWidth="true"
                          Class="mt-4"
                          Style="height: 60px; font-size: 1.5rem; font-weight: bold;"
                          OnClick="StartSearch">
                    🔥 LET JIMBO COOK! 🔥
                </MudButton>
            </MudPaper>
        </MudItem>

        <!-- Right Panel - Needs/Wants -->
        <MudItem xs="12" md="7">
            <MudPaper Class="pa-4" Elevation="3">
                <MudTabs Elevation="0" ApplyEffectsToContainer="true">
                    <MudTabPanel Text="@($"Needs ({Needs.Count})")">
                        <MudButton Color="Color.Primary" 
                                  Variant="Variant.Filled" 
                                  Class="mb-3"
                                  OnClick="() => AddCondition(true)">
                            Add Need
                        </MudButton>
                        
                        @foreach (var need in Needs)
                        {
                            <ConditionCard Condition="need" 
                                         OnRemove="() => Needs.Remove(need)" />
                        }
                    </MudTabPanel>
                    
                    <MudTabPanel Text="@($"Wants ({Wants.Count})")">
                        <MudButton Color="Color.Secondary" 
                                  Variant="Variant.Filled" 
                                  Class="mb-3"
                                  OnClick="() => AddCondition(false)">
                            Add Want
                        </MudButton>
                        
                        @foreach (var want in Wants)
                        {
                            <ConditionCard Condition="want" 
                                         OnRemove="() => Wants.Remove(want)" />
                        }
                    </MudTabPanel>
                </MudTabs>
            </MudPaper>
        </MudItem>
    </MudGrid>
</MudContainer>

@code {
    private string ConfigName = "My Filter";
    private string ConfigDescription = "Custom filter configuration";
    private string ConfigAuthor = "Player";
    private string SelectedDeck = "GhostDeck";
    private string SelectedStake = "WhiteStake";
    private bool ScoreNaturalNegatives = true;
    private bool ScoreDesiredNegatives = true;

    private List<FilterCondition> Needs = new();
    private List<FilterCondition> Wants = new();

    private List<string> AvailableDecks = new()
    {
        "RedDeck", "BlueDeck", "YellowDeck", "GreenDeck", "BlackDeck",
        "MagicDeck", "NebuladDeck", "GhostDeck", "AbandonedDeck", 
        "CheckeredDeck", "ZodiacDeck", "PaintedDeck", "AnaglyphDeck",
        "PlasmaDeck", "ErraticdDeck", "ChallengeDeck"
    };

    private List<string> AvailableStakes = new()
    {
        "WhiteStake", "RedStake", "GreenStake", "BlackStake",
        "BlueStake", "PurpleStake", "OrangeStake", "GoldStake"
    };

    private async Task LoadConfig(IBrowserFile file)
    {
        try
        {
            var json = await file.OpenReadStream().ReadToEndAsync();
            var config = JsonSerializer.Deserialize<FilterConfig>(json);
            
            if (config != null)
            {
                ConfigName = config.name;
                ConfigDescription = config.description;
                ConfigAuthor = config.author;
                SelectedDeck = config.filter_config.deck;
                SelectedStake = config.filter_config.stake;
                ScoreNaturalNegatives = config.filter_config.scoreNaturalNegatives;
                ScoreDesiredNegatives = config.filter_config.scoreDesiredNegatives;
                Needs = config.filter_config.Needs;
                Wants = config.filter_config.Wants;
                
                Snackbar.Add("Config loaded successfully!", Severity.Success);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to load config: {ex.Message}", Severity.Error);
        }
    }

    private async Task SaveConfig()
    {
        var config = new FilterConfig(
            ConfigName,
            ConfigDescription,
            ConfigAuthor,
            new List<string> { "FOOL", "SEARCH" },
            new FilterSettings(
                Needs.Count,
                Wants.Count,
                Needs,
                Wants,
                1,
                8,
                SelectedDeck,
                SelectedStake,
                ScoreNaturalNegatives,
                ScoreDesiredNegatives
            )
        );

        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        
        await JS.InvokeVoidAsync("downloadFile", $"{ConfigName}.json", json);
        Snackbar.Add("Config saved!", Severity.Success);
    }

    private void AddCondition(bool isNeed)
    {
        var newCondition = new FilterCondition(
            "Joker",
            "Blueprint",
            new List<int> { 1, 2, 3, 4, 5, 6 },
            "None"
        );

        if (isNeed)
            Needs.Add(newCondition);
        else
            Wants.Add(newCondition);
    }

    private void StartSearch()
    {
        Snackbar.Add("🔥 JIMBO IS COOKING! 🔥", Severity.Info);
        // TODO: Launch the actual search process
    }
}
```

## 4. Condition Card Component (Shared/ConditionCard.razor)

```razor
@using TheFool.Models

<MudCard Class="mb-2">
    <MudCardContent>
        <MudGrid>
            <MudItem xs="10">
                <MudText>
                    <strong>@Condition.type:</strong> @Condition.value
                    @if (!string.IsNullOrEmpty(Condition.edition))
                    {
                        <MudChip Size="Size.Small" Color="Color.Primary">@Condition.edition</MudChip>
                    }
                </MudText>
                <MudText Typo="Typo.caption">
                    Antes: @string.Join(", ", Condition.searchAntes)
                </MudText>
            </MudItem>
            <MudItem xs="2" Class="d-flex justify-end">
                <MudIconButton Icon="@Icons.Material.Filled.Delete" 
                              Color="Color.Error" 
                              Size="Size.Small"
                              OnClick="OnRemove" />
            </MudItem>
        </MudGrid>
    </MudCardContent>
</MudCard>

@code {
    [Parameter] public FilterCondition Condition { get; set; }
    [Parameter] public EventCallback OnRemove { get; set; }
}
```

## 5. JavaScript for File Download (wwwroot/js/fileDownload.js)

```javascript
window.downloadFile = (filename, content) => {
    const blob = new Blob([content], { type: 'application/json' });
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    window.URL.revokeObjectURL(url);
};
```

## 6. Update Program.cs

```csharp
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();

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

## 7. Update _Host.cshtml

```html
@page "/"
@namespace TheFool.Pages
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
@{
    Layout = "_Layout";
}

<component type="typeof(App)" render-mode="ServerPrerendered" />

<script src="_framework/blazor.server.js"></script>
<script src="_content/MudBlazor/MudBlazor.min.js"></script>
<script src="js/fileDownload.js"></script>
```

## THAT'S IT! 

### To Run:
```bash
dotnet run
# Open browser to https://localhost:5001
```

### What This Gives You:
- ✅ Load/Save JSON configs
- ✅ Modern dark UI with MudBlazor
- ✅ Cross-platform (runs anywhere .NET runs)
- ✅ Can deploy as web app
- ✅ MINIMAL code (< 300 lines total)
- ✅ NO ViewModels, NO complexity
- ✅ BIG RED "LET JIMBO COOK!" BUTTON

### To Make Desktop App:
Just wrap it in Blazor Hybrid (MAUI or Photino.Blazor) - same exact code!

### Next Steps for 0.2.0:
- Wire up the button to actually launch MotelySearch
- Add results streaming
- Add condition editor dialog

But this MVP is DONE and SHIPPABLE! 🚀