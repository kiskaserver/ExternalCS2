using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CS2GameHelper.Core;

// Используем record для простой сериализации
public record AimBucketData
{
    public int Count { get; set; }
    public double SumX { get; set; }
    public double SumY { get; set; }
}

public class AimTrainer
{
    private readonly string _filePath;
    private readonly object _sync = new();

    // Кэш коррекций: расстояние → средняя ошибка
    private readonly Dictionary<int, AimBucketData> _buckets = new();

    // Максимальное количество наблюдений на бакет (защита от переполнения)
    private const int MaxBucketCount = 10_000;

    public AimTrainer(string? storageFile = null)
    {
        _filePath = storageFile ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "aim_trainer.json");
        Load();
    }

    public void Save()
    {
        lock (_sync)
        {
            try
            {
                // Фильтруем пустые или повреждённые бакеты
                var serializable = _buckets
                    .Where(kv => kv.Value.Count > 0 && kv.Value.Count <= MaxBucketCount)
                    .ToDictionary(
                        kv => kv.Key.ToString(),
                        kv => kv.Value
                    );

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
                Console.WriteLine($"[AimTrainer ERROR] Save error: {ex.Message}\n{ex.StackTrace}");
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
                        if (int.TryParse(keyStr, out int key) && data.Count > 0)
                        {
                            // Защита от аномалий
                            if (data.Count <= MaxBucketCount)
                                _buckets[key] = data;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AimTrainer ERROR] Load error: {ex.Message}\n{ex.StackTrace}");
                // On error — a new file will be created on next Save()
            }
        }
    }

    private int GetBucketKey(float distance)
    {
        // Бакеты по 100 единиц (настраивается)
        return Math.Max(0, (int)Math.Round(distance / 100.0f));
    }

    public Vector2 GetCorrection(float distance)
    {
        lock (_sync)
        {
            var key = GetBucketKey(distance);
            if (_buckets.TryGetValue(key, out var bucket) && bucket.Count > 0)
            {
                return new Vector2(
                    (float)(bucket.SumX / bucket.Count),
                    (float)(bucket.SumY / bucket.Count)
                );
            }
            return Vector2.Zero;
        }
    }

    public void AddObservation(float distance, float residualX, float residualY)
    {
        lock (_sync)
        {
            var key = GetBucketKey(distance);
            if (!_buckets.TryGetValue(key, out var bucket))
            {
                bucket = new AimBucketData();
                _buckets[key] = bucket;
            }

            // Защита от переполнения и аномалий
            if (bucket.Count < MaxBucketCount && 
                Math.Abs(residualX) < 100 && 
                Math.Abs(residualY) < 100)
            {
                bucket.Count++;
                bucket.SumX += residualX;
                bucket.SumY += residualY;
            }

            // Сохраняем периодически (каждые 20 наблюдений в бакете)
            if (bucket.Count % 20 == 0)
            {
                Save();
            }
        }
    }
}