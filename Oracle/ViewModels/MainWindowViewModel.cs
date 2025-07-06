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
using Oracle.Models;
using Oracle.Services;

namespace Oracle.ViewModels;

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

    [ObservableProperty]
    private ObservableCollection<SearchResult> _searchResults = new();

    [ObservableProperty]
    private int _searchResultsCount;

    public class SearchResult
    {
        public string Seed { get; set; } = string.Empty;
        public double Score { get; set; }
    }

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
            Name = "Oracle",
            IsRequired = false
        };
        SearchCriteria.Add(testTarot);

        // Subscribe to collection changes
        SearchCriteria.CollectionChanged += (s, e) => 
        {
            // Handle items being added or removed
            if (e.NewItems != null)
            {
                foreach (SearchCriteriaItem item in e.NewItems)
                {
                    item.RequiredStatusChanged += OnItemRequiredStatusChanged;
                }
            }
            
            if (e.OldItems != null)
            {
                foreach (SearchCriteriaItem item in e.OldItems)
                {
                    item.RequiredStatusChanged -= OnItemRequiredStatusChanged;
                }
            }
            
            OnPropertyChanged(nameof(RequiredItems));
            OnPropertyChanged(nameof(OptionalItems));
            OnPropertyChanged(nameof(HasRequiredItems));
            OnPropertyChanged(nameof(HasOptionalItems));
        };
        
        // Subscribe to existing items
        foreach (var item in SearchCriteria)
        {
            item.RequiredStatusChanged += OnItemRequiredStatusChanged;
        }

        Console.WriteLine("MainWindowViewModel initialized with test data");
    }
    
    private void OnItemRequiredStatusChanged(object? sender, bool isRequired)
    {
        // Refresh the filtered collections when an item's required status changes
        OnPropertyChanged(nameof(RequiredItems));
        OnPropertyChanged(nameof(OptionalItems));
        OnPropertyChanged(nameof(HasRequiredItems));
        OnPropertyChanged(nameof(HasOptionalItems));
        
        if (sender is SearchCriteriaItem item)
        {
            var status = isRequired ? "Required" : "Optional";
            StatusText = $"📋 {item.Name} moved to {status} section";
            Console.WriteLine($"Item {item.Name} is now {status}");
        }
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
                    new Avalonia.Platform.Storage.FilePickerFileType("Ouija JSON Configuration")
                    {
                        Patterns = new[] { "*.json" }
                    },
                    new Avalonia.Platform.Storage.FilePickerFileType("User Configuration")
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

            // Save both formats
            var userConfig = new UserConfiguration
            {
                Name = configName,
                Deck = Config.Deck ?? "",
                Stake = Config.Stake ?? "",
                SearchItems = SearchCriteria.ToList(),
                LastModified = DateTime.Now
            };

            // Save user config
            var userSuccess = await _userConfigService.SaveConfigurationAsync(configName, userConfig);
            
            // Also save as Ouija JSON format
            var ouijaConfig = ConvertToOuijaConfig();
            var ouijaSuccess = await SaveOuijaJsonAsync(file.Path.LocalPath, ouijaConfig);
            
            if (userSuccess && ouijaSuccess)
            {
                StatusText = $"💾 Configuration '{configName}' saved successfully as both formats!";
                Console.WriteLine($"Saved config: {configName} (User + Ouija formats)");
            }
            else if (userSuccess)
            {
                StatusText = $"💾 Configuration '{configName}' saved as user format only!";
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
                    new Avalonia.Platform.Storage.FilePickerFileType("Ouija JSON Configuration")
                    {
                        Patterns = new[] { "*.json" }
                    },
                    new Avalonia.Platform.Storage.FilePickerFileType("User Configuration")
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
            var filePath = selectedFile.Path.LocalPath;
            
            // Extract config name from filename (without extension)
            var configName = System.IO.Path.GetFileNameWithoutExtension(fileName);
            
            // Try to load as Ouija JSON first
            var ouijaConfig = await LoadOuijaJsonAsync(filePath);
            if (ouijaConfig != null)
            {
                ConfigName = configName;
                LoadFromOuijaConfig(ouijaConfig);
                StatusText = $"📂 Ouija configuration '{configName}' loaded successfully!";
                Console.WriteLine($"Loaded Ouija config: {configName}");
                return;
            }
            
            // Fall back to user configuration format
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
                    item.RequiredStatusChanged += OnItemRequiredStatusChanged;
                    SearchCriteria.Add(item);
                }
                
                StatusText = $"📂 User configuration '{userConfig.Name}' loaded successfully!";
                Console.WriteLine($"Loaded user config: {userConfig.Name} with {userConfig.SearchItems.Count} items");
            }
            else
            {
                StatusText = "❌ Failed to load configuration - invalid format!";
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
    private void ToggleRequired(SearchCriteriaItem item)
    {
        item.IsRequired = !item.IsRequired;
        
        // Trigger property change notifications for the filtered collections
        OnPropertyChanged(nameof(RequiredItems));
        OnPropertyChanged(nameof(OptionalItems));
        OnPropertyChanged(nameof(HasRequiredItems));
        OnPropertyChanged(nameof(HasOptionalItems));
        
        var section = item.IsRequired ? "Required Items" : "Scoring Items";
        StatusText = $"📋 Moved {item.Name} to {section}";
        Console.WriteLine($"Toggled {item.Name} required status to: {item.IsRequired}");
    }

    [RelayCommand]
    private async Task CopySeedAsync(string seed)
    {
        try
        {
            // Get the main window to access clipboard
            var mainWindow = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;

            if (mainWindow?.Clipboard != null)
            {
                await mainWindow.Clipboard.SetTextAsync(seed);
                StatusText = $"📋 Copied seed {seed} to clipboard!";
            }
            else
            {
                StatusText = "❌ Cannot access clipboard";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"❌ Failed to copy: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task StartSearchAsync()
    {
        try
        {
            IsSearching = true;
            StatusText = "🔥 JIMBO IS COOKING! Starting Ouija search...";
            
            Console.WriteLine("Starting Ouija search...");
            
            // Convert our search criteria to Ouija format
            var ouijaConfig = ConvertToOuijaConfig();
            
            // Start the actual Ouija search
            // Convert OuijaConfig to SearchCriteria for the service call
            var searchCriteria = new SearchCriteria
            {
                SeedCount = 1000,
                ThreadCount = 4,
                MinScore = 1,
                Deck = Config.Deck ?? "",
                Stake = Config.Stake ?? "",
                Needs = SearchCriteria.Where(x => x.IsRequired).Select(x => x.Name).ToList(),
                Wants = SearchCriteria.Where(x => !x.IsRequired).Select(x => x.Name).ToList()
            };
            
            var progress = new Progress<SearchProgress>(p =>
            {
                StatusText = p.Message ?? "Searching...";
            });
            
            await _ouijaService.StartSearchAsync(searchCriteria, "gpu", 1, 64, 4, 8, progress);
            
            // For MVP: Add some mock results to demonstrate the UI
            SearchResults.Clear();
            var random = new Random();
            for (int i = 0; i < 10; i++)
            {
                SearchResults.Add(new SearchResult
                {
                    Seed = $"SEED{random.Next(100000, 999999)}",
                    Score = Math.Round(random.NextDouble() * 1000 + 100, 2)
                });
            }
            SearchResultsCount = SearchResults.Count;
            
            StatusText = $"✅ Search completed! Found {SearchResultsCount} seeds";
            Console.WriteLine("Ouija search started successfully");
        }
        catch (Exception ex)
        {
            StatusText = $"❌ Search failed: {ex.Message}";
            Console.WriteLine($"Search error: {ex.Message}");
        }
        finally
        {
            IsSearching = false;
        }
    }
    
    private Motely.Filters.OuijaConfig ConvertToOuijaConfig()
    {
        var requiredItems = SearchCriteria.Where(x => x.IsRequired).ToList();
        var optionalItems = SearchCriteria.Where(x => !x.IsRequired).ToList();
        
        var config = new Motely.Filters.OuijaConfig
        {
            NumNeeds = requiredItems.Count,
            NumWants = optionalItems.Count,
            Needs = requiredItems.Select(ConvertToOuijaDesire).ToArray(),
            Wants = optionalItems.Select(ConvertToOuijaDesire).ToArray(),
            MaxSearchAnte = 8,
            Deck = ConvertToOuijaDeck(Config.Deck ?? ""),
             Stake = ConvertToOuijaStake(Config.Stake ?? ""),
            ScoreNaturalNegatives = false,
            ScoreDesiredNegatives = true
        };

        return config;
    }
    
    private Motely.Filters.OuijaConfig.Desire ConvertToOuijaDesire(SearchCriteriaItem item)
    {
        return new Motely.Filters.OuijaConfig.Desire
         {
             Type = item.Type,
             Value = item.Name,
             Edition = "",
             DesireByAnte = 8,
             SearchAntes = new[] { 1, 2, 3, 4, 5, 6, 7, 8 }
         };
     }
     
     private string ConvertToOuijaDeck(string displayDeck)
     {
         return displayDeck switch
         {
             "🔴 Red Deck" => "Red Deck",
             "🔵 Blue Deck" => "Blue Deck",
             "🟡 Yellow Deck" => "Yellow Deck",
             "🟢 Green Deck" => "Green Deck",
             "⚫ Black Deck" => "Black Deck",
             "✨ Magic Deck" => "Magic Deck",
             "🌌 Nebula Deck" => "Nebula Deck",
             "👻 Ghost Deck" => "Ghost Deck",
             "🏚️ Abandoned Deck" => "Abandoned Deck",
             "♥️♠️ Checkered Deck" => "Checkered Deck",
             "♈ Zodiac Deck" => "Zodiac Deck",
             "🎨 Painted Deck" => "Painted Deck",
             "🔴🔵 Anaglyph Deck" => "Anaglyph Deck",
             "⚡ Plasma Deck" => "Plasma Deck",
             "🎲 Erratic Deck" => "Erratic Deck",
             _ => "Red Deck"
         };
     }
     
     private string ConvertToOuijaStake(string displayStake)
     {
         return displayStake switch
         {
             "⚪ White Stake" => "White Stake",
             "🔴 Red Stake" => "Red Stake",
             "🟢 Green Stake" => "Green Stake",
             "⚫ Black Stake" => "Black Stake",
             "🔵 Blue Stake" => "Blue Stake",
             "🟣 Purple Stake" => "Purple Stake",
             "🟠 Orange Stake" => "Orange Stake",
             "🏆 Gold Stake" => "Gold Stake",
             _ => "White Stake"
         };
     }
    
    private string ConvertFromOuijaDeck(string ouijaDeck)
      {
          return ouijaDeck switch
          {
              "Red Deck" => "🔴 Red Deck",
              "Blue Deck" => "🔵 Blue Deck",
              "Yellow Deck" => "🟡 Yellow Deck",
              "Green Deck" => "🟢 Green Deck",
              "Black Deck" => "⚫ Black Deck",
              "Magic Deck" => "✨ Magic Deck",
              "Nebula Deck" => "🌌 Nebula Deck",
              "Ghost Deck" => "👻 Ghost Deck",
              "Abandoned Deck" => "🏚️ Abandoned Deck",
              "Checkered Deck" => "♥️♠️ Checkered Deck",
              "Zodiac Deck" => "♈ Zodiac Deck",
              "Painted Deck" => "🎨 Painted Deck",
              "Anaglyph Deck" => "🔴🔵 Anaglyph Deck",
              "Plasma Deck" => "⚡ Plasma Deck",
              "Erratic Deck" => "🎲 Erratic Deck",
              _ => "🔴 Red Deck"
          };
      }
      
      private string ConvertFromOuijaStake(string ouijaStake)
      {
          return ouijaStake switch
          {
              "White Stake" => "⚪ White Stake",
              "Red Stake" => "🔴 Red Stake",
              "Green Stake" => "🟢 Green Stake",
              "Black Stake" => "⚫ Black Stake",
              "Blue Stake" => "🔵 Blue Stake",
              "Purple Stake" => "🟣 Purple Stake",
              "Orange Stake" => "🟠 Orange Stake",
              "Gold Stake" => "🏆 Gold Stake",
              _ => "⚪ White Stake"
          };
      }
      
      private void AddItemToConfig(dynamic target, SearchCriteriaItem item)
    {
        switch (item.Type.ToLower())
        {
            case "joker":
                if (target.Joker == null) target.Joker = new List<string>();
                target.Joker.Add(item.Name);
                break;
            case "tarot":
                if (target.Tarot == null) target.Tarot = new List<string>();
                target.Tarot.Add(item.Name);
                break;
            case "voucher":
                if (target.Voucher == null) target.Voucher = new List<string>();
                target.Voucher.Add(item.Name);
                break;
            case "spectral":
                if (target.Spectral == null) target.Spectral = new List<string>();
                target.Spectral.Add(item.Name);
                break;
            case "card":
                if (target.PlayingCard == null) target.PlayingCard = new List<string>();
                target.PlayingCard.Add(item.GetFilterString());
                break;
        }
    }
    
    private string ConvertDeckName(string displayName)
    {
        return displayName switch
        {
            "🔴 Red Deck" => "Red Deck",
            "🔵 Blue Deck" => "Blue Deck",
            "🟡 Yellow Deck" => "Yellow Deck",
            "🟢 Green Deck" => "Green Deck",
            "⚫ Black Deck" => "Black Deck",
            "✨ Magic Deck" => "Magic Deck",
            "🌌 Nebula Deck" => "Nebula Deck",
            "👻 Ghost Deck" => "Ghost Deck",
            "🏚️ Abandoned Deck" => "Abandoned Deck",
            "♥️♠️ Checkered Deck" => "Checkered Deck",
            "♈ Zodiac Deck" => "Zodiac Deck",
            "🎨 Painted Deck" => "Painted Deck",
            "🔴🔵 Anaglyph Deck" => "Anaglyph Deck",
            "⚡ Plasma Deck" => "Plasma Deck",
            "🎲 Erratic Deck" => "Erratic Deck",
            _ => "Red Deck"
        };
    }
    
    private string ConvertStakeName(string displayName)
    {
        return displayName switch
        {
            "⚪ White Stake" => "White Stake",
            "🔴 Red Stake" => "Red Stake",
            "🟢 Green Stake" => "Green Stake",
            "⚫ Black Stake" => "Black Stake",
            "🔵 Blue Stake" => "Blue Stake",
            "🟣 Purple Stake" => "Purple Stake",
            "🟠 Orange Stake" => "Orange Stake",
            "🏆 Gold Stake" => "Gold Stake",
            _ => "White Stake"
        };
    }

    private async Task<bool> SaveOuijaJsonAsync(string filePath, Motely.Filters.OuijaConfig ouijaConfig)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(ouijaConfig, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
            
            await System.IO.File.WriteAllTextAsync(filePath, json);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving Ouija JSON: {ex.Message}");
            return false;
        }
    }

    private async Task<Motely.Filters.OuijaConfig?> LoadOuijaJsonAsync(string filePath)
    {
        try
        {
            var json = await System.IO.File.ReadAllTextAsync(filePath);
            var ouijaConfig = System.Text.Json.JsonSerializer.Deserialize<Motely.Filters.OuijaConfig>(json, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
            
            return ouijaConfig;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading Ouija JSON: {ex.Message}");
            return null;
        }
    }

    private void LoadFromOuijaConfig(Motely.Filters.OuijaConfig ouijaConfig)
    {
        try
        {
            // Clear existing criteria
            SearchCriteria.Clear();
            
            // Set deck and stake with proper conversion
            Config.Deck = ConvertFromOuijaDeck(ouijaConfig.Deck ?? "Red Deck");
            Config.Stake = ConvertFromOuijaStake(ouijaConfig.Stake ?? "White Stake");
            
            // Load needs (required items)
            if (ouijaConfig.Needs != null)
            {
                foreach (var need in ouijaConfig.Needs)
                {
                    var item = new SearchCriteriaItem
                    {
                        Type = need.Type,
                        Name = need.Value,
                        IsRequired = true
                    };
                    item.RequiredStatusChanged += OnItemRequiredStatusChanged;
                    SearchCriteria.Add(item);
                }
            }
            
            // Load wants (optional items)
            if (ouijaConfig.Wants != null)
            {
                foreach (var want in ouijaConfig.Wants)
                {
                    var item = new SearchCriteriaItem
                    {
                        Type = want.Type,
                        Name = want.Value,
                        IsRequired = false
                    };
                    item.RequiredStatusChanged += OnItemRequiredStatusChanged;
                    SearchCriteria.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading from Ouija config: {ex.Message}");
        }
    }



}
