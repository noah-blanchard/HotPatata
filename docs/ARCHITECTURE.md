# HotPatata — ARCHITECTURE.md

> This document describes the technical structure of the MVP: what was planned and, where it differs, what was built
> ("As built" notes). [`PROJECT_SPEC.md`](PROJECT_SPEC.md) is the gameplay source of truth; testing and tooling live in
> [`TESTING.md`](TESTING.md).
>
> Prefer simple, inspectable Unity components over framework-heavy abstractions.
>
> Section numbers are cited from code comments: add sub-sections, never renumber.

---

## 1. Technical goals

The architecture must support:

- rapid gameplay iteration;
- local pass-sandbox testing;
- 2–4 player online sessions;
- host-authoritative bomb gameplay;
- reusable obstacle prefabs;
- fast checkpoint reset;
- editor-driven level assembly;
- coding-agent operation through Unity Editor / MCP when available.

The architecture does **not** need to solve:

- dedicated-server scale;
- competitive anti-cheat;
- large persistent worlds;
- hundreds of network entities;
- complex persistence.

---

## 2. Recommended stack

Target:

- **Unity 6.3 LTS**
- **Input System**
- **Netcode for GameObjects**
- **Unity Multiplayer Services**
- **Sessions / Relay** for friend-hosted online sessions
- **Multiplayer Play Mode** or equivalent local multi-instance workflow for testing

As built: Unity **6000.3.25f1** (6.3 LTS), URP 17.3, Input System 1.20, Netcode for GameObjects 2.13, Multiplayer
Services (Sessions + Relay, project linked to Unity Cloud), Multiplayer Play Mode, glTFast (potato model). All gameplay
code is in one assembly, `Assets/Scripts/HotPatata.asmdef` (namespace `HotPatata`); Editor/dev-only code is in
`HotPatata.DebugTools` (`Assets/Scripts/DebugTools`) and `HotPatata.EditorTools` (`Assets/EditorTools`).

The target topology is:

```text
Player 2 ─┐
Player 3 ─┼── Relay / session transport ── Host
Player 4 ─┘
```

The host owns authoritative game decisions.

---

## 3. Authority model

### 3.1 Host authoritative

The host/server instance is authoritative for:

- bomb state;
- current bomb carrier;
- bomb release;
- catch acceptance;
- bomb environment collision failure;
- fuse expiration;
- explosion;
- section state;
- active checkpoint;
- team reset;
- finish/completion state;
- synchronized obstacle timing when gameplay-critical.

### 3.2 Client responsibility

Each client is responsible for:

- gathering local input;
- local camera;
- local presentation;
- requesting movement/actions;
- displaying replicated game state;
- optional prediction/interpolation.

### 3.3 Important rule

A catch is decided **exactly once** by the authoritative gameplay path.

Do not let multiple clients independently finalize bomb ownership.

Likewise, only the authoritative game state may cause an explosion/reset.

---

## 4. Scene plan

### `Bootstrap`

Purpose:

- initialize Unity services;
- initialize networking/session services;
- persist minimal connection/session manager objects;
- route to menu/lobby.

Expected root objects:

```text
Bootstrap
├── AppRoot
│   ├── ServiceBootstrap
│   ├── SessionManager
│   └── SceneFlow
└── EventSystem
```

As built: the `Bootstrap` scene holds `BootstrapEntry`, which instantiates the persistent `NetworkManager` prefab
(`Assets/Prefabs/Network`: `NetworkManager` + `UnityTransport` + `NetworkBootstrap`) exactly once. `NetworkBootstrap`
runs the session flow (Host Online / Join with code / Play Local / Direct IP, the level from `gameplayScenes`) and
`SessionService` wraps Multiplayer Services (§13.1); the menu and the lobby are in-world UI Toolkit boards on the
menu island, driven by `MenuView` (§6.2, #79).

### `Lobby`

As built: **not a separate scene.** The lobby is a phase of `NetworkBootstrap` in `Bootstrap`, shown on the Lobby
station's board (`LobbyStation`: session code, player list with host marker, Start for the host; the players step onto
the stage in front of it; §6.2). The Level station (before hosting, and for the host from the lobby) also picks the **spawn point**
(`Start` or `CP1`..`CPn`, stored in `RunOptions.StartCheckpoint`): `RunManager` begins the run, and every rematch, as
if the team had just reached that checkpoint (its spawns, carrier slot and fuse; earlier checkpoints count as reached).
It is a practice aid; only the authority's choice matters. The original plan follows.

Purpose:

- create session;
- join by code;
- display connected players;
- optional ready state;
- host starts run.

Suggested root:

```text
Lobby
├── LobbyController
├── UI
│   ├── HostPanel
│   ├── JoinPanel
│   ├── PlayerList
│   └── StartButton
├── Camera
└── EventSystem
```

### `PassSandbox`

Purpose:

- prove movement + throw + catch + fuse + failure;
- minimal geometry;
- no art dependency.

Suggested root:

```text
PassSandbox
├── RunManager
├── SectionRoot
│   ├── Floor
│   ├── Platform_A
│   ├── Platform_B
│   ├── Spawn_01
│   ├── Spawn_02
│   ├── Spawn_03
│   ├── Spawn_04
│   ├── BombSpawn
│   └── KillZone
├── Lighting
└── Main Camera / player cameras as required
```

As built: one bomb, a `KitDemo` group showing every kit prefab, and no pre-placed players: `PlayerSpawner` builds the
offline two-player rig or, on the host, one networked player per connection. A pass range lane on the east side has
marks at 4 / 8 / 12 / 16 m (used by `PassFeelTests` and the F4/F5 practice bot, see [`TESTING.md`](TESTING.md)).

### `PrototypeCourse` (Milestone 5, extended with Acts 2-3)

One straight course along +Z (~680 m, about 8-10 minutes, 7 checkpoints), every piece a kit prefab, all parented under
`SectionRoot/Course`; one large `KillZone` under everything. Players always start on the safe court, 6 m apart.
Act 1 (beats A-G) is hand-kept in the scene. Acts 2-3 live under `Course/Act2` and `Course/Act3` and are generated by
`Assets/EditorTools/CourseBuilder.cs` (menu **HotPatata/Course/Build Acts 2-3**, idempotent: it rebuilds only those
two groups and `Backdrop/Extension`, moves the `FinishZone`, sizes the `KillZone`). Change Acts 2-3 in the builder,
not by hand.

**Act 1: Training Grounds**

| Beat | z (m) | What it asks |
|---|---|---|
| A Safe Court | 0-30 | flat and wide: learn charge, throw, timed catch at 6 m |
| B First Gap | 30-48 | 4.5 m pit, broad landing: the pass is the challenge, not the jump |
| C Stair Relay | 51-79 | two 1.2 m steps up, then a 1.8 m step (C3, above the jump height: jump and **mantle**), big recovery space: relay the bomb upward |
| Checkpoint 1 | 87 | after the teaching beats |
| D Moving Pair | 93-106 | two slow platforms that periodically line up: wait or risk it |
| E Split Lanes | 118-154 | centre wall with two openings, a blocker in each lane: the bomb must cross between lanes |
| Checkpoint 2 | 162 | |
| F Vertical Catch | 170-204 | a `LaunchPad` throws the receiver 6 m up; catch the bomb near the top, land on the broad 8.4 m ledge |
| Checkpoint 3 | 199 | |
| G Final Sprint | 204-298 | a 7.6 m entry gap from F2 that needs a **sprint** jump (F2 is the run-up), narrow hops, a **low bar** on `G_Landing_1` (1.3 m clearance: slide or crouch under, too tall to mantle), three falling platforms, faster moving pair, onto `G_Finish` |

**Act 2: Patata Factory** (floor 8.4-9.6 m, normal fuse)

| Beat | z (m) | What it asks |
|---|---|---|
| Checkpoint 4 | 291.6 | on `G_Finish` (the old finish) |
| H Conveyor Hall | 300-347 | three `Conveyor` belts, 1.5 m apart: sides carry forward (3 m/s), the centre carries back (4 m/s); a hurdle on each side belt. Receivers drift, so throws must lead |
| I Piston Alley | 349-385 | five `Obstacle_Piston` floor tiles rise and fall 3 m in a wave: a raised tile blocks the pass along the corridor, the receiver's tile is a moving target |
| Checkpoint 5 | 392 | |
| J Windmill Wall | 404-433 | an 11 m wall; the bomb goes through a hole guarded by lethal `Obstacle_Windmill` blades (balcony to balcony), players ride `Platform_Elevator`s at the sides over the top and drop |
| K Sweeper Pit | 435-481 | two knee-high lethal `Obstacle_Sweeper`s turning opposite ways (jump them); the exit runs under an `Obstacle_Crusher` (1.45 m clearance when down: slide, crouch, or throw the bomb low beneath it) |

**Act 3: The Climb & The Drop**

| Beat | z (m) | What it asks |
|---|---|---|
| Checkpoint 6 | 488 | fuse 5 s from here |
| L Elevator Tower | 483-548 | 9.6 → 30.6 m with the bomb: a `LaunchPad` (8 m) or an elevator to L1, an elevator to L2 (a sweeper on it), then an elevator or a 1.4 m mantle staircase to the summit |
| Checkpoint 7 | 541 | on the summit; fuse 4.5 s from here |
| M Mega Slide | 548-616 | 18° downhill, two lanes split by a low divider (two tall stretches block passes); three `Obstacle_Hoop`s spin over the divider. Slide at 15-18 m/s and pass sideways, leading the throw |
| N Factory Finale | 616-678 | run-out under a crusher, a forward belt through three piston gates, the `FinishZone` podium at z 671 |

The finish shows `RunResultsUI` (time, resets); the host presses R for a rematch (`RunManager.Restart`, which rearms
everything). Levels are chosen in the Bootstrap menu (`gameplayScenes` on `NetworkBootstrap`).

`CourseBuilder` also creates the Factory/Slide prefabs and materials if they are missing. `Checkpoint.holdFuseOverride`
changes the bomb's hold time from that checkpoint on (CP6 5 s, CP7 4.5 s). `PrototypeCourse/Backdrop` holds collider-free
decoration (islands, trees, clouds) kept at |x| >= 25 m from the course so it never enters a pass path (spec §3).
Course tests: `CourseTests` (Act 1, checkpoints, finish) and `FactoryCourseTests` (Acts 2–3).

(Original plan follows.)

Purpose:

- first 3–5 minute greybox course.

Suggested organization:

```text
PrototypeCourse
├── RunManager
├── Checkpoints
│   ├── CP_00
│   ├── CP_01
│   ├── CP_02
│   └── CP_03
├── Sections
│   ├── SafeCourt
│   ├── FirstGap
│   ├── StairRelay
│   ├── MovingPair
│   ├── SplitLanes
│   ├── VerticalCatch
│   └── FinalSprint
├── FinishZone
├── Lighting
└── Environment
```

### `PlaytestCourse` (M10, #68)

The playtest course for the bomb obstacles: one straight course along +Z (~820 m, about 10 minutes, 9 checkpoints),
generated **entirely** by `Assets/EditorTools/PlaytestCourseBuilder.cs` (menu **HotPatata/Course/Build Playtest
Course**). The scene was created as a copy of `PrototypeCourse` (run manager, bomb, lighting, look, spawner, test rig,
start spawns on `SectionRoot`); every build rebuilds `SectionRoot/Course` (one group per act), the backdrop, the kill
zone and the finish. Rebuild it after rebuilding the kit (**HotPatata/Course/Build KayKit Kit**). It is second in the
Bootstrap level list, after `PatataPark`. Each new obstacle is taught alone, then combined, then twisted.

**Act 1: Warm-up** (the classic kit, floor 0 → 3.8 m)

| Beat | z (m) | What it asks |
|---|---|---|
| A Start Court | -2–30 | flat and wide, the pair starts 6 m apart |
| B First Gap | 30–50 | 4.5 m pit, broad landing |
| C Stair Relay | 50–74 | two 1.2 m steps, a 1.4 m mantle |
| Checkpoint 1 | 80 | |
| D Moving Pair | 86–98 | two platforms sliding across in opposite phase |
| E Conveyor Hall | 110–146 | side belts forward, centre belt back, hurdles |
| Checkpoint 2 | 152 | |
| F Sweeper Pit | 158–188 | two knee-high lethal sweepers turning opposite ways |

**Act 2: Hot & Cold** (fuse zones and laser curtains, §13.13–§13.14)

| Beat | z (m) | What it asks |
|---|---|---|
| H Forbidden Strips | 196–232 | a 4 m then a 6 m strip the carrier may not cross: walk over, throw across |
| I Laser Window | 232–266 | a wall with a window; runners go through the curtains on both sides |
| Checkpoint 3 | 260 | |
| J Hot Corridor | 266–314 | a 32 m hot zone (x2) with two sweepers; two cold pockets (x0.5) on side ledges |
| K Laser Slalom | 314–362 | three walls, the window alternates sides; the last window is a spinning hoop |
| Checkpoint 4 | 368 | |

**Act 3: Switchboard** (gates, plates, actuators, §13.15, §13.17)

| Beat | z (m) | What it asks |
|---|---|---|
| L Gate Bridge | 374–396 | a pass through a ring extends a bridge over a 12 m gap for 8 s |
| M The Lock | 396–434 | a plate holds a door open; the doorway is a curtain, so the bomb goes through the window; the plate holder climbs over the wall (no carrying on top) while the catcher waits in a cold pocket |
| Checkpoint 5 | 428 | |
| N Shutter and Crusher | 434–470 | the window's shutter opens on a rhythm; runners slide under a crusher in a tunnel |
| Checkpoint 6 (arch) | 480 | throw through the arch across a 4 m gap to a teammate on the pad |
| O Switch Chain | 474–540 | ring 1 raises a lift up a 5 m cliff (12 s), ring 2 extends the next bridge (8 s) |
| Checkpoint 7 | 530 | |

**Act 4: Grand Finale** (tubes and cannon, §13.16; fuse 5 s from checkpoint 8)

| Beat | z (m) | What it asks |
|---|---|---|
| P Tube Intro | 540–582 | the bomb goes through a tube, the runners through a curtain |
| Q Tube Junction | 582–624 | three lanes the bomb cannot cross (falling platforms, a sweeper, a reverse belt); three mouths, one per lane, each landing on that lane's pad |
| Checkpoint 8 | 632 | fuse 5 s |
| R Cannon Canyon | 624–702 | the cannon fires the bomb 44 m onto the far pad while the team rides two shuttles; a cold pocket beside the cannon |
| S Mega Slide | 702–739 | 12 m down at 18°, two lanes, hoops over the divider, a hot zone on the lower half |
| T Finale | 739–822 | belt through two piston gates, checkpoint 9 (arch, fuse 5 s), a last ring opens the podium door, `FinishZone` at z 815 |

Tests: `PlaytestCourseTests`. PassSandbox also has a small `KitDemo/BombObstacles` corner (menu **HotPatata/Course/Build
Sandbox Bomb Obstacles**) to try the zones, curtains and a ring-driven lift with the F4 bot.

### `PatataPark` (the KayKit course)

The first course built for the KayKit look (§25.1), after PatataWorks in the Bootstrap level list: one course along +Z (~680 m,
9 checkpoints, 4 acts) laid out on the KayKit grid (floors in whole metres, mostly 4 and 6 m tiles, 1 m steps), with
the classic kit and the #68 obstacles in new combinations. Generated **entirely** by
`Assets/EditorTools/PatataParkBuilder.cs` (menu **HotPatata/Course/Build Patata Park**). The scene was created as a
copy of `PlaytestCourse` (run manager, bomb, lighting, look, spawner, test rig, start spawns on `SectionRoot`); every
build rebuilds `SectionRoot/Course`, the kill zone and the backdrop (floating KayKit islands at |x| >= 34 m, no
colliders, plus clouds), and keeps the scene in the build and in `NetworkBootstrap.gameplayScenes`.

**Act 1: Block Hop** (classic kit, floor 0 → 6 m)

| Beat | z (m) | What it asks |
|---|---|---|
| A Start | -6–18 | a wide court, the pair starts 6 m apart |
| B Island Hop | 21–58 | three 6 m islands zig-zag up 1 m each, then a landing: teammates stand on different islands, so pass |
| C Ramp | 58–82 | 4 m up over 12 m (18°) to the first plateau |
| Checkpoint 1 | 76 | |
| D Falling Run | 82–106 | four falling platforms zig-zag over a 24 m pit |
| E Piston Wave | 118–154 | five floor tiles rise and fall 3 m in a wave; a raised tile blocks the pass |
| Checkpoint 2 | 161 | |
| F Windmill Wall | 167–199 | a sweeper, then an 8 m wall: the bomb through the windmill's hole, the runners through a tunnel under a crusher |
| Checkpoint 3 | 194 | |

**Act 2: Hot & Cold** (floor 6 → 10 m)

| Beat | z (m) | What it asks |
|---|---|---|
| G Forbidden Bridges | 199–243 | two bridges over a pit, each with a staggered no-carry strip: the bomb crosses the 6 m gap twice |
| H Laser Slalom | 243–291 | three laser walls, the window alternates sides, the last window is a spinning hoop |
| Checkpoint 4 | 287 | |
| I Hot Climb | 291–319 | three 1 m steps under a hot zone (x2), a cold pocket at the top, a sweeper |

**Act 3: Switchboard** (floor 10 → 16 m)

| Beat | z (m) | What it asks |
|---|---|---|
| J Ring Bridge | 315–331 | a pass through a ring extends a bridge over a 12 m gap for 8 s |
| K The Lock | 331–367 | a plate holds the door (a curtain) open, the bomb takes the window; the plate holder climbs 1 m stairs and mantles over the wall (no carrying on top) while the catcher waits in a cold pocket |
| Checkpoint 5 | 362 | |
| Checkpoint 6 (arch) | 377 | throw through the arch across a 4 m gap to a teammate on the pad |
| M Lift Chain | 387–437 | ring 1 raises a lift up a 6 m cliff (12 s), ring 2 extends the next bridge (8 s) |
| Checkpoint 7 | 427 | |

**Act 4: Sky Finale** (tubes, cannon, slide; fuse 5 s from checkpoint 8)

| Beat | z (m) | What it asks |
|---|---|---|
| N Tube Junction | 437–509 | three lanes the bomb cannot cross (falling platforms, a sweeper, a reverse belt), one tube mouth per lane |
| Checkpoint 8 | 487 | fuse 5 s |
| O Cannon Canyon | 503–557 | the cannon fires the bomb 44 m onto the far pad while the team rides two shuttles; a cold pocket by the cannon |
| P Sky Slide | 557–594 | 12 m down at 18°, two lanes, hoops over the divider, a hot zone on the lower half |
| Q Finale | 594–674 | belt through two piston gates, checkpoint 9 (arch, fuse 5 s), a last ring opens the podium door, `FinishZone` at z 667 |

Tests: `PatataParkTests`.

### `PatataWorks` (M11, #83)

Generated by `PatataWorksBuilder` (**HotPatata/Course/Build PatataWorks**), also in **Rebuild All Courses**.
Its first build copies the tested PlaytestCourse infrastructure (run, bomb, spawns, camera, network components),
then replaces the backdrop and course geometry. Later builds replace the generated factory. Never edit the scene by
hand. The course is first in `NetworkBootstrap.gameplayScenes`, with nine checkpoint choices.

Rooms are authored along local +Z, rotated as a unit, and connected at different heights. The route spans 216 m in X
and 176 m in Z, makes left and right turns, climbs 52 m in total and descends 28 m. The builder creates an outer
factory shell with windows and a roof; the atrium and boiler shaft are open inside the building, and the roof is open
to the sky.

| Section | Entrance, heading | Height at exit | Bomb question |
|---|---|---:|---|
| Loading dock | (0, 0, 0), north | 0 | crate warm-up; pass through a ring to open the dock door |
| Atrium | (0, 0, 48), north | 14 | two ramp flights; receiver's middle-landing plate raises a lift; a cross-landing pass opens the return door |
| Sorting hall | (0, 14, 112), east | 14 | opposing belts, windows, two colour-and-symbol tube routes, no-carry inspection strips, piston and timed door |
| Silo catwalks | (96, 14, 112), north | 18 | low full-power pass under a ceiling, falling grates and a crane platform |
| Cold storage | (96, 18, 176), west | 18 | cold lane around shelves versus straight hot lane; laser window |
| Chute | (-72, 18, 176), south | -10 | 28 m slide with no-carry strips; the bomb takes its own tube |
| Basement furnaces | (-72, -10, 104), west / south / east | -10 | hot press, windmill window, low crusher passage, cold respite, sweeper |
| Boiler shaft | (-72, -10, 40), south | 24 | four rising platforms; a cannon from the penultimate landing reaches the upper pad |
| Roof | (-72, 24, 16), east / north | 24 | chimneys, final relay, checkpoint arch and finish below the PatataWorks board |

The cannon covers the **last** 8.5 m of the shaft. Both players can relay up the earlier platforms together, so the
carrier need not wait at the bottom with a short fuse while the receiver climbs 34 m. The atrium plate is on the
receiver landing; its holder can return by the stairs. These are design provisions for a two-player team, not proof
that players can clear every puzzle. First-clear duration and checkpoint timing remain unmeasured.

**Enclosed-course rules:**

- Floors, one-metre walls, frames and ceilings use `Environment` colliders. Ramps stay within the motor's slope
  limit; wall thickness exceeds the camera near plane. Moving parts use existing clocked and resettable components.
- `PassCorridor` stores an intended release/catch pair in its room's space. Normal passes solve the softest reachable
  low arc, low-ceiling passes use maximum throw speed, and transit exits use their fixed flight time. Sampling uses
  `ThrowBallistics` and `GameTuning`.
- `CourseCeiling` identifies structural ceilings separately from rings and timed hazards. The builder requires 1.5 m
  of upward clearance over every sampled point. PlayMode tests sweep bomb-sized clearance against solid geometry;
  timed passes skip their moving obstacles but still test fixed walls.
- `CourseDecoration` roots have no colliders; renderer bounds remain at least 0.5 m from sampled paths. Openings must
  be at least catch-radius + 0.5 m. A `CourseRoute` records room endpoints for extent, turn, height and ceiling tests.
  These components are test metadata and do not affect gameplay.
- Kill volumes protect the atrium, sorting, silo and chute pits; a shared basement recovery volume spans the building.
- Existing signal sources drive one actuator each (the finish arch instead claims its checkpoint). Tube mouths use
  the existing colour plus pip-count symbols, exit pad lamps and replicated timing.

Tests: `PatataWorksTests`. Reset/finish tests use teleports; the two-player clear, host with three clients, and
1080p/60 fps gates remain open in M11.6.

---

## 5. Project folders

As built:

```text
Assets/
├── Art/
│   ├── Materials/        kit materials (Greybox_*, Pad_*), stylized materials, a sky per scene (Sky_HotPatata_<Scene>)
│   ├── Models/           Bomb/ (potato.glb), Characters/ (Mixamo: James, Remy, The Boss; Animations/, Controllers/),
│   │                     Player/ (the old KayKit mannequin, unused), Map/ (KayKit Platformer Pack, CC0)
│   ├── Shaders/          HotPatata/Stylized (file HotPatataToon.shader), HotPatata/Particle, HotPatata/Sky, speed lines
│   ├── Textures/Icons/   zone and tube icons drawn in code by BombObstacleKitBuilder
│   └── VFX/              textures for particles and trails
├── Audio/                HotPatataMixer (Master > SFX); SFX/ optional real clips (see its README; procedural fallback otherwise)
├── EditorTools/          CourseKit, CourseBuilder, PlaytestCourseBuilder, PatataParkBuilder, PatataWorksBuilder, BombObstacleKitBuilder,
│                         KayKitKitBuilder, PlayerCharacterBuilder, MenuBackdropBuilder, LookBuilder
├── Prefabs/              Bomb/ Gameplay/ Menu/ (MenuBackdrop, generated) Network/ Obstacles/ Platforms/
│                         Player/ (Player; Characters/, generated) VFX/
├── Scenes/               Bootstrap, PassSandbox, PrototypeCourse, PlaytestCourse, PatataPark, PatataWorks
├── ScriptableObjects/    Tuning/GameTuning.asset, Kit/KayKitPalette.asset (generated)
├── Scripts/
│   ├── Core/             GameTuning, NetMode, SectionClock, IResettable, PatataLog, Settings, AudioVolumes
│   ├── Networking/       NetworkBootstrap, BootstrapEntry, SessionService, NetworkPlayer/Bomb/RunState/FallingPlatform,
│   │                     NetworkBombGate/SignalActuator/BombTransit
│   ├── Player/           Player, PlayerMotor, PlayerLook, PlayerThrower, PlayerCatcher, PlayerCatchVolume,
│   │                     FirstPersonCamera, PlayerViewFeel, SpeedEffects, PlayerPresentation, PlayerAnimator,
│   │                     PlayerCharacter, PlayerShapeMesh, PlayerPause, ...
│   ├── Bomb/             BombController, BombFuse, BombPhysics, CatchResolver, FlightHistory, AimAssist,
│   │                     ThrowBallistics, BombAudio, BombPresentation, ExplosionFx, ProceduralSfx
│   ├── Run/              RunManager, Checkpoint, KillZone, FinishZone, PlayerZone, PlayerSpawner, PlayerSpawn
│   ├── Obstacles/        MovingPlatform, RotatingObstacle, FallingPlatform, Conveyor, LaunchPad, IPlatformCarrier
│   ├── Kit/              KitSkin, KitPalette (KayKit visuals, §25.1)
│   ├── Menu/             the living menu backdrop and the in-world menu (§6.2): MenuHotPotato, MenuFloat,
│   │                     MenuCloudDrift, MenuCameraDrift, MenuFlow, MenuStation, MenuCameraRig, MenuLobbyStage
│   ├── Zones/            Zone, IBombZoneEffect, BombZoneSweep, FuseZone, BombBarrier, BombGate, PressurePlate,
│   │                     ISignalSource, SignalActuator, SignalIndicator, BombTransit, TransitMouth, TransitPresentation
│   ├── UI/               ScreenStack, UIScreen, UIScreenCatalog, UIParts, CursorPolicy (UI Toolkit base, §6.2),
│   │                     MenuView, StationScreens, MenuParts, CodeDials, PauseMenu, SettingsScreen, SettingRows,
│   │                     ConfirmScreen, ResultsScreen, RunResultsUI, AimReticle
│   ├── Debug/            DebugHud, LocalPlayerSwitcher, PlayerBot
│   └── DebugTools/       Editor/dev-build only: LatencySimulator, ThrowDebugOverlay, ThrowTelemetry, PassPartner,
│                         UISampleScreen
├── Settings/             URP assets (PC_RPAsset, PC_Renderer), Look/HotPatata_Look.asset, Look/MenuFocus_<Station>.asset
├── Tests/
│   ├── EditMode/
│   └── PlayMode/
└── UI/                   UI Toolkit (§6.2): Screens/ (one UXML per screen), Styles/ (HotPatata.uss, HotPatataTheme.tss),
                          Fonts/ (Lilita One, Nunito, SIL OFL), Icons/ (SVG), Resources/HotPatataPanel.asset
                          (PanelSettings), Resources/HotPatataScreens.asset (UIScreenCatalog)
```

Keep folder naming boring and predictable.

---

## 6. Core runtime components

Names are recommendations. The agent may adjust exact names only when a clear Unity convention improves the design.

### 6.1 `GameTuning`

One ScriptableObject, `GameTuning` (`Assets/Scripts/Core/GameTuning.cs`, asset
`Assets/ScriptableObjects/Tuning/GameTuning.asset`), grouped by `[Header]`:

```text
Movement                 run speed, progressive acceleration, braking, air control, jump, gravity, coyote, buffer
Movement - flow          turn rates, reverse angle, overspeed deceleration, air drag
Movement - sprint        sprintSpeed, acceleration, forward cone
Movement - slide/crouch  entry speed, boost + cooldown, friction, exit speed, steering, crouch speed/height
Movement - mantle        min/max height, reach, duration
Camera / Look            sensitivity, pitch limits, field of view
View feel                viewEffectsStrength, FOV kicks, roll, bob, landing dip, speed lines, vignette, shake, flashReduction
Bomb - fuse              holdFuseDuration, warningDuration, caughtGraceDuration
Bomb - throw             throwSpeedMin/Max, throwChargeTime, throwUpAngle, bombGravityScale, run-speed inheritance
Bomb - aim assist        strength, cone, range, max correction/elevation, lead
Catch                    catchRadius, facing bonus, vertical scale, late grace, window, cooldown
Network                  catchLagCompensation
Bomb - feedback          beepIntervals, stageThresholds, beepVolume
Bomb - motion / VFX      tumble, hand sway, trail, fuse spark rates, flight puffs
Run                      resetDelay
```

Starting values are in spec §20. Do not scatter magic numbers across scripts.

**Player settings layer (M6.5).** Some tuning values are also player choices: `viewEffectsStrength`,
`flashReduction`, `beepVolume`, `mouseSensitivity`, `stickLookSpeed`, `fieldOfView`, plus invert Y, master volume and
display (window mode, resolution, vsync, frame cap). `Settings` (`Assets/Scripts/Core/Settings.cs`) holds them in a
`SettingsData` saved as JSON in `Application.persistentDataPath/settings.json`. Systems read them through
`Settings.ViewEffectsStrength(tuning)` and similar, never the tuning field directly. `BootstrapEntry` loads the file
once at the game's entry. Until then (tests, a gameplay scene played directly) and until the player saves anything,
`Settings.Current` is null and every accessor returns the `GameTuning` value, so the designer values are the
first-launch defaults and tests still drive the tuning. The shared asset is never written at runtime. The settings
screen (§6.2) edits `Settings.Editable(tuning)` and commits with `Settings.Save(data, tuning)`, which clamps, applies
(`AudioListener.volume`, vsync/frame cap, and resolution/window mode outside the Editor), writes the file and raises
`Settings.Changed`. `Settings.Preview(data, tuning)` does the same without writing the file, so a settings screen can
apply every slider step live and `Save` once; display changes are only re-applied when they differ from the screen.

**Audio volumes.** `Assets/Audio/HotPatataMixer.mixer` has `Master > SFX` (music and UI groups come with #47/#48),
with `MasterVolume` and `SfxVolume` exposed. The master slider (`masterVolume`) is `AudioListener.volume`, so it
reaches every sound, routed or not. Each group has its own setting (`sfxVolume` now), applied as decibels by
`AudioVolumes` (on the persistent `NetworkManager` prefab, from `Start` and on `Settings.Changed`). Every gameplay
sound plays through the SFX group: the bomb's `AudioSource` (`BombAudio`) and the tube/cannon tone
(`TransitPresentation.output`, set by `BombObstacleKitBuilder`). A new sound source must be routed to a group too.

**Key rebinding (#18).** `InputRebinding` lists the bindings a player may change per device (every button, plus the
WASD parts; sticks and mouse delta stay as authored). It runs the interactive rebind (`PerformInteractiveRebinding`,
limited to the device, Esc / Start cancels), reports conflicts on the same control, and resets one binding or all.
The overrides are saved as `SettingsData.bindingOverrides` (`SaveBindingOverridesAsJson`). Each `PlayerInputReader`
applies them to its own copy of HotPatataControls on creation and again on `Settings.Changed`, so a rebind works
immediately. A screen edits `InputRebinding.CreateEditableCopy(asset)` and commits with `InputRebinding.Commit`.

### 6.2 UI: screens and HUD (#14)

**Decision (owner):** UI Toolkit for menus, screens, the HUD and world-space UI (#79: the in-world menu's boards are
UI Toolkit world-space panels, so they keep the UXML convention and the theme); uGUI is not used. IMGUI (`OnGUI`)
stays for dev tools only; the menu and the lobby (#15, in the world since #79) and the results (#23) moved onto this
base, the HUD (#24) follows.

- **Panel and theme.** One `PanelSettings`, `Assets/UI/Resources/HotPatataPanel.asset` (scale with screen size from
  1920x1080, match 0.5, sort order 100), with the theme `Assets/UI/Styles/HotPatataTheme.tss` = Unity's runtime theme +
  `HotPatata.uss`.
  - **Look ("Sunny toy box", owner-approved mock-up).** Cream rounded panels (`hp-panel`), chunky buttons with a
    darker bottom lip (`hp-button`, variants `--secondary` sky, `--neutral` plum, `--danger` red, sizes `--big` /
    `--small`), outlined Lilita One titles (`hp-title`, the tilted `hp-logo`, the results `hp-banner`), white cards
    (`hp-card`, `hp-row`, `hp-slot`, `hp-stat`), a sun-yellow game-code ticket (`hp-ticket`), notes (`hp-note`).
    The colours are tokens on `:root` (`--hp-cream`, `--hp-ink`, `--hp-orange`, `--hp-sky`, `--hp-plum`,
    `--hp-red`, `--hp-sun`).
  - **Focus** (keyboard, gamepad) fills a button with sun yellow and grows it; a focused row or field takes a gold
    edge on a sun tint. The default theme's focus rules are overridden with `:focus:enabled`.
  - **Fonts** (`Assets/UI/Fonts`, SIL OFL): Lilita One for titles and buttons, Nunito (Regular / Bold / Black, static
    instances of the variable font) for text, as dynamic SDF font assets pre-filled with Latin-1, with a fallback
    for symbols generated from bundled Liberation Sans. Font assets and their material/atlas subassets must be
    persistent and buildable; never serialize Unity's internal DynamicOS font fallback (it carries `DontSave`).
  - **Icons** (`Assets/UI/Icons`): white SVGs (Unity's built-in vector import), tinted in USS (`hp-icon--*`), plus the
    four slot shapes and the potato.
  - **Slot chips** (`UIParts.Chip`): a disc in the slot colour holding the slot's shape, the shape in white or navy
    by the colour's luminance (spec §19: never colour alone; slot 4 is white).
  - **Spacing.** USS has no `gap` (and no `:first-child`): stacked children are spaced by a bottom margin scoped to
    their container, rows of children by a right margin cancelled on the row.
- **Entrance motion.** When a screen is pushed, or shows again under a closing one, its root gets `hp-screen--enter`
  for one tick (`UIScreen.PlayEntrance`): the panel pops in (opacity, scale, translate, `ease-out-back`) and the
  navigable elements cascade in (`hp-stagger-N`, removed after 0.7 s so hover and focus stay instant). Short, no flash,
  no shake; it never moves the focus.
- **`ScreenStack`** (`Assets/Scripts/UI`): the screen router, created on first use (`ScreenStack.Get()`) as a
  persistent `UIRoot` object, so it works from Bootstrap and in a gameplay scene played directly. `Push` /
  `Pop` / `Clear` / `Back`. Only the top screen shows. Focus goes to the screen's first element (or the one with
  `hp-first-focus`) and returns to where it was when the screen above closes. Back is the UI Cancel action (Esc,
  gamepad B) and goes to the top screen's `OnBack` (a root menu sets `CanGoBack` to false).
- **`UIScreen`**: one screen, its layout a UXML file in `Assets/UI/Screens` (`FromTemplate(Templates.x)`, then
  `Require<T>(name)`); `ReleasesCursor` (menus: yes), `CanGoBack`, `BlocksGameplay` (menus: yes), `OnShow` / `OnHide` /
  `OnBack`, `FirstFocus`, `Navigable` (the elements up / down walk, in tree order).
- **UXML convention (#76).** Every screen's layout can be opened and edited in UI Builder; the code never builds it.
  - **Files.** One `.uxml` per screen in `Assets/UI/Screens` (`Pause`, `Confirm`, `Settings`, `Results`,
    `UISample`) and per menu board (`StationTitle`, `StationPlay`, `StationLevel`, `StationLobby`, `Nameplate`). Buttons hold an icon (`hp-icon`) and a label (`hp-button__label`) as children. It is styled only through the shared `hp-*` classes, with no inline styles, no
    per-screen colours and no `<Style>` tags (the theme comes from the PanelSettings).
  - **Loading.** `UIScreenCatalog` (`Assets/UI/Resources/HotPatataScreens.asset`, next to the PanelSettings) holds
    one `VisualTreeAsset` per screen. `ScreenStack.Catalog` loads it, and a screen reads its own layout through
    `Templates`. A new screen adds a field there and assigns its UXML. There is no path lookup.
  - **Naming.** Every element the code needs has a kebab-case `name` (`host-online`, `code-field`, `player-list`).
    `Build()` finds each one once with `Require<T>(name)` and then wires behaviour.
    - A renamed or deleted element throws at once, naming the screen, the file and the element (`[UI]
      PlayStation (StationPlay.uxml): no Button named "host-online"`), and the stack is left as it was.
    - AGENTS' "No Find-by-name lookups" rule is about scene GameObjects. Querying a screen's own UXML elements by
      name is the normal UI Toolkit way and is what this convention requires.
  - **UXML / code split.** Layout and static text go in UXML. Code owns:
    - behaviour: the clicks, the host-only controls shown or hidden, `hp-first-focus`;
    - dynamic content: the lobby list, the code, the error text, values, ranges and choices, and texts that depend
      on state (online or offline pause, the question asked);
    - which elements are `Navigable`.

    The up / down walk follows the elements' tree order, so moving an element in UI Builder also moves it in the
    gamepad order without touching C#.
  - **Reusable pieces.** The setting rows are `[UxmlElement]` custom controls (`<hp:SliderRow name="field-of-view"
    label="Field of view"/>` with `xmlns:hp="HotPatata"`). They appear in the UI Builder Library under Project >
    HotPatata. The layout gives the label; the code gives the range, step, format and value (`SliderRow.Setup`), the
    choices (`ChoiceRow.SetOptions`) or the state (`ToggleRow.SetValueWithoutNotify`). The panel and title are plain
    classed elements (`hp-panel`, `hp-title`). A future multi-element piece can be a `<ui:Template>` /
    `<ui:Instance>`.
  - **UI Builder setup.** In the Viewport, set the canvas theme to `HotPatataTheme` (Assets/UI/Styles) and the canvas
    size to 1920x1080, the PanelSettings reference resolution. What you see then matches the game. Both are
    per-user Builder settings and are not saved in the UXML.
- **Navigation.** UI Toolkit reads keyboard and gamepad through an `EventSystem` with the Input System
  `InputSystemUIInputModule` and its default UI actions (arrows / WASD / stick / d-pad, Enter / A, Esc / B). The stack
  creates one under `UIRoot` if the scene has none. The mouse works directly.
- **Cursor.** `CursorPolicy` is the only code that sets `Cursor.lockState`: gameplay asks for a lock
  (`SetGameplayLock`: the local rig, `NetworkPlayer`, leaving a game), a screen on top that releases the cursor wins
  over it, and closing the screen gives the lock back. Esc belongs to the pause menu, not to the cursor.
- **Dev check.** F6 or gamepad Select opens `UISampleScreen` (dev builds only) in any scene.

**Screens built on it (#15, #16, #17, #23).**

- **Main menu and lobby: the in-world menu** (`MenuView`, #15, #79). `NetworkBootstrap` is logic only (the session
  flow, the command line, no drawing); it exposes its phase (`Menu`, `Working`, `Lobby`, `InGame`), status, last
  message, selected level and spawn point, and the lobby list. The menu is **part of the island**: each step is a
  **station**, a board (a world-space `UIDocument`) with a Cinemachine camera spot, and choosing an option flies the
  camera to the next station. `MenuView`, which `NetworkBootstrap` adds outside batch mode (headless bots run without
  UI), builds every station's board and shows the station `MenuFlow` points at:

  | Station | Board (`StationScreens.cs`, UXML) | What it holds |
  |---|---|---|
  | Title | `TitleStation` (`StationTitle`), the root: Back does nothing | the HOT PATATA logo over the island (the potato show below it) and one prompt: PLAY |
  | Play | `PlayStation` (`StationPlay`) | your name, Host Online (to Level), the game code (`CodeDials`) + Join, Play Local (to Level), Settings (the settings screen over the scene), the LAN / testing toggle (address, Join IP, Host on LAN to Level), the last error, the working line ("Joining ...") |
  | Level | `LevelStation` (`StationLevel`) | level and spawn point, then the button for why you came: Host Online, Host on LAN, Play Local, or Done for the host from the lobby; Back; the working line ("Creating your game...") |
  | Lobby | `LobbyStation` (`StationLobby`) | the game code ticket (Copy code), a card per player (slot chip, name, HOST and YOU badges) and per free seat, the host's level + Change level (to Level) + Start, else "Waiting for the host to start...", Leave; Back asks before leaving |

  - **`MenuFlow`** (plain state, `Assets/Scripts/Menu`): the current station and why Level was opened (`HostOnline`,
    `HostLan`, `Local`, `LobbyEdit`). Board choices (`Choose`) and Back move it; the session flow wins (`Sync`): an open
    lobby shows the Lobby (or keeps the host on its level choice), and back in the Menu phase after a session (a
    failed host or join, leaving the lobby, a game that ended) it shows Play with the error, never Title. Title only
    shows on first launch. Working and InGame keep the station, so the board that started the work shows it.
  - **`MenuStation`**: the station's id, camera spot and board. Only the shown station's board takes input: an idle
    board fades its panel out (`hp-station--idle`; the Title logo stays as scenery), is disabled so focus never
    wanders onto it, and leaves the pointer's raycast layer (`UI` to Ignore Raycast) so it never catches a click meant
    for another. Back (Esc / B) goes to the shown board's screen; focus goes to its first element when the camera
    arrives, and back where it was when a screen of the stack (Settings, a dialog) closes over it.
  - **Station boards** are `UIScreen`s built into the station's document instead of the stack (`StationScreen`),
    with the same conventions (catalog layout, `Require<T>`, `Navigable`, entrance motion). Their panel is
    `Assets/UI/Resources/HotPatataWorldPanel.asset` (world space, the same theme, 200 px per metre); one
    `PanelInputConfiguration` in the backdrop lets the main camera raycast them for the mouse, and keyboard and
    gamepad come through the same `EventSystem`. Focus reads from the camera's distance (a navy edge and a bigger
    grow, `.hp-board`).
  - **`CodeDials`** (`[UxmlElement]`): the game code as six dials, so a gamepad can enter it. One focusable row in the
    walk; Enter / A edits, up / down turn the current dial through A-Z then 0-9, left / right pick the dial, Enter /
    A or Esc / B stop (Esc then does not leave the station). The keyboard types straight in (Backspace clears back,
    Ctrl+V pastes a code), the mouse clicks a dial or its arrows. The name stays a text field (it has a default).
  - **Travel** (`MenuCameraRig` on the menu camera with a `CinemachineBrain`, the one component that decides where
    the camera looks): the target station's spot gets the priority and the brain blends there, `EaseInOut` over
    `GameTuning.menuTravelSeconds` on unscaled time, or a **cut** when the camera effects are 0
    (`Settings.ViewEffectsStrength`, spec §19): no swoop, and nothing flashes. Each station also has a framing volume
    (`MenuStation.Focus`, a light vignette, `Assets/Settings/Look/MenuFocus_<Station>.asset`) whose weight is the
    camera-effects strength while the station shows (0 = none). No depth of field: the world-space boards write no
    depth, so it would blur them as if they were sky. The Title spot keeps the slow drift
    (`MenuCameraDrift`); the other spots are steady so the boards read.
  - **Lobby stage** (`MenuLobbyStage`): while the lobby is open the potato show pauses (`MenuHotPotato.SetPaused`) and
    the show's player `i` (slot `i`'s character) hops to stage spot `i` under the Lobby board for each joined player
    (`GameTuning.menuStepSeconds`), waves, and shows a nameplate over its head (`Nameplate.uxml`: the slot chip and the
    name; spec §19, never colour alone). Free seats stay in the show; leaving the lobby sends everyone back and the
    show resumes.

  The boards re-read the bootstrap's state every 200 ms (messages, the lobby list) and call its public methods
  (`HostOnlineAsync`, `JoinCodeAsync`, `PlayLocal`, `StartHostDirect`, `StartClientDirect`, `StartLevel`,
  `LeaveAsync`). Text fields keep the keyboard's arrows and WASD while you type; a gamepad still moves between rows.
  The pause menu, the in-game settings and the results stay screen-space screens on the stack (they are over
  gameplay).

- **Pause menu** (`PauseScreen`, `PauseMenu`, `Assets/Scripts/UI/PauseMenu.cs`): Resume, Settings, Leave to menu
  (behind a `ConfirmScreen` whose safe answer, Cancel, has the focus).
  - Opened and closed by the **Pause** action of HotPatataControls (Esc, gamepad Start; rebindable like every button,
    #18), read by `PlayerPause` on the local player; Back (Esc / B) closes it too. `PauseMenu.Toggle` ignores a frame
    on which a screen already opened or closed (`ScreenStack.LastChangeFrame`): Esc is both the UI's Back and the
    gameplay Pause, so it never closes and reopens the menu at once. With another screen on top (settings, a
    question), Pause leaves it to that screen's Back.
  - **Offline the game freezes** (`Time.timeScale` 0 while a `PauseScreen` is anywhere in the stack, settings
    included). **Online nothing pauses**, for anyone: a carrier who opens the menu keeps the bomb while its fuse
    burns, as the rules say, and the menu says so.
  - While a menu is on top (`UIScreen.BlocksGameplay`, true for every menu) `PlayerInputReader.Blocked` makes every
    gameplay value read neutral (no move, no look, presses dropped, scripted input included), and `PlayerThrower`
    cancels a charge, so closing a menu never throws by itself. Only Pause still reads, so Start closes the menu.
  - Leave goes through `NetworkBootstrap.LeaveAsync` (in session mode `SessionService.LeaveAsync`, never
    `NetworkManager.Shutdown`); a course played directly in the Editor loads the entry scene (build index 0). The
    IMGUI in-game Leave button is gone; F10 still leaves, as a dev shortcut.
- **Settings screen** (`SettingsScreen`, #17), reachable from the Play station (over the scene) and the pause menu. Sections and rows:

  | Section | Rows (stored in `SettingsData`) |
  |---|---|
  | Accessibility | camera effects (`viewEffectsStrength`), flash reduction (`flashReduction`), fuse warning volume (`beepVolume`) |
  | Look | mouse sensitivity, stick sensitivity, invert Y, field of view |
  | Audio | master volume (`AudioListener.volume`), effects volume (the mixer's SFX group, `AudioVolumes`) |
  | Display | window mode (fullscreen, borderless, windowed), resolution, vertical sync, frame cap (30-240 or none, with vsync off) |

  It edits `Settings.Editable(tuning)`: every change applies at once (`Settings.Preview`) and the file is written once
  when the screen closes (`Settings.Save`, in `OnHide`). **Reset to defaults** goes back to the `GameTuning` values
  (keeping the key bindings). The `GameTuning` asset is only read. Window mode and resolution apply in a build only
  (the Game view owns them in the Editor; the screen says so). Key bindings get their own page with #18.
- **Results screen** (`ResultsScreen`, `Results.uxml`, #23): COURSE COMPLETE!, the team (slot chips and names), the
  run time (`m:ss.s`) and the explosions / resets, from the replicated run state so every player sees it. The host
  (or an offline player) gets **Rematch** (`RunManager.Restart`; R still works) and Leave; the others see "Waiting
  for the host to restart..." and can leave (behind a `ConfirmScreen`). Back does nothing. `RunResultsUI`, placed in
  each course scene, pushes it while the run is `Completed` and pops it when the run starts again. The old IMGUI
  screen is gone. While any menu is on top, the IMGUI `AimReticle` draws nothing.
- **Menu backdrop** (`Assets/Prefabs/Menu/MenuBackdrop.prefab`, one instance in `Bootstrap`). A small KayKit island,
  home of the in-world menu (above), where the four slots' characters pass the live potato in arcs, hand to hand
  (`MenuHotPotato`). The holder faces the next catcher, a catch squashes the catcher and bystanders cheer, wave or hop.
  The wick sparks faster (`GameTuning.fuseSparkRates`) until the fuse runs out in the game's own explosion
  (`ExplosionFx`, its flash always reduced at least 60 %, more if the player asked through `flashReduction`); the
  holder flinches, the others cheer, and a new potato pops in. Flags sway, stars and collectables spin
  (`MenuFloat`), small platforms bob, clouds drift (`MenuCloudDrift`), sparkles float, and on the Title station the
  camera drifts slowly (`MenuCameraDrift` on the Title spot, scaled by `viewEffectsStrength`).
  - **Visual only.** The show's players are copies of each slot's character prefab (`PlayerCharacter`, driven by
    `MenuCharacter.controller`, its states named after the Mixamo clips; the potato leaves the `HandSocket` at the
    moment `PlayerCharacterBuilder` measured on the Throw clip), the potato a copy of the bomb's `Visual` and
    `FlightFx`. None of
    the gameplay components (`Player`, `BombController`, networking) and no colliders (the boards' generated panel
    colliders aside). It runs on unscaled time (the menu is offline and local, so no `SectionClock`) and turns itself
    off in batch mode.
  - **Generated** by `MenuBackdropBuilder` (menu **HotPatata/Menu/Build Menu Backdrop**, also run by **Rebuild All
    Courses**): it writes the prefab (with the stations: boards, camera spots, the lobby stage and the board input),
    the world panel asset and the animator controller, places the instance, gives the camera its brain and rig and
    sets the courses' sky, ambient light and fog. Station layout (board position and size, camera spot) is a table in
    the builder; keep decoration out of the boards' way. Never edit it by hand.
- **Setting rows** (`SettingRows.cs`, UXML custom controls): `SliderRow`, `ToggleRow`, `ChoiceRow` are one focusable
  line each (`hp-row`) with a label; the inner field only takes the mouse. Up / down walk the rows
  (`UIScreen.Navigable`: the layout's order, skipping disabled or hidden rows, so the d-pad never gets lost), left /
  right change the focused row, Enter / A toggles or steps forward. A focused row scrolls into view. The rows' labels
  and order live in `Settings.uxml`.

---

## 7. Player subsystem

### 7.1 `PlayerMotor`

Responsibilities:

- movement input;
- grounded state;
- acceleration/braking;
- jump;
- coyote time;
- jump buffer;
- air control.

Explicit state (`MoveState`): `Ground`, `Air`, `Slide`, `Mantle`, plus a `Crouched` posture flag and `IsSprinting`.

- **Ground**: the horizontal velocity *carves*. Its direction rotates toward the stick at a speed-dependent turn rate (`groundTurnRate` → `sprintTurnRate`), and its magnitude settles toward the target speed (run / sprint / crouch). Reversals sharper than `reverseAngle` brake through zero. Overspeed bleeds off at `overspeedDeceleration`.
- **Air**: same steering at `airTurnRate`, momentum kept, `airDrag` without input, `airOverspeedDrag` above sprint speed (no bunny-hop).
- **Slide**: starts on a crouch press (or a landing with crouch held) at `slideMinEntrySpeed`+. It has a cooldown-limited boost capped at sprint speed + boost, friction, slope gravity along the ground normal (downward ray), and light steering. It ends on release or below `slideExitSpeed`.
- **Mantle**: while airborne with forward input, a forward ray finds a ledge face, a downward ray finds its flat top (not Hazard) 0.1–1.4 m above the feet, and two capsule checks confirm room. A scripted rise-then-over via `CharacterController.Move` follows, and the forward speed is kept.
- **Posture**: the capsule height changes at the feet pivot. Standing up waits for headroom (`CheckCapsule`). `HeightScale` (smoothed) lowers the eye pivot (`PlayerLook`) and the catch sphere (`PlayerCatchVolume`).
- **Replication**: `PackedState` (state + crouch + sprint, one byte) is owner-written on `NetworkPlayer`. Remote copies apply it to their capsule, eye and catch height, so the host's catch sweeps and `FlightHistory` use the sliding player's real catch centre. `PlayerAnimator` uses it for the remote sprint blend (Speed 2 = run clip at 1.35×) and the procedural slide / crouch-walk pose.
- `PlayerMotor.MotionFraction` (run = 1, sprint = 2, slides above) is the single speed scale for presentation.

Should **not** know bomb game rules.

### 7.2 `PlayerLook` / camera controller

Responsibilities:

- own aim yaw/pitch (yaw turns the whole body, pitch turns the eye pivot);
- pitch constraints;
- first-person camera placed at the eye pivot (`CameraTarget`), no orbit and no camera collision;
- keep the hand and throw anchors on the eye pivot so the held bomb stays in view.

Network replication of camera is unnecessary.

`PlayerLook` owns `Yaw`; the body always faces it and pitch rotates only the eye pivot. `HandAnchor` and `ThrowOrigin`
are children of that pivot. The followed player's own renderers are `ShadowsOnly` (`PlayerPresentation.SetLocalView`);
the held bomb is never hidden.

### 7.3 `PlayerThrower`

Responsibilities:

- determine whether local player may throw;
- track the throw charge (hold to charge, release to throw; expose `Charging` / `Charge01` for the UI);
- aim/release direction (the rendered view) and launch speed from the charge (`throwSpeedMin` to `throwSpeedMax`),
  plus a share of the thrower's forward run speed;
- the release-time aim assist (`AimAssist.TryAssist`, pure, direction only, capped; PROJECT_SPEC §8.3);
- launch on the release frame (the animation is a follow-through, never a delay);
- send throw request to authoritative bomb system;
- expose release anchor.

Should not directly mutate authoritative bomb ownership on clients.

Throw feel (spec §8.2–9.2): the bomb flies a plain ballistic arc and nothing steers it after release (soft homing and
the magnet were removed on purpose). `AimAssist.TryAssist` turns the launch direction a few degrees toward a receiver in
a small cone at the throw's **own** speed; a throw too weak to reach only gets a heading correction and falls short (the
assist never creates range). Throws inherit run speed only up to `throwInheritMaxSpeed`. A remote client draws its own
throw immediately (`NetworkBomb.PredictLocalThrow`, drawing only).

### 7.4 `PlayerCatchVolume`

Responsibilities:

- describe catchable region;
- expose receiver eligibility;
- provide hand/catch anchor.

The authoritative catch resolver uses this information.

### 7.4b `PlayerCatcher`

Catching is a timed action (PROJECT_SPEC §9). Responsibilities:

- read the catch input and open a short catch window (`catchWindowDuration`);
- enforce the cooldown after a window closes (`catchCooldown`);
- refuse to open a window while the player holds the bomb or control is locked;
- expose `WindowOpen`, `OnCooldown` and remaining fractions for the resolver and the UI.

It never touches the bomb; the resolver only reads `WindowOpen`. On a networked build the window is a
request to the host, which checks it when resolving the catch.

### 7.5 `PlayerPresentation`

Responsibilities:

- name/color;
- carrier indicator;
- catch-ready indicator;
- local animation hooks.

No authoritative game rules.

### 7.6 First-person feel (`PlayerViewFeel`, `SpeedEffects`)

`PlayerViewFeel` runs for the local, camera-followed player only and computes:

- a FOV curve over `MotionFraction` (`fovKickAtSpeed` run → `fovKickAtSprint` → `fovKickMax` in slides);
- roll: strafe lean plus the slide roll (`slideRollDegrees`);
- a foot-plant bob (a dip on each footfall) and the landing dip;
- throw/catch punches, a slide rumble, and the explosion shake (`explosionShake`, by distance).

`PlayerLook` applies roll/kick/bob to the eye pivot (so the held bomb moves with the view) and `FirstPersonCamera`
applies the FOV. Above run speed, `SpeedEffects` (on the camera) drives the anime speed lines (a Full Screen Pass
feature `SpeedLines` on `PC_Renderer` reading the global `_HotPatataSpeedLines`) and emits wind streaks from
`VFX_WindStreaks`; it adds a vignette only while sliding. Read `Volume.sharedProfile`, never `.profile` (it clones).

Removed on purpose, do not add back: movement sounds (footsteps, landings, wind, slide scrape), chromatic aberration,
lens distortion. Every effect scales with `GameTuning.viewEffectsStrength` and `flashReduction` dims every flash
(spec §19).

---

## 8. Bomb subsystem

### 8.1 `BombController`

Owns the bomb state machine.

Suggested state enum:

```csharp
Held
Thrown
CaughtGrace
Exploding
Resetting
InTransit   // #68: carried by a tube or cannon, Thrown -> InTransit -> Thrown (PROJECT_SPEC §5)
```

It also applies the fuse zones (§10.6): while the bomb is Held it ticks the fuse at `FuseZone.RateFor(carrier)` and
explodes it (`BombFailReason.ForbiddenZone`) when the carrier stands in a forbidden zone, also during `CaughtGrace`.

Responsibilities:

- current state;
- current carrier;
- state transitions;
- attach/detach;
- release;
- normalized reset;
- interaction gates.

### 8.2 `BombFuse`

Responsibilities:

- authoritative fuse;
- fuse reset on valid catch;
- warning threshold;
- expiry event;
- normalized state on checkpoint reset.

Presentation may read replicated normalized fuse data or warning stage.

### 8.3 `BombPhysics`

Responsibilities:

- Rigidbody configuration;
- thrown movement: plain ballistic flight under the bomb's own gravity, nothing steers it after release
  (the arc maths is shared in `ThrowBallistics`);
- environment collision reporting;
- transition between attached and thrown physics modes.

While held:

- Rigidbody should not produce accidental lethal collisions.

While thrown:

- collision is gameplay-critical.

### 8.4 `CatchResolver`

Responsibilities:

- decide whether incoming bomb contact is a valid catch (thrower excluded, receiver's catch window open);
- own the catch geometry: a swept test of the bomb's path between physics steps against each receiver's reach
  (`ReachFor`: `catchRadius` plus a facing bonus), and a short grace for a slightly late press;
- reject ineligible receiver;
- resolve simultaneous/near-simultaneous catch claims;
- change authoritative carrier exactly once;
- enter catch grace state.

Do not duplicate catch decisions inside player scripts.

`BombController.IntendedReceiver` (replicated) only drives the "CATCH!" marker. `PlayerCatcher.Hint` explains failed
catches ("Too late by N ms" / "Too early"). Online, remote receivers are covered by lag compensation (§13.2).

### 8.5 `BombAudio`

Responsibilities:

- beep cadence;
- warning sounds;
- throw sound;
- catch sound;
- explosion sound.

Should consume bomb state; should not control bomb state. Its `AudioSource` outputs to the mixer's SFX group (§6.1).

### 8.6 `BombPresentation`

Responsibilities:

- emissive pulse;
- warning intensity;
- holder visual;
- trail;
- explosion VFX hooks.

As built, the bomb is a potato (`Assets/Art/Models/Bomb/potato.glb`, glTFast; UVs flipped in the copy
`Potato_UVfixed.asset`; toon material `Bomb_Potato`). The model is the `Model` child of `Bomb/Visual`.
`BombPresentation` drives its `_EmissionColor` pulse (scaled by `emissionScale` so it reads through bloom), a scale pop,
and cosmetic motion (random-axis tumble in flight scaled by throw speed; sway and a small jolt in the hand:
`tumble*`/`handSway*` in `GameTuning`). Rotation is applied to `Visual` only, never the Rigidbody. To swap the model,
replace the mesh/material on `Model` and keep `BombPresentation.bodyRenderer` pointing at its renderer.

VFX, all presentation-only and driven by state events (so they also play on remote mirrors):

- a `Wick` with `FuseSparks` under `Visual`, rate per fuse stage from `fuseSparkRates`;
- `FlightFx` on the bomb root: a `TrailRenderer` (`trailTime`/`trailWidth`, width by throw speed, warmer at urgent
  stages) and `FlightPuffs`, active only while Thrown;
- a pooled `ExplosionFx` (`VFX_Explosion`: toon fireball puffs, potato debris, sparks, shock ring, flash sphere and
  light, a billboarded "BOOM!").

`BombAudio` plays beeps (cadence from `beepIntervals`; "beep" is a gameplay term), catch, throw and explosion, using
clips from `Assets/Audio/SFX` when assigned and `ProceduralSfx` otherwise.

---

## 9. Run / checkpoint subsystem

### 9.1 `RunManager`

Authoritative responsibilities:

- current run state;
- current checkpoint;
- section failure;
- reset sequence;
- completion;
- run timer if used;
- explosion count if displayed.

Suggested run states:

```text
Initializing
WaitingForPlayers
Playing
Failing
Resetting
Completed
```

### 9.2 `Checkpoint`

Data:

- checkpoint ID;
- required trigger;
- spawn slots;
- bomb spawn/holder policy;
- optional section tuning override.

Behavior:

- report player presence;
- activate only when all required players satisfy condition.

### 9.3 `SectionReset`

May be a service owned by `RunManager`.

Reset order recommendation:

1. lock interactions;
2. play short explosion/failure feedback;
3. clear player movement;
4. teleport players;
5. reset gameplay obstacles;
6. reset bomb;
7. clear stale velocities;
8. resume section.

Target total interruption: ~1 second.

### 9.4 `FinishZone`

Authoritative completion condition:

- all required players have entered / satisfied finish state.

---

## 10. Obstacle architecture

Obstacle prefabs should share simple conventions, not a giant inheritance hierarchy.

### 10.1 Standard prefab structure

Prefer:

```text
PrefabName
├── Visual
├── Collision
├── Logic      (only if useful)
└── Gizmos / Waypoints / Anchors
```

`Visual` holds no mesh of its own: a `KitSkin` draws its box in KayKit pieces (§25.1). Builders and resize helpers size
`Visual` and `Collision` exactly as before.

### 10.2 Reusable MVP prefabs

Create these as needed:

```text
Platforms/
- Platform_Basic
- Platform_Narrow
- Platform_Moving
- Platform_Falling
- Platform_Elevator   (vertical MovingPlatform, Dwell)
- Platform_Conveyor   (Conveyor: carries riders, scrolling stripes)

Obstacles/
- Obstacle_RotatingBar
- Obstacle_Piston     (MovingPlatform, Dwell: floor tiles, gates; the MovingBlocker)
- Obstacle_Crusher    (vertical Dwell slab, lethal underside)
- Obstacle_Sweeper    (knee-high lethal RotatingObstacle bar)
- Obstacle_Windmill   (lethal blades turning in a wall's plane)
- Obstacle_Hoop       (spinning ring: a moving pass window)
- Obstacle_Tube       (#68: up to three mouth/exit/pad routes)
- Obstacle_Cannon     (#68: basket + barrel, one long exit arc)
- Actuator_Door       (#68: signal-driven portcullis, lethal lower edge while closing)

Gameplay/
- PlayerSpawn
- BombSpawn
- Checkpoint
- KillZone
- FinishZone
- LaunchPad            (prefab lives in Obstacles/)
- Zone_Forbidden / Zone_Hot / Zone_Cold, LaserCurtain   (#68 fuse zones)
- BombGate_Ring, BombGate_Arch, PressurePlate           (#68 signal sources)

Platforms/ (#68): Actuator_Bridge, Actuator_Lift (signal-driven)
```

The #68 kit is generated by `BombObstacleKitBuilder` (menu **HotPatata/Course/Build Bomb Obstacle Prefabs**); its
`Resize*`/`Configure*` helpers size placed instances with property overrides only.

### 10.3 `MovingPlatform`

Expose:

- waypoint A;
- waypoint B;
- speed;
- movement mode (`PingPong` constant speed, or `Dwell`: wait at each end for `dwellFraction` of the leg, then ease across);
- start phase.

Must be deterministic enough for networked play: the position is `MovingPlatform.Evaluate(SectionClock.Now, ...)`,
a pure function. Anything that carries riders implements `IPlatformCarrier` (`FrameDelta`), which `PlayerMotor`
adds to its move while grounded on it (moving platforms, elevators, pistons, `Conveyor` belts).
Lethal obstacles (sweeper, windmill, crusher) are on the `Hazard` layer, striped, with a child `KillZone` trigger.
A crusher never closes below 1.45 m, so crouching or sliding under it is safe. `MovingPlatform.Evaluate` is pure and
also has a `Dwell` mode (wait at the ends, then ease), used by elevators and pistons.

Stateful objects (`FallingPlatform`) implement `IResettable`, which `RunManager` calls on reset. Zones (`Checkpoint`,
`FinishZone`, the falling-platform trigger) use the stateless `PlayerZone.Collect` overlap query, never Enter/Exit
bookkeeping. `KillZone` fails the section for a player and explodes a bomb. `LaunchPad` launches only the machine that
owns the player.

### 10.4 `RotatingObstacle`

Expose:

- axis;
- speed;
- phase.

Gameplay-critical transform timing should be consistent for all clients.

### 10.5 `FallingPlatform`

Expose:

- trigger condition;
- warning delay;
- fall delay;
- reset behavior.

Must support checkpoint reset.

### 10.6 Zones, signals and transit (#68)

Composition, not inheritance: a `Zone` (a trigger `BoxCollider` on the `Trigger` layer, self-registering) plus one
or more effect components on the same object.

- **Players in a zone:** `Zone.CollectPlayers` → `PlayerZone.Collect` (box zones are tested as oriented boxes).
- **The flying bomb in a zone:** `BombZoneSweep` (on the Bomb, host only) sweeps, every physics step, the stretch the
  Thrown bomb is about to fly (cut at the first Environment/Hazard hit) against `Zone.All` and calls each touched
  zone's `IBombZoneEffect.OnBombPassed`, nearest first. Effects are idempotent (a deep zone is reported on several
  steps). Trigger events are never used for the bomb.
- **`FuseZone`** (carrier): forbidden ∞ / hot x2 / cold x0.5 (`GameTuning.hotZoneFuseRate`, `coldZoneFuseRate`); the
  most severe zone wins. `BombFuse.Rate` is set by `BombController`, mirrored by `NetworkBomb`, and shown by
  `BombPresentation` (sparks x rate, frost tint) and `BombAudio` (beep interval / rate).
- **`BombBarrier`** (flight): crossing is a lethal contact (`BombController.ReportZoneContact`, same late-catch hold
  as world contact). `LaserCurtain` = forbidden `FuseZone` + `BombBarrier` + striped posts, beams and signs.
- **Signals:** `ISignalSource` is a `BombGate` (flight effect; records the server time of the last pass, active for
  `holdSeconds`, 0 = latched until reset) or a `PressurePlate` (any player on it). A `SignalActuator` (door, bridge,
  lift; `IPlatformCarrier`, `IResettable`) has exactly one source. The host records only the moment the direction
  changes (time, progress, opening); `SignalActuator.Progress` derives the motion from the server clock.
  `NetworkBombGate` and `NetworkSignalActuator` replicate those few numbers. A door's `KillZone` edge is armed only
  while it closes. `SignalIndicator` lights gates, plates and arches (pulsing before a timed gate closes).
- **Checkpoint arch:** `Checkpoint.claimGate` (a latched `BombGate_Arch`) must have been passed this section.
- **Transit:** `TransitMouth` (flight effect, the only safe volume) → `BombTransit.Capture` →
  `BombController.EnterTransit`: the bomb is inert at the exit's hold point (inside the pipe, or in the cannon's
  basket) until `ReleaseAt`, then `LeaveTransit` throws it from the muzzle with `BombTransit.ExitVelocity`, the arc
  that reaches catch height above the exit's pad after `flightTime`. `LastThrower` is null, `BombThrown(null)` is
  raised. `NetworkBombTransit` replicates the active exit and release time for `TransitPresentation` (exit lamp ramp,
  rising tone). Resets and explosions clear a transit.

---

## 11. Prefab requirements

### Player prefab

Suggested structure:

```text
Player
├── Visual              (hidden from the local player's own camera)
├── CharacterBody / Motor
├── CatchVolume
├── NameplateAnchor
└── CameraTarget        (eye pivot: carries the camera and pitches with the look)
    ├── ThrowOrigin     (right / down / forward of the eyes)
    └── HandAnchor      (same place: the carrier sees the bomb here)
```

Required components depend on chosen motor/network implementation.

### Bomb prefab

Suggested structure:

```text
Bomb
├── Visual
├── Collider
├── Rigidbody
├── BombController
├── BombFuse
├── BombPhysics
├── BombAudio
└── BombPresentation
```

### Checkpoint prefab

```text
Checkpoint
├── Trigger
├── Spawn_01
├── Spawn_02
├── Spawn_03
├── Spawn_04
├── BombAnchor
└── Visual
```

---

## 12. Layers / collision policy

Define explicit layers early. As built, all six exist (`ProjectSettings/TagManager.asset`).

Suggested layers:

```text
Player
PlayerCatch
Bomb
Environment
Hazard
Trigger
```

Create and document a collision matrix.

Critical requirements:

- held bomb must not accidentally explode against its carrier;
- thrown bomb must detect valid catch volumes;
- thrown bomb must detect environment reliably;
- non-gameplay trigger volumes must not accidentally count as lethal world contact;
- player body collision must not generate duplicate catch paths.

Do not solve collision behavior using object-name checks.

---

## 13. Network representation

### `NetworkPlayer`

Replicate only what gameplay needs.

Potential synchronized data:

- player ID;
- display identity/color;
- movement state / transform according to chosen NGO model;
- optional catch-ready state;
- connected/required status.

### `NetworkBomb`

Synchronize:

- bomb state;
- current carrier/player ID;
- thrown transform/velocity as necessary;
- fuse state / normalized remaining time;
- explosion event/state.

Do not run separate authoritative bomb simulations on each client.

### `NetworkRunState`

Synchronize:

- checkpoint ID;
- run state;
- reset event;
- completion state;
- optional timer.

---

### 13.1 As implemented in Milestones 3 and 4

Online games use **Unity Multiplayer Services** (`SessionService`): the host creates a Relay-backed session
(`CreateSessionAsync(... .WithRelayNetwork())`, no port forwarding) and shares its 6-character code; others join with
`JoinSessionByCodeAsync`. The SDK starts and stops the Netcode host/client itself, so never call
`NetworkManager.Shutdown` while a session exists (use `SessionService.LeaveAsync`). The lobby is a UI state of the
Bootstrap scene (code, player list with host marker, Start for the host); Start makes the host load the level and
every client follows. Direct IP (port 7777) remains as a LAN / testing path. One persistent `NetworkManager` prefab (`NetworkManager` + `UnityTransport` + `NetworkBootstrap`)
is created by the `Bootstrap` scene; `NetworkBootstrap` offers Host / Join / Play Local and handles
disconnects. The host loads the chosen level (`PassSandbox` or `PrototypeCourse`) through NGO scene management; clients follow.

Authority split (the same gameplay classes run offline and online; `NetMode.IsAuthority` is true offline
and on the host):

| Concern | Who decides | How it reaches others |
|---|---|---|
| Player movement, aim | owning client (owner-authoritative `NetworkTransform`) | transform + replicated pitch |
| Throw | host | owner sends `RequestThrow(origin, velocity)`; host clamps speed, checks the release point, then `TryThrow` |
| Catch | host | owner sends `RequestCatch`; host opens that player's catch window; `CatchResolver` decides once |
| Bomb state, carrier, fuse, explosion | host only | `NetworkBomb` snapshot (one atomic `NetworkVariable`) + fuse fraction; clients mirror it via `BombController.ApplyMirror` and raise the same events |
| Bomb position | host physics | server-authoritative `NetworkTransform`; while held, each client attaches it to the carrier's hand locally |
| Run state, checkpoint, section clock | host | `NetworkRunState`; the section clock start is a server time, so platforms match everywhere |
| Lock / teleport on reset | host | `NetworkPlayer.locked` variable; `TeleportOwner` RPC (owners move themselves) |
| Player name | host sanitises (`PlayerNames.Sanitize`) | owner sends its menu name on spawn (`SubmitName`); `NetworkPlayer.displayName` variable, so late joiners get it too. In the lobby, before players spawn, the name travels as the session player property `name` |

Remote clients never simulate the bomb (kinematic, collider off) and never run zone / checkpoint / finish logic.
`PlayerSpawner` builds the offline two-player rig, or on the host spawns one player per connection into the
first free slot. Level objects are kept in sync by deriving their state from shared time: moving platforms and rotating bars from
`SectionClock`, falling platforms from one replicated trigger time (`NetworkFallingPlatform`). Moves that are
teleports (resets, respawns) are sent as teleports (`NetworkPlayer.SyncTeleport`), otherwise other machines
interpolate the player across the level and sweep them through triggers.

### 13.2 Catch lag compensation (M3.5)

Problem: the host measures the catch window from when the request ARRIVES, while the receiver presses when they SEE the
bomb (delayed by network + interpolation). Without compensation the timed catch broke between ~110 and ~240 ms RTT.

Fix, as built:

- A remote client that sees its (late-rendered) bomb reach its catch sphere while its own window is open sends
  `NetworkPlayer.ClaimCatch`.
- `CatchResolver.TryResolveCompensatedCatch` accepts it if the host's `FlightHistory` (bomb + every catch centre,
  recorded each FixedUpdate while Thrown, online only) had the bomb within `catchRadius + 0.4 m` of that player in the
  last `GameTuning.catchLagCompensation` seconds (0.35 s, also the abuse cap).
- A lethal contact right after the bomb passed a remote eligible receiver is held (`BombController.ExplosionPending`:
  still Thrown, frozen) for `min(cap, rtt + 0.15 s)` so the late catch can win.
- Host "Too late/early" hints for remote players are deferred by the same cap and dropped if the catch lands.
- Offline and for the host's own player nothing changes.

Measured with bots (numbers and method in [`TESTING.md`](TESTING.md)): ~230 ms RTT 20/20 (1 client) and 22/22 incl.
client→client; ~450 ms still 0/14, beyond the cap (raising `catchLagCompensation` toward 0.5 would cover it, at the
cost of longer freezes before explosions).

Movement posture is replicated too: the owner writes `PackedState` on `NetworkPlayer` (§7.1), so the host's catch
sweeps and `FlightHistory` use a crouching or sliding player's real catch centre.

## 14. Session flow

As built: Bootstrap menu (Host Online / Join with code / Play Local / Direct IP) -> Lobby (host presses Start) ->
level -> Leave returns to the menu, and a new game can be created again.

Expected high-level flow:

```text
Boot
  ↓
Initialize services
  ↓
Main Menu
  ├── Create Session
  └── Join Session Code
          ↓
        Lobby
          ↓
      Host Start
          ↓
 Load PassSandbox / Course
          ↓
    Spawn Players
          ↓
      Spawn Bomb
          ↓
        Play
   ┌──────┴──────┐
 Explode       Checkpoint
   ↓               ↓
 Reset           Continue
   └──────┬────────┘
          ↓
        Finish
          ↓
     Results / Replay
```

---

## 15. Local-first development rule

Implement in this order:

1. local movement;
2. local bomb hold;
3. local throw;
4. local catch;
5. local fuse;
6. local world-contact explosion;
7. local reset;
8. verify game feel;
9. network the proven behavior.

Do not begin by building the full online lobby around an unproven pass mechanic.

---

## 16. Unity Editor / coding-agent workflow

When the coding agent has Unity Editor / MCP access, it should use the editor to:

- create scenes;
- create GameObjects;
- create prefab assets;
- add components;
- assign serialized references;
- position greybox geometry;
- inspect hierarchy;
- inspect Console errors;
- enter Play Mode when supported;
- save scenes/prefabs.

### 16.1 Important rule

Prefer Editor/MCP object operations over manually editing serialized `.unity` or `.prefab` YAML.

Direct YAML editing is only a fallback when:

- editor tooling is unavailable;
- the change is understood and deterministic;
- the project is backed up/version-controlled.

### 16.2 Agent iteration loop

For each small feature:

```text
Read task
↓
Inspect current Unity project
↓
Implement smallest coherent change
↓
Let Unity compile
↓
Inspect Console
↓
Fix compile/runtime setup errors
↓
Run minimal Play Mode validation
↓
Save scene/prefab
↓
Mark acceptance criteria
```

The agent must not continue stacking features on top of compilation failures.

---

## 17. Suggested assembly standards for generated prefabs

Every generated gameplay prefab should:

- use a clear root name;
- avoid unnecessary nested objects;
- expose meaningful parameters via serialized fields;
- have required component references assigned;
- be reusable in more than one scene when appropriate;
- support reset if stateful;
- avoid Find-by-name runtime dependencies;
- avoid hidden scene-only dependencies;
- include gizmos where waypoints/ranges are otherwise hard to understand.

---

## 18. Testing strategy

The suites, how to run them, bot sessions, latency simulation and CI are documented in [`TESTING.md`](TESTING.md).

### 18.1 Edit Mode

Pure logic: tuning/layer configuration, fuse maths, aim assist and arc maths, `MovingPlatform.Evaluate`,
`FlightHistory`, session-code cleanup.

### 18.2 Play Mode

Real components in `PassSandbox` / `PrototypeCourse`, driven through `ScriptedInput`: the bomb state machine and its
rules, throw and reset, movement, the prefab kit, the pass feel, first-person feel, and both course halves.

### 18.3 Multiplayer tests

Bots and the Network Simulator (several processes on one machine), plus a manual test on two physical machines
(MVP_TASKS M3.7, still open).

Critical network rule, still the one to protect:

> A bomb that visually appears caught must not explode because the host resolved a stale environment hit afterward.

---

## 19. Logging

During prototype development, use concise structured debug logs for:

- bomb state transition;
- carrier change;
- fuse expiry;
- catch accepted/rejected;
- illegal collision;
- checkpoint activation;
- reset start/end.

Example conceptual output:

```text
[Bomb] Held -> Thrown carrier=Player2
[Bomb] Catch accepted Player3
[Bomb] Thrown -> CaughtGrace carrier=Player3
[Run] Section fail reason=BombWorldContact object=Platform_Moving
```

Debug spam should be removable/disableable.

As built: `PatataLog.Bomb/Run/Throw` (`Assets/Scripts/Core/PatataLog.cs`) are compiled out of release builds and
switchable at runtime (`PatataLog.Enabled`). Client lines are tagged `(mirror)`.

---

## 20. Performance target

Initial target:

- **60 FPS** on a typical development PC.

The MVP has very low entity count, so architecture should favor clarity over micro-optimization.

Avoid allocations in tight per-frame loops where obvious, but do not over-engineer pooling or ECS.

---

## 21. Dependency policy

Before adding a package:

1. confirm Unity built-in/package stack cannot reasonably solve it;
2. confirm the dependency materially reduces implementation time;
3. keep third-party dependencies minimal.

Do not add a large movement/framework/networking asset merely to avoid implementing a small MVP behavior.

---

## 22. Definition of a clean technical state

Before moving from one milestone to the next:

- project compiles;
- no recurring Console exceptions;
- required scene references assigned;
- prefabs are saved;
- tested task acceptance criteria pass;
- no known blocker is hidden behind TODO comments;
- new public tuning fields have sensible defaults;
- README/spec does not contradict implementation.

---

## 23. Architecture decisions intentionally deferred

Do not prematurely lock:

- custom character motor vs CharacterController-based motor;
- exact network transform strategy;
- advanced client prediction;
- lag compensation details (since decided, see §13.2);
- animation system sophistication;
- final art pipeline.

Choose the simplest approach that allows the pass sandbox to be tested.

---

## 24. Recommended first technical vertical slice

The first complete architecture slice should contain:

```text
PassSandbox scene
+ Player prefab
+ Bomb prefab
+ RunManager
+ one checkpoint/reset state
+ local movement
+ throw/catch
+ fuse
+ world-contact explosion
```

Only after that works should the network layer mirror the same state transitions.

---

## 25. Look / rendering (M9.3)

**Stylized cinematic, at golden hour** (owner decision, replacing the first "soft, bright party-game toon" look): a
low warm sun with long soft shadows, a cool sky filling the shadows, a warm haze, and a filmic grade. Dramatic, but
the potato, the hazards and the slot identity always read. Kept on purpose: the comic **BOOM!** and the cartoon
explosion (`ExplosionFx`), no chromatic aberration or lens distortion, and every flash and view effect still honours
`flashReduction` / `viewEffectsStrength`.

- **Shaders** (`Assets/Art/Shaders`): kit, pads, bomb, characters and backdrop use `HotPatata/Stylized` (the file is
  still `HotPatataToon.shader`, and every property name is the old toon's, so no material reference changed),
  hand-written URP HLSL (not Shader Graph) that reuses URP's ShadowCaster/DepthOnly/DepthNormals passes.
  - **Light:** a soft wrapped Lambert (`_Wrap`) with the main light's real shadows, nudged towards the old two-band
    ramp by `_Stylize`; GGX specular (`_Smoothness`, optional `_GlossMap`); optional normal map (`_BumpMap`);
    additional lights the same way. The shadowed side is filled only by the sky (SH ambient times SSAO, slightly
    tinted by `_ShadeColor`, strength `_AmbientStrength`): deep, never black.
  - **Rim:** light-aware, strongest against the low sun (a warm edge on backlit shapes).
  - **Kept:** `_TopColor` on upward faces, an optional spec blob, emission always added (MaterialPropertyBlock
    friendly), a fake bevel on scaled unit cubes (`_EdgeWidth`), world checker/stripes (`_Pattern`; hazards are
    striped so they do not rely on red alone), `_VERTEX_COLOR` for particle meshes, `_SuitTint`.
  - Particles, trails, flashes and the BOOM use `HotPatata/Particle`.
- **Materials:** the kit materials kept their names (`Greybox_*`, `Pad_*`), so prefab references did not change. The
  characters' materials (`Assets/Art/Materials/Characters`) take each model's diffuse, normal and gloss maps; Remy's
  hair and eyelashes keep their imported materials (they need alpha). The potato's material gets a stronger sky fill
  and a warm rim, so it reads in the long shadows.
- **Light, sky and grade: generated** by `LookBuilder` (menu **HotPatata/Look/Apply Look To All Scenes**, never set by
  hand), for Bootstrap, PassSandbox, PrototypeCourse, PlaytestCourse, PatataPark and PatataWorks:
  - the sun: 22° high, warm, soft shadows, from behind and to the side of each scene's main run (`PC_RPAsset` shadow
    distance 75 m); the menu's sun is built the same way by `MenuBackdropBuilder`;
  - trilight ambient (cool sky, warm equator, dark ground) and a warm linear haze matched to the horizon;
  - a sky per scene (`Sky_HotPatata_<Scene>`, a copy of `Sky_HotPatata`) with its sun and glow where that scene's
    sun is;
  - the global `LookVolume` (`HotPatata_Look.asset`): ACES tonemapping, more contrast, a warm white balance, split
    toning (teal shadows, warm highlights), bloom only above 1 (what really shines). No vignette in game: the speed
    effects own it.
  - MSAA 4x on `PC_RPAsset`.
- **Backdrop:** see §4 (`PrototypeCourse/Backdrop`, |x| >= 25 m, no colliders).
- **Player identity:** one colour and one shape per slot, both in `GameTuning` (`playerColors`, `playerShapes`) and
  read through `PlayerIdentity`. Slots 1–4: royal blue ●, sky blue ▲, plum ■, white ◆. The palette was chosen by
  simulating protanopia, deuteranopia and tritanopia (Machado 2009, full severity) and measuring CIEDE2000: every
  pair stays at least 23 apart in all four visions (the old orange/cyan/green/pink palette fell to 8.7 in tritanopia),
  and every colour stays at least 18.7 from hazard red, the potato orange and glow, and the carrier yellow. Warm hues
  are left to the bomb and hazards. Spec §19 (never colour alone): in game each slot is **its own character**
  (`GameTuning.playerCharacters`: 1 James, 2 Remy, 3 The Boss, 4 James until a 4th model; not a player choice, spec
  §17.2), with a **ring at its feet** in the slot colour holding the slot's shape as a flat glyph
  (`PlayerShapeMesh.Flat`: ●, ▲, ■, ◆; hidden under your own camera), and the carrier indicator takes the slot's shape
  (`PlayerShapeMesh`: sphere, pyramid, cube, octahedron, each reading as its glyph from any side while it spins, in the
  carrier yellow); the lobby and results show the glyph in the slot colour.
- **Characters** (`PlayerCharacter`, `Assets/Prefabs/Player/Characters`, generated by `PlayerCharacterBuilder`, menu
  **HotPatata/Player/Build Characters**; never edit them by hand):
  - Mixamo models and clips, all **Humanoid**; the clips copy James's avatar, so one set plays on every character.
    Clips live in `Assets/Art/Models/Characters/Animations` (Standing Idle, Slow Run, Fast Run, Jump, Running Jump,
    Throw; optional Cheering, Waving, Hit Reaction for the menu show). The builder names each clip after its file,
    bakes the root into the pose (the motor moves the player; root motion stays off) and trims the jumps to take-off
    to peak.
  - Each model is fitted to 1.75 m, feet on the ground, with a **`HandSocket`** in the right palm. A slot's character
    is swapped in by `PlayerPresentation.ApplyIdentity` when the slot is set (offline and online, no extra networking)
    and bound to `PlayerAnimator`.
  - `PlayerCharacter.controller`: Locomotion (0 Standing Idle, 1 Slow Run, 2 Fast Run on `Speed`), Jump (standing or
    running air pose), and a Throw layer on an upper-body mask (`ThrowUpperBody.mask`), so the legs keep running.
  - **The held potato**: in third person `HandAnchor` sits at the palm (`PlayerAnimator`, a small serialized offset
    and yaw so it points forward out of the hand); first person keeps the camera anchor. The potato's model size is
    `GameTuning.potatoVisualScale` (visual only, the same in the hand, in flight and in first person).
  - **The held throw**: the Throw clip plays to `GameTuning.throwAnimationHoldNormalized` (the frame where the hand is
    back and up, measured on the clip and checked by `ConfigurationTests`) and waits there (`ThrowSpeed` 0) while the
    button is held, then follows through on release. The flight itself is unchanged. Simulated swatches:
  [`images/player-palette.png`](images/player-palette.png); four players in PassSandbox, raw and simulated:
  [`images/player-lineup.png`](images/player-lineup.png).

### 25.1 KayKit kit

`PatataWorks` extends the palette with Platformer timber floors, structural pillars and horizontal struts. Its
interior roles are `Floor`, `Ceiling`, `Brick`, `Frame`, `Stairs`, `Pillar`, `Truss`, `Railing` and `Roof`; brick walls
use `KayKit_Brick`. Floor panels form one visible layer, avoiding hidden stacks inside the slabs. No additional art pack.

Factory lighting is owned by `LookBuilder`: the existing golden-hour sun and grade, reduced ambient fill, and steady
warm practical point lights (orange at furnaces). Forward+ handles additional lights; they cast no extra shadows.
Window openings and roof-light slots admit sunlight. No baked probes or new flashes/audio. The finish name board is
a world-space UI Toolkit document (`Assets/UI/World/PatataWorksSign.uxml`).

The level kit is drawn with the **KayKit Platformer Pack 1.0** (Kay Lousberg, CC0, `Assets/Art/Models/Map/`; credit
is optional but welcome in the credits). Gameplay never changes: colliders, sizes, layers and resize helpers stay as
they were, only the look does.

- **`KitSkin`** (`Assets/Scripts/Kit`, presentation only) sits on a kit `Visual`. Its box is the object's scale times
  `unitBox` (a unit cube, or `(1,2,1)` for a cylinder), tiled with the best-fitting piece of one family (least stretch,
  plus a small cost per piece) and drawn as one combined mesh through the object's own `MeshRenderer`, so belt scroll
  and gate lamps keep working. The mesh is built on enable and cached per size; the `MeshFilter` is serialized empty
  and never saved with a mesh. Off-grid boxes stretch the pieces a few percent; boxes on the KayKit grid use them
  unstretched. A platform box standing on edge (a piston gate, a door) is drawn with blocks.
- **Families** (`KitShape`): `Platform` (coloured top over grey sides), `Barrier` (rounded blocks one metre deep),
  `Arrow` (platforms with a chevron, pointing -Z unless flipped), `Pipe` (tubes along Y). The meshes are listed in
  `KitPalette` (`Assets/ScriptableObjects/Kit/KayKitPalette.asset`), generated from the pack; they are imported
  readable because the skin combines them at run time.
- **One look per role** (`KitRole`, `CourseKit.Look`): ground green platforms, walls neutral (white) blocks, movers
  (moving platforms, elevators, pistons, bridges, lifts) blue platforms, falling platforms yellow, belts blue arrows,
  slides green arrows, hazards red blocks with stripes (never colour alone, spec §19), gates and hoops yellow blocks.
  Launch pads are a squat KayKit spring pad. Zones, pads, signs and beams keep their own materials.
- **Tubes and cannons** are KayKit pipes (`pipe_end`, `pipe_90_A`, `pipe_straight`, 1 m radius) in their slot colour
  (`Tube_Pipe_N`: the pack's texture through `_SuitTint`, so grey and white bands stay). A mouth is a flange whose rim
  lies on the collision ring; `ConfigureTube` routes the pipe up behind the mouth and overhead into the exit (two
  elbows) when the exit is high and far enough, else low then up (one elbow), else straight; the exit is a flange
  along the launch direction. Straight runs carry a capsule never wider than they are long (a short one would overhang
  the exit arc), elbows a box. The cannon barrel is a pipe with a flange at the muzzle.
- **Materials:** `KayKit_Toon`, `KayKit_Hazard`, `KayKit_Belt` are `HotPatata/Stylized` with the pack's palette texture
  (mipmaps off, so the colour columns never blend), a lighter shade tint than the greybox, no fake bevel.
- **Rebuilding:** menu **HotPatata/Course/Build KayKit Kit** (palette, materials, the hand-made classic prefabs, the
  generated prefabs, and the hand-placed boxes of PrototypeCourse Act 1 and PassSandbox), then **HotPatata/Course/Rebuild
  All Courses**. Builders place boxes with `CourseKit.Block(..., KitRole)`.

### 25.2 Industrial look (spike #87)

A semi-realistic alternative to the KayKit kit, tried on its own test map `IndustrialLab` (`Assets/Scenes/IndustrialLab.unity`,
menu **HotPatata/Course/Build Industrial Lab**; **… (KayKit baseline)** builds the same map in the KayKit look). The map is
not in the menu, in `NetworkBootstrap.gameplayScenes` or in the build settings: open it in the Editor and play. It is
about 150 m long: a covered entry, a 4 m pit, a grated catwalk with a pass, a raw-metal ramp, a rubber pad, a lit covered
hall and an open finish, with two checkpoints. A calibration wall on each side (`|x|` = 32 m, no colliders) shows a 1 m, a
4 m and a 12 m panel per material, in the industrial look (east) and in KayKit (west). Gameplay dimensions follow
PatataWorks; the look changes no collider (the 38 colliders of the map are identical in both looks).

- **Shader `HotPatata/Industrial`** (`Assets/Art/Shaders/HotPatataIndustrial.shader`): a fork of `HotPatata/Stylized`
  (same wrapped-Lambert light with real shadows, sky fill times SSAO, GGX, Forward+ additional lights, light-aware rim),
  so `Stylized` and every scene that uses it are untouched. Added:
  - **box mapping in metres**: the texture is projected on the dominant axis of each face from the object-space position
    times the object's scale (`_TileSize` = metres per repeat). A 12 m slab and a 1 m cube share one texel density, nothing
    stretches, the texture sticks to moving platforms, and neither the mesh UVs (KayKit's atlas) nor tangents are used;
  - maps, all always sampled with neutral defaults (no keyword to tick): `_BaseMap` (sRGB), `_BumpMap` (OpenGL, `_BumpScale`,
    `_FlipNormalY`), `_GlossMap` (R, smoothness, or roughness with `_GlossIsRoughness`; remapped by `_SmoothnessMin/Max`),
    `_MetallicMap` (R, times `_Metallic`), `_OcclusionMap` (R, `_OcclusionStrength`, affects the sky fill only);
  - **metals**: F0 from the albedo, no diffuse, and a cheap environment reflection from the ambient SH (`_EnvReflection`): no
    reflection probe (§25.1: no baked probes);
  - **repetition**: a macro layer of the same texture at `_MacroScale` (`_MacroStrength`) and a faint procedural variation
    (`_VariationStrength`) that also gives the texture-less placeholders some life.
  - Not supported: height/displacement, opacity, per-pixel emission maps. The scrolling belt pattern of `Stylized` is not
    in this shader (a rubber belt would not animate).
- **Materials** (`Assets/Art/Materials/Industrial`): `Industrial_Concrete`, `_PaintedMetal`, `_RawMetal`, `_Rubber`, and two
  **Material Variants** of the painted metal, `_PaintedMetal_Yellow` (structure, railings) and `_PaintedMetal_Safety` (pale
  landing-edge strips), which inherit the parent's textures and only override the tint. They work without any texture (flat
  colour, faint variation). `IndustrialMaterialBuilder` (**Build Industrial Materials**) creates what is missing and never
  rewrites an existing material, so the textures you assign survive every rebuild of the kit or of a course;
  **Reset Industrial Placeholders** is the one explicit way back to the defaults.
- **`KitShape.BevelBox`** (`KitSkin`, appended to the enum): a chamfered box generated by script (chamfer a tenth of the
  thinnest side, 2–6 cm, constant in metres), cached per size, never saved, with every vertex inside the box so a visual
  never sticks out of its collider. It needs no palette piece.
- **Look sets** (`CourseKit.UseLookSet`): `KayKit` (default, everywhere) or `Industrial`, which maps roles to the new
  materials (`Floor/Ceiling/Brick/Stairs/Roof/Ground/Wall` concrete; `Pillar/Truss/Mover` painted metal;
  `Frame/Railing/Gate` yellow painted metal; `Grating/Slide` raw metal; `Rubber/Belt` rubber; `Accent` the pale edge).
  Hazards and falling platforms keep their KayKit look (their stripes are what stops them relying on colour, spec §19). Only
  `IndustrialLabBuilder` turns it on.
- **Details** (`IndustrialKit`, all `CourseDecoration` without colliders): landing-edge strips and joints (5 mm above the
  surface at most), column base plates and caps, braces, beams. `IndustrialLabTests` checks them against the pass corridors.
- **Light**: unchanged (`LookBuilder`, one scene line added); warm `FactoryLamp`s in the covered hall.

**Importing your textures**

| | |
|---|---|
| Folder | `Assets/Art/Textures/Industrial/<Concrete, PaintedMetal, RawMetal or Rubber>/` (sources and licences in `CREDITS.md`) |
| File names | end in `_BaseColor`, `_Normal`, `_Roughness`, `_Metallic`, `_AO`. ambientCG (`_Color`, `_NormalGL`, `_Roughness`, `_Metalness`, `_AmbientOcclusion`) and Poly Haven (`_diff`, `_nor_gl`, `_rough`, `_metal`, `_ao`, with a trailing `_2k`) names are understood as they are |
| Maps | required: base colour, **OpenGL** normal. Recommended: roughness. Metals only: metalness. Optional: AO. Ignore height, displacement, opacity |
| Import | set by `IndustrialTextureImporter` on first import: base colour sRGB; normal as Normal map; roughness, metallic and AO linear (sRGB off); all Repeat, trilinear, mip maps on, aniso 8, High Quality compression, 2048 max. Square power-of-two, tileable, 1K–2K |
| Assign | open `Industrial_<X>.mat`: Base Map, Normal Map, Gloss or Roughness Map (tick *Map Is Roughness*, no conversion or packing), Metallic Map (set *Metallic* to 1), Occlusion Map. Assign on the parent: the variants follow |
| DirectX normals | tick *Flip Normal Y* (the importer warns) |
| Tiling | **Tile Size (m)** (concrete about 2, metal 1, rubber 0.5), never the material's Tiling; *Macro Strength* against repetition |
| Check | open `IndustrialLab`, look at the calibration wall (same density at 1, 4 and 12 m, no stretch), then run the route |

**Result so far (placeholders only; textures and the owner's play-test are still to come).** Same render time as the KayKit
look in the Editor (four views of the map, 1080p: about 1.1–1.5 ms both, the first view is noisy), with 80% fewer triangles
(37 k against 181 k, 125 mesh renderers in both) and five texture samples per pixel instead of three. Before/after:
[`images/industrial-lab`](images/industrial-lab). The bevelled boxes and the lighting read well; the look cannot be judged
without real textures. Limitations: no animated belt, no reflection probe (metals are dull rather than mirror-like), the
placeholders are flat. Decision: pending the textured pass.
