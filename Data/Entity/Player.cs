﻿using System.Media;
using System.Numerics;
using CS2GameHelper.Core.Data;
using CS2GameHelper.Data.Game;
using CS2GameHelper.Graphics;
using CS2GameHelper.Utils;

namespace CS2GameHelper.Data.Entity;

public class Player : EntityBase
{
    private Matrix4x4 MatrixViewProjection { get; set; }
    public Matrix4x4 MatrixViewport { get; private set; }
    public Matrix4x4 MatrixViewProjectionViewport { get; private set; }
    private Vector3 ViewOffset { get; set; }
    public Vector3 EyePosition { get; private set; }
    private Vector3 ViewAngles { get; set; }
    public Vector3 AimPunchAngle { get; private set; }
    public Vector3 AimDirection { get; private set; }
    public Vector3 EyeDirection { get; private set; }
    public static int Fov => 90;
    public int FFlags { get; private set; }

    private int PreviousTotalHits { get; set; }

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