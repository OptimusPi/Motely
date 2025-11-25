using System;
using Terminal.Gui;

namespace Motely.TUI;

public class SettingsWindow : Window
{
    private TextField _threadCountField;
    private TextField _batchCharCountField;
    private TextField _apiHostField;
    private TextField _apiPortField;

    public SettingsWindow()
    {
        Title = "Settings";
        X = Pos.Center();
        Y = Pos.Center();
        Width = 70;
        Height = 18;
        CanFocus = true;

        ColorScheme = new ColorScheme()
        {
            Normal = new Terminal.Gui.Attribute(ColorName.White, ColorName.Black),
            Focus = new Terminal.Gui.Attribute(ColorName.Black, ColorName.BrightRed),
            HotNormal = new Terminal.Gui.Attribute(ColorName.White, ColorName.Black),
            HotFocus = new Terminal.Gui.Attribute(ColorName.White, ColorName.BrightRed),
        };

        // Title
        var titleLabel = new Label()
        {
            X = Pos.Center(),
            Y = 1,
            Text = "SETTINGS",
            TextAlignment = Alignment.Center,
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.BrightBlue, ColorName.Black),
            },
        };
        Add(titleLabel);

        // Thread Count
        var threadLabel = new Label()
        {
            X = 2,
            Y = 3,
            Text = "Thread Count:",
        };
        Add(threadLabel);

        _threadCountField = new TextField()
        {
            X = 2,
            Y = 4,
            Width = 20,
            Text = TuiSettings.ThreadCount.ToString(),
        };
        Add(_threadCountField);

        var threadHint = new Label()
        {
            X = 24,
            Y = 4,
            Text = $"(1-{Environment.ProcessorCount}, default: {Environment.ProcessorCount})",
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.Gray, ColorName.Black),
            },
        };
        Add(threadHint);

        // Batch Character Count
        var batchLabel = new Label()
        {
            X = 2,
            Y = 6,
            Text = "Batch Character Count:",
        };
        Add(batchLabel);

        _batchCharCountField = new TextField()
        {
            X = 2,
            Y = 7,
            Width = 20,
            Text = TuiSettings.BatchCharacterCount.ToString(),
        };
        Add(_batchCharCountField);

        var batchHint = new Label()
        {
            X = 24,
            Y = 7,
            Text = "(1-7, default: 2, recommended: 2-4)",
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.Gray, ColorName.Black),
            },
        };
        Add(batchHint);

        // API Server Host
        var hostLabel = new Label()
        {
            X = 2,
            Y = 9,
            Text = "API Server Host:",
        };
        Add(hostLabel);

        _apiHostField = new TextField()
        {
            X = 2,
            Y = 10,
            Width = 40,
            Text = TuiSettings.ApiServerHost,
        };
        Add(_apiHostField);

        // API Server Port
        var portLabel = new Label()
        {
            X = 2,
            Y = 12,
            Text = "API Server Port:",
        };
        Add(portLabel);

        _apiPortField = new TextField()
        {
            X = 2,
            Y = 13,
            Width = 20,
            Text = TuiSettings.ApiServerPort.ToString(),
        };
        Add(_apiPortField);

        var portHint = new Label()
        {
            X = 24,
            Y = 13,
            Text = "(1-65535, default: 3141)",
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.Gray, ColorName.Black),
            },
        };
        Add(portHint);

        // Secret option
        var secretButton = new Button()
        {
            X = 2,
            Y = 15,
            Text = "Secret...",
            Width = 20,
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.DarkGray, ColorName.Black),
                Focus = new Terminal.Gui.Attribute(ColorName.Black, ColorName.BrightMagenta),
                HotNormal = new Terminal.Gui.Attribute(ColorName.BrightMagenta, ColorName.Black),
                HotFocus = new Terminal.Gui.Attribute(ColorName.White, ColorName.BrightMagenta),
            },
        };
        secretButton.Accept += (s, e) =>
        {
            // Secret functionality placeholder
            MessageBox.Query("Secret Discovered!", "You found the secret option!\n\nJimbo is proud of you! 🃏", "OK");
        };
        Add(secretButton);

        // Save button
        var saveButton = new Button()
        {
            X = Pos.Center() - 10,
            Y = Pos.AnchorEnd(1),
            Text = "_Save",
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.White, ColorName.Black),
                Focus = new Terminal.Gui.Attribute(ColorName.Black, ColorName.BrightBlue),
                HotNormal = new Terminal.Gui.Attribute(ColorName.BrightBlue, ColorName.Black),
                HotFocus = new Terminal.Gui.Attribute(ColorName.White, ColorName.BrightBlue),
            },
        };
        saveButton.Accept += (s, e) => SaveSettings();
        Add(saveButton);

        // Cancel button
        var cancelButton = new Button()
        {
            X = Pos.Center() + 2,
            Y = Pos.AnchorEnd(1),
            Text = "_Cancel",
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.White, ColorName.Black),
                Focus = new Terminal.Gui.Attribute(ColorName.Black, ColorName.BrightRed),
                HotNormal = new Terminal.Gui.Attribute(ColorName.BrightRed, ColorName.Black),
                HotFocus = new Terminal.Gui.Attribute(ColorName.White, ColorName.BrightRed),
            },
        };
        cancelButton.Accept += (s, e) => Application.RequestStop();
        Add(cancelButton);

        // ESC key handler
        KeyDown += (sender, e) =>
        {
            if (e.KeyCode == KeyCode.Esc)
            {
                Application.RequestStop();
                e.Handled = true;
            }
        };

        _threadCountField.SetFocus();
    }

    private void SaveSettings()
    {
        try
        {
            // Validate and save thread count
            if (int.TryParse(_threadCountField.Text.ToString(), out int threadCount))
            {
                if (threadCount < 1 || threadCount > Environment.ProcessorCount)
                {
                    ShowErrorDialog(
                        "Invalid Thread Count",
                        $"Thread count must be between 1 and {Environment.ProcessorCount}"
                    );
                    return;
                }
                TuiSettings.ThreadCount = threadCount;
            }
            else
            {
                ShowErrorDialog("Invalid Thread Count", "Thread count must be a valid number");
                return;
            }

            // Validate and save batch character count
            if (int.TryParse(_batchCharCountField.Text.ToString(), out int batchCharCount))
            {
                if (batchCharCount < 1 || batchCharCount > 7)
                {
                    ShowErrorDialog(
                        "Invalid Batch Character Count",
                        "Batch character count must be between 1 and 7"
                    );
                    return;
                }
                TuiSettings.BatchCharacterCount = batchCharCount;
            }
            else
            {
                ShowErrorDialog(
                    "Invalid Batch Character Count",
                    "Batch character count must be a valid number"
                );
                return;
            }

            // Validate and save API host
            var host = _apiHostField.Text.ToString();
            if (string.IsNullOrWhiteSpace(host))
            {
                ShowErrorDialog("Invalid API Host", "API host cannot be empty");
                return;
            }
            TuiSettings.ApiServerHost = host;

            // Validate and save API port
            if (int.TryParse(_apiPortField.Text.ToString(), out int port))
            {
                if (port < 1 || port > 65535)
                {
                    ShowErrorDialog("Invalid API Port", "API port must be between 1 and 65535");
                    return;
                }
                TuiSettings.ApiServerPort = port;
            }
            else
            {
                ShowErrorDialog("Invalid API Port", "API port must be a valid number");
                return;
            }

            // Success - close window
            Application.RequestStop();
        }
        catch (Exception ex)
        {
            ShowErrorDialog("Error Saving Settings", ex.Message);
        }
    }

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
}
