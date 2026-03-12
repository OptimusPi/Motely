using System.Collections.ObjectModel;
using System.Text.Json;
using Motely.Filters;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Motely.TUI;

public class FilterBuilderWindow : Window
{
    private ListView _mustList;
    private ListView _shouldList;
    private List<string> _mustItems = new(); // TODO use REAL motely enums
    private List<string> _shouldItems = new();
    private List<string> _mustNotItems = new();
    private Label _statusLabel;
    private CleanButton _startSearchBtn;
    private bool _isDialogOpen = false;

    public FilterBuilderWindow()
    {
        Title = "Filter Builder";
        X = Pos.Center();
        Y = Pos.Center();
        Width = 90;
        Height = 24;
        SetScheme(BalatroTheme.Window);

        // Create two columns in inner panel boxes
        var yStart = 3;

        // FILTER ITEMS panel (was MUST)
        var filterPanel = new FrameView()
        {
            X = 2,
            Y = yStart,
            Width = 40,
            Height = 13,
            Title = "Filter Items (Required)",
        };
        filterPanel.SetScheme(BalatroTheme.InnerPanel);
        Add(filterPanel);

        _mustList = new ListView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 2,
            CanFocus = true,
        };
        _mustList.SetScheme(
            new Scheme()
            {
                Normal = new Attribute(BalatroTheme.White, BalatroTheme.InnerPanelGrey),
                Focus = new Attribute(BalatroTheme.White, BalatroTheme.Blue),
                HotNormal = new Attribute(BalatroTheme.White, BalatroTheme.InnerPanelGrey),
                HotFocus = new Attribute(BalatroTheme.White, BalatroTheme.Blue),
            }
        );
        _mustList.SetSource(new ObservableCollection<string>(_mustItems));
        filterPanel.Add(_mustList);

        var mustAddBtn = new CleanButton()
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Text = " + Add ",
        };
        mustAddBtn.SetScheme(BalatroTheme.GreenButton);
        mustAddBtn.Accept += (s, e) => AddItem("must");
        filterPanel.Add(mustAddBtn);

        var mustRemoveBtn = new CleanButton()
        {
            X = Pos.Right(mustAddBtn) + 1,
            Y = Pos.AnchorEnd(1),
            Text = " - Remove ",
        };
        mustRemoveBtn.SetScheme(BalatroTheme.ModalButton);
        mustRemoveBtn.Accept += (s, e) => RemoveItem("must");
        filterPanel.Add(mustRemoveBtn);

        // SCORE ITEMS panel (was SHOULD)
        var scorePanel = new FrameView()
        {
            X = Pos.Right(filterPanel) + 2,
            Y = yStart,
            Width = 40,
            Height = 13,
            Title = "Score Items (Bonus Points)",
        };
        scorePanel.SetScheme(BalatroTheme.InnerPanel);
        Add(scorePanel);

        _shouldList = new ListView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill() - 2,
            CanFocus = true,
        };
        _shouldList.SetScheme(
            new Scheme()
            {
                Normal = new Attribute(BalatroTheme.White, BalatroTheme.InnerPanelGrey),
                Focus = new Attribute(BalatroTheme.White, BalatroTheme.Blue),
                HotNormal = new Attribute(BalatroTheme.White, BalatroTheme.InnerPanelGrey),
                HotFocus = new Attribute(BalatroTheme.White, BalatroTheme.Blue),
            }
        );
        _shouldList.SetSource(new ObservableCollection<string>(_shouldItems));
        scorePanel.Add(_shouldList);

        var shouldAddBtn = new CleanButton()
        {
            X = 0,
            Y = Pos.AnchorEnd(1),
            Text = " + Add ",
        };
        shouldAddBtn.SetScheme(BalatroTheme.GreenButton);
        shouldAddBtn.Accept += (s, e) => AddItem("should");
        scorePanel.Add(shouldAddBtn);

        var shouldRemoveBtn = new CleanButton()
        {
            X = Pos.Right(shouldAddBtn) + 1,
            Y = Pos.AnchorEnd(1),
            Text = " - Remove ",
        };
        shouldRemoveBtn.SetScheme(BalatroTheme.ModalButton);
        shouldRemoveBtn.Accept += (s, e) => RemoveItem("should");
        scorePanel.Add(shouldRemoveBtn);

        // Action buttons row (above Back)
        // Start Search button - initially disabled until filter is saved
        _startSearchBtn = new CleanButton()
        {
            X = 2,
            Y = Pos.AnchorEnd(4),
            Text = " save first... ",
            Enabled = false,
        };
        _startSearchBtn.SetScheme(
            new Scheme()
            {
                Normal = new Attribute(BalatroTheme.Red, BalatroTheme.DarkGrey),
                Focus = new Attribute(BalatroTheme.Red, BalatroTheme.DarkGrey),
                HotNormal = new Attribute(BalatroTheme.Red, BalatroTheme.DarkGrey),
                HotFocus = new Attribute(BalatroTheme.Red, BalatroTheme.DarkGrey),
            }
        );
        _startSearchBtn.Accept += (s, e) => StartSearch();
        Add(_startSearchBtn);

        var saveBtn = new CleanButton()
        {
            X = Pos.Right(_startSearchBtn) + 2,
            Y = Pos.AnchorEnd(4),
            Text = " Save Filter ",
        };
        saveBtn.SetScheme(BalatroTheme.GreenButton);
        saveBtn.Accept += (s, e) => SaveFilter();
        Add(saveBtn);

        // Load Filter button - purple for "import" feel
        var loadBtn = new CleanButton()
        {
            X = Pos.Right(saveBtn) + 2,
            Y = Pos.AnchorEnd(4),
            Text = " Load Filter ",
        };
        loadBtn.SetScheme(BalatroTheme.PurpleButton);
        loadBtn.Accept += (s, e) => LoadFilter();
        Add(loadBtn);

        // Status label (same row as action buttons)
        _statusLabel = new Label()
        {
            X = Pos.Right(loadBtn) + 4,
            Y = Pos.AnchorEnd(4),
            Width = Dim.Fill(),
            Text = "",
        };
        Add(_statusLabel);

        // Back button - FULL WIDTH at very bottom
        var backBtn = new CleanButton()
        {
            X = 1,
            Y = Pos.AnchorEnd(1),
            Text = "Back",
            Width = Dim.Fill() - 2,
            TextAlignment = Alignment.Center,
        };
        backBtn.SetScheme(BalatroTheme.BackButton);
        backBtn.Accept += (s, e) => MotelyTUI.CloseWindow(this);
        Add(backBtn);

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
                    "Bac_k"
                );

                if (choice == 0) // Main Menu
                {
                    MotelyTUI.CloseWindow(this);
                }
                else if (choice == 1) // Exit
                {
                    MotelyTUI.App?.RequestStop();
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
            App?.Run(categoryDialog);

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
            string listType = "must"; // default to Filter Items
            if (_shouldList.HasFocus)
                listType = "should";

            ShowItemSelectorAndAdd(category, listType);
        }
        finally
        {
            _isDialogOpen = false;
        }
    }

    private void ShowItemSelectorAndAdd(string category, string listType, bool banItem = false)
    {
        var itemDialog = new ItemSelectorDialog(category);
        App?.Run(itemDialog);

        if (itemDialog.SelectedItem != null)
        {
            var displayText = $"{itemDialog.SelectedItem} ({category})";

            // Check if ban item was selected in dialog
            if (itemDialog.BanItem || banItem)
            {
                _mustNotItems.Add(displayText);
                _statusLabel.Text = $"Banned '{itemDialog.SelectedItem}'";
            }
            else
            {
                switch (listType)
                {
                    case "must":
                        _mustItems.Add(displayText);
                        _mustList.SetSource(new ObservableCollection<string>(_mustItems));
                        _statusLabel.Text = $"Added '{itemDialog.SelectedItem}' to Filter Items";
                        break;
                    case "should":
                        _shouldItems.Add(displayText);
                        _shouldList.SetSource(new ObservableCollection<string>(_shouldItems));
                        _statusLabel.Text = $"Added '{itemDialog.SelectedItem}' to Score Items";
                        break;
                }
            }
        }
    }

    private void RemoveItem(string listType)
    {
        switch (listType)
        {
            case "must":
                var mustSelectedIndex = _mustList.SelectedItem ?? 0;
                if (mustSelectedIndex >= 0 && mustSelectedIndex < _mustItems.Count)
                {
                    _mustItems.RemoveAt(mustSelectedIndex);
                    _mustList.SetSource(new ObservableCollection<string>(_mustItems));
                    _statusLabel.Text = "Item removed from Filter Items";
                }
                break;
            case "should":
                var shouldSelectedIndex = _shouldList.SelectedItem ?? 0;
                if (shouldSelectedIndex >= 0 && shouldSelectedIndex < _shouldItems.Count)
                {
                    _shouldItems.RemoveAt(shouldSelectedIndex);
                    _shouldList.SetSource(new ObservableCollection<string>(_shouldItems));
                    _statusLabel.Text = "Item removed from Score Items";
                }
                break;
        }
    }

    private JamlClauseDto ParseDisplayTextToClause(string displayText)
    {
        var lastParenIndex = displayText.LastIndexOf('(');
        if (lastParenIndex < 0)
            return new JamlClauseDto { Joker = displayText };

        var itemName = displayText.Substring(0, lastParenIndex).Trim();
        var category = displayText.Substring(lastParenIndex + 1).TrimEnd(')').Trim();

        return category switch
        {
            "Legendary" => new JamlClauseDto { SoulJoker = itemName },
            "Tarot" => new JamlClauseDto { Tarot = itemName },
            "Spectral" => new JamlClauseDto { Spectral = itemName },
            "Planet" => new JamlClauseDto { Planet = itemName },
            "Voucher" => new JamlClauseDto { Voucher = itemName },
            "Boss" => new JamlClauseDto { Boss = itemName },
            "Tags" => new JamlClauseDto { Tag = itemName },
            "Card" => new JamlClauseDto { StandardCard = itemName },
            _ => new JamlClauseDto { Joker = itemName },
        };
    }

    private static string ClauseToDisplayText(IJamlClause clause) =>
        clause switch
        {
            JokerClause c => $"{(c.Jokers.Length > 0 ? c.Jokers[0].ToString() : "?")} (Joker)",
            LegendaryJokerClause c => $"{(c.Jokers.Length > 0 ? c.Jokers[0].ToString() : "?")} (Legendary)",
            TarotCardClause c => $"{(c.Tarots.Length > 0 ? c.Tarots[0].ToString() : "?")} (Tarot)",
            SpectralCardClause c => $"{(c.Spectrals.Length > 0 ? c.Spectrals[0].ToString() : "?")} (Spectral)",
            PlanetCardClause c => $"{(c.Planets.Length > 0 ? c.Planets[0].ToString() : "?")} (Planet)",
            VoucherClause c => $"{(c.Vouchers.Length > 0 ? c.Vouchers[0].ToString() : "?")} (Voucher)",
            BossClause c => $"{(c.Bosses.Length > 0 ? c.Bosses[0].ToString() : "?")} (Boss)",
            TagClause c => $"{(c.Tags.Length > 0 ? c.Tags[0].ToString() : "?")} (Tags)",
            StandardCardClause c => $"{c.Rank} {c.Suit} (Card)",
            _ => clause.GetType().Name,
        };

    private string? _loadedFilterPath; // Track loaded filter path for Start Search

    private void LoadFilter()
    {
        var filters = FilterLibrary.DiscoverLocalFilters();

        if (filters.Count == 0)
        {
            ShowErrorDialog(
                "No Filters Found",
                "No filter files found in JamlFilters/ or JsonFilters/"
            );
            return;
        }

        var dialog = new Dialog()
        {
            Title = "Load Filter",
            Width = 60,
            Height = 20,
        };
        dialog.SetScheme(BalatroTheme.Window);

        var instructionLabel = new Label()
        {
            X = Pos.Center(),
            Y = 1,
            Text = "Select a filter to load (JAML first, then JSON):",
            TextAlignment = Alignment.Center,
        };
        dialog.Add(instructionLabel);

        var filterStrings = filters.Select(static filter => filter.DisplayName).ToArray();
        var filterList = new ListView()
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill() - 2,
            Height = Dim.Fill() - 7,
            CanFocus = true,
        };
        filterList.SetScheme(
            new Scheme()
            {
                Normal = new Attribute(BalatroTheme.White, BalatroTheme.DarkGrey),
                Focus = new Attribute(BalatroTheme.White, BalatroTheme.Blue),
                HotNormal = new Attribute(BalatroTheme.White, BalatroTheme.DarkGrey),
                HotFocus = new Attribute(BalatroTheme.White, BalatroTheme.Blue),
            }
        );
        filterList.SetSource(new ObservableCollection<string>(filterStrings));
        filterList.SelectedItem = 0;

        void DoLoad()
        {
            var selectedIndex = filterList.SelectedItem ?? 0;
            if (selectedIndex >= 0 && selectedIndex < filters.Count)
            {
                var selected = filters[selectedIndex];
                try
                {
                    var content = File.ReadAllText(selected.FullPath);
                    if (!JamlConfigLoader.TryLoad(content, out var config, out _) || config == null)
                    {
                        ShowErrorDialog("Load Error", "Failed to parse filter file");
                        return;
                    }

                    _mustItems.Clear();
                    _shouldItems.Clear();
                    _mustNotItems.Clear();

                    foreach (var clause in config.Must)
                        _mustItems.Add(ClauseToDisplayText(clause));
                    foreach (var clause in config.Should)
                        _shouldItems.Add(ClauseToDisplayText(clause));

                    // Update list views
                    _mustList.SetSource(new ObservableCollection<string>(_mustItems));
                    _shouldList.SetSource(new ObservableCollection<string>(_shouldItems));

                    // Enable Start Search since filter is now loaded
                    _loadedFilterPath = selected.FullPath;
                    _startSearchBtn.Text = " Start Search ";
                    _startSearchBtn.Enabled = true;
                    _startSearchBtn.SetScheme(BalatroTheme.BlueButton);

                    _statusLabel.Text = $"Loaded: {selected.DisplayName}";
                    App?.RequestStop(dialog);
                }
                catch (Exception ex)
                {
                    ShowErrorDialog("Load Error", $"Failed to load filter: {ex.Message}");
                }
            }
        }

        filterList.KeyDown += (sender, e) =>
        {
            if (e.KeyCode == KeyCode.Enter)
            {
                DoLoad();
                e.Handled = true;
            }
        };

        filterList.Accepting += (sender, e) => DoLoad();
        dialog.Add(filterList);

        var loadBtn = new CleanButton()
        {
            X = 1,
            Y = Pos.AnchorEnd(3),
            Text = "Load Filter",
            Width = Dim.Fill() - 2,
            TextAlignment = Alignment.Center,
        };
        loadBtn.SetScheme(BalatroTheme.BlueButton);
        loadBtn.Accept += (s, e) => DoLoad();
        dialog.Add(loadBtn);

        var cancelBtn = new CleanButton()
        {
            X = 1,
            Y = Pos.AnchorEnd(1),
            Text = "Back",
            Width = Dim.Fill() - 2,
            TextAlignment = Alignment.Center,
        };
        cancelBtn.SetScheme(BalatroTheme.BackButton);
        cancelBtn.Accept += (s, e) => App?.RequestStop(dialog);
        dialog.Add(cancelBtn);

        filterList.SetFocus();
        App?.Run(dialog);
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

        var saveBtn = new CleanButton() { Text = " Save " };
        saveBtn.SetScheme(BalatroTheme.BlueButton);
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
                var config = new JamlDto
                {
                    Name = name,
                    Description = "Created with Filter Builder TUI",
                    Author = Environment.UserName,
                    Must = _mustItems.Select(ParseDisplayTextToClause).ToList(),
                    Should = _shouldItems.Select(ParseDisplayTextToClause).ToList(),
                    MustNot = _mustNotItems.Select(ParseDisplayTextToClause).ToList(),
                };

                var serializer = new SerializerBuilder()
                    .WithNamingConvention(NullNamingConvention.Instance)
                    .DisableAliases() // Prevent &o0/*o0 anchor/alias references
                    .ConfigureDefaultValuesHandling(
                        DefaultValuesHandling.OmitNull
                            | DefaultValuesHandling.OmitEmptyCollections
                            | DefaultValuesHandling.OmitDefaults
                    )
                    .Build();
                var jaml = serializer.Serialize(config);

                var filePath = FilterLibrary.SaveJamlFilter(name.ToString() ?? "Untitled", jaml);

                _statusLabel.Text = $"Filter '{name}' saved to {Path.GetFileName(filePath)}";
                _loadedFilterPath = filePath;

                // Enable Start Search button now that filter is saved
                _startSearchBtn.Text = " Start Search ";
                _startSearchBtn.Enabled = true;
                _startSearchBtn.SetScheme(BalatroTheme.BlueButton);

                App?.RequestStop(dialog);
            }
            catch (Exception ex)
            {
                ShowErrorDialog("Error", $"Failed to save filter: {ex.Message}");
            }
        };

        var cancelBtn = new CleanButton()
        {
            X = Pos.Right(saveBtn) + 2,
            Y = Pos.AnchorEnd(1),
            Text = " Back ",
        };
        cancelBtn.SetScheme(BalatroTheme.BackButton);
        cancelBtn.Accept += (s, e) => App?.RequestStop(dialog);

        saveBtn.X = 2;
        saveBtn.Y = Pos.AnchorEnd(1);
        dialog.Add(saveBtn);
        dialog.Add(cancelBtn);

        App?.Run(dialog);
    }

    private void StartSearch()
    {
        // If we loaded a filter directly, use that file
        if (!string.IsNullOrEmpty(_loadedFilterPath) && File.Exists(_loadedFilterPath))
        {
            var format = _loadedFilterPath.EndsWith(".jaml", StringComparison.OrdinalIgnoreCase)
                ? "jaml"
                : "json";
            _statusLabel.Text = $"Starting search with loaded filter...";
            var searchWindow = new SearchWindow(
                _loadedFilterPath,
                format,
                TuiSettings.DefaultSource,
                TuiSettings.DefaultSink
            );
            MotelyTUI.ShowWindow(searchWindow);
            return;
        }

        if (_mustItems.Count == 0 && _shouldItems.Count == 0)
        {
            ShowErrorDialog(
                "Empty Filter",
                "Please add at least one item to MUST or SHOULD lists before starting a search."
            );
            return;
        }

        var config = new JamlDto
        {
            Name = "TUI_QuickFilter",
            Description = "Quick filter from TUI",
            Author = Environment.UserName,
            Must = _mustItems.Select(ParseDisplayTextToClause).ToList(),
            Should = _shouldItems.Select(ParseDisplayTextToClause).ToList(),
            MustNot = _mustNotItems.Select(ParseDisplayTextToClause).ToList(),
        };

        var serializer = new SerializerBuilder()
            .WithNamingConvention(NullNamingConvention.Instance)
            .DisableAliases() // Prevent &o0/*o0 anchor/alias references
            .ConfigureDefaultValuesHandling(
                DefaultValuesHandling.OmitNull
                    | DefaultValuesHandling.OmitEmptyCollections
                    | DefaultValuesHandling.OmitDefaults
            )
            .Build();
        var jaml = serializer.Serialize(config);

        try
        {
            var filePath = FilterLibrary.SaveJamlFilter("TUI_QuickFilter", jaml);

            _statusLabel.Text = $"Starting search with quick filter...";

            // Launch search window with full file path
            var searchWindow = new SearchWindow(
                filePath,
                "jaml",
                TuiSettings.DefaultSource,
                TuiSettings.DefaultSink
            );
            MotelyTUI.ShowWindow(searchWindow);
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Error", $"Failed to start search: {ex.Message}");
        }
    }

    // Balatro-styled error dialog (OK button)
    private static void ShowErrorDialog(string title, string message)
    {
        var dialog = new Dialog()
        {
            Title = title,
            Width = Math.Min(70, message.Length + 10),
            Height = 10,
        };
        dialog.SetScheme(BalatroTheme.Window);

        var label = new Label()
        {
            X = Pos.Center(),
            Y = 2,
            Text = message,
            TextAlignment = Alignment.Center,
        };
        label.SetScheme(BalatroTheme.ErrorText);
        dialog.Add(label);

        var okBtn = new CleanButton()
        {
            X = 1,
            Y = Pos.AnchorEnd(1),
            Text = "Back",
            Width = Dim.Fill() - 2,
            TextAlignment = Alignment.Center,
        };
        okBtn.SetScheme(BalatroTheme.BackButton);
        okBtn.Accept += (s, e) => MotelyTUI.App?.RequestStop(dialog);
        dialog.Add(okBtn);

        MotelyTUI.App?.Run(dialog);
    }

    // Balatro-styled choice dialog (3 buttons)
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
        };
        dialog.SetScheme(BalatroTheme.Window);

        var label = new Label()
        {
            X = Pos.Center(),
            Y = 2,
            Text = message,
            TextAlignment = Alignment.Center,
        };
        dialog.Add(label);

        int result = -1;

        var btn1 = new CleanButton() { Text = $" {button1} " };
        btn1.SetScheme(BalatroTheme.ModalButton);
        btn1.Accept += (s, e) =>
        {
            result = 0;
            MotelyTUI.App?.RequestStop(dialog);
        };

        var btn2 = new CleanButton() { Text = $" {button2} " };
        btn2.SetScheme(BalatroTheme.ModalButton);
        btn2.Accept += (s, e) =>
        {
            result = 1;
            MotelyTUI.App?.RequestStop(dialog);
        };

        var btn3 = new CleanButton()
        {
            X = Pos.Right(btn2) + 2,
            Y = Pos.AnchorEnd(1),
            Text = $" {button3} ",
        };
        btn3.SetScheme(BalatroTheme.BackButton);
        btn3.Accept += (s, e) =>
        {
            result = 2;
            MotelyTUI.App?.RequestStop(dialog);
        };

        btn1.X = 2;
        btn1.Y = Pos.AnchorEnd(1);
        btn2.X = Pos.Right(btn1) + 2;
        btn2.Y = Pos.AnchorEnd(1);
        dialog.Add(btn1);
        dialog.Add(btn2);
        dialog.Add(btn3);

        MotelyTUI.App?.Run(dialog);
        return result;
    }
}
