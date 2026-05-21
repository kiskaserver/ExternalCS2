using System.Numerics;

namespace CS2GameHelper.Core
{
    /// <summary>
    /// Полный контекст для коррекции прицеливания, передаваемый в провайдеры.
    /// 10 признаков: 3D-смещение, дистанция, скорость цели, временной шаг,
    /// скорость движения мыши игрока, модуль ускорения цели.
    /// </summary>
    public readonly struct AimContext
    {
        public AimContext(
            float distance,
            Vector3 targetPos,
            Vector3 playerPos,
            Vector3 targetVelocity,
            float deltaTimeMs,
            float aimSpeed,
            float targetAccelMag)
        {
            Distance = distance;
            TargetPos = targetPos;
            PlayerPos = playerPos;
            TargetVelocity = targetVelocity;
            DeltaTimeMs = deltaTimeMs;
            AimSpeed = aimSpeed;
            TargetAccelMag = targetAccelMag;
        }

        public float Distance { get; }
        public Vector3 TargetPos { get; }
        public Vector3 PlayerPos { get; }
        public Vector3 TargetVelocity { get; }
        public float DeltaTimeMs { get; }
        public float AimSpeed { get; }
        public float TargetAccelMag { get; }
    }
}
