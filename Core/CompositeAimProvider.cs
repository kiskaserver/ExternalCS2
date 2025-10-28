using System.Numerics;

namespace CS2GameHelper.Core
{
    public class CompositeAimProvider : IAimCorrectionProvider
    {
        private readonly StatisticalAimProvider _stat;
        private readonly NeuralAimProvider _neural;

        public CompositeAimProvider()
        {
            _stat = new StatisticalAimProvider();
            _neural = new NeuralAimProvider();
        }

        public Vector2 GetCorrection(float distance, Vector3 targetPos, Vector3 playerPos, Vector3 targetVelocity)
        {
            var stat = _stat.GetCorrection(distance, targetPos, playerPos, targetVelocity);
            var neural = _neural.GetCorrection(distance, targetPos, playerPos, targetVelocity);
            return stat + neural;
        }

        public void AddObservation(float distance, Vector3 targetPos, Vector3 playerPos, Vector3 targetVelocity, float residualX, float residualY)
        {
            _stat.AddObservation(distance, targetPos, playerPos, targetVelocity, residualX, residualY);
            _neural.AddObservation(distance, targetPos, playerPos, targetVelocity, residualX, residualY);
        }

        public void Save()
        {
            _stat.Save();
            _neural.Save();
        }

        public void Dispose()
        {
            _neural.Dispose();
        }
    }
}