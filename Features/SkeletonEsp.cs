using System.Numerics;
using CS2GameHelper.Core.Data;
using CS2GameHelper.Data.Entity;
using CS2GameHelper.Graphics;

namespace CS2GameHelper.Features;

public static class SkeletonEsp
{
    private static readonly (string Start, string End)[] BoneConnections =
    [
        // Spine chain
        ("head", "neck_0"),
        ("neck_0", "spine_1"),
        ("spine_1", "spine_2"),
        ("spine_2", "pelvis"),

        // Left arm chain
        ("spine_1", "arm_upper_L"),
        ("arm_upper_L", "arm_lower_L"),
        ("arm_lower_L", "hand_L"),

        // Right arm chain
        ("spine_1", "arm_upper_R"),
        ("arm_upper_R", "arm_lower_R"),
        ("arm_lower_R", "hand_R"),

        // Left leg chain
        ("pelvis", "leg_upper_L"),
        ("leg_upper_L", "leg_lower_L"),
        ("leg_lower_L", "ankle_L"),

        // Right leg chain
        ("pelvis", "leg_upper_R"),
        ("leg_upper_R", "leg_lower_R"),
        ("leg_lower_R", "ankle_R")
    ];

    public static void Draw(ModernGraphics graphics)
    {
        var player = graphics.GameData.Player;
        var entities = graphics.GameData.Entities;
        if (player == null || entities == null)
        {
            return;
        }

        foreach (var entity in entities)
        {
            if (!IsValidEntity(entity, player)) continue;

            var color = GetTeamColor(entity.Team);
            DrawSkeleton(graphics, entity, color);
        }
    }

    private static bool IsValidEntity(Entity entity, Player player)
    {
        return entity.IsAlive() &&
               entity.AddressBase != player.AddressBase;
    }

    private static uint GetTeamColor(Team team)
    {
        return team == Team.Terrorists ? 0xFFFFFF00 : 0xFF0000FF; // Yellow : Blue
    }

    private static void DrawSkeleton(ModernGraphics graphics, Entity entity, uint color)
    {
        var bonePositions = entity.BonePos;
        if (bonePositions == null) return;

        foreach (var (startBone, endBone) in BoneConnections)
        {
            if (!bonePositions.ContainsKey(startBone) || !bonePositions.ContainsKey(endBone))
                continue;

            graphics.DrawLineWorld(color, bonePositions[startBone], bonePositions[endBone]);
        }
    }
}