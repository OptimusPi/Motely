using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using SukiUI.Controls;

namespace TheFool.Views;

public partial class MainWindow : SukiWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }
    
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
