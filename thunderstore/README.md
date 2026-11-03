# Enemy HP

Client-side mod for R.E.P.O. that adds:
- **Health bars** above enemies you aim at or have just hit, showing name, `HP / max`, a colour that shifts green → yellow → red, and a pale "chunk" that shows the damage just dealt before sliding away.
- **Damage numbers** that pop out of the enemy when it takes damage, rise and fade. White for chip damage, orange for medium, red for big hits, gold **KILL** on the finishing blow.

Only the person who installs it sees these. Nobody else in the lobby needs it.

## Good to know
- The game doesn't track who dealt damage, so you'll see numbers for **everyone's** hits on an enemy, not just yours.
- If you **host** (or play solo), HP and damage are exact.
- If you're a **guest**, the game never sends you enemy HP. The mod tracks it from the hits it hears about, so it can be slightly off (e.g. if an enemy heals).
- Bars don't show through walls when you're only aiming. They do stay up briefly after a hit.

## Install
**r2modman / Gale:** Settings → *Import local mod* → choose `EnemyHP-x.y.z.zip` → *Start modded*.
**Manual:** install BepInExPack, then put `EnemyHP.dll` in `BepInEx\plugins\`.

## Settings
These are in `BepInEx\config\gabe.enemyhp.cfg` or r2modman's config editor:
- **Display:** `HealthBars`, `EnemyNames`, `DamageNumbers`, `Scale` (0.5 to 2)
- **Health bar:** `AimAngle` (default 7°), `AimRange` (default 30 m), `ShowAfterHitSeconds` (default 5)
