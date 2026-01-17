using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Motely.API;

namespace Motely.TUI;

public class ApiServerWindow : Window
{
    private TextView _logView;
    private Label _statusLabel;
    private Label _urlLabel;
    private Label _tunnelLabel;
    private CleanButton _stopButton;
    private CleanButton _tunnelButton;
    private WebApplication? _server;
    private CancellationTokenSource? _cts;
    private Process? _tunnelProcess;
    private Task? _serverTask;
    private bool _isRunning = false;
    private string _serverUrl = "";

    public ApiServerWindow(string host = "localhost", int port = 3141)
    {
        _serverUrl = $"http://{host}:{port}/";

        // Wide window to accommodate long cloudflare URLs
        Title = "API Server";
        X = Pos.Center();
        Y = Pos.Center();
        Width = Dim.Percent(85); // Use 85% of screen width
        Height = 25; // Taller for bigger request log
        CanFocus = true;
        SetScheme(BalatroTheme.Window);

        // Status row
        _statusLabel = new Label()
        {
            X = 1,
            Y = 1,
            Text = "Starting...",
        };
        _statusLabel.SetScheme(new Scheme()
        {
            Normal = new Attribute(BalatroTheme.Orange, BalatroTheme.ModalGrey),
        });
        Add(_statusLabel);

        // URL (clickable hint) - full width to show complete URL
        _urlLabel = new Label()
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill() - 2,
            Text = _serverUrl,
        };
        _urlLabel.SetScheme(new Scheme()
        {
            Normal = new Attribute(BalatroTheme.Blue, BalatroTheme.ModalGrey),
        });
        _urlLabel.MouseClick += (s, e) =>
        {
            CopyToClipboard(_serverUrl);
            LogMessage($"[CLIPBOARD] Copied URL: {_serverUrl}");
            e.Handled = true;
        };
        Add(_urlLabel);

        // Tunnel status & button - full width to show complete URL
        _tunnelLabel = new Label()
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill() - 2, // Full width minus margins
            Text = "",
        };
        _tunnelLabel.SetScheme(new Scheme()
        {
            Normal = new Attribute(BalatroTheme.Green, BalatroTheme.ModalGrey),
        });
        _tunnelLabel.MouseClick += (s, e) =>
        {
            if (!string.IsNullOrWhiteSpace(_tunnelLabel.Text.ToString()))
            {
                var urlText = _tunnelLabel.Text.ToString();
                if (urlText.StartsWith("http"))
                {
                    CopyToClipboard(urlText);
                    LogMessage($"[CLIPBOARD] Copied URL: {urlText}");
                }
            }
            e.Handled = true;
        };
        Add(_tunnelLabel);

        // Open Web UI button - launches browser with local URL
        var openWebButton = new CleanButton()
        {
            X = Pos.AnchorEnd(34),
            Y = 1,
            Text = "Open Web UI",
        };
        openWebButton.SetScheme(BalatroTheme.GreenButton);
        openWebButton.Accept += (s, e) => OpenInBrowser(_serverUrl);
        Add(openWebButton);

        _tunnelButton = new CleanButton()
        {
            X = Pos.AnchorEnd(18),
            Y = 1,
            Text = "Start Tunnel",
        };
        _tunnelButton.SetScheme(BalatroTheme.PurpleButton);
        _tunnelButton.Accept += (s, e) => StartTunnel();
        Add(_tunnelButton);

        // Endpoints panel removed to make room for log
        
        // Request log (expanded to fill space)
        var logFrame = new FrameView()
        {
            X = 1,
            Y = 5,
            Width = Dim.Fill() - 2,
            Height = Dim.Fill() - 4, // Leave room for buttons at bottom
            Title = "Request Log",
        };
        logFrame.SetScheme(BalatroTheme.InnerPanel);
        Add(logFrame);

        // Copy Logs button inside the frame
        var copyLogsButton = new CleanButton()
        {
            X = Pos.AnchorEnd(15), // "Copy Logs" is ~9 chars + padding
            Y = 0,
            Text = "Copy Logs",
        };
        copyLogsButton.SetScheme(BalatroTheme.BackButton); // Orange
        copyLogsButton.Accept += (s, e) => 
        {
            if (_logView?.Text != null)
            {
                CopyToClipboard(_logView.Text.ToString());
                // Flash message
                 copyLogsButton.Text = "COPIED!";
                 copyLogsButton.SetScheme(BalatroTheme.GreenButton);
                 
                 Task.Run(async () => {
                     await Task.Delay(1000);
                     MotelyTUI.App?.Invoke(() => {
                         copyLogsButton.Text = "Copy Logs";
                         copyLogsButton.SetScheme(BalatroTheme.BackButton);
                     });
                 });
            }
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
        _logView.SetScheme(new Scheme()
        {
            Normal = new Attribute(BalatroTheme.LightGrey, BalatroTheme.InnerPanelGrey),
            Focus = new Attribute(BalatroTheme.White, BalatroTheme.InnerPanelGrey),
        });
        logFrame.Add(_logView);

        // Stop Server button - red, above Back
        _stopButton = new CleanButton()
        {
            X = 1,
            Y = Pos.AnchorEnd(3),
            Text = "Stop Server Host",
            Width = Dim.Fill() - 2,
            TextAlignment = Alignment.Center,
        };
        _stopButton.SetScheme(BalatroTheme.RedButton);
        _stopButton.Accept += async (s, e) => await StopServerOnlyAsync();
        Add(_stopButton);

        // Back button - orange
        var backButton = new CleanButton()
        {
            X = 1,
            Y = Pos.AnchorEnd(1),
            Text = "Back",
            Width = Dim.Fill() - 2,
            TextAlignment = Alignment.Center,
        };
        backButton.SetScheme(BalatroTheme.BackButton);
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

            // Create the API using the API Program
            var args = new[] { "--urls", $"http://{host}:{port}" };
            _server = MotelyApiHost.CreateHost(args);

            // Apply TUI thread budget to API search manager (multi-search allocator uses this budget)
            SearchManager.Instance.SetThreadBudget(TuiSettings.ThreadCount);

            App?.Invoke(() =>
            {
                _statusLabel.Text = "Running";
                _statusLabel.SetScheme(new Scheme()
                {
                    Normal = new Attribute(BalatroTheme.Green, BalatroTheme.ModalGrey),
                });
            });

            LogMessage($"Clean API started on {_serverUrl}");
            LogMessage("Web UI available at same URL");

            await _server.StartAsync(_cts.Token);
            await _server.WaitForShutdownAsync(_cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected during Stop
        }
        catch (Exception ex)
        {
            LogMessage($"[ERROR] {ex.Message}");
            App?.Invoke(() =>
            {
                _statusLabel.Text = "Failed";
                _statusLabel.SetScheme(new Scheme()
                {
                    Normal = new Attribute(BalatroTheme.Red, BalatroTheme.ModalGrey),
                });
            });
        }
        finally
        {
            if (_server != null)
            {
                try
                {
                    using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await _server.StopAsync(stopCts.Token);
                }
                catch { }
                try { await _server.DisposeAsync(); } catch { }
            }

            // Restore original console output
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            
            _isRunning = false;
            App?.Invoke(() =>
            {
                _statusLabel.Text = "Stopped";
                _statusLabel.SetScheme(new Scheme()
                {
                    Normal = new Attribute(BalatroTheme.Gray, BalatroTheme.ModalGrey),
                });
                _stopButton.Visible = false; // Hide when server stops (back button remains)
            });

            _server = null;
            _cts = null;
        }
    }

    private async Task StopServerOnlyAsync()
    {
        if (_isRunning)
        {
            try
            {
                LogMessage("Stopping searches...");
                await SearchManager.Instance.StopAllSearchesAsync();
            }
            catch (Exception ex)
            {
                LogMessage($"[WARN] Failed to stop all searches: {ex.Message}");
            }

            LogMessage("Stopping clean API server...");
            var cts = _cts;
            var server = _server;

            App?.Invoke(() =>
            {
                _stopButton.Enabled = false;
                _stopButton.Visible = false;
            });

            try { cts?.Cancel(); } catch { }

            if (server != null)
            {
                try
                {
                    using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await server.StopAsync(stopCts.Token);
                }
                catch { }
                try { await server.DisposeAsync(); } catch { }
            }
        }
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
        StopTunnel();
        App?.Invoke(() => App?.RequestStop());
    }

    private bool ShowStopConfirmDialog()
    {
        var dialog = new Dialog()
        {
            Title = "Stop API Server?",
            Width = 60,
            Height = 9,
        };
        dialog.SetScheme(BalatroTheme.Window);

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
        stopBtn.SetScheme(BalatroTheme.RedButton);
        stopBtn.Accept += (s, e) =>
        {
            stop = true;
            MotelyTUI.App?.RequestStop(dialog);
        };
        dialog.Add(stopBtn);

        var cancelBtn = new CleanButton()
        {
            X = Pos.Right(stopBtn) + 2,
            Y = Pos.AnchorEnd(1),
            Text = " Cancel ",
        };
        cancelBtn.SetScheme(BalatroTheme.BackButton);
        cancelBtn.Accept += (s, e) =>
        {
            stop = false;
            MotelyTUI.App?.RequestStop(dialog);
        };
        dialog.Add(cancelBtn);

        MotelyTUI.App?.Run(dialog);
        return stop;
    }

    private void StartTunnel()
    {
        if (_tunnelProcess != null)
        {
            LogMessage("[TUNNEL] Already running");
            return;
        }

        _tunnelButton.Text = "Starting...";
        _tunnelButton.Enabled = false;

        Task.Run(() =>
        {
            try
            {
                var cloudflared = FindCloudflared();
                if (string.IsNullOrEmpty(cloudflared))
                    throw new FileNotFoundException("cloudflared not found. Install from https://developers.cloudflare.com/cloudflare-one/connections/connect-apps/install-and-setup/installation/");

                LogMessage($"[TUNNEL] Found cloudflared: {cloudflared}");
                LogMessage("[TUNNEL] Starting free trycloudflare.com tunnel...");

                var uri = new Uri(_serverUrl);
                var port = uri.Port;

                var psi = new ProcessStartInfo
                {
                    FileName = cloudflared,
                    Arguments = $"tunnel --url http://localhost:{port}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                _tunnelProcess = new Process { StartInfo = psi };
                _tunnelProcess.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data)) { LogMessage($"[TUNNEL] {e.Data}"); ParseTunnelOutput(e.Data); }
                };
                _tunnelProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data)) { LogMessage($"[TUNNEL] {e.Data}"); ParseTunnelOutput(e.Data); }
                };

                _tunnelProcess.Start();
                _tunnelProcess.BeginOutputReadLine();
                _tunnelProcess.BeginErrorReadLine();

                App?.Invoke(() =>
                {
                    _tunnelButton.Text = "Stop Tunnel";
                    _tunnelButton.Enabled = true;
                    _tunnelButton.SetScheme(BalatroTheme.RedButton);
                });
            }
            catch (Exception ex)
            {
                App?.Invoke(() =>
                {
                    _tunnelButton.Text = "Start Tunnel";
                    _tunnelButton.Enabled = true;
                    _tunnelLabel.Text = "";
                });
                LogMessage($"[TUNNEL] Error: {ex.Message}");
                _tunnelProcess = null;
            }
        });
    }

    private void ParseTunnelOutput(string line)
    {
        if (line.Contains("trycloudflare.com"))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (part.StartsWith("https://") && part.Contains("trycloudflare.com"))
                {
                    var tunnelUrl = part.TrimEnd('/', '.', ',');
                    App?.Invoke(() => _tunnelLabel.Text = tunnelUrl);
                    LogMessage($"[TUNNEL] Public URL: {tunnelUrl}");
                    break;
                }
            }
        }
    }

    private string? FindCloudflared()
    {
        var candidates = new List<string>();
        if (OperatingSystem.IsWindows())
        {
            candidates.Add("cloudflared.exe");
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "cloudflared", "cloudflared.exe"));
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "cloudflared", "cloudflared.exe"));
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
                if (!string.IsNullOrEmpty(dir)) candidates.Add(Path.Combine(dir, "cloudflared.exe"));
        }
        else
        {
            candidates.Add("cloudflared");
            candidates.Add("/usr/local/bin/cloudflared");
            candidates.Add("/usr/bin/cloudflared");
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", "cloudflared"));
            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(':'))
                if (!string.IsNullOrEmpty(dir)) candidates.Add(Path.Combine(dir, "cloudflared"));
        }

        foreach (var candidate in candidates)
        {
            try
            {
                if (File.Exists(candidate)) return candidate;
                if (candidate == "cloudflared" || candidate == "cloudflared.exe")
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = candidate,
                        Arguments = "--version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    };
                    using var proc = Process.Start(psi);
                    proc?.WaitForExit(2000);
                    if (proc?.ExitCode == 0) return candidate;
                }
            }
            catch { }
        }
        return null;
    }

    private void StopTunnel()
    {
        if (_tunnelProcess != null && !_tunnelProcess.HasExited)
        {
            try
            {
                _tunnelProcess.Kill();
                _tunnelProcess.Dispose();
                LogMessage("[TUNNEL] Stopped");
            }
            catch { }
        }
        _tunnelProcess = null;
        App?.Invoke(() =>
        {
            _tunnelLabel.Text = "";
            _tunnelButton.Text = "Start Tunnel";
            _tunnelButton.Enabled = true;
            _tunnelButton.SetScheme(BalatroTheme.PurpleButton);
        });
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
                    Arguments = $"/c echo {text} | clip",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                };
                Process.Start(psi)?.WaitForExit();
            }
            else if (OperatingSystem.IsMacOS())
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "pbcopy",
                    Arguments = text,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                Process.Start(psi)?.WaitForExit();
            }
            else if (OperatingSystem.IsLinux())
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "xclip",
                    Arguments = "-selection clipboard " + text,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                try
                {
                    Process.Start(psi)?.WaitForExit();
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
            App?.Invoke(() =>
            {
                if (_logView != null)
                {
                    _logView.Text += message;
                    _logView.MoveEnd();
                }
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
        private ApiServerWindow _window;

        public ApiLogWriter(ApiServerWindow window)
        {
            _window = window;
        }

        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;

        public override void Write(char value)
        {
            try
            {
                MotelyTUI.App?.Invoke(() =>
                {
                    _window?.LogMessage(value.ToString());
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
            
            // Strip ANSI escape codes that cause weird characters in TUI
            value = StripAnsiCodes(value);
            
            // Filter out verbose API messages that clutter the TUI
            if (ShouldFilterMessage(value))
                return;
                
            try
            {
                MotelyTUI.App?.Invoke(() =>
                {
                    _window?.LogMessage(value);
                });
            }
            catch (ObjectDisposedException)
            {
                // Window closed while writing - ignore
            }
        }

        public override void WriteLine(string? value)
        {
            if (value == null)
                return;
            
            // Strip ANSI escape codes that cause weird characters in TUI
            value = StripAnsiCodes(value);
            
            // Filter out verbose API messages that clutter the TUI
            if (ShouldFilterMessage(value))
                return;
                
            try
            {
                MotelyTUI.App?.Invoke(() =>
                {
                    _window?.LogMessage(value);
                });
            }
            catch (ObjectDisposedException)
            {
                // Window closed while writing - ignore
            }
        }

        private string StripAnsiCodes(string input)
        {
            // Remove ANSI escape sequences that cause display issues in TUI
            // Pattern matches: \x1b[...m or \x1b[...H or similar ANSI codes
            return System.Text.RegularExpressions.Regex.Replace(input, @"\x1b\[[0-9;]*[mHJK]", "");
        }

        private bool ShouldFilterMessage(string message)
        {
            // Filter out verbose API logging that clutters the TUI interface
            var filters = new[]
            {
                "[Scheduler]",
                "[SearchManager]",
                "Error reading",
                "Error deleting",
                "Error canceling",
                "Error waiting",
                "Error exporting",
                "Error checkpointing",
                "Error disposing",
                "Error saving",
                "Failed to read",
                "Failed to read from",
                "Failed to dump",
                "Error parsing",
                "Warning: Failed",
                "Search failed:",
                "was cancelled",
                "Checkpoint failed",
                "SaveBatchPosition failed"
            };

            return filters.Any(filter => message.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }
    }
}
