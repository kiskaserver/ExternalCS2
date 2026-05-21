using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CS2GameHelper.Core;

public record AimBucketData
{
    public double SumX { get; set; }
    public double SumY { get; set; }
    public double Weight { get; set; } // Используем вес вместо счётчика для EMA
}

public class StatisticalAimProvider : IAimCorrectionProvider
{
    private readonly string _filePath;
    private readonly object _sync = new();
    private readonly Dictionary<int, AimBucketData> _buckets = new();
    private readonly Queue<(DateTime When, float Distance, float ResX, float ResY)> _pending = new();
    private const double MaxWeight = 100.0;
    private const double Alpha = 0.1; // EMA коэффициент
    private const int PendingWindowMs = 600;
    private const int HitMatchWindowMs = 250;

    public StatisticalAimProvider(string? storageFile = null)
    {
        _filePath = storageFile ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "stat_aim.json");
        Load();
    }

    public Vector2 GetCorrection(in AimContext ctx)
    {
        lock (_sync)
        {
            var key = GetBucketKey(ctx.Distance);
            if (_buckets.TryGetValue(key, out var bucket) && bucket.Weight > 0.1)
            {
                return new Vector2((float)(bucket.SumX / bucket.Weight), (float)(bucket.SumY / bucket.Weight));
            }
            return Vector2.Zero;
        }
    }

    public void AddObservation(in AimContext ctx, float residualX, float residualY)
    {
        if (Math.Abs(residualX) > 50 || Math.Abs(residualY) > 50) return;

        lock (_sync)
        {
            _pending.Enqueue((DateTime.UtcNow, ctx.Distance, residualX, residualY));
            var cutoff = DateTime.UtcNow.AddMilliseconds(-PendingWindowMs);
            while (_pending.Count > 0 && _pending.Peek().When < cutoff)
                _pending.Dequeue();
        }
    }

    public void ConfirmHit()
    {
        var cutoff = DateTime.UtcNow.AddMilliseconds(-HitMatchWindowMs);
        lock (_sync)
        {
            while (_pending.Count > 0)
            {
                var entry = _pending.Dequeue();
                if (entry.When < cutoff) continue;
                UpdateBucket(entry.Distance, entry.ResX, entry.ResY);
            }
        }
    }

    private void UpdateBucket(float distance, float residualX, float residualY)
    {
        var key = GetBucketKey(distance);
        if (!_buckets.TryGetValue(key, out var bucket))
        {
            bucket = new AimBucketData();
            _buckets[key] = bucket;
        }

        // EMA update
        bucket.SumX = Alpha * residualX + (1 - Alpha) * bucket.SumX;
        bucket.SumY = Alpha * residualY + (1 - Alpha) * bucket.SumY;
        bucket.Weight = Alpha + (1 - Alpha) * bucket.Weight;

        if (bucket.Weight > MaxWeight)
        {
            bucket.SumX *= MaxWeight / bucket.Weight;
            bucket.SumY *= MaxWeight / bucket.Weight;
            bucket.Weight = MaxWeight;
        }
    }

    public void Save()
    {
        lock (_sync)
        {
            try
            {
                var serializable = _buckets
                    .Where(kv => kv.Value.Weight > 0.1)
                    .ToDictionary(kv => kv.Key.ToString(), kv => kv.Value);

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
                };

                var json = JsonSerializer.Serialize(serializable, options);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StatAim ERROR] Save: {ex.Message}");
            }
        }
    }

    private void Load()
    {
        lock (_sync)
        {
            if (!File.Exists(_filePath)) return;

            try
            {
                var json = File.ReadAllText(_filePath);
                var dict = JsonSerializer.Deserialize<Dictionary<string, AimBucketData>>(json);

                if (dict != null)
                {
                    _buckets.Clear();
                    foreach (var (keyStr, data) in dict)
                    {
                        if (int.TryParse(keyStr, out int key) && data.Weight > 0.1)
                        {
                            _buckets[key] = data;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StatAim ERROR] Load: {ex.Message}");
            }
        }
    }

    private int GetBucketKey(float distance) => Math.Max(0, (int)(distance / 100f));
}