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

    private float _currentServerTime;
    private IntPtr _plantedC4;

    protected override void FrameAction()
    {
        // Сбрасываем состояние в начале каждого кадра
        _isBombPlanted = false;

        var gameProcess = graphics.GameProcess;
        var clientModule = gameProcess.ModuleClient;
        var engineModule = gameProcess.ModuleEngine;
        var process = gameProcess.Process;

        if (clientModule == null || engineModule == null || process == null) return;

        // <<< ИСПОЛЬЗУЕМ ТОЧНОЕ ВРЕМЯ ИЗ СЕРВЕРНЫХ ТИКОВ
        var networkGameClient = engineModule.Read<IntPtr>(Offsets.engine2_dll.dwNetworkGameClient);
        if (networkGameClient == IntPtr.Zero) return;

        var serverTickCount = ReadSafe(() => process.Read<int>(networkGameClient + Offsets.engine2_dll.dwNetworkGameClient_serverTickCount), 0);
        _currentServerTime = serverTickCount * 0.015625f;

        var tempC4 = clientModule.Read<IntPtr>(Offsets.client_dll.dwPlantedC4);
        if (tempC4 == IntPtr.Zero) return;

        _plantedC4 = ReadSafe(() => process.Read<IntPtr>(tempC4), IntPtr.Zero);
        if (_plantedC4 == IntPtr.Zero) return;

        // <<< КЛЮЧЕВОЕ ВОЗВРАЩЕНИЕ: Используем главный флаг активности бомбы
        // Этот флаг, скорее всего, является самым надежным индикатором
        _isBombPlanted = ReadSafe(() => clientModule.Read<bool>(Offsets.client_dll.dwPlantedC4 - 0x8), false);

        // Если флаг говорит, что бомба неактивна, выходим. Это самая важная проверка.
        if (!_isBombPlanted) return;

        // Дополнительная проверка на всякий случай (если флаг не сработал)
        var isDefused = ReadSafe(() => process.Read<bool>(_plantedC4 + Offsets.m_bBombDefused), false);
        if (isDefused)
        {
            _isBombPlanted = false; // Явно сбрасываем, если флаг дефузации сработал
            return;
        }

        // Читаем остальные данные
        var c4Blow = ReadSafe(() => process.Read<float>(_plantedC4 + Offsets.m_flC4Blow), 0f);
        var defuseCountDown = ReadSafe(() => process.Read<float>(_plantedC4 + Offsets.m_flDefuseCountDown), 0f);
        _beingDefused = ReadSafe(() => process.Read<bool>(_plantedC4 + Offsets.m_bBeingDefused), false);

        // Compute times с использованием точного времени
        _timeLeft = c4Blow - _currentServerTime;
        _defuseLeft = _beingDefused ? defuseCountDown - _currentServerTime : 0f;

        // <<< КЛЮЧЕВОЕ ИСПРАВЛЕНИЕ: Не показываем таймер, если время истекло
        if (_timeLeft < 0)
        {
            _isBombPlanted = false; // Сбрасываем флаг, чтобы таймер пропал
            return; // Выходим, чтобы не рисовать
        }

        // Read bomb site
        var site = ReadSafe(() => process.Read<int>(_plantedC4 + Offsets.m_nBombSite), 0);
        _bombSite = site == 1 ? "B" : "A";
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