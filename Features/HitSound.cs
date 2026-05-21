using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using CS2GameHelper.Data.Entity;
using CS2GameHelper.Graphics;
using CS2GameHelper.Utils;
using System.Numerics;

namespace CS2GameHelper.Features;

public static class HitSound
{
    private static int _lastDamage = 0;
    private static DateTime _lastPlayTime = DateTime.MinValue;
    private static readonly object _sync = new();

    // Один фоновый поток-проигрыватель вместо Task.Run на каждый хит.
    // Bounded(8) — при любом всплеске избыточные звуки дропаются, а не плодят потоки.
    private static readonly BlockingCollection<string> _soundQueue = new(boundedCapacity: 8);

    static HitSound()
    {
        var worker = new Thread(() =>
        {
            foreach (var path in _soundQueue.GetConsumingEnumerable())
            {
                try
                {
                    using var player = new System.Media.SoundPlayer(path);
                    player.PlaySync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[HitSound] Playback error: {ex.Message}");
                }
            }
        })
        {
            IsBackground = true,
            Name = "HitSoundPlayer",
            Priority = ThreadPriority.BelowNormal
        };
        worker.Start();
    }

    private class HitText
    {
        public string Text { get; set; } = string.Empty;
        public DateTime ExpireAt { get; set; } = DateTime.MinValue;
        public Vector2 BasePosition { get; set; } = Vector2.Zero;
        public float State { get; set; } = 0f;
        public uint Color { get; set; } // ← Сохраняем цвет сразу
    }

    private static readonly List<HitText> _hitTexts = new();
    private static readonly object _textLock = new();

    public static void Process(ModernGraphics graphics)
    {
        var gameProcess = graphics.GameProcess;
        var player = graphics.GameData.Player;
        if (gameProcess?.Process == null || player == null || !player.IsAlive())
            return;

        if ((DateTime.Now - _lastPlayTime).TotalMilliseconds < 80)
            return;

        var localController = gameProcess.ModuleClient?.Read<IntPtr>(Offsets.client_dll.dwLocalPlayerController) ?? IntPtr.Zero;
        if (localController == IntPtr.Zero) return;

        var actionTracking = gameProcess.Read<IntPtr>(
            IntPtr.Add(localController, Offsets.m_pActionTrackingServices)
        );
        if (actionTracking == IntPtr.Zero) return;

        int currentDamage = gameProcess.Read<int>(
            IntPtr.Add(actionTracking, Offsets.m_flTotalRoundDamageDealt)
        );

        if (currentDamage > _lastDamage)
        {
            var delta = currentDamage - _lastDamage;
            var cfg = ConfigManager.Load();
            var hsCfg = cfg.HitSound ?? new ConfigManager.HitSoundConfig();

            if (!hsCfg.Enabled) return;

            // Используем настраиваемый порог хедшота
            bool isHeadshot = delta >= hsCfg.HeadshotDamageThreshold;
            string text = isHeadshot ? hsCfg.HeadshotText : hsCfg.HitText;
            string soundFile = isHeadshot ? hsCfg.HeadshotSoundFile : hsCfg.HitSoundFile;
            uint baseColor = ParseColorHex(isHeadshot ? hsCfg.HeadshotColor : hsCfg.HitColor);

            Console.WriteLine($"[HitSound] 💥 {text}! Damage: {currentDamage} (delta: {delta})");

            PlayHitSound(soundFile);

            // === ДОБАВЛЯЕМ ТЕКСТ С ЦВЕТОМ ===
            var screenSize = gameProcess.WindowRectangleClient;
            if (screenSize.Width <= 0 || screenSize.Height <= 0) return;

            var center = new Vector2(screenSize.Width / 2f, screenSize.Height / 2f);

            lock (_textLock)
            {
                _hitTexts.Add(new HitText
                {
                    Text = text,
                    ExpireAt = DateTime.Now.AddSeconds(hsCfg.TextDurationSeconds),
                    BasePosition = center,
                    Color = baseColor // ← Сохраняем цвет
                });
            }

            lock (_sync)
            {
                _lastDamage = currentDamage;
                _lastPlayTime = DateTime.Now;
            }
        }
    }

    public static void DrawHitTexts(ModernGraphics graphics)
    {
        var now = DateTime.Now;
        lock (_textLock)
        {
            for (int i = _hitTexts.Count - 1; i >= 0; i--)
            {
                var hitText = _hitTexts[i];
                if (now > hitText.ExpireAt)
                {
                    _hitTexts.RemoveAt(i);
                    continue;
                }

                // Анимация
                hitText.State += 1f;
                float offsetX = 100f * MathF.Sin(hitText.State / 50f) - 50f;
                float offsetY = -50f - (hitText.State * 2);
                var pos = new Vector2(hitText.BasePosition.X + offsetX, hitText.BasePosition.Y + offsetY);

                // Прозрачность
                float lifeTime = (float)(hitText.ExpireAt - now).TotalMilliseconds;
                float totalLife = 1500f; // 1.5s default, but we use config in Process
                float alpha = Math.Clamp(1f - ((totalLife - lifeTime) / totalLife), 0.1f, 1f);

                // Используем сохранённый цвет
                byte a = (byte)(255 * alpha);
                uint rgb = hitText.Color & 0x00FFFFFFu;
                uint color = ((uint)a << 24) | rgb;

                graphics.DrawText(hitText.Text, pos.X, pos.Y, color, fontSize: 32, useCustomFont: true);
            }
        }
    }

    private static void PlayHitSound(string soundFile)
    {
        try
        {
            var path = soundFile;
            if (!Path.IsPathRooted(path))
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(path))
            {
                Console.WriteLine($"[HitSound] ❌ Sound not found: {path}");
                return;
            }

            // TryAdd — неблокирующий, избыток просто дропаем.
            _soundQueue.TryAdd(path);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HitSound] Error: {ex.Message}");
        }
    }

    private static uint ParseColorHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return 0xFFFFFFFFu;
        hex = hex.Trim().TrimStart('#').TrimStart('0', 'x', 'X');
        if (hex.Length > 8) hex = hex.Substring(hex.Length - 8);
        if (hex.Length == 6) hex = "FF" + hex; // добавляем альфу
        try
        {
            return Convert.ToUInt32(hex, 16);
        }
        catch
        {
            return 0xFFFFFFFFu;
        }
    }
}