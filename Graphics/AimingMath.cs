using System;
using System.Drawing;
using System.Numerics;
using CS2GameHelper.Data.Entity;
using CS2GameHelper.Data.Game;

namespace CS2GameHelper.Graphics
{
    public static class AimingMath
    {
        /// <summary>
        /// Рассчитывает углы прицеливания от игрока к точке в мире.
        /// </summary>
        public static void GetAimAngles(Player player, Vector3 pointWorld, out float angleSize, out Vector2 aimAngles)
        {
            aimAngles = Vector2.Zero;
            angleSize = 0f;

            var aimDirection = player.AimDirection;
            var aimDirectionDesired = (pointWorld - player.EyePosition).GetNormalized();

            var horizontalAngle = aimDirectionDesired.GetSignedAngleTo(aimDirection, new Vector3(0, 0, 1));
            var verticalAngle = aimDirectionDesired.GetSignedAngleTo(aimDirection,
                Vector3.Cross(aimDirectionDesired, new Vector3(0, 0, 1)).GetNormalized());

            aimAngles = new Vector2(horizontalAngle, verticalAngle);
            angleSize = aimDirection.GetAngleTo(aimDirectionDesired);
        }

        /// <summary>
        /// Конвертирует углы прицеливания в смещение в пикселях на экране.
        /// </summary>
        public static void GetAimPixels(Vector2 aimAngles, double anglePerPixelHorizontal, double anglePerPixelVertical, out Point aimPixels)
        {
            var fovRatio = 90.0 / Player.Fov;
            aimPixels = new Point(
                (int)Math.Round(aimAngles.X / anglePerPixelHorizontal * fovRatio),
                (int)Math.Round(aimAngles.Y / anglePerPixelVertical * fovRatio)
            );
        }

        public static double GetYaw(Vector3 direction) => Math.Atan2(direction.Y, direction.X);

        public static double GetPitch(Vector3 direction)
        {
            var clampedZ = Math.Clamp(direction.Z, -1f, 1f);
            return Math.Asin(-clampedZ);
        }

        public static double NormalizeRadians(double value)
        {
            const double twoPi = Math.PI * 2;
            value %= twoPi;
            if (value <= -Math.PI) value += twoPi;
            else if (value > Math.PI) value -= twoPi;
            return value;
        }
    }
}