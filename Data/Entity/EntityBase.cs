using CS2GameHelper.Core.Data;
using System.Numerics;
using CS2GameHelper.Data.Game;
using CS2GameHelper.Utils;

namespace CS2GameHelper.Data.Entity;

[Flags]
public enum EntityFlags
{
    None = 0,
    OnGround = 1,
    PartiallyInWater = 2,
    FullyInWater = 4,
    NoInterpolation = 8,
    OnFire = 16,
    StaticProp = 32
}

public abstract class EntityBase
{
    protected IntPtr EntityList { get; private set; }
    protected IntPtr ControllerBase { get; private set; }
    public IntPtr AddressBase { get; private set; }

    private bool LifeState { get; set; }
    public int Health { get; private set; }
    public Team Team { get; private set; }
    protected Vector3 Origin { get; private set; }
    public int ShotsFired { get; private set; }

    // === НОВЫЕ ПОЛЯ ===
    public Vector3 Position => Origin; // Position = Origin в CS2
    public int Armor { get; private set; }
    public bool HasHelmet { get; private set; }
    public bool IsVisible { get; private set; }
    public EntityFlags Flags { get; private set; }
    public Vector2? ViewAngle { get; private set; }
    public bool IsReloading { get; private set; }
    public bool IsDefusing { get; private set; }

    public IntPtr ObserverTarget { get; private set; }

    private IntPtr CurrentWeapon { get; set; }
    public string CurrentWeaponName { get; private set; } = null!;

    private short WeaponIndex { get; set; }
    public Vector3 Velocity { get; private set; }

    public virtual bool IsAlive()
    {
        return ControllerBase != IntPtr.Zero &&
               AddressBase != IntPtr.Zero &&
               LifeState &&
               Health > 0 &&
               Team is Team.Terrorists or Team.CounterTerrorists;
    }

    protected abstract IntPtr ReadControllerBase(GameProcess gameProcess);
    protected abstract IntPtr ReadAddressBase(GameProcess gameProcess);

    public virtual bool Update(GameProcess gameProcess)
    {
        if (gameProcess.ModuleClient == null)
            throw new ArgumentNullException(nameof(gameProcess.ModuleClient), "ModuleClient cannot be null.");
        EntityList = gameProcess.ModuleClient.Read<IntPtr>(Offsets.dwEntityList);
        ControllerBase = ReadControllerBase(gameProcess);
        AddressBase = ReadAddressBase(gameProcess);
        if (ControllerBase == IntPtr.Zero || AddressBase == IntPtr.Zero) return false;

        if (gameProcess.Process == null)
            throw new ArgumentNullException(nameof(gameProcess.Process), "Process cannot be null.");

        LifeState = gameProcess.Process.Read<bool>(AddressBase + Offsets.m_lifeState);
        Health = gameProcess.Process.Read<int>(AddressBase + Offsets.m_iHealth);
        Team = gameProcess.Process.Read<int>(AddressBase + Offsets.m_iTeamNum).ToTeam();
        Origin = gameProcess.Process.Read<Vector3>(AddressBase + Offsets.m_vOldOrigin);
        ShotsFired = gameProcess.Process.Read<int>(AddressBase + Offsets.m_iShotsFired);

        // === Читаем недостающие поля ===
        Armor = gameProcess.Process.Read<int>(AddressBase + Offsets.m_ArmorValue);
        HasHelmet = gameProcess.Process.Read<bool>(AddressBase + Offsets.m_bHasHelmet);
        IsVisible = gameProcess.Process.Read<bool>(AddressBase + Offsets.m_bSpotted);
        Flags = (EntityFlags)gameProcess.Process.Read<int>(AddressBase + Offsets.m_fFlags);
        IsDefusing = gameProcess.Process.Read<bool>(AddressBase + Offsets.m_bBeingDefused);

        // ViewAngle (углы камеры)
        try
        {
            var viewPunch = gameProcess.Process.Read<Vector2>(AddressBase + Offsets.m_angEyeAngles);
            ViewAngle = viewPunch;
        }
        catch
        {
            ViewAngle = null;
        }

        // Оружие
        try
        {
            CurrentWeapon = gameProcess.Process.Read<IntPtr>(AddressBase + Offsets.m_pClippingWeapon);
            var weaponIndexAddress = CurrentWeapon + Offsets.m_AttributeManager + Offsets.m_Item + Offsets.m_iItemDefinitionIndex;
            WeaponIndex = gameProcess.Process.Read<short>(weaponIndexAddress);

            var name = Enum.GetName(typeof(WeaponIndexes), WeaponIndex);
            CurrentWeaponName = string.IsNullOrEmpty(name) ? $"weapon_{WeaponIndex}" : name;
        }
        catch
        {
            CurrentWeapon = IntPtr.Zero;
            WeaponIndex = -1;
            CurrentWeaponName = string.Empty;
        }

        Velocity = gameProcess.Process.Read<Vector3>(AddressBase + Offsets.m_vecAbsVelocity);

        if (gameProcess.Process != null)
        {
            ObserverTarget = gameProcess.Process.Read<IntPtr>(AddressBase + Offsets.m_hObserverTarget);
        }


        return true;
    }


}