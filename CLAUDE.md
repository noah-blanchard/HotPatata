# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project state

**BEEP!** (repo/project name "HotPatata") is a 2–4 player cooperative third-person parkour / hot-potato game: players finish a course while throwing a live bomb between them. Unity **6000.3.25f1** (Unity 6.3 LTS), URP 17.3, Input System 1.20, Windows PC target.

The project is at **Milestone 0**: there are no gameplay scripts, `.asmdef` files, prefabs, or tests yet. `Assets/` holds only the Unity URP template (`Scenes/SampleScene.unity`, `Settings/` render pipeline assets, `InputSystem_Actions.inputactions`, `TutorialInfo/`). Networking packages (Netcode for GameObjects, Multiplayer Services) are **not yet installed**. `MVP_TASKS.md` M3.1 covers installing them.

## Source-of-truth docs (read before implementing)

- `PROJECT_SPEC.md` is the gameplay source of truth. **If a task conflicts with it, the spec wins.** It defines the non-negotiable rules, bomb state model, tuning starting values, and the explicit out-of-scope list.
- `ARCHITECTURE.md` holds the intended technical structure: scenes, folder layout, component names, authority model, layer policy.
- `MVP_TASKS.md` is the ordered milestone plan (M0 → M8) with acceptance criteria. Work in order and do not skip acceptance criteria.

## Rules that shape all code

- **Do not invent gameplay features.** Out of scope: bounce/ricochet, bomb resting on geometry, floor pickup, multiple bombs, combat, grabbing, matchmaking, etc. (full list in spec §17.2).
- **Local first, then network.** Build and validate the local PassSandbox loop (move, jump, throw, catch, fuse, world-contact explosion, reset) before any networking or level content (`ARCHITECTURE.md` §15, `MVP_TASKS.md` M1.11 gate). Netcode work waits for Milestone 3.
- **Host authoritative, single decision point.** Bomb carrier, catch acceptance, fuse expiry, explosion, checkpoint activation, section reset, and finish are each decided once, on the host. Clients only send input or requests and display replicated state. Catch logic lives only in `CatchResolver`, never duplicated in player scripts.
- **Bomb state machine is explicit:** `Held → Thrown → CaughtGrace → Held`, plus `Exploding` and `Resetting`. While `Thrown`, contact with any environment collider is an immediate explosion (no bounce). Contact with a valid catch volume is a catch. While `Held`, world contact must never fail the bomb.
- **Collision by layers/markers, never object names.** Planned layers: `Player`, `PlayerCatch`, `Bomb`, `Environment`, `Hazard`, `Trigger`. Any bomb-safe surface needs an explicit marker component or layer.
- **All tuning goes in ScriptableObject(s)** (`GameTuning`, under `Assets/ScriptableObjects/Tuning/`). Do not scatter magic numbers. Starting values are in spec §20.
- **Separation of concerns:** `PlayerMotor` must not know bomb rules. Presentation and audio components (`BombAudio`, `BombPresentation`, `PlayerPresentation`) consume state and never control it.
- **Prefabs:** no Find-by-name runtime lookups or hidden scene-only dependencies. Stateful objects (moving/falling platforms, rotating bars) must support checkpoint reset. Use the Input System (never the legacy input manager).
- **Dependencies:** do not add third-party packages unless the built-in stack clearly can't do the job.

## Working with the Unity Editor

- Prefer Editor/MCP operations (the installed `unity` plugin and its `unity-cli` skill) for creating scenes, GameObjects, prefabs, components and serialized references, and for reading the Console. Hand-editing `.unity` / `.prefab` YAML is a last resort.
- After each change: let Unity compile, check the Console, and fix errors before starting the next task. Never stack features on top of compile errors.
- `Library/`, `Temp/`, `Logs/`, `UserSettings/` are gitignored. Keep `.meta` files with their assets.
- The planned tests use the Unity Test Framework (1.6.0 is installed): EditMode tests in `Assets/Tests/EditMode/` for pure logic (bomb state transitions, fuse math), PlayMode tests in `Assets/Tests/PlayMode/` for behavior. There are no tests yet and no build/lint scripts. Everything runs through the Unity Editor.

## Planned layout (not yet created, see `ARCHITECTURE.md` §4–5)

- Scenes: `Bootstrap`, `Lobby`, `PassSandbox` (build first), `PrototypeCourse`.
- `Assets/Scripts/` is split into `Core`, `Networking`, `Player`, `Bomb`, `Run`, `Obstacles`, `UI`, `Debug`. Prefabs are grouped as `Player`, `Bomb`, `Gameplay`, `Platforms`, `Obstacles`.
- Runtime systems: `RunManager` (run state, checkpoint, reset order) with `Checkpoint`, `KillZone`, `FinishZone`. The bomb is `BombController` + `BombFuse` + `BombPhysics` + `CatchResolver`. The player is `PlayerMotor` + `PlayerLook` + `PlayerThrower` + `PlayerCatchVolume`.
- Development logs use a concise `[Bomb]` / `[Run]` prefix format that can be switched off (`ARCHITECTURE.md` §19).
