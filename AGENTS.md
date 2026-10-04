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
  - `Bootstrap` (entry: menu and lobby);
  - `PassSandbox` (one bomb, the kit demo, the pass range);
  - `PrototypeCourse` (~680 m, 7 checkpoints, 3 acts);
  - `PlaytestCourse` (~10 min, 9 checkpoints, the classic kit plus the #68 bomb obstacles);
  - `PatataPark` (~680 m, 9 checkpoints, the KayKit course; first in the menu).
- **Code:** all gameplay code is in `Assets/Scripts/HotPatata.asmdef` (namespace `HotPatata`).
  - Dev-only code is in `HotPatata.DebugTools`.
  - Editor tools are in `Assets/EditorTools`.

**Status:** M0–M5 and M9 are done. Open human gates:

- M1.11 proof-of-fun;
- M3.7 two physical machines;
- the full-course playtest.

M10 (bomb obstacles, #68) is built; its group playtest is open. M6 (UX) and M7 (hardening, external playtest) follow.
The live table is at the top of `docs/MVP_TASKS.md`.

## Docs (read before implementing)

| Doc | Role |
|---|---|
| `docs/PROJECT_SPEC.md` | Gameplay source of truth. **If a task conflicts with it, the spec wins.** Scope list in §17.2, tuning in §20. |
| `docs/ARCHITECTURE.md` | How things are built: components, authority model (§3, §13), networking and lag compensation (§13.1–13.2), feel (§7.6), bomb VFX (§8.6), course layout (§4), look (§25). |
| `docs/MVP_TASKS.md` | Ordered milestones with acceptance criteria and status. Work in order; do not skip criteria. |
| `docs/TESTING.md` | Test suites, running tests, dev tools, bots, latency simulation, CI. |

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
  - Teleports go through `Player.TeleportTo`; never move a player transform directly online.
  - Level motion derives from `SectionClock` (server time), never `Time.time`.
- **Bomb state machine is explicit:** `Held → Thrown → CaughtGrace → Held`, plus `Thrown → InTransit → Thrown`
  (tubes and cannons), `Exploding` and `Resetting`.
  - While Thrown, any environment contact explodes the bomb.
  - While Held, world contact never fails it.
  - While InTransit, no fuse burns and nothing can catch or explode it.
- **Zones** (`Assets/Scripts/Zones`) are composed: a `Zone` volume plus effect components (`FuseZone`,
  `BombBarrier`, `BombGate`, `TransitMouth`). The host sweeps the flight against zones (`BombZoneSweep`); never rely on
  trigger events for a fast bomb. One signal source drives one actuator: no AND/OR wiring.
- **Throw and catch.**
  - Catching is never automatic: `CatchResolver` accepts only while `PlayerCatcher.WindowOpen`.
  - The throw is a plain ballistic arc: no homing, no magnet (removed on purpose).
  - The aim assist never creates range.
- **Collision by layers or markers,** never object names. Layers: `Player`, `PlayerCatch`, `Bomb`, `Environment`,
  `Hazard`, `Trigger`.
- **All tuning goes in `GameTuning`** (`Assets/ScriptableObjects/Tuning/`). No magic numbers.
- **Separation of concerns.**
  - `PlayerMotor` knows no bomb rules.
  - Presentation and audio (`BombPresentation`, `BombAudio`, `PlayerPresentation`, VFX) consume state and never
    control it.
  - Use `PlayerMotor.MotionFraction` for speed-driven presentation.
- **Removed on purpose, do not add back:** movement sounds (footsteps, landings, wind, slide scrape), chromatic
  aberration, lens distortion.
- **Accessibility.** Every view effect honours `viewEffectsStrength`, and every flash honours `flashReduction`.
  - Read player-facing values (those two, `beepVolume`, look sensitivity, FOV, invert Y) through `Settings`, never
    the `GameTuning` field: the tuning value is only the default. Never write `GameTuning` at runtime.
- **Post-processing.** Read `Volume.sharedProfile`, never `.profile` (it clones).
- **Course.** Acts 2–3 are generated by `Assets/EditorTools/CourseBuilder.cs` (menu
  `HotPatata/Course/Build Acts 2-3`), the whole `PlaytestCourse` by `PlaytestCourseBuilder.cs` (menu
  `HotPatata/Course/Build Playtest Course`) and the whole `PatataPark` by `PatataParkBuilder.cs` (menu
  `HotPatata/Course/Build Patata Park`): change the builder and rebuild, never edit them by hand. The living menu
  backdrop in `Bootstrap` (`Assets/Prefabs/Menu/MenuBackdrop.prefab`) is generated the same way by
  `MenuBackdropBuilder.cs` (menu `HotPatata/Menu/Build Menu Backdrop`); it holds visual copies only, never gameplay
  components. Shared helpers live in `CourseKit.cs`. Keep decoration out of pass paths (|x| ≥ 25 m).
- **Look.** Kit visuals are KayKit pieces drawn by `KitSkin` (ARCHITECTURE §25.1): give a box a `KitRole`, never a
  greybox material. Rebuild with `HotPatata/Course/Build KayKit Kit`, then `HotPatata/Course/Rebuild All Courses`.
- **Prefabs.**
  - Kit prefabs go in `Assets/Prefabs/{Platforms,Obstacles,Gameplay}`.
  - No Find-by-name lookups.
  - Stateful objects implement `IResettable`.
  - Zones use `PlayerZone.Collect`.
  - Anything a player rides implements `IPlatformCarrier`.
- **UI.** Screens and the HUD are UI Toolkit on the `ScreenStack` (ARCHITECTURE §6.2), uGUI only for world space,
  IMGUI only for dev tools. A screen's layout is one UXML in `Assets/UI/Screens` (`hp-*` classes only, listed in
  `UIScreenCatalog`); its code finds kebab-case named elements with `Require<T>` and never builds layout. Only `CursorPolicy` sets the cursor lock. A menu on top blocks the local player's gameplay
  input through `PlayerInputReader` (read input there, never from devices directly). Online, a menu never pauses the
  game; offline the pause menu freezes it.
- **Input System only** (never the legacy input manager). **No third-party packages** unless the built-in stack
  clearly cannot do the job.
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
