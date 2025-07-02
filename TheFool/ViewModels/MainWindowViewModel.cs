using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
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
    private readonly UserConfigService _userConfigService;

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
        ConfigService configService,
        UserConfigService userConfigService)
    {
        _ouijaService = ouijaService;
        _databaseService = databaseService;
        _configService = configService;
        _userConfigService = userConfigService;

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
    private async Task SaveConfigAsync()
    {
        try
        {
            // Get the main window to access the StorageProvider
            var mainWindow = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (mainWindow?.StorageProvider == null)
            {
                StatusText = "❌ Cannot access file system!";
                return;
            }

            // Use ConfigName if provided, otherwise suggest a default name
            var suggestedName = !string.IsNullOrWhiteSpace(ConfigName) 
                ? ConfigName 
                : $"config_{DateTime.Now:yyyyMMdd_HHmmss}";

            // Configure file picker options for saving
            var filePickerOptions = new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                Title = "Save Configuration",
                SuggestedFileName = $"{suggestedName}.json",
                DefaultExtension = "json",
                FileTypeChoices = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("JSON Configuration")
                    {
                        Patterns = new[] { "*.json" }
                    }
                }
            };

            // Show save file dialog
            var file = await mainWindow.StorageProvider.SaveFilePickerAsync(filePickerOptions);
            
            if (file == null)
            {
                StatusText = "💾 Save cancelled";
                return;
            }

            var fileName = file.Name;
            var configName = System.IO.Path.GetFileNameWithoutExtension(fileName);
            
            // Update ConfigName to match the saved file
            ConfigName = configName;

            var userConfig = new UserConfiguration
            {
                Name = configName,
                Deck = Config.Deck ?? "",
                Stake = Config.Stake ?? "",
                SearchItems = SearchCriteria.ToList(),
                LastModified = DateTime.Now
            };

            var success = await _userConfigService.SaveConfigurationAsync(configName, userConfig);
            
            if (success)
            {
                StatusText = $"💾 Configuration '{configName}' saved successfully!";
                Console.WriteLine($"Saved config: {configName}");
            }
            else
            {
                StatusText = "❌ Failed to save configuration!";
            }
        }
        catch (Exception ex)
        {
            StatusText = "❌ Error saving configuration!";
            Console.WriteLine($"Save error: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task LoadConfigAsync()
    {
        try
        {
            // Get the main window to access the StorageProvider
            var mainWindow = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (mainWindow?.StorageProvider == null)
            {
                StatusText = "❌ Cannot access file system!";
                return;
            }

            // Configure file picker options
            var filePickerOptions = new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "Load Configuration",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("JSON Configuration")
                    {
                        Patterns = new[] { "*.json" }
                    },
                    new Avalonia.Platform.Storage.FilePickerFileType("All Files")
                    {
                        Patterns = new[] { "*.*" }
                    }
                }
            };

            // Show file picker dialog
            var files = await mainWindow.StorageProvider.OpenFilePickerAsync(filePickerOptions);
            
            if (files.Count == 0)
            {
                StatusText = "📂 No file selected";
                return;
            }

            var selectedFile = files[0];
            var fileName = selectedFile.Name;
            
            // Extract config name from filename (without extension)
            var configName = System.IO.Path.GetFileNameWithoutExtension(fileName);
            
            // Try to load the configuration
            var userConfig = await _userConfigService.LoadConfigurationAsync(configName);
            
            if (userConfig != null)
            {
                // Update the UI with loaded configuration
                Config.Deck = userConfig.Deck;
                Config.Stake = userConfig.Stake;
                ConfigName = userConfig.Name;
                
                // Clear existing search criteria and add loaded ones
                SearchCriteria.Clear();
                foreach (var item in userConfig.SearchItems)
                {
                    SearchCriteria.Add(item);
                }
                
                StatusText = $"📂 Configuration '{userConfig.Name}' loaded successfully!";
                Console.WriteLine($"Loaded config: {userConfig.Name} with {userConfig.SearchItems.Count} items");
            }
            else
            {
                StatusText = $"❌ Configuration '{configName}' not found!";
            }
        }
        catch (Exception ex)
        {
            StatusText = "❌ Error loading configuration!";
            Console.WriteLine($"Load error: {ex.Message}");
        }
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
