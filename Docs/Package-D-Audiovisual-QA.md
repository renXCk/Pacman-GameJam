# Package D: Audiovisual, Lighting, VFX & QA Polish

**Owner:** Dev D (Senior Unity Technical Artist & Audio/VFX Engineer)  
**Target:** Unity 6 / 2022.3 LTS with Universal Render Pipeline (URP), Linear Color Space, HDR enabled.

---

## 1. URP Global Volume Configuration Matrix

To achieve the retro-arcade horror atmosphere while protecting disk profiles from mutation at runtime via [`PostProcessRig`](file:///c:/Users/Duncan%20Joseph/Desktop/Repositories/Pacman-GameJam/Assets/_Project/Scripts/Lighting/PostProcessRig.cs), configure the global `VolumeProfile` (`Assets/Settings/DefaultVolumeProfile.asset` or scene `[Global Volume]`) with the following baseline overrides:

| URP Volume Component | Setting | Recommended Value | Visual Rationale / Gameplay Role |
| :--- | :--- | :--- | :--- |
| **Tonemapping** | Mode | `ACES` | Film-grade contrast curve with natural highlight rolloff for intense flare flashes. |
| **Bloom** | Threshold | `0.9` | Prevents standard diffuse textures from bleeding while igniting emissive materials. |
| | Intensity | `1.2` (boosts to `4.8` on flare) | High-contrast neon arcade glow for cells, ghost eyes, and flares without point lights. |
| | Scatter | `0.7` | Soft, atmospheric dispersion simulating thick dusty maze air. |
| | Tint | `#FFFFFF` | Neutral baseline tint; color is driven by material HDR emission. |
| **Vignette** | Color | `#000000` | Classic horror claustrophobia edge darkening. |
| | Intensity | `0.35` (clamps to `0.85` on catch) | Subtle in normal play; violently constricts peripheral vision upon ghost catch. |
| | Smoothness | `0.45` | Soft falloff keeping center gameplay area legible. |
| **Film Grain** | Type | `Medium` | 1980s analog CRT/film aesthetic. |
| | Intensity | `0.25` (scales up to `0.95`) | Dynamically surges as flashlight battery depletes below 15% and on death. |
| | Response | `0.8` | Reacts to dark areas, preserving grain in deep maze shadows. |
| **Color Adjustments** | Post Exposure | `0.0 EV` | Baseline exposure. Blasts to `+3.5 EV` during flare detonation. |
| | Contrast | `15.0` | Gritty horror tone separation. Spikes to `40.0` on death. |
| | Color Filter | `#FFFFFF` | Shifts to cold cyan `#73BFFF` during frightened ghost phase. |
| | Saturation | `0.0` | Collapses to `-100.0` (pure monochrome) during player catch sequence. |

---

## 2. Event-to-Listener Binding Matrix

All audiovisual and lighting systems are completely decoupled from game actors. They listen strictly to Dev C's ScriptableObject event channels:

| Event Channel Asset (`Assets/.../Events/`) | Channel Type | Raised By | Audio Manager (`Game.Audio`) | Lighting Manager (`Game.Lighting`) | VFX Manager (`Game.VFX`) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `GameStateChanged.asset` | `GameStateEventChannel` | `GameManager` | `OnGameStateChanged()`<br>• `Ready`: Plays ready jingle, fades ambient.<br>• `Playing`: Starts sirens & ambient.<br>• `PlayerCaught`: Stops sirens, plays death sting.<br>• `LevelComplete`: Stops sirens, plays fanfare.<br>• `GameOver`: Stops ambient, plays game over cue. | `OnGameStateChanged()`<br>• `Ready`: Smooth ambient fade-up from black.<br>• `PlayerCaught`: Immediate black collapse.<br>• `LevelComplete`: Pulsing corridor wall sine waves.<br>• `GameOver`: Ambient fades to pitch black. | *No direct state coupling; listens to atomic gameplay events below.* |
| `ModeChanged.asset` | `GhostModeEventChannel` | `GhostDirector` | `OnModeChanged()`<br>• Crossfades between Scatter/Chase siren and high-anxiety Frightened alarm. | `OnModeChanged()`<br>• Shifts ambient light tint between dim horror baseline and cold frightened cyan. | *Ghost color shifts are handled per-entity by `GhostVisuals`.* |
| `PickupCollected.asset` | `PickupEventChannel` | Pickups (Dev A) | `OnPickupCollected()`<br>• `Cell`: Alternates waka cadence pitch.<br>• `Battery`: Plays battery recharge chime. | *No global lighting change (cells glow via Bloom).* | `OnPickupCollected()`<br>• Spawns pooled `PF_CellCollectFX` sparkle at pickup coordinates. |
| `FlareCollected.asset` | `VoidEventChannel` | Flare Pickup (Dev A) | `OnFlareCollected()`<br>• Plays 2D heavy ignition explosion blast. | `OnFlareCollected()`<br>• Triggers `PostProcessRig.TriggerFlareFlash()` (`+3.5 EV` flash to cold blue). | `OnFlareCollected()`<br>• Spawns pooled `PF_FlareIgnitionFX` 80-particle spark burst. |
| `GhostBanished.asset` | `GhostBanishedEventChannel` | `GhostDirector` (Dev B) | `OnGhostBanished()`<br>• 3D spatial playback at ghost position with combo pitch scaling ($1.0\times, 1.25\times, 1.5\times, 1.75\times$). | *No global change (handled via ghost visual eyes transition).* | `OnGhostBanished()`<br>• Spawns `PF_GhostBanishFX` tinted dynamically to the ghost's signature color (`info.Ghost.Color`). |
| `PlayerCaught.asset` | `VoidEventChannel` | `GhostDirector` (Dev B) | `OnPlayerCaught()`<br>• Plays horror jumpscare screech & mechanical thud. | `OnPlayerCaught()`<br>• Collapses ambient to 0 and triggers monochrome vignette clamp. | `OnPlayerCaught()`<br>• Spawns `PF_PlayerCaughtFX` screen distortion in front of camera. |
| `LevelComplete.asset` | `VoidEventChannel` | `GameManager` | `OnLevelComplete()`<br>• Retro victory chime. | `OnLevelComplete()`<br>• Pulsing emissive ambient sine waves across maze walls. | *No extra particles.* |
| `BatteryChanged.asset` | `FloatEventChannel` | Flashlight (Dev A) | *Optional battery hum modulation.* | `OnBatteryChanged()`<br>• Dims spotlight intensity, narrows cone, flickers below 15%, adds film grain. | *No particle impact.* |
| `PauseChanged.asset` | `BoolEventChannel` | `PauseController` | `OnPauseChanged()`<br>• Cleanly pauses/unpauses all active ambient, siren, and spatial audio sources. | *Time scale handles animation freeze.* | *Particles freeze via unscaled/scaled time.* |
| `ExtraLifeAwarded.asset` | `VoidEventChannel` | `ScoreSystem` | `OnExtraLifeAwarded()`<br>• Plays 10k arcade bonus chime. | *No lighting change.* | *No particle impact.* |

---

## 3. Standalone 60 FPS Performance Validation Checklist

Target: **Locked 60.0 FPS on 1080p Standalone PC (Target Frame Time: $\le 16.6\text{ ms}$)**

### A. Dynamic Light Budget Audit
- [ ] **Max Active Dynamic Lights $\le 8$**: Verify via `LightingManager` and `AudiovisualQAValidator` that total non-baked dynamic lights never exceed 8 simultaneous instances.
- [ ] **Zero Point Lights on Pellets**: All 240+ pellets must use [`M_PowerCell_Emissive`](file:///c:/Users/Duncan%20Joseph/Desktop/Repositories/Pacman-GameJam/Assets/_Project/Art/Materials/M_PowerCell_Emissive.mat) with URP Bloom. Never place a `Light` component on a pellet prefab.
- [ ] **Spotlight Shadow Optimization**: The player's flashlight spotlight is the only shadow-casting dynamic light in the scene. Ghost point lights must have shadows set to **None**.

### B. Zero-GC Memory Allocation Verification
- [ ] **Audio Pooling**: In Unity Profiler > CPU Usage (Hierarchy view), inspect `AudioManager.Update` and event callback invocations. `GC.Alloc` must read **0 B** during continuous playback, siren crossfades, and spatial sound dispatch.
- [ ] **VFX Pooling**: Spawning 100 cell collection particles or detonating flares must register **0 B** of `GC.Alloc` because all instances are pre-allocated inside `VFXManager`.
- [ ] **No `Update()` Polling**: All scripts (`AudioManager`, `LightingManager`, `VFXManager`, `FlashlightController`) operate purely on throttled intervals or event-driven triggers.

### C. Rendering & Draw Call Batching
- [ ] **SRP Batcher Compatibility**: Open the Frame Debugger (`Window > Analysis > Frame Debugger`). Confirm that corridor wall meshes and cell pickups render within `SRP Batch` calls rather than individual draw calls.
- [ ] **GPU Post-Processing Overhead**: Verify that Bloom thresholding and ACES Tonemapping take $\le 1.8\text{ ms}$ on the GPU by keeping Volume count to 1 global volume.
- [ ] **Particle System Budgets**:
  - `PF_CellCollectFX`: Max particles per instance $\le 16$, simulation space: `World`.
  - `PF_FlareIgnitionFX`: Max particles per instance $\le 100$, duration $\le 1.2\text{ s}$.
  - `PF_GhostBanishFX`: Max particles per instance $\le 60$, duration $\le 0.9\text{ s}$.

### D. Automated QA Tool Execution
Run the automated verification suite inside the Unity Editor:
1. Open `Assets/_Project/Scenes/Game.unity`.
2. Click **Game > QA > Validate Audiovisual & Performance Setup**.
3. Verify the console prints:  
   `[QA PASSED] All Audiovisual, Lighting, VFX, and Performance systems are 100% wired and budget-compliant for 60 FPS!`
