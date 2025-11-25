using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Motely.Executors;
using Motely.Filters;
using Terminal.Gui;

namespace Motely.TUI;

public class SearchWindow : Window
{
    private TextView _outputView;
    private TableView _topResultsTable;
    private Button _stopButton;
    private Label _statusLabel;
    private string _configName;
    private string _configFormat;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _searchRunning = false;
    private JsonSearchExecutor? _executor;
    private TopResultsTracker _topResults = new TopResultsTracker();
    private MotelyJsonConfig? _config;

    public SearchWindow(string configName, string configFormat)
    {
        Title = $"Search: {configName}";
        _configName = configName;
        _configFormat = configFormat;

        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();
        CanFocus = true;

        // Title centered at top
        var titleLabel = new Label()
        {
            X = Pos.Center(),
            Y = 0,
            Text = $"Running search with {configFormat.ToUpper()} filter: {configName}",
            TextAlignment = Alignment.Center,
        };
        Add(titleLabel);

        // Top 10 Results label
        var topLabel = new Label()
        {
            X = 2,
            Y = 2,
            Text = "[TOP 10 RESULTS]",
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.BrightRed, ColorName.Black), // Signature Balatro red
            },
        };
        Add(topLabel);

        // Top 10 Results Table
        _topResultsTable = new TableView()
        {
            X = 2,
            Y = 3,
            Width = Dim.Fill() - 4,
            Height = 13, // Header + 10 rows + borders
            FullRowSelect = true,
            CanFocus = true,
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.White, ColorName.Black),
                Focus = new Terminal.Gui.Attribute(ColorName.Black, ColorName.BrightRed),
            },
        };

        // Handle mouse clicks on table rows to copy Blueprint URL
        _topResultsTable.MouseClick += (s, e) =>
        {
            if (_topResultsTable.Table?.Rows > 0 && _topResultsTable.SelectedRow >= 0)
            {
                CopyBlueprintUrl(_topResultsTable.SelectedRow);
                e.Handled = true;
            }
        };

        // Initialize with empty table
        var emptyTable = new System.Data.DataTable();
        emptyTable.Columns.Add("Waiting for results...");
        _topResultsTable.Table = new DataTableSource(emptyTable);

        Add(_topResultsTable);

        // Log label
        var logLabel = new Label()
        {
            X = 2,
            Y = Pos.Bottom(_topResultsTable) + 1,
            Text = "[SEARCH LOG]",
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.BrightBlue, ColorName.Black), // Signature Balatro blue
            },
        };
        Add(logLabel);

        // Output view (scrollable text) - now below the table
        _outputView = new TextView()
        {
            X = 2,
            Y = Pos.Bottom(logLabel) + 1,
            Width = Dim.Fill() - 4,
            Height = Dim.Fill() - 6,
            ReadOnly = true,
            WordWrap = false,
            CanFocus = true,
        };
        Add(_outputView);

        // Status label
        _statusLabel = new Label()
        {
            X = 2,
            Y = Pos.AnchorEnd(3),
            Width = Dim.Fill() - 4,
            Text = "Initializing search...",
        };
        Add(_statusLabel);

        // Stop button
        _stopButton = new Button()
        {
            X = Pos.Center(),
            Y = Pos.AnchorEnd(1),
            Text = "Stop Search (Ctrl+C) | ESC for Menu",
        };
        _stopButton.Accept += (s, e) => StopSearch();
        Add(_stopButton);

        // Keyboard shortcuts
        KeyDown += (sender, e) =>
        {
            if (e.KeyCode == (KeyCode.C | KeyCode.CtrlMask))
            {
                StopSearch();
                e.Handled = true;
            }
            else if (e.KeyCode == KeyCode.Esc)
            {
                // Stop search if running
                if (_searchRunning)
                {
                    StopSearch();
                }

                // Show menu with options
                var choice = ShowFourChoiceDialog(
                    "ESC Menu",
                    "What would you like to do?",
                    "Save Top 10",
                    "Main Menu",
                    "Exit",
                    "Cancel"
                );

                if (choice == 0) // Save Top 10
                {
                    SaveTopResults();
                }
                else if (choice == 1) // Main Menu
                {
                    // Return to main menu
                    Application.RequestStop();
                }
                else if (choice == 2) // Exit
                {
                    // Exit the entire application
                    Application.RequestStop();
                    // Set a flag to indicate full exit
                    Environment.Exit(0);
                }
                // choice == 3 (Cancel) - do nothing, stay in search window

                e.Handled = true;
            }
        };

        // Start search automatically on background thread
        Task.Run(async () => await RunSearchAsync());
    }

    private async Task RunSearchAsync()
    {
        var originalOut = Console.Out;
        try
        {
            _searchRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();

            // Load config for Blueprint URL generation
            var configContent = File.ReadAllText(_configName);
            _config = _configFormat.ToLower() switch
            {
                "json" => ConfigFormatConverter.LoadFromJsonString(configContent),
                "yaml" => ConfigFormatConverter.LoadFromYamlString(configContent),
                _ => ConfigFormatConverter.LoadFromJsonString(configContent),
            };

            // Create callback to receive typed results directly (NO CSV PARSING!)
            Action<MotelySeedScoreTally> resultCallback = (tally) =>
            {
                Application.Invoke(() =>
                {
                    _topResults.AddResult(tally, _topResultsTable);
                });
            };

            // Redirect console output to TextView (for logging only, not parsing!)
            var writer = new TextViewWriter(_outputView);
            Console.SetOut(writer);

            Application.Invoke(() =>
            {
                _statusLabel.Text = "Search running...";
            });

            // Build search parameters
            var parameters = new JsonSearchParams
            {
                Threads = TuiSettings.ThreadCount,
                BatchSize = TuiSettings.BatchCharacterCount,
                StartBatch = 0,
                EndBatch = 0,
                EnableDebug = false,
                NoFancy = true, // Disable fancy output in TUI
                Quiet = false,
                SpecificSeed = null,
                Wordlist = null,
                RandomSeeds = null,
                Cutoff = 0,
                AutoCutoff = false,
            };

            // Execute search with callback to receive typed results directly
            _executor = new JsonSearchExecutor(_configName, parameters, _configFormat, resultCallback);
            var result = await Task.Run(() => _executor.Execute(), _cancellationTokenSource.Token);

            // Restore original console output ALWAYS
            Console.SetOut(originalOut);

            if (_searchRunning) // Only update UI if window still active
            {
                Application.Invoke(() =>
                {
                    _statusLabel.Text =
                        result == 0
                            ? "Search completed successfully!"
                            : $"Search exited with code {result}";
                    _stopButton.Text = "Close";
                    _searchRunning = false;
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Search was cancelled - restore console output
            Console.SetOut(originalOut);

            if (_searchRunning)
            {
                Application.Invoke(() =>
                {
                    _statusLabel.Text = "Search cancelled";
                    _stopButton.Text = "Close";
                    _searchRunning = false;
                });
            }
        }
        catch (Exception ex)
        {
            // Restore console output on error too
            Console.SetOut(originalOut);

            if (_searchRunning) // Only update UI if window still active
            {
                Application.Invoke(() =>
                {
                    _outputView.Text += $"\n\nError: {ex.Message}\n{ex.StackTrace}";
                    _statusLabel.Text = "Search failed!";
                    _stopButton.Text = "Close";
                    _searchRunning = false;
                });
            }
        }
        finally
        {
            // ALWAYS restore console output
            try
            {
                Console.SetOut(originalOut);
            }
            catch { }
            _searchRunning = false;
        }
    }

    private void StopSearch()
    {
        if (_searchRunning)
        {
            _cancellationTokenSource?.Cancel();
            _executor?.Cancel();
            _searchRunning = false;
            _statusLabel.Text = "Search stopped by user";
            _stopButton.Text = "Close";
            _stopButton.Enabled = true;

            try
            {
                Console.SetOut(Console.Out);
            }
            catch { }
        }
        else
        {
            Application.RequestStop();
        }
    }

    private void SaveTopResults()
    {
        if (_topResults.Count == 0)
        {
            var noResultsDialog = new Dialog()
            {
                Title = "No Results",
                Width = 40,
                Height = 9,
                ColorScheme = new ColorScheme()
                {
                    Normal = new Terminal.Gui.Attribute(ColorName.BrightRed, ColorName.Black),
                    Focus = new Terminal.Gui.Attribute(ColorName.Black, ColorName.BrightRed),
                },
            };
            noResultsDialog.Add(new Label()
            {
                X = Pos.Center(),
                Y = 2,
                Text = "No results to save yet!",
                TextAlignment = Alignment.Center,
            });
            var okBtn = new Button() { Text = "OK" };
            okBtn.Accept += (s, e) => Application.RequestStop(noResultsDialog);
            noResultsDialog.AddButton(okBtn);
            Application.Run(noResultsDialog);
            return;
        }

        // Ask for filename
        var dialog = new Dialog()
        {
            Title = "Save Top 10 Results",
            X = Pos.Center(),
            Y = Pos.Center(),
            Width = 60,
            Height = 10,
        };

        var label = new Label()
        {
            X = 1,
            Y = 1,
            Text = "Filename (without extension):",
        };
        dialog.Add(label);

        var textField = new TextField()
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill() - 2,
            Text = $"top10_{_configName}_{DateTime.Now:yyyyMMdd_HHmmss}",
        };
        dialog.Add(textField);

        var formatLabel = new Label()
        {
            X = 1,
            Y = 3,
            Text = "Format: CSV",
        };
        dialog.Add(formatLabel);

        var okButton = new Button() { Text = "Save" };
        var cancelButton = new Button() { Text = "Cancel" };

        okButton.Accept += (s, e) =>
        {
            var filename = textField.Text.ToString();
            if (string.IsNullOrWhiteSpace(filename))
            {
                var errorDialog = new Dialog()
                {
                    Title = "Error",
                    Width = 45,
                    Height = 9,
                    ColorScheme = new ColorScheme()
                    {
                        Normal = new Terminal.Gui.Attribute(ColorName.BrightRed, ColorName.Black),
                        Focus = new Terminal.Gui.Attribute(ColorName.Black, ColorName.BrightRed),
                    },
                };
                errorDialog.Add(new Label()
                {
                    X = Pos.Center(),
                    Y = 2,
                    Text = "Filename cannot be empty!",
                    TextAlignment = Alignment.Center,
                });
                var okBtn = new Button() { Text = "OK" };
                okBtn.Accept += (s, e) => Application.RequestStop(errorDialog);
                errorDialog.AddButton(okBtn);
                Application.Run(errorDialog);
                return;
            }

            try
            {
                var filepath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), filename + ".csv");
                using (var writer = new System.IO.StreamWriter(filepath))
                {
                    // Write header
                    writer.WriteLine(string.Join(",", _topResults._columnHeaders));

                    // Write top 10 results (already sorted descending)
                    foreach (var result in _topResults._topResults)
                    {
                        writer.WriteLine(string.Join(",", result.Data));
                    }
                }

                var successDialog = new Dialog()
                {
                    Title = "Success",
                    Width = Math.Min(70, filepath.Length + 20),
                    Height = 9,
                    ColorScheme = new ColorScheme()
                    {
                        Normal = new Terminal.Gui.Attribute(ColorName.BrightBlue, ColorName.Black),
                        Focus = new Terminal.Gui.Attribute(ColorName.Black, ColorName.BrightBlue),
                    },
                };
                successDialog.Add(new Label()
                {
                    X = Pos.Center(),
                    Y = 2,
                    Text = $"Saved to:\n{filepath}",
                    TextAlignment = Alignment.Center,
                });
                var okBtn = new Button() { Text = "OK" };
                okBtn.Accept += (s, e) => Application.RequestStop(successDialog);
                successDialog.AddButton(okBtn);
                Application.Run(successDialog);
                Application.RequestStop(dialog);
            }
            catch (Exception ex)
            {
                var errorDialog = new Dialog()
                {
                    Title = "Error",
                    Width = Math.Min(60, ex.Message.Length + 25),
                    Height = 10,
                    ColorScheme = new ColorScheme()
                    {
                        Normal = new Terminal.Gui.Attribute(ColorName.BrightRed, ColorName.Black),
                        Focus = new Terminal.Gui.Attribute(ColorName.Black, ColorName.BrightRed),
                    },
                };
                errorDialog.Add(new Label()
                {
                    X = Pos.Center(),
                    Y = 2,
                    Text = $"Failed to save:\n{ex.Message}",
                    TextAlignment = Alignment.Center,
                });
                var okBtn = new Button() { Text = "OK" };
                okBtn.Accept += (s, e) => Application.RequestStop(errorDialog);
                errorDialog.AddButton(okBtn);
                Application.Run(errorDialog);
            }
        };

        cancelButton.Accept += (s, e) => Application.RequestStop(dialog);

        dialog.AddButton(okButton);
        dialog.AddButton(cancelButton);

        Application.Run(dialog);
    }

    private void CopyBlueprintUrl(int rowIndex)
    {
        try
        {
            // Get the seed from the selected row
            var table = _topResultsTable.Table;
            if (table == null || rowIndex >= table.Rows || rowIndex < 0)
                return;

            var seed = table[rowIndex, 0]?.ToString();
            if (string.IsNullOrWhiteSpace(seed))
                return;

            // Build Blueprint URL
            var deckName = _config?.Deck ?? "Red";
            var stakeName = _config?.Stake ?? "White";
            var maxAnte = _config?.MaxBossAnte ?? 8;

            var url = $"https://miaklwalker.github.io/Blueprint/?seed={seed}&deck={deckName}+Deck&antes={maxAnte}&stake={stakeName}+Stake";

            // Copy to clipboard using platform-specific command
            CopyToClipboard(url);

            MessageBox.Query("Blueprint URL Copied!", $"Seed: {seed}\n\nURL copied to clipboard:\n{url}", "OK");
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", $"Failed to copy Blueprint URL: {ex.Message}", "OK");
        }
    }

    private void CopyToClipboard(string text)
    {
        try
        {
            // Cross-platform clipboard copy
            if (OperatingSystem.IsWindows())
            {
                // Windows: use clip (pipe text to stdin to avoid special char issues)
                var psi = new System.Diagnostics.ProcessStartInfo("clip")
                {
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                var proc = System.Diagnostics.Process.Start(psi);
                proc?.StandardInput.Write(text);
                proc?.StandardInput.Close();
                proc?.WaitForExit();
            }
            else if (OperatingSystem.IsMacOS())
            {
                // macOS: use pbcopy
                var psi = new System.Diagnostics.ProcessStartInfo("pbcopy")
                {
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                var proc = System.Diagnostics.Process.Start(psi);
                proc?.StandardInput.Write(text);
                proc?.StandardInput.Close();
                proc?.WaitForExit();
            }
            else
            {
                // Linux: use xclip or xsel
                var psi = new System.Diagnostics.ProcessStartInfo("xclip", "-selection clipboard")
                {
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                var proc = System.Diagnostics.Process.Start(psi);
                proc?.StandardInput.Write(text);
                proc?.StandardInput.Close();
                proc?.WaitForExit();
            }
        }
        catch
        {
            // Fallback: just show the URL
            throw new Exception("Clipboard not available. Please copy manually.");
        }
    }

    // Helper class to redirect Console.WriteLine to TextView (logging only, NO CSV PARSING!)
    private class TextViewWriter : System.IO.TextWriter
    {
        private TextView _textView;

        public TextViewWriter(TextView textView)
        {
            _textView = textView;
        }

        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;

        public override void Write(char value)
        {
            try
            {
                Application.Invoke(() =>
                {
                    if (_textView != null)
                    {
                        _textView.Text += value.ToString();
                        _textView.MoveEnd();
                    }
                });
            }
            catch (ObjectDisposedException)
            {
                // Window closed while writing - ignore
            }
        }

        public override void Write(string? value)
        {
            if (value == null)
                return;
            try
            {
                Application.Invoke(() =>
                {
                    if (_textView != null)
                    {
                        _textView.Text += value;
                        _textView.MoveEnd();
                    }
                });
            }
            catch (ObjectDisposedException)
            {
                // Window closed while writing - ignore
            }
        }

        public override void WriteLine(string? value)
        {
            try
            {
                Application.Invoke(() =>
                {
                    if (_textView != null)
                    {
                        _textView.Text += (value ?? string.Empty) + "\n";
                        _textView.MoveEnd();
                    }
                });
            }
            catch (ObjectDisposedException)
            {
                // Window closed while writing - ignore
            }
        }
    }

    // Tracks and displays top 10 search results using TYPED data (no CSV parsing!)
    private class TopResultsTracker
    {
        internal List<string> _columnHeaders = new List<string>();
        internal SortedSet<SeedResult> _topResults = new SortedSet<SeedResult>(
            new ScoreDescendingComparer()
        );
        private const int MAX_RESULTS = 10;

        public int Count => _topResults.Count;

        public void AddResult(MotelySeedScoreTally tally, TableView tableView)
        {
            // Build column headers on first result (Seed, Score, + tally columns)
            if (_columnHeaders.Count == 0)
            {
                _columnHeaders.Add("Seed");
                _columnHeaders.Add("Score");
                // Add tally column headers if any
                for (int i = 0; i < tally.TallyCount; i++)
                {
                    _columnHeaders.Add($"Col{i + 1}");
                }
            }

            // Build data list: [seed, score, tally1, tally2, ...]
            var data = new List<string> { tally.Seed, tally.Score.ToString() };
            data.AddRange(tally.TallyColumns.Select(v => v.ToString()));

            var result = new SeedResult
            {
                Seed = tally.Seed,
                Score = tally.Score,
                Data = data,
            };

            // Add to top results (SortedSet maintains descending order)
            _topResults.Add(result);

            // Keep only top 10
            while (_topResults.Count > MAX_RESULTS)
            {
                var min = _topResults.Min;
                if (min != null)
                    _topResults.Remove(min);
            }

            UpdateTable(tableView);
        }

        private void UpdateTable(TableView tableView)
        {
            if (tableView == null)
                return;

            try
            {
                // Create DataTable
                var dt = new DataTable();

                // Add columns
                if (_columnHeaders.Count > 0)
                {
                    foreach (var header in _columnHeaders)
                    {
                        dt.Columns.Add(header);
                    }
                }
                else
                {
                    // No headers yet, show waiting message
                    dt.Columns.Add("Waiting for results...");
                }

                // Add rows (top 10, already sorted by score descending via ScoreDescendingComparer)
                foreach (var result in _topResults)
                {
                    var row = dt.NewRow();
                    for (int i = 0; i < Math.Min(result.Data.Count, dt.Columns.Count); i++)
                    {
                        row[i] = result.Data[i];
                    }
                    dt.Rows.Add(row);
                }

                // Update table
                tableView.Table = new DataTableSource(dt);
            }
            catch (Exception ex)
            {
                // Log table update errors to console
                Console.WriteLine($"[DEBUG] Table update error: {ex.Message}");
            }
        }

        internal class SeedResult
        {
            public string Seed { get; set; } = "";
            public int Score { get; set; }
            public List<string> Data { get; set; } = new List<string>();

            public override bool Equals(object? obj)
            {
                return obj is SeedResult other && Seed == other.Seed;
            }

            public override int GetHashCode()
            {
                return Seed.GetHashCode();
            }
        }

        private class ScoreDescendingComparer : IComparer<SeedResult>
        {
            public int Compare(SeedResult? x, SeedResult? y)
            {
                if (x == null || y == null)
                    return 0;

                // First compare by score (descending - highest first)
                int scoreCompare = x.Score.CompareTo(y.Score);
                if (scoreCompare != 0)
                    return -scoreCompare; // Negate to get descending order

                // If scores are equal, compare by seed name (ascending) to maintain uniqueness
                return string.Compare(x.Seed, y.Seed, StringComparison.Ordinal);
            }
        }
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

    // Compact Balatro-styled choice dialog (4 buttons)
    private static int ShowFourChoiceDialog(
        string title,
        string message,
        string button1,
        string button2,
        string button3,
        string button4
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

        var btn4 = new Button() { Text = button4 };
        btn4.Accept += (s, e) =>
        {
            result = 3;
            Application.RequestStop(dialog);
        };

        dialog.AddButton(btn1);
        dialog.AddButton(btn2);
        dialog.AddButton(btn3);
        dialog.AddButton(btn4);

        Application.Run(dialog);
        return result;
    }
}
