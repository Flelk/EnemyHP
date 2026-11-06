using System.Collections.Generic;
using UnityEngine;

namespace EnemyHP;

internal class Tracked
{
    public EnemyHealth Health;
    public Enemy Enemy;
    public int Hp;
    public int Max;
    public float Display = 1f;        // eased fraction the bar actually draws
    public float Ghost = 1f;          // trailing "damage chunk" fraction of the bar
    public float GhostHoldUntil;
    public float LastHit = -999f;
    public float Alpha;              // current bar opacity (fades in/out)
    public Vector3 Anchor;           // smoothed world position the bar is drawn at
    public bool AnchorValid;
    public float TopOffset;          // cached height of the head above the enemy's center
    public float TopUpdatedAt = -999f;
    public bool InSight;             // cached line-of-sight result
    public float SightCheckAt;
    public string Name;              // cached display name
    public bool NameFinal;
    public float NameRetryAt;
    public int RecentHits;           // stacks popups that land at the same moment
    public float RecentHitTime = -999f;

    public float Fraction => Max > 0 ? Mathf.Clamp01((float)Hp / Max) : 0f;
}

internal static class Tracker
{
    internal static readonly Dictionary<EnemyHealth, Tracked> All = new();

    internal static Tracked Get(EnemyHealth eh)
    {
        if (All.TryGetValue(eh, out var t)) return t;
        t = new Tracked
        {
            Health = eh,
            Enemy = eh.GetComponent<Enemy>(),
            Max = Mathf.Max(1, eh.health),
            // Host knows the real value; on guests healthCurrent never changes, so this equals max.
            Hp = Mathf.Clamp(eh.healthCurrent, 0, Mathf.Max(1, eh.health)),
        };
        t.Ghost = t.Display = t.Fraction;
        All[eh] = t;
        return t;
    }

    internal static void Damage(EnemyHealth eh, int amount, bool killed, int exactHp)
    {
        var t = Get(eh);
        t.Ghost = Mathf.Max(t.Ghost, t.Display);
        t.Hp = exactHp >= 0 ? exactHp : Mathf.Max(0, t.Hp - amount);
        if (killed) t.Hp = 0;
        Hit(t, amount, killed);
    }

    internal static void GuestDeath(EnemyHealth eh)
    {
        var t = Get(eh);
        int remaining = t.Hp;
        t.Ghost = Mathf.Max(t.Ghost, t.Display);
        t.Hp = 0;
        Hit(t, remaining, true);
    }

    static void Hit(Tracked t, int amount, bool killed)
    {
        float now = Time.unscaledTime;
        t.RecentHits = now - t.RecentHitTime < 0.25f ? t.RecentHits + 1 : 0;
        t.RecentHitTime = now;
        t.LastHit = now;
        t.GhostHoldUntil = now + 0.35f;
        Hud.SpawnPopup(t, amount, killed);
    }

    internal static void Reset(EnemyHealth eh)
    {
        var t = Get(eh);
        t.Max = Mathf.Max(1, eh.health);
        t.Hp = t.Max;
        t.Ghost = t.Display = 1f;
        t.LastHit = -999f;
    }

    internal static void Sync(EnemyHealth eh)
    {
        var t = Get(eh);
        t.Hp = Mathf.Clamp(eh.healthCurrent, 0, t.Max);
    }

    internal static void Prune()
    {
        List<EnemyHealth> dead = null;
        foreach (var key in All.Keys)
            if (key == null) (dead ??= new List<EnemyHealth>()).Add(key);
        if (dead != null) foreach (var k in dead) All.Remove(k);
    }
}
