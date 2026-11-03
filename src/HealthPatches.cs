using HarmonyLib;

namespace EnemyHP;

/// <summary>
/// How R.E.P.O. handles enemy health:
///  - EnemyHealth.Hurt runs the real math, but ONLY on the host (or single player).
///  - The host then sends HurtRPC(damage) to everyone, or DeathRPC if it was a killing blow.
///  - Guests never receive the enemy's actual HP, so they keep an estimate from those messages.
/// The game does not record who caused the damage, so every hit on an enemy is shown.
/// </summary>
[HarmonyPatch(typeof(EnemyHealth))]
internal static class HealthPatches
{
    static bool IsHost => SemiFunc.IsMasterClientOrSingleplayer();

    // Host / single player: exact damage = HP before minus HP after (includes damage resistance).
    [HarmonyPatch(nameof(EnemyHealth.Hurt)), HarmonyPrefix]
    static void HurtPrefix(EnemyHealth __instance, out int __state) => __state = __instance.healthCurrent;

    [HarmonyPatch(nameof(EnemyHealth.Hurt)), HarmonyPostfix]
    static void HurtPostfix(EnemyHealth __instance, int __state)
    {
        if (!IsHost) return;
        int dealt = __state - __instance.healthCurrent;
        if (dealt > 0) Tracker.Damage(__instance, dealt, __instance.healthCurrent <= 0, __instance.healthCurrent);
    }

    // Guests: damage arrives from the host. The host also gets this message but already handled it above.
    [HarmonyPatch(nameof(EnemyHealth.HurtRPC)), HarmonyPostfix]
    static void HurtRpcPostfix(EnemyHealth __instance, int _damage)
    {
        if (IsHost || _damage <= 0) return;
        Tracker.Damage(__instance, _damage, false, -1);
    }

    // Guests: a killing blow only sends DeathRPC (no HurtRPC), so the rest of the estimated HP is the final hit.
    [HarmonyPatch(nameof(EnemyHealth.DeathRPC)), HarmonyPostfix]
    static void DeathRpcPostfix(EnemyHealth __instance)
    {
        if (IsHost) return;
        Tracker.GuestDeath(__instance);
    }

    // Enemies respawn later in the level with full health.
    [HarmonyPatch(nameof(EnemyHealth.OnSpawn)), HarmonyPostfix]
    static void OnSpawnPostfix(EnemyHealth __instance) => Tracker.Reset(__instance);

    // Host only knows heal amounts; keep the tracked value in sync.
    [HarmonyPatch(nameof(EnemyHealth.Heal)), HarmonyPostfix]
    static void HealPostfix(EnemyHealth __instance)
    {
        if (IsHost) Tracker.Sync(__instance);
    }
}
