using System;
using System.IO;
using System.Threading.Tasks;
using CS2GameHelper.Data.Entity;
using CS2GameHelper.Graphics;
using CS2GameHelper.Utils;

namespace CS2GameHelper.Features;

public static class HitSound
{
    private static int _lastDamage = 0;
    private static DateTime _lastPlayTime = DateTime.MinValue;
    private static readonly object _sync = new();

    public static void Process(ModernGraphics graphics)
    {
        var gameProcess = graphics.GameProcess;
        var player = graphics.GameData.Player;

        if (gameProcess?.Process == null || player == null || !player.IsAlive())
            return;
   
        if ((DateTime.Now - _lastPlayTime).TotalMilliseconds < 80)
            return;

        // 1. Читаем указатель на CPlayerController
        // ModuleClient?.Read may return a nullable native int (nint?), coalesce to IntPtr.Zero for safe use
         var localController = gameProcess.ModuleClient?.Read<IntPtr>(Offsets.client_dll.dwLocalPlayerController) ?? IntPtr.Zero;
        if (localController == IntPtr.Zero)
            return;

        // 2. Читаем m_pActionTrackingServices (указатель на CCSPlayerController_ActionTrackingServices)
        var actionTracking = gameProcess.Process.Read<IntPtr>(
            IntPtr.Add(localController, Offsets.m_pActionTrackingServices)
        );
        if (actionTracking == IntPtr.Zero)
            return;

        // 3. Читаем m_flTotalRoundDamageDealt из ActionTrackingServices
        int currentDamage = gameProcess.Process.Read<int>(
            IntPtr.Add(actionTracking, Offsets.m_flTotalRoundDamageDealt)
        );

        // 4. Проверяем увеличение урона
        if (currentDamage > _lastDamage)
        {
            Console.WriteLine($"[HitSound] 💥 Hit! Damage: {currentDamage} (delta: {currentDamage - _lastDamage})");
            PlayHitSound();
            lock (_sync)
            {
                _lastDamage = currentDamage;
                _lastPlayTime = DateTime.Now;
            }
        }


    }

    private static void PlayHitSound()
    {
        try
        {
            string soundPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "sounds", "hit.wav");
            if (!File.Exists(soundPath))
            {
                Console.WriteLine($"[HitSound] ❌ Sound not found: {soundPath}");
                return;
            }

            _ = Task.Run(() =>
            {
                try
                {
                    using var player = new System.Media.SoundPlayer(soundPath);
                    player.PlaySync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[HitSound] Playback error: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HitSound] Error: {ex.Message}");
        }
    }
}