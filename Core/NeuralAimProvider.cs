using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Numerics;
using TorchSharp;
using System.Text.Json;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace CS2GameHelper.Core
{
    public record TrainingDataPoint(float[] Input, float[] Output);

    public class NeuralAimNetwork : Module<Tensor, Tensor>
    {
        private readonly Module<Tensor, Tensor> _layer1;
        private readonly Module<Tensor, Tensor> _layer2;
        private readonly Module<Tensor, Tensor> _outputLayer;

        public NeuralAimNetwork() : base(nameof(NeuralAimNetwork))
        {
            // Вход: [dx, dy, dz, distance, velX, velY, velZ] → 7 фич
            _layer1 = Linear(7, 64);
            _layer2 = Linear(64, 64);
            _outputLayer = Linear(64, 2);
            RegisterComponents();
        }

        public override Tensor forward(Tensor x)
        {
            x = functional.relu(_layer1.forward(x));
            x = functional.relu(_layer2.forward(x));
            x = functional.tanh(_outputLayer.forward(x)) * 5.0f; // [-5, +5] пикселей
            return x;
        }
    }

    public class NeuralAimProvider : IAimCorrectionProvider, IDisposable
    {
        private const int MaxTrainingDataSize = 2000;
        private const int TrainingBatchSize = 64;
        private const string ModelFileName = "neural_aim.pth";
        private const string StatsFileName = "neural_stats.json";

        private readonly string _modelPath;
        private readonly string _statsPath;
        private readonly ConcurrentQueue<TrainingDataPoint> _trainingDataQueue = new();
        private readonly Thread _trainingThread;
        private readonly ManualResetEventSlim _trainingSignal = new(false);
        private readonly object _networkLock = new object();

        private NeuralAimNetwork? _network;
        private torch.optim.Optimizer? _optimizer;
        private volatile bool _isDisposed;

        // Нормализация
        private float[] _inputMean = new float[7];
        private float[] _inputStd = new float[7] { 1, 1, 1, 1, 1, 1, 1 };

        public NeuralAimProvider()
        {
            _modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ModelFileName);
            _statsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, StatsFileName);
            LoadStats();
            InitNetwork();
            _trainingThread = new Thread(TrainingLoop) { IsBackground = true, Priority = ThreadPriority.BelowNormal };
            _trainingThread.Start();
        }

        private void InitNetwork()
        {
            if (File.Exists(_modelPath))
            {
                _network = new NeuralAimNetwork();
                _network.load(_modelPath);
                _network.eval();
            }
            else
            {
                _network = new NeuralAimNetwork();
            }

            _optimizer = torch.optim.Adam(_network.parameters(), lr: 0.001);
            Console.WriteLine("[NeuralAim] Initialized.");
        }

        private void LoadStats()
        {
            if (!File.Exists(_statsPath)) return;
            try
            {
                var json = File.ReadAllText(_statsPath);
                var stats = JsonSerializer.Deserialize<(float[] Mean, float[] Std)>(json);
                if (stats.Mean.Length == 7 && stats.Std.Length == 7)
                {
                    _inputMean = stats.Mean;
                    _inputStd = stats.Std;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NeuralAim] Failed to load stats: {ex.Message}");
            }
        }

        private void SaveStats()
        {
            try
            {
                var stats = (_inputMean, _inputStd);
                var json = JsonSerializer.Serialize(stats, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_statsPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NeuralAim] Failed to save stats: {ex.Message}");
            }
        }

        public Vector2 GetCorrection(float distance, Vector3 targetPos, Vector3 playerPos, Vector3 targetVelocity)
        {
            if (_network == null || _isDisposed) return Vector2.Zero;

            var delta = targetPos - playerPos;
            var inputRaw = new[]
            {
                delta.X, delta.Y, delta.Z,
                distance,
                targetVelocity.X, targetVelocity.Y, targetVelocity.Z
            };

            // Нормализация
            var inputNorm = new float[7];
            for (int i = 0; i < 7; i++)
            {
                inputNorm[i] = (inputRaw[i] - _inputMean[i]) / (_inputStd[i] + 1e-6f);
            }

            try
            {
                lock (_networkLock)
                {
                    _network.eval();
                    using var inputTensor = torch.tensor(inputNorm, dtype: ScalarType.Float32).reshape(1, 7);
                    using var pred = _network.forward(inputTensor);
                    var arr = pred.data<float>().ToArray();
                    return new Vector2(arr[0], arr[1]);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NeuralAim] Inference error: {ex.Message}");
                return Vector2.Zero;
            }
        }

        public void AddObservation(float distance, Vector3 targetPos, Vector3 playerPos, Vector3 targetVelocity, float residualX, float residualY)
        {
            if (_isDisposed) return;

            var delta = targetPos - playerPos;
            var input = new[]
            {
                delta.X, delta.Y, delta.Z,
                distance,
                targetVelocity.X, targetVelocity.Y, targetVelocity.Z
            };
            var output = new[] { residualX, residualY };

            _trainingDataQueue.Enqueue(new TrainingDataPoint(input, output));

            while (_trainingDataQueue.Count > MaxTrainingDataSize)
                _trainingDataQueue.TryDequeue(out _);

            _trainingSignal.Set();
        }

        private void TrainingLoop()
        {
            var inputAccum = new List<float[]>();
            while (!_isDisposed)
            {
                _trainingSignal.Wait(2000);

                if (_trainingDataQueue.Count < TrainingBatchSize || _network == null || _optimizer == null)
                {
                    _trainingSignal.Reset();
                    continue;
                }

                var batch = _trainingDataQueue.DequeueBatch(TrainingBatchSize);
                if (batch.Count == 0) continue;

                try
                {
                    // Обновляем статистику нормализации
                    lock (_networkLock)
                    {
                        foreach (var point in batch)
                        {
                            inputAccum.Add(point.Input);
                            if (inputAccum.Count > 10000) inputAccum.RemoveAt(0);
                        }

                        // Пересчитываем mean/std каждые 500 новых точек
                        if (inputAccum.Count % 500 == 0)
                        {
                            for (int i = 0; i < 7; i++)
                            {
                                var col = inputAccum.Select(x => x[i]).ToArray();
                                _inputMean[i] = col.Average();
                                _inputStd[i] = (float)Math.Sqrt(col.Select(x => Math.Pow(x - _inputMean[i], 2)).Average()) + 0.01f;
                            }
                            SaveStats();
                        }

                        // Нормализуем батч
                        var inputsNorm = new float[batch.Count * 7];
                        var targets = new float[batch.Count * 2];
                        for (int i = 0; i < batch.Count; i++)
                        {
                            for (int j = 0; j < 7; j++)
                                inputsNorm[i * 7 + j] = (batch[i].Input[j] - _inputMean[j]) / (_inputStd[j] + 1e-6f);
                            targets[i * 2 + 0] = batch[i].Output[0];
                            targets[i * 2 + 1] = batch[i].Output[1];
                        }

                        _network.train();
                        using var inputTensor = torch.tensor(inputsNorm, dtype: ScalarType.Float32).reshape(batch.Count, 7);
                        using var targetTensor = torch.tensor(targets, dtype: ScalarType.Float32).reshape(batch.Count, 2);

                        _optimizer.zero_grad();
                        using var pred = _network.forward(inputTensor);
                        using var loss = functional.mse_loss(pred, targetTensor);
                        loss.backward();
                        _optimizer.step();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[NeuralAim] Training error: {ex.Message}");
                }

                _trainingSignal.Reset();
            }
        }

        public void Save()
        {
            if (_network != null)
            {
                _network.eval();
                _network.save(_modelPath);
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _trainingSignal.Set();
            _trainingThread?.Join();
            _trainingSignal.Dispose();

            Save();
            _network?.Dispose();
            _optimizer?.Dispose();
            Console.WriteLine("[NeuralAim] Disposed.");
        }
    }
}