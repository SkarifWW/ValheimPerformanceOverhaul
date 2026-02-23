# Valheim Performance Overhaul

A comprehensive performance optimization mod focused on reducing CPU and GPU load — especially on large bases, busy servers, and zones with many light sources.

---

## Installation

Install via **r2modman** or **Thunderstore Mod Manager** (recommended) — everything is placed automatically.

**Manual install:**
1. Install [BepInExPack Valheim](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/) first.
2. Copy `ValheimPerformanceOverhaul.dll` into `BepInEx/plugins/`.
3. Launch the game — a config file is generated automatically at `BepInEx/config/com.Skarif.ValheimPerformanceOverhaul.cfg`.

---

## What it does

### 🔦 Light Culling *(biggest FPS impact)*
- Limits active light sources to a configurable maximum (default: 15).
- Disables shadow casting beyond a set distance.
- **Light LOD system:** transitions lights through Full → No Shadows → Emissive → Billboard → Disabled as distance increases.

### 💤 Distance Culler
- Puts distant creatures and building pieces to "sleep" — pauses their Update logic.
- Physics culling for Rigidbodies beyond a set range.
- Configurable exclusions (e.g. portals, tombstones are never culled).

### 🏗️ Piece Optimization
- `WearNTear.GetSupport()` results are cached with a configurable TTL.
- Distant pieces skip their Update cycle entirely.
- Asynchronous WearNTear initialization — spreads load over multiple frames on scene load.

### 🤖 AI Throttling
- Monsters beyond 60 m update AI only every 5 seconds instead of every frame.
- LOS (line-of-sight) checks are cached per-target with a 0.5 s timeout.
- Idle tamed animals inside player bases enter a low-power mode.

### 🎨 Graphics Settings
- Configurable shadow distance, resolution, and cascade count.
- Bloom and screen-space reflections toggle.
- Terrain quality multiplier.

### 🌿 Vegetation
- Grass render distance and density control.
- Detail object distance and density.

### 🎵 Audio Pooling
- Reuses AudioSource components instead of creating new ones per sound effect.

### ♻️ Object Pooling
- Reuses `ItemDrop` GameObjects to reduce instantiation overhead when loot spawns.

### 🧠 GC Control
- Prevents Unity's garbage collection from firing during combat or movement.

### ⚡ JIT Warm-up
- Pre-compiles critical game methods on spawn to eliminate the first-use stutter.

### 🗺️ Minimap Optimization
- Configurable texture resolution and update frequency.

---

## Configuration

All settings are available in `BepInEx/config/com.Skarif.ValheimPerformanceOverhaul.cfg`.

If you have [BepInEx Configuration Manager](https://thunderstore.io/c/valheim/p/Azumatt/Official_BepInEx_ConfigurationManager/) installed, press **F1** in-game to adjust all settings with a GUI in real time.

### Key settings

| Setting | Default | Description |
|---|---|---|
| Max Active Lights | 15 | Max simultaneous light sources |
| Light Cull Distance | 60 m | Beyond this distance lights turn off |
| Creature Cull Distance | 80 m | Creatures sleep beyond this distance |
| Piece Cull Distance | 100 m | Building pieces sleep beyond this distance |
| Support Cache Duration | 5 s | How long structural support values are cached |
| Grass Density Multiplier | 0.7 | 1.0 = vanilla, lower = fewer grass |
| Shadow Distance | 50 m | Maximum shadow render distance |

---

## Performance expectations

Results depend heavily on scene complexity. Typical gains:

| Scenario | Expected FPS gain |
|---|---|
| Open world, few structures | ~5–10% |
| Medium base (50–100 pieces) | ~10–20% |
| Large base (300+ pieces, 10+ light sources) | ~20–40% |
| Busy server with many players/mobs | ~15–30% |

---

## Important

- ✅ Works standalone — no other mods required.
- ✅ Compatible with most content mods (Epic Loot, Jotunn-based mods, etc.).
- ⚠️ If Object Pooling conflicts with a loot mod, disable it in config (`4. Object Pooling → Enabled = false`).
- ⚠️ The mod was created in collaboration with AI. Despite the fact that I conducted test runs in various situations, the mod is in BETA. Although the mod technically cannot break the world, it is advisable to make backups.
- ❌ Does not support crossplay (Steam + Game Pass mixed sessions). Pure Steam servers are fine.
---

## Changelog

## v2.6.3
### Added
- Welcome screen on main menu launch
  - Displays a feedback message on first launch
  - Clickable link to Steam profile for bug reports and suggestions
  - "Don't show anymore" checkbox to permanently hide the screen


### 2.6.0
- Removed NetworkManager (ZSteamSocket-only, broke crossplay)
- Removed ZDOOptimizer (potential desync risk on servers)
- Fixed LightLODManager: removed duplicate ScanForLights() on Start
- Fixed AdvancedLightManager: removed periodic FindObjectsByType scan every 5s (caused micro-freezes)
- DistanceCuller refactored: all cullers now managed by one central Update loop instead of one Update() per object
- PiecePatches: replaced ConcurrentDictionary with Dictionary (main thread only, 3–5x faster)
- AsyncWearInit: added early exit when queue is empty

### 2.5.1
- Initial public release
