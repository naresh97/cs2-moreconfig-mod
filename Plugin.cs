using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace MoreConfig;

static class PluginInfo
{
    public const string PLUGIN_GUID = "com.nareshkumarrao.more_config";
    public const string PLUGIN_NAME = "MoreConfig";
    public const string PLUGIN_VERSION = "1.0.0.0";
}

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public static ManualLogSource Log;

    public static ConfigEntry<bool> DisableChirp;
    public static ConfigEntry<bool> DisableHighRentNotification;

    private void Awake()
    {
        Log = Logger;
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

        var harmony = new Harmony(PluginInfo.PLUGIN_GUID);
        harmony.PatchAll();

        DisableChirp = Config.Bind("Annoying", "DisableChirp", false, "Disable chirps");
        DisableHighRentNotification = Config.Bind("Annoying", "DisableHighRentNotification", false, "Disable high rent notification");
    }
}

[HarmonyPatch(typeof(Game.Triggers.CreateChirpSystem), "OnUpdate")]
class TrafficPatch
{
    static bool Prefix()
    {
        return !Plugin.DisableChirp.Value;
    }
}

[HarmonyPatch(typeof(Game.Simulation.RentAdjustSystem), "OnUpdate")]
class HighRentPatch
{
    static void Postfix(Game.Simulation.RentAdjustSystem __instance)
    {
        if (!Plugin.DisableHighRentNotification.Value)
            return;

        var instanceTraverse = Traverse.Create(__instance);
        var iconCommandBuffer = instanceTraverse.Field("m_IconCommandSystem").Method("CreateCommandBuffer").GetValue();
        var buildingQuery = instanceTraverse.Field("m_BuildingQuery").GetValue();

        RemoveHighRentIconJob jobData = default;
        jobData._iconCommandBuffer = (Game.Notifications.IconCommandBuffer) iconCommandBuffer;
        jobData._buildingConfigurationData = ((Unity.Entities.EntityQuery)instanceTraverse.Field("m_BuildingParameterQuery").GetValue()).GetSingleton<Game.Prefabs.BuildingConfigurationData>();
        jobData._entityType = (Unity.Entities.EntityTypeHandle) instanceTraverse.Field("__TypeHandle").Field("__Unity_Entities_Entity_TypeHandle").GetValue();

        Unity.Entities.JobChunkExtensions.ScheduleParallel(jobData, (Unity.Entities.EntityQuery) buildingQuery, default(Unity.Jobs.JobHandle));
    }
}

[Unity.Burst.BurstCompile]
struct RemoveHighRentIconJob : Unity.Entities.IJobChunk
{
    public Game.Notifications.IconCommandBuffer _iconCommandBuffer;
    public Unity.Entities.EntityTypeHandle _entityType;
    public Game.Prefabs.BuildingConfigurationData _buildingConfigurationData;

    public void Execute(in Unity.Entities.ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in Unity.Burst.Intrinsics.v128 chunkEnabledMask)
    {
        var nativeArray = chunk.GetNativeArray(_entityType);
        for(var i =  0; i < nativeArray.Length; i++)
        {
            var entity = nativeArray[i];
            _iconCommandBuffer.Remove(entity, _buildingConfigurationData.m_HighRentNotification);
        }
    }
}