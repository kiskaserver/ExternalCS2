using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;
using Point = System.Drawing.Point;

using CS2GameHelper.Core;
using CS2GameHelper.Core.Data;
using CS2GameHelper.Data.Entity;
using CS2GameHelper.Data.Game;
using CS2GameHelper.Graphics;
using CS2GameHelper.Utils;

namespace CS2GameHelper.Features;

public enum MouseMessages
{
    WmLButtonDown = 0x0201,
    WmLButtonUp = 0x0202,
    WmMouseMove = 0x0200
}

// ===========================================

record AimTargetResult(
    bool Found,
    Vector3 TargetPosition,
    Vector2 AimAngles,
    float Distance,
    int TargetId
);

public class NeuralAimNetwork : Module<Tensor, Tensor>
{
    private readonly Module<Tensor, Tensor> _layer1;
    private readonly Module<Tensor, Tensor> _layer2;
    private readonly Module<Tensor, Tensor> _layer3;
    private readonly Module<Tensor, Tensor> _outputLayer;

    // === Исправлено: конструктор с параметрами по умолчанию ===
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
        x = functional.tanh(_outputLayer.forward(x)) * 5.0f; // масштабируем выход
        return x;
    }
}

public class TargetMotionData
{
    public Vector3 Position { get; set; }
    public Vector3 Velocity { get; set; }
    public Vector3 Acceleration { get; set; }
    public float Timestamp { get; set; }

    public TargetMotionData(Vector3 position, Vector3 velocity, Vector3 acceleration, float timestamp)
    {
        Position = position;
        Velocity = velocity;
        Acceleration = acceleration;
        Timestamp = timestamp;
    }
}

// === Состояние аимбота (вместо несуществующего 'State') ===
public enum AimBotState
{
    Up,
    DownSuppressed,
    Down
}

public class AimBot : ThreadedServiceBase
{
    private const float AimBotSmoothing = 3f;
    private static double HumanReactThreshold = 30.0;
    private const int SuppressMs = 200;
    private const int UserMouseDeltaResetMs = 50;
    private const int AimUpdateIntervalMs = 500;
    private const int AimEventWindowMs = 1000;
    private static double _anglePerPixelHorizontal = 1;
    private static double _anglePerPixelVertical = 1;
    private const double HumanEaseDistancePixels = 35.0;
    private const double HumanMinimumGain = 0.15;
    private const int LockJitterStartMs = 600;
    private const int LockJitterStrongMs = 1500;

    private static ConfigManager? _config;
    private static AimTrainer? _aimTrainer;

    private static readonly string[] AimBonePriority = { "head", "neck", "chest", "pelvis" };

    private readonly object _stateLock = new();
    private AimBotState _currentState = AimBotState.Up; // ← ЗАМЕНА 'State'

    private static readonly Random HumanizationRandom = new();

    private readonly Keys AimBotHotKey;
    private double _aiAggressiveness = 1.0; // было 2 → теперь в [0,1]

    private int _aimSuccessCount;
    private int _aimTotalCount;
    private double _dynamicFov = GraphicsMath.DegreeToRadian(15f);
    private double _dynamicSmoothing = AimBotSmoothing;
    private DateTime _lastAimEvent = DateTime.MinValue;
    private DateTime _lastAiUpdate = DateTime.MinValue;
    private DateTime _lastMouseMoveTime = DateTime.MinValue;

    private int _lastMouseX;
    private int _lastMouseY;

    private DateTime _lastSuppressed = DateTime.MinValue;
    private int _lastTargetId = -1;
    private int _currentTargetId = -1;
    private int _activeTargetId = -1;
    private DateTime _lastTargetLockTime = DateTime.MinValue;

    private Vector3 _lastTargetPos = Vector3.Zero;
    private DateTime _lastTargetUpdate = DateTime.MinValue;
    private Vector3 _lastTargetVel = Vector3.Zero;

    private int _userMouseDeltaX;
    private int _userMouseDeltaY;
    private double _userMoveAvg;
    private int _userMoveCount;
    private double _userMoveSum;

    // Новые поля для самообучения с TorchSharp
    private NeuralAimNetwork? _neuralNetwork;
    private torch.optim.Optimizer? _optimizer;
    private readonly List<(Vector3 targetPos, Vector3 playerPos, float distance, Vector2 angle)> _trainingData = new();
    private const int MaxTrainingDataSize = 1000;

    private DateTime _lastShotTime = DateTime.MinValue;
    private const int MinShootIntervalMs = 100;

    private readonly string _modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "aim_model.pth");

    public AimBot(GameProcess gameProcess, GameData gameData)
    {
        GameProcess = gameProcess;
        GameData = gameData;
        AimBotHotKey = Config.AimBotKey;
        _aimTrainer ??= new AimTrainer();

        LoadNeuralNetwork();
        if (_neuralNetwork == null)
            InitNeuralNetwork();
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (MouseMessages)wParam == MouseMessages.WmMouseMove)
        {
            var mouseInput = Marshal.PtrToStructure<User32.MSLLHOOKSTRUCT>(lParam);
            var dx = mouseInput.Point.X - _lastMouseX;
            var dy = mouseInput.Point.Y - _lastMouseY;
            _userMouseDeltaX = dx;
            _userMouseDeltaY = dy;
            _lastMouseMoveTime = DateTime.Now;
            _lastMouseX = mouseInput.Point.X;
            _lastMouseY = mouseInput.Point.Y;
            var moveLen = Math.Sqrt(dx * dx + dy * dy);
            _userMoveSum += moveLen;
            _userMoveCount++;
        }

        return nCode < 0 || ProcessMouseMessage((MouseMessages)wParam)
            ? User32.CallNextHookEx(MouseHook != null ? MouseHook.HookHandle : IntPtr.Zero, nCode, wParam, lParam)
            : new IntPtr(1);
    }

    private bool ProcessMouseMessage(MouseMessages mouseMessage)
    {
        if (mouseMessage == MouseMessages.WmLButtonUp)
        {
            lock (_stateLock)
            {
                _currentState = AimBotState.Up; // ← ИСПРАВЛЕНО
            }
            return true;
        }

        if (mouseMessage != MouseMessages.WmLButtonDown) return true;

        if (GameProcess == null || !GameProcess.IsValid ||
            GameData == null || GameData.Player == null || !GameData.Player.IsAlive() ||
            TriggerBot.IsHotKeyDown() ||
            GameData.Player.IsGrenade())
            return true;

        lock (_stateLock)
        {
            if (_currentState == AimBotState.Up) // ← ИСПРАВЛЕНО
                _currentState = AimBotState.DownSuppressed;
        }

        return true;
    }

    private void SaveNeuralNetwork()
    {
        if (_neuralNetwork != null)
        {
            _neuralNetwork.save(_modelPath);
        }
    }

    private void LoadNeuralNetwork()
    {
        if (File.Exists(_modelPath))
        {
            _neuralNetwork = new NeuralAimNetwork();
            _neuralNetwork.load(_modelPath);
            _neuralNetwork.eval();

            _optimizer = torch.optim.Adam(_neuralNetwork.parameters(), 0.001);
        }
    }

    private void InitNeuralNetwork()
    {
        _neuralNetwork = new NeuralAimNetwork();
        _optimizer = torch.optim.Adam(_neuralNetwork.parameters(), lr: 0.001);
        Console.WriteLine("[AimBot] Создана новая нейросеть.");
    }

    private static ConfigManager Config => _config ??= ConfigManager.Load();

    private static MouseMoveMethod MouseMoveMethod =>
        MouseMoveMethod.TryMouseMoveNew;

    private bool IsCalibrated { get; set; }

    protected override string ThreadName => nameof(AimBot);

    private GameProcess? GameProcess { get; set; }
    private GlobalHook? MouseHook { get; set; }
    private GameData? GameData { get; set; }
    private float CurrentSmoothing { get; set; } = AimBotSmoothing;

    public override void Dispose()
    {
        SaveNeuralNetwork();
        _aimTrainer?.Save(); // если у AimTrainer есть метод Save()

        base.Dispose();

        MouseHook?.Dispose();
        GameData = null;
        GameProcess = null;

        _neuralNetwork?.Dispose();
        _optimizer?.Dispose();
    }

    private bool IsHotKeyDown()
    {
        return (User32.GetAsyncKeyState((int)AimBotHotKey) & 0x8000) != 0;
    }

    protected override void FrameAction()
    {
        // Активен ТОЛЬКО при нажатии клавиши
        bool isManualMode = IsHotKeyDown();
        bool isAutoMode = Config.AimBotAutoShoot;

        if (!isManualMode && !isAutoMode)
            return;

        try
        {
            if (GameProcess == null || !GameProcess.IsValid ||
                GameData?.Player == null || !GameData.Player.IsAlive())
                return;

            var userMoveLen = Math.Sqrt(_userMouseDeltaX * _userMouseDeltaX + _userMouseDeltaY * _userMouseDeltaY);
            if (userMoveLen > HumanReactThreshold)
                _lastSuppressed = DateTime.Now;

            if ((DateTime.Now - _lastSuppressed).TotalMilliseconds < SuppressMs)
                return;

            if (!IsCalibrated)
            {
                Calibrate();
                IsCalibrated = true;
            }

            if ((DateTime.Now - _lastAiUpdate).TotalMilliseconds > AimUpdateIntervalMs && _userMoveCount > 0)
            {
                _userMoveAvg = _userMoveSum / _userMoveCount;
                _aiAggressiveness = 1.0 - Math.Min(_userMoveAvg / 20.0, 0.7);
                _userMoveSum = 0;
                _userMoveCount = 0;
                _lastAiUpdate = DateTime.Now;
            }

            if (_aimTotalCount > 0 && (DateTime.Now - _lastAimEvent).TotalMilliseconds > AimEventWindowMs)
            {
                var successRate = _aimSuccessCount / (double)_aimTotalCount;
                if (successRate < 0.5)
                {
                    _dynamicFov = Math.Max(GraphicsMath.DegreeToRadian(5f), _dynamicFov - GraphicsMath.DegreeToRadian(0.5f));
                    _dynamicSmoothing = Math.Min(_dynamicSmoothing + 0.5, 10.0);
                }
                else if (successRate > 0.8)
                {
                    _dynamicFov = Math.Min(GraphicsMath.DegreeToRadian(30f), _dynamicFov + GraphicsMath.DegreeToRadian(0.5f));
                    _dynamicSmoothing = Math.Max(_dynamicSmoothing - 0.5, 1.0);
                }

                _aimSuccessCount = 0;
                _aimTotalCount = 0;
                _lastAimEvent = DateTime.Now;
            }

            var aimResult = GetAimTargetWithPrediction(_dynamicFov);
            Point aimPixels = Point.Empty;

            if (aimResult.Found)
            {
                GetAimPixels(aimResult.AimAngles, out aimPixels);

                _trainingData.Add((
                    targetPos: aimResult.TargetPosition,
                    playerPos: GameData.Player.EyePosition,
                    distance: aimResult.Distance,
                    angle: aimResult.AimAngles
                ));

                if (_trainingData.Count > MaxTrainingDataSize)
                    _trainingData.RemoveAt(0);
            }

            if (_neuralNetwork != null && _optimizer != null && _trainingData.Count > 10)
            {
                TrainNeuralNetwork();
            }

            if (aimResult.Found)
            {
                Vector2 neuralCorrection = Vector2.Zero;

                if (_neuralNetwork != null && _trainingData.Count > 50)
                {
                    try
                    {
                        var input = torch.tensor(new[]
                        {
                            aimResult.TargetPosition.X, aimResult.TargetPosition.Y, aimResult.TargetPosition.Z,
                            GameData.Player.EyePosition.X, GameData.Player.EyePosition.Y, GameData.Player.EyePosition.Z
                        }, dtype: ScalarType.Float32).reshape(1, 6);

                        using var prediction = _neuralNetwork.forward(input);
                        var predArray = prediction.data<float>().ToArray();
                        neuralCorrection = new Vector2(predArray[0], predArray[1]);
                    }
                    catch { /* ignore */ }
                }

                if (_aimTrainer != null)
                {
                    var statCorrection = _aimTrainer.GetCorrection(aimResult.Distance);
                    neuralCorrection.X += statCorrection.X;
                    neuralCorrection.Y += statCorrection.Y;
                }

                aimPixels.X = (int)Math.Round(aimPixels.X - neuralCorrection.X);
                aimPixels.Y = (int)Math.Round(aimPixels.Y - neuralCorrection.Y);
            }

            ApplyHumanizedAimAdjustments(ref aimPixels, aimResult.Found);

            aimPixels.X = Math.Max(Math.Min(aimPixels.X, 50), -50);
            aimPixels.Y = Math.Max(Math.Min(aimPixels.Y, 50), -50);

            var adapt = _aiAggressiveness;
            if ((DateTime.Now - _lastMouseMoveTime).TotalMilliseconds < UserMouseDeltaResetMs)
                adapt *= 0.5;

            aimPixels.X = (int)(aimPixels.X * adapt);
            aimPixels.Y = (int)(aimPixels.Y * adapt);

            var shouldWait = false;

            if (isAutoMode && aimResult.Found)
            {
                // Авто-режим: эмулируем нажатие ЛКМ с ограничением по скорости
                if ((DateTime.Now - _lastShotTime).TotalMilliseconds > MinShootIntervalMs)
                {
                    // Временно устанавливаем состояние, чтобы TryMouseDown сработал
                    lock (_stateLock)
                    {
                        _currentState = AimBotState.DownSuppressed;
                    }
                    shouldWait = TryMouseDown(); // ← вызовет MouseLeftDown()
                    _lastShotTime = DateTime.Now;
                }
            }
            else if (isManualMode)
            {
                // Ручной режим: стреляем по реальному нажатию игрока
                shouldWait = TryMouseDown();
            }

            shouldWait |= TryMouseMoveNew(aimPixels);

            if (shouldWait)
                Thread.Sleep(20);

            if (aimResult.Found)
                _aimSuccessCount++;

            if (aimResult.Found && _aimTrainer != null)
            {
                Thread.Sleep(40);

                if (GameData?.Player != null)
                {
                    var aimAfter = GameData.Player.EyeDirection;
                    var aimBefore = GameData.Player.AimDirection;

                    var yawBefore = GetYaw(aimBefore);
                    var yawAfter = GetYaw(aimAfter);
                    var pitchBefore = GetPitch(aimBefore);
                    var pitchAfter = GetPitch(aimAfter);

                    var deltaYaw = NormalizeRadians(yawAfter - yawBefore);
                    var deltaPitch = pitchAfter - pitchBefore;

                    var residualPixelsX = deltaYaw / _anglePerPixelHorizontal;
                    var residualPixelsY = deltaPitch / _anglePerPixelVertical;

                    _aimTrainer.AddObservation(aimResult.Distance, (float)residualPixelsX, (float)residualPixelsY);
                }
            }

            if (!aimResult.Found)
            {
                _activeTargetId = -1;
                _lastTargetLockTime = DateTime.MinValue;
            }

            _aimTotalCount++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AimBot ERROR] {ex.Message}\n{ex.StackTrace}");
        }
    }

    // ... остальные методы (GetAimTargetWithPrediction, GetAimAngles, GetAimPixels,
    // TryMouseMoveOld, TryMouseMoveNew, TryMouseDown, Calibrate, ApplyHumanizedAimAdjustments,
    // GetYaw, GetPitch, NormalizeRadians, TrainNeuralNetwork) остаются БЕЗ ИЗМЕНЕНИЙ ...

    // Ниже — копия ваших методов без изменений (они корректны)

    private AimTargetResult GetAimTargetWithPrediction(double customFov)
    {
        var minAngleSize = float.MaxValue;
        Vector2 bestAimAngles = new((float)Math.PI, (float)Math.PI);
        Vector3 bestAimPosition = Vector3.Zero;
        float bestDistance = 0f;
        int bestTargetId = -1;
        bool targetFound = false;

        if (GameData?.Player == null || GameData.Entities == null)
            return new AimTargetResult(false, Vector3.Zero, Vector2.Zero, 0f, -1);

        foreach (var entity in GameData.Entities.Where(entity =>
                    entity.IsAlive() &&
                    entity.AddressBase != GameData.Player.AddressBase &&
                    entity.Team != GameData.Player.Team &&
                    entity.IsSpotted))
        {
            Vector3? bestBonePos = null;
            Vector2 bestAngles = Vector2.Zero;
            float bestAngleSize = float.MaxValue;

            foreach (var bone in AimBonePriority)
            {
                if (!entity.BonePos.TryGetValue(bone, out var bonePos)) continue;

                var dt = (float)(DateTime.Now - _lastTargetUpdate).TotalSeconds;

                if (entity.Id != _lastTargetId)
                {
                    _lastTargetPos = bonePos;
                    _lastTargetVel = Vector3.Zero;
                }
                else if (dt > 0.001f && dt < 0.5f)
                {
                    _lastTargetVel = (bonePos - _lastTargetPos) / dt;
                    _lastTargetPos = bonePos;
                }

                _lastTargetId = entity.Id;
                _lastTargetUpdate = DateTime.Now;

                var distanceToTarget = Vector3.Distance(GameData.Player.EyePosition, bonePos);
                var dynamicPredictionTime = 0.05f + Math.Min(distanceToTarget / 1000f, 1f) * 0.15f;
                var predictedPos = bonePos + _lastTargetVel * dynamicPredictionTime;

                GetAimAngles(predictedPos, out var angleToBoneSize, out var anglesToBone);
                if (angleToBoneSize > customFov) continue;

                if (angleToBoneSize < bestAngleSize)
                {
                    bestAngleSize = angleToBoneSize;
                    bestAngles = anglesToBone;
                    bestBonePos = predictedPos;
                }
            }

            if (bestBonePos != null && bestAngleSize < minAngleSize)
            {
                minAngleSize = bestAngleSize;
                bestAimAngles = bestAngles;
                bestAimPosition = bestBonePos.Value;
                bestDistance = Vector3.Distance(GameData.Player.EyePosition, bestAimPosition);
                bestTargetId = entity.Id;
                targetFound = true;
            }
        }

        if (targetFound)
        {
            var smoothingAcceleration = Math.Max(1.0f, bestDistance / 100.0f);
            var currentSmoothing = AimBotSmoothing * smoothingAcceleration;
            currentSmoothing = Math.Min(currentSmoothing, 50.0f);
            bestAimAngles *= 1f / Math.Max(currentSmoothing, 1f);
        }

        _currentTargetId = targetFound ? bestTargetId : -1;
        if (targetFound)
            _lastTargetId = bestTargetId;

        return new AimTargetResult(targetFound, bestAimPosition, bestAimAngles, bestDistance, bestTargetId);
    }

    private void GetAimAngles(Vector3 pointWorld, out float angleSize, out Vector2 aimAngles)
    {
        aimAngles = Vector2.Zero;
        angleSize = 0f;

        if (GameData == null || GameData.Player == null) return;

        var aimDirection = GameData.Player.AimDirection;
        var aimDirectionDesired = (pointWorld - GameData.Player.EyePosition).GetNormalized();

        var horizontalAngle = aimDirectionDesired.GetSignedAngleTo(aimDirection, new Vector3(0, 0, 1));
        var verticalAngle = aimDirectionDesired.GetSignedAngleTo(aimDirection,
            Vector3.Cross(aimDirectionDesired, new Vector3(0, 0, 1)).GetNormalized());

        aimAngles = new Vector2(horizontalAngle, verticalAngle);
        angleSize = aimDirection.GetAngleTo(aimDirectionDesired);
    }

    private static void GetAimPixels(Vector2 aimAngles, out Point aimPixels)
    {
        var fovRatio = 90.0 / Player.Fov;
        aimPixels = new Point(
            (int)Math.Round(aimAngles.X / _anglePerPixelHorizontal * fovRatio),
            (int)Math.Round(aimAngles.Y / _anglePerPixelVertical * fovRatio)
        );
    }

    private static bool TryMouseMoveOld(Point aimPixels)
    {
        if (aimPixels.X == 0 && aimPixels.Y == 0) return false;
        if (Math.Abs(aimPixels.X) > 100 || Math.Abs(aimPixels.Y) > 100) return false;
        Utility.MouseMove(aimPixels.X, aimPixels.Y);
        return true;
    }

    private static bool TryMouseMoveNew(Point aimPixels)
    {
        if (aimPixels.X == 0 && aimPixels.Y == 0) return false;
        if (Math.Abs(aimPixels.X) > 100 || Math.Abs(aimPixels.Y) > 100) return false;
        Utility.WindMouseMove(0, 0, aimPixels.X, aimPixels.Y, 9.0, 3.0, 15.0, 12.0);
        return true;
    }

    private bool TryMouseDown()
    {
        var mouseDown = false;
        lock (_stateLock)
        {
            if (_currentState == AimBotState.DownSuppressed)
            {
                mouseDown = true;
                _currentState = AimBotState.Down;
            }
        }

        if (mouseDown) Utility.MouseLeftDown();
        return mouseDown;
    }

    private void Calibrate()
    {
        var horizontalSamples = new[]
        {
            CalibrationMeasureHorizontalAnglePerPixel(100),
            CalibrationMeasureHorizontalAnglePerPixel(-200),
            CalibrationMeasureHorizontalAnglePerPixel(300),
            CalibrationMeasureHorizontalAnglePerPixel(-400),
            CalibrationMeasureHorizontalAnglePerPixel(200)
        }.Where(sample => sample > 0).ToList();

        if (horizontalSamples.Count > 0)
            _anglePerPixelHorizontal = horizontalSamples.Average();

        var verticalSamples = new[]
        {
            CalibrationMeasureVerticalAnglePerPixel(60),
            CalibrationMeasureVerticalAnglePerPixel(-120),
            CalibrationMeasureVerticalAnglePerPixel(180),
            CalibrationMeasureVerticalAnglePerPixel(-240),
            CalibrationMeasureVerticalAnglePerPixel(120)
        }.Where(sample => sample > 0).ToList();

        if (verticalSamples.Count > 0)
            _anglePerPixelVertical = verticalSamples.Average();
        else
            _anglePerPixelVertical = _anglePerPixelHorizontal;
    }

    private double CalibrationMeasureHorizontalAnglePerPixel(int deltaPixels)
    {
        Thread.Sleep(100);
        if (GameData?.Player == null) return 0.0;

        var yawStart = GetYaw(GameData.Player.EyeDirection);
        Utility.MouseMove(deltaPixels, 0);
        Thread.Sleep(100);
        if (GameData?.Player == null) return 0.0;

        var yawEnd = GetYaw(GameData.Player.EyeDirection);
        return Math.Abs(NormalizeRadians(yawEnd - yawStart)) / Math.Abs(deltaPixels);
    }

    private double CalibrationMeasureVerticalAnglePerPixel(int deltaPixels)
    {
        Thread.Sleep(100);
        if (GameData?.Player == null) return 0.0;

        var pitchStart = GetPitch(GameData.Player.EyeDirection);
        Utility.MouseMove(0, deltaPixels);
        Thread.Sleep(100);
        if (GameData?.Player == null) return 0.0;

        var pitchEnd = GetPitch(GameData.Player.EyeDirection);
        return Math.Abs(pitchEnd - pitchStart) / Math.Abs(deltaPixels);
    }

    private void ApplyHumanizedAimAdjustments(ref Point aimPixels, bool hasTarget)
    {
        if (!hasTarget || _currentTargetId == -1)
        {
            _currentTargetId = -1;
            return;
        }

        if (_currentTargetId != _activeTargetId)
        {
            _activeTargetId = _currentTargetId;
            _lastTargetLockTime = DateTime.Now;
        }

        var distance = Math.Sqrt(aimPixels.X * (double)aimPixels.X + aimPixels.Y * (double)aimPixels.Y);
        if (distance > 0)
        {
            var gain = Math.Clamp(distance / HumanEaseDistancePixels, HumanMinimumGain, 1.0);
            aimPixels.X = (int)Math.Round(aimPixels.X * gain);
            aimPixels.Y = (int)Math.Round(aimPixels.Y * gain);
        }

        var lockDuration = (DateTime.Now - _lastTargetLockTime).TotalMilliseconds;
        if (lockDuration > LockJitterStartMs && distance < 8)
        {
            var jitterRange = lockDuration > LockJitterStrongMs ? 2 : 1;
            aimPixels.X += HumanizationRandom.Next(-jitterRange, jitterRange + 1);
            aimPixels.Y += HumanizationRandom.Next(-jitterRange, jitterRange + 1);
        }
    }

    private static double GetYaw(Vector3 direction)
    {
        return Math.Atan2(direction.Y, direction.X);
    }

    private static double GetPitch(Vector3 direction)
    {
        var clampedZ = Math.Clamp(direction.Z, -1f, 1f);
        return Math.Asin(-clampedZ);
    }

    private static double NormalizeRadians(double value)
    {
        const double twoPi = Math.PI * 2;
        value %= twoPi;
        if (value <= -Math.PI) value += twoPi;
        else if (value > Math.PI) value -= twoPi;
        return value;
    }

    private void TrainNeuralNetwork()
    {
        try
        {
            if (_neuralNetwork == null || _optimizer == null || _trainingData.Count < 10) return;

            var inputs = new List<float>();
            var targets = new List<float>();

            foreach (var data in _trainingData.TakeLast(100))
            {
                inputs.AddRange(new[]
                {
                    data.targetPos.X, data.targetPos.Y, data.targetPos.Z,
                    data.playerPos.X, data.playerPos.Y, data.playerPos.Z
                });

                targets.AddRange(new[] { data.angle.X, data.angle.Y });
            }

            if (inputs.Count == 0) return;

            var batchSize = inputs.Count / 6;
            var inputTensor = torch.tensor(inputs.ToArray(), dtype: ScalarType.Float32).reshape(batchSize, 6);
            var targetTensor = torch.tensor(targets.ToArray(), dtype: ScalarType.Float32).reshape(batchSize, 2);

            _optimizer.zero_grad();
            var predictions = _neuralNetwork.forward(inputTensor);
            var loss = functional.mse_loss(predictions, targetTensor);
            loss.backward();
            _optimizer.step();

            if (_trainingData.Count > 100)
                _trainingData.RemoveRange(0, _trainingData.Count - 100);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AimBot] Ошибка обучения нейронной сети: {ex.Message}");
        }
    }
}