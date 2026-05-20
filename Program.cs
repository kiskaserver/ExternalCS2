using System;
using System.Threading;
using CS2GameHelper.Core;
using CS2GameHelper.Data.Game;
using CS2GameHelper.Features;
using CS2GameHelper.Graphics;
using CS2GameHelper.Utils;
using Keys = CS2GameHelper.Utils.Keys;

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
    private readonly VoteTeller _voteTeller;
    private readonly ConfigManager _config;
    private bool _disposed;

    private Program(ConfigManager config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        Offsets.UpdateOffsets().GetAwaiter().GetResult();

        _gameProcess = new GameProcess();
        _gameProcess.Start();

        _gameData = new GameData(_gameProcess);
        _gameData.Start();

        // Создаём ЕДИНСТВЕННЫЙ UserInputHandler
        _inputHandler = new UserInputHandler();

        // Передаём его в компоненты, которые нуждаются во вводе
        _graphics = new ModernGraphics(_gameProcess, _gameData, _inputHandler, _config);
        _graphics.Start();

        _triggerBot = new TriggerBot(_gameProcess, _gameData, _inputHandler);
        if (_config.TriggerBot)
        {
            _triggerBot.Start();
        }

        _aimBot = new AimBot(_gameProcess, _gameData, _inputHandler); // ← передаём inputHandler
        if (_config.AimBot)
        {
            _aimBot.Start();
        }

        _bombTimer = new BombTimer(_graphics);
        if (_config.BombTimer)
        {
            _bombTimer.Start();
        }

        _voteTeller = new VoteTeller(_gameProcess);
        if (_config.VoteTeller.Enabled)
        {
            _voteTeller.Start();
        }
    }

    public static void Main()
    {
        User32.TryEnablePerMonitorDpiAwareness();

        var config = ConfigManager.Load();
        using var program = new Program(config);

        Console.WriteLine("CS2GameHelper started!");
        Console.WriteLine("Controls:");
        if (config.MenuToggleKey == Keys.None)
        {
            Console.WriteLine("  Menu hotkey is disabled");
        }
        else
        {
            Console.WriteLine($"  {OverlayMenu.FormatKey(config.MenuToggleKey)} - Toggle settings menu");
        }
        Console.WriteLine("  F11 - Toggle overlay visibility");
        Console.WriteLine("  Alt+Z - Temporarily hide overlay");
        Console.WriteLine("  Close this window to exit");
        Console.WriteLine();

        // Create a simple message loop instead of blocking on console input
        var running = true;
        Thread consoleThread = new Thread(() =>
        {
            try
            {
                while (running)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true).Key;
                        if (key == ConsoleKey.Q || key == ConsoleKey.Escape)
                        {
                            running = false;
                            break;
                        }
                    }
                    Thread.Sleep(100);
                }
            }
            catch
            {
                // Console might not be available in some scenarios
            }
        });
        
        consoleThread.IsBackground = true;
        consoleThread.Start();

        // Main application loop
        while (running)
        {
            Thread.Sleep(50);
        }

        Console.WriteLine("Shutting down...");
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
            _voteTeller?.Dispose();
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