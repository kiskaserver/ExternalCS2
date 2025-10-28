using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Net.Http;
using System.Text.Json;

namespace CS2GameHelper.Utils;

public abstract class Offsets
{
    #region offsets

    public const float WeaponRecoilScale = 2f;
    public static int dwLocalPlayerPawn;
    public static int m_vOldOrigin;
    public static int m_vecViewOffset;
    public static int m_AimPunchAngle;
    public static int m_modelState;
    public static int m_pGameSceneNode;
    public static int m_fFlags;
    public static int m_iIDEntIndex;
    public static int m_lifeState;
    public static int m_iHealth;
    public static int m_iTeamNum;
    public static int m_iNumRoundKills;
    public static int m_flTotalRoundDamageDealt;
    public static int m_hLastAttacker;
    public static int m_flDeathInfoTime;
    public static int dwEntityList;
    public static int m_bDormant;
    public static int m_iShotsFired;
    public static int m_hPawn;
    public static int m_hObserverTarget;
    public static int dwLocalPlayerController;
    public static int dwViewMatrix;
    public static int dwViewAngles;
    public static int m_entitySpottedState;
    public static int m_Item;
    public static int m_pClippingWeapon;
    public static int m_pActionTrackingServices;
    public static int m_AttributeManager;
    public static int m_iItemDefinitionIndex;
    public static int m_bIsScoped;
    public static int m_flFlashDuration;
    public static int m_iszPlayerName;
    public static int dwPlantedC4;
    public static int dwGlobalVars;
    public static int m_nBombSite;
    public static int m_bBombDefused;
    public static int m_vecAbsVelocity;
    public static int m_flDefuseCountDown;
    public static int m_flC4Blow;
    public static int m_bBeingDefused;
    public const nint m_nCurrentTickThisFrame = 0x34;
    public static int m_ArmorValue;
    public static int m_bHasHelmet;
    public static int m_bSpotted;
    public static int m_bInReload;
    public static int m_angEyeAngles;
    public static int m_CBodyComponent;
    public static int m_bGameRestart;

    public static class engine2_dll
    {
        public static int dwNetworkGameClient;
        public static int dwNetworkGameClient_serverTickCount;
        public static int dwBuildNumber;
        public static int dwWindowHeight;
        public static int dwWindowWidth;
    }

    public static class client_dll
    {
        public static int dwPlantedC4;
        public static int dwLocalPlayerPawn;
        public static int dwViewAngles;
        public static int dwViewMatrix;
        public static int dwGameRules;
        public static int dwLocalPlayerController;
    }

    public static int m_vecOrigin;

    public static readonly Dictionary<string, int> Bones = new()
    {
        { "head", 6 },
        { "neck_0", 5 },
        { "spine_1", 4 },
        { "spine_2", 2 },
        { "pelvis", 0 },
        { "arm_upper_L", 8 },
        { "arm_lower_L", 9 },
        { "hand_L", 10 },
        { "arm_upper_R", 13 },
        { "arm_lower_R", 14 },
        { "hand_R", 15 },
        { "leg_upper_L", 22 },
        { "leg_lower_L", 23 },
        { "ankle_L", 24 },
        { "leg_upper_R", 25 },
        { "leg_lower_R", 26 },
        { "ankle_R", 27 }
    };

    public static async Task UpdateOffsets()
    {
        try
        {
            var offsetsJson = await FetchJson("https://raw.githubusercontent.com/sezzyaep/CS2-OFFSETS/refs/heads/main/offsets.json");
            var clientJson = await FetchJson("https://raw.githubusercontent.com/sezzyaep/CS2-OFFSETS/refs/heads/main/client_dll.json");

            using var offsetsDoc = JsonDocument.Parse(offsetsJson);
            using var clientDoc = JsonDocument.Parse(clientJson);

            var root = offsetsDoc.RootElement;
            if (!root.TryGetProperty("client.dll", out var clientDllElem))
                throw new InvalidOperationException("offsets.json is missing 'client.dll'");
            if (!root.TryGetProperty("engine2.dll", out var engine2Elem))
                throw new InvalidOperationException("offsets.json is missing 'engine2.dll'");

            var clientRoot = clientDoc.RootElement;
            if (!clientRoot.TryGetProperty("client.dll", out var clientContainer))
                throw new InvalidOperationException("client_dll.json is missing 'client.dll'");
            if (!clientContainer.TryGetProperty("classes", out var classesElem))
                throw new InvalidOperationException("client_dll.json is missing 'classes'");

            var classes = new Dictionary<string, Dictionary<string, int>>();
            foreach (var classProp in classesElem.EnumerateObject())
            {
                var className = classProp.Name;
                var fields = new Dictionary<string, int>();
                if (classProp.Value.TryGetProperty("fields", out var fieldsElem))
                {
                    foreach (var field in fieldsElem.EnumerateObject())
                    {
                        fields[field.Name] = field.Value.GetInt32();
                    }
                }
                classes[className] = fields;
            }

            dynamic destData = new ExpandoObject();

            // === Глобальные смещения ===
            destData.dwBuildNumber = GetInt(engine2Elem, "dwBuildNumber");
            destData.dwNetworkGameClient = GetInt(engine2Elem, "dwNetworkGameClient");
            destData.dwNetworkGameClient_serverTickCount = GetInt(engine2Elem, "dwNetworkGameClient_deltaTick");
            destData.dwWindowHeight = GetInt(engine2Elem, "dwWindowHeight");
            destData.dwWindowWidth = GetInt(engine2Elem, "dwWindowWidth");
            destData.dwLocalPlayerController = GetInt(clientDllElem, "dwLocalPlayerController");
            destData.dwEntityList = GetInt(clientDllElem, "dwEntityList");
            destData.dwViewMatrix = GetInt(clientDllElem, "dwViewMatrix");
            destData.dwPlantedC4 = GetInt(clientDllElem, "dwPlantedC4");
            destData.dwLocalPlayerPawn = GetInt(clientDllElem, "dwLocalPlayerPawn");
            destData.dwViewAngles = GetInt(clientDllElem, "dwViewAngles");
            destData.dwGlobalVars = GetInt(clientDllElem, "dwGlobalVars");
            destData.dwGameRules = GetInt(clientDllElem, "dwGameRules");

            // === Поля классов ===
            destData.m_fFlags = GetField(classes, "C_BaseEntity", "m_fFlags");
            destData.m_vOldOrigin = GetField(classes, "C_BasePlayerPawn", "m_vOldOrigin");
            destData.m_vecViewOffset = GetField(classes, "C_BaseModelEntity", "m_vecViewOffset");
            destData.m_aimPunchAngle = GetField(classes, "C_CSPlayerPawn", "m_aimPunchAngle");
            destData.m_modelState = GetField(classes, "CSkeletonInstance", "m_modelState");
            destData.m_pGameSceneNode = GetField(classes, "C_BaseEntity", "m_pGameSceneNode");
            destData.m_iIDEntIndex = GetField(classes, "C_CSPlayerPawn", "m_iIDEntIndex");
            destData.m_lifeState = GetField(classes, "C_BaseEntity", "m_lifeState");
            destData.m_iHealth = GetField(classes, "C_BaseEntity", "m_iHealth");
            destData.m_iTeamNum = GetField(classes, "C_BaseEntity", "m_iTeamNum");
            destData.m_bDormant = GetField(classes, "CGameSceneNode", "m_bDormant");
            destData.m_iShotsFired = GetField(classes, "C_CSPlayerPawn", "m_iShotsFired");
            destData.m_hPawn = GetField(classes, "CBasePlayerController", "m_hPawn");
            destData.m_hObserverTarget = GetField(classes, "CPlayer_ObserverServices", "m_hObserverTarget");
            destData.m_entitySpottedState = GetField(classes, "C_CSPlayerPawn", "m_entitySpottedState");
            destData.m_Item = GetField(classes, "C_AttributeContainer", "m_Item");
            destData.m_pClippingWeapon = GetField(classes, "C_CSPlayerPawn", "m_pClippingWeapon");
            destData.m_AttributeManager = GetField(classes, "C_EconEntity", "m_AttributeManager");
            destData.m_iItemDefinitionIndex = GetField(classes, "C_EconItemView", "m_iItemDefinitionIndex");
            destData.m_bIsScoped = GetField(classes, "C_CSPlayerPawn", "m_bIsScoped");
            destData.m_flFlashDuration = GetField(classes, "C_CSPlayerPawnBase", "m_flFlashDuration");
            destData.m_iszPlayerName = GetField(classes, "CBasePlayerController", "m_iszPlayerName");
            destData.m_nBombSite = GetField(classes, "C_PlantedC4", "m_nBombSite");
            destData.m_bBombDefused = GetField(classes, "C_PlantedC4", "m_bBombDefused");
            destData.m_vecAbsVelocity = GetField(classes, "C_BaseEntity", "m_vecAbsVelocity");
            destData.m_flDefuseCountDown = GetField(classes, "C_PlantedC4", "m_flDefuseCountDown");
            destData.m_flC4Blow = GetField(classes, "C_PlantedC4", "m_flC4Blow");
            destData.m_bBeingDefused = GetField(classes, "C_PlantedC4", "m_bBeingDefused");
            destData.m_bGameRestart = GetField(classes, "C_CSGameRules", "m_bGameRestart");

            destData.m_ArmorValue = GetField(classes, "C_CSPlayerPawn", "m_ArmorValue");
            destData.m_bHasHelmet = GetField(classes, "CCSPlayer_ItemServices", "m_bHasHelmet");
            destData.m_bSpotted = GetField(classes, "EntitySpottedState_t", "m_bSpotted");
            destData.m_angEyeAngles = GetField(classes, "C_CSPlayerPawn", "m_angEyeAngles");
            destData.m_CBodyComponent = GetField(classes, "C_BaseEntity", "m_CBodyComponent");
            destData.m_vecOrigin = GetField(classes, "CGameSceneNode", "m_vecOrigin");
            destData.m_iNumRoundKills = GetField(classes, "CCSPlayerController_ActionTrackingServices", "m_iNumRoundKills");
            destData.m_flTotalRoundDamageDealt = GetField(classes, "CCSPlayerController_ActionTrackingServices", "m_flTotalRoundDamageDealt");
            destData.m_pActionTrackingServices = GetField(classes, "CCSPlayerController", "m_pActionTrackingServices");
            destData.m_hLastAttacker = GetField(classes, "C_BreakableProp", "m_hLastAttacker");
            destData.m_flDeathInfoTime = GetField(classes, "C_CSPlayerPawn", "m_flDeathInfoTime");

            UpdateStaticFields(destData);
            Console.WriteLine("[Offsets] Successfully loaded without DTOs.");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[Offsets] Failed to update: {ex.Message}");
            Console.ResetColor();
            throw;
        }
    }

    private static int GetInt(JsonElement obj, string name)
    {
        if (obj.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number)
            return prop.GetInt32();
        throw new InvalidOperationException($"Field '{name}' not found in JSON or is not a number.");
    }

    private static int GetField(Dictionary<string, Dictionary<string, int>> classes, string className, string fieldName)
    {
        if (!classes.TryGetValue(className, out var fields))
            throw new InvalidOperationException($"Class '{className}' not found in client_dll.json");
        if (!fields.TryGetValue(fieldName, out var offset))
            throw new InvalidOperationException($"Field '{className}.fields.{fieldName}' not found in client_dll.json");
        return offset;
    }

    private static async Task<string> FetchJson(string url)
    {
        url = url.Trim();
        using var client = new HttpClient();
        var content = await client.GetStringAsync(url);
        if (string.IsNullOrWhiteSpace(content) || content.TrimStart().StartsWith("<"))
            throw new InvalidOperationException($"Invalid response from {url}");
        return content;
    }

    private static void UpdateStaticFields(dynamic data)
    {
        dwLocalPlayerPawn = data.dwLocalPlayerPawn;
        m_vOldOrigin = data.m_vOldOrigin;
        m_vecViewOffset = data.m_vecViewOffset;
        m_AimPunchAngle = data.m_aimPunchAngle;
        m_modelState = data.m_modelState;
        m_pGameSceneNode = data.m_pGameSceneNode;
        m_iIDEntIndex = data.m_iIDEntIndex;
        m_lifeState = data.m_lifeState;
        m_iHealth = data.m_iHealth;
        m_iTeamNum = data.m_iTeamNum;
        m_bDormant = data.m_bDormant;
        m_iShotsFired = data.m_iShotsFired;
        engine2_dll.dwNetworkGameClient = data.dwNetworkGameClient;
        engine2_dll.dwNetworkGameClient_serverTickCount = data.dwNetworkGameClient_serverTickCount;
        engine2_dll.dwBuildNumber = data.dwBuildNumber;
        engine2_dll.dwWindowHeight = data.dwWindowHeight;
        engine2_dll.dwWindowWidth = data.dwWindowWidth;
        client_dll.dwPlantedC4 = data.dwPlantedC4;
        client_dll.dwLocalPlayerPawn = data.dwLocalPlayerPawn;
        client_dll.dwViewAngles = data.dwViewAngles;
        client_dll.dwViewMatrix = data.dwViewMatrix;
        client_dll.dwGameRules = data.dwGameRules;
        m_hPawn = data.m_hPawn;
        m_hObserverTarget = data.m_hObserverTarget;
        m_hLastAttacker = data.m_hLastAttacker;
        m_fFlags = data.m_fFlags;
        m_iNumRoundKills = data.m_iNumRoundKills;
        m_flTotalRoundDamageDealt = data.m_flTotalRoundDamageDealt;
        m_flDeathInfoTime = data.m_flDeathInfoTime;
        dwLocalPlayerController = data.dwLocalPlayerController;
        dwViewMatrix = data.dwViewMatrix;
        dwViewAngles = data.dwViewAngles;
        dwEntityList = data.dwEntityList;
        m_entitySpottedState = data.m_entitySpottedState;
        m_Item = data.m_Item;
        m_pClippingWeapon = data.m_pClippingWeapon;
        m_AttributeManager = data.m_AttributeManager;
        m_iItemDefinitionIndex = data.m_iItemDefinitionIndex;
        m_bIsScoped = data.m_bIsScoped;
        m_flFlashDuration = data.m_flFlashDuration;
        m_iszPlayerName = data.m_iszPlayerName;
        dwPlantedC4 = data.dwPlantedC4;
        dwGlobalVars = data.dwGlobalVars;
        m_pActionTrackingServices = data.m_pActionTrackingServices;
        m_nBombSite = data.m_nBombSite;
        m_bBombDefused = data.m_bBombDefused;
        m_vecAbsVelocity = data.m_vecAbsVelocity;
        m_flDefuseCountDown = data.m_flDefuseCountDown;
        m_flC4Blow = data.m_flC4Blow;
        m_bBeingDefused = data.m_bBeingDefused;
        m_ArmorValue = data.m_ArmorValue;
        m_bHasHelmet = data.m_bHasHelmet;
        m_bSpotted = data.m_bSpotted;
        m_angEyeAngles = data.m_angEyeAngles;
        m_CBodyComponent = data.m_CBodyComponent;
        m_bGameRestart = data.m_bGameRestart;
        m_vecOrigin = data.m_vecOrigin;
    }

    #endregion
}