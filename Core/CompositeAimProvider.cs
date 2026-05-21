using System;
using System.Numerics;

namespace CS2GameHelper.Core
{
    public class CompositeAimProvider : IAimCorrectionProvider, IDisposable
    {
        private readonly StatisticalAimProvider _stat;
        private readonly NeuralAimProvider _neural;

        public CompositeAimProvider()
        {
            _stat = new StatisticalAimProvider();
            _neural = new NeuralAimProvider();
        }

        public Vector2 GetCorrection(in AimContext ctx)
        {
            var stat = _stat.GetCorrection(in ctx);
            var neural = _neural.GetCorrection(in ctx);
            return stat + neural;
        }

        public void AddObservation(in AimContext ctx, float residualX, float residualY)
        {
            _stat.AddObservation(in ctx, residualX, residualY);
            _neural.AddObservation(in ctx, residualX, residualY);
        }

        public void ConfirmHit()
        {
            _stat.ConfirmHit();
            _neural.ConfirmHit();
        }

        public void Save()
        {
            _stat.Save();
            _neural.Save();
        }

        public void Dispose()
        {
            (_stat as IDisposable)?.Dispose();
            _neural.Dispose();
        }
    }
}