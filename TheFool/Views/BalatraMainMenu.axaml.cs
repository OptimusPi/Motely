using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using System;

namespace Oracle.Views
{
    public partial class BalatraMainMenu : UserControl
    {
        private TextBlock? _profileName;
        private Grid? _modalContainer;
        private Border? _searchModal;
        private string _playerName = "Jimbo";
        private Border? _profileButton;

        public BalatraMainMenu()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            
            _profileName = this.FindControl<TextBlock>("ProfileName");
            _modalContainer = this.FindControl<Grid>("ModalContainer");
            _searchModal = this.FindControl<Border>("SearchModal");
            
            // Find the profile button border
            var bottomMenu = this.FindControl<Grid>("BottomMenu");
            if (bottomMenu != null)
            {
                foreach (var child in bottomMenu.Children)
                {
                    if (child is Border border && border.Classes.Contains("profile-button"))
                    {
                        _profileButton = border;
                        border.PointerPressed += OnProfileClick;
                        break;
                    }
                }
            }
        }

        private void OnProfileClick(object? sender, PointerPressedEventArgs e)
        {
            // TODO: Show profile modal or handle profile editing
            Console.WriteLine($"Profile clicked! Current name: {_playerName}");
        }
        
        private void OnHelpClick(object? sender, PointerPressedEventArgs e)
        {
            // TODO: Show help modal
            Console.WriteLine("Help clicked!");
        }

        private void OnSearchClick(object? sender, RoutedEventArgs e)
        {
            ShowModal(_searchModal);
        }

        private void OnOptionsClick(object? sender, RoutedEventArgs e)
        {
            // TODO: Show options modal
            Console.WriteLine("Options clicked!");
        }

        private void OnQuitClick(object? sender, RoutedEventArgs e)
        {
            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown();
            }
        }

        private void OnFunRunClick(object? sender, RoutedEventArgs e)
        {
            // TODO: Show collection modal
            Console.WriteLine("Collection clicked!");
        }

        private void OnStartSearchClick(object? sender, RoutedEventArgs e)
        {
            // Navigate to the search interface
            if (this.GetVisualRoot() is MainWindow mainWindow)
            {
                mainWindow.ShowTabbedInterface();
            }
            Console.WriteLine($"Start Search clicked! Author: {_playerName}");
        }

        private void OnManageFiltersClick(object? sender, RoutedEventArgs e)
        {
            // TODO: Show filter management interface
            Console.WriteLine($"Manage Filters clicked! Author: {_playerName}");
        }

        private void CloseModal(object? sender, RoutedEventArgs e)
        {
            if (_searchModal != null)
            {
                _searchModal.IsVisible = false;
            }
        }

        private void ShowModal(Border? modal)
        {
            if (modal != null)
            {
                modal.IsVisible = true;
            }
        }

        public string GetPlayerName() => _playerName;
    }
}