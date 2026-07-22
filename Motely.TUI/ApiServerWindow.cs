using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Motely.HelperAPI;

namespace Motely.TUI;

public class ApiServerWindow : Window
{
    private TextView _logView = null!;
    private Label _statusLabel = null!;
    private Label _urlLabel = null!;
    private CleanButton _stopButton = null!;
    private WebApplication? _server;
    private CancellationTokenSource? _cts;
    private Task? _serverTask;
    private bool _isRunning = false;
    private string _serverUrl = "";

    public ApiServerWindow(string host = "localhost", int port = 3141)
    {
        _serverUrl = $"http://{host}:{port}/";

        Title = "API Server";
        X = Pos.Center();
        Y = Pos.Center();
        Width = Dim.Percent(80); // Use 80% of screen width
        Height = 24; // Taller for bigger request log
        CanFocus = true;
        ColorScheme = BalatroTheme.Window;

        // Status row
        _statusLabel = new Label()
        {
            X = 1,
            Y = 1,
            Text = "Starting...",
        };
        _statusLabel.ColorScheme = new ColorScheme()
        {
            Normal = new Attribute(BalatroTheme.Orange, BalatroTheme.ModalGrey),
        };
        Add(_statusLabel);

        // URL (clickable hint) - full width to show complete URL
        _urlLabel = new Label()
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill()! - 2,
            Text = _serverUrl,
        };
        _urlLabel.ColorScheme = new ColorScheme()
        {
            Normal = new Attribute(BalatroTheme.Blue, BalatroTheme.ModalGrey),
        };
        _urlLabel.MouseEvent += (s, e) =>
        {
            if (e.Flags.HasFlag(MouseFlags.Button1Clicked))
            {
                CopyToClipboard(_serverUrl);
                LogMessage($"[CLIPBOARD] Copied URL: {_serverUrl}");
                e.Handled = true;
            }
        };
        Add(_urlLabel);

        // Open Web UI button - launches browser with local URL
        var openWebButton = new CleanButton()
        {
            X = Pos.AnchorEnd(34),
            Y = 1,
            Text = "Open Web UI",
        };
        openWebButton.ColorScheme = BalatroTheme.GreenButton;
        openWebButton.Accept += (s, e) => OpenInBrowser(_serverUrl);
        Add(openWebButton);

        // Removed tunnel button - user can do that on their own
        // Endpoints panel removed to make room for log

        // Request log (expanded to fill space)
        var logFrame = new FrameView()
        {
            X = 1,
            Y = 5,
            Width = Dim.Fill()! - 2,
            Height = Dim.Fill()! - 4, // Leave room for buttons at bottom
            Title = "Request Log",
        };
        logFrame.ColorScheme = BalatroTheme.InnerPanel;
        Add(logFrame);

        // Copy Logs button inside the frame
        var copyLogsButton = new CleanButton()
        {
            X = Pos.AnchorEnd(15), // "Copy Logs" is ~9 chars + padding
            Y = 0,
            Text = "Copy Logs",
        };
        copyLogsButton.ColorScheme = BalatroTheme.BackButton; // Orange
        copyLogsButton.Accept += (s, e) =>
        {
            CopyToClipboard(_logView.Text);
            copyLogsButton.Text = "COPIED!";
            copyLogsButton.ColorScheme = BalatroTheme.GreenButton;
            Application.AddTimeout(
                TimeSpan.FromSeconds(1),
                () =>
                {
                    copyLogsButton.Text = "Copy Logs";
                    copyLogsButton.ColorScheme = BalatroTheme.BackButton;
                    return false;
                }
            );
        };
        logFrame.Add(copyLogsButton);

        _logView = new TextView()
        {
            X = 0,
            Y = 1, // Below the button row
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            WordWrap = true,
            CanFocus = true,
        };
        _logView.ColorScheme = new ColorScheme()
        {
            Normal = new Attribute(BalatroTheme.LightGrey, BalatroTheme.InnerPanelGrey),
            Focus = new Attribute(BalatroTheme.White, BalatroTheme.InnerPanelGrey),
        };
        logFrame.Add(_logView);

        // Stop Server button - red, above Back
        _stopButton = new CleanButton()
        {
            X = 1,
            Y = Pos.AnchorEnd(3),
            Text = "Stop Server Host",
            Width = Dim.Fill()! - 2,
            TextAlignment = Alignment.Center,
        };
        _stopButton.ColorScheme = BalatroTheme.RedButton;
        _stopButton.Accept += (s, e) => _ = StopServerSafeAsync();
        Add(_stopButton);

        // Back button - orange
        var backButton = new CleanButton()
        {
            X = 1,
            Y = Pos.AnchorEnd(1),
            Text = "Back",
            Width = Dim.Fill()! - 2,
            TextAlignment = Alignment.Center,
        };
        backButton.ColorScheme = BalatroTheme.BackButton;
        backButton.Accept += (s, e) => AttemptClose();
        Add(backButton);

        // Keyboard shortcuts
        KeyDown += (sender, e) =>
        {
            if (e.KeyCode == KeyCode.Esc)
            {
                AttemptClose();
                e.Handled = true;
            }
        };

        // Start server automatically
        _serverTask = StartServerAsync(host, port);
    }

    private async Task StartServerAsync(string host, int port)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        try
        {
            _isRunning = true;
            _cts = new CancellationTokenSource();

            // Redirect console output to capture API logs
            var logWriter = new ApiLogWriter(this);
            Console.SetOut(logWriter);
            Console.SetError(logWriter);

            // Same host as Motely.HelperAPI standalone: HelperApiHost.Build (CORS, worker status,
            // wwwroot/WASM — one implementation for CLI and TUI).
            _server = HelperApiHost.Build(["--urls", $"http://{host}:{port}"]);
            await _server.StartAsync(_cts.Token);

            // Status once the server is listening.
            Application.Invoke(() =>
            {
                _statusLabel.Text = "Running";
                _statusLabel.ColorScheme = new ColorScheme()
                {
                    Normal = new Attribute(BalatroTheme.Green, BalatroTheme.ModalGrey),
                };
            });

            var version =
                typeof(global::Motely.Program).Assembly.GetName().Version?.ToString(3) ?? "?";
            LogMessage($"Hosting Motely API v{version}");
            LogMessage($"Listening on {_serverUrl}");
            LogMessage("Web UI available at same URL");
            await _server.WaitForShutdownAsync(_cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected during Stop
        }
        catch (Exception ex)
        {
            LogMessage($"[ERROR] {ex.Message}");
            Application.Invoke(() =>
            {
                _statusLabel.Text = "Failed";
                _statusLabel.ColorScheme = new ColorScheme()
                {
                    Normal = new Attribute(BalatroTheme.Red, BalatroTheme.ModalGrey),
                };
            });
        }
        finally
        {
            if (_server != null)
            {
                try
                {
                    // Aggressive shutdown - only wait 1 second for graceful shutdown
                    using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                    await _server.StopAsync(stopCts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Timeout - force dispose, this is expected
                }
                catch { }

                // Force dispose regardless
                try
                {
                    await _server.DisposeAsync();
                }
                catch { }
            }

            // Restore original console output
            Console.SetOut(originalOut);
            Console.SetError(originalError);

            _isRunning = false;
            Application.Invoke(() =>
            {
                _statusLabel.Text = "Stopped";
                _statusLabel.ColorScheme = new ColorScheme()
                {
                    Normal = new Attribute(BalatroTheme.Gray, BalatroTheme.ModalGrey),
                };
                _stopButton.Visible = false; // Hide when server stops (back button remains)
            });

            _server = null;
            _cts = null;
        }
    }

    private async Task StopServerSafeAsync()
    {
        try
        {
            await StopServerOnlyAsync();
        }
        catch (Exception ex)
        {
            LogMessage($"[ERROR] Stop failed: {ex.Message}");
        }
    }

    private async Task StopServerOnlyAsync()
    {
        if (!_isRunning)
            return;

        // Update UI immediately - don't wait for anything
        Application.Invoke(() =>
        {
            _stopButton.Enabled = false;
            _stopButton.Text = "Stopping...";
        });

        LogMessage("Stopping server...");

        // Cancel token FIRST - signals everything to stop immediately
        try
        {
            _cts?.Cancel();
        }
        catch { }

        // Force stop server with very short timeout (500ms)
        var server = _server;
        if (server != null)
        {
            try
            {
                using var stopCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
                await server.StopAsync(stopCts.Token);
            }
            catch { }

            try
            {
                await server.DisposeAsync();
            }
            catch { }
        }

        LogMessage("Server stopped.");
        Application.Invoke(() => _stopButton.Visible = false);
    }

    private void AttemptClose()
    {
        if (_isRunning)
        {
            var shouldStop = ShowStopConfirmDialog();
            if (!shouldStop)
                return;
        }

        _ = StopAndCloseAsync();
    }

    private async Task StopAndCloseAsync()
    {
        await StopServerOnlyAsync();
        Application.Invoke(() => MotelyTUI.CloseWindow(this));
    }

    private bool ShowStopConfirmDialog()
    {
        var dialog = new Dialog()
        {
            Title = "Stop API Server?",
            Width = 60,
            Height = 9,
        };
        dialog.ColorScheme = BalatroTheme.Window;

        var label = new Label()
        {
            X = Pos.Center(),
            Y = 2,
            Text = "API server is still running.\nStop it before closing?",
            TextAlignment = Alignment.Center,
        };
        dialog.Add(label);

        var stop = false;

        var stopBtn = new CleanButton()
        {
            X = 2,
            Y = Pos.AnchorEnd(1),
            Text = " Stop & Close ",
        };
        stopBtn.ColorScheme = BalatroTheme.RedButton;
        stopBtn.Accept += (s, e) =>
        {
            stop = true;
            Application.RequestStop(dialog);
        };
        dialog.Add(stopBtn);

        var cancelBtn = new CleanButton()
        {
            X = Pos.Right(stopBtn) + 2,
            Y = Pos.AnchorEnd(1),
            Text = " Cancel ",
        };
        cancelBtn.ColorScheme = BalatroTheme.BackButton;
        cancelBtn.Accept += (s, e) =>
        {
            stop = false;
            Application.RequestStop(dialog);
        };
        dialog.Add(cancelBtn);

        Application.Run(dialog);
        return stop;
    }

    private void CopyToClipboard(string text)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd",
                    Arguments = "/c clip",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                };
                var process = Process.Start(psi);
                if (process != null)
                {
                    process.StandardInput.Write(text);
                    process.StandardInput.Close();
                    process.WaitForExit();
                }
            }
            else if (OperatingSystem.IsMacOS())
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "pbcopy",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                };
                var process = Process.Start(psi);
                if (process != null)
                {
                    process.StandardInput.Write(text);
                    process.StandardInput.Close();
                    process.WaitForExit();
                }
            }
            else if (OperatingSystem.IsLinux())
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "xclip",
                    Arguments = "-selection clipboard",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                };
                try
                {
                    var process = Process.Start(psi);
                    if (process != null)
                    {
                        process.StandardInput.Write(text);
                        process.StandardInput.Close();
                        process.WaitForExit();
                    }
                }
                catch
                {
                    // Fallback if xclip not available
                    LogMessage("[CLIPBOARD] xclip not available for Linux clipboard copy");
                }
            }
        }
        catch (Exception ex)
        {
            LogMessage($"[CLIPBOARD] Failed to copy: {ex.Message}");
        }
    }

    private void OpenInBrowser(string url)
    {
        OpenUrl(url);
    }

    private void LogMessage(string message)
    {
        try
        {
            Application.Invoke(() =>
            {
                _logView.Text += message + "\n";
                _logView.MoveEnd();
            });
        }
        catch (ObjectDisposedException)
        {
            // Window closed while logging - ignore
        }
    }

    private void OpenUrl(string url)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", url);
            }
            else if (OperatingSystem.IsLinux())
            {
                Process.Start("xdg-open", url);
            }
        }
        catch (Exception ex)
        {
            LogMessage($"[BROWSER] Failed to open: {ex.Message}");
        }
    }

    // Custom writer to capture API console output and redirect to TUI Request Log
    private class ApiLogWriter : System.IO.TextWriter
    {
        private readonly ApiServerWindow _window;

        public ApiLogWriter(ApiServerWindow window)
        {
            _window = window;
        }

        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;

        // LogMessage already dispatches via App.Invoke — call it directly, no double-Invoke.
        public override void Write(char value) => _window.LogMessage(value.ToString());

        public override void Write(string? value)
        {
            if (value != null)
                _window.LogMessage(value);
        }

        public override void WriteLine(string? value)
        {
            if (value != null)
                _window.LogMessage(value);
        }
    }
}
