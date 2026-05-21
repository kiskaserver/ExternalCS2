using System.Numerics;

namespace CS2GameHelper.Core
{
    public interface IAimCorrectionProvider
    {
        Vector2 GetCorrection(in AimContext ctx);

        /// <summary>
        /// Регистрирует «ожидающее» наблюдение. Оно НЕ попадёт в обучение,
        /// пока не будет вызван <see cref="ConfirmHit"/> в течение короткого окна,
        /// что фильтрует промахи и оставляет только данные подтверждённых попаданий.
        /// </summary>
        void AddObservation(in AimContext ctx, float residualX, float residualY);

        /// <summary>
        /// Подтверждает попадание: переносит свежие pending-наблюдения в обучение.
        /// </summary>
        void ConfirmHit();

        void Save();
    }
}