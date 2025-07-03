using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Motely.TheFool.Controls;

/// <summary>
/// GPU-accelerated Balatro background using hardware shaders
/// This is a high-performance replacement for the CPU-based BalatroBkgnd
/// </summary>
public class BalatroBkgndGPU : ShaderAnimationControl
{
    static BalatroBkgndGPU()
    {
        // Set default Balatro-style properties
        SpinSpeedProperty.OverrideDefaultValue<BalatroBkgndGPU>(2.0);
        MoveSpeedProperty.OverrideDefaultValue<BalatroBkgndGPU>(7.0);
        ContrastProperty.OverrideDefaultValue<BalatroBkgndGPU>(3.5);
        LightingProperty.OverrideDefaultValue<BalatroBkgndGPU>(1.0);
        SpinAmountProperty.OverrideDefaultValue<BalatroBkgndGPU>(0.2);
        PixelFilterProperty.OverrideDefaultValue<BalatroBkgndGPU>(60.0);
        
        // Balatro colors
        Color1Property.OverrideDefaultValue<BalatroBkgndGPU>(Color.FromRgb(222, 68, 59));   // Red
        Color2Property.OverrideDefaultValue<BalatroBkgndGPU>(Color.FromRgb(0, 107, 180));   // Blue  
        Color3Property.OverrideDefaultValue<BalatroBkgndGPU>(Color.FromRgb(22, 35, 37));    // Dark background
    }
    
    public BalatroBkgndGPU()
    {
        // Initialize with Balatro-specific settings
        SpinSpeed = 2.0;
        MoveSpeed = 7.0;
        Contrast = 3.5;
        Lighting = 1.0;
        SpinAmount = 0.2;
        PixelFilter = 60.0;
        
        // Set Balatro colors
        Color1 = Color.FromRgb(222, 68, 59);   // Red
        Color2 = Color.FromRgb(0, 107, 180);   // Blue  
        Color3 = Color.FromRgb(22, 35, 37);    // Dark background
    }
}