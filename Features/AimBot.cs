using System;
using System.Drawing;
using System.Numerics;
using System.Threading;
using CS2GameHelper.Core;
using CS2GameHelper.Core.Data;
using CS2GameHelper.Data.Entity;
using CS2GameHelper.Data.Game;
using CS2GameHelper.Features.Aiming;
using CS2GameHelper.Graphics;
using CS2GameHelper.Utils;
using Point = System.Drawing.Point;
using Keys = CS2GameHelper.Utils.Keys;

namespace CS2GameHelper.Features
{
    public enum AimBotState { Up, DownSuppressed, Down }

    public record AimTargetResult(bool Found, Vector3 TargetPosition, Vector2 AimAngles, float Distance, int TargetId, Vector3 TargetVelocity);

    public class AimBot : ThreadedServiceBase
    {
        private const int SuppressMs = 200;
        private const int UserMouseDeltaResetMs = 50;
        private const int AimEventWindowMs = 1000;

        private readonly ConfigManager _config = ConfigManager.Load();
        private readonly object _stateLock = new();
        private readonly Random _humanizationRandom;

        // Кэшированные параметры настройки (читаются через _config.AimBotTuning).
        private readonly double _humanReactThreshold;
        private readonly double _humanEaseDistancePixels;
        private readonly double _humanMinimumGain;
        private readonly int _lockJitterStartMs;
        private readonly int _lockJitterStrongMs;
        private readonly int _minShootIntervalMs;
        private readonly int _aimUpdateIntervalMs;
        private readonly double _aimBotSmoothing;

        private static double _anglePerPixelHorizontal = 1;
        private static double _anglePerPixelVertical = 1;

        private readonly CompositeAimProvider _correctionProvider;
        private readonly TargetSelector _targetSelector;
        private readonly UserInputHandler _inputHandler;

        private AimBotState _currentState = AimBotState.Up;
        private double _aiAggressiveness = 1.0;
        private int _aimSuccessCount, _aimTotalCount;
        private double _dynamicFov = GraphicsMath.DegreeToRadian(15f);
        private double _dynamicSmoothing;
        private DateTime _lastAimEvent = DateTime.MinValue;
        private DateTime _lastAiUpdate = DateTime.MinValue;
        private DateTime _lastSuppressed = DateTime.MinValue;
        private int _activeTargetId = -1;
        private DateTime _lastTargetLockTime = DateTime.MinValue;
        private double _userMoveAvg, _userMoveSum;
        private int _userMoveCount;
        private DateTime _lastShotTime = DateTime.MinValue;
        private bool _isCalibrated;

        // Контекст прицеливания (для расчёта deltaTime/accel и ConfirmHit).
        private DateTime _lastFrameTime = DateTime.MinValue;
        private Vector3 _lastTargetVelocity = Vector3.Zero;
        private int _lastTargetIdForAccel = -1;
        private int _aimBotLastDamage;

        public AimBot(GameProcess gameProcess, GameData gameData, UserInputHandler inputHandler)
        {
            GameProcess = gameProcess;
            GameData = gameData;
            _inputHandler = inputHandler; // ← используем внешний handler

            var tuning = _config.AimBotTuning ?? new ConfigManager.AimBotTuningConfig();
            _humanReactThreshold = tuning.HumanReactThreshold;
            _humanEaseDistancePixels = tuning.HumanEaseDistancePixels;
            _humanMinimumGain = tuning.HumanMinimumGain;
            _lockJitterStartMs = tuning.LockJitterStartMs;
            _lockJitterStrongMs = tuning.LockJitterStrongMs;
            _minShootIntervalMs = tuning.MinShootIntervalMs;
            _aimUpdateIntervalMs = tuning.AimUpdateIntervalMs;
            _aimBotSmoothing = tuning.AimSmoothing;
            _dynamicSmoothing = _aimBotSmoothing;
            _humanizationRandom = tuning.HumanizationSeed > 0
                ? new Random(tuning.HumanizationSeed)
                : new Random();

            _correctionProvider = new CompositeAimProvider();
            _targetSelector = new TargetSelector();
            Console.WriteLine("[AimBot] Initialized with composite correction and shared input handler.");
        }

        protected override string ThreadName => nameof(AimBot);
        public GameProcess? GameProcess { get; set; }
        public GameData? GameData { get; set; }

        public override void Dispose()
        {
            _correctionProvider.Save();
            _correctionProvider.Dispose();
            // NOTE: _inputHandler is shared (owned by Program). Do NOT dispose it here,
            // otherwise hooks are unhooked while other features still need them and
            // Program.Dispose triggers a redundant unhook/finalizer pass.
            base.Dispose();
        }

        // Используем UserInputHandler для проверки хоткея (читаем из _config напрямую —
        // чтобы изменение AimBotKey через меню применялось без перезапуска).
        private bool IsHotKeyDown() => _inputHandler.IsKeyDown(_config.AimBotKey);

        private static bool TryMouseMoveNew(Point aimPixels)
        {
            if (aimPixels.X == 0 && aimPixels.Y == 0) return false;
            if (Math.Abs(aimPixels.X) > 100 || Math.Abs(aimPixels.Y) > 100) return false;
            Utility.WindMouseMove(aimPixels.X, aimPixels.Y, G_0: 9.0, W_0: 3.0, M_0: 15.0, D_0: 12.0);
            return true;
        }

        protected override void FrameAction()
        {
            bool isManualMode = IsHotKeyDown();
            bool isAutoMode = _config.AimBotAutoShoot;

            if (!isManualMode && !isAutoMode) return;

            try
            {
                if (GameProcess == null || !GameProcess.IsValid || GameData?.Player == null || !GameData.Player.IsAlive())
                    return;

                var userMoveLen = Math.Sqrt(_inputHandler.LastMouseDelta.X * (double)_inputHandler.LastMouseDelta.X + _inputHandler.LastMouseDelta.Y * (double)_inputHandler.LastMouseDelta.Y);
                if (userMoveLen > _humanReactThreshold) _lastSuppressed = DateTime.Now;
                if ((DateTime.Now - _lastSuppressed).TotalMilliseconds < SuppressMs) return;

                if (!_isCalibrated)
                {
                    Calibrate();
                    _isCalibrated = true;
                }

                if ((DateTime.Now - _lastAiUpdate).TotalMilliseconds > _aimUpdateIntervalMs && _userMoveCount > 0)
                {
                    _userMoveAvg = _userMoveSum / _userMoveCount;
                    _aiAggressiveness = 1.0 - Math.Min(_userMoveAvg / 20.0, 0.7);
                    _userMoveSum = 0; _userMoveCount = 0; _lastAiUpdate = DateTime.Now;
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
                    _aimSuccessCount = 0; _aimTotalCount = 0; _lastAimEvent = DateTime.Now;
                }

                var aimResult = _targetSelector.FindBestTarget(GameData, _dynamicFov);
                Point aimPixels = Point.Empty;

                // === Расчёт фич для AimContext ===
                var nowFrame = DateTime.UtcNow;
                float deltaTimeMs = _lastFrameTime == DateTime.MinValue ? 16f
                    : (float)Math.Clamp((nowFrame - _lastFrameTime).TotalMilliseconds, 1.0, 100.0);
                _lastFrameTime = nowFrame;
                float aimSpeed = (float)Math.Sqrt(
                    _inputHandler.LastMouseDelta.X * (double)_inputHandler.LastMouseDelta.X +
                    _inputHandler.LastMouseDelta.Y * (double)_inputHandler.LastMouseDelta.Y);

                float targetAccelMag = 0f;
                if (aimResult.Found)
                {
                    if (aimResult.TargetId == _lastTargetIdForAccel)
                    {
                        var dv = aimResult.TargetVelocity - _lastTargetVelocity;
                        targetAccelMag = dv.Length() / Math.Max(deltaTimeMs, 1f) * 1000f; // в unit/s^2
                    }
                    _lastTargetVelocity = aimResult.TargetVelocity;
                    _lastTargetIdForAccel = aimResult.TargetId;
                }
                else
                {
                    _lastTargetIdForAccel = -1;
                    _lastTargetVelocity = Vector3.Zero;
                }

                if (aimResult.Found)
                {
                    AimingMath.GetAimAngles(GameData.Player, aimResult.TargetPosition, out _, out var angles);
                    AimingMath.GetAimPixels(angles, _anglePerPixelHorizontal, _anglePerPixelVertical, out aimPixels);

                    var ctx = new AimContext(
                        aimResult.Distance,
                        aimResult.TargetPosition,
                        GameData.Player.EyePosition,
                        aimResult.TargetVelocity,
                        deltaTimeMs,
                        aimSpeed,
                        targetAccelMag);
                    var correction = _correctionProvider.GetCorrection(in ctx);

                    aimPixels.X = (int)Math.Round(aimPixels.X - correction.X);
                    aimPixels.Y = (int)Math.Round(aimPixels.Y - correction.Y);
                }

                ApplyHumanizedAimAdjustments(ref aimPixels, aimResult);

                aimPixels.X = Math.Max(Math.Min(aimPixels.X, 50), -50);
                aimPixels.Y = Math.Max(Math.Min(aimPixels.Y, 50), -50);

                var adapt = _aiAggressiveness;
                if ((DateTime.Now - _inputHandler.LastMouseMoveTime).TotalMilliseconds < UserMouseDeltaResetMs)
                    adapt *= 0.5;

                aimPixels.X = (int)(aimPixels.X * adapt);
                aimPixels.Y = (int)(aimPixels.Y * adapt);

                var shouldWait = false;

                if (isAutoMode && aimResult.Found && (DateTime.Now - _lastShotTime).TotalMilliseconds > _minShootIntervalMs)
                {
                    lock (_stateLock) { _currentState = AimBotState.DownSuppressed; }
                    shouldWait = TryMouseDown();
                    _lastShotTime = DateTime.Now;
                }
                else if (isManualMode)
                {
                    shouldWait = TryMouseDown();
                }

                shouldWait |= TryMouseMoveNew(aimPixels);

                if (shouldWait) Thread.Sleep(1); // минимальная пауза

                if (aimResult.Found) _aimSuccessCount++;

                // === СБОР ОСТАТКОВ БЕЗ SLEEP ===
                if (aimResult.Found && GameData?.Player != null)
                {
                    // 1. Сохраняем НАПРАВЛЕНИЕ ДО движения мыши
                    var aimDirectionBefore = GameData.Player.AimDirection;

                    // 2. Вычисляем ЖЕЛАЕМЫЙ вектор взгляда
                    var desiredDirection = (aimResult.TargetPosition - GameData.Player.EyePosition).GetNormalized();

                    // 3. Вычисляем УГЛОВУЮ ошибку (в радианах)
                    var horizontalError = desiredDirection.GetSignedAngleTo(aimDirectionBefore, new Vector3(0, 0, 1));
                    var verticalError = desiredDirection.GetSignedAngleTo(aimDirectionBefore,
                        Vector3.Cross(desiredDirection, new Vector3(0, 0, 1)).GetNormalized());

                    // 4. Конвертируем ошибку в ПИКСЕЛИ (как если бы мы её компенсировали)
                    var errorPixelsX = horizontalError / _anglePerPixelHorizontal;
                    var errorPixelsY = verticalError / _anglePerPixelVertical;

                    // 5. Но мы уже ВЫПОЛНИЛИ коррекцию через aimPixels!
                    //    Поэтому реальный остаток = ошибка ДО коррекции - то, что мы применили
                    var appliedCorrectionX = aimPixels.X;
                    var appliedCorrectionY = aimPixels.Y;

                    var residualX = (float)(errorPixelsX - appliedCorrectionX);
                    var residualY = (float)(errorPixelsY - appliedCorrectionY);

                    // 6. Добавляем наблюдение (пойдёт в PENDING; промоверится при ConfirmHit)
                    var obsCtx = new AimContext(
                        aimResult.Distance,
                        aimResult.TargetPosition,
                        GameData.Player.EyePosition,
                        aimResult.TargetVelocity,
                        deltaTimeMs,
                        aimSpeed,
                        targetAccelMag);
                    _correctionProvider.AddObservation(in obsCtx, residualX, residualY);
                }

                // === Детект попадания по дельте урона → ConfirmHit() для обучения ===
                TryConfirmHitFromDamage();

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

        private bool TryMouseDown()
        {
            var mouseDown = false;
            lock (_stateLock) { if (_currentState == AimBotState.DownSuppressed) { mouseDown = true; _currentState = AimBotState.Down; } }
            if (mouseDown) Utility.MouseLeftDown();
            return mouseDown;
        }

        private void ApplyHumanizedAimAdjustments(ref Point aimPixels, AimTargetResult aimResult)
        {
            if (!aimResult.Found)
            {
                _activeTargetId = -1;
                _lastTargetLockTime = DateTime.MinValue;
                return;
            }

            if (aimResult.TargetId != _activeTargetId)
            {
                _activeTargetId = aimResult.TargetId;
                _lastTargetLockTime = DateTime.Now;
            }

            var lockDuration = (DateTime.Now - _lastTargetLockTime).TotalMilliseconds;
            var pixelDistance = Math.Sqrt(aimPixels.X * (double)aimPixels.X + aimPixels.Y * (double)aimPixels.Y);

            if (pixelDistance > 0)
            {
                var gain = Math.Clamp(pixelDistance / _humanEaseDistancePixels, _humanMinimumGain, 1.0);
                aimPixels.X = (int)Math.Round(aimPixels.X * gain);
                aimPixels.Y = (int)Math.Round(aimPixels.Y * gain);
            }

            if (lockDuration > _lockJitterStartMs && pixelDistance < 8)
            {
                var jitterRange = lockDuration > _lockJitterStrongMs ? 2 : 1;
                aimPixels.X += _humanizationRandom.Next(-jitterRange, jitterRange + 1);
                aimPixels.Y += _humanizationRandom.Next(-jitterRange, jitterRange + 1);
            }
        }

        private void TryConfirmHitFromDamage()
        {
            try
            {
                if (GameProcess?.Process == null || GameProcess.ModuleClient == null) return;
                var localController = GameProcess.ModuleClient.Read<IntPtr>(Offsets.client_dll.dwLocalPlayerController);
                if (localController == IntPtr.Zero) return;
                var actionTracking = GameProcess.Read<IntPtr>(
                    IntPtr.Add(localController, Offsets.m_pActionTrackingServices));
                if (actionTracking == IntPtr.Zero) return;
                int currentDamage = GameProcess.Read<int>(
                    IntPtr.Add(actionTracking, Offsets.m_flTotalRoundDamageDealt));

                // Сначала калибруем (например, после перезахода в раунд damage может сброситься).
                if (currentDamage < _aimBotLastDamage)
                {
                    _aimBotLastDamage = currentDamage;
                    return;
                }

                if (currentDamage > _aimBotLastDamage)
                {
                    _correctionProvider.ConfirmHit();
                    _aimBotLastDamage = currentDamage;
                }
            }
            catch
            {
                // Чтение памяти может вернуть мусор — глушим, цикл аим-бота не должен падать.
            }
        }

        private void Calibrate()
        {
            var horizontalSamples = new[]
            {
                CalibrationMeasureHorizontalAnglePerPixel(100),
                CalibrationMeasureHorizontalAnglePerPixel(-200),
                CalibrationMeasureHorizontalAnglePerPixel(300)
            }.Where(sample => sample > 0).ToList();

            if (horizontalSamples.Count > 0)
                _anglePerPixelHorizontal = horizontalSamples.Average();

            var verticalSamples = new[]
            {
                CalibrationMeasureVerticalAnglePerPixel(60),
                CalibrationMeasureVerticalAnglePerPixel(-120),
                CalibrationMeasureVerticalAnglePerPixel(180)
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
            var yawStart = AimingMath.GetYaw(GameData.Player.EyeDirection);
            Utility.MouseMove(deltaPixels, 0);
            Thread.Sleep(100);
            if (GameData?.Player == null) return 0.0;
            var yawEnd = AimingMath.GetYaw(GameData.Player.EyeDirection);
            return Math.Abs(AimingMath.NormalizeRadians(yawEnd - yawStart)) / Math.Abs(deltaPixels);
        }

        private double CalibrationMeasureVerticalAnglePerPixel(int deltaPixels)
        {
            Thread.Sleep(100);
            if (GameData?.Player == null) return 0.0;
            var pitchStart = AimingMath.GetPitch(GameData.Player.EyeDirection);
            Utility.MouseMove(0, deltaPixels);
            Thread.Sleep(100);
            if (GameData?.Player == null) return 0.0;
            var pitchEnd = AimingMath.GetPitch(GameData.Player.EyeDirection);
            return Math.Abs(pitchEnd - pitchStart) / Math.Abs(deltaPixels);
        }
    }
}