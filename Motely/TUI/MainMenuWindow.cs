using System.Collections.ObjectModel;
using System;
using System.IO;
using System.Linq;
using Terminal.Gui;

namespace Motely.TUI;

public class MainMenuWindow : Window
{
    private BalatroShaderBackground? _background;

    public MainMenuWindow()
    {
        Title = "Motely - Balatro Seed Searcher";
        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();
        CanFocus = true;

        // Add optimized Balatro shader background (now efficient!)
        _background = new BalatroShaderBackground();
        Add(_background);
        _background.Start();

        // HORIZONTAL LAYOUT: Jimbo on left, title/subtitle on right

        // Add detailed Jimbo face on the LEFT
        var jimboLabel = new Label()
        {
            X = 2,
            Y = 1,
            Text = JimboArt.JimboFace,
            TextAlignment = Alignment.Start,
            CanFocus = false,
        };
        Add(jimboLabel);

        // Title on the RIGHT of Jimbo
        var titleLabel = new Label()
        {
            X = Pos.Right(jimboLabel) + 3,
            Y = 2,
            Text = "BALATRO SEED SEARCHER",
            TextAlignment = Alignment.Start,
            CanFocus = false,
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.BrightRed, ColorName.Black), // Signature Balatro red
            },
        };
        Add(titleLabel);

        // Subtitle below title
        var subtitleLabel = new Label()
        {
            X = Pos.Right(jimboLabel) + 3,
            Y = Pos.Bottom(titleLabel),
            Text = "\"Jimbo says: Choose your adventure!\"",
            TextAlignment = Alignment.Start,
            CanFocus = false,
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.White, ColorName.Black),
            },
        };
        Add(subtitleLabel);

        // Menu buttons with proper Terminal.Gui hotkeys using underscores
        var btnLoadConfig = new Button()
        {
            X = 2,
            Y = Pos.Bottom(jimboLabel) + 1,
            Text = "_Load Config File (JSON/YAML/TOML)",
            Width = 45,
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.White, ColorName.Black),
                Focus = new Terminal.Gui.Attribute(ColorName.Black, ColorName.BrightRed),
                HotNormal = new Terminal.Gui.Attribute(ColorName.BrightRed, ColorName.Black),
                HotFocus = new Terminal.Gui.Attribute(ColorName.White, ColorName.BrightRed)
            }
        };
        btnLoadConfig.Accept += (s, e) => ShowLoadConfig();
        Add(btnLoadConfig);

        var btnBuildFilter = new Button()
        {
            X = 2,
            Y = Pos.Bottom(btnLoadConfig),
            Text = "_Build Custom Filter",
            Width = 45,
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.White, ColorName.Black),
                Focus = new Terminal.Gui.Attribute(ColorName.Black, ColorName.BrightRed),
                HotNormal = new Terminal.Gui.Attribute(ColorName.BrightRed, ColorName.Black),
                HotFocus = new Terminal.Gui.Attribute(ColorName.White, ColorName.BrightRed)
            }
        };
        btnBuildFilter.Accept += (s, e) =>
        {
            var filterBuilder = new FilterBuilderWindow();
            Application.Run(filterBuilder);
        };
        Add(btnBuildFilter);

        var btnApiServer = new Button()
        {
            X = 2,
            Y = Pos.Bottom(btnBuildFilter),
            Text = "Start _API Server",
            Width = 45,
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.White, ColorName.Black),
                Focus = new Terminal.Gui.Attribute(ColorName.Black, ColorName.BrightRed),
                HotNormal = new Terminal.Gui.Attribute(ColorName.BrightRed, ColorName.Black),
                HotFocus = new Terminal.Gui.Attribute(ColorName.White, ColorName.BrightRed)
            }
        };
        btnApiServer.Accept += (s, e) =>
        {
            var apiWindow = new ApiServerWindow(TuiSettings.ApiServerHost, TuiSettings.ApiServerPort);
            Application.Run(apiWindow);
        };
        Add(btnApiServer);

        var btnSettings = new Button()
        {
            X = 2,
            Y = Pos.Bottom(btnApiServer),
            Text = "_Settings",
            Width = 45,
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.White, ColorName.Black),
                Focus = new Terminal.Gui.Attribute(ColorName.Black, ColorName.BrightRed),
                HotNormal = new Terminal.Gui.Attribute(ColorName.BrightRed, ColorName.Black),
                HotFocus = new Terminal.Gui.Attribute(ColorName.White, ColorName.BrightRed)
            }
        };
        btnSettings.Accept += (s, e) =>
        {
            var settingsWindow = new SettingsWindow();
            Application.Run(settingsWindow);
        };
        Add(btnSettings);

        var btnExit = new Button()
        {
            X = 2,
            Y = Pos.Bottom(btnSettings),
            Text = "E_xit",
            Width = 45,
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.White, ColorName.Black),
                Focus = new Terminal.Gui.Attribute(ColorName.Black, ColorName.BrightRed),
                HotNormal = new Terminal.Gui.Attribute(ColorName.BrightRed, ColorName.Black),
                HotFocus = new Terminal.Gui.Attribute(ColorName.White, ColorName.BrightRed)
            }
        };
        btnExit.Accept += (s, e) =>
        {
            if (ShowConfirmDialog("Exit Motely", "Are you sure you want to exit?"))
            {
                _background?.Stop();
                Application.RequestStop();
            }
        };
        Add(btnExit);

        // Instructions
        var instructionsLabel = new Label()
        {
            X = 2,
            Y = Pos.Bottom(btnExit) + 1,
            Text = "Use UP/DOWN arrows or TAB, press underlined letter for hotkey, ESC to quit",
            TextAlignment = Alignment.Start,
            CanFocus = false,
        };
        Add(instructionsLabel);

        // Version info
        var versionLabel = new Label()
        {
            X = 2,
            Y = Pos.Bottom(instructionsLabel) + 1,
            Text = "Motely v1.0 - Powered by Terminal.Gui",
            TextAlignment = Alignment.Start,
            CanFocus = false,
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.Gray, ColorName.Black),
            },
        };
        Add(versionLabel);

        // Set focus to first button
        btnLoadConfig.SetFocus();

        // Handle ESC key with confirmation dialog
        KeyDown += (sender, e) =>
        {
            if (e.KeyCode == KeyCode.Esc)
            {
                if (ShowConfirmDialog("Exit Motely", "Are you sure you want to exit?"))
                {
                    _background?.Stop();
                    Application.RequestStop();
                }
                e.Handled = true;
            }
        };
    }

    private void ShowLoadConfig()
    {
        var filters = new System.Collections.Generic.List<(string name, string format)>();

        // Scan for available filters
        var currentDir = Directory.GetCurrentDirectory();

        if (Directory.Exists(Path.Combine(currentDir, "JsonItemFilters")))
        {
            var jsonFiles = Directory.GetFiles(
                Path.Combine(currentDir, "JsonItemFilters"),
                "*.json"
            );
            foreach (var file in jsonFiles)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                filters.Add((name, "json"));
            }
        }

        if (Directory.Exists(Path.Combine(currentDir, "YamlItemFilters")))
        {
            var yamlFiles = Directory.GetFiles(
                Path.Combine(currentDir, "YamlItemFilters"),
                "*.yaml"
            );
            foreach (var file in yamlFiles)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                filters.Add((name, "yaml"));
            }
        }

        if (Directory.Exists(Path.Combine(currentDir, "TomlItemFilters")))
        {
            var tomlFiles = Directory.GetFiles(
                Path.Combine(currentDir, "TomlItemFilters"),
                "*.toml"
            );
            foreach (var file in tomlFiles)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                filters.Add((name, "toml"));
            }
        }

        if (filters.Count == 0)
        {
            ShowErrorDialog(
                "No Filters Found",
                "No filter files found in JsonItemFilters/, YamlItemFilters/, or TomlItemFilters/"
            );
            return;
        }

        var dialog = new Dialog()
        {
            Title = "Load Config File",
            Width = 60,
            Height = 20,
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.White, ColorName.Black),
                Focus = new Terminal.Gui.Attribute(ColorName.Black, ColorName.BrightRed),
                HotNormal = new Terminal.Gui.Attribute(ColorName.White, ColorName.Black),
                HotFocus = new Terminal.Gui.Attribute(ColorName.White, ColorName.BrightRed)
            }
        };

        var instructionLabel = new Label() { X = 1, Y = 1, Text = "Select a filter to run:" };
        dialog.Add(instructionLabel);

        var filterStrings = filters
            .Select((f, i) => $"{f.name} ({f.format.ToUpper()})")
            .ToArray();
        var filterList = new ListView()
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill() - 2,
            Height = Dim.Fill() - 5,
            AllowsMarking = false,
            CanFocus = true,
        };
        filterList.SetSource(new ObservableCollection<string>(filterStrings));

        // Handle Enter key for selection
        filterList.KeyDown += (sender, e) =>
        {
            if (e.KeyCode == KeyCode.Enter)
            {
                if (filterList.SelectedItem >= 0 && filterList.SelectedItem < filters.Count)
                {
                    var selected = filters[filterList.SelectedItem];
                    Application.RequestStop(dialog);

                    var searchWindow = new SearchWindow(selected.name, selected.format);
                    Application.Run(searchWindow);
                }
                e.Handled = true;
            }
        };

        dialog.Add(filterList);

        var cancelBtn = new Button() { Text = "Cancel" };
        cancelBtn.Accept += (s, e) => Application.RequestStop(dialog);
        dialog.AddButton(cancelBtn);

        filterList.SetFocus();
        Application.Run(dialog);
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
                HotFocus = new Terminal.Gui.Attribute(ColorName.White, ColorName.BrightRed)
            }
        };

        var label = new Label()
        {
            X = Pos.Center(),
            Y = 2,
            Text = message,
            TextAlignment = Alignment.Center
        };
        dialog.Add(label);

        bool result = false;
        var yesBtn = new Button() { Text = "Yes" };
        yesBtn.Accept += (s, e) => { result = true; Application.RequestStop(dialog); };

        var noBtn = new Button() { Text = "No" };
        noBtn.Accept += (s, e) => { result = false; Application.RequestStop(dialog); };

        dialog.AddButton(yesBtn);
        dialog.AddButton(noBtn);

        Application.Run(dialog);
        return result;
    }

    // Compact Balatro-styled info dialog (OK button)
    private static void ShowInfoDialog(string title, string message)
    {
        var dialog = new Dialog()
        {
            Title = title,
            Width = Math.Min(60, message.Length + 10),
            Height = 9,
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.White, ColorName.Black),
                Focus = new Terminal.Gui.Attribute(ColorName.Black, ColorName.BrightBlue),
                HotNormal = new Terminal.Gui.Attribute(ColorName.White, ColorName.Black),
                HotFocus = new Terminal.Gui.Attribute(ColorName.White, ColorName.BrightBlue)
            }
        };

        var label = new Label()
        {
            X = Pos.Center(),
            Y = 2,
            Text = message,
            TextAlignment = Alignment.Center
        };
        dialog.Add(label);

        var okBtn = new Button() { Text = "OK" };
        okBtn.Accept += (s, e) => Application.RequestStop(dialog);
        dialog.AddButton(okBtn);

        Application.Run(dialog);
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
                HotFocus = new Terminal.Gui.Attribute(ColorName.White, ColorName.BrightRed)
            }
        };

        var label = new Label()
        {
            X = Pos.Center(),
            Y = 2,
            Text = message,
            TextAlignment = Alignment.Center
        };
        dialog.Add(label);

        var okBtn = new Button() { Text = "OK" };
        okBtn.Accept += (s, e) => Application.RequestStop(dialog);
        dialog.AddButton(okBtn);

        Application.Run(dialog);
    }

}
