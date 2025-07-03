using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using TheFool.Controls;

namespace TheFool.Views;

public partial class TestBalatroBkgnd : Window
{
    private BalatroBkgndGPU? _background;
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
        _background = this.FindControl<BalatroBkgndGPU>("BalatroBkgndGPU");
        _slowMotionButton = this.FindControl<Button>("SlowMotionButton");
        _normalSpeedButton = this.FindControl<Button>("NormalSpeedButton");
        _hyperspeedButton = this.FindControl<Button>("HyperspeedButton");
        
        // Wire up events (GPU version doesn't expose speed controls, so these are placeholders)
        if (_slowMotionButton != null)
            _slowMotionButton.Click += SlowMotion_Click;
        if (_normalSpeedButton != null)
            _normalSpeedButton.Click += NormalSpeed_Click;
        if (_hyperspeedButton != null)
            _hyperspeedButton.Click += Hyperspeed_Click;
    }
    
    private void SlowMotion_Click(object? sender, RoutedEventArgs e)
    {
        // GPU version handles animation internally - no external speed control needed
        // The shader automatically animates at optimal GPU speed
    }
    
    private void NormalSpeed_Click(object? sender, RoutedEventArgs e)
    {
        // GPU version handles animation internally - no external speed control needed
        // The shader automatically animates at optimal GPU speed
    }
    
    private void Hyperspeed_Click(object? sender, RoutedEventArgs e)
    {
        // GPU version handles animation internally - no external speed control needed
        // The shader automatically animates at optimal GPU speed
    }
}
