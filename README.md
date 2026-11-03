# EnemyHP

Enemy health bars and floating damage numbers for R.E.P.O. (client-side BepInEx 5 mod).
Player docs: [thunderstore/README.md](thunderstore/README.md).

## How it works
| File | What |
| --- | --- |
| `src/HealthPatches.cs` | Harmony hooks on `EnemyHealth`: `Hurt` (host: exact damage), `HurtRPC` / `DeathRPC` (guests: damage from the host), `OnSpawn` (respawn reset), `Heal` |
| `src/Tracker.cs` | Per-enemy HP state (exact on host, estimated on guests) |
| `src/Hud.cs` | IMGUI drawing: bars above the head (mesh bounds), aim + line-of-sight check, popup animation |

## Build
`.\build.ps1` → `dist\EnemyHP-<version>.zip`. `build.cmd` also installs into the r2modman *Default* profile.
