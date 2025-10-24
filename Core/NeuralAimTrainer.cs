using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Numerics;
using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace CS2GameHelper.Core
{
    public record TrainingDataPoint(float[] Input, float[] Output);

    public class NeuralAimNetwork : Module<Tensor, Tensor>
    {
        private readonly Module<Tensor, Tensor> _layer1;
        private readonly Module<Tensor, Tensor> _layer2;
        private readonly Module<Tensor, Tensor> _layer3;
        private readonly Module<Tensor, Tensor> _outputLayer;

        public NeuralAimNetwork(int inputSize = 6, int hiddenSize = 64, int outputSize = 2)
            : base(nameof(NeuralAimNetwork))
        {
            _layer1 = Linear(inputSize, hiddenSize);
            _layer2 = Linear(hiddenSize, hiddenSize);
            _layer3 = Linear(hiddenSize, hiddenSize);
            _outputLayer = Linear(hiddenSize, outputSize);
            RegisterComponents();
        }

        public override Tensor forward(Tensor x)
        {
            x = functional.relu(_layer1.forward(x));
            x = functional.relu(_layer2.forward(x));
            x = functional.relu(_layer3.forward(x));
            x = functional.tanh(_outputLayer.forward(x)) * 5.0f;
            return x;
        }
    }

    public class NeuralAimTrainer : IDisposable
    {
        private const int MaxTrainingDataSize = 1000;
        private const int TrainingBatchSize = 64;
        private const string ModelFileName = "aim_model.pth";

        private readonly string _modelPath;
        private readonly ConcurrentQueue<TrainingDataPoint> _trainingDataQueue = new();
        private readonly Thread _trainingThread;
        private readonly ManualResetEventSlim _trainingSignal = new(false);
        private readonly object _networkLock = new object();

        private NeuralAimNetwork? _network;
        private torch.optim.Optimizer? _optimizer;
        private volatile bool _isDisposed;

        public NeuralAimTrainer()
        {
            _modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ModelFileName);
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
            Console.WriteLine("[NeuralAimTrainer] Нейросеть инициализирована.");
        }

        public void AddDataPoint(Vector3 targetPos, Vector3 playerPos, Vector2 angles)
        {
            if (_isDisposed) return;

            var input = new[] { targetPos.X, targetPos.Y, targetPos.Z, playerPos.X, playerPos.Y, playerPos.Z };
            var output = new[] { angles.X, angles.Y };

            _trainingDataQueue.Enqueue(new TrainingDataPoint(input, output));

            if (_trainingDataQueue.Count > MaxTrainingDataSize)
            {
                _trainingDataQueue.TryDequeue(out _);
            }
            _trainingSignal.Set();
        }

        public Vector2 GetCorrection(Vector3 targetPos, Vector3 playerPos)
        {
            if (_network == null || _isDisposed) return Vector2.Zero;

            try
            {
                lock (_networkLock)
                {
                    _network.eval();
                    using var inputTensor = torch.tensor(new[]
                    {
                        targetPos.X, targetPos.Y, targetPos.Z,
                        playerPos.X, playerPos.Y, playerPos.Z
                    }, dtype: ScalarType.Float32).reshape(1, 6);

                    using var prediction = _network.forward(inputTensor);
                    var predArray = prediction.data<float>().ToArray();
                    return new Vector2(predArray[0], predArray[1]);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NeuralAimTrainer] Ошибка при получении коррекции: {ex.Message}");
                return Vector2.Zero;
            }
        }

        private void TrainingLoop()
        {
            while (!_isDisposed)
            {
                _trainingSignal.Wait(1000);

                if (_trainingDataQueue.Count < TrainingBatchSize || _network == null || _optimizer == null)
                {
                    _trainingSignal.Reset();
                    continue;
                }

                var batch = _trainingDataQueue.DequeueBatch(TrainingBatchSize);
                if (batch.Count == 0) continue;

                try
                {
                    var inputs = batch.SelectMany(p => p.Input).ToArray();
                    var targets = batch.SelectMany(p => p.Output).ToArray();

                    lock (_networkLock)
                    {
                        _network.train();
                        using var inputTensor = torch.tensor(inputs, dtype: ScalarType.Float32).reshape(batch.Count, 6);
                        using var targetTensor = torch.tensor(targets, dtype: ScalarType.Float32).reshape(batch.Count, 2);

                        _optimizer.zero_grad();
                        using var predictions = _network.forward(inputTensor);
                        using var loss = functional.mse_loss(predictions, targetTensor);
                        loss.backward();
                        _optimizer.step();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[NeuralAimTrainer] Ошибка в цикле обучения: {ex.Message}");
                }

                _trainingSignal.Reset();
            }
        }
        
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _trainingSignal.Set();
            _trainingThread?.Join(); // ИСПРАВЛЕНО: добавлен оператор '?'
            _trainingSignal.Dispose();

            if (_network != null)
            {
                _network.eval();
                _network.save(_modelPath);
                _network.Dispose();
            }
            _optimizer?.Dispose();
            Console.WriteLine("[NeuralAimTrainer] Ресурсы освобождены, модель сохранена.");
        }
    }


    public static class ConcurrentQueueExtensions
    {
        // Обратите внимание: добавляем <T> для метода
        public static List<T> DequeueBatch<T>(this ConcurrentQueue<T> queue, int count)
        {
            var list = new List<T>();
            for (int i = 0; i < count; i++)
            {
                // Use a nullable out variable to avoid CS8600 when T is a reference type
                if (!queue.TryDequeue(out T? item)) break;
                // item may be null for nullable reference types; the null-forgiving operator
                // is safe here because a successfully dequeued item (even if null) is intended
                // to be part of the returned batch. We preserve the original signature.
                list.Add(item!);
            }
            return list;
        }
    }
}