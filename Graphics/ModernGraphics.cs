using System.IO;
using System.Linq;
using System.Numerics;
using CS2GameHelper.Core;
using CS2GameHelper.Data.Game;
using CS2GameHelper.Features;
using CS2GameHelper.Utils;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SkiaSharp;
using System.Threading;
using System.Windows.Forms;

namespace CS2GameHelper.Graphics;

public class ModernGraphics : ThreadedServiceBase
{
    private readonly GameProcess _gameProcess;
    private readonly GameData _gameData;
    private IWindow? _window;
    private GL? _gl;
    private SKSurface? _surface;
    private SKCanvas? _canvas;
    private SKPaint? _paint;
    private SKTypeface? _defaultFont;
    private SKTypeface? _undefeatedFont;
    private SKTypeface? _emojiFont;

    private GRGlInterface? _grInterface;
    private GRContext? _grContext;
    private GRBackendRenderTarget? _renderTarget;

    private readonly FpsCounter _fpsCounter = new();
    private readonly List<DrawCommand> _drawCommands = new();
    private readonly object _renderLock = new();
    private bool _overlayVisible = true;
    private bool _lastF11 = false;
    private DateTime _autoHideUntil = DateTime.MinValue;
    
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = unchecked((int)0x08000000);
    private const uint LWA_ALPHA = 0x02;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private static readonly IntPtr HWND_NOTOPMOST = new(-2);

    public ModernGraphics(GameProcess gameProcess, GameData gameData)
    {
        _gameProcess = gameProcess ?? throw new ArgumentNullException(nameof(gameProcess));
        _gameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
    }

    protected override string ThreadName => nameof(ModernGraphics);

    public GameProcess GameProcess => _gameProcess;
    public GameData GameData => _gameData;

    // Programmatically show/hide overlay window (useful to hide for recording)
    public void SetOverlayVisible(bool visible)
    {
        if (_window?.Native?.Win32 is not { Hwnd: { } hwnd }) return;

        try
        {
            User32.ShowWindow(hwnd, visible ? 4 : 0); // 4 = SW_SHOWNOACTIVATE, 0 = SW_HIDE
        }
        catch
        {
            // ignore
        }
    }

    private void EnsureInitialized()
    {
        if (_window != null)
        {
            return;
        }

        InitializeGraphics();
        InitializeFonts();
    }

    private void InitializeGraphics()
    {
        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(800, 600);
        options.Title = "CS2 Overlay";
        options.WindowBorder = WindowBorder.Hidden;
        options.IsVisible = true;
        options.TopMost = true;
        options.TransparentFramebuffer = true;
        options.VSync = false;
        
        _window = Window.Create(options);
        _window.Load += OnWindowLoad;
        _window.Render += OnWindowRender;
        _window.Resize += OnWindowResize;
        _window.Initialize();
    }

    private void InitializeFonts()
    {
        _defaultFont = SKTypeface.FromFamilyName("Arial");
        
        try
        {
            var fontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "fonts", "undefeated.ttf");
            if (File.Exists(fontPath))
            {
                _undefeatedFont = SKTypeface.FromFile(fontPath);
            }
        }
        catch
        {
            _undefeatedFont = _defaultFont;
        }
        // Load emoji font from assets/fonts/emoji.ttf (preferred). Fall back to default font.
        try
        {
            var emojiPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "fonts", "emoji.ttf");
            if (File.Exists(emojiPath))
            {
                _emojiFont = SKTypeface.FromFile(emojiPath);
            }
            else
            {
                _emojiFont = _defaultFont;
            }
        }
        catch
        {
            _emojiFont = _defaultFont;
        }
        
        _paint = new SKPaint
        {
            IsAntialias = true,
            TextSize = 12,
            Typeface = _defaultFont,
            TextEncoding = SKTextEncoding.Utf16
        };
    }

    /// <summary>
    /// True when the custom 'undefeated' font was successfully loaded.
    /// </summary>
    public bool IsUndefeatedFontLoaded => _undefeatedFont != null && _undefeatedFont != _defaultFont;
    /// <summary>
    /// True when an emoji-capable font was successfully loaded.
    /// </summary>
    public bool IsEmojiFontLoaded => _emojiFont != null && _emojiFont != _defaultFont;

    private void OnWindowLoad()
    {
        var window = _window ?? throw new InvalidOperationException("Window has not been initialized.");

        _gl = window.CreateOpenGL();
        _gl.ClearColor(0f, 0f, 0f, 0f);
        _gl.Enable(GLEnum.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.Viewport(0, 0, (uint)window.Size.X, (uint)window.Size.Y);

        ApplyWindowStyles();

        _grInterface = GRGlInterface.Create();

        if (_grInterface == null || !_grInterface.Validate())
        {
            throw new InvalidOperationException("Unable to create GRGlInterface.");
        }

        _grContext = GRContext.CreateGl(_grInterface);

        RecreateSurface(window.Size.X, window.Size.Y);
    }


    private void OnWindowRender(double deltaTime)
    {
        if (_gl == null || _surface == null || _canvas == null)
        {
            return;
        }

        _gl.Clear(ClearBufferMask.ColorBufferBit);

        lock (_renderLock)
        {
            _canvas.Clear(SKColors.Transparent);

            foreach (var command in _drawCommands)
            {
                ExecuteDrawCommand(command);
            }

            _drawCommands.Clear();
        }

        _surface.Flush();
        _grContext?.Flush();
        _gl.Flush();
    }


    private void OnWindowResize(Vector2D<int> newSize)
    {
        _gl?.Viewport(0, 0, (uint)newSize.X, (uint)newSize.Y);
        RecreateSurface(newSize.X, newSize.Y);
    }


    private void RecreateSurface(int width, int height)
    {
        _surface?.Dispose();
        _surface = null;
        _canvas = null;

        _renderTarget?.Dispose();
        _renderTarget = null;

        if (_grContext != null)
        {
            var framebufferInfo = new GRGlFramebufferInfo(0, (uint)GLEnum.Rgba8);
            _renderTarget = new GRBackendRenderTarget(width, height, 0, 8, framebufferInfo);
            _surface = SKSurface.Create(_grContext, _renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
        }
        else
        {
            var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            _surface = SKSurface.Create(info);
        }

        _canvas = _surface?.Canvas;
    }


    private void ApplyWindowStyles()
    {
        if (_window?.Native?.Win32 is not { Hwnd: { } hwnd } || hwnd == IntPtr.Zero)
        {
            return;
        }

        var exStyle = User32.GetWindowLong(hwnd, GWL_EXSTYLE);
        // Make the overlay click-through and non-activatable so it doesn't steal keyboard focus
        exStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        User32.SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
        User32.SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);
        // Do not force topmost here; UpdateWindowPosition will set topmost only when the game is foreground

        // (capture-exclusion removed - default behavior)
    }

    protected override void FrameAction()
    {
        EnsureInitialized();
        _window?.DoEvents();

        var hasWindow = _gameProcess.HasWindow;
        var isValid = _gameProcess.IsValid;

        if (hasWindow)
        {
            UpdateWindowPosition();
        }

        // Hotkey F11 toggles overlay visibility
        var f11Down = (User32.GetAsyncKeyState((int)Keys.F11) & 0x8000) != 0;
        if (f11Down && !_lastF11)
        {
            _overlayVisible = !_overlayVisible;
            SetOverlayVisible(_overlayVisible);
        }
        _lastF11 = f11Down;

        // Auto-hide on Alt+Z (NVIDIA overlay): hide briefly so the overlay UI can receive input
        var altDown = (User32.GetAsyncKeyState((int)Keys.Menu) & 0x8000) != 0;
        var zDown = (User32.GetAsyncKeyState((int)Keys.Z) & 0x8000) != 0;
        if (altDown && zDown)
        {
            _autoHideUntil = DateTime.UtcNow.AddMilliseconds(1000);
            SetOverlayVisible(false);
        }

        if (_autoHideUntil > DateTime.UtcNow)
        {
            // keep hidden until timer expires
        }
        else if (!_overlayVisible)
        {
            // restore if previously toggled visible
            SetOverlayVisible(true);
            _overlayVisible = true;
        }

        RenderFrame(isValid, hasWindow);
    }

    private void UpdateWindowPosition()
    {
        if (_window == null) return;
        
    var gameRect = _gameProcess.WindowRectangleClient;
        if (gameRect.Width <= 0 || gameRect.Height <= 0)
            return;

        // If the game is running fullscreen (approx by comparing to primary screen resolution),
        // hide overlays to avoid DirectX/OpenGL composition conflicts that can lead to a black screen.
        try
        {
            _window.Position = new Vector2D<int>(gameRect.X, gameRect.Y);
            _window.Size = new Vector2D<int>(gameRect.Width, gameRect.Height);

            // Only force topmost when the game window is active/foreground so overlay doesn't float above other apps
            if (_gameProcess.IsWindowActive)
            {
                if (_window.Native?.Win32 is { Hwnd: { } hwnd })
                    User32.SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
            else
            {
                if (_window.Native?.Win32 is { Hwnd: { } hwnd })
                    User32.SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
            // Ensure cursor isn't clipped by any overlay windows
            try { User32.ClipCursor(IntPtr.Zero); } catch { }
        }
        catch
        {
            // ignore positioning failures
        }
    }

    private void RenderFrame(bool isValid, bool hasWindow)
    {
        lock (_renderLock)
        {
            _drawCommands.Clear();
            
            _fpsCounter.Update();
            AddHudText($"{Math.Max(_fpsCounter.Fps, 1)} FPS", 5, 20, SKColors.White, 16);
            
            var player = _gameData.Player;
            var entities = _gameData.Entities;

            if (isValid)
            {
                var config = ConfigManager.Load();

                if (config.Esp.Box.Enabled)
                {
                    EspBox.Draw(this);
                }

                if (config.Esp.Radar.Enabled) 
                { 
                    Radar.Draw(this); 
                }

                if (config.EspAimCrosshair)
                {
                    EspAimCrosshair.Draw(this);
                }

                if (config.SkeletonEsp)
                {
                    SkeletonEsp.Draw(this);
                }

                if (config.BombTimer)
                {
                    BombTimer.Draw(this);
                }

                if (config.SpectatorList)
                {
                    SpectatorList.Draw(this);
                }
            }
            else if (hasWindow)
            {
                AddText("Waiting for game data...", 5, 40, SKColors.Orange, 14);
            }
            else
            {
                AddText("Waiting for Counter-Strike 2 window...", 5, 40, SKColors.Orange, 14);
            }
            
            AddHudText($"Player: {(player != null ? ((nuint)player.AddressBase).ToString("X") : "null")}", 5, 75, SKColors.LightGray, 12);
            var aliveCount = entities?.Count(e => e.IsAlive()) ?? 0;
            AddHudText($"Entities alive: {aliveCount}", 5, 90, SKColors.LightGray, 12);

            // Overlay status indicator
            var status = _overlayVisible ? "Overlay: ON" : "Overlay: OFF";
            if (_autoHideUntil > DateTime.UtcNow)
            {
                var ms = (_autoHideUntil - DateTime.UtcNow).TotalMilliseconds;
                status += $" (auto-hide {Math.Max(0, (int)ms)} ms)";
            }
            AddHudText(status, 5, 110, SKColors.Yellow, 12);
        }
        
        _window?.DoRender();
    }

    private void AddHudText(string text, float x, float y, SKColor color, float fontSize = 12, bool useCustomFont = false)
    {
        // HUD text is drawn on the main overlay canvas to simplify rendering and avoid multi-window issues.
        AddText(text, x, y, color, fontSize, useCustomFont);
    }


    // Drawing command methods
    public void AddLine(Vector2 start, Vector2 end, SKColor color)
    {
        lock (_renderLock)
        {
            _drawCommands.Add(new DrawCommand
            {
                Type = DrawCommandType.Line,
                Start = start,
                End = end,
                Color = color
            });
        }
    }

    public void AddRectangle(Vector2 topLeft, Vector2 bottomRight, SKColor color)
    {
        lock (_renderLock)
        {
            _drawCommands.Add(new DrawCommand
            {
                Type = DrawCommandType.Rectangle,
                Start = topLeft,
                End = bottomRight,
                Color = color,
                Filled = false
            });
        }
    }

    public void AddRectangleFilled(Vector2 topLeft, Vector2 bottomRight, SKColor color)
    {
        lock (_renderLock)
        {
            _drawCommands.Add(new DrawCommand
            {
                Type = DrawCommandType.Rectangle,
                Start = topLeft,
                End = bottomRight,
                Color = color,
                Filled = true
            });
        }
    }

    public void AddText(string text, float x, float y, SKColor color, float fontSize = 12, bool useCustomFont = false)
    {
        lock (_renderLock)
        {
            _drawCommands.Add(new DrawCommand
            {
                Type = DrawCommandType.Text,
                Text = text,
                Start = new Vector2(x, y),
                Color = color,
                FontSize = fontSize,
                UseCustomFont = useCustomFont
            });
        }
    }

    public void DrawLine(uint color, Vector2 start, Vector2 end)
    {
        AddLine(start, end, ConvertColor(color));
    }

    public void DrawRect(float x, float y, float width, float height, uint color)
    {
        var topLeft = new Vector2(x, y);
        var bottomRight = new Vector2(x + width, y + height);
        AddRectangleFilled(topLeft, bottomRight, ConvertColor(color));
    }

    public void DrawRectOutline(float x, float y, float width, float height, uint color)
    {
        var topLeft = new Vector2(x, y);
        var bottomRight = new Vector2(x + width, y + height);
        AddRectangle(topLeft, bottomRight, ConvertColor(color));
    }

    public void DrawText(string text, float x, float y, uint color, float fontSize = 12, bool useCustomFont = false)
    {
        AddText(text, x, y, ConvertColor(color), fontSize, useCustomFont);
    }

    /// <summary>
    /// Measure text using Skia paint and return width/height as a Vector2 (X=width, Y=height).
    /// This is a lightweight helper used by features to layout text before drawing.
    /// </summary>
    public Vector2 MeasureText(string text, float fontSize = 12, bool useCustomFont = false)
    {
        if (string.IsNullOrEmpty(text)) return Vector2.Zero;

        // If the text contains emoji, prefer the emoji font when available.
        var typeface = _defaultFont;
        if (ContainsEmoji(text) && _emojiFont != null)
        {
            typeface = _emojiFont;
        }
        else
        {
            typeface = useCustomFont && _undefeatedFont != null ? _undefeatedFont : _defaultFont;
        }

        using var paint = new SKPaint
        {
            IsAntialias = true,
            TextSize = fontSize,
            Typeface = typeface,
            TextEncoding = SKTextEncoding.Utf16,
            TextAlign = SKTextAlign.Left,
            Style = SKPaintStyle.Fill
        };

        float width;
        try
        {
            width = paint.MeasureText(text);
        }
        catch
        {
            width = fontSize * text.Length * 0.5f;
        }

        float ascent;
        float descent;
        try
        {
            var metrics = paint.FontMetrics;
            ascent = Math.Abs(metrics.Ascent);
            descent = Math.Abs(metrics.Descent);
        }
        catch
        {
            ascent = fontSize;
            descent = fontSize * 0.2f;
        }

        var height = ascent + descent;
        return new Vector2(width, height);
    }

    // Heuristic to detect emoji/codepoints outside BMP that likely require an emoji-capable font.
    private static bool ContainsEmoji(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        for (int i = 0; i < text.Length; i++)
        {
            int cp = char.ConvertToUtf32(text, i);
            // Skip the low surrogate when we consumed a surrogate pair
            if (char.IsHighSurrogate(text[i])) i++;

            // Broad ranges that contain emoji and symbols
            if ((cp >= 0x1F300 && cp <= 0x1FAFF) || // Misc Symbols and Pictographs / Emoji
                (cp >= 0x1F600 && cp <= 0x1F64F) || // Emoticons
                (cp >= 0x2600 && cp <= 0x26FF) ||   // Misc symbols
                (cp >= 0x2700 && cp <= 0x27BF))     // Dingbats
            {
                return true;
            }
        }

        return false;
    }

    


    public void DrawLineWorld(uint color, Vector3 start, Vector3 end)
    {
        var player = _gameData.Player;
        var matrix = player?.MatrixViewProjectionViewport;
        if (player == null || matrix == null)
        {
            return;
        }

        var startProjected = matrix.Value.Transform(start);
        var endProjected = matrix.Value.Transform(end);

        if (startProjected.Z >= 1 || endProjected.Z >= 1)
        {
            return;
        }

        DrawLine(color, new Vector2(startProjected.X, startProjected.Y), new Vector2(endProjected.X, endProjected.Y));
    }

    private static SKColor ConvertColor(uint color)
    {
        var a = (byte)((color >> 24) & 0xFF);
        var r = (byte)((color >> 16) & 0xFF);
        var g = (byte)((color >> 8) & 0xFF);
        var b = (byte)(color & 0xFF);
        return new SKColor(r, g, b, a);
    }

    private void ExecuteDrawCommand(DrawCommand command)
    {
        if (_canvas == null || _paint == null) return;
        ExecuteDrawCommand(command, _canvas, _paint, false);
    }

    public void DrawCircle(float cx, float cy, float radius, uint color)
    {
        lock (_renderLock)
        {
            _drawCommands.Add(new DrawCommand
            {
                Type = DrawCommandType.Circle,
                Start = new Vector2(cx, cy),
                Radius = radius,
                Color = ConvertColor(color)
            });
        }
    }

    public void DrawCircleOutline(float cx, float cy, float radius, uint color)
    {
        lock (_renderLock)
        {
            _drawCommands.Add(new DrawCommand
            {
                Type = DrawCommandType.CircleOutline,
                Start = new Vector2(cx, cy),
                Radius = radius,
                Color = ConvertColor(color)
            });
        }
    }

    public void DrawCircleFilled(float cx, float cy, float radius, uint color)
    {
        lock (_renderLock)
        {
            _drawCommands.Add(new DrawCommand
            {
                Type = DrawCommandType.CircleFilled,
                Start = new Vector2(cx, cy),
                Radius = radius,
                Color = ConvertColor(color)
            });
        }
    }

    private void ExecuteDrawCommand(DrawCommand command, SKCanvas? canvas, SKPaint? paint, bool isHud)
    {
        if (canvas == null || paint == null) return;

        paint.Color = command.Color;

        switch (command.Type)
        {
            case DrawCommandType.Circle:
            case DrawCommandType.CircleFilled:
            case DrawCommandType.CircleOutline:
            {
                var path = new SKPath();
                path.AddCircle(command.Start.X, command.Start.Y, command.Radius);

                // Configure paint style depending on the draw type.
                if (command.Type == DrawCommandType.CircleFilled)
                {
                    paint.Style = SKPaintStyle.Fill;
                }
                else
                {
                    paint.Style = SKPaintStyle.Stroke;
                    // Use a thinner stroke for explicit "outline" requests, otherwise a default stroke width.
                    paint.StrokeWidth = command.Type == DrawCommandType.CircleOutline ? 1f : 2f;
                }

                canvas.DrawPath(path, paint);
                path.Dispose();
                break;
            }


            case DrawCommandType.Line:
                canvas.DrawLine(command.Start.X, command.Start.Y, command.End.X, command.End.Y, paint);
                break;

            case DrawCommandType.Rectangle:
                var rect = SKRect.Create(command.Start.X, command.Start.Y,
                    command.End.X - command.Start.X, command.End.Y - command.Start.Y);
                paint.Style = command.Filled ? SKPaintStyle.Fill : SKPaintStyle.Stroke;
                canvas.DrawRect(rect, paint);
                break;

            case DrawCommandType.Text:
                paint.Style = SKPaintStyle.Fill;
                paint.TextSize = command.FontSize;
                // Always use UTF-16 encoding for .NET strings
                paint.TextEncoding = SKTextEncoding.Utf16;
                paint.TextAlign = SKTextAlign.Center;
                // Choose an appropriate typeface. If the text contains emoji, prefer the emoji font.
                var typeface = _defaultFont;
                if (ContainsEmoji(command.Text) && _emojiFont != null)
                {
                    typeface = _emojiFont;
                }
                else
                {
                    typeface = command.UseCustomFont && _undefeatedFont != null ? _undefeatedFont : _defaultFont;
                }
                paint.Typeface = typeface;

                // If the chosen typeface cannot render the glyph, fall back to default font
                try
                {
                    if (typeface != null && typeface != _defaultFont)
                    {
                        // Use SKFont to check glyphs availability
                        var skFont = new SKFont(typeface, command.FontSize);
                        var chars = command.Text.ToCharArray();
                        var glyphBuffer = new ushort[chars.Length];
                        skFont.GetGlyphs(chars, glyphBuffer);
                        var count = chars.Length;
                        var hasAny = false;
                        for (var gi = 0; gi < count; gi++) if (glyphBuffer[gi] != 0) { hasAny = true; break; }
                        if (!hasAny)
                        {
                            paint.Typeface = _defaultFont;
                        }
                    }
                }
                catch
                {
                    // ignore and draw with current typeface
                }

                // Clamp text position to canvas/window bounds to avoid drawing off-screen
                float canvasWidth = canvas.LocalClipBounds.Width;
                float canvasHeight = canvas.LocalClipBounds.Height;
                if (canvasWidth <= 0 || canvasHeight <= 0)
                {
                    if (_window != null)
                    {
                        canvasWidth = _window.Size.X;
                        canvasHeight = _window.Size.Y;
                    }
                }

                const float padding = 8f;
                var x = command.Start.X;
                var y = command.Start.Y + command.FontSize / 2f;

                float textWidth;
                try
                {
                    textWidth = paint.MeasureText(command.Text);
                }
                catch
                {
                    // fallback estimate
                    textWidth = command.FontSize * command.Text.Length * 0.5f;
                }

                float ascent;
                float descent;
                try
                {
                    var metrics = paint.FontMetrics;
                    ascent = metrics.Ascent;
                    descent = metrics.Descent;
                }
                catch
                {
                    ascent = -command.FontSize;
                    descent = command.FontSize * 0.2f;
                }

                // Horizontal clamp depending on text alignment
                switch (paint.TextAlign)
                {
                    case SKTextAlign.Center:
                    {
                        var left = x - textWidth / 2f;
                        var right = x + textWidth / 2f;
                        if (left < padding) x = padding + textWidth / 2f;
                        if (right > canvasWidth - padding) x = canvasWidth - padding - textWidth / 2f;
                        break;
                    }
                    case SKTextAlign.Left:
                    {
                        if (x < padding) x = padding;
                        if (x + textWidth > canvasWidth - padding) x = canvasWidth - padding - textWidth;
                        break;
                    }
                    case SKTextAlign.Right:
                    {
                        if (x - textWidth < padding) x = padding + textWidth;
                        if (x > canvasWidth - padding) x = canvasWidth - padding;
                        break;
                    }
                }

                // Vertical clamp using font metrics
                var top = y + ascent; // ascent is typically negative
                var bottom = y + descent;
                if (top < padding) y = padding - ascent;
                if (bottom > canvasHeight - padding) y = canvasHeight - padding - descent;

                canvas.DrawText(command.Text, x, y, paint);
                break;
        }
    }

    public override void Dispose()
    {
        base.Dispose();
        
        _paint?.Dispose();
        _canvas?.Dispose();
        _surface?.Dispose();
        _renderTarget?.Dispose();
        _grContext?.Dispose();
        _grInterface?.Dispose();
        _defaultFont?.Dispose();
        _undefeatedFont?.Dispose();
        _window?.Dispose();
        _gl?.Dispose();
    }

    private enum DrawCommandType
    {
        Line,
        Rectangle,
        Text,
        Circle,
        CircleFilled,
        CircleOutline
    }

    private struct DrawCommand
    {
        public DrawCommandType Type;
        public Vector2 Start;
        public Vector2 End;
        public SKColor Color;
        public string Text;
        public float FontSize;
        public bool UseCustomFont;
        public bool Filled;
        public float Radius;
    }
}