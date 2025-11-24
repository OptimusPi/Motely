using Terminal.Gui;
using Motely.Executors;
using Motely.Filters;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Data;

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

        // Title
        var titleLabel = new Label()
        {
            X = Pos.Center(),
            Y = 1,
            Text = $"Running search with {configFormat.ToUpper()} filter: {configName}",
            TextAlignment = Alignment.Center
        };
        Add(titleLabel);

        // Top 10 Results label
        var topLabel = new Label()
        {
            X = 2,
            Y = 3,
            Text = "[TOP 10 RESULTS]",
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.BrightRed, ColorName.Black) // Signature Balatro red
            }
        };
        Add(topLabel);

        // Top 10 Results Table
        _topResultsTable = new TableView()
        {
            X = 2,
            Y = 4,
            Width = Dim.Fill() - 4,
            Height = 13, // Header + 10 rows + borders
            FullRowSelect = true,
            CanFocus = true,
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.White, ColorName.Black),
                Focus = new Terminal.Gui.Attribute(ColorName.Black, ColorName.BrightRed)
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
                Normal = new Terminal.Gui.Attribute(ColorName.BrightBlue, ColorName.Black) // Signature Balatro blue
            }
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
            CanFocus = true
        };
        Add(_outputView);

        // Status label
        _statusLabel = new Label()
        {
            X = 2,
            Y = Pos.AnchorEnd(3),
            Width = Dim.Fill() - 4,
            Text = "Initializing search..."
        };
        Add(_statusLabel);

        // Stop button
        _stopButton = new Button()
        {
            X = Pos.Center(),
            Y = Pos.AnchorEnd(1),
            Text = "Stop Search (Ctrl+C) | ESC for Menu"
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
                // Stop search if running, then go back to menu
                if (_searchRunning)
                {
                    StopSearch();
                }
                Application.RequestStop();
                e.Handled = true;
            }
        };

        // Start search automatically
        Task.Run(() => RunSearch());
    }

    private void RunSearch()
    {
        var originalOut = Console.Out;
        try
        {
            _searchRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();

            // Redirect console output to TextView
            var writer = new TextViewWriter(_outputView, _topResults, _topResultsTable);
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
                AutoCutoff = false
            };

            // Execute search
            _executor = new JsonSearchExecutor(_configName, parameters, _configFormat);
            var result = _executor.Execute();

            // Restore original console output ALWAYS
            Console.SetOut(originalOut);

            if (_searchRunning) // Only update UI if window still active
            {
                Application.Invoke(() =>
                {
                    _statusLabel.Text = result == 0 ? "Search completed successfully!" : $"Search exited with code {result}";
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
                    _outputView.Text += $"\n\n❌ Error: {ex.Message}\n{ex.StackTrace}";
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

    // Helper class to redirect Console.WriteLine to TextView
    private class TextViewWriter : System.IO.TextWriter
    {
        private TextView _textView;
        private TopResultsTracker _topResults;
        private TableView _tableView;

        public TextViewWriter(TextView textView, TopResultsTracker topResults, TableView tableView)
        {
            _textView = textView;
            _topResults = topResults;
            _tableView = tableView;
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
                // Window closed while writing - ignore this specific case only
            }
        }

        public override void Write(string? value)
        {
            if (value == null) return;
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
                // Window closed while writing - ignore this specific case only
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

                    // Parse CSV and update top results
                    if (!string.IsNullOrEmpty(value) && _topResults != null)
                    {
                        _topResults.ProcessLine(value, _tableView);
                    }
                });
            }
            catch (ObjectDisposedException)
            {
                // Window closed while writing - ignore this specific case only
            }
        }
    }

    // Tracks and displays top 10 search results
    private class TopResultsTracker
    {
        private List<string> _columnHeaders = new List<string>();
        private SortedSet<SeedResult> _topResults = new SortedSet<SeedResult>(new ScoreDescendingComparer());
        private bool _headersSet = false;
        private const int MAX_RESULTS = 10;

        public void ProcessLine(string line, TableView tableView)
        {
            // Skip empty or non-CSV lines
            if (string.IsNullOrWhiteSpace(line) || !line.Contains(','))
                return;

            // First line with commas is the header
            if (!_headersSet && line.StartsWith("Seed,"))
            {
                _columnHeaders = ParseCsvLine(line);
                _headersSet = true;
                Console.WriteLine($"[DEBUG] Headers detected: {string.Join(", ", _columnHeaders)}");
                UpdateTable(tableView);
                return;
            }

            // Skip non-result lines (comments, progress, etc.)
            if (!_headersSet || line.StartsWith("#") || !char.IsLetterOrDigit(line[0]))
                return;

            // Try to parse as CSV result
            try
            {
                var parts = ParseCsvLine(line);
                if (parts.Count >= 2)
                {
                    string seed = parts[0];
                    if (int.TryParse(parts[1], out int score))
                    {
                        var result = new SeedResult { Seed = seed, Score = score, Data = parts };

                        // Add to top results (SortedSet will maintain order)
                        _topResults.Add(result);
                        Console.WriteLine($"[DEBUG] Added result: {seed} with score {score} (total: {_topResults.Count})");

                        // Keep only top 10
                        while (_topResults.Count > MAX_RESULTS)
                        {
                            var min = _topResults.Min;
                            if (min != null)
                                _topResults.Remove(min);
                        }

                        UpdateTable(tableView);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log parsing errors
                Console.WriteLine($"[DEBUG] Failed to parse line: {ex.Message}");
            }
        }

        private List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;
            bool removeAnsiCodes = true; // Strip ANSI color codes

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                // Check for ANSI escape sequence
                if (removeAnsiCodes && c == '\u001b' && i + 1 < line.Length && line[i + 1] == '[')
                {
                    // Skip to 'm' which ends the ANSI code
                    int j = i + 2;
                    while (j < line.Length && line[j] != 'm')
                        j++;
                    i = j; // Skip past the ANSI code
                    continue;
                }

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            result.Add(current.ToString().Trim());
            return result;
        }

        private void UpdateTable(TableView tableView)
        {
            if (tableView == null) return;

            try
            {
                // Create DataTable
                var dt = new DataTable();

                // Add columns
                if (_headersSet && _columnHeaders.Count > 0)
                {
                    foreach (var header in _columnHeaders)
                    {
                        dt.Columns.Add(header.Replace("\"", "").Trim());
                    }
                }
                else
                {
                    // No headers yet, show waiting message
                    dt.Columns.Add("Waiting for results...");
                }

                // Add rows (top 10, sorted by score descending)
                foreach (var result in _topResults.Reverse())
                {
                    var row = dt.NewRow();
                    for (int i = 0; i < Math.Min(result.Data.Count, dt.Columns.Count); i++)
                    {
                        row[i] = result.Data[i].Replace("\"", "").Trim();
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

        private class SeedResult
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
                if (x == null || y == null) return 0;

                // First compare by score (descending)
                int scoreCompare = y.Score.CompareTo(x.Score);
                if (scoreCompare != 0) return scoreCompare;

                // If scores are equal, compare by seed name (ascending) to maintain uniqueness
                return string.Compare(x.Seed, y.Seed, StringComparison.Ordinal);
            }
        }
    }

    // Compact Balatro-styled choice dialog (3 buttons)
    private static int ShowChoiceDialog(string title, string message, string button1, string button2, string button3)
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

        int result = -1;

        var btn1 = new Button() { Text = button1 };
        btn1.Accept += (s, e) => { result = 0; Application.RequestStop(dialog); };

        var btn2 = new Button() { Text = button2 };
        btn2.Accept += (s, e) => { result = 1; Application.RequestStop(dialog); };

        var btn3 = new Button() { Text = button3 };
        btn3.Accept += (s, e) => { result = 2; Application.RequestStop(dialog); };

        dialog.AddButton(btn1);
        dialog.AddButton(btn2);
        dialog.AddButton(btn3);

        Application.Run(dialog);
        return result;
    }
}
