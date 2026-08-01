using System.Data;
using Motely;
using Motely.DataLake;
using Motely.Filters;

namespace Motely.TUI;

public class SearchWindow : Window
{
    private readonly string _configPath;
    private readonly string? _source;
    private readonly string? _sink;
    private SeedLakeSink? _lakeSink;
    private readonly Label _statusLabel;
    private readonly Label _progressLabel;
    private readonly TextField _cutoffField;
    private readonly TableView _resultsTable;
    private readonly DataTable _dataTable = new();
    private readonly CleanButton _stopBtn;
    private readonly SpinnerView _spinner;
    private IMotelySearch? _search;
    private int _highestScoreSeen = int.MinValue;
    private CancellationTokenSource? _cts;
    private bool _searchRunning = false;
    private int _resultCount = 0;
    private int _tallyColumnCount = 0;

    // --cutoff semantics (match Motely.CLI):
    //   auto     → running maximum: emit every seed at-or-above the best so far.
    //   <int>    → fixed floor: skip anything below this score.
    //   (blank)  → no cutoff.
    private bool _cutoffAuto = true;
    private int _cutoffFixed = int.MinValue;
    private int _currentHigh = int.MinValue;

    public SearchWindow(string configPath, string? source = null, string? sink = null)
    {
        _configPath = configPath;
        _source = string.IsNullOrWhiteSpace(source) ? null : source;
        _sink = string.IsNullOrWhiteSpace(sink) ? null : sink;

        Title = $"Search: {Path.GetFileNameWithoutExtension(configPath)}";
        // Tile on the left half so the editor / results browser can sit next to it.
        X = 0;
        Y = 0;
        Width = Dim.Percent(55);
        Height = Dim.Fill()! - 5;
        CanFocus = true;
        ColorScheme = BalatroTheme.Window;

        _spinner = new SpinnerView
        {
            X = 1,
            Y = 1,
            Style = new SpinnerStyle.Dots(),
            AutoSpin = true,
            SpinDelay = 120,
            Visible = true,
        };
        Add(_spinner);

        _statusLabel = new Label
        {
            X = Pos.Right(_spinner) + 1,
            Y = 1,
            Width = Dim.Fill()! - 4,
            Text = "Starting search...",
        };
        _statusLabel.ColorScheme = new ColorScheme
        {
            Normal = new Attribute(BalatroTheme.Orange, BalatroTheme.ModalGrey),
        };
        Add(_statusLabel);

        _progressLabel = new Label
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill()! - 2,
            Text = "",
        };
        _progressLabel.ColorScheme = new ColorScheme
        {
            Normal = new Attribute(BalatroTheme.LightGrey, BalatroTheme.ModalGrey),
        };
        Add(_progressLabel);

        // --cutoff row: matches Motely.CLI semantics. "auto" = running max, "<int>" = floor, "" = off.
        var cutoffLabel = new Label
        {
            X = 1,
            Y = 3,
            Text = "--cutoff:",
        };
        Add(cutoffLabel);

        _cutoffField = new TextField
        {
            X = Pos.Right(cutoffLabel) + 1,
            Y = 3,
            Width = 10,
            Text = "auto",
        };
        _cutoffField.ColorScheme = new ColorScheme
        {
            Normal = new Attribute(BalatroTheme.White, BalatroTheme.DarkGrey),
            Focus = new Attribute(BalatroTheme.White, BalatroTheme.Blue),
        };
        Add(_cutoffField);

        var cutoffApplyBtn = new CleanButton
        {
            X = Pos.Right(_cutoffField) + 1,
            Y = 3,
            Text = " Apply ",
        };
        cutoffApplyBtn.ColorScheme = BalatroTheme.GreenButton;
        cutoffApplyBtn.Accept += (_, _) => ApplyCutoffInput();
        Add(cutoffApplyBtn);

        var cutoffHint = new Label
        {
            X = Pos.Right(cutoffApplyBtn) + 2,
            Y = 3,
            Text = "(auto | <int> | blank)",
        };
        cutoffHint.ColorScheme = new ColorScheme
        {
            Normal = new Attribute(BalatroTheme.LightGrey, BalatroTheme.ModalGrey),
        };
        Add(cutoffHint);

        var resultsFrame = new FrameView
        {
            X = 1,
            Y = 5,
            Width = Dim.Fill()! - 2,
            Height = Dim.Fill()! - 9,
            Title = "Results (live)",
        };
        resultsFrame.ColorScheme = BalatroTheme.InnerPanel;
        Add(resultsFrame);

        _dataTable.Columns.Add("#", typeof(int));
        _dataTable.Columns.Add("Seed", typeof(string));
        _dataTable.Columns.Add("Score", typeof(int));

        _resultsTable = new TableView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            FullRowSelect = true,
            CanFocus = true,
        };
        _resultsTable.Style.AlwaysShowHeaders = true;
        _resultsTable.Style.ShowHorizontalHeaderUnderline = true;
        // Top-score rows get the gold treatment — row's score == best score so far = highlight.
        var goldScheme = new ColorScheme
        {
            Normal = new Attribute(BalatroTheme.Orange, BalatroTheme.DarkGrey),
            Focus = new Attribute(BalatroTheme.Orange, BalatroTheme.Blue),
            HotNormal = new Attribute(BalatroTheme.Orange, BalatroTheme.DarkGrey),
            HotFocus = new Attribute(BalatroTheme.Orange, BalatroTheme.Blue),
        };
        _resultsTable.Style.RowColorGetter = args =>
        {
            if (args.Table is not DataTableSource src)
                return null;
            if (
                src.DataTable.Columns.Count < 3
                || args.RowIndex < 0
                || args.RowIndex >= src.DataTable.Rows.Count
            )
                return null;
            var v = src.DataTable.Rows[args.RowIndex][2];
            if (v is int score && score == _highestScoreSeen && _highestScoreSeen > int.MinValue)
                return goldScheme;
            return null;
        };
        _resultsTable.ColorScheme = new ColorScheme
        {
            Normal = new Attribute(BalatroTheme.White, BalatroTheme.InnerPanelGrey),
            Focus = new Attribute(BalatroTheme.White, BalatroTheme.Blue),
            HotNormal = new Attribute(BalatroTheme.Orange, BalatroTheme.InnerPanelGrey),
            HotFocus = new Attribute(BalatroTheme.Orange, BalatroTheme.Blue),
        };
        _resultsTable.Table = new DataTableSource(_dataTable);
        resultsFrame.Add(_resultsTable);

        _stopBtn = new CleanButton
        {
            X = 1,
            Y = Pos.AnchorEnd(3),
            Text = "Stop Search",
            Width = Dim.Fill()! - 2,
            TextAlignment = Alignment.Center,
        };
        _stopBtn.ColorScheme = BalatroTheme.RedButton;
        _stopBtn.Accept += (_, _) => StopSearch();
        Add(_stopBtn);

        var backBtn = new CleanButton
        {
            X = 1,
            Y = Pos.AnchorEnd(1),
            Text = "Back",
            Width = Dim.Fill()! - 2,
            TextAlignment = Alignment.Center,
        };
        backBtn.ColorScheme = BalatroTheme.BackButton;
        backBtn.Accept += (_, _) => AttemptClose();
        Add(backBtn);

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == KeyCode.Esc)
            {
                AttemptClose();
                e.Handled = true;
            }
        };

        _ = Task.Run(RunSearch);
    }

    private void EnsureTallyColumns(int count)
    {
        if (_tallyColumnCount >= count)
            return;
        for (int i = _tallyColumnCount; i < count; i++)
            _dataTable.Columns.Add($"t{i}", typeof(int));
        _tallyColumnCount = count;
        _resultsTable.Table = new DataTableSource(_dataTable);
    }

    private void RunSearch()
    {
        try
        {
            _cts = new CancellationTokenSource();
            _searchRunning = true;

            if (
                !JamlFileSource.TryLoadFromFile(_configPath, out var config, out var configError)
                || config == null
            )
                throw new InvalidOperationException(configError ?? "Failed to load search config.");

            // Push fixed cutoff into the engine (matches Motely.CLI behaviour): the scorer
            // drops below-threshold seeds before any callback fires. Auto runs caller-side
            // via PassesCutoff() because the engine threshold is fixed per-plan.
            int engineCutoff = (!_cutoffAuto && _cutoffFixed > int.MinValue) ? _cutoffFixed : 0;
            var plan = JamlSearchBuilder.CreatePlan(config, engineCutoff);
            var settings = JamlSearchBuilder
                .CreateSettings(config, engineCutoff)
                .WithDeck(config.Deck)
                .WithStake(config.Stake)
                .WithThreadCount(TuiSettings.ThreadCount)
                .WithQuietMode(true)
                .WithAutoScoreCutoff(_cutoffAuto);

            switch (TuiSettings.SearchMode)
            {
                case SearchMode.FileSource:
                    if (!string.IsNullOrWhiteSpace(_source))
                    {
                        var provider = new SeedSourceProvider(_source);
                        if (provider.SeedCount == 0)
                        {
                            provider.Dispose();
                            throw new InvalidOperationException(
                                "Resolved source contained no seeds."
                            );
                        }
                        settings.WithProviderSearch(provider);
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            "FileSource mode requires a source file."
                        );
                    }
                    break;

                case SearchMode.Random:
                    settings.WithProviderSearch(
                        new MotelyRandomSeedProvider(TuiSettings.RandomSeedCount)
                    );
                    break;

                case SearchMode.Palindrome:
                    settings.WithProviderSearch(new MotelyPalindromeSeedProvider());
                    break;

                case SearchMode.Psychosis:
                    settings.WithProviderSearch(new MotelyPsychosisSeedProvider());
                    break;

                case SearchMode.Keyword:
                    if (string.IsNullOrWhiteSpace(TuiSettings.Keywords))
                        throw new InvalidOperationException(
                            "Keyword mode requires keywords to be set in settings."
                        );

                    var keywords = TuiSettings.Keywords.Split(
                        ',',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                    );
                    char[]? paddingChars = string.IsNullOrWhiteSpace(TuiSettings.PaddingChars)
                        ? null
                        : TuiSettings.PaddingChars.ToCharArray();

                    settings.WithProviderSearch(
                        new MotelyKeywordSeedProvider(keywords, paddingChars)
                    );
                    break;

                case SearchMode.Sequential:
                default:
                    settings
                        .WithSequentialSearch()
                        .WithBatchCharacterCount(TuiSettings.BatchCharacterCount);
                    if (
                        TuiSettings.SequentialStartSeedSearchIndex.HasValue
                        || TuiSettings.SequentialStopSeedSearchIndex.HasValue
                    )
                    {
                        long startIdx = TuiSettings.SequentialStartSeedSearchIndex ?? 0;
                        long stopIdx =
                            TuiSettings.SequentialStopSeedSearchIndex
                            ?? SeedMath.MaxSearchIndexInclusive(MotelyGlobals.MaxSeedLength);
                        var (sb, ebExclusive) = SeedMath.SearchIndexRangeToBatchRange(
                            startIdx,
                            stopIdx,
                            TuiSettings.BatchCharacterCount
                        );
                        settings.WithStartBatchIndex(sb).WithEndBatchIndex(ebExclusive);
                    }
                    break;
            }

            int scoreTallyColumns = plan.ScoreTallyColumnCount;

            Application.Invoke(() => EnsureTallyColumns(scoreTallyColumns));

            // Same contract as Motely.CLI: found seeds land in the seed lake so --drown can
            // replay them. Without this the sink setting is inert and TUI runs write nothing.
            if (_sink is not null)
                _lakeSink = new SeedLakeSink(_sink, config.Id);

            settings.WithScoredResultCallback(tally =>
            {
                _lakeSink?.OnScored(in tally);
                var resultCount = Interlocked.Increment(ref _resultCount);
                var seed = tally.Seed;
                var score = tally.Score;
                var tallyValues = tally.TallyValuesSpan.ToArray();
                Application.Invoke(() => AppendRow(resultCount, seed, score, tallyValues));
            });

            Application.Invoke(() =>
            {
                _statusLabel.Text = "Running";
                _statusLabel.ColorScheme = new ColorScheme
                {
                    Normal = new Attribute(BalatroTheme.Green, BalatroTheme.ModalGrey),
                };
            });

            var search = settings.Start(_cts.Token);
            _search = search;

            Application.AddTimeout(
                TimeSpan.FromMilliseconds(1000),
                () =>
                {
                    if (!_searchRunning)
                        return false;

                    var searched = search.TotalSeedsSearched;
                    var matches = search.MatchingSeeds;
                    var elapsedMs = search.ElapsedMs;
                    var speed = elapsedMs > 0 ? searched / (double)elapsedMs * 1000.0 : 0;
                    var elapsed = TimeSpan.FromMilliseconds(elapsedMs);

                    _progressLabel.Text =
                        $"{searched:N0} seeds | {matches} matches | {speed:N0} seeds/sec | {elapsed:hh\\:mm\\:ss}";

                    if (search.IsCompleted)
                    {
                        OnSearchComplete();
                        return false;
                    }
                    return true;
                }
            );

            OnSearchComplete();
        }
        catch (OperationCanceledException)
        {
            Application.Invoke(OnSearchStopped);
        }
        catch (Exception ex)
        {
            Application.Invoke(() =>
            {
                _statusLabel.Text = "Error";
                _statusLabel.ColorScheme = new ColorScheme
                {
                    Normal = new Attribute(BalatroTheme.Red, BalatroTheme.ModalGrey),
                };
                _progressLabel.Text = ex.Message;
                _stopBtn.Visible = false;
                _searchRunning = false;
            });
        }
    }

    /// <summary>Cutoff gate — matches Motely.CLI --cutoff semantics.</summary>
    private bool PassesCutoff(int score)
    {
        if (_cutoffAuto)
        {
            // Running maximum: emit every seed at-or-above the best so far (ties included).
            if (score < _currentHigh)
                return false;
            _currentHigh = Math.Max(_currentHigh, score);
            return true;
        }
        if (_cutoffFixed > int.MinValue && score < _cutoffFixed)
            return false;
        return true;
    }

    private void ApplyCutoffInput()
    {
        var raw = (_cutoffField.Text?.ToString() ?? "").Trim();
        if (string.Equals(raw, "auto", StringComparison.OrdinalIgnoreCase))
        {
            _cutoffAuto = true;
            _cutoffFixed = int.MinValue;
            _currentHigh = int.MinValue;
            _statusLabel.Text = "Cutoff: auto (running max).";
        }
        else if (string.IsNullOrEmpty(raw))
        {
            _cutoffAuto = false;
            _cutoffFixed = int.MinValue;
            _statusLabel.Text = "Cutoff: off.";
        }
        else if (int.TryParse(raw, out var n))
        {
            _cutoffAuto = false;
            _cutoffFixed = n;
            _statusLabel.Text = $"Cutoff: ≥ {n}.";
        }
        else
        {
            _statusLabel.Text = $"Invalid cutoff '{raw}' — use 'auto', an integer, or leave blank.";
        }
    }

    private void AppendRow(int idx, string seed, int score, int[] tallies)
    {
        EnsureTallyColumns(tallies.Length);
        var row = _dataTable.NewRow();
        row[0] = idx;
        row[1] = seed;
        row[2] = score;
        for (int i = 0; i < tallies.Length && i < _tallyColumnCount; i++)
            row[3 + i] = tallies[i];
        _dataTable.Rows.Add(row);

        if (score > _highestScoreSeen)
            _highestScoreSeen = score;

        // Scroll to show the newest row.
        _resultsTable.SelectedRow = _dataTable.Rows.Count - 1;
        _resultsTable.EnsureSelectedCellIsVisible();
        _resultsTable.SetNeedsDraw();
    }

    private void OnSearchComplete()
    {
        _searchRunning = false;
        _statusLabel.Text = "Completed";
        _statusLabel.ColorScheme = new ColorScheme
        {
            Normal = new Attribute(BalatroTheme.Green, BalatroTheme.ModalGrey),
        };
        _spinner.AutoSpin = false;
        _spinner.Visible = false;
        _stopBtn.Visible = false;

        if (_search != null)
        {
            try
            {
                _search.Dispose();
            }
            catch { }

            var searched = _search.TotalSeedsSearched;
            var matches = _search.MatchingSeeds;
            var elapsed = TimeSpan.FromMilliseconds(_search.ElapsedMs);
            _progressLabel.Text =
                $"Done: {searched:N0} seeds | {matches} matches | {elapsed:hh\\:mm\\:ss}";
        }
    }

    private void OnSearchStopped()
    {
        _searchRunning = false;
        _statusLabel.Text = "Stopped";
        _statusLabel.ColorScheme = new ColorScheme
        {
            Normal = new Attribute(BalatroTheme.Gray, BalatroTheme.ModalGrey),
        };
        _spinner.AutoSpin = false;
        _spinner.Visible = false;
        _stopBtn.Visible = false;
    }

    private void StopSearch()
    {
        if (!_searchRunning)
            return;

        _stopBtn.Enabled = false;
        _stopBtn.Text = "Stopping...";

        _cts?.Cancel();

        OnSearchStopped();
    }

    private void AttemptClose()
    {
        if (_searchRunning)
            StopSearch();

        _search?.Dispose();

        MotelyTUI.CloseWindow(this);
    }

    protected override void Dispose(bool disposing)
    {
        _search?.Dispose();
        _cts?.Dispose();
        _lakeSink?.Dispose();
        _lakeSink = null;

        base.Dispose(disposing);
    }

}
