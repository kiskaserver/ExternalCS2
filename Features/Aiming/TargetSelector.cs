using System.Linq;
using CS2GameHelper.Data.Entity;
using CS2GameHelper.Data.Game;
using CS2GameHelper.Graphics;
using System.Numerics;

namespace CS2GameHelper.Features.Aiming
{
    public class TargetSelector
    {
        private static readonly string[] AimBonePriority = { "head", "neck", "chest", "pelvis" };

        private int _lastTargetId = -1;
        private Vector3 _lastTargetPos = Vector3.Zero;
        private DateTime _lastTargetUpdate = DateTime.MinValue;
        private Vector3 _lastTargetVel = Vector3.Zero;

        public AimTargetResult FindBestTarget(GameData gameData, double customFov)
        {
            if (gameData?.Player == null || gameData.Entities == null)
                return new AimTargetResult(false, Vector3.Zero, Vector2.Zero, 0f, -1, Vector3.Zero); // <-- добавлен Vector3.Zero

            var minAngleSize = float.MaxValue;
            Vector2 bestAimAngles = new(float.Pi, float.Pi);
            Vector3 bestAimPosition = Vector3.Zero;
            float bestDistance = 0f;
            int bestTargetId = -1;
            Vector3 bestTargetVelocity = Vector3.Zero; // <-- ОБЪЯВЛЕНО!
            bool targetFound = false;

            foreach (var entity in gameData.Entities.Where(entity =>
                entity.IsAlive() &&
                entity.AddressBase != gameData.Player.AddressBase &&
                entity.Team != gameData.Player.Team &&
                entity.IsSpotted))
            {
                foreach (var bone in AimBonePriority)
                {
                    if (!entity.BonePos.TryGetValue(bone, out var bonePos)) continue;

                    Vector3 predictedPos = bonePos;
                    Vector3 targetVelocity = Vector3.Zero;
                    var dt = (float)(DateTime.Now - _lastTargetUpdate).TotalSeconds;

                    if (entity.Id != _lastTargetId)
                    {
                        _lastTargetPos = bonePos;
                        _lastTargetVel = Vector3.Zero;
                    }
                    else if (dt > 0.001f && dt < 0.5f)
                    {
                        _lastTargetVel = (bonePos - _lastTargetPos) / dt;
                        targetVelocity = _lastTargetVel;
                    }

                    _lastTargetId = entity.Id;
                    _lastTargetUpdate = DateTime.Now;
                    _lastTargetPos = bonePos;

                    var distanceToTarget = Vector3.Distance(gameData.Player.EyePosition, bonePos);
                    var dynamicPredictionTime = 0.05f + Math.Min(distanceToTarget / 1000f, 1f) * 0.15f;
                    predictedPos = bonePos + _lastTargetVel * dynamicPredictionTime;

                    AimingMath.GetAimAngles(gameData.Player, predictedPos, out var angleToBoneSize, out var anglesToBone);
                    if (angleToBoneSize > customFov) continue;

                    if (angleToBoneSize < minAngleSize)
                    {
                        minAngleSize = angleToBoneSize;
                        bestAimAngles = anglesToBone;
                        bestAimPosition = predictedPos;
                        bestDistance = distanceToTarget;
                        bestTargetId = entity.Id;
                        bestTargetVelocity = targetVelocity; // <-- ИСПОЛЬЗУЕТСЯ!
                        targetFound = true;
                    }
                }
            }

            return new AimTargetResult(targetFound, bestAimPosition, bestAimAngles, bestDistance, bestTargetId, bestTargetVelocity); // <-- передано!
        }
    }
}