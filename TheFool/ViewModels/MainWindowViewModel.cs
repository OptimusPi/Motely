using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TheFool.Models;
using TheFool.Services;

namespace TheFool.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IOuijaService _ouijaService;
    private readonly DatabaseService _databaseService;
    private readonly ConfigService _configService;
    private readonly DispatcherTimer _uiUpdateTimer;
    private readonly object _pendingUpdatesLock = new();
    private List<SeedResult> _pendingResults = new();

    [ObservableProperty]
    private string _configName = "Default";

    [ObservableProperty]
    private SearchCriteria _config = new();

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private double _searchProgress;

    [ObservableProperty]
    private string _statusText = "Ready to search";

    [ObservableProperty]
    private string _progressText = "";

    [ObservableProperty]
    private string _runtime = "";

    [ObservableProperty]
    private string _seedsPerSecond = "";

    [ObservableProperty]
    private ObservableCollection<SeedResult> _searchResults = new();

    [ObservableProperty]
    private TextDocument _consoleDocument = new();

    [ObservableProperty]
    private TextDocument _configPreviewDocument = new();

    [ObservableProperty]
    private bool _addAsNeed = true;

    [ObservableProperty]
    private string? _selectedJoker;

    [ObservableProperty]
    private string? _selectedEdition;

    [ObservableProperty]
    private string? _selectedTarot;

    [ObservableProperty]
    private string? _selectedSpectral;

    [ObservableProperty]
    private string? _selectedTag;

    [ObservableProperty]
    private string? _selectedVoucher;

    [ObservableProperty]
    private string _selectedEngine = "Ouija (GPU)";

    [ObservableProperty]
    private int _batchMultiplier = 8;

    [ObservableProperty]
    private int _threadGroups = 64;

    [ObservableProperty]
    private int _cpuThreadCount = 16;

    [ObservableProperty]
    private int _cpuBatchedDigits = 4;

    public List<string> AvailableEngines { get; } = new()
    {
        "Ouija (GPU)",
        "Motely (CPU)"
    };

    public List<int> AvailableBatchMultipliers { get; } = new() { 1, 2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096 };
    public List<int> AvailableThreadGroups { get; } = new() { 32, 64, 128, 256, 512, 1024 };
    public List<int> AvailableCpuThreads { get; } = new() { 1, 2, 4, 8, 12, 16, 24, 32, 48, 64 };
    public List<int> AvailableBatchedDigits { get; } = new() { 1, 2, 4, 8, 16 };

    public bool IsGpuEngine => SelectedEngine == "Ouija (GPU)";
    public bool IsCpuEngine => SelectedEngine == "Motely (CPU)";

    partial void OnSelectedEngineChanged(string value)
    {
        OnPropertyChanged(nameof(IsGpuEngine));
        OnPropertyChanged(nameof(IsCpuEngine));
    }

    [ObservableProperty]
    private string? _selectedRank;

    [ObservableProperty]
    private string? _selectedSuit;

    // Available options for UI
    public List<string> AvailableDecks { get; } = new()
    {
        "🔴 Red Deck", "🔵 Blue Deck", "🟡 Yellow Deck", "🟢 Green Deck", "⚫ Black Deck",
        "✨ Magic Deck", "🌌 Nebula Deck", "👻 Ghost Deck", "🏚️ Abandoned Deck", "♥️♠️ Checkered Deck",
        "♈ Zodiac Deck", "🎨 Painted Deck", "🔴🔵 Anaglyph Deck", "⚡ Plasma Deck", "🎲 Erratic Deck"
    };

    public List<string> AvailableStakes { get; } = new()
    {
        "⚪ White Stake", "🔴 Red Stake", "🟢 Green Stake", "⚫ Black Stake", "🔵 Blue Stake",
        "🟣 Purple Stake", "🟠 Orange Stake", "🏆 Gold Stake"
    };

    public List<string> AllJokers { get; } = new()
    {
        "Joker", "Greedy Joker", "Lusty Joker", "Wrathful Joker", "Gluttonous Joker",
        "Jolly Joker", "Zany Joker", "Mad Joker", "Crazy Joker", "Droll Joker",
        "Sly Joker", "Wily Joker", "Clever Joker", "Devious Joker", "Crafty Joker"
    };

    public List<string> AvailableEditions { get; } = new()
    {
        "No Edition", "✨ Foil", "🎆 Holographic", "🌈 Polychrome", "➖ Negative"
    };

    public List<string> AvailableTarots { get; } = new()
    {
        "The Fool", "The Magician", "The High Priestess", "The Empress", "The Emperor"
    };

    public List<string> AvailableSpectrals { get; } = new()
    {
        "Familiar", "Grim", "Incantation", "Talisman", "Aura", "Wraith", "Sigil"
    };

    public List<string> AvailableTags { get; } = new()
    {
        "Uncommon Tag", "Rare Tag", "Negative Tag", "Foil Tag", "Holographic Tag"
    };

    public List<string> AvailableVouchers { get; } = new()
    {
        "Overstock", "Clearance Sale", "Hone", "Reroll Surplus", "Crystal Ball"
    };

    public List<string> AvailableRanks { get; } = new()
    {
        "2", "3", "4", "5", "6", "7", "8", "9", "10", "Jack", "Queen", "King", "Ace"
    };

    public List<string> AvailableSuits { get; } = new()
    {
        "♠️Spades", "♥️Hearts", "♦️Diamonds", "♣️Clubs"
    };

    [ObservableProperty]
    private ObservableCollection<SearchCriteriaItem> _searchCriteria = new();

    public IEnumerable<SearchCriteriaItem> RequiredItems => SearchCriteria.Where(x => x.IsRequired).OrderBy(x => x.Name);
    public IEnumerable<SearchCriteriaItem> OptionalItems => SearchCriteria.Where(x => !x.IsRequired).OrderBy(x => x.Name);
    public bool HasRequiredItems => SearchCriteria.Any(x => x.IsRequired);
    public bool HasOptionalItems => SearchCriteria.Any(x => !x.IsRequired);
    public int RequiredCount => SearchCriteria.Count(x => x.IsRequired);
    public int OptionalCount => SearchCriteria.Count(x => !x.IsRequired);
    public string ResultsTitle => $"Results ({SearchResults.Count})";
    public bool HasResults => SearchResults.Count > 0;

    public MainWindowViewModel(
        IOuijaService ouijaService,
        DatabaseService databaseService,
        ConfigService configService)
    {
        _ouijaService = ouijaService;
        _databaseService = databaseService;
        _configService = configService;

        // Load configuration
        Config = _configService.CurrentCriteria;

        // Set up throttled UI updates - max 4 updates per second
        _uiUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _uiUpdateTimer.Tick += FlushPendingUpdates;

        // Subscribe to collection changes
        SearchCriteria.CollectionChanged += (s, e) => 
        {
            OnPropertyChanged(nameof(RequiredItems));
            OnPropertyChanged(nameof(OptionalItems));
            OnPropertyChanged(nameof(HasRequiredItems));
            OnPropertyChanged(nameof(HasOptionalItems));
            OnPropertyChanged(nameof(RequiredCount));
            OnPropertyChanged(nameof(OptionalCount));
            // Update search criteria visual state
        };

        // Load existing results
        _ = LoadExistingResults();
    }

    private async Task LoadExistingResults()
    {
        try
        {
            var results = await _databaseService.GetTopResultsAsync(50);
            foreach (var result in results)
            {
                SearchResults.Add(result);
            }
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(ResultsTitle));
        }
        catch
        {
            // Ignore errors loading existing results
        }
    }

    [RelayCommand(CanExecute = nameof(CanStartSearch))]
    private async Task StartSearchAsync()
    {
        IsSearching = true;
        SearchProgress = 0;
        StatusText = "Starting search...";
        _uiUpdateTimer.Start();

        try
        {
            // Save current configuration
            _configService.UpdateSearchCriteria(Config);

            // Convert criteria items to strings for the search
            Config.Needs.Clear();
            Config.Wants.Clear();
            
            foreach (var item in SearchCriteria)
            {
                if (item.IsRequired)
                    Config.Needs.Add(item.GetFilterString());
                else
                    Config.Wants.Add(item.GetFilterString());
            }

            var progress = new Progress<SearchProgress>(OnProgressUpdate);
            await _ouijaService.StartSearchAsync(
                Config, 
                SelectedEngine,
                BatchMultiplier,
                ThreadGroups,
                CpuThreadCount,
                CpuBatchedDigits,
                progress);

            // Save results to database
            if (_pendingResults.Any())
            {
                await _databaseService.BulkInsertResultsAsync(_pendingResults);
                StatusText = $"Search complete. Found {_pendingResults.Count} new results.";
            }
            else
            {
                StatusText = "Search complete. No results found.";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Search failed: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
            _uiUpdateTimer.Stop();
            FlushPendingUpdates(null, null);
            OnPropertyChanged(nameof(ResultsTitle));
        }
    }

    [RelayCommand]
    private async Task StopSearchAsync()
    {
        await _ouijaService.StopSearchAsync();
        StatusText = "Search stopped by user";
    }

    [RelayCommand]
    private void SaveConfig()
    {
        _configService.UpdateSearchCriteria(Config);
        StatusText = $"Configuration '{ConfigName}' saved";
    }

    [RelayCommand]
    private void SaveAsConfig()
    {
        // TODO: Implement save as dialog
        StatusText = "Save As not implemented yet";
    }

    [RelayCommand]
    private void LoadConfig()
    {
        // TODO: Implement load dialog
        StatusText = "Load not implemented yet";
    }

    [RelayCommand]
    private void NextToSearchCriteria()
    {
        // TODO: Navigate to search criteria tab
    }

    [RelayCommand]
    private void NextToRun()
    {
        // TODO: Navigate to run tab
    }

    [RelayCommand]
    private void AddCriteria()
    {
        string? criteriaText = null;

        if (!string.IsNullOrEmpty(SelectedJoker))
        {
            criteriaText = SelectedEdition switch
            {
                null or "Base" => SelectedJoker,
                _ => $"{SelectedJoker} ({SelectedEdition})"
            };
        }
        else if (!string.IsNullOrEmpty(SelectedTarot))
        {
            criteriaText = SelectedTarot;
        }
        else if (!string.IsNullOrEmpty(SelectedSpectral))
        {
            criteriaText = SelectedSpectral;
        }
        else if (!string.IsNullOrEmpty(SelectedTag))
        {
            criteriaText = SelectedTag;
        }
        else if (!string.IsNullOrEmpty(SelectedVoucher))
        {
            criteriaText = SelectedVoucher;
        }
        else if (!string.IsNullOrEmpty(SelectedRank) && !string.IsNullOrEmpty(SelectedSuit))
        {
            criteriaText = $"{SelectedRank} of {SelectedSuit}";
        }

        if (!string.IsNullOrEmpty(criteriaText))
        {
            if (AddAsNeed)
            {
                if (!Config.Needs.Contains(criteriaText))
                {
                    Config.Needs.Add(criteriaText);
                }
            }
            else
            {
                if (!Config.Wants.Contains(criteriaText))
                {
                    Config.Wants.Add(criteriaText);
                }
            }

            // Clear selections
            SelectedJoker = null;
            SelectedEdition = null;
            SelectedTarot = null;
            SelectedSpectral = null;
            SelectedTag = null;
            SelectedVoucher = null;
            SelectedRank = null;
            SelectedSuit = null;
        }
    }

    [RelayCommand]
    private async Task RefreshResultsAsync()
    {
        try
        {
            SearchResults.Clear();
            var results = await _databaseService.GetTopResultsAsync(100);
            foreach (var result in results)
            {
                SearchResults.Add(result);
            }
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(ResultsTitle));
            StatusText = $"Refreshed {results.Count} results";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to refresh results: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ExportResults()
    {
        // TODO: Implement export functionality
        StatusText = "Export not implemented yet";
    }

    [RelayCommand]
    private void AddQuickJoker()
    {
        var item = new SearchCriteriaItem
        {
            Type = "Joker",
            Name = "Select Joker",
            ShowAntes = true,
            IsRequired = false  // Default to WANTS
        };
        SearchCriteria.Add(item);
    }

    [RelayCommand]
    private void AddQuickTarot()
    {
        var item = new SearchCriteriaItem
        {
            Type = "Tarot",
            Name = "The Fool",
            IsRequired = false
        };
        SearchCriteria.Add(item);
    }

    [RelayCommand]
    private void AddQuickSpectral()
    {
        var item = new SearchCriteriaItem
        {
            Type = "Spectral",
            Name = "Familiar",
            IsRequired = false
        };
        SearchCriteria.Add(item);
    }

    [RelayCommand]
    private void AddQuickTag()
    {
        var item = new SearchCriteriaItem
        {
            Type = "Tag",
            Name = "Uncommon Tag",
            IsRequired = false
        };
        SearchCriteria.Add(item);
    }

    [RelayCommand]
    private void AddQuickVoucher()
    {
        var item = new SearchCriteriaItem
        {
            Type = "Voucher",
            Name = "Overstock",
            IsRequired = false
        };
        SearchCriteria.Add(item);
    }

    [RelayCommand]
    private void AddQuickCard()
    {
        var item = new SearchCriteriaItem
        {
            Type = "Card",
            Name = "Ace",
            Details = "Spades",
            HasDetails = true,
            IsRequired = false
        };
        SearchCriteria.Add(item);
    }

    [RelayCommand]
    private void RemoveCriteria(SearchCriteriaItem item)
    {
        SearchCriteria.Remove(item);
    }

    private bool CanStartSearch() => !IsSearching;

    private void OnProgressUpdate(SearchProgress progress)
    {
        Dispatcher.UIThread.Post(() =>
        {
            SearchProgress = progress.PercentComplete;
            StatusText = progress.Message;
            
            if (progress.Elapsed != TimeSpan.Zero)
            {
                Runtime = progress.Elapsed.ToString(@"hh\:mm\:ss");
            }
            
            if (progress.SeedsPerSecond > 0)
            {
                SeedsPerSecond = $"{progress.SeedsPerSecond:N0} seeds/sec";
            }
        });

        if (progress.NewResults?.Any() == true)
        {
            lock (_pendingUpdatesLock)
            {
                _pendingResults.AddRange(progress.NewResults);
            }
        }
    }

    private void FlushPendingUpdates(object? sender, EventArgs? e)
    {
        List<SeedResult> toAdd;
        lock (_pendingUpdatesLock)
        {
            toAdd = _pendingResults.ToList();
            _pendingResults.Clear();
        }

        if (toAdd.Any())
        {
            Dispatcher.UIThread.Post(() =>
            {
                foreach (var result in toAdd)
                {
                    SearchResults.Add(result);
                }
                OnPropertyChanged(nameof(HasResults));
                OnPropertyChanged(nameof(ResultsTitle));
            });
        }
    }
}
