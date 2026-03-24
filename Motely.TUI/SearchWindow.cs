using System.Text.Json;
using Motely.DB.SeedSource;
using Motely.Filters;

namespace Motely.TUI;

public class SearchWindow : Window
{
    private readonly string _configPath;
    private readonly string _configFormat;
    private readonly string? _source;
    private readonly string? _sink;
    private Label _statusLabel;
    private Label _progressLabel;
    private TextView _resultsView;
    private CleanButton _stopBtn;
    private IMotelySearch? _search;
    private ISeedResultSink? _activeSink;
    private CancellationTokenSource? _cts;
    private bool _searchRunning = false;
    private int _resultCount = 0;

    public SearchWindow(string configPath, string configFormat, string? source = null, string? sink = null)
    {
        _configPath = configPath;
        _configFormat = configFormat;
        _source = string.IsNullOrWhiteSpace(source) ? null : source;
        _sink = string.IsNullOrWhiteSpace(sink) ? null : sink;

        Title = $"Search: {Path.GetFileNameWithoutExtension(configPath)}";
        X = Pos.Center();
        Y = Pos.Center();
        Width = Dim.Percent(85);
        Height = 24;
        CanFocus = true;
        ColorScheme = BalatroTheme.Window;

        // Status label (top)
        _statusLabel = new Label()
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill() - 2,
            Text = "Starting search...",
        };
        _statusLabel.ColorScheme =
            new ColorScheme() { Normal = new Attribute(BalatroTheme.Orange, BalatroTheme.ModalGrey) };
        Add(_statusLabel);

        // Progress label (below status)
        _progressLabel = new Label()
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill() - 2,
            Text = "",
        };
        _progressLabel.ColorScheme =
            new ColorScheme() { Normal = new Attribute(BalatroTheme.LightGrey, BalatroTheme.ModalGrey) };
        Add(_progressLabel);

        // Results frame
        var resultsFrame = new FrameView()
        {
            X = 1,
            Y = 4,
            Width = Dim.Fill() - 2,
            Height = Dim.Fill() - 8,
            Title = "Results",
        };
        resultsFrame.ColorScheme = BalatroTheme.InnerPanel;
        Add(resultsFrame);

        _resultsView = new TextView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            WordWrap = false,
            CanFocus = true,
        };
        _resultsView.ColorScheme =
            new ColorScheme()
            {
                Normal = new Attribute(BalatroTheme.LightGrey, BalatroTheme.InnerPanelGrey),
                Focus = new Attribute(BalatroTheme.White, BalatroTheme.InnerPanelGrey),
            };
        resultsFrame.Add(_resultsView);

        // Stop button
        _stopBtn = new CleanButton()
        {
            X = 1,
            Y = Pos.AnchorEnd(3),
            Text = "Stop Search",
            Width = Dim.Fill() - 2,
            TextAlignment = Alignment.Center,
        };
        _stopBtn.ColorScheme = BalatroTheme.RedButton;
        _stopBtn.Accept += (s, e) => StopSearch();
        Add(_stopBtn);

        // Back button
        var backBtn = new CleanButton()
        {
            X = 1,
            Y = Pos.AnchorEnd(1),
            Text = "Back",
            Width = Dim.Fill() - 2,
            TextAlignment = Alignment.Center,
        };
        backBtn.ColorScheme = BalatroTheme.BackButton;
        backBtn.Accept += (s, e) => AttemptClose();
        Add(backBtn);

        // ESC key
        KeyDown += (sender, e) =>
        {
            if (e.KeyCode == KeyCode.Esc)
            {
                AttemptClose();
                e.Handled = true;
            }
        };

        // Start search after window is ready
        _ = Task.Run(() => RunSearch());
    }

    private void RunSearch()
    {
        try
        {
            _cts = new CancellationTokenSource();
            _searchRunning = true;

            if (
                !TryLoadConfig(_configPath, _configFormat, out var config, out var configError)
                || config == null
            )
                throw new InvalidOperationException(configError ?? "Failed to load search config.");

            var plan = JamlSearchBuilder.CreatePlan(config);
            var settings = plan
                .Settings.WithDeck(config.Deck)
                .WithStake(config.Stake)
                .WithThreadCount(TuiSettings.ThreadCount)
                .WithBatchCharacterCount(TuiSettings.BatchCharacterCount)
                .WithQuietMode(true);

            if (!string.IsNullOrWhiteSpace(_source))
            {
                var sourceSeeds = SeedReader.ReadSeeds(_source);
                if (sourceSeeds.Count == 0)
                    throw new InvalidOperationException("Resolved source contained no seeds.");

                settings.WithListSearch(sourceSeeds, sourceSeeds.Count);
            }
            else
            {
                settings.WithSequentialSearch();
            }

            bool hasStructuredScores = plan.ShouldClauseCount > 0;
            _activeSink = !string.IsNullOrWhiteSpace(_sink)
                ? SeedResultSinkFactory.Create(_sink, plan.ShouldClauseCount)
                : null;

            if (hasStructuredScores)
            {
                settings.WithScoredResultCallback(tally =>
                {
                    _activeSink?.AppendScoredResult(tally.Seed, tally.Score, tally.TallyValuesSpan);

                    var resultCount = Interlocked.Increment(ref _resultCount);
                    var line = $"{resultCount,6} | {tally.Seed,-10} | {tally.Score,6}";
                    Application.Invoke(() =>
                    {
                        _resultsView.Text += line + "\n";
                        _resultsView.MoveEnd();
                    });
                });
            }

            if (!hasStructuredScores)
            {
                settings.WithSeedMatchCallback(line =>
                {
                    _activeSink?.AppendSeed(line);

                    var resultCount = Interlocked.Increment(ref _resultCount);
                    var displayLine = $"{resultCount,6} | {line}";

                    Application.Invoke(() =>
                    {
                        _resultsView.Text += displayLine + "\n";
                        _resultsView.MoveEnd();
                    });
                });
            }

            _search = settings.CreateSearch();

            Application.Invoke(() =>
            {
                _statusLabel.Text = "Running";
                _statusLabel.ColorScheme =
                    new ColorScheme()
                    {
                        Normal = new Attribute(BalatroTheme.Green, BalatroTheme.ModalGrey),
                    };
            });

            var searchTask = Task.Run(() => _search.Start(_cts.Token), _cts.Token);

            // Poll progress on a timer
            Application.AddTimeout(
                TimeSpan.FromMilliseconds(500),
                () =>
                {
                    if (!_searchRunning || _search == null)
                        return false;

                    var searched = _search.TotalSeedsSearched;
                    var matches = _search.MatchingSeeds;
                    var elapsed = _search.ElapsedTime;
                    var speed =
                        elapsed.TotalMilliseconds > 0
                            ? searched / elapsed.TotalMilliseconds * 1000.0
                            : 0;

                    _progressLabel.Text =
                        $"{searched:N0} seeds | {matches} matches | {speed:N0} seeds/sec | {elapsed:hh\\:mm\\:ss}";

                    if (_search.IsCompleted)
                    {
                        OnSearchComplete();
                        return false;
                    }

                    return true;
                }
            );

            // Wait for completion
            searchTask.ContinueWith(t =>
            {
                if (!t.IsCanceled)
                    Application.Invoke(() => OnSearchComplete());
            });
        }
        catch (OperationCanceledException)
        {
            Application.Invoke(() => OnSearchStopped());
        }
        catch (Exception ex)
        {
            Application.Invoke(() =>
            {
                _statusLabel.Text = "Error";
                _statusLabel.ColorScheme =
                    new ColorScheme()
                    {
                        Normal = new Attribute(BalatroTheme.Red, BalatroTheme.ModalGrey),
                    };
                _progressLabel.Text = ex.Message;
                _stopBtn.Visible = false;
                _searchRunning = false;
            });
        }
    }

    private void OnSearchComplete()
    {
        _searchRunning = false;
        _statusLabel.Text = "Completed";
        _statusLabel.ColorScheme =
            new ColorScheme() { Normal = new Attribute(BalatroTheme.Green, BalatroTheme.ModalGrey) };
        _stopBtn.Visible = false;
        _activeSink?.Dispose();
        _activeSink = null;

        // Final progress update
        if (_search != null)
        {
            var searched = _search.TotalSeedsSearched;
            var matches = _search.MatchingSeeds;
            var elapsed = _search.ElapsedTime;
            _progressLabel.Text =
                $"Done: {searched:N0} seeds | {matches} matches | {elapsed:hh\\:mm\\:ss}";
        }
    }

    private void OnSearchStopped()
    {
        _searchRunning = false;
        _statusLabel.Text = "Stopped";
        _statusLabel.ColorScheme =
            new ColorScheme() { Normal = new Attribute(BalatroTheme.Gray, BalatroTheme.ModalGrey) };
        _stopBtn.Visible = false;
        _activeSink?.Dispose();
        _activeSink = null;
    }

    private void StopSearch()
    {
        if (!_searchRunning)
            return;

        _stopBtn.Enabled = false;
        _stopBtn.Text = "Stopping...";

        // Cancellation goes through CTS — _search observes the token
        try
        {
            _cts?.Cancel();
        }
        catch { }

        OnSearchStopped();
    }

    private void AttemptClose()
    {
        if (_searchRunning)
            StopSearch();

        try
        {
            _search?.Dispose();
            _activeSink?.Dispose();
            _activeSink = null;
        }
        catch { }

        MotelyTUI.CloseWindow(this);
    }

    private static bool TryLoadConfig(
        string path,
        string configFormat,
        out JamlConfig? config,
        out string? error
    )
    {
        config = null;
        error = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Config path is required.";
            return false;
        }

        if (!File.Exists(path))
        {
            error = $"Config file not found: {path}";
            return false;
        }

        var content = File.ReadAllText(path);
        var format = configFormat.ToLowerInvariant();

        return JamlConfigLoader.TryLoad(content, out config, out error);
    }
}
