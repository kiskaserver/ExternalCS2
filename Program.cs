using System;
using System.Threading;
using CS2GameHelper.Core;
using CS2GameHelper.Data.Game;
using CS2GameHelper.Features;
using CS2GameHelper.Graphics;
using CS2GameHelper.Utils;

namespace CS2GameHelper;

public sealed class Program : IDisposable
{
    private readonly GameProcess _gameProcess;
    private readonly GameData _gameData;
    private readonly UserInputHandler _inputHandler; // ← ЕДИНЫЙ ИСТОЧНИК ВВОДА
    private readonly ModernGraphics _graphics;
    private readonly TriggerBot _triggerBot;
    private readonly AimBot _aimBot;
    private readonly BombTimer _bombTimer;
    private bool _disposed;

    private Program()
    {
        Offsets.UpdateOffsets().GetAwaiter().GetResult();

        var features = ConfigManager.Load();

        _gameProcess = new GameProcess();
        _gameProcess.Start();

        _gameData = new GameData(_gameProcess);
        _gameData.Start();

        // Создаём ЕДИНСТВЕННЫЙ UserInputHandler
        _inputHandler = new UserInputHandler();

        // Передаём его в компоненты, которые нуждаются во вводе
        _graphics = new ModernGraphics(_gameProcess, _gameData, _inputHandler);
        _graphics.Start();

        _triggerBot = new TriggerBot(_gameProcess, _gameData);
        if (features.TriggerBot)
        {
            _triggerBot.Start();
        }

        _aimBot = new AimBot(_gameProcess, _gameData, _inputHandler); // ← передаём inputHandler
        if (features.AimBot)
        {
            _aimBot.Start();
        }

        _bombTimer = new BombTimer(_graphics);
        if (features.BombTimer)
        {
            _bombTimer.Start();
        }
    }

    public static void Main()
    {
        User32.TryEnablePerMonitorDpiAwareness();

        using var program = new Program();

        Console.WriteLine("CS2 helper started. Press 'q' to quit.");
        while (true)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true).Key;
                if (key == ConsoleKey.Q)
                {
                    break;
                }
            }

            Thread.Sleep(100);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // ВАЖНО: Dispose в обратном порядке создания
            _bombTimer?.Dispose();
            _aimBot?.Dispose();
            _triggerBot?.Dispose();
            _graphics?.Dispose();
            _inputHandler?.Dispose(); // ← освобождаем хуки
            _gameData?.Dispose();
            _gameProcess?.Dispose();
        }

        _disposed = true;
    }
}