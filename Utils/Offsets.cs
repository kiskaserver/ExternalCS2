using System.Dynamic;
using System.Net.Http;
using CS2GameHelper.DTO.ClientDllDTO;
using CS2GameHelper.Utils.DTO;
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
    public static int dwEntityList;
    public static int m_bDormant;
    public static int m_iShotsFired;
    public static int m_hPawn;
    public static int dwLocalPlayerController;
    public static int dwViewMatrix;
    public static int dwViewAngles;
    public static int m_entitySpottedState;
    public static int m_Item;
    public static int m_pClippingWeapon;
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
    // Backwards-compatible grouped offsets for older code that expects nested types
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

            var sourceDataDw = JsonSerializer.Deserialize<OffsetsDTO>(offsetsJson)
                               ?? throw new InvalidOperationException("Failed to deserialize offsets.json.");
            var sourceDataClient = JsonSerializer.Deserialize<ClientDllDTO>(clientJson)
                                   ?? throw new InvalidOperationException("Failed to deserialize client_dll.json.");

            var engine2 = sourceDataDw.engine2dll
                          ?? throw new InvalidOperationException("offsets.json is missing engine2.dll data.");
            var offsetsClient = sourceDataDw.clientdll
                               ?? throw new InvalidOperationException("offsets.json is missing client.dll data.");
            var clientDll = sourceDataClient.clientdll
                            ?? throw new InvalidOperationException("client_dll.json is missing client.dll data.");
            var clientClasses = clientDll.classes
                                ?? throw new InvalidOperationException("client_dll.json is missing classes data.");

            dynamic destData = new ExpandoObject();

            // Offsets
            destData.dwBuildNumber = engine2.dwBuildNumber;
            destData.dwNetworkGameClient = engine2.dwNetworkGameClient;
            destData.dwNetworkGameClient_serverTickCount = engine2.dwNetworkGameClient_deltaTick;
            destData.dwWindowHeight = engine2.dwWindowHeight;
            destData.dwWindowWidth = engine2.dwWindowWidth;
            destData.dwLocalPlayerController = offsetsClient.dwLocalPlayerController;
            destData.dwEntityList = offsetsClient.dwEntityList;
            destData.dwViewMatrix = offsetsClient.dwViewMatrix;
            destData.dwPlantedC4 = offsetsClient.dwPlantedC4;
            destData.dwLocalPlayerPawn = offsetsClient.dwLocalPlayerPawn;
            destData.dwViewAngles = offsetsClient.dwViewAngles;
            destData.dwGlobalVars = offsetsClient.dwGlobalVars;
            destData.dwGameRules = offsetsClient.dwGameRules;

            // client.dll
            destData.m_fFlags = clientClasses.C_BaseEntity.fields.m_fFlags;
            destData.m_vOldOrigin = clientClasses.C_BasePlayerPawn.fields.m_vOldOrigin;
            destData.m_vecViewOffset = clientClasses.C_BaseModelEntity.fields.m_vecViewOffset;
            destData.m_aimPunchAngle = clientClasses.C_CSPlayerPawn.fields.m_aimPunchAngle;
            destData.m_modelState = clientClasses.CSkeletonInstance.fields.m_modelState;
            destData.m_pGameSceneNode = clientClasses.C_BaseEntity.fields.m_pGameSceneNode;
            destData.m_iIDEntIndex = clientClasses.C_CSPlayerPawn.fields.m_iIDEntIndex;
            destData.m_lifeState = clientClasses.C_BaseEntity.fields.m_lifeState;
            destData.m_iHealth = clientClasses.C_BaseEntity.fields.m_iHealth;
            destData.m_iTeamNum = clientClasses.C_BaseEntity.fields.m_iTeamNum;
            destData.m_bDormant = clientClasses.CGameSceneNode.fields.m_bDormant;
            destData.m_iShotsFired = clientClasses.C_CSPlayerPawn.fields.m_iShotsFired;
            destData.m_hPawn = clientClasses.CBasePlayerController.fields.m_hPawn;
            destData.m_entitySpottedState = clientClasses.C_CSPlayerPawn.fields.m_entitySpottedState;
            destData.m_Item = clientClasses.C_AttributeContainer.fields.m_Item;
            destData.m_pClippingWeapon = clientClasses.C_CSPlayerPawn.fields.m_pClippingWeapon;
            destData.m_AttributeManager = clientClasses.C_EconEntity.fields.m_AttributeManager;
            destData.m_iItemDefinitionIndex = clientClasses.C_EconItemView.fields.m_iItemDefinitionIndex;
            destData.m_bIsScoped = clientClasses.C_CSPlayerPawn.fields.m_bIsScoped;
            destData.m_flFlashDuration = clientClasses.C_CSPlayerPawnBase.fields.m_flFlashDuration;
            destData.m_iszPlayerName = clientClasses.CBasePlayerController.fields.m_iszPlayerName;
            destData.m_nBombSite = clientClasses.C_PlantedC4.fields.m_nBombSite;
            destData.m_bBombDefused = clientClasses.C_PlantedC4.fields.m_bBombDefused;
            destData.m_vecAbsVelocity = clientClasses.C_BaseEntity.fields.m_vecAbsVelocity;
            destData.m_flDefuseCountDown = clientClasses.C_PlantedC4.fields.m_flDefuseCountDown;
            destData.m_flC4Blow = clientClasses.C_PlantedC4.fields.m_flC4Blow;
            destData.m_bBeingDefused = clientClasses.C_PlantedC4.fields.m_bBeingDefused;
            destData.m_bGameRestart = clientClasses.C_CSGameRules.fields.m_bGameRestart;

            // Newly added fields client.dll
            destData.m_ArmorValue = clientClasses.C_CSPlayerPawn.fields.m_ArmorValue;
            destData.m_bHasHelmet = clientClasses.CCSPlayer_ItemServices.fields.m_bHasHelmet;
            destData.m_bSpotted = clientClasses.C_CSPlayerPawn.fields.m_bSpotted;
            destData.m_angEyeAngles = clientClasses.C_CSPlayerPawn.fields.m_angEyeAngles;   // Vector2 (pitch/yaw)
            destData.m_CBodyComponent = clientClasses.C_BaseEntity.fields.m_CBodyComponent;
            destData.m_vecOrigin = clientClasses.CGameSceneNode.fields.m_vecOrigin;

            UpdateStaticFields(destData);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"An error occurred: {ex.Message}");
            throw;
        }
    }

    private static async Task<string> FetchJson(string url)
    {
        using var client = new HttpClient();
        return await client.GetStringAsync(url);
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
        // Fill grouped/nested compatibility containers
        engine2_dll.dwNetworkGameClient = data.dwNetworkGameClient;
        engine2_dll.dwNetworkGameClient_serverTickCount = data.dwNetworkGameClient_serverTickCount;
        engine2_dll.dwBuildNumber = data.dwBuildNumber;
        engine2_dll.dwWindowHeight = data.dwWindowHeight ?? 0;
        engine2_dll.dwWindowWidth = data.dwWindowWidth ?? 0;

        client_dll.dwPlantedC4 = data.dwPlantedC4;
        client_dll.dwLocalPlayerPawn = data.dwLocalPlayerPawn;
        client_dll.dwViewAngles = data.dwViewAngles;
        client_dll.dwViewMatrix = data.dwViewMatrix;
        // Optional fields (may be absent in some offset sources)
        client_dll.dwGameRules = data.dwGameRules;
        m_hPawn = data.m_hPawn;
        m_fFlags = data.m_fFlags;
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
        m_nBombSite = data.m_nBombSite;
        m_bBombDefused = data.m_bBombDefused;
        m_vecAbsVelocity = data.m_vecAbsVelocity;
        m_flDefuseCountDown = data.m_flDefuseCountDown;
        m_flC4Blow = data.m_flC4Blow;
        m_bBeingDefused = data.m_bBeingDefused;
    }

    #endregion

}
