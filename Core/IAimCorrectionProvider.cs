using System.Numerics;

namespace CS2GameHelper.Core
{
    public interface IAimCorrectionProvider
    {
        Vector2 GetCorrection(float distance, Vector3 targetPos, Vector3 playerPos, Vector3 targetVelocity);
        void AddObservation(float distance, Vector3 targetPos, Vector3 playerPos, Vector3 targetVelocity, float residualX, float residualY);
        void Save();
    }
}