using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
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

    [ObservableProperty]
    private string _configName = "My Awesome Balatro Config";

    [ObservableProperty]
    private SearchCriteria _config = new();

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string _statusText = "🃏 Ready to find some epic seeds!";

    [ObservableProperty]
    private string _selectedEngine = "Ouija (GPU)";

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

    [ObservableProperty]
    private ObservableCollection<SearchCriteriaItem> _searchCriteria = new();

    public IEnumerable<SearchCriteriaItem> RequiredItems => SearchCriteria.Where(x => x.IsRequired).OrderBy(x => x.Name);
    public IEnumerable<SearchCriteriaItem> OptionalItems => SearchCriteria.Where(x => !x.IsRequired).OrderBy(x => x.Name);
    public bool HasRequiredItems => SearchCriteria.Any(x => x.IsRequired);
    public bool HasOptionalItems => SearchCriteria.Any(x => !x.IsRequired);

    public MainWindowViewModel(
        IOuijaService ouijaService,
        DatabaseService databaseService,
        ConfigService configService)
    {
        _ouijaService = ouijaService;
        _databaseService = databaseService;
        _configService = configService;

        // Set some default values to test the UI
        Config.Deck = AvailableDecks.First();
        Config.Stake = AvailableStakes.First();

        // Add some test criteria
        var testJoker = new SearchCriteriaItem
        {
            Type = "Joker",
            Name = "Joker",
            IsRequired = true
        };
        SearchCriteria.Add(testJoker);

        var testTarot = new SearchCriteriaItem
        {
            Type = "Tarot", 
            Name = "The Fool",
            IsRequired = false
        };
        SearchCriteria.Add(testTarot);

        // Subscribe to collection changes
        SearchCriteria.CollectionChanged += (s, e) => 
        {
            OnPropertyChanged(nameof(RequiredItems));
            OnPropertyChanged(nameof(OptionalItems));
            OnPropertyChanged(nameof(HasRequiredItems));
            OnPropertyChanged(nameof(HasOptionalItems));
        };

        Console.WriteLine("MainWindowViewModel initialized with test data");
    }

    [RelayCommand]
    private void SaveConfig()
    {
        StatusText = $"🎯 Configuration '{ConfigName}' saved successfully!";
        Console.WriteLine($"Save config: {ConfigName}");
    }

    [RelayCommand]
    private void LoadConfig()
    {
        StatusText = "📂 Load config functionality coming soon!";
        Console.WriteLine("Load config clicked");
    }

    [RelayCommand]
    private void AddQuickJoker()
    {
        var joker = new SearchCriteriaItem
        {
            Type = "Joker",
            Name = $"Random Joker #{SearchCriteria.Count + 1}",
            IsRequired = false
        };
        SearchCriteria.Add(joker);
        StatusText = $"🃏 Added joker! Total items: {SearchCriteria.Count}";
        Console.WriteLine($"Added joker: {joker.Name}");
    }

    [RelayCommand]
    private void AddQuickTarot()
    {
        var tarot = new SearchCriteriaItem
        {
            Type = "Tarot",
            Name = "The Magician",
            IsRequired = false
        };
        SearchCriteria.Add(tarot);
        StatusText = $"🌟 Added tarot! Total items: {SearchCriteria.Count}";
        Console.WriteLine($"Added tarot: {tarot.Name}");
    }

    [RelayCommand]
    private void AddQuickVoucher()
    {
        var voucher = new SearchCriteriaItem
        {
            Type = "Voucher",
            Name = "Overstock",
            IsRequired = false
        };
        SearchCriteria.Add(voucher);
        StatusText = $"🎫 Added voucher! Total items: {SearchCriteria.Count}";
        Console.WriteLine($"Added voucher: {voucher.Name}");
    }

    [RelayCommand]
    private void RemoveCriteria(SearchCriteriaItem item)
    {
        SearchCriteria.Remove(item);
        StatusText = $"✕ Removed {item.Name}! Total items: {SearchCriteria.Count}";
        Console.WriteLine($"Removed: {item.Name}");
    }

    [RelayCommand]
    private async Task StartSearchAsync()
    {
        IsSearching = true;
        StatusText = "🔥 JIMBO IS COOKING! 🔥";
        
        Console.WriteLine("Starting search simulation...");
        
        // Simulate search for demo
        await Task.Delay(2000);
        
        IsSearching = false;
        StatusText = "🎉 Search complete! Found some epic seeds!";
        
        Console.WriteLine("Search simulation complete");
    }
}
