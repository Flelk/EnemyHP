using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace EnemyHP;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;

    internal static ConfigEntry<bool> ShowBars;
    internal static ConfigEntry<bool> ShowNames;
    internal static ConfigEntry<bool> ShowDamage;
    internal static ConfigEntry<float> AimAngle;
    internal static ConfigEntry<float> AimRange;
    internal static ConfigEntry<float> RecentHitSeconds;
    internal static ConfigEntry<float> UiScale;

    void Awake()
    {
        Log = Logger;

        ShowBars = Config.Bind("Display", "HealthBars", true, "Show a health bar above enemies you aim at or have just hit.");
        ShowNames = Config.Bind("Display", "EnemyNames", true, "Show the enemy's name above its health bar.");
        ShowDamage = Config.Bind("Display", "DamageNumbers", true, "Show floating damage numbers when an enemy takes damage.");
        UiScale = Config.Bind("Display", "Scale", 1f,
            new ConfigDescription("Size of bars and numbers.", new AcceptableValueRange<float>(0.5f, 2f)));
        AimAngle = Config.Bind("Health bar", "AimAngle", 7f,
            new ConfigDescription("How close (degrees) your crosshair must be to an enemy to show its bar.", new AcceptableValueRange<float>(1f, 30f)));
        AimRange = Config.Bind("Health bar", "AimRange", 30f,
            new ConfigDescription("Max distance (meters) for showing a bar when aiming.", new AcceptableValueRange<float>(5f, 100f)));
        RecentHitSeconds = Config.Bind("Health bar", "ShowAfterHitSeconds", 5f,
            new ConfigDescription("Keep the bar up this long after the enemy is hit, while you can still see the enemy.", new AcceptableValueRange<float>(0f, 30f)));

        var go = new GameObject("EnemyHP");
        go.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(go);
        go.AddComponent<Hud>();

        new Harmony(MyPluginInfo.PLUGIN_GUID).PatchAll(typeof(HealthPatches));
        Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME} {MyPluginInfo.PLUGIN_VERSION} loaded.");
    }
}
