using HarmonyLib;
using ProjectM;
using Stunlock.Core;
using Unity.Collections;
using SanguineArchives.VBloodArchives.Data;

namespace SanguineArchives.VBloodArchives.Patches;

[HarmonyPatch(typeof(UpdateBuffsBuffer_Destroy), nameof(UpdateBuffsBuffer_Destroy.OnUpdate))]
internal class UpdateBuffsBuffer_DestroyPatch
{
    public static void Prefix(UpdateBuffsBuffer_Destroy __instance)
    {
        var entities = __instance.__query_401358720_0.ToEntityArray(Allocator.Temp);
        foreach (var buffEntity in entities)
        {
            var prefabGUID = buffEntity.Read<PrefabGUID>();
            if (VBloodCollectionData.Buff_InCombat_VBlood_Set.Contains(prefabGUID))
            {
                Age age = buffEntity.Read<Age>();
                Buff buff = buffEntity.Read<Buff>();
                var targetPrefabGUID = buff.Target.Read<PrefabGUID>();
                var vbloodString = targetPrefabGUID.GetPrefabName();
                var isDead = buff.Target.Read<Health>().IsDead;
                Core.KillVBloodService.SetCombatDuration(vbloodString, age.Value);
                Core.TrackVBloodCombat.StopTrackingForVBlood(vbloodString, isDead);
                Core.Log.LogInfo($"UpdateBuffsBuffer_Destroy: Stop Tracking {vbloodString}...");
            }
        }
        entities.Dispose();
    }
}
