# AGENTS.md

Guidance for coding agents (Claude Code, Codex, and others) working in this repository. `CLAUDE.md` imports this
file. Keep rules here and descriptions in `docs/`.

## Project

**HotPatata** is a 2–4 player cooperative first-person parkour / hot-potato game: the team finishes a course while
throwing a live bomb between them.

- **Engine and packages:** Unity **6000.3.25f1** (6.3 LTS), URP 17.3, Input System 1.20, Netcode for GameObjects
  2.13, Multiplayer Services (Relay sessions, linked to Unity Cloud project HotPatata), Multiplayer Play Mode.
- **Target:** Windows PC (primary). Linux and macOS builds ship with each release, best effort (macOS unsigned).
- **Scenes:**
  - `Bootstrap` (entry: the in-world menu and lobby in the menu's factory hall);
  - `PatataWilds` (realistic outdoor course: forest, river, cliffs, dawn to dusk; 25 checkpoints in 5 acts, first in the
    menu; 20+ min target pending playtest);
  - `PatataCanopy` (tree-top course over the void in the same nature look; 25 checkpoints in 5 acts, every section with a
    contract; second in the menu);
  - `IndustrialPlant` (closed lamp-lit factory, eleven rooms, 9 checkpoints, third in the menu);
  - `PassSandbox` (one bomb, the kit demo, the pass range; last in the menu).
  - The older courses (`PrototypeCourse`, `PlaytestCourse`, `PatataPark`, `PatataWorks`, `IndustrialLab`) were removed;
    their obstacle layouts are archived in `docs/OBSTACLES.md` §3.2.
- **Code:** all gameplay code is in `Assets/Scripts/HotPatata.asmdef` (namespace `HotPatata`).
  - Dev-only code is in `HotPatata.DebugTools`.
  - Editor tools are in `Assets/EditorTools`.

**Status:** M0–M5 and M9 are done. Open human gates:

- M1.11 proof-of-fun;
- M3.7 two physical machines;
- the full-course playtest.

M10 (bomb obstacles, #68) is built; its group playtest is open. M6 (UX) and M7 (hardening, external playtest) follow.
M11 (`PatataWorks`, #83) was built, then removed in the cleanup; its layouts live on in the industrial plant.
M12 (`PatataWilds`) is built; the same human gates and a readability review remain open.
M13 (`PatataCanopy`, body screens, hands-free plates, switches, section contracts) is built, with its atmosphere pass (M13.7:
its own misty day, giant trees, a forest floor that follows the course); its playtest is open. M14
(contracts for PatataWilds) is next.
The live table is at the top of `docs/MVP_TASKS.md`.

## Docs (read before implementing)

| Doc | Role |
|---|---|
| `docs/PROJECT_SPEC.md` | Gameplay source of truth. **If a task conflicts with it, the spec wins.** Scope list in §17.2, tuning in §20. |
| `docs/ARCHITECTURE.md` | How things are built: components, authority model (§3, §13), networking and lag compensation (§13.1–13.2), feel (§7.6), bomb VFX (§8.6), course layout (§4), look (§25). |
| `docs/MVP_TASKS.md` | Ordered milestones with acceptance criteria and status. Work in order; do not skip criteria. |
| `docs/TESTING.md` | Test suites, running tests, dev tools, bots, latency simulation, CI. |
| `docs/OBSTACLES.md` | Every obstacle system, its looks, where the courses use it (removed courses archived), and how to make your own obstacle prefab. |

Keep the docs true: when a change alters behaviour, a rule or a milestone status, update the matching doc in the
same PR. Code comments cite doc sections (e.g. `PROJECT_SPEC §8.3`), so never renumber sections.

## Non-negotiable rules

- **Do not invent gameplay features.** Out of scope (spec §17.2): bounce/ricochet, the bomb resting on geometry,
  floor pickup, multiple bombs, combat, grabbing, matchmaking, and the rest of that list.
- **Host authoritative, single decision point.** Each of these is decided once, on the host: carrier, catch, fuse
  expiry, explosion, checkpoint, reset, finish.
  - Catch logic lives only in `CatchResolver`.
  - Rules run through `NetMode.IsAuthority`, which is true offline and on the host.
  - Clients only request (`NetworkPlayer.RequestThrow/RequestCatch/ClaimCatch`) and mirror state
    (`BombController.ApplyMirror`).
  - `Player.IsLocal` guards owner-only code.
- **Movement and time online.**
  - Teleports go through `Player.TeleportTo`; never move a player transform directly online. (The one exception:
    `NetworkPlayer` places a remote copy on its moving carrier, presentation of the owner's stamp.)
  - Level motion derives from `SectionClock` (server time), never `Time.time`. Any other time-driven pose or readout uses
    `SimulationClock.ServerNow` (one monotonic sample per frame), never `NetMode.ServerTime` directly.
  - A new mover implements `ITimePosed` (its pose at any time); a new carrier implements `IPlatformCarrier` with a
    registered `CarrierId` (docs/OBSTACLES.md §4.4).
  - Moving hazards are judged for remote players on the host at the time they saw them (`HazardRewind`), never from the
    host's copy of them.
- **Bomb state machine is explicit:** `Held → Thrown → CaughtGrace → Held`, plus `Thrown → InTransit → Thrown`
  (tubes and cannons), `Exploding` and `Resetting`.
  - While Thrown, any environment contact explodes the bomb.
  - While Held, world contact never fails it.
  - While InTransit, no fuse burns and nothing can catch or explode it.
- **Zones** (`Assets/Scripts/Zones`) are composed: a `Zone` volume plus effect components (`FuseZone`,
  `BombBarrier`, `BombGate`, `TransitMouth`). The host sweeps the flight against zones (`BombZoneSweep`); never rely on
  trigger events for a fast bomb. One signal source drives one actuator: no AND/OR wiring. A `SignalSwitch` is an
  actuator too (it turns objects on and off); a hands-free plate (`countCarrier` off) ignores its carrier; a body screen
  (`BodyScreen`) stops players only and is pure physics.
- **Throw and catch.**
  - Catching is never automatic: `CatchResolver` accepts only while `PlayerCatcher.WindowOpen`.
  - The throw is a plain ballistic arc: no homing, no magnet (removed on purpose).
  - The aim assist never creates range.
- **Collision by layers or markers,** never object names. Layers: `Player`, `PlayerCatch`, `Bomb`, `Environment`,
  `Hazard`, `Trigger`, `BodyScreen` (collides with `Player` only).
- **All tuning goes in `GameTuning`** (`Assets/ScriptableObjects/Tuning/`). No magic numbers.
- **Separation of concerns.**
  - `PlayerMotor` knows no bomb rules.
  - Presentation and audio (`BombPresentation`, `BombAudio`, `PlayerPresentation`, VFX) consume state and never
    control it.
  - Use `PlayerMotor.MotionFraction` for speed-driven presentation.
- **Removed on purpose, do not add back:** movement sounds (footsteps, landings, wind, slide scrape), chromatic
  aberration, lens distortion. PatataWilds' fixed world ambience (river, falls, birds, crickets, summit wind:
  `NatureKit.Ambient`) is not a movement sound: it never follows the player's speed.
- **Accessibility.** Every view effect honours `viewEffectsStrength`, and every flash honours `flashReduction`.
  - Read player-facing values (those two, `beepVolume`, look sensitivity, FOV, invert Y) through `Settings`, never
    the `GameTuning` field: the tuning value is only the default. Never write `GameTuning` at runtime.
- **Post-processing.** Read `Volume.sharedProfile`, never `.profile` (it clones).
- **Course.** `IndustrialPlant` is generated by `IndustrialPlantBuilder.cs` (menu `HotPatata/Course/Build Industrial
  Plant`), `PatataWilds` by `PatataWildsBuilder.cs` + `.Acts.cs` (menu
  `HotPatata/Course/Build PatataWilds`; its terrain and scatter by `NatureDressing`, its trees by `NatureTreeBuilder`) and
  `PatataCanopy` by `PatataCanopyBuilder.cs` + `.Acts.cs` (its forest in `.Forest.cs`, its air in `.Atmosphere.cs`; menu `HotPatata/Course/Build PatataCanopy`):
  change the builder and rebuild, never edit them by hand. A new section declares a `SectionContract` (spec §13.20, how-to
  in docs/OBSTACLES.md §5): what it forces, and each shortcut with the geometry that locks it (14 m gaps, faces of 3.4 m or
  more, walls up to a roof; a window a player fits through is not bomb-only). `CourseContractCheck` must report nothing. The level list's order lives in one place,
  `CourseKit.RegisterInMenu`. The living menu
  backdrop in `Bootstrap` (`Assets/Prefabs/Menu/MenuBackdrop.prefab`) is generated the same way by
  `MenuBackdropBuilder.cs` (menu `HotPatata/Menu/Build Menu Backdrop`); it holds visual copies only, never gameplay
  components. Shared helpers live in `CourseKit.cs` (`PrepareCourseScene`, `ValidatePasses`, `RegisterInMenu`). Keep
  decoration outside declared `PassCorridor` volumes, scattered plants included. In
  PatataWilds every water surface gets a kill zone under it (`NatureKit.Water`), and a checkpoint is a campfire, never a
  pad.
- **Players' look.** Each slot shows its Mixamo character (Humanoid, `PlayerCharacter`), generated by
  `PlayerCharacterBuilder` (menu `HotPatata/Player/Build Characters`, then rebuild the menu backdrop): never edit the
  character prefabs or their controllers by hand. A new clip goes in `Assets/Art/Models/Characters/Animations`, set up
  by the builder. Choosing your character is out of scope (spec §17.2 cosmetics).
- **Light and grade.** The cinematic golden-hour look (ARCHITECTURE §25) comes from `LookBuilder` (menu
  `HotPatata/Look/Apply Look To All Scenes`): sun, ambient, fog, each scene's sky and the look volume. Each nature course has
  its own day (five `TimeOfDayPreset`s, its blended HDRI sky and grade volumes), written by `LookBuilder` through
  `NatureDayLook` with its table (`PatataWildsLook`, `PatataCanopyLook`); `TimeOfDayBlender` only interpolates them from the
  current checkpoint (presentation). The height mist reads a `MistField` baked by the course builder; every HotPatata shader
  fogs through `HP_MixFog` (`HotPatataFog.hlsl`), never `MixFog` alone. Never set them by hand in a scene; change the builder
  and re-apply. The shared shader is `HotPatata/Stylized` (file
  `HotPatataToon.shader`): keep its property names, materials and builders rely on them.
- **Look.** Kit visuals are KayKit pieces drawn by `KitSkin` (ARCHITECTURE §25.1): give a box a `KitRole`, never a
  greybox material. PatataWilds draws the same roles in the nature look (`LookSet.Nature`, ARCHITECTURE §25.3:
  generated rock, log and plank shapes, `HotPatata/Nature`, Poly Haven assets fetched by `tools/Fetch-PolyHaven.ps1` and
  credited in `CREDITS.md`). Rebuild with `HotPatata/Course/Build KayKit Kit`, then `HotPatata/Course/Rebuild All Courses`.
- **Prefabs.**
  - Kit prefabs go in `Assets/Prefabs/{Platforms,Obstacles,Gameplay}`; variants (same logic, another visual or motion) in
    `Assets/Prefabs/Variants`, made as Prefab Variants (docs/OBSTACLES.md §4).
  - Obstacle systems are generic: logic reads serialized references, never child names or renderers. A hand-made visual
    sits under a `CustomVisual` (renderers only; the look passes skip it) and is animated from the state
    (`IObstacleState`, `ObstacleVisualDriver`), never by writing the state. Every obstacle prefab must pass
    `ObstaclePrefabValidator` (`ObstaclePrefabTests` checks them all).
  - No Find-by-name lookups.
  - Stateful objects implement `IResettable`.
  - Zones use `PlayerZone.Collect`.
  - Anything a player rides implements `IPlatformCarrier`.
- **UI.** Screens and the HUD are UI Toolkit on the `ScreenStack` (ARCHITECTURE §6.2); world-space UI is UI Toolkit
  too (world-space panels, never uGUI); IMGUI only for dev tools. A screen's layout is one UXML in `Assets/UI/Screens`
  (`hp-*` classes only, listed in `UIScreenCatalog`); its code finds kebab-case named elements with `Require<T>` and
  never builds layout. The main menu and the lobby are in-world stations (boards + Cinemachine camera spots) placed by
  `MenuBackdropBuilder`, never by hand; `MenuCameraRig` alone moves the menu camera, and travel honours
  `viewEffectsStrength` (0 = a cut). Only `CursorPolicy` sets the cursor lock. A menu on top blocks the local player's gameplay
  input through `PlayerInputReader` (read input there, never from devices directly). Online, a menu never pauses the
  game; offline the pause menu freezes it.
- **Input System only** (never the legacy input manager). **No third-party packages** unless the built-in stack
  clearly cannot do the job. Unity's own Cinemachine package is allowed (owner decision, #79: the menu camera).
- "Beep" (`beepIntervals`, `BombAudio.Beeped`) is a gameplay term for the fuse sound, not the old project name. Keep it.

## Workflow

- Prefer Unity Editor / MCP operations for scenes, GameObjects, prefabs, components and references. Hand-edit
  `.unity` / `.prefab` YAML only as a last resort.
- After each change, let Unity compile, check the Console, and fix errors before the next task. Never stack features on
  compile errors.
- Run only the test suites for the system you touched (see `docs/TESTING.md`). Run `PassFeelTests` after any
  throw/catch tuning change.
- **Never** use `simulate_key` or `InputTestFixture` against a live Editor: leftover virtual devices break all input.
  The recovery steps are in `docs/TESTING.md`.
- Online sessions: never call `NetworkManager.Shutdown` in session mode (use `SessionService.LeaveAsync`).
- Editor screenshots (`capture_game_view`) land under `Assets/`. Delete them, never commit them.
- `Library/`, `Temp/`, `Logs/` and `UserSettings/` are gitignored. Keep `.meta` files with their assets.
- Git: branch from `main`, sync with `git merge origin/main` (not rebase), and open a PR. CI builds only on `v*` tags (global
  `vX.Y.Z` = every platform, `vX.Y.Z-linux` = one platform; see `docs/TESTING.md` §4), not on pushes to `main`.
