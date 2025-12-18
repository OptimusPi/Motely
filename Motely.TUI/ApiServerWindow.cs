using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
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

        // Endpoints panel
        var endpointsFrame = new FrameView()
        {
            X = 1,
            Y = 5,
            Width = Dim.Fill() - 2,
            Height = 5,
            Title = "Endpoints",
        };
        endpointsFrame.SetScheme(BalatroTheme.InnerPanel);
        Add(endpointsFrame);

        // Endpoint buttons (visual, show what's available)
        var searchEndpoint = new CleanButton()
        {
            X = 1,
            Y = 0,
            Text = "POST /search",
        };
        searchEndpoint.SetScheme(BalatroTheme.BlueButton);
        searchEndpoint.Accept += (s, e) => LogMessage("[INFO] /search - Search random seeds with filter");
        endpointsFrame.Add(searchEndpoint);

        var searchDesc = new Label()
        {
            X = Pos.Right(searchEndpoint) + 2,
            Y = 0,
            Text = "Search random seeds",
        };
        endpointsFrame.Add(searchDesc);

        var analyzeEndpoint = new CleanButton()
        {
            X = 1,
            Y = 2,
            Text = "POST /analyze",
        };
        analyzeEndpoint.SetScheme(BalatroTheme.GreenButton);
        analyzeEndpoint.Accept += (s, e) => LogMessage("[INFO] /analyze - Analyze a specific seed");
        endpointsFrame.Add(analyzeEndpoint);

        var analyzeDesc = new Label()
        {
            X = Pos.Right(analyzeEndpoint) + 2,
            Y = 2,
            Text = "Analyze specific seed",
        };
        endpointsFrame.Add(analyzeDesc);

        // Request log (taller for better visibility)
        var logFrame = new FrameView()
        {
            X = 1,
            Y = 10,
            Width = Dim.Fill() - 2,
            Height = 11,
            Title = "Request Log",
        };
        logFrame.SetScheme(BalatroTheme.InnerPanel);
        Add(logFrame);

        _logView = new TextView()
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            WordWrap = true, // Wrap long lines instead of cutting them off!
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
        _stopButton.Accept += (s, e) => StopServerOnly();
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
        backButton.Accept += (s, e) => StopAndClose();
        Add(backButton);

        // Keyboard shortcuts
        KeyDown += (sender, e) =>
        {
            if (e.KeyCode == KeyCode.Esc)
            {
                StopAndClose();
                e.Handled = true;
            }
        };

        // Start server automatically
        Task.Run(() => StartServerAsync(host, port));
    }

    private string FindSolutionRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var dir = new DirectoryInfo(currentDir);
        
        // Look for .sln file by going up directories
        while (dir != null)
        {
            var slnFiles = dir.GetFiles("*.sln");
            if (slnFiles.Length > 0)
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        
        // Fallback: go up to BalatroSeedOracle root
        return Path.Combine(currentDir, "..", "..", "..");
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

            // Create the API using the factory
            var args = new[] { "--urls", $"http://{host}:{port}" };
            _server = MotelyApiFactory.CreateApi(args);

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

            // Start the API directly
            await _server.RunAsync($"http://{host}:{port}");
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
        }
    }

    private void StopServerOnly()
    {
        if (_isRunning)
        {
            LogMessage("Stopping clean API server...");
            _cts?.Cancel();
            _cts = null;
            _server = null;
            _stopButton.Enabled = false;
            _stopButton.Visible = false; // Hide when stopped
        }
    }

    private void StopAndClose()
    {
        StopServerOnly();
        StopTunnel();
        App?.RequestStop();
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

    private void LogMessage(string message)
    {
        try
        {
            App?.Invoke(() =>
            {
                if (_logView != null)
                {
                    _logView.Text += message + "\n";
                    _logView.MoveEnd();
                }
            });
        }
        catch (ObjectDisposedException)
        {
            // Window closed while logging - ignore
        }
    }

    private void CopyToClipboard(string text)
    {
        try
        {
            // Use platform-specific commands directly (Terminal.Gui clipboard may truncate)
            if (OperatingSystem.IsWindows())
            {
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
        }
        catch
        {
            // Ignore clipboard errors
        }
    }

    private void OpenInBrowser(string url)
    {
        try
        {
            LogMessage($"[BROWSER] Opening {url}");
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
            try
            {
                MotelyTUI.App?.Invoke(() =>
                {
                    _window?.LogMessage(value ?? string.Empty);
                });
            }
            catch (ObjectDisposedException)
            {
                // Window closed while writing - ignore
            }
        }
    }
}
