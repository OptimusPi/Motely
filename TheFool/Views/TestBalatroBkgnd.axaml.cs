using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Motely.TheFool.Controls;

namespace TheFool.Views;

public partial class TestBalatroBkgnd : Window
{
    private BalatroBkgnd? _background;
    private Button? _slowMotionButton;
    private Button? _normalSpeedButton;
    private Button? _hyperspeedButton;
    
    public TestBalatroBkgnd()
    {
        InitializeComponent();
    }
    
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        
        // Find controls
        _background = this.FindControl<BalatroBkgnd>("BalatroBkgnd");
        _slowMotionButton = this.FindControl<Button>("SlowMotionButton");
        _normalSpeedButton = this.FindControl<Button>("NormalSpeedButton");
        _hyperspeedButton = this.FindControl<Button>("HyperspeedButton");
        
        // Wire up events
        if (_slowMotionButton != null)
            _slowMotionButton.Click += SlowMotion_Click;
        if (_normalSpeedButton != null)
            _normalSpeedButton.Click += NormalSpeed_Click;
        if (_hyperspeedButton != null)
            _hyperspeedButton.Click += Hyperspeed_Click;
    }
    
    private void SlowMotion_Click(object? sender, RoutedEventArgs e)
    {
        if (_background != null)
        {
            _background.SpinSpeed = 0.5;
            _background.MoveSpeed = 2.0;
        }
    }
    
    private void NormalSpeed_Click(object? sender, RoutedEventArgs e)
    {
        if (_background != null)
        {
            _background.SpinSpeed = 2.0;
            _background.MoveSpeed = 7.0;
        }
    }
    
    private void Hyperspeed_Click(object? sender, RoutedEventArgs e)
    {
        if (_background != null)
        {
            _background.SpinSpeed = 5.0;
            _background.MoveSpeed = 15.0;
        }
    }
}
