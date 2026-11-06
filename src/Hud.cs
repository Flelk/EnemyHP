using System.Collections.Generic;
using UnityEngine;

namespace EnemyHP;

/// <summary>Draws health bars and damage popups on top of the game (IMGUI, screen space).</summary>
internal class Hud : MonoBehaviour
{
    class Popup
    {
        public Vector3 World;
        public string Text;
        public bool Kill;
        public Color Color;
        public float Size;
        public float Born;
        public Vector2 Drift;
    }

    const float PopupLife = 1.15f;

    static readonly List<Popup> popups = new();
    static Texture2D white;
    static GUIStyle textStyle;
    float nextScan;

    // ---------- popups ----------

    internal static void SpawnPopup(Tracked t, int amount, bool kill)
    {
        if (!Plugin.ShowDamage.Value || t.Enemy == null) return;

        float frac = (float)amount / t.Max;
        Color color =
            kill ? new Color(1f, 0.82f, 0.25f) :          // gold
            frac >= 0.25f ? new Color(1f, 0.33f, 0.28f) : // big hit: red
            frac >= 0.10f ? new Color(1f, 0.62f, 0.25f) : // medium: orange
                            new Color(0.96f, 0.96f, 0.96f); // chip: white

        float size = kill ? 34f : Mathf.Lerp(22f, 32f, Mathf.Clamp01(frac / 0.4f));

        popups.Add(new Popup
        {
            World = PopupAnchor(t),
            Text = amount > 0 ? amount.ToString() : "",
            Kill = kill,
            Color = color,
            Size = size,
            Born = Time.unscaledTime,
            // Spread simultaneous hits so they don't overlap.
            Drift = new Vector2(Random.Range(-28f, 28f) + t.RecentHits * 18f, -t.RecentHits * 14f),
        });
        if (popups.Count > 40) popups.RemoveAt(0);
    }

    static Vector3 PopupAnchor(Tracked t)
    {
        var center = t.Enemy.CenterTransform ? t.Enemy.CenterTransform.position : t.Enemy.transform.position;
        var top = TopOf(t);
        return Vector3.Lerp(center, top, 0.6f);
    }

    // ---------- per frame ----------

    void Update()
    {
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 0.5f;
            Tracker.Prune();
            foreach (var eh in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None)) Tracker.Get(eh);
        }

        var cam = GetCamera();
        float dt = Time.unscaledDeltaTime;
        float now = Time.unscaledTime;

        foreach (var t in Tracker.All.Values)
        {
            if (t.Health == null) continue;

            // The fill eases to the real value, in either direction.
            t.Display = Mathf.Lerp(t.Display, t.Fraction, 1f - Mathf.Exp(-14f * dt));
            if (Mathf.Abs(t.Display - t.Fraction) < 0.0015f) t.Display = t.Fraction;

            // Trailing chunk: hold briefly, then glide down, slowing as it catches up.
            if (now > t.GhostHoldUntil)
                t.Ghost = Mathf.MoveTowards(t.Ghost, t.Display, dt * (0.2f + 3.5f * Mathf.Abs(t.Ghost - t.Display)));
            if (t.Ghost < t.Display) t.Ghost = t.Display;

            if (t.Alpha <= 0f) t.AnchorValid = false; // snap into place next time it appears

            bool want = Plugin.ShowBars.Value && cam != null && IsAlive(t) &&
                        (now - t.LastHit < Plugin.RecentHitSeconds.Value || IsAimedAt(cam, t));
            t.Alpha = Mathf.MoveTowards(t.Alpha, want ? 1f : 0f, dt * (want ? 8f : 3f));
        }

        popups.RemoveAll(p => now - p.Born > PopupLife);
    }

    // After everything has moved this frame. Horizontal follows closely; height is softer
    // because mesh bounds wobble with the enemy's animation.
    void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        foreach (var t in Tracker.All.Values)
        {
            if (t.Alpha <= 0f || t.Health == null || t.Enemy == null) continue;

            var target = TopOf(t);
            if (!t.AnchorValid || (target - t.Anchor).sqrMagnitude > 25f) // first frame or teleport
            {
                t.Anchor = target;
                t.AnchorValid = true;
                continue;
            }
            float xz = 1f - Mathf.Exp(-25f * dt), y = 1f - Mathf.Exp(-9f * dt);
            t.Anchor = new Vector3(
                Mathf.Lerp(t.Anchor.x, target.x, xz),
                Mathf.Lerp(t.Anchor.y, target.y, y),
                Mathf.Lerp(t.Anchor.z, target.z, xz));
        }
    }

    static bool IsAlive(Tracked t) =>
        t.Enemy != null && t.Enemy.gameObject.activeInHierarchy && !t.Health.dead &&
        (t.Enemy.EnemyParent == null || t.Enemy.EnemyParent.Spawned);

    static Camera GetCamera()
    {
        var cu = CameraUtils.Instance;
        if (cu != null && cu.MainCamera != null) return cu.MainCamera;
        return Camera.main;
    }

    static bool IsAimedAt(Camera cam, Tracked t)
    {
        var target = t.Enemy.CenterTransform ? t.Enemy.CenterTransform.position : t.Enemy.transform.position;
        var from = cam.transform.position;
        var dir = target - from;
        float dist = dir.magnitude;
        if (dist > Plugin.AimRange.Value || dist < 0.01f) return false;
        if (Vector3.Angle(cam.transform.forward, dir) > Plugin.AimAngle.Value) return false;

        // Line of sight: no bars through walls.
        int mask = t.Enemy.VisionMask.value != 0 ? t.Enemy.VisionMask.value : Physics.DefaultRaycastLayers;
        if (Physics.Raycast(from, dir / dist, out var hit, dist, mask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<Enemy>() == t.Enemy) return true;
            if (hit.collider.GetComponentInParent<PlayerAvatar>() != null) return true; // our own body
            return false;
        }
        return true;
    }

    // Follows the enemy live; only the (costlier) head height is sampled from the meshes.
    static Vector3 TopOf(Tracked t)
    {
        var basePos = t.Enemy.CenterTransform ? t.Enemy.CenterTransform.position : t.Enemy.transform.position;
        float now = Time.unscaledTime;
        if (now - t.TopUpdatedAt >= 0.1f)
        {
            t.TopUpdatedAt = now;
            float topY = basePos.y + 0.8f;
            bool any = false;
            if (t.Health.renderers != null)
            {
                foreach (var r in t.Health.renderers)
                {
                    if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                    topY = any ? Mathf.Max(topY, r.bounds.max.y) : r.bounds.max.y;
                    any = true;
                }
            }
            // Clamp so one odd mesh can't send the bar into the sky.
            t.TopOffset = Mathf.Clamp(topY - basePos.y, 0.3f, 3.5f);
        }
        return new Vector3(basePos.x, basePos.y + t.TopOffset + 0.35f, basePos.z);
    }

    // The name the game itself uses for the enemy, translated to the player's language.
    static string NameOf(Tracked t)
    {
        float now = Time.unscaledTime;
        if (t.NameFinal || (t.Name != null && now < t.NameRetryAt)) return t.Name;
        t.NameRetryAt = now + 1f;

        var parent = t.Enemy.EnemyParent != null ? t.Enemy.EnemyParent : t.Enemy.GetComponentInParent<EnemyParent>(true);
        string name = null;
        if (parent != null)
        {
            // The game's Virus points at the Mentalist's translation, so it keeps its own name.
            if (parent.enemyName == "Virus")
            {
                name = parent.enemyName;
                t.NameFinal = true;
            }
            else
            {
                name = Localized(parent);
                if (name != null) t.NameFinal = true;
                else if (!string.IsNullOrWhiteSpace(parent.enemyName)) name = parent.enemyName;
            }
        }
        if (name == null)
        {
            // Last resort: prefabs are named "Enemy - Duck".
            string go = t.Enemy.transform.root.name.Replace("(Clone)", "").Trim();
            name = go.StartsWith("Enemy - ") ? go.Substring(8) : "Enemy";
        }
        if (t.Name != name) Plugin.Log.LogInfo($"Name for {t.Enemy.transform.root.name}: {name}");
        return t.Name = name;
    }

    static string Localized(EnemyParent parent)
    {
        try
        {
            string s = parent.enemyNameLocalized?.GetLocalizedString();
            return string.IsNullOrWhiteSpace(s) || s.StartsWith("No translation found") ? null : s;
        }
        catch (System.Exception e)
        {
            Plugin.Log.LogWarning($"Couldn't read the name of {parent.enemyName}: {e.Message}");
            return null;
        }
    }

    // Viewport -> screen keeps this correct with R.E.P.O.'s low-res render texture.
    static bool ToScreen(Camera cam, Vector3 world, out Vector2 screen)
    {
        var v = cam.WorldToViewportPoint(world);
        screen = new Vector2(v.x * Screen.width, (1f - v.y) * Screen.height);
        return v.z > 0.1f && v.x > -0.2f && v.x < 1.2f && v.y > -0.2f && v.y < 1.2f;
    }

    // ---------- drawing ----------

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;
        var cam = GetCamera();
        if (cam == null) return;
        EnsureResources();

        float scale = Plugin.UiScale.Value * Mathf.Clamp(Screen.height / 1080f, 0.6f, 2f);

        foreach (var t in Tracker.All.Values)
            if (t.Alpha > 0.01f && t.Enemy != null) DrawBar(cam, t, scale);

        float now = Time.unscaledTime;
        foreach (var p in popups) DrawPopup(cam, p, now, scale);

        GUI.color = Color.white;
    }

    static void DrawBar(Camera cam, Tracked t, float scale)
    {
        if (!t.AnchorValid || !ToScreen(cam, t.Anchor, out var pos)) return;

        float dist = Vector3.Distance(cam.transform.position, t.Anchor);
        float k = scale * Mathf.Lerp(1f, 0.65f, Mathf.InverseLerp(4f, 30f, dist));
        float w = 124f * k, h = 9f * k;
        float a = t.Alpha;
        var bar = new Rect(pos.x - w / 2f, pos.y - h / 2f, w, h);

        // Frame + background
        Fill(new Rect(bar.x - 2, bar.y - 2, bar.width + 4, bar.height + 4), new Color(0f, 0f, 0f, 0.55f * a));
        Fill(bar, new Color(0.12f, 0.12f, 0.14f, 0.85f * a));

        // Trailing damage chunk, then the real fill
        float f = t.Display;
        if (t.Ghost > f)
            Fill(new Rect(bar.x, bar.y, bar.width * t.Ghost, bar.height), new Color(1f, 0.92f, 0.85f, 0.75f * a));
        var fillColor = HealthColor(f);
        var fill = new Rect(bar.x, bar.y, bar.width * f, bar.height);
        Fill(fill, new Color(fillColor.r, fillColor.g, fillColor.b, a));
        // Top highlight for a bit of depth
        Fill(new Rect(fill.x, fill.y, fill.width, Mathf.Max(1f, h * 0.3f)), new Color(1f, 1f, 1f, 0.18f * a));
        // Quarter ticks
        for (int i = 1; i < 4; i++)
            Fill(new Rect(bar.x + bar.width * i / 4f, bar.y, 1f, bar.height), new Color(0f, 0f, 0f, 0.35f * a));

        // Name (left) and HP (right) above the bar
        int fs = Mathf.RoundToInt(13f * k);
        float lineH = fs + 6f;
        var line = new Rect(bar.x, bar.y - lineH - 1f, bar.width, lineH);
        if (Plugin.ShowNames.Value)
        {
            Text(line, NameOf(t), fs, TextAnchor.LowerLeft, new Color(1f, 1f, 1f, 0.9f * a), a);
        }
        Text(line, $"{Mathf.RoundToInt(f * t.Max)} / {t.Max}", fs, Plugin.ShowNames.Value ? TextAnchor.LowerRight : TextAnchor.LowerCenter,
            new Color(1f, 1f, 1f, 0.95f * a), a);
    }

    static void DrawPopup(Camera cam, Popup p, float now, float scale)
    {
        float age = now - p.Born;
        float u = age / PopupLife;
        if (!ToScreen(cam, p.World, out var pos)) return;

        // Pop: overshoot to 135%, settle to 100%. Rise with ease-out. Fade over the last 40%.
        float pop = age < 0.08f ? Mathf.Lerp(0.4f, 1.35f, age / 0.08f)
                  : age < 0.22f ? Mathf.Lerp(1.35f, 1f, (age - 0.08f) / 0.14f) : 1f;
        float rise = 70f * scale * (1f - (1f - u) * (1f - u));
        float alpha = u < 0.6f ? 1f : 1f - (u - 0.6f) / 0.4f;

        var center = pos + p.Drift * scale * Mathf.Min(1f, age / 0.25f) + new Vector2(0f, -rise);
        int fs = Mathf.RoundToInt(p.Size * scale * pop);

        if (p.Text.Length > 0)
            Text(new Rect(center.x - 150, center.y - fs, 300, fs * 1.6f), p.Text, fs, TextAnchor.MiddleCenter,
                new Color(p.Color.r, p.Color.g, p.Color.b, alpha), alpha, outline: 2f);
        if (p.Kill)
        {
            int ks = Mathf.RoundToInt(15f * scale * pop);
            Text(new Rect(center.x - 150, center.y + fs * 0.45f, 300, ks * 1.6f), "KILL", ks, TextAnchor.MiddleCenter,
                new Color(1f, 0.82f, 0.25f, alpha), alpha, outline: 1.5f);
        }
    }

    static readonly Color Red = new(1f, 0.32f, 0.27f), Yellow = new(1f, 0.78f, 0.25f), Green = new(0.33f, 0.86f, 0.45f);

    static Color HealthColor(float f) =>
        f < 0.5f ? Color.Lerp(Red, Yellow, f / 0.5f) : Color.Lerp(Yellow, Green, (f - 0.5f) / 0.5f);

    // ---------- primitives ----------

    static void EnsureResources()
    {
        if (white == null)
        {
            white = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            white.SetPixel(0, 0, Color.white);
            white.Apply();
        }
        textStyle ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Overflow };
    }

    static void Fill(Rect r, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(r, white);
    }

    static readonly Vector2[] OutlineDirs =
    {
        new(-1, 0), new(1, 0), new(0, -1), new(0, 1), new(-1, -1), new(1, -1), new(-1, 1), new(1, 1),
    };

    static void Text(Rect r, string s, int size, TextAnchor anchor, Color color, float alpha, float outline = 1f)
    {
        textStyle.fontSize = Mathf.Max(6, size);
        textStyle.alignment = anchor;
        GUI.color = Color.white;

        textStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f * alpha);
        foreach (var d in OutlineDirs)
            GUI.Label(new Rect(r.x + d.x * outline, r.y + d.y * outline, r.width, r.height), s, textStyle);

        textStyle.normal.textColor = color;
        GUI.Label(r, s, textStyle);
    }
}
