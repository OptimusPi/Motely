using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Oracle.Controls;

namespace Oracle.Views;

public partial class MainWindow : Window
{
    private Grid? _mainGrid;
    private BalatraMainMenu? _mainMenu;
    private Grid? _contentGrid;
    
    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        InitializeMainMenu();
    }
    
    private void InitializeMainMenu()
    {
        // Find the main grid that contains the background
        _mainGrid = this.FindControl<Grid>("MainGrid");
        if (_mainGrid == null) return;
        
        // Find the content grid (the one with tabs)
        _contentGrid = this.FindControl<Grid>("ContentGrid");
        if (_contentGrid != null)
        {
            // Hide the tabbed interface initially
            _contentGrid.IsVisible = false;
        }
        
        // Create and add the main menu
        _mainMenu = new BalatraMainMenu();
        _mainGrid.Children.Add(_mainMenu);
    }
    
    public void ShowTabbedInterface()
    {
        if (_mainMenu != null) _mainMenu.IsVisible = false;
        if (_contentGrid != null) _contentGrid.IsVisible = true;
    }
    
    public void ShowMainMenu()
    {
        if (_contentGrid != null) _contentGrid.IsVisible = false;
        if (_mainMenu != null) _mainMenu.IsVisible = true;
    }
}