using System.Media;
using System.Numerics;
using CS2GameHelper.Core.Data;
using CS2GameHelper.Data.Game;
using CS2GameHelper.Graphics;
using CS2GameHelper.Utils;

namespace CS2GameHelper.Data.Entity;

// <<< НОВОЕ: Класс для хранения "снимка" состояния для рендера
public class RenderSnapshot
{
    public Vector3 Position { get; set; }
    public Matrix4x4 MatrixViewProjection { get; set; }
    public Vector3 ViewAngles { get; set; }
}

public class Player : EntityBase
{
    // <<< НОВОЕ: Объект для блокировки. Он нужен для потокобезопасности.
    private readonly object _renderDataLock = new object();

    // <<< ИЗМЕНЕНО: Сделаем свойства public, чтобы к ним был доступ из других классов (например, из ESP)
    public Matrix4x4 MatrixViewProjection { get; set; }
    public Matrix4x4 MatrixViewport { get; private set; }
    public Matrix4x4 MatrixViewProjectionViewport { get; set; }
    private Vector3 ViewOffset { get; set; }
    public Vector3 EyePosition { get; private set; }
    public Vector3 ViewAngles { get; private set; } // Сделаем private, т.к. будем использовать снепшот
    public Vector3 AimPunchAngle { get; private set; }
    public Vector3 AimDirection { get; private set; }
    public Vector3 EyeDirection { get; private set; }
    public static int Fov => 90;
    public int FFlags { get; private set; }

    private int PreviousTotalHits { get; set; }

    // <<< НОВОЕ: Добавляем свойство для атомарного снепшота
    public RenderSnapshot RenderData { get; private set; } = new RenderSnapshot();

    // <<< НОВОЕ: Предоставляем доступ к объекту блокировки извне
    public object RenderDataLock => _renderDataLock;

    protected override IntPtr ReadControllerBase(GameProcess gameProcess)
    {
        if (gameProcess.ModuleClient == null)
            throw new ArgumentNullException(nameof(gameProcess.ModuleClient), "ModuleClient cannot be null.");
        return gameProcess.ModuleClient.Read<IntPtr>(Offsets.dwLocalPlayerController);
    }

    protected override IntPtr ReadAddressBase(GameProcess gameProcess)
    {
        if (gameProcess.ModuleClient == null)
            throw new ArgumentNullException(nameof(gameProcess.ModuleClient), "ModuleClient cannot be null.");
        return gameProcess.ModuleClient.Read<IntPtr>(Offsets.dwLocalPlayerPawn);
    }

    public override bool Update(GameProcess gameProcess)
    {
        if (!base.Update(gameProcess)) return false;

        if (gameProcess.ModuleClient == null)
            throw new ArgumentNullException(nameof(gameProcess.ModuleClient), "ModuleClient cannot be null.");
        MatrixViewProjection = Matrix4x4.Transpose(gameProcess.ModuleClient.Read<Matrix4x4>(Offsets.dwViewMatrix));
        MatrixViewport = Utility.GetMatrixViewport(gameProcess.WindowRectangleClient.Size);
        MatrixViewProjectionViewport = MatrixViewProjection * MatrixViewport;

        if (gameProcess.Process == null)
            throw new ArgumentNullException(nameof(gameProcess.Process), "Process cannot be null.");

        ViewOffset = gameProcess.Process.Read<Vector3>(AddressBase + Offsets.m_vecViewOffset);
        EyePosition = Origin + ViewOffset;
        ViewAngles = gameProcess.ModuleClient.Read<Vector3>(Offsets.dwViewAngles);
        AimPunchAngle = gameProcess.Process.Read<Vector3>(AddressBase + Offsets.m_AimPunchAngle);
        FFlags = gameProcess.Process.Read<int>(AddressBase + Offsets.m_fFlags);

        EyeDirection =
            GraphicsMath.GetVectorFromEulerAngles(GraphicsMath.DegreeToRadian(ViewAngles.X), GraphicsMath.DegreeToRadian(ViewAngles.Y));
        AimDirection = GraphicsMath.GetVectorFromEulerAngles
        (
            GraphicsMath.DegreeToRadian(ViewAngles.X + AimPunchAngle.X * Offsets.WeaponRecoilScale),
            GraphicsMath.DegreeToRadian(ViewAngles.Y + AimPunchAngle.Y * Offsets.WeaponRecoilScale)
        );

        try
        {
            var totalHits = gameProcess.Process.Read<int>
            (
                gameProcess.Process.Read<IntPtr>(AddressBase + 0x1518) + 0x40
            );

            if (totalHits != PreviousTotalHits && totalHits > 0)
            {
                using var player = new SoundPlayer("hit.wav");
                player.Play();
            }

            PreviousTotalHits = totalHits;
        }
        catch (Exception)
        {
            // ignored
        }

        // <<< ИЗМЕНЕНО: Создаем атомарный снепшот внутри блока lock
        // Это гарантирует, что все данные в снепшоте относятся к одному и тому же моменту времени
        // и другой поток не сможет прочитать их "полураспавшимися".
        lock (_renderDataLock)
        {
            RenderData.Position = this.Position;
            RenderData.MatrixViewProjection = this.MatrixViewProjection;
            RenderData.ViewAngles = this.ViewAngles;
        }

        return true;
    }

    public bool IsGrenade()
    {
        return new HashSet<string>
        {
            nameof(WeaponIndexes.Smokegrenade), nameof(WeaponIndexes.Flashbang), nameof(WeaponIndexes.Hegrenade),
            nameof(WeaponIndexes.Molotov)
        }.Contains(CurrentWeaponName);
    }
}