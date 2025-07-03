using System;
using System.IO;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;
using Motely.TheFool.Extensions;
using Avalonia.Rendering;

namespace Motely.TheFool.Controls;

/// <summary>
/// High-performance GPU-accelerated Balatro background using SkSL shaders
/// </summary>
public class ShaderAnimationControl : Control
{
    private CompositionCustomVisual? _customVisual;
    private ShaderVisualHandler? _handler;
    
    #region Properties
    
    public static readonly StyledProperty<double> SpinSpeedProperty =
        AvaloniaProperty.Register<ShaderAnimationControl, double>(nameof(SpinSpeed), 2.0);
    
    public static readonly StyledProperty<double> MoveSpeedProperty =
        AvaloniaProperty.Register<ShaderAnimationControl, double>(nameof(MoveSpeed), 7.0);
    
    public static readonly StyledProperty<double> ContrastProperty =
        AvaloniaProperty.Register<ShaderAnimationControl, double>(nameof(Contrast), 3.5);
    
    public static readonly StyledProperty<double> LightingProperty =
        AvaloniaProperty.Register<ShaderAnimationControl, double>(nameof(Lighting), 1.0);
    
    public static readonly StyledProperty<double> SpinAmountProperty =
        AvaloniaProperty.Register<ShaderAnimationControl, double>(nameof(SpinAmount), 0.2);
    
    public static readonly StyledProperty<double> PixelFilterProperty =
        AvaloniaProperty.Register<ShaderAnimationControl, double>(nameof(PixelFilter), 60.0);
    
    public static readonly StyledProperty<Color> Color1Property =
        AvaloniaProperty.Register<ShaderAnimationControl, Color>(nameof(Color1), Color.FromRgb(222, 68, 59));
    
    public static readonly StyledProperty<Color> Color2Property =
        AvaloniaProperty.Register<ShaderAnimationControl, Color>(nameof(Color2), Color.FromRgb(0, 107, 180));
    
    public static readonly StyledProperty<Color> Color3Property =
        AvaloniaProperty.Register<ShaderAnimationControl, Color>(nameof(Color3), Color.FromRgb(22, 35, 37));
    
    public double SpinSpeed
    {
        get => GetValue(SpinSpeedProperty);
        set => SetValue(SpinSpeedProperty, value);
    }
    
    public double MoveSpeed
    {
        get => GetValue(MoveSpeedProperty);
        set => SetValue(MoveSpeedProperty, value);
    }
    
    public double Contrast
    {
        get => GetValue(ContrastProperty);
        set => SetValue(ContrastProperty, value);
    }
    
    public double Lighting
    {
        get => GetValue(LightingProperty);
        set => SetValue(LightingProperty, value);
    }
    
    public double SpinAmount
    {
        get => GetValue(SpinAmountProperty);
        set => SetValue(SpinAmountProperty, value);
    }
    
    public double PixelFilter
    {
        get => GetValue(PixelFilterProperty);
        set => SetValue(PixelFilterProperty, value);
    }
    
    public Color Color1
    {
        get => GetValue(Color1Property);
        set => SetValue(Color1Property, value);
    }
    
    public Color Color2
    {
        get => GetValue(Color2Property);
        set => SetValue(Color2Property, value);
    }
    
    public Color Color3
    {
        get => GetValue(Color3Property);
        set => SetValue(Color3Property, value);
    }
    
    #endregion
    
    static ShaderAnimationControl()
    {
        ClipToBoundsProperty.OverrideDefaultValue<ShaderAnimationControl>(true);
    }
    
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        
        var compositor = ElementComposition.GetElementVisual(this)?.Compositor;
        if (compositor == null) return;
        
        _handler = new ShaderVisualHandler(this);
        _customVisual = compositor.CreateCustomVisual(_handler);
        
        ElementComposition.SetElementChildVisual(this, _customVisual);
        
        _customVisual.SendHandlerMessage(ShaderVisualHandler.StartAnimationMessage);
    }
    
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        
        _customVisual?.SendHandlerMessage(ShaderVisualHandler.StopAnimationMessage);
        _handler?.Dispose();
        _handler = null;
        _customVisual = null;
    }
    
    protected override Size ArrangeOverride(Size finalSize)
    {
        _customVisual?.SendHandlerMessage(new ShaderVisualHandler.SizeChangedMessage { Size = finalSize });
        return base.ArrangeOverride(finalSize);
    }
    
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        
        // Notify handler of property changes
        if (change.Property == SpinSpeedProperty ||
            change.Property == MoveSpeedProperty ||
            change.Property == ContrastProperty ||
            change.Property == LightingProperty ||
            change.Property == SpinAmountProperty ||
            change.Property == PixelFilterProperty ||
            change.Property == Color1Property ||
            change.Property == Color2Property ||
            change.Property == Color3Property)
        {
            _customVisual?.SendHandlerMessage(ShaderVisualHandler.InvalidateMessage);
        }
    }
}

/// <summary>
/// Visual handler for GPU-accelerated shader rendering
/// </summary>
internal sealed class ShaderVisualHandler : CompositionCustomVisualHandler, IDisposable
{
    public static readonly object StartAnimationMessage = new();
    public static readonly object StopAnimationMessage = new();
    public static readonly object InvalidateMessage = new();
    
    public class SizeChangedMessage
    {
        public Size Size { get; set; }
    }
    
    private readonly ShaderAnimationControl _control;
    private SKRuntimeEffect? _effect;
    private DateTime _startTime;
    private string? _shaderSource;
    private bool _disposed;
    private bool _animating;
    
    public ShaderVisualHandler(ShaderAnimationControl control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
        _startTime = DateTime.Now;
    }
    
    public override void OnMessage(object message)
    {
        if (_disposed) return;
        
        switch (message)
        {
            case var _ when ReferenceEquals(message, StartAnimationMessage):
                _animating = true;
                RegisterForNextAnimationFrameUpdate();
                break;
                
            case var _ when ReferenceEquals(message, StopAnimationMessage):
                _animating = false;
                break;
                
            case var _ when ReferenceEquals(message, InvalidateMessage):
                Invalidate();
                break;
                
            case SizeChangedMessage sizeMsg:
                SetSize(sizeMsg.Size);
                break;
        }
    }
    
    public override void OnRender(ImmediateDrawingContext context)
    {
        if (_disposed) return;
        
        // Lazy load and compile shader
        if (_effect == null)
        {
            if (_shaderSource == null)
            {
                _shaderSource = LoadShaderSource();
            }
            
            if (_shaderSource != null)
            {
                _effect = SKRuntimeEffect.CreateShader(_shaderSource, out var errorText);
                if (_effect == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Shader compilation failed: {errorText}");
                    DrawFallback(context);
                    return;
                }
            }
            else
            {
                DrawFallback(context);
                return;
            }
        }
        
        var bounds = GetRenderBounds();
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        
        var leaseFeature = context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) as ISkiaSharpApiLeaseFeature;
        if (leaseFeature == null)
        {
            DrawFallback(context);
            return;
        }
        
        using var lease = leaseFeature.Lease();
        var canvas = lease.SkCanvas;
        if (canvas == null)
        {
            DrawFallback(context);
            return;
        }
        
        // Calculate animation time
        var elapsed = (float)(DateTime.Now - _startTime).TotalSeconds;
        
        // Convert Avalonia colors to SkiaSharp
        var color1 = ToSKColor(_control.Color1);
        var color2 = ToSKColor(_control.Color2);
        var color3 = ToSKColor(_control.Color3);
        
        // Create shader uniforms
        using var uniformData = new SKRuntimeEffectUniforms(_effect)
        {
            ["iTime"] = elapsed,
            ["iResolution"] = new[] { (float)bounds.Width, (float)bounds.Height },
            ["spin_rotation_speed"] = (float)_control.SpinSpeed,
            ["move_speed"] = (float)_control.MoveSpeed,
            ["offset"] = new[] { 0f, 0f },
            ["colour_1"] = color1.ToShaderArray(),
            ["colour_2"] = color2.ToShaderArray(),
            ["colour_3"] = color3.ToShaderArray(),
            ["contrast"] = (float)_control.Contrast,
            ["lighting"] = (float)_control.Lighting,
            ["spin_amount"] = (float)_control.SpinAmount,
            ["pixel_filter"] = (float)_control.PixelFilter
        };
        
        // Create and apply shader
        using var shader = _effect.ToShader(uniformData);
        using var paint = new SKPaint
        {
            Shader = shader,
            IsAntialias = false // Keep pixelated look
        };
        
        // Draw the effect
        canvas.DrawRect(SKRect.Create((float)bounds.Width, (float)bounds.Height), paint);
    }
    
    public override void OnAnimationFrameUpdate()
    {
        if (!_disposed && _animating)
        {
            Invalidate();
            RegisterForNextAnimationFrameUpdate();
        }
    }
    
    private void DrawFallback(ImmediateDrawingContext context)
    {
        var bounds = GetRenderBounds();
        var leaseFeature = context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) as ISkiaSharpApiLeaseFeature;
        if (leaseFeature == null) return;
        
        using var lease = leaseFeature.Lease();
        var canvas = lease.SkCanvas;
        if (canvas == null) return;
        
        using var paint = new SKPaint();
        var colors = new[]
        {
            ToSKColor(_control.Color1),
            ToSKColor(_control.Color2),
            ToSKColor(_control.Color3)
        };
        
        paint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint((float)bounds.Width, (float)bounds.Height),
            colors,
            null,
            SKShaderTileMode.Clamp);
        
        canvas.DrawRect(SKRect.Create((float)bounds.Width, (float)bounds.Height), paint);
    }
    
    private string? LoadShaderSource()
    {
        try
        {
            // Try to load from embedded resource first
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "Motely.TheFool.Shaders.balatro_background.sksl";
            
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load embedded shader: {ex.Message}");
        }
        
        // Fall back to loading from file system
        try
        {
            var shaderPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "",
                "Shaders",
                "balatro_background.sksl");
            
            if (System.IO.File.Exists(shaderPath))
            {
                return System.IO.File.ReadAllText(shaderPath);
            }
            
            // Try alternative path
            shaderPath = System.IO.Path.Combine("Shaders", "balatro_background.sksl");
            if (System.IO.File.Exists(shaderPath))
            {
                return System.IO.File.ReadAllText(shaderPath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load shader from file: {ex.Message}");
        }
        
        return null;
    }
    
    private static SKColor ToSKColor(Color color)
    {
        return new SKColor(color.R, color.G, color.B, color.A);
    }
    
    private void SetSize(Size size)
    {
        // Handle size changes if needed
        Invalidate();
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        _animating = false;
        _effect?.Dispose();
        _effect = null;
    }
}