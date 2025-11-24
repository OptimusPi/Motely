using Terminal.Gui;
using Motely.API;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Motely.TUI;

public class ApiServerWindow : Window
{
    private TextView _logView;
    private Label _statusLabel;
    private Label _urlLabel;
    private Label _activeSearchesLabel;
    private Button _stopButton;
    private MotelyApiServer? _server;
    private CancellationTokenSource? _cts;
    private bool _isRunning = false;

    public ApiServerWindow(string host = "localhost", int port = 3141)
    {
        Title = "Motely API Server";
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
            Text = "MOTELY API SERVER",
            TextAlignment = Alignment.Center,
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.BrightBlue, ColorName.Black)
            }
        };
        Add(titleLabel);

        // Status
        _statusLabel = new Label()
        {
            X = 2,
            Y = 3,
            Text = "Status: Starting...",
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.BrightRed, ColorName.Black)
            }
        };
        Add(_statusLabel);

        // URL
        _urlLabel = new Label()
        {
            X = 2,
            Y = 4,
            Text = $"URL: http://{host}:{port}/",
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.White, ColorName.Black)
            }
        };
        Add(_urlLabel);

        // Active searches
        _activeSearchesLabel = new Label()
        {
            X = 2,
            Y = 5,
            Text = "Active Searches: 0",
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.White, ColorName.Black)
            }
        };
        Add(_activeSearchesLabel);

        // Log label
        var logLabel = new Label()
        {
            X = 2,
            Y = 7,
            Text = "[REQUEST LOG]",
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.BrightBlue, ColorName.Black)
            }
        };
        Add(logLabel);

        // Log view
        _logView = new TextView()
        {
            X = 2,
            Y = 8,
            Width = Dim.Fill() - 4,
            Height = Dim.Fill() - 6,
            ReadOnly = true,
            WordWrap = false,
            CanFocus = true
        };
        Add(_logView);

        // Stop button
        _stopButton = new Button()
        {
            X = Pos.Center(),
            Y = Pos.AnchorEnd(1),
            Text = "Stop Server (ESC)",
            ColorScheme = new ColorScheme()
            {
                Normal = new Terminal.Gui.Attribute(ColorName.White, ColorName.Black),
                Focus = new Terminal.Gui.Attribute(ColorName.Black, ColorName.BrightRed)
            }
        };
        _stopButton.Accept += (s, e) => StopServer();
        Add(_stopButton);

        // Keyboard shortcuts
        KeyDown += (sender, e) =>
        {
            if (e.KeyCode == KeyCode.Esc)
            {
                StopServer();
                e.Handled = true;
            }
        };

        // Start server automatically
        Task.Run(() => StartServerAsync(host, port));
    }

    private async Task StartServerAsync(string host, int port)
    {
        try
        {
            _isRunning = true;
            _cts = new CancellationTokenSource();

            _server = new MotelyApiServer(host, port, LogMessage);

            Application.Invoke(() =>
            {
                _statusLabel.Text = "Status: Running";
                _statusLabel.ColorScheme = new ColorScheme()
                {
                    Normal = new Terminal.Gui.Attribute(ColorName.BrightBlue, ColorName.Black)
                };
            });

            LogMessage("=".PadRight(60, '='));
            LogMessage("API Endpoints:");
            LogMessage($"  POST {_server.Url}search   - Search 1M random seeds");
            LogMessage($"  POST {_server.Url}analyze  - Analyze specific seed");
            LogMessage("=".PadRight(60, '='));

            await _server.StartAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            LogMessage($"[ERROR] Server failed: {ex.Message}");
            Application.Invoke(() =>
            {
                _statusLabel.Text = "Status: Failed";
                _statusLabel.ColorScheme = new ColorScheme()
                {
                    Normal = new Terminal.Gui.Attribute(ColorName.BrightRed, ColorName.Black)
                };
            });
        }
        finally
        {
            _isRunning = false;
            Application.Invoke(() =>
            {
                _statusLabel.Text = "Status: Stopped";
                _statusLabel.ColorScheme = new ColorScheme()
                {
                    Normal = new Terminal.Gui.Attribute(ColorName.Gray, ColorName.Black)
                };
                _stopButton.Text = "Close";
            });
        }
    }

    private void StopServer()
    {
        if (_isRunning)
        {
            LogMessage("Stopping server...");
            _cts?.Cancel();
            _server?.Stop();
            _stopButton.Enabled = false;
        }
        else
        {
            Application.RequestStop();
        }
    }

    private void LogMessage(string message)
    {
        try
        {
            Application.Invoke(() =>
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
}
