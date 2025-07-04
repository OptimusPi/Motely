using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;
using System;

namespace TheFool.Controls
{
    public class BalatroStyleBackground : Control
    {
        public override void Render(DrawingContext context)
        {
            context.Custom(new BalatroStyleBackgroundDrawOp(new Rect(0, 0, Bounds.Width, Bounds.Height)));
            Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Render);
        }
    }

    internal class BalatroStyleBackgroundDrawOp : ICustomDrawOperation
    {
        private SKRuntimeShaderBuilder? _shaderBuilder;

        public BalatroStyleBackgroundDrawOp(Rect bounds)
        {
            Bounds = bounds;
            InitShader();
        }

        private void InitShader()
        {
            var sksl = @"
                uniform float2 resolution;
                uniform float time;
                uniform float spin_rotation;
                uniform float spin_speed;
                uniform float2 offset;
                uniform float4 colour_1;
                uniform float4 colour_2;
                uniform float4 colour_3;
                uniform float contrast;
                uniform float lighting;
                uniform float spin_amount;
                uniform float pixel_filter;
                uniform float polar_coordinates;
                uniform float2 polar_center;
                uniform float polar_zoom;
                uniform float polar_repeat;

                float2 polar_coords(float2 uv, float2 center, float zoom, float repeat) {
                    float2 dir = uv - center;
                    float radius = length(dir) * 2.0;
                    float angle = atan(dir.y, dir.x) * 1.0 / (3.1415926 * 2.0);
                    return fract(float2(radius * zoom, angle * repeat));
                }

                float4 main(float2 coord) {
                    float2 uv_norm = coord / resolution;
                    if (polar_coordinates > 0.5) {
                        uv_norm = polar_coords(uv_norm, polar_center, polar_zoom, polar_repeat);
                    }
                    
                    float2 screenSize = resolution;
                    float pixel_size = length(screenSize) / pixel_filter;
                    float2 uv = (floor(coord * (1.0 / pixel_size)) * pixel_size - 0.5 * screenSize) / length(screenSize) - offset;
                    float uv_len = length(uv);

                    float speed = (spin_rotation * 0.2) + 302.2;
                    float new_pixel_angle = atan(uv.y, uv.x) + speed - 20.0 * (1.0 * spin_amount * uv_len + (1.0 - 1.0 * spin_amount));
                    float2 mid = (screenSize / length(screenSize)) / 2.0;
                    uv = float2(uv_len * cos(new_pixel_angle) + mid.x, uv_len * sin(new_pixel_angle) + mid.y) - mid;

                    uv *= 30.0;
                    speed = time * spin_speed;
                    float2 uv2 = float2(uv.x + uv.y);

                    for (int i = 0; i < 6; i++) {
                        uv2 += sin(max(uv.x, uv.y)) + uv;
                        uv += 0.5 * float2(cos(5.1123314 + 0.353 * uv2.y + speed * 0.131121), sin(uv2.x - 0.113 * speed));
                        uv -= 1.0 * cos(uv.x + uv.y) - 1.0 * sin(uv.x * 0.711 - uv.y);
                    }

                    float contrast_mod = (0.25 * contrast + 0.5 * spin_amount + 1.2);
                    float paint_res = min(2.0, max(0.0, length(uv) * 0.035 * contrast_mod));
                    float c1p = max(0.0, 1.0 - contrast_mod * abs(1.0 - paint_res));
                    float c2p = max(0.0, 1.0 - contrast_mod * abs(paint_res));
                    float c3p = 1.0 - min(1.0, c1p + c2p);

                    float ligth = (lighting - 0.2) * max(c1p * 5.0 - 4.0, 0.0) + lighting * max(c2p * 5.0 - 4.0, 0.0);
                    float4 ret_col = (0.3 / contrast) * colour_1 + (1.0 - 0.3 / contrast) * (colour_1 * c1p + colour_2 * c2p + float4(c3p * colour_3.rgb, c3p * colour_1.a)) + ligth;

                    return ret_col;
                }
";

            var effect = SKRuntimeEffect.CreateShader(sksl, out var errorStr);
            if (effect != null && string.IsNullOrEmpty(errorStr))
            {
                _shaderBuilder = new SKRuntimeShaderBuilder(effect);
                
                // Set Balatro shader parameters from GitHub configuration
                _shaderBuilder.Uniforms["spin_rotation"] = -2.0f;
                _shaderBuilder.Uniforms["spin_speed"] = 5.0f;
                _shaderBuilder.Uniforms["offset"] = new SKPoint(0.0f, 0.0f);
                _shaderBuilder.Uniforms["colour_1"] = new float[] { 0.871f, 0.267f, 0.231f, 1.0f }; // Red-orange
                _shaderBuilder.Uniforms["colour_2"] = new float[] { 0.0f, 0.42f, 0.706f, 1.0f }; // Blue
                _shaderBuilder.Uniforms["colour_3"] = new float[] { 0.086f, 0.137f, 0.145f, 1.0f }; // Dark gray-green
                _shaderBuilder.Uniforms["contrast"] = 3.5f;
                _shaderBuilder.Uniforms["lighting"] = 0.4f;
                _shaderBuilder.Uniforms["spin_amount"] = 0.2f;
                _shaderBuilder.Uniforms["pixel_filter"] = 745.0f;
                _shaderBuilder.Uniforms["polar_coordinates"] = 0f; // Disabled by default
                _shaderBuilder.Uniforms["polar_center"] = new SKPoint(0.5f, 0.5f);
                _shaderBuilder.Uniforms["polar_zoom"] = 1f;
                _shaderBuilder.Uniforms["polar_repeat"] = 5f;
            }
            else {
                Console.WriteLine($"Error creating shader: {errorStr}");
            }
        }

        public Rect Bounds { get; }

        public void Dispose()
        {

        }

        public bool Equals(ICustomDrawOperation? other) => false;

        public bool HitTest(Point p) => false;

        public void Render(ImmediateDrawingContext context)
        {
            if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is ISkiaSharpApiLeaseFeature leaseFeature)
            {
                using var lease = leaseFeature.Lease();
                var canvas = lease.SkCanvas;
                canvas.Save();

                if (_shaderBuilder != null)
                {
                    // Update time-based uniforms
                    var time = Environment.TickCount / 1000.0f;
                    _shaderBuilder.Uniforms["time"] = time;
                    _shaderBuilder.Uniforms["resolution"] = new SKSize((float)Bounds.Width, (float)Bounds.Height);
                    _shaderBuilder.Uniforms["spin_rotation"] = time * 0.5f; // Animate spin rotation
                    
                    // Create the shader from the builder
                    var shader = _shaderBuilder.Build();
                    
                    using var paint = new SKPaint
                    {
                        Shader = shader,
                    };
                    
                    // Fill the entire bounds with the shader
                    var rect = new SKRect(0, 0, (float)Bounds.Width, (float)Bounds.Height);
                    canvas.DrawRect(rect, paint);
                }

                canvas.Restore();
            }
        }
    }
}