using System.Text.Json;
using Motely.Executors;
using Motely.Filters;

namespace Motely.TUI;

public class SearchWindow : Window
{
    private readonly string _configPath;
    private readonly string _configFormat;
    private Label _statusLabel;
    private Label _progressLabel;
    private TextView _resultsView;
    private CleanButton _stopBtn;
    private IMotelySearchContext? _search;
    private CancellationTokenSource? _cts;
    private bool _searchRunning = false;
    private int _resultCount = 0;

    public SearchWindow(string configPath, string configFormat)
    {
        _configPath = configPath;
        _configFormat = configFormat;

        Title = $"Search: {Path.GetFileNameWithoutExtension(configPath)}";
        X = Pos.Center();
        Y = Pos.Center();
        Width = Dim.Percent(85);
        Height = 24;
        CanFocus = true;
        SetScheme(BalatroTheme.Window);

        // Status label (top)
        _statusLabel = new Label()
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill() - 2,
            Text = "Starting search...",
        };
        _statusLabel.SetScheme(
            new Scheme() { Normal = new Attribute(BalatroTheme.Orange, BalatroTheme.ModalGrey) }
        );
        Add(_statusLabel);

        // Progress label (below status)
        _progressLabel = new Label()
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill() - 2,
            Text = "",
        };
        _progressLabel.SetScheme(
            new Scheme() { Normal = new Attribute(BalatroTheme.LightGrey, BalatroTheme.ModalGrey) }
        );
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
        resultsFrame.SetScheme(BalatroTheme.InnerPanel);
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
        _resultsView.SetScheme(
            new Scheme()
            {
                Normal = new Attribute(BalatroTheme.LightGrey, BalatroTheme.InnerPanelGrey),
                Focus = new Attribute(BalatroTheme.White, BalatroTheme.InnerPanelGrey),
            }
        );
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
        _stopBtn.SetScheme(BalatroTheme.RedButton);
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
        backBtn.SetScheme(BalatroTheme.BackButton);
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

            var parameters = new JsonSearchParams
            {
                Threads = TuiSettings.ThreadCount,
                BatchCharCount = TuiSettings.BatchCharacterCount,
                Quiet = true,
            };

            // Launch search via orchestrator
            Action<MotelySeedScoreTally> onResult = (tally) =>
            {
                var resultCount = Interlocked.Increment(ref _resultCount);
                var line = $"{tally.Seed}  score: {tally.Score}";
                App?.Invoke(() =>
                {
                    _resultsView.Text += line + "\n";
                    _resultsView.MoveEnd();
                });
            };

            if (
                !TryLoadConfig(_configPath, _configFormat, out var config, out var configError)
                || config == null
            )
                throw new InvalidOperationException(configError ?? "Failed to load search config.");

            parameters.Deck = config.Deck.ToString();
            parameters.Stake = config.Stake.ToString();
            parameters.ResultCallback = onResult;

            _search = MotelySearchOrchestrator.LaunchWithContext(config, parameters);

            App?.Invoke(() =>
            {
                _statusLabel.Text = "Running";
                _statusLabel.SetScheme(
                    new Scheme()
                    {
                        Normal = new Attribute(BalatroTheme.Green, BalatroTheme.ModalGrey),
                    }
                );
            });

            var searchTask = _search.Start(_cts.Token);

            // Poll progress on a timer
            MotelyTUI.App?.AddTimeout(
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
                    App?.Invoke(() => OnSearchComplete());
            });
        }
        catch (OperationCanceledException)
        {
            App?.Invoke(() => OnSearchStopped());
        }
        catch (Exception ex)
        {
            App?.Invoke(() =>
            {
                _statusLabel.Text = "Error";
                _statusLabel.SetScheme(
                    new Scheme()
                    {
                        Normal = new Attribute(BalatroTheme.Red, BalatroTheme.ModalGrey),
                    }
                );
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
        _statusLabel.SetScheme(
            new Scheme() { Normal = new Attribute(BalatroTheme.Green, BalatroTheme.ModalGrey) }
        );
        _stopBtn.Visible = false;

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
        _statusLabel.SetScheme(
            new Scheme() { Normal = new Attribute(BalatroTheme.Gray, BalatroTheme.ModalGrey) }
        );
        _stopBtn.Visible = false;
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
