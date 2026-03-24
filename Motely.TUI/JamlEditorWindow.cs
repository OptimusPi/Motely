using System.Collections.ObjectModel;
using Motely.Filters;

namespace Motely.TUI;

public class JamlEditorWindow : Window
{
    private readonly TextView _editor;
    private readonly Label _statusLabel;
    private string? _filePath;

    public JamlEditorWindow(string? filePath = null)
    {
        _filePath = filePath;

        Title = string.IsNullOrWhiteSpace(filePath)
            ? "JAML Editor"
            : $"JAML Editor: {Path.GetFileName(filePath)}";
        X = Pos.Center();
        Y = Pos.Center();
        Width = Dim.Percent(90);
        Height = Dim.Percent(85);
        CanFocus = true;
        ColorScheme = BalatroTheme.Window;

        var toolbar = new View
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill() - 2,
            Height = 1,
        };
        Add(toolbar);

        var saveButton = new CleanButton
        {
            X = 0,
            Y = 0,
            Text = " Save ",
        };
        saveButton.ColorScheme = BalatroTheme.GreenButton;
        saveButton.Accept += (_, _) => SaveEditorContent();
        toolbar.Add(saveButton);

        var runButton = new CleanButton
        {
            X = Pos.Right(saveButton) + 1,
            Y = 0,
            Text = " Save + Search ",
        };
        runButton.ColorScheme = BalatroTheme.BlueButton;
        runButton.Accept += (_, _) => SaveAndSearch();
        toolbar.Add(runButton);

        var loadButton = new CleanButton
        {
            X = Pos.Right(runButton) + 1,
            Y = 0,
            Text = " Load Local ",
        };
        loadButton.ColorScheme = BalatroTheme.PurpleButton;
        loadButton.Accept += (_, _) => LoadLocalFilter();
        toolbar.Add(loadButton);

        _statusLabel = new Label
        {
            X = Pos.Right(loadButton) + 2,
            Y = 0,
            Width = Dim.Fill(),
            Text = string.IsNullOrWhiteSpace(filePath) ? "New JAML document" : filePath,
        };
        toolbar.Add(_statusLabel);

        var editorFrame = new FrameView
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill() - 2,
            Height = Dim.Fill() - 7,
            Title = "JAML",
        };
        editorFrame.ColorScheme = BalatroTheme.InnerPanel;
        Add(editorFrame);

        _editor = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = false,
            WordWrap = false,
            CanFocus = true,
            Text = LoadInitialText(filePath),
        };
        editorFrame.Add(_editor);

        var backButton = new CleanButton
        {
            X = 1,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill() - 2,
            Text = "Back",
            TextAlignment = Alignment.Center,
        };
        backButton.ColorScheme = BalatroTheme.BackButton;
        backButton.Accept += (_, _) => MotelyTUI.CloseWindow(this);
        Add(backButton);

        KeyDown += (_, e) =>
        {
            if (e.IsCtrl && e.KeyCode == KeyCode.S)
            {
                SaveEditorContent();
                e.Handled = true;
                return;
            }

            if (e.KeyCode == KeyCode.F5)
            {
                SaveAndSearch();
                e.Handled = true;
                return;
            }

            if (e.KeyCode == KeyCode.Esc)
            {
                MotelyTUI.CloseWindow(this);
                e.Handled = true;
            }
        };

        _editor.SetFocus();
    }

    private static string LoadInitialText(string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            return File.ReadAllText(filePath);

        return "name: New Filter\ndescription: Created in Motely.TUI\ndeck: Red\nstake: White\nmust:\n";
    }

    private void LoadLocalFilter()
    {
        var filters = FilterLibrary.DiscoverLocalFilters();
        if (filters.Count == 0)
        {
            ShowMessage("No local filters found.");
            return;
        }

        var dialog = new Dialog
        {
            Title = "Load Local Filter",
            Width = 60,
            Height = 20,
        };
        dialog.ColorScheme = BalatroTheme.Window;

        var filterList = new ListView
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill() - 2,
            Height = Dim.Fill() - 5,
            CanFocus = true,
        };
        filterList.SetSource(
            new ObservableCollection<string>(filters.Select(static filter => filter.DisplayName).ToList())
        );
        dialog.Add(filterList);

        void LoadSelected()
        {
            var selectedIndex = filterList.SelectedItem;
            if (selectedIndex < 0 || selectedIndex >= filters.Count)
                return;

            var selected = filters[selectedIndex];
            _filePath = selected.FullPath;
            Title = $"JAML Editor: {Path.GetFileName(_filePath)}";
            _editor.Text = File.ReadAllText(selected.FullPath);
            _statusLabel.Text = selected.FullPath;
            Application.RequestStop(dialog);
        }

        var loadButton = new CleanButton
        {
            X = 1,
            Y = Pos.AnchorEnd(3),
            Width = Dim.Fill() - 2,
            Text = "Load",
            TextAlignment = Alignment.Center,
        };
        loadButton.ColorScheme = BalatroTheme.BlueButton;
        loadButton.Accept += (_, _) => LoadSelected();
        dialog.Add(loadButton);

        var backButton = new CleanButton
        {
            X = 1,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill() - 2,
            Text = "Back",
            TextAlignment = Alignment.Center,
        };
        backButton.ColorScheme = BalatroTheme.BackButton;
        backButton.Accept += (_, _) => Application.RequestStop(dialog);
        dialog.Add(backButton);

        filterList.Accepting += (_, _) => LoadSelected();
        Application.Run(dialog);
    }

    private void SaveEditorContent()
    {
        try
        {
            var content = _editor.Text?.ToString() ?? string.Empty;
            if (!JamlConfigLoader.TryLoad(content, out _, out var error))
            {
                ShowMessage(error ?? "Failed to parse JAML.", isError: true);
                return;
            }

            if (string.IsNullOrWhiteSpace(_filePath))
            {
                var requestedName = PromptForName();
                if (string.IsNullOrWhiteSpace(requestedName))
                    return;

                _filePath = FilterLibrary.SaveJamlFilter(requestedName, content);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
                File.WriteAllText(_filePath, content);
            }

            Title = $"JAML Editor: {Path.GetFileName(_filePath)}";
            _statusLabel.Text = _filePath;
            ShowMessage($"Saved {_filePath}");
        }
        catch (Exception ex)
        {
            ShowMessage($"Save failed: {ex.Message}", isError: true);
        }
    }

    private void SaveAndSearch()
    {
        SaveEditorContent();
        if (string.IsNullOrWhiteSpace(_filePath) || !File.Exists(_filePath))
            return;

        var searchWindow = new SearchWindow(_filePath, "jaml", TuiSettings.DefaultSource, TuiSettings.DefaultSink);
        MotelyTUI.ShowWindow(searchWindow);
    }

    private string? PromptForName()
    {
        string? result = null;

        var dialog = new Dialog
        {
            Title = "Save JAML Filter",
            Width = 50,
            Height = 10,
        };
        dialog.ColorScheme = BalatroTheme.Window;

        var nameLabel = new Label
        {
            X = 1,
            Y = 1,
            Text = "Filter name:",
        };
        dialog.Add(nameLabel);

        var nameField = new TextField
        {
            X = Pos.Right(nameLabel) + 1,
            Y = 1,
            Width = 26,
            Text = _filePath is null ? string.Empty : Path.GetFileNameWithoutExtension(_filePath),
        };
        dialog.Add(nameField);

        var saveButton = new CleanButton
        {
            X = 1,
            Y = Pos.AnchorEnd(1),
            Text = "Save",
        };
        saveButton.ColorScheme = BalatroTheme.GreenButton;
        saveButton.Accept += (_, _) =>
        {
            result = nameField.Text?.ToString();
            Application.RequestStop(dialog);
        };
        dialog.Add(saveButton);

        var backButton = new CleanButton
        {
            X = Pos.Right(saveButton) + 1,
            Y = Pos.AnchorEnd(1),
            Text = "Back",
        };
        backButton.ColorScheme = BalatroTheme.BackButton;
        backButton.Accept += (_, _) => Application.RequestStop(dialog);
        dialog.Add(backButton);

        Application.Run(dialog);
        return result;
    }

    private void ShowMessage(string message, bool isError = false)
    {
        _statusLabel.Text = message;
        _statusLabel.ColorScheme =
            new ColorScheme
            {
                Normal = new Attribute(
                    isError ? BalatroTheme.Red : BalatroTheme.Green,
                    BalatroTheme.DarkGrey
                ),
            };
    }
}
