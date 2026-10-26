# Art and Level Design Guide

**Target Game:** *Pacman Horror* (Game Jam 1: Retro Revolution)  
**Owners:** Dev D (Art, Lighting, Audio, QA) & Dev A (Maze, Player, Pickups)  
**Reference Engine:** Unity 6 / 2022.3 LTS (URP)

---

## 1. High-Level Design Concept

Our game remakes the 1980 arcade classic **Pac-Man** as a **first-person survival horror game**.

### The Core Design Translation
In the original arcade game, top-down omniscient vision gave the player full spatial awareness of every ghost in the maze. In our game, we plunge the player *inside* the maze in pitch darkness:
* **The Maze:** An oppressive, claustrophobic labyrinth where visibility is strictly limited to your flashlight beam.
* **Pellets $\rightarrow$ Power Cells:** Bioluminescent / neon power cells that must all be harvested to restore power and escape.
* **Power Pellets $\rightarrow$ Flares:** High-intensity emergency flares that flood corridors with light, blinding and frightening the ghosts so they can be banished.
* **Ghosts $\rightarrow$ Specters:** Four distinct hunters preserving their iconic 1980 arcade behavioral personalities (Blinky chases, Pinky ambushes, Inky flanks, Clyde patrols).
* **Fruit $\rightarrow$ Battery:** Scattered replacement batteries that yield bonus points and replenish dying flashlight reserves.

---

## 2. Spatial & Level Metrics (For Dev A's Maze Construction)

To ensure the player, camera, and Dev B's `NavMeshAgent` ghosts navigate smoothly without clipping, snagging, or blocking corridors, adhere to these standardized dimensions:

```
                +-------------------------+
                |        CEILING          |  Height: 3.2m - 3.5m
                |                         |
                |   [Player]      [Ghost] |
                |    (0.8m)        (0.8m) |
                |                         |
                +-------------------------+
                |         FLOOR           |  Corridor Width: 2.5m - 3.0m
```

| Metric Element | Recommended Value | Rationale |
| :--- | :--- | :--- |
| **Grid / Tile Unit** | **3.0m $\times$ 3.0m** | Standard modular grid step for walls, corridors, and intersections. |
| **Corridor Width** | **2.5m – 3.0m** (1 tile) | Allows the player capsule ($r=0.4\text{m}$) and ghost capsule ($r=0.4\text{m}$) to flank or pass each other without hard collisions. |
| **Wall Height** | **3.2m – 3.5m** | Prevents the camera from seeing over walls and creates vertical claustrophobia. |
| **Ghost House** | **3 $\times$ 2 tiles** (center) | Central containment room with one-way ghost egress door (Blinky spawns outside; Pinky, Inky, Clyde inside). |
| **Scatter Corners** | **4 Corner Transforms** | Top-Right (Blinky), Top-Left (Pinky), Bottom-Right (Inky), Bottom-Left (Clyde). Fed into `GhostMazeLayout`. |
| **Warp Tunnels** | **Opposing Flanks** | Side corridors connecting opposite sides of the maze. |

---

## 3. Player Controller & Flashlight Specifications

### Player Metrics
* **Component:** Unity `CharacterController`
* **Capsule Height:** `1.8m`
* **Capsule Radius:** `0.4m`
* **Camera Height:** `1.6m` (human eye level, childed to player root)
* **Tag:** Must be tagged **`Player`**
* **Transform Anchor:** Attach `TransformAnchorBinder` targeting `ScriptableObjects/Runtime/PlayerAnchor` (Dev B's Ghost AI reads this for pursuit and flanking).

### Movement Tuning (Driven by `PlayerConfig.asset`)
* **Walk Speed:** `4.5 m/s` (Calibrated against Level 1 Ghost speed of `3.8 m/s`—the player is slightly faster than ghosts on baseline level).
* **Mouse Sensitivity:** `0.12`
* **Input Actions:** `WASD` / Arrows for locomotion, `Mouse` for look. Disabled during `Ready`, `PlayerCaught`, and `GameOver` states via `GameStateChanged` event listener.

### Flashlight Rig
* **Light Type:** Spot Light attached to camera.
* **Spot Angle:** Outer `50°`, Inner `35°`.
* **Range:** `14.0m` - `16.0m`.
* **Shadows:** **Soft Shadows** (Flashlight is the *only* dynamic light in the scene allowed to cast shadows).
* **Ghost Slowdown Sensor:** Attach Dev B's `FlashlightBeamSensor` component to the Spot Light GameObject.
* **Battery Discharge:** Drains at `2 units/sec` (lasts 50s continuous from 100 max).
* **Broadcast:** Raise `BatteryChanged(currentCharge / maxCharge)` float event whenever charge changes so the HUD battery bar updates.

---

## 4. Pickups & Collectibles Specification

```
   [Power Cell]                  [Flare]                     [Battery]
   Small glowing core            Road flare beacon           Canister with LED
   y = 0.9m                      y = 1.0m                    y = 0.8m
   Emissive Yellow / Cyan        Pulsing Red / White         Fluorescent Green
```

### 1. Power Cells (Pellets)
* **Visual:** Small floating emissive core or gem floating at $y = 0.9\text{m}$ with gentle bobbing/rotation.
* **Material:** `M_PowerCell_Emissive` using HDR emission ($1.5 - 2.5$) and URP Bloom. **NEVER attach Point Lights to pellets** (prevents performance collapse across 240+ pellets).
* **Collider:** Sphere Collider (`isTrigger = true`, Radius $= 0.45\text{m}$).
* **Events Raised:**
  1. Call `LevelProgress.Collect()`.
  2. Raise `PickupCollected` with `PickupConfig_Cell` asset (awards 10 pts, triggers audio waka cadence).

### 2. Flares (Power Pellets)
* **Visual:** Larger emergency flare beacon placed in the 4 outer quadrant corridors.
* **Material:** Bright pulsing emissive core with subtle spark particles.
* **Collider:** Sphere Collider (`isTrigger = true`, Radius $= 0.6\text{m}$).
* **Events Raised:**
  1. Call `LevelProgress.Collect()`.
  2. Raise `FlareCollected` void event (triggers `PostProcessRig` screen flash, plays explosion blast, and shifts all ghosts into Frightened mode).

### 3. Batteries (Fruit Bonus)
* **Visual:** High-tech battery canister with green LED level indicator. Spawns dynamically under the ghost house.
* **Collider:** Box/Sphere Collider (`isTrigger = true`, Radius $= 0.5\text{m}$).
* **Action:** Restores $+50$ flashlight charge (via `PlayerConfig.RechargeAmount`).
* **Events Raised:**
  1. Raise `PickupCollected` with `PickupConfig_Battery` asset (adds bonus score scaling with level). *Does NOT decrement level cell counter.*

---

## 5. Art Direction & Visual Aesthetics

### Aesthetic Pillar: "Retro Arcade Horror"
We merge the gritty analog horror of 1980s low-budget cinema with vibrant, high-contrast neon arcade cues:
1. **Low Albedo Corridors:** Walls and floors should be dark brutalist concrete, distressed metal panels, or dark modular tiles (`#15181C`). This swallows light and maximizes contrast.
2. **Neon Emissive Trims:** Subtle floor runner lines or wall seams glowing with faint cyan/yellow emissives guide player navigation around key junctions.
3. **CRT / Horror Post-Processing:**
   - **Bloom:** Threshold `0.9`, Intensity `1.2` (explodes to `4.8` on flare ignition).
   - **Vignette:** Tight peripheral edge falloff (`0.35`, clamps to `0.85` on ghost catch).
   - **Film Grain:** Medium analog grain simulating a 1980s security monitor or camera feed.

### Lighting Budget Constraints (Locked 60 FPS)
* **Total Dynamic Real-Time Lights:** $\le 8$ active in the scene at any time:
  - 1x Player Flashlight (Spot Light, Shadows Enabled).
  - 4x Ghost Proximity Glow (Point Lights, Shadows DISABLED, signature color per ghost).
  - 1x Flare Ignition Light (Transient Point Light during flare detonation).
  - 1-2x Ambient/House Lights.
* **Static Lighting:** Maze walls and floors use static baking or unlit lightmaps where applicable.

---

## 6. Dev A Implementation Checklist

- [ ] **Scene Setup:** Build or instantiate the maze under the `--- Maze goes here ---` GameObject in `Assets/_Project/Scenes/Game.unity`.
- [ ] **NavMesh:**
  - Mark maze walls as *Not Walkable*.
  - Put the Ghost House door on a layer that NavMesh bakes ignore (e.g., `Ignore Raycast`) so ghost agents can path through while the physical collider blocks the player.
  - Bake the `NavMeshSurface`.
- [ ] **Layout Anchors:** Add a `GhostMazeLayout` component and assign the transforms for:
  - `houseExit` (just outside the door)
  - `houseInside` (inside holding pen)
  - `topLeft`, `topRight`, `bottomLeft`, `bottomRight` (the 4 scatter corners)
- [ ] **Player Controller:**
  - Attach `CharacterController` and your input locomotion script.
  - Read speed and sensitivity from `ScriptableObjects/Configs/PlayerConfig`.
  - Add `TransformAnchorBinder` pointing at `ScriptableObjects/Runtime/PlayerAnchor`.
  - Add `FlashlightBeamSensor` to the Spot Light GameObject.
- [ ] **Event Wiring:**
  - On `LevelStarted`: Call `LevelProgress.Begin(totalCellsAndFlares)` and reset maze pickups.
  - On Cell Pickup: Call `LevelProgress.Collect()` and raise `PickupCollected` (`PickupConfig_Cell`).
  - On Flare Pickup: Call `LevelProgress.Collect()` and raise `FlareCollected`.
  - On Battery Pickup: Restore flashlight battery and raise `PickupCollected` (`PickupConfig_Battery`).
  - Flashlight: Broadcast `BatteryChanged(charge / max)` when battery updates.
