using System;

namespace Motely.TUI;

public class BalatroShaderBackground : View
{
    private double _time;
    private double _spinTime;
    private bool _isRunning;

    private Color[,]? _frameBuffer;
    private int _bufferWidth;
    private int _bufferHeight;

    private static readonly (int R, int G, int B) Color1 = (254, 95, 85); // #FE5F55 Red
    private static readonly (int R, int G, int B) Color2 = (0, 157, 255); // #009dff Blue
    private static readonly (int R, int G, int B) Color3 = (55, 66, 68); // #374244 Black

    private const double Contrast = 1.8;
    private const double SpinAmount = 0.6;
    private const double ZoomScale = 12.0;
    private const double SpinEase = 0.5;
    private const double PixelSize = 200.0;
    private const double ParallaxX = 0.05;
    private const int LoopCount = 5;

    public BalatroShaderBackground()
    {
        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();
        CanFocus = false;

        DrawingContent += (s, e) => DrawToScreen();
    }

    public Color GetColorAt(int screenX, int screenY)
    {
        if (_frameBuffer == null || screenY < 0 || screenY >= _bufferHeight)
            return new Color(Color3.R, Color3.G, Color3.B);

        if (screenX < 0 || screenX >= _bufferWidth)
            return new Color(Color3.R, Color3.G, Color3.B);

        return _frameBuffer[screenX, screenY];
    }

    public void Start()
    {
        if (_isRunning)
            return;
        _isRunning = true;

        // Calculate initial frame immediately
        UpdateFrameBuffer();

        MotelyTUI.App?.AddTimeout(
            TimeSpan.FromMilliseconds(83),
            () =>
            {
                if (!_isRunning)
                    return false;

                _time += 0.03;
                _spinTime += 0.02;
                UpdateFrameBuffer();
                SetNeedsDraw();
                return true;
            }
        );
    }

    public void Stop() => _isRunning = false;

    private void UpdateFrameBuffer()
    {
        try
        {
            int screenWidth = MotelyTUI.App?.Driver?.Cols ?? 80;
            int screenHeight = MotelyTUI.App?.Driver?.Rows ?? 24;

            if (
                _frameBuffer == null
                || _bufferWidth != screenWidth
                || _bufferHeight != screenHeight
            )
            {
                _frameBuffer = new Color[screenWidth, screenHeight];
                _bufferWidth = screenWidth;
                _bufferHeight = screenHeight;
            }

            double resolution = Math.Sqrt(screenWidth * screenWidth + screenHeight * screenHeight);
            CalculateFrame(screenWidth, screenHeight, resolution);
        }
        catch { }
    }

    public void DrawToScreen()
    {
        if (_frameBuffer == null || MotelyTUI.App?.Driver == null)
            return;

        var driver = MotelyTUI.App.Driver;
        var block = new System.Text.Rune('█');
        int screenWidth = Math.Min(driver.Cols, _bufferWidth);
        int screenHeight = Math.Min(driver.Rows, _bufferHeight);

        for (int y = 0; y < screenHeight; y++)
        {
            for (int x = 0; x < screenWidth; x++)
            {
                var color = _frameBuffer[x, y];
                driver.SetAttribute(new Attribute(color, color));
                driver.Move(x, y);
                driver.AddRune(block);
            }
        }
    }

    private void CalculateFrame(int width, int height, double resolution)
    {
        double time = _time;
        double spinTime = _spinTime;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                _frameBuffer![x, y] = CalculatePixel(
                    x,
                    y,
                    width,
                    height,
                    resolution,
                    time,
                    spinTime
                );
            }
        }
    }

    private static Color CalculatePixel(
        int x,
        int y,
        int width,
        int height,
        double resolution,
        double time,
        double spinTime
    )
    {
        double pixSize = resolution / PixelSize;

        // Terminal chars are ~2x taller than wide, stretch X to compensate
        double uvX =
            ((Math.Floor(x / pixSize) * pixSize - 0.5 * width) / resolution - ParallaxX) * 0.6;
        double uvY = (Math.Floor(y / pixSize) * pixSize - 0.5 * height) / resolution;
        double uvLen = Math.Sqrt(uvX * uvX + uvY * uvY);

        double speed = spinTime * SpinEase * 0.2 + 302.2;
        double newAngle =
            Math.Atan2(uvY, uvX)
            + speed
            - SpinEase * 20.0 * (SpinAmount * uvLen + (1.0 - SpinAmount));

        double midX = (width / resolution) / 2.0;
        double midY = (height / resolution) / 2.0;

        uvX = uvLen * Math.Cos(newAngle) + midX - midX;
        uvY = uvLen * Math.Sin(newAngle) + midY - midY;

        uvX *= 30.0 + ZoomScale;
        uvY *= 30.0 + ZoomScale;

        double animSpeed = time * 2.0;
        double uv2X = uvX + uvY;
        double uv2Y = uvX + uvY;

        for (int i = 0; i < LoopCount; i++)
        {
            double maxUv = Math.Max(uvX, uvY);
            uv2X += Math.Sin(maxUv) + uvX;
            uv2Y += Math.Sin(maxUv) + uvY;
            uvX += 0.5 * Math.Cos(5.1123314 + 0.353 * uv2Y + animSpeed * 0.131121);
            uvY += 0.5 * Math.Sin(uv2X - 0.113 * animSpeed);
            double cosVal = Math.Cos(uvX + uvY);
            double sinVal = Math.Sin(uvX * 0.711 - uvY);
            uvX -= cosVal - sinVal;
            uvY -= cosVal - sinVal;
        }

        double contrastMod = 0.25 * Contrast + 0.5 * SpinAmount + 1.2;
        double paintRes = Math.Sqrt(uvX * uvX + uvY * uvY) * 0.035 * contrastMod;
        paintRes = Math.Clamp(paintRes, 0.0, 2.0);

        double c1p = Math.Max(0.0, 1.0 - contrastMod * Math.Abs(1.0 - paintRes));
        double c2p = Math.Max(0.0, 1.0 - contrastMod * Math.Abs(paintRes));
        double c3p = 1.0 - Math.Min(1.0, c1p + c2p);

        double cf = 0.3 / Contrast;
        double ncf = 1.0 - cf;

        int r = (int)(cf * Color1.R + ncf * (Color1.R * c1p + Color2.R * c2p + Color3.R * c3p));
        int g = (int)(cf * Color1.G + ncf * (Color1.G * c1p + Color2.G * c2p + Color3.G * c3p));
        int b = (int)(cf * Color1.B + ncf * (Color1.B * c1p + Color2.B * c2p + Color3.B * c3p));

        return new Color(Math.Clamp(r, 0, 255), Math.Clamp(g, 0, 255), Math.Clamp(b, 0, 255));
    }
}
