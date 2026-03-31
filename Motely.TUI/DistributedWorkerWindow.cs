using System.Diagnostics;

namespace Motely.TUI;

/// <summary>
/// TUI window for launching and monitoring a Motely Distributed Worker process.
/// Connects to the seedfinder.app pool and claims search blocks to process.
/// </summary>
public class DistributedWorkerWindow : Window
{
    private TextView _logView = null!;
    private Label _statusLabel = null!;
    private CleanButton _startButton = null!;
    private CleanButton _stopButton = null!;
    private TextField _poolUrlField = null!;
    private TextField _threadsField = null!;
    private Process? _workerProcess;
    private bool _isRunning = false;
    private readonly CancellationTokenSource _cts = new();

    public DistributedWorkerWindow()
    {
        Title = "Distributed Worker";
        X = Pos.Center();
        Y = Pos.Center();
        Width = Dim.Percent(85);
        Height = 28;
        CanFocus = true;
        ColorScheme = BalatroTheme.Window;

        // Status row
        _statusLabel = new Label()
        {
            X = 1,
            Y = 1,
            Text = "Stopped",
        };
        _statusLabel.ColorScheme = new ColorScheme() { Normal = new Attribute(BalatroTheme.Orange, BalatroTheme.ModalGrey) };
        Add(_statusLabel);

        // Pool URL label
        var poolLabel = new Label() { X = 1, Y = 3, Text = "Pool URL:" };
        poolLabel.ColorScheme = BalatroTheme.Hint;
        Add(poolLabel);

        _poolUrlField = new TextField()
        {
            X = 1,
            Y = 4,
            Width = Dim.Fill() - 2,
            Text = TuiSettings.WorkerPoolUrl,
        };
        Add(_poolUrlField);

        // Thread count
        var threadsLabel = new Label() { X = 1, Y = 6, Text = "Threads:" };
        threadsLabel.ColorScheme = BalatroTheme.Hint;
        Add(threadsLabel);

        _threadsField = new TextField()
        {
            X = 1,
            Y = 7,
            Width = 10,
            Text = TuiSettings.WorkerThreads.ToString(),
        };
        Add(_threadsField);

        var threadsHint = new Label()
        {
            X = 13,
            Y = 7,
            Text = $"(1-{Environment.ProcessorCount} cores available)",
        };
        threadsHint.ColorScheme = BalatroTheme.Hint;
        Add(threadsHint);

        // Log frame
        var logFrame = new FrameView()
        {
            X = 1,
            Y = 9,
            Width = Dim.Fill() - 2,
            Height = Dim.Fill() - 5,
            Title = "Worker Log",
        };
        logFrame.ColorScheme = BalatroTheme.InnerPanel;
        Add(logFrame);

        var copyLogsButton = new CleanButton()
        {
            X = Pos.AnchorEnd(12),
            Y = 0,
            Text = "Copy Logs",
        };
        copyLogsButton.ColorScheme = BalatroTheme.BackButton;
        copyLogsButton.Accept += (s, e) =>
        {
            CopyToClipboard(_logView.Text?.ToString() ?? "");
            copyLogsButton.Text = "COPIED!";
            copyLogsButton.ColorScheme = BalatroTheme.GreenButton;
            Application.AddTimeout(TimeSpan.FromSeconds(1.5), () =>
            {
                copyLogsButton.Text = "Copy Logs";
                copyLogsButton.ColorScheme = BalatroTheme.BackButton;
                return false;
            });
        };
        logFrame.Add(copyLogsButton);

        _logView = new TextView()
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            WordWrap = false,
            CanFocus = true,
        };
        _logView.ColorScheme = new ColorScheme()
        {
            Normal = new Attribute(BalatroTheme.LightGrey, BalatroTheme.InnerPanelGrey),
            Focus = new Attribute(BalatroTheme.White, BalatroTheme.InnerPanelGrey),
        };
        logFrame.Add(_logView);

        // Bottom button row
        _startButton = new CleanButton()
        {
            X = 1,
            Y = Pos.AnchorEnd(3),
            Width = 16,
            Text = "Start Worker",
        };
        _startButton.ColorScheme = BalatroTheme.GreenButton;
        _startButton.Accept += (s, e) => StartWorker();
        Add(_startButton);

        _stopButton = new CleanButton()
        {
            X = 19,
            Y = Pos.AnchorEnd(3),
            Width = 10,
            Text = "Stop",
            Enabled = false,
        };
        _stopButton.ColorScheme = BalatroTheme.RedButton;
        _stopButton.Accept += (s, e) => StopWorker();
        Add(_stopButton);

        var backButton = new CleanButton()
        {
            X = Pos.AnchorEnd(8),
            Y = Pos.AnchorEnd(3),
            Width = 8,
            Text = "Back",
        };
        backButton.ColorScheme = BalatroTheme.BackButton;
        backButton.Accept += (s, e) =>
        {
            StopWorker();
            MotelyTUI.CloseWindow(this);
        };
        Add(backButton);

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == KeyCode.Esc)
            {
                StopWorker();
                MotelyTUI.CloseWindow(this);
                e.Handled = true;
            }
        };

        LogMessage("MotelyWorker connects to the seedfinder.app pool and claims search blocks.");
        LogMessage($"Pool: {TuiSettings.WorkerPoolUrl}");
        LogMessage("");
        LogMessage("Press 'Start Worker' to begin.");
    }

    private void StartWorker()
    {
        if (_isRunning) return;

        var poolUrl = _poolUrlField.Text?.ToString()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(poolUrl))
        {
            LogMessage("[ERROR] Pool URL is required.");
            return;
        }

        if (!int.TryParse(_threadsField.Text?.ToString(), out int threads) || threads < 1)
            threads = Environment.ProcessorCount;
        threads = Math.Clamp(threads, 1, Environment.ProcessorCount);

        TuiSettings.WorkerPoolUrl = poolUrl;
        TuiSettings.WorkerThreads = threads;
        TuiSettings.Save();

        var workerExe = FindWorkerExecutable();
        if (workerExe == null)
        {
            LogMessage("[ERROR] MotelyWorker executable not found.");
            LogMessage($"  Searched in: {AppContext.BaseDirectory}");
            LogMessage("  Build with: dotnet publish Motely.DistributedWorker -c Release");
            return;
        }

        _isRunning = true;
        SetStatus("Running", BalatroTheme.Green);
        _startButton.Enabled = false;
        _stopButton.Enabled = true;

        _logView.Text = "";
        LogMessage($"[{DateTime.Now:HH:mm:ss}] Starting MotelyWorker");
        LogMessage($"[{DateTime.Now:HH:mm:ss}] Pool: {poolUrl}  Threads: {threads}");
        LogMessage("");

        var psi = new ProcessStartInfo(workerExe)
        {
            Arguments = $"--pool {poolUrl} --threads {threads}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            _workerProcess = Process.Start(psi)!;

            _ = Task.Run(async () =>
            {
                while (!_workerProcess.StandardOutput.EndOfStream && !_cts.Token.IsCancellationRequested)
                {
                    var line = await _workerProcess.StandardOutput.ReadLineAsync(_cts.Token).ConfigureAwait(false);
                    if (line != null) Application.Invoke(() => LogMessage(line));
                }
            }, _cts.Token);

            _ = Task.Run(async () =>
            {
                while (!_workerProcess.StandardError.EndOfStream && !_cts.Token.IsCancellationRequested)
                {
                    var line = await _workerProcess.StandardError.ReadLineAsync(_cts.Token).ConfigureAwait(false);
                    if (line != null) Application.Invoke(() => LogMessage(line));
                }
            }, _cts.Token);

            _ = Task.Run(async () =>
            {
                await _workerProcess.WaitForExitAsync(_cts.Token).ConfigureAwait(false);
                var code = _workerProcess.ExitCode;
                Application.Invoke(() =>
                {
                    _isRunning = false;
                    SetStatus("Stopped", BalatroTheme.Orange);
                    _startButton.Enabled = true;
                    _stopButton.Enabled = false;
                    LogMessage($"\n[{DateTime.Now:HH:mm:ss}] Worker exited (code {code})");
                });
            }, _cts.Token);
        }
        catch (Exception ex)
        {
            _isRunning = false;
            SetStatus("Error", BalatroTheme.Red);
            _startButton.Enabled = true;
            _stopButton.Enabled = false;
            LogMessage($"[ERROR] Failed to start: {ex.Message}");
        }
    }

    private void StopWorker()
    {
        if (!_isRunning || _workerProcess == null) return;
        try { _workerProcess.Kill(entireProcessTree: true); } catch { }
        _isRunning = false;
        SetStatus("Stopped", BalatroTheme.Orange);
        _startButton.Enabled = true;
        _stopButton.Enabled = false;
        LogMessage($"[{DateTime.Now:HH:mm:ss}] Worker stopped by user.");
    }

    private static string? FindWorkerExecutable()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "MotelyWorker"),
            Path.Combine(baseDir, "MotelyWorker.exe"),
            Path.Combine(baseDir, "..", "Motely.DistributedWorker", "MotelyWorker"),
            Path.Combine(baseDir, "..", "Motely.DistributedWorker", "MotelyWorker.exe"),
            Path.Combine(baseDir, "..", "..", "..", "Motely.DistributedWorker", "bin", "Debug", "net10.0", "MotelyWorker.exe"),
            Path.Combine(baseDir, "..", "..", "..", "Motely.DistributedWorker", "bin", "Release", "net10.0", "MotelyWorker.exe"),
            Path.Combine(baseDir, "..", "..", "Motely.DistributedWorker", "bin", "Release", "net10.0", "linux-x64", "publish", "MotelyWorker"),
            Path.Combine(baseDir, "..", "..", "Motely.DistributedWorker", "bin", "Release", "net10.0", "win-x64", "publish", "MotelyWorker.exe"),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            var exeLinux = Path.Combine(dir, "MotelyWorker");
            if (File.Exists(exeLinux)) return exeLinux;
            var exeWin = exeLinux + ".exe";
            if (File.Exists(exeWin)) return exeWin;
        }

        return null;
    }

    private void LogMessage(string message)
    {
        var existing = _logView.Text?.ToString() ?? "";
        _logView.Text = existing + message + "\n";
    }

    private void SetStatus(string text, Color color)
    {
        _statusLabel.Text = text;
        _statusLabel.ColorScheme = new ColorScheme() { Normal = new Attribute(color, BalatroTheme.ModalGrey) };
    }

    private static void CopyToClipboard(string text)
    {
        try { Clipboard.TrySetClipboardData(text); } catch { }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts.Cancel();
            _cts.Dispose();
            StopWorker();
            _workerProcess?.Dispose();
        }
        base.Dispose(disposing);
    }
}
