using CS2GameHelper.Utils;
using SkiaSharp;

namespace CS2GameHelper.Features;

internal class BombTimer(Graphics.ModernGraphics graphics) : ThreadedServiceBase
{
    private static string _bombSite = string.Empty;
    private static bool _isBombPlanted;
    private static float _timeLeft;
    private static float _defuseLeft;
    private static bool _beingDefused;

    private float _currentTime;
    private IntPtr _globalVars;
    private IntPtr _plantedC4;

    protected override void FrameAction()
    {
        var gameProcess = graphics.GameProcess;
        var moduleClient = gameProcess.ModuleClient;
        var process = gameProcess.Process;

        // Reset state if game process is invalid
        if (moduleClient == null || process == null)
        {
            _isBombPlanted = false;
            return;
        }

        // Read global vars and current time
        _globalVars = moduleClient.Read<IntPtr>(Offsets.dwGlobalVars);
        if (_globalVars == IntPtr.Zero)
        {
            _isBombPlanted = false;
            return;
        }

        // Safely read current time (tick or real time)
        _currentTime = ReadSafe(() => process.Read<float>(_globalVars + 0x0), 0f); // realtime

        // Read planted C4 entity pointer
        var tempC4 = moduleClient.Read<IntPtr>(Offsets.dwPlantedC4);
        if (tempC4 == IntPtr.Zero)
        {
            _isBombPlanted = false;
            return;
        }

        _plantedC4 = ReadSafe(() => process.Read<IntPtr>(tempC4), IntPtr.Zero);
        if (_plantedC4 == IntPtr.Zero)
        {
            _isBombPlanted = false;
            return;
        }

        // Read planted flag (optional, fallback to true if pointer is valid)
        _isBombPlanted = ReadSafe(() => moduleClient.Read<bool>(Offsets.dwPlantedC4 - 0x8), true);

        // Read bomb entity fields
        var c4Blow = ReadSafe(() => process.Read<float>(_plantedC4 + Offsets.m_flC4Blow), 0f);
        var defuseCountDown = ReadSafe(() => process.Read<float>(_plantedC4 + Offsets.m_flDefuseCountDown), 0f);
        _beingDefused = ReadSafe(() => process.Read<bool>(_plantedC4 + Offsets.m_bBeingDefused), false);

        // Compute times
        _timeLeft = Math.Max(c4Blow - _currentTime, 0f);
        _defuseLeft = _beingDefused ? Math.Max(defuseCountDown - _currentTime, 0f) : 0f;

        // Read bomb site
        if (_isBombPlanted)
        {
            var site = ReadSafe(() => process.Read<int>(_plantedC4 + Offsets.m_nBombSite), 0);
            _bombSite = site == 1 ? "B" : "A";
        }

        // Optional: read defused flag to keep memory access consistent (no-op)
        _ = ReadSafe(() => process.Read<bool>(_plantedC4 + Offsets.m_bBombDefused), false);
    }

    // Helper to safely read memory without crashing on exceptions
    private static T ReadSafe<T>(Func<T> readFunc, T defaultValue)
    {
        try
        {
            return readFunc();
        }
        catch
        {
            return defaultValue;
        }
    }

    public static void Draw(Graphics.ModernGraphics graphics)
    {
        if (!_isBombPlanted) return;

        var bombText = $"Bomb planted on site: {_bombSite}";
        var timerText = $"Time left: {_timeLeft:0.00} seconds";
        var defuseText = _beingDefused && _defuseLeft > 0
            ? $"Defuse time: {_defuseLeft:0.00} seconds"
            : null;

        graphics.AddText(bombText, 10, 500, SKColors.WhiteSmoke, 16);
        graphics.AddText(timerText, 10, 520, SKColors.Orange, 14);
        if (defuseText != null)
        {
            graphics.AddText(defuseText, 10, 540, SKColors.Cyan, 14);
        }
    }
}