using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace TheFool.Services;

public static class ErrorHandler
{
    public static void SetupGlobalHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        Console.WriteLine($"Unhandled exception: {exception}");
        
        if (e.IsTerminating)
        {
            ShowCriticalError(exception);
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Console.WriteLine($"Unobserved task exception: {e.Exception}");
        e.SetObserved();
    }

    public static async Task ShowErrorAsync(Window parent, string title, string message, Exception? exception = null)
    {
        await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var dialog = new Window
            {
                Title = title,
                Width = 500,
                Height = 300,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            var content = new StackPanel { Margin = new Thickness(20) };
            content.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 15)
            });

            if (exception != null)
            {
                var expander = new Expander
                {
                    Header = "Technical Details",
                    Content = new ScrollViewer
                    {
                        Content = new TextBox
                        {
                            Text = exception.ToString(),
                            IsReadOnly = true,
                            AcceptsReturn = true,
                            TextWrapping = TextWrapping.Wrap
                        },
                        MaxHeight = 150
                    }
                };
                content.Children.Add(expander);
            }

            var okButton = new Button
            {
                Content = "OK",
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 15, 0, 0)
            };
            okButton.Click += (s, e) => dialog.Close();
            content.Children.Add(okButton);

            dialog.Content = content;
            
            if (parent.IsVisible)
            {
                await dialog.ShowDialog(parent);
            }
            else
            {
                dialog.Show();
            }
        });
    }

    private static void ShowCriticalError(Exception? exception)
    {
        var message = exception?.Message ?? "Unknown error";
        
        // Just use console for critical errors - simpler and cross-platform
        Console.Error.WriteLine($"Critical error: {message}");
        Console.Error.WriteLine(exception?.ToString());
    }
}
