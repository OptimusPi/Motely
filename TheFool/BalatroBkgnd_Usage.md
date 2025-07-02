# BalatroBkgnd Usage Examples

## Basic Usage

Simply add the control to your XAML:

```xml
<controls:BalatroBkgnd />
```

## Custom Colors (SolioJazz Theme)

```xml
<controls:BalatroBkgnd 
    Color1="#DE443B"
    Color2="#006BB4"
    Color3="#162325" />
```

## Adjusting Animation Speed

```xml
<controls:BalatroBkgnd 
    SpinSpeed="3.0"
    MoveSpeed="10.0" />
```

## Fine-Tuning Visual Parameters

```xml
<controls:BalatroBkgnd 
    SpinSpeed="2.0"
    MoveSpeed="7.0"
    Contrast="4.0"
    Lighting="0.6"
    SpinAmount="0.35"
    PixelFilter="600" />
```

## Preset Configurations

### Calm and Hypnotic
```xml
<controls:BalatroBkgnd 
    SpinSpeed="1.5"
    MoveSpeed="5.0"
    Contrast="3.0"
    Lighting="0.3"
    SpinAmount="0.2" />
```

### Fast and Energetic
```xml
<controls:BalatroBkgnd 
    SpinSpeed="4.0"
    MoveSpeed="12.0"
    Contrast="4.5"
    Lighting="0.5"
    SpinAmount="0.4" />
```

### Classic Balatro
```xml
<controls:BalatroBkgnd 
    SpinSpeed="2.0"
    MoveSpeed="7.0"
    Contrast="3.5"
    Lighting="0.4"
    SpinAmount="0.25"
    PixelFilter="740"
    Color1="#DE443B"
    Color2="#006BB4"
    Color3="#162325" />
```

## Troubleshooting

1. If the shader doesn't compile, check the console output for error messages
2. The control will fall back to a gradient if the shader fails to load
3. Make sure the shader file is included as an embedded resource
4. For best performance, use on devices with GPU acceleration

## Parameter Reference

- **SpinSpeed** (0-10): Controls rotation speed of the spiral
- **MoveSpeed** (0-20): Controls flow speed of the paint effect
- **Contrast** (0-10): Sharpness of boundaries between colors
- **Lighting** (0-1): Intensity of highlight effects
- **SpinAmount** (0-1): How much the spiral varies with distance
- **PixelFilter** (100-1000): Pixelation level (lower = bigger pixels)
