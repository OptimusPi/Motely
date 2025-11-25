using System.Collections.ObjectModel;
using Motely.Filters;
using Terminal.Gui;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Motely.TUI;

public class FilterBuilderWindow : Window
{
    private ListView _mustList;
    private ListView _shouldList;
    private ListView? _mustNotList;
    private List<string> _mustItems = new();
    private List<string> _shouldItems = new();
    private List<string> _mustNotItems = new();
    private Label _statusLabel;
    private bool _isDialogOpen = false;

    public FilterBuilderWindow()
    {
        Title = "Filter Builder";
        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();

        // Title with Jimbo
        var titleLabel = new Label()
        {
            X = Pos.Center(),
            Y = 1,
            Text = "🔧 BUILD YOUR PERFECT FILTER 🔧",
            TextAlignment = Alignment.Center,
        };
        Add(titleLabel);

        var instructionLabel = new Label()
        {
            X = Pos.Center(),
            Y = 2,
            Text = "Jimbo says: Add items to find your dream seeds!",
            TextAlignment = Alignment.Center,
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.White, ColorName.Black),
            },
        };
        Add(instructionLabel);

        // Create three columns for Must, Should, MustNot
        var yStart = 4;

        // MUST column
        var mustLabel = new Label()
        {
            X = 2,
            Y = yStart,
            Text = "MUST Have (Required)",
        };
        Add(mustLabel);

        _mustList = new ListView()
        {
            X = 2,
            Y = yStart + 1,
            Width = 35,
            Height = 15,
            AllowsMarking = false,
            CanFocus = true,
        };
        _mustList.SetSource(new ObservableCollection<string>(_mustItems));
        Add(_mustList);

        var mustAddBtn = new Button()
        {
            X = 2,
            Y = Pos.Bottom(_mustList),
            Text = "Add Item (A)",
        };
        mustAddBtn.Accept += (s, e) => AddItem("must");
        Add(mustAddBtn);

        var mustRemoveBtn = new Button()
        {
            X = Pos.Right(mustAddBtn) + 1,
            Y = Pos.Bottom(_mustList),
            Text = "Remove",
        };
        mustRemoveBtn.Accept += (s, e) => RemoveItem("must");
        Add(mustRemoveBtn);

        // SHOULD column
        var shouldLabel = new Label()
        {
            X = Pos.Right(_mustList) + 3,
            Y = yStart,
            Text = "SHOULD Have (Bonus Points)",
        };
        Add(shouldLabel);

        _shouldList = new ListView()
        {
            X = Pos.Right(_mustList) + 3,
            Y = yStart + 1,
            Width = 35,
            Height = 15,
            AllowsMarking = false,
            CanFocus = true,
        };
        _shouldList.SetSource(new ObservableCollection<string>(_shouldItems));
        Add(_shouldList);

        var shouldAddBtn = new Button()
        {
            X = Pos.Right(_mustList) + 3,
            Y = Pos.Bottom(_shouldList),
            Text = "Add Item (A)",
        };
        shouldAddBtn.Accept += (s, e) => AddItem("should");
        Add(shouldAddBtn);

        var shouldRemoveBtn = new Button()
        {
            X = Pos.Right(shouldAddBtn) + 1,
            Y = Pos.Bottom(_shouldList),
            Text = "Remove",
        };
        shouldRemoveBtn.Accept += (s, e) => RemoveItem("should");
        Add(shouldRemoveBtn);

        // MUST NOT column (if there's space)
        if (Application.Driver.Cols > 120)
        {
            var mustNotLabel = new Label()
            {
                X = Pos.Right(_shouldList) + 3,
                Y = yStart,
                Text = "MUST NOT Have",
            };
            Add(mustNotLabel);

            _mustNotList = new ListView()
            {
                X = Pos.Right(_shouldList) + 3,
                Y = yStart + 1,
                Width = 30,
                Height = 15,
                AllowsMarking = false,
                CanFocus = true,
            };
            _mustNotList.SetSource(new ObservableCollection<string>(_mustNotItems));
            Add(_mustNotList);

            var mustNotAddBtn = new Button()
            {
                X = Pos.Right(_shouldList) + 3,
                Y = Pos.Bottom(_mustNotList),
                Text = "Add (A)",
            };
            mustNotAddBtn.Accept += (s, e) => AddItem("mustnot");
            Add(mustNotAddBtn);

            var mustNotRemoveBtn = new Button()
            {
                X = Pos.Right(mustNotAddBtn) + 1,
                Y = Pos.Bottom(_mustNotList),
                Text = "Remove",
            };
            mustNotRemoveBtn.Accept += (s, e) => RemoveItem("mustnot");
            Add(mustNotRemoveBtn);
        }

        // Hotkey instructions
        var hotkeysLabel = new Label()
        {
            X = 2,
            Y = Pos.Bottom(_mustList) + 2,
            Width = Dim.Fill() - 4,
            Text =
                "Quick Add: (J)oker (L)egendary (C)ard (T)arot (S)pectral (P)lanet (V)oucher (B)oss (R)eward Tags",
        };
        Add(hotkeysLabel);

        // Bottom buttons
        var startSearchBtn = new Button()
        {
            X = 2,
            Y = Pos.AnchorEnd(3),
            Text = "Start Search",
        };
        startSearchBtn.Accept += (s, e) => StartSearch();
        Add(startSearchBtn);

        var saveBtn = new Button()
        {
            X = Pos.Right(startSearchBtn) + 2,
            Y = Pos.AnchorEnd(3),
            Text = "Save Filter",
        };
        saveBtn.Accept += (s, e) => SaveFilter();
        Add(saveBtn);

        var backBtn = new Button()
        {
            X = Pos.Right(saveBtn) + 2,
            Y = Pos.AnchorEnd(3),
            Text = "Back to Menu",
        };
        backBtn.Accept += (s, e) => Application.RequestStop();
        Add(backBtn);

        // Status label
        _statusLabel = new Label()
        {
            X = Pos.Right(backBtn) + 4,
            Y = Pos.AnchorEnd(3),
            Width = Dim.Fill(),
            Text = "",
        };
        Add(_statusLabel);

        // Keyboard shortcuts
        KeyDown += (sender, e) =>
        {
            if (e.KeyCode == KeyCode.A)
            {
                // Determine which list has focus
                if (_mustList.HasFocus)
                    AddItem("must");
                else if (_shouldList.HasFocus)
                    AddItem("should");
                else if (_mustNotList?.HasFocus == true)
                    AddItem("mustnot");
                e.Handled = true;
            }
            else if (e.KeyCode == KeyCode.J)
            {
                AddItemQuick("Joker");
                e.Handled = true;
            }
            else if (e.KeyCode == KeyCode.L)
            {
                AddItemQuick("Legendary");
                e.Handled = true;
            }
            else if (e.KeyCode == KeyCode.T)
            {
                AddItemQuick("Tarot");
                e.Handled = true;
            }
            else if (e.KeyCode == KeyCode.S && !e.IsCtrl)
            {
                AddItemQuick("Spectral");
                e.Handled = true;
            }
            else if (e.KeyCode == KeyCode.P)
            {
                AddItemQuick("Planet");
                e.Handled = true;
            }
            else if (e.KeyCode == KeyCode.V)
            {
                AddItemQuick("Voucher");
                e.Handled = true;
            }
            else if (e.KeyCode == KeyCode.B)
            {
                AddItemQuick("Boss");
                e.Handled = true;
            }
            else if (e.KeyCode == KeyCode.R)
            {
                AddItemQuick("Tags");
                e.Handled = true;
            }
            else if (e.KeyCode == KeyCode.C && !e.IsCtrl)
            {
                AddItemQuick("Card");
                e.Handled = true;
            }
            else if (e.KeyCode == KeyCode.Esc)
            {
                // Show menu with options
                var choice = ShowChoiceDialog(
                    "ESC Menu",
                    "What would you like to do?",
                    "Main Menu",
                    "Exit",
                    "Cancel"
                );

                if (choice == 0) // Main Menu
                {
                    // Return to main menu
                    Application.RequestStop();
                }
                else if (choice == 1) // Exit
                {
                    // Exit the entire application
                    Application.RequestStop();
                    Environment.Exit(0);
                }
                // choice == 2 (Cancel) - do nothing, stay in filter builder

                e.Handled = true;
            }
        };

        _mustList.SetFocus();
    }

    private void AddItem(string listType)
    {
        if (_isDialogOpen)
            return; // Prevent re-entrant dialog spawning
        _isDialogOpen = true;

        try
        {
            // Show category selector
            var categoryDialog = new CategorySelectorDialog();
            Application.Run(categoryDialog);

            if (categoryDialog.SelectedCategory != null)
            {
                ShowItemSelectorAndAdd(categoryDialog.SelectedCategory, listType);
            }
        }
        finally
        {
            _isDialogOpen = false;
        }
    }

    private void AddItemQuick(string category)
    {
        if (_isDialogOpen)
            return; // Prevent re-entrant dialog spawning
        _isDialogOpen = true;

        try
        {
            // Determine which list has focus
            string listType = "must"; // default
            if (_shouldList.HasFocus)
                listType = "should";
            else if (_mustNotList?.HasFocus == true)
                listType = "mustnot";

            ShowItemSelectorAndAdd(category, listType);
        }
        finally
        {
            _isDialogOpen = false;
        }
    }

    private void ShowItemSelectorAndAdd(string category, string listType)
    {
        var itemDialog = new ItemSelectorDialog(category);
        Application.Run(itemDialog);

        if (itemDialog.SelectedItem != null)
        {
            var displayText = $"{itemDialog.SelectedItem} ({category})";

            switch (listType)
            {
                case "must":
                    _mustItems.Add(displayText);
                    _mustList.SetSource(new ObservableCollection<string>(_mustItems));
                    _statusLabel.Text = $"Added '{itemDialog.SelectedItem}' to MUST list";
                    break;
                case "should":
                    _shouldItems.Add(displayText);
                    _shouldList.SetSource(new ObservableCollection<string>(_shouldItems));
                    _statusLabel.Text = $"Added '{itemDialog.SelectedItem}' to SHOULD list";
                    break;
                case "mustnot":
                    _mustNotItems.Add(displayText);
                    _mustNotList?.SetSource(new ObservableCollection<string>(_mustNotItems));
                    _statusLabel.Text = $"Added '{itemDialog.SelectedItem}' to MUST NOT list";
                    break;
            }
        }
    }

    private void RemoveItem(string listType)
    {
        switch (listType)
        {
            case "must":
                if (_mustList.SelectedItem >= 0 && _mustList.SelectedItem < _mustItems.Count)
                {
                    _mustItems.RemoveAt(_mustList.SelectedItem);
                    _mustList.SetSource(new ObservableCollection<string>(_mustItems));
                    _statusLabel.Text = "Item removed from MUST list";
                }
                break;
            case "should":
                if (_shouldList.SelectedItem >= 0 && _shouldList.SelectedItem < _shouldItems.Count)
                {
                    _shouldItems.RemoveAt(_shouldList.SelectedItem);
                    _shouldList.SetSource(new ObservableCollection<string>(_shouldItems));
                    _statusLabel.Text = "Item removed from SHOULD list";
                }
                break;
            case "mustnot":
                if (
                    _mustNotList?.SelectedItem >= 0
                    && _mustNotList.SelectedItem < _mustNotItems.Count
                )
                {
                    _mustNotItems.RemoveAt(_mustNotList.SelectedItem);
                    _mustNotList.SetSource(new ObservableCollection<string>(_mustNotItems));
                    _statusLabel.Text = "Item removed from MUST NOT list";
                }
                break;
        }
    }

    private MotelyJsonConfig.MotleyJsonFilterClause ParseDisplayTextToClause(string displayText)
    {
        // Parse format: "ItemName (Category)"
        var lastParenIndex = displayText.LastIndexOf('(');
        if (lastParenIndex < 0)
            return new MotelyJsonConfig.MotleyJsonFilterClause
            {
                Type = "Joker",
                Value = displayText,
                Antes = new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, // Default: search all antes
            };

        var itemName = displayText.Substring(0, lastParenIndex).Trim();
        var category = displayText.Substring(lastParenIndex + 1).TrimEnd(')').Trim();

        // Map category to type
        var type = category switch
        {
            "Joker" => "Joker",
            "Legendary" => "SoulJoker",
            "Card" => "PlayingCard",
            "Tarot" => "TarotCard",
            "Spectral" => "SpectralCard",
            "Planet" => "PlanetCard",
            "Voucher" => "Voucher",
            "Boss" => "BossBlind",
            "Tags" => "Tag",
            _ => "Joker",
        };

        return new MotelyJsonConfig.MotleyJsonFilterClause
        {
            Type = type,
            Value = itemName,
            Antes = new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, // Default: search all antes
        };
    }

    private void SaveFilter()
    {
        var dialog = new Dialog()
        {
            Title = "Save Filter",
            X = Pos.Center(),
            Y = Pos.Center(),
            Width = 60,
            Height = 10,
        };

        var nameLabel = new Label()
        {
            X = 1,
            Y = 1,
            Text = "Filter Name:",
        };
        dialog.Add(nameLabel);

        var nameField = new TextField()
        {
            X = Pos.Right(nameLabel) + 1,
            Y = 1,
            Width = 30,
            Text = "",
        };
        dialog.Add(nameField);

        var saveBtn = new Button() { Text = "Save" };
        saveBtn.Accept += (s, e) =>
        {
            var name = nameField.Text;
            if (string.IsNullOrWhiteSpace(name))
            {
                ShowErrorDialog("Error", "Please enter a filter name");
                return;
            }

            try
            {
                // Build MotelyJsonConfig
                var config = new MotelyJsonConfig
                {
                    Name = name,
                    Description = "Created with Filter Builder TUI",
                    Author = Environment.UserName,
                    DateCreated = DateTime.UtcNow,
                    Must = _mustItems.Select(ParseDisplayTextToClause).ToList(),
                    Should = _shouldItems.Select(ParseDisplayTextToClause).ToList(),
                    MustNot = _mustNotItems.Select(ParseDisplayTextToClause).ToList(),
                };

                // Serialize to YAML
                var serializer = new SerializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();
                var yaml = serializer.Serialize(config);

                // Save to file
                var fileName = $"{name.Replace(" ", "_")}.yaml";
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), fileName);
                File.WriteAllText(filePath, yaml);

                _statusLabel.Text = $"Filter '{name}' saved to {fileName}";
                Application.RequestStop(dialog);
            }
            catch (Exception ex)
            {
                ShowErrorDialog("Error", $"Failed to save filter: {ex.Message}");
            }
        };

        var cancelBtn = new Button() { Text = "Cancel" };
        cancelBtn.Accept += (s, e) => Application.RequestStop(dialog);

        dialog.AddButton(saveBtn);
        dialog.AddButton(cancelBtn);

        Application.Run(dialog);
    }

    private void StartSearch()
    {
        if (_mustItems.Count == 0 && _shouldItems.Count == 0)
        {
            ShowErrorDialog(
                "Empty Filter",
                "Please add at least one item to MUST or SHOULD lists before starting a search."
            );
            return;
        }

        // Build MotelyJsonConfig
        var config = new MotelyJsonConfig
        {
            Name = "TUI_QuickFilter",
            Description = "Quick filter from TUI",
            Author = Environment.UserName,
            DateCreated = DateTime.UtcNow,
            Must = _mustItems.Select(ParseDisplayTextToClause).ToList(),
            Should = _shouldItems.Select(ParseDisplayTextToClause).ToList(),
            MustNot = _mustNotItems.Select(ParseDisplayTextToClause).ToList(),
        };

        // Serialize to YAML
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        var yaml = serializer.Serialize(config);

        try
        {
            // Save to temporary file in YamlItemFilters
            var yamlDir = Path.Combine(Directory.GetCurrentDirectory(), "YamlItemFilters");
            if (!Directory.Exists(yamlDir))
            {
                Directory.CreateDirectory(yamlDir);
            }

            var fileName = "TUI_QuickFilter.yaml";
            var filePath = Path.Combine(yamlDir, fileName);
            File.WriteAllText(filePath, yaml);

            _statusLabel.Text = $"Starting search with quick filter...";

            // Launch search window
            var searchWindow = new SearchWindow("TUI_QuickFilter", "yaml");
            Application.Run(searchWindow);
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Error", $"Failed to start search: {ex.Message}");
        }
    }

    // Compact Balatro-styled confirmation dialog (Yes/No)
    private static bool ShowConfirmDialog(string title, string message)
    {
        var dialog = new Dialog()
        {
            Title = title,
            Width = Math.Min(60, message.Length + 10),
            Height = 9,
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.White, ColorName.Black),
                Focus = new Terminal.Gui.Attribute(ColorName.Black, ColorName.BrightRed),
                HotNormal = new Terminal.Gui.Attribute(ColorName.White, ColorName.Black),
                HotFocus = new Terminal.Gui.Attribute(ColorName.White, ColorName.BrightRed),
            },
        };

        var label = new Label()
        {
            X = Pos.Center(),
            Y = 2,
            Text = message,
            TextAlignment = Alignment.Center,
        };
        dialog.Add(label);

        bool result = false;
        var yesBtn = new Button() { Text = "Yes" };
        yesBtn.Accept += (s, e) =>
        {
            result = true;
            Application.RequestStop(dialog);
        };

        var noBtn = new Button() { Text = "No" };
        noBtn.Accept += (s, e) =>
        {
            result = false;
            Application.RequestStop(dialog);
        };

        dialog.AddButton(yesBtn);
        dialog.AddButton(noBtn);

        Application.Run(dialog);
        return result;
    }

    // Compact Balatro-styled error dialog (OK button, red theme)
    private static void ShowErrorDialog(string title, string message)
    {
        var dialog = new Dialog()
        {
            Title = title,
            Width = Math.Min(70, message.Length + 10),
            Height = 10,
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.BrightRed, ColorName.Black),
                Focus = new Terminal.Gui.Attribute(ColorName.Black, ColorName.BrightRed),
                HotNormal = new Terminal.Gui.Attribute(ColorName.White, ColorName.Black),
                HotFocus = new Terminal.Gui.Attribute(ColorName.White, ColorName.BrightRed),
            },
        };

        var label = new Label()
        {
            X = Pos.Center(),
            Y = 2,
            Text = message,
            TextAlignment = Alignment.Center,
        };
        dialog.Add(label);

        var okBtn = new Button() { Text = "OK" };
        okBtn.Accept += (s, e) => Application.RequestStop(dialog);
        dialog.AddButton(okBtn);

        Application.Run(dialog);
    }

    // Compact Balatro-styled choice dialog (3 buttons)
    private static int ShowChoiceDialog(
        string title,
        string message,
        string button1,
        string button2,
        string button3
    )
    {
        var dialog = new Dialog()
        {
            Title = title,
            Width = Math.Min(60, Math.Max(message.Length + 10, 50)),
            Height = 9,
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.White, ColorName.Black),
                Focus = new Terminal.Gui.Attribute(ColorName.Black, ColorName.BrightRed),
                HotNormal = new Terminal.Gui.Attribute(ColorName.BrightYellow, ColorName.Black),
                HotFocus = new Terminal.Gui.Attribute(ColorName.BrightYellow, ColorName.BrightRed),
            },
        };

        var label = new Label()
        {
            X = Pos.Center(),
            Y = 2,
            Text = message,
            TextAlignment = Alignment.Center,
        };
        dialog.Add(label);

        int result = -1;

        var btn1 = new Button() { Text = button1 };
        btn1.Accept += (s, e) =>
        {
            result = 0;
            Application.RequestStop(dialog);
        };

        var btn2 = new Button() { Text = button2 };
        btn2.Accept += (s, e) =>
        {
            result = 1;
            Application.RequestStop(dialog);
        };

        var btn3 = new Button() { Text = button3 };
        btn3.Accept += (s, e) =>
        {
            result = 2;
            Application.RequestStop(dialog);
        };

        dialog.AddButton(btn1);
        dialog.AddButton(btn2);
        dialog.AddButton(btn3);

        Application.Run(dialog);
        return result;
    }
}
