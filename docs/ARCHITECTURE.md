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
`SessionService` wraps Multiplayer Services (§13.1); the menu and the lobby are in-world UI Toolkit boards in the
menu's misty forest clearing, driven by `MenuView` (§6.2, #79).

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

Removed in the project cleanup. Its beats (Training Grounds, Patata Factory, The Climb & The Drop) are archived in
[`OBSTACLES.md`](OBSTACLES.md) §3.2; the scene, `CourseBuilder`'s acts, `CourseTests` and `FactoryCourseTests` are in git
history. The generated kit prefabs it introduced (conveyor, elevator, piston, crusher, sweeper, windmill, hoop) are built by
`KitPrefabBuilder` (menu **HotPatata/Kit/Build Generated Obstacle Prefabs**), and their behaviour is tested alone by
`ObstacleKitTests`.

### `PlaytestCourse` (M10, #68)

Removed in the project cleanup; its four acts (Warm-up, Hot & Cold, Switchboard, Grand Finale) are archived in
[`OBSTACLES.md`](OBSTACLES.md) §3.2. PassSandbox keeps its small `KitDemo/BombObstacles` corner (menu **HotPatata/Course/Build
Sandbox Bomb Obstacles**) to try the zones, curtains and a ring-driven lift with the F4 bot.

### `PatataPark` (the KayKit course)

Removed in the project cleanup; its four acts are archived in [`OBSTACLES.md`](OBSTACLES.md) §3.2.

### `PatataWorks` (M11, #83)

Removed in the project cleanup; its sections and signature puzzles are archived in [`OBSTACLES.md`](OBSTACLES.md) §3.2. Its
proven layouts are the industrial plant's (§25.2), and its enclosed-course machinery lives on: `PassCorridor`,
`CourseCeiling`, `CourseDecoration`, `CourseRoute`, and in `CourseKit`, `PrepareCourseScene` (a missing course scene is
copied from the course template), `ValidatePasses` (every declared pass reachable, openings wide enough, 1.5 m of ceiling
clearance) and `RegisterInMenu`.

### `PatataWilds` (M12), `PatataCanopy` (M13), `PatataTemple` (M15), `IndustrialPlant`, `PatataRanger` (M17), `OldOak`

Archived at the fresh start (2026-10-16): their scenes, builders (`PatataWildsBuilder`, `PatataCanopyBuilder`,
`PatataTempleBuilder`, `IndustrialPlantBuilder`), tests and generated land are on the branch `archive/maps-v1`, and every section
is described in [`MAP_ARCHIVE.md`](MAP_ARCHIVE.md). What they left in the project: the kit and its looks, the four nature days
(`PatataWildsLook`, `PatataCanopyLook` with its leaf dapple, `PatataTempleLook` with its storm, `PatataRangerLook`) that a level
borrows, the industrial look (§25.2), the timber kit and the structure library (§27), and `NatureDressing`'s shared planting
helpers.

### `Templates/CourseTemplate`

The scene a new level is copied from (`CourseKit.CourseTemplatePath`, `PrepareCourseScene`): a `RunManager`, the start spawns
under `SectionRoot`, the bomb, the camera, the local test rig, the `PlayerSpawner` and the look volume, no geometry. It is the
industrial plant's scene emptied. Never in the build.

### Levels (`Assets/Scenes/Levels/`, M18)

Made by hand with the level tools (§27, `LEVEL_AUTHORING.md`); listed in the menu with **Register In Menu**. `PassSandbox` stays
in the level list as a dev-only scene (`NetworkBootstrap.devOnlyScenes`): a release build (`!Debug.isDebugBuild`) takes it out of
the list on start (`HideDevOnlyScenes`), and with no other level the menu says there is none.

---

## 5. Project folders

As built:

```text
Assets/
├── Art/
│   ├── Materials/        kit materials (Greybox_*, Pad_*), stylized materials, a sky per scene (Sky_HotPatata_<Scene>)
│   ├── Models/           Bomb/ (potato.glb), Characters/ (Mixamo: James, Remy, The Boss; Animations/, Controllers/),
│   │                     Player/ (the old KayKit mannequin, unused), Map/ (KayKit Platformer Pack, CC0),
│   │                     Nature/ (Poly Haven scans; Generated/ trees, plants, rocks and the water plane, §25.3)
│   ├── Nature/Generated/ PatataWilds' terrain meshes and FoliageSet (generated by NatureDressing)
│   ├── Sky/HDRI/         PatataWilds' five Poly Haven skies
│   ├── Shaders/          HotPatata/Stylized (file HotPatataToon.shader), HotPatata/Particle, HotPatata/Sky, speed lines,
│   │                     HotPatata/Industrial, HotPatata/Nature, HotPatata/Water, HotPatata/SkyBlend
│   ├── Textures/Icons/   zone and tube icons drawn in code by BombObstacleKitBuilder
│   ├── Textures/Nature/  PatataWilds' Poly Haven texture sets, one folder per surface; Generated/ (water, sprays)
│   └── VFX/              textures for particles and trails
├── Audio/                the sound library (§26, see its README): Music/, Ambience/, SFX/ (a folder = a cue), HotPatataMixer,
│                         Resources/HotPatataSoundBank (generated); Ambient/ synthesised fallbacks (NatureAudioFactory)
├── EditorTools/          CourseKit, KitPrefabBuilder, BombObstacleKitBuilder, KitVariantBuilder, ObstaclePrefabValidator,
│                         KayKitKitBuilder, PlayerCharacterBuilder, MenuBackdropBuilder, LookBuilder, the Industrial* builders,
│                         PatataWildsBuilder (+ .Acts), PatataWildsLook, NatureKit, NatureRestyle, NatureMaterialBuilder,
│                         NatureAssetImporter, NatureTextureFactory, NatureAudioFactory, NatureTreeBuilder, NatureDressing,
│                         PatataCanopyBuilder (+ .Acts), CourseContractCheck
├── Prefabs/              Bomb/ Gameplay/ Menu/ (MenuBackdrop, generated) Nature/ Network/ Obstacles/ Platforms/
│                         Player/ (Player; Characters/, generated) Variants/ (kit variants, §10.7) VFX/
├── Scenes/               Bootstrap, PassSandbox, Templates/CourseTemplate, Levels/ (authored levels)
├── ScriptableObjects/    Tuning/GameTuning.asset, Kit/KayKitPalette.asset (generated)
├── Scripts/
│   ├── Core/             GameTuning, NetMode, SimulationClock, SectionClock, IResettable, PatataLog, SyncStats,
│   │                     Settings, AudioVolumes
│   ├── Networking/       NetworkBootstrap, BootstrapEntry, SessionService, NetworkPlayer/Bomb/RunState/FallingPlatform,
│   │                     NetworkBombGate/SignalActuator/BombTransit
│   ├── Player/           Player, PlayerMotor, RiderReconstruction, PlayerLook, PlayerThrower, PlayerCatcher, PlayerCatchVolume,
│   │                     FirstPersonCamera, PlayerViewFeel, SpeedEffects, PlayerPresentation, PlayerAnimator,
│   │                     PlayerCharacter, PlayerShapeMesh, PlayerPause, ...
│   ├── Bomb/             BombController, BombFuse, BombPhysics, CatchResolver, FlightHistory, AimAssist,
│   │                     ThrowBallistics, BombAudio, BombPresentation, ExplosionFx, ProceduralSfx
│   ├── Audio/            SoundBank, CueSettings, CueKeys, AudioDirector (+ Sfx, LoopHandle), MusicDirector, MusicContext,
│   │                     AmbienceDirector, AmbienceZone, CourseAudio, CourseSounds, ObstacleSounds, UiSounds, PlayerSounds,
│   │                     AudioSurface (§26)
│   ├── Run/              RunManager, Checkpoint, KillZone, HazardRewind, FinishZone, PlayerZone, PlayerSpawner, PlayerSpawn
│   ├── Obstacles/        MovingPlatform, RotatingObstacle, FallingPlatform, Conveyor, LaunchPad, IPlatformCarrier,
│   │                     CarrierRegistry, ITimePosed, IObstacleState, ObstacleVisualDriver (§10.7)
│   ├── Kit/              KitSkin, KitPalette (KayKit visuals, §25.1), NatureShapes (generated nature shapes, §25.3),
│   │                     CustomVisual (a hand-made visual the look passes skip, §10.7)
│   ├── Nature/           TimeOfDayPreset, TimeOfDayBlender, CampfirePresentation, FoliageSet, FoliageInstancer,
│   │                     NatureWater, NatureTerrain, AmbientLoop (PatataWilds presentation, §25.3), TempleStorm (§25.4)
│   ├── Menu/             the living menu backdrop and the in-world menu (§6.2): MenuHotPotato, MenuFloat,
│   │                     MenuCloudDrift, MenuCameraDrift, MenuFlow, MenuStation, MenuCameraRig, MenuLobbyStage
│   ├── Zones/            Zone, IBombZoneEffect, BombZoneSweep, FuseZone, BombBarrier, BombGate, PressurePlate,
│   │                     ISignalSource, SignalActuator, SignalIndicator, BombTransit, TransitMouth, TransitPresentation,
│   │                     SunBeam, SunBeamVisual, PlateGauge (the systems for three, §10.6)
│   ├── UI/               ScreenStack, UIScreen, UIScreenCatalog, UIParts, CursorPolicy (UI Toolkit base, §6.2),
│   │                     MenuView, StationScreens, MenuParts, CodeDials, PauseMenu, SettingsScreen, SettingRows,
│   │                     ConfirmScreen, ResultsScreen, RunResultsUI, AimReticle
│   ├── Debug/            DebugHud, LocalPlayerSwitcher, PlayerBot
│   └── DebugTools/       Editor/dev-build only: LatencySimulator, ThrowDebugOverlay, ThrowTelemetry, PassPartner,
│                         UISampleScreen, NetSyncProbe (network time-frame diagnostics, F7)
├── Settings/             URP assets (PC_RPAsset, PC_Renderer), Look/HotPatata_Look.asset, Look/MenuFocus_<Station>.asset,
│                         Look/TimeOfDay/ (PatataWilds' presets and grades, written by LookBuilder)
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

**Audio volumes.** `Assets/Audio/HotPatataMixer.mixer` (M16, §26):

| Group | Plays | Slider | Paused snapshot |
|---|---|---|---|
| `Master` | everything | `masterVolume` (`AudioListener.volume`) | |
| `Master/Music` | `MusicDirector` | `musicVolume` (`MusicVolume`) | low-pass |
| `Master/Ambience` | `AmbienceDirector`, builder emitters, campfires | `ambienceVolume` (`AmbienceVolume`) | low-pass |
| `Master/SFX` | parent of the three below | `sfxVolume` (`SfxVolume`) | |
| `Master/SFX/Bomb` | `BombAudio`, the tube/cannon warning | (SFX) and `beepVolume` | clear |
| `Master/SFX/Movement` | `PlayerSounds` | (SFX) | low-pass |
| `Master/SFX/World` | the run's moments, obstacles | (SFX) | low-pass |
| `Master/UI` | menu sounds | `uiVolume` (`UiVolume`) | clear |

The master slider is `AudioListener.volume`, so it reaches every sound, routed or not. Each group's setting is applied as
decibels by `AudioVolumes` (on the persistent `NetworkManager` prefab, from `Start` and on `Settings.Changed`). The
mixer runs on unscaled time, so the pause snapshot (`Paused`, entered while `PauseMenu.IsOpen`) moves while an offline
pause freezes the game; the bomb and the menus stay clear because online nothing pauses. `SoundBank.GroupFor` routes a
cue by its folder. A new sound source must be routed to a group too.

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
  `HotPatata.uss` + `HotPatataIndustrial.uss`.
  - **Industrial theme (#87, active).** `HotPatataIndustrial.uss` re-skins every `hp-*` class to match the industrial
    look (§25.2): dark blue-grey steel panels with a hazard-amber top rail, light text on
    dark, near-black edges, squarer corners, industrial orange / steel blue / gunmetal / signal red buttons, amber focus.
    It only overrides tokens and shapes: removing its import from the `.tss` brings back the toy box look below. The base
    sheet keeps text (`--hp-ink`) apart from edges (`--hp-outline`) and white surfaces (`--hp-surface*`), notes, errors
    and veils as tokens, so a theme never has to restate a rule's layout.
  - **Look ("Sunny toy box", owner-approved mock-up; the base under the industrial theme).** Cream rounded panels (`hp-panel`), chunky buttons with a
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
  message, selected level and spawn point, and the lobby list. The menu is **part of the clearing**: each step is a
  **station**, a board (a world-space `UIDocument`) with a Cinemachine camera spot, and choosing an option flies the
  camera to the next station. `MenuView`, which `NetworkBootstrap` adds outside batch mode (headless bots run without
  UI), builds every station's board and shows the station `MenuFlow` points at:

  | Station | Board (`StationScreens.cs`, UXML) | What it holds |
  |---|---|---|
  | Title | `TitleStation` (`StationTitle`), the root: Back does nothing | the HOT PATATA logo floating over the clearing (the potato show below it, in the mist) and one prompt: PLAY |
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
- **Menu backdrop** (`Assets/Prefabs/Menu/MenuBackdrop.prefab`, one instance in `Bootstrap`). A forest clearing at a misty
  blue-grey dawn in the nature look (§25.3; `MenuBackdropBuilder.Forest.cs`): a grass floor, flat round the show and rising far off;
  trees all round (broadleaf and firs from 15 m, giant oaks beyond 34 m, one instanced `FoliageSet`, `Menu_Foliage`) that
  dissolve into the mist, never standing on a camera spot's line to its board or the show; grass, ferns and bushes in the
  clearing; a small campfire right of the show, always burning (the checkpoints' fire, `CampfirePresentation.alwaysLit`), its
  light dim and a warm glow round it in the mist; light shafts behind the show; the mist on the floor (`MistField`). **No wall, no sky, no horizon**: the light is
  `MenuForestLook` (through `NatureDayLook`, one held moment: a low sun behind the show shining towards the title camera, a haze
  from 10 to 80 m, a low mist), and the menu camera clears to the haze's colour (`MenuForestLook.Haze`), so the far trees melt
  into it. From the Title station the four slots' characters, about 21 m away, stand as silhouettes against the bright mist
  while they pass the live potato in arcs, hand to hand (`MenuHotPotato`); the lobby stage is 9.5 m from its camera, in front
  of the haze and under a warm stage light, so the joined players step forward out of the mist into colour. It is the home of
  the in-world menu (above). The holder faces the next catcher, a catch squashes the catcher and bystanders cheer, wave or hop.
  The wick sparks faster (`GameTuning.fuseSparkRates`) until the fuse runs out in the game's own explosion
  (`ExplosionFx`, its flash always reduced at least 60 %, more if the player asked through `flashReduction`); the
  holder flinches, the others cheer, and a new potato pops in. Motes drift in the low sun, and on the Title station the camera drifts slowly (`MenuCameraDrift` on the Title spot, scaled by `viewEffectsStrength`).
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

Removed on purpose, do not add back: chromatic aberration, lens distortion, wind that follows the speed. Movement
sounds came back in M16 as real recordings (footsteps by surface, jump, landings, slide: `PlayerSounds`, §26). Every effect scales with `GameTuning.viewEffectsStrength` and `flashReduction` dims every flash
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
- explosion sound;
- a fuse loop while the fuse burns, and the tube or cannon "thunk".

Should consume bomb state; should not control bomb state. Its `AudioSource` outputs to the mixer's Bomb group (§6.1).
Each sound is a folder of `Assets/Audio/SFX/Bomb` (§26); an empty folder plays the `ProceduralSfx` placeholder.
`BombAudio.AnyBeeped` lets the music dip under each beep.

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
the sounds of `Assets/Audio/SFX/Bomb/*` (§26) when their folder holds any and `ProceduralSfx` otherwise.

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
`Visual` and `Collision` exactly as before. A variant may switch `Visual` off and draw its own model under a `CustomVisual`
beside the colliders (§10.7).

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
a pure function. `SectionClock` is a façade over `SimulationClock` (§13.1): one sample of the network time per frame,
monotonic on clients. Every time-driven mover also implements `ITimePosed` (`TryPoseAt(serverTime)`, `LethalAt`): it
can say where its moving part was at any time, which the host uses to judge remote players against moving hazards
(§13.1). Anything that carries riders implements `IPlatformCarrier`:

- `FrameDelta`: how far a rider is carried this frame (moving platforms, elevators, pistons, `Conveyor` belts). A
  carrier that moves calls `CarrierMotion.Moved` right after writing its pose (#97): it carries its local riders
  (`PlayerMotor.CarryRiders`) and pushes its pose to its kinematic body (`autoSyncTransforms` is off, so otherwise the
  collider would lag the drawn deck until the next fixed step). A deck that rises carries its riders first, then moves
  its collider; any other motion moves the collider first. The rider's own move comes after, so its ground stick
  always lands back on the deck: one combined move pointed up on a lift faster than the 2 m/s stick, and riders lost
  their footing every other frame. A belt does not move, so the rider's motor carries itself;
- `CarrierId`: the same number on every machine, derived at load by `CarrierRegistry` from the scene and the
  sibling-index path (never names), and checked unique by `CarrierRegistryTests`;
- `Moves` (false for a belt);
- `AnchorPosition`: the origin of a rider's offset when riders are synced relative to the carrier (§13.1).
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
- **Hands-free plate:** `PressurePlate.countCarrier` off: `PressurePlate.IsHeld` leaves out `BombController.Carrier`, and
  while the bomb is Thrown or InTransit its `LastThrower` (`PressurePlate.ExcludedPlayer`: a bomb in flight is still its
  thrower's, PROJECT_SPEC §13.21).
- **Heavy plate** (M15): `PressurePlate.requiredBodies` (2): active when `Counted` ≥ it. **Hourglass plate:**
  `memorySeconds`: the host keeps `ActiveUntil` (open-ended while held, the release time plus the sand after),
  `NetworkPressurePlate` replicates it and clients only `Mirror` it; reset with the section. `PlateGauge` (presentation) lights
  a footprint per counted body and empties the ring of sand; `SignalIndicator` pulses an hourglass plate while its sand runs.
- **Sun beam** (M15): `SunBeam` is an `ISignalSource` on a long thin `Zone`: `Cut` while `PlayerZone.Collect` finds a body in
  it, `Active` = cut (or whole with `activeWhileWhole`); `Reach` (the nearest body along it) drives `SunBeamVisual` (the shaft
  stops at the body, the eye lights while whole). Stateless like a plate; its actuator is replicated.
- **Pivot** (M15): a `SignalActuator` whose waypoints share a position and differ by a turn (`rotateWithWaypoints`). It reports
  `FrameRotation` and `AnchorRotation` (`IPlatformCarrier` default members, identity for every other carrier): `PlayerMotor.Carry`
  swings a rider round the anchor and `CarriedYaw` turns their view; `NetworkPlayer` sends the rider's offset in the carrier's
  frame and `RiderReconstruction.Blend` rebuilds it with the carrier's rotation as this machine draws it.
- **Switch:** `SignalSwitch` is a `SignalActuator` without a moving part (same source, replication, reset): the
  `ApplyProgress` hook turns its `targets` (any objects) on or off when the progress crosses 0.5. Turning an object off
  removes its `Zone`, `FuseZone` and colliders from play, so no rule knows about switches.
- **Body screen:** `BodyScreen` is a marker; its solid box sits on the `BodyScreen` layer (§12), which collides with
  `Player` only. The bomb's masks (`Environment`, `Hazard`) and the motor's ledge test never see it.
- **Checkpoint arch:** `Checkpoint.claimGate` (a latched `BombGate_Arch`) must have been passed this section.
- **Section contracts** (metadata, never read by gameplay): `SectionContract` beside `PassCorridor`; the reach maths
  (`MaxGap`, `MaxClimb`, `MaxLobHeight`, `MaxSpread`) are pure statics, `CourseContractCheck` (editor) measures a scene. A
  section for three carries `minPlayers` and its `twoFail` line (the check warns when it is missing); a `Spread` lock measures
  the walk between two sources through its `via` points (`WalkLength`).
- **A level's minimum players:** `NetworkBootstrap.sceneMinPlayers` (same order as `gameplayScenes`): the level list labels a
  level for three "(3+ players)", the lobby's Start is disabled below it and `StartLevel` refuses. Offline play is not limited.
- **Transit:** `TransitMouth` (flight effect, the only safe volume) → `BombTransit.Capture` →
  `BombController.EnterTransit`: the bomb is inert at the exit's hold point (inside the pipe, or in the cannon's
  basket) until `ReleaseAt`, then `LeaveTransit` throws it from the muzzle with `BombTransit.ExitVelocity`, the arc
  that reaches catch height above the exit's pad after `flightTime`. `LastThrower` is null, `BombThrown(null)` is
  raised. `NetworkBombTransit` replicates the active exit and release time for `TransitPresentation` (exit lamp ramp,
  rising tone). Resets and explosions clear a transit.

### 10.7 Generic obstacle prefabs (the cleanup)

Every obstacle system is a component that reads serialized references, never child names, renderers or materials, so any
prefab with any visual behaves as that system ([`OBSTACLES.md`](OBSTACLES.md) §4 is the how-to):

- **References only.** `MovingPlatform` (`Platform`, `WaypointA/B`), `SignalActuator` (`Platform`, `WaypointClosed/Open`,
  `LethalEdge`, its `source`), `FallingPlatform` (`Body`, `Trigger`), `BombTransit` (exits: hold, muzzle, pad),
  `TransitMouth` (its transit and exit), `Zone` (its volume). Builders use these accessors; resizing helpers
  (`AddMover`, `ResizeActuator`, ...) still assume the kit's `Visual` + `Collision` boxes and are for kit prefabs only.
- **State for presentation.** Every system implements `IObstacleState` (`Progress` 0..1, `Active`): moving platform (along
  A→B, heading to B), actuator (open amount, opening), falling platform (warning share, triggered), rotating obstacle (turn,
  spinning), transit (delay share, holding the bomb), bomb gate (hold left, active), pressure plate, checkpoint.
  `ObstacleVisualDriver` writes it into an Animator (`Progress` float, `Active` bool) and raises `onActivated` /
  `onDeactivated` / `onProgress` on every machine, from state that is already replicated. Presentation never writes state.
- **Hand-made visuals.** A `CustomVisual` root holds renderers only (never a collider); `IndustrialRestyle` and
  `NatureRestyle` skip everything under it.
- **Overridable motion.** `MovingPlatform.PositionAt`, `SignalActuator.PositionAt` / `RotationAt` (and
  `rotateWithWaypoints`: the part turns from the closed waypoint's rotation to the open one's), `FallingPlatform.ApplyPose`
  and `RotatingObstacle.RotationAt` are virtual. An override must stay a pure function of the clock-derived value it is
  given, so every machine still shows the same thing; riders are carried by translation only.
- **Validation.** `ObstaclePrefabValidator` (menus **HotPatata/Kit/Validate Selected Prefabs** and **Validate All Obstacle
  Prefabs**): references set and inside the prefab, a moving part is a child (never the object carrying the system) with a
  kinematic body, waypoints never ride on it, colliders are not mesh colliders, sit on gameplay layers and never under a
  `CustomVisual`, a Hazard part has a `KillZone`, replicated systems have their network companion and a `NetworkObject`.
  `ObstaclePrefabTests` runs it on every obstacle prefab of the project, variants included.
- **Examples.** `KitVariantBuilder` (menu **HotPatata/Kit/Build Example Variants**) makes `Platform_Moving_Boulder` (the
  moving platform drawn as a scanned boulder) and `Actuator_Bridge_Drawbridge` (the bridge hinged at its near end, standing
  up when closed) as Prefab Variants in `Assets/Prefabs/Variants`. `ObstacleKitTests` plays them.

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

Define explicit layers early. As built, all seven exist (`ProjectSettings/TagManager.asset`).

Suggested layers:

```text
Player
PlayerCatch
Bomb
Environment
Hazard
Trigger
BodyScreen   (M13: collides with Player only; body screens, §10.6)
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
disconnects. The host loads the chosen level (an authored level, or `PassSandbox` in a development build) through NGO scene management; clients follow.

Authority split (the same gameplay classes run offline and online; `NetMode.IsAuthority` is true offline
and on the host):

| Concern | Who decides | How it reaches others |
|---|---|---|
| Player movement, aim | owning client (owner-authoritative `NetworkTransform`) | world transform + replicated pitch; on a moving carrier, the owner's per-tick `PlayerStamp` (carrier id + offset) and every other machine rebuilds the rider on the carrier as it draws it (`RiderReconstruction`) |
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

Remote players against time-driven level objects (the analysis and the staged plan are in
[`netcode-deterministic-plan.md`](netcode-deterministic-plan.md)):

- **One clock.** `SimulationClock.ServerNow` is NGO's network time sampled once per frame. On a client, NGO's
  corrections are slewed in (at most `clockMaxSlew`, a snap only past `clockSnapSeconds`), so the level never runs
  backwards. On the host and offline it is the raw time. Every pose and readout of a time-driven object uses it:
  `SectionClock`, actuators, falling platforms, gates and transit.
- **Player stamps.** Once per network tick, the owner sends a `NetworkPlayer.PlayerStamp`:
  - the `ServerNow` it drew the level at;
  - its feet;
  - the moving carrier it stands on, if any (`CarrierId`), and its offset from the carrier's `AnchorPosition`.

  A teleport sends a fresh stamp with no carrier and a new `Epoch`.

  The carrier comes from `PlayerMotor.RidingCarrier`, which is stable: a frame where the deck is not under the feet
  does not count as leaving (`carrierLeaveGraceSeconds`), a jump, a launch or a teleport does (#97).
- **Riders.** A remote player on a moving carrier is drawn at that offset from the carrier as THIS machine draws it,
  so they stay glued to it whatever the latency. `NetworkPlayer.LateUpdate` runs after NGO applies the transform in
  PreLateUpdate, and before the held bomb is placed. Boarding and leaving blend over `riderBlendSeconds`; only the
  offset is smoothed. A carrier the stamp stops naming is kept `riderLeaveGraceSeconds` before the blend out starts. A
  teleport snaps on a new stamp `Epoch`, never on a distance (`riderSnapDistance`, 8 m, is only a fallback: a rider
  leaving a 9 m/s lift is legitimately metres from their lagging world position). The host's copy is placed the
  same way, so plates, checkpoints, catch centres and triggers see the rider on the platform. Presentation
  (`PlayerAnimator`, `PlayerDustFx`) reads a remote rider's motion relative to their carrier, so a rider standing on a
  lift stands.
- **Throws from a carrier.** The throw keeps the carrier's velocity at the release point (`CarrierMotion.VelocityAt`,
  PROJECT_SPEC §8.2). A client sends its release point in the carrier's frame (`carrierId` + offset) and the throw's
  own velocity; the host releases from its own copy of the carrier, at its own time, and adds the carrier's velocity
  there. The client's lift was drawn a latency earlier, so its world point would be inside or above the host's deck.
- **Moving hazards** (§13.2 has the cap). A `KillZone` under an `ITimePosed` mover (rotating bar, crusher, a closing
  door's edge) does not fail the section on the host for a remote player's trigger; that trigger is only logged.
  `HazardRewind` judges each stamp instead: the player's capsule at the stamp's feet against every armed moving zone
  posed at the stamp's time, plus the midpoint from the previous stamp. The host's own player, static zones (water,
  void) and offline play keep the trigger.

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
sweeps and `FlightHistory` use a crouching or sliding player's real catch centre. A receiver riding a moving platform is
rebuilt on it (§13.1), so their catch centre is on the platform too. `FlightHistory` itself still records host-frame
samples; making it tick-exact is deferred (netcode plan, stage 3 note).

Moving hazards use the same idea for deaths (`HazardRewind`, §13.1). The host poses a moving lethal zone at the time a
remote player's stamp says they saw it. That time is clamped to `[now − GameTuning.hazardRewindCap, now]` (0.35 s), so a
client can only make itself older, never claim a future time to slip past a hazard.

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

Real components in `PassSandbox` and the authored levels, driven through `ScriptedInput`: the bomb state machine
and its rules, throw and reset, movement, the prefab kit and its variants, the pass feel, first-person feel, and both courses.

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
  hand), for Bootstrap, PassSandbox and the levels (a level's day then overrides it, §25.3):
  - the sun: 22° high, warm, soft shadows, from behind and to the side of each scene's main run (`PC_RPAsset` shadow
    distance 75 m); the menu's sun is built the same way by `MenuBackdropBuilder`;
  - trilight ambient (cool sky, warm equator, dark ground) and a warm linear haze matched to the horizon;
  - a sky per scene (`Sky_HotPatata_<Scene>`, a copy of `Sky_HotPatata`) with its sun and glow where that scene's
    sun is;
  - the global `LookVolume` (`HotPatata_Look.asset`): ACES tonemapping, more contrast, a warm white balance, split
    toning (teal shadows, warm highlights), bloom only above 1 (what really shines). No vignette in game: the speed
    effects own it.
  - MSAA 4x on `PC_RPAsset`.
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

The enclosed courses (first `PatataWorks`, removed since) extend the palette with Platformer timber floors, structural pillars and horizontal struts. Its
interior roles are `Floor`, `Ceiling`, `Brick`, `Frame`, `Stairs`, `Pillar`, `Truss`, `Railing` and `Roof`; brick walls
use `KayKit_Brick`. Floor panels form one visible layer, avoiding hidden stacks inside the slabs. No additional art pack.

Factory lighting is owned by `LookBuilder`: the existing golden-hour sun and grade, reduced ambient fill, and steady
warm practical point lights (orange at furnaces). Forward+ handles additional lights; they cast no extra shadows.
Window openings and roof-light slots admit sunlight. No baked probes or new flashes/audio.

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
  generated prefabs, and the hand-placed boxes of PassSandbox), then **HotPatata/Course/Rebuild All Courses** (the sandbox's
  bomb obstacles and the menu backdrop). Builders place boxes with `CourseKit.Block(..., KitRole)`.

### 25.2 Industrial look (spike #87)

A semi-realistic alternative to the KayKit kit. Its course, `IndustrialPlant`, is archived since the fresh start (`MAP_ARCHIVE.md`
§6.4, branch `archive/maps-v1`); the look (shader, materials, decals, signs) stays. The description below is the plant as it was. The
`IndustrialLab` test map (a calibration wall of 1, 4 and 12 m panels per material) was removed in the cleanup:

- **`IndustrialPlant`** (`Assets/Scenes/IndustrialPlant.unity`, menu **HotPatata/Course/Build Industrial Plant**, `IndustrialPlantBuilder`): the
  showcase, a full closed factory of eleven rooms, about 680 m, nine checkpoints. Rooms are built along +Z and turned as units, as in
  PatataWorks: Gatehouse (gate, door, passes) → Sorting line (belts, no-carry strips, a two-route tube, timed gate, piston) → Atrium (stair ramps,
  a plate lift, a return gate and door; climbs 14 m) → Void catwalks (falling grates, a crane platform, low-throw deck) → Cold storage (cold and hot
  lanes, a laser wall) → Chute (a 28 m mega slide, a tube) → Furnace intake (hot zone, crusher) → Furnace loop (windmill wall, crusher, cold respite)
  → Boiler approach (sweeper) → Boiler shaft (rising platforms and a cannon; climbs 34 m) → Control room (arch checkpoint, finish). Six turns, two
  climbs and a drop. The obstacle layouts are PatataWorks' proven ones (pass lengths, jump distances); the arrangement, the solid shell and the look
  are new. Every room has its own **surface theme** (a floor, a wall and a ceiling material), so the plant never shows one texture twice in a row.

**The plant is closed and lit by lamps only.** Walls, roofs and floors are solid, a ceiling step wall closes the gap at every turn where two
rooms have different heights, and a solid foundation fills each room below its floors (its top is the visible pit floor in the pit rooms), so a
deeper pit never looks out under a shallower one. Each room owns a stretch of floor: floors at its start level are cut where the previous room's
stop, floors at its end level where the next room's start, and at a turn the corner square belongs to the next room unless it says otherwise
(the cold storage leaves the catwalks' crane gap and exit, the control room leaves the shaft's top floors with their lift wells and cannon gap).
No two rooms' floors overlap at one height, so two themes never flicker on one surface. There is **no sun**: a directional light's shadows depend on the camera (cascades, a shadow distance), so under a roof
the floor would light up as you approached. `LookBuilder` gives this scene a neutral fill standing in for the light the lamps bounce off walls and floors (no floor goes black between two lamps), a warm haze, and a second global volume (`HotPatata_Look_Plant`, priority 1) adding +0.45 EV of exposure on top of the shared look; the light is about 130 warm
point lamps (hanging, on walls, at the furnaces), none casting shadows, so the lighting is the same wherever the player stands. Lamp fittings and
furnace mouths are emissive and bloom.

**Shader `HotPatata/Industrial`** (`Assets/Art/Shaders/HotPatataIndustrial.shader`): a fork of `HotPatata/Stylized` (same wrapped-Lambert light,
sky fill times SSAO, GGX, Forward+ additional lights, light-aware rim), so `Stylized` and every scene that uses it are untouched.
- **Box mapping in metres**: the texture is projected on the dominant axis of each face from the object-space position times the object's scale
  (`_TileSize` = metres per repeat). A 12 m slab and a 1 m cube share one texel density, nothing stretches, the texture sticks to moving platforms,
  and neither the mesh UVs nor tangents are used.
- **Maps**, all always sampled with neutral defaults (no keyword to tick): `_BaseMap` (sRGB), `_BumpMap` (OpenGL, `_BumpScale`, `_FlipNormalY`),
  `_GlossMap` (R; smoothness, or roughness with `_GlossIsRoughness`; remapped by `_SmoothnessMin/Max`), `_MetallicMap` (R, times `_Metallic`),
  `_OcclusionMap` (R, sky fill only). `_BaseSaturation` and `_BaseBrightness` recolour a source texture (the painted metal is a red paint, kept as a
  grey wear map under a steel tint, never hazard red).
- **Metals**: F0 from the albedo, no diffuse, a cheap environment reflection from the ambient SH (no probe).
- **Against tiling, four layers**: (1) *hex tiling* (`_AntiTile`, a per-material keyword): each map is sampled three times at random offsets and
  rotations and blended, so no tile is visibly repeated; it is on for concrete, plaster, block, grit and rust, and off for brick, tile and plate,
  where rotation would break the lines; (2) a *macro layer* of the same texture at a larger scale (`_MacroScale/_MacroStrength`); (3) a
  *world-space variation* of three octaves (3, 13 and 52 m, `_VariationScale/_VariationStrength`): neighbouring slabs and rooms differ and nothing
  repeats inside a room, with no texture at all; (4) a *grunge mask* (`_GrungeMap`, `_GrungeSize`, `_GrungeStrength`): a large world-scale dirt
  mask that darkens and roughens, which is what makes a floor look used.
- **Decals** (`_Decal`): the same lighting on an alpha-blended quad with its own UVs, drawn just above a surface (see below).
- **Hazard stripes** (`_Stripe*`): `Industrial_Hazard` keeps the rule that a hazard never relies on colour alone (spec §19). **Belts**:
  `_PatternScroll` (set by `Conveyor`) slides the texture along the belt.
- Not supported: height or displacement, opacity, per-pixel emission maps.

**Materials** (`Assets/Art/Materials/Industrial`), one per surface, each fed by its own texture folder under `Assets/Art/Textures/Industrial`:

| Material | Folder | Used for | Borrows until you add a set |
|---|---|---|---|
| `Industrial_Floor_Concrete` | `FloorConcrete` | gatehouse, atrium, boiler approach floors | `Concrete` |
| `Industrial_Floor_Plate` | `FloorPlate` | sorting line, furnace loop, shaft (steel tread plate) | `RawMetal` |
| `Industrial_Floor_Tile` | `FloorTile` | cold storage, control room | `Concrete`, pale and glossy |
| `Industrial_Floor_Grit` | `FloorGrit` | void catwalks, chute, furnace intake (rough, dirty) | `Concrete`, dark |
| `Industrial_Wall_Plaster` | `WallPlaster` | gatehouse, cold storage, control room | `Concrete`, warm |
| `Industrial_Wall_Brick` | `WallBrick` | atrium, furnace rooms | `Concrete`, red |
| `Industrial_Wall_Panel` | `WallPanel` | sorting line, boiler approach (painted sheet) | `PaintedMetal`, green-grey |
| `Industrial_Wall_Block` | `WallBlock` | void catwalks, chute, shaft (concrete block) | `Concrete`, cool |
| `Industrial_Ceiling_Concrete` | `CeilingConcrete` | gatehouse, atrium, chute, furnace intake, shaft | `Concrete`, dark |
| `Industrial_Ceiling_Panel` | `CeilingPanel` | sorting line, cold storage, furnace loop, boiler approach, control room | `RawMetal` |
| `Industrial_Metal_Rust` | `MetalRust` | ducts of the furnace and chute rooms | `RawMetal`, orange |
| `Industrial_PaintedMetal` (+ `_Yellow`, `_Safety`, `_Hazard`, `_Pipe_1..3`) | `PaintedMetal` | columns, trusses, movers, lifts, machines; variants for frames and railings, landing strips, hazards, slot-coloured pipes | |
| `Industrial_RawMetal`, `Industrial_Rubber`, `Industrial_Concrete` | `RawMetal`, `Rubber`, `Concrete` | gratings and slides, belts and pads, generic (the lab) | |
| `Industrial_Lamp`, `Industrial_Glow` | none | emissive fittings, furnace mouths and screens | |

Materials are created only when missing and never rewritten by a rebuild, so what you set in the Inspector survives. **Build Industrial Materials**
creates what is missing (and the empty texture folders); **Assign Industrial Textures** refills every material from its folder (own set first, else
the borrowed one); **Reset Industrial Placeholders** goes back to the defaults. A material with no texture anywhere is a flat colour with the
world-space variation.

**Looks and themes**: `CourseKit.UseLookSet(Industrial)` draws the kit in bevelled boxes (`KitShape.BevelBox`: a chamfer of a tenth of the
thinnest side, 2–6 cm, every vertex inside the collider's box); `CourseKit.UseTheme(...)` picks the floor, wall and ceiling materials of a room.
`IndustrialRestyle` redraws the KayKit prefabs placed in the scene (belts, movers, hazards, pipes, kit boxes) with the room's theme; fuse zones,
laser beams, pads, icons and plates keep their own materials on purpose (gameplay cues). Hazards keep their stripes.

**Surface detail** (`IndustrialDecals`): after the rooms are built, about 300 decals are scattered with a fixed seed (the same plant on every
rebuild): stains, oil, cracks, scuffs and drips lying 6 mm above a floor or a wall, never over a trigger zone, a moving platform or a floor's edge,
with no collider and marked `CourseDecoration`, and never on anything that moves (movers, falling platforms, actuators, rigid bodies). Every PNG of `Textures/Industrial/Decals` is used (a name with *drip*, *leak* or *streak* goes on
walls only; *arrow*, *stencil*, *number* or *line* on floors only). Procedural placeholders (`IndustrialTextureFactory`) are written when the
folders are empty: three tileable grunge masks and five decals; delete them when you add real ones.

**Relief.** Every textured surface has a height map (`_HeightMap`: the set's displacement map, `_disp` or `_Displacement`, else its AO map
as a stand-in) and a depth in metres per surface (`Surface.relief`: 2.8 cm for bricks, about 2 cm for blocks and corrugated panels, 1 cm for tiles
and grit, a few millimetres for concrete and metals). The shader ray-marches it (parallax occlusion, up to 16 steps at grazing angles, in the box
mapping's frame) so mortar joints and grooves sink; a hex-tiled material takes one offset step instead. The relief fades out between half and all of
`_ParallaxFade` (24 m). It is visual only: collisions stay the boxes.

**Mouldings, frames and props** (`IndustrialDetails`, `IndustrialSigns`). After the rooms are built, the walls are read from the colliders: a
*wall run* is a stretch of static wall with a static floor at its foot and nothing at all (obstacle, trigger, machine) within 0.92 m in front of it,
up to head height. Each run is split into bays by pilasters (base, yellow-and-black caution band, shaft, two-step capital, bolts) and gets a
baseboard, a dado rail and a wainscot panel frame; by the room's seed a bay may hold a framed door, electrical cabinets (conduits, indicator
lamps, an electrical warning sign), a valve station (riser, flanges, hand wheel, gauge), barrels, a pallet of crates or a workbench (vice, toolbox,
pegboard). A run may carry a pipe (flanges, brackets) and a cable tray; tall walls get framed windows, and each room paints its zone number (00–10)
and a direction arrow at its entrance. Cornices run under every ceiling; steel angles with bolts line the open edges of static slabs. Signs are
procedural PNGs in `Textures/Industrial/Signs` (warning, electrical, arrow, information, zone numbers; replace a file to restyle it), drawn as decals,
never in hazard red. Everything is a collider-free `CourseDecoration`, never on anything that moves, and kept 0.85 m from every intended pass
arc. The menu hall gets the same dressing from runs placed by hand between its machines and columns. About 4,300 pieces in the plant.

**What you can download (nothing is required: every surface works now, from the borrowed sets and the procedural layers).** Free CC0 sets from
[ambientCG](https://ambientcg.com) and [Poly Haven](https://polyhaven.com) (I did not check specific asset ids; search these terms):

| Folder | Search for | Maps |
|---|---|---|
| `FloorConcrete` | concrete floor, polished or poured concrete | Color, NormalGL, Roughness, AO |
| `FloorPlate` | diamond plate, tread plate, checker plate | Color, NormalGL, Roughness, Metalness |
| `FloorTile` | tiles, industrial or ceramic floor tiles | Color, NormalGL, Roughness |
| `FloorGrit` | asphalt, rough dirty concrete | Color, NormalGL, Roughness |
| `WallPlaster` | plaster, painted concrete wall | Color, NormalGL, Roughness |
| `WallBrick` | bricks | Color, NormalGL, Roughness, AO |
| `WallPanel` | corrugated or painted sheet metal | Color, NormalGL, Roughness, Metalness |
| `WallBlock` | concrete blocks, cinder blocks | Color, NormalGL, Roughness |
| `CeilingConcrete` | rough concrete, plaster | Color, NormalGL, Roughness |
| `CeilingPanel` | metal panels, ceiling tiles | Color, NormalGL, Roughness |
| `MetalRust` | rust, rusty metal | Color, NormalGL, Roughness, Metalness |
| `Grunge` (optional) | grunge, dirt, streak masks | one greyscale file (white = clean, black = dirt), tileable |
| `Decals` (optional) | decals: stains, cracks, leaks, stencils, arrows, tape | PNG with alpha |

Required per surface: base colour and an **OpenGL** normal; recommended: roughness and displacement (relief); metals: metalness; optional: AO.
Ignore opacity. Square power-of-two, tileable, 1K–2K. File names end in `_BaseColor`, `_Normal`, `_Roughness`, `_Metallic`, `_AO` (ambientCG's `_Color`,
`_NormalGL`, `_Roughness`, `_Metalness`, `_AmbientOcclusion` and Poly Haven's `_diff`, `_nor_gl`, `_rough`, `_metal`, `_ao`, with a trailing `_2k`,
work as they are). `IndustrialTextureImporter` sets the import on a texture's first import (base colour sRGB; normal as a Normal map; roughness,
metallic, AO, displacement and grunge linear; all Repeat, trilinear, mip maps, aniso 8, High Quality, 2048 max; decals sRGB with alpha as transparency, Clamp).
A DirectX normal map gets *Flip Normal Y* automatically. Then run **HotPatata/Course/Assign Industrial Textures**, tune **Tile Size (m)** on the
material (never its Tiling), and rebuild the plant to move the decals. Record every set in `Textures/Industrial/CREDITS.md`.

**Result so far.** The plant reads as a believable dark factory with ambientCG's four sets; borrowed sets give each room its own character until
real ones are added. Render time on the lab matches the KayKit look in the Editor (four views, 1080p: about 1.1–1.5 ms both) with 80% fewer
triangles; the plant's hex-tiled materials sample each map three times (12 samples per pixel on those surfaces) and its ~130 lamps are the main cost:
neither is measured on a player build yet. Limitations: no reflection probe (metals are dull rather than mirror-like), parallax relief has no self-shadowing,
decals have no normal map; the detail pieces add draw calls (SRP Batcher, same shader) still to be measured on a player build. Tests: `IndustrialLookTests`, `IndustrialPlantTests` (closed in 26 directions from every route point, passes clear,
signals one-to-one, every checkpoint resets, transit flights reach their pads, no sun, diverse surfaces and decals). Before/after:
[`images/industrial-lab`](images/industrial-lab), [`images/industrial-plant`](images/industrial-plant). Decision: pending the owner's play-test.

### 25.3 Nature look (PatataWilds, M12)

PatataWilds (§4) is the realistic outdoor look: Poly Haven (CC0) textures, models and skies, generated trees and plants,
and a day that moves from dawn to dusk. Credits: `CREDITS.md` at the repository root.

**Assets** (`tools/Fetch-PolyHaven.ps1` from `tools/nature-assets.json`: incremental, rewrites the Poly Haven block of
`CREDITS.md`; never `.blend`, packed ARM maps or DirectX normals): 27 texture sets in `Assets/Art/Textures/Nature/<Surface>`
(2K for the main ground and rock, 1K elsewhere, JPG), ten scanned models in `Assets/Art/Models/Nature/<id>` (1K FBX) and five
skies in `Assets/Art/Sky/HDRI` (the Qwantani series, one place photographed at dawn, noon, late afternoon, sunset and dusk,
so the horizon never changes). About 300 MB in Git LFS. `NatureAssetImporter` sets them on first import: colour sRGB,
normals as normal maps, roughness/AO/displacement linear, alpha masks as alpha from greyscale with coverage-preserving
mips, skies as glossy-convolved cubemaps, models without materials, cameras, lights or colliders and with Unity's mesh LODs
(`generateMeshLods`), ambient loops compressed mono. `NatureTextureFactory` draws what no download provides: the water's
ripple normal and foam noise, and the fir, pine and grass sprays (Poly Haven's sapling textures are unwrapped geometry,
not cards). `NatureAudioFactory` synthesises the ambience loops (fire, river, waterfall, wind, birds, crickets) as WAVs in
`Assets/Audio/Ambient`; they are only fallbacks: a recording dropped in `Assets/Audio/Ambience/Emitters/<kind>` plays instead
(§26).

**Scanned props for PatataCanopy and PatataTemple.** The same manifest lists the Poly Haven props of the two later
courses (treehouse life: crates, barrels, baskets, a hanging lantern, a ladder, a modular pier; offerings: vases, pots,
a brass lamp; roots and a pachira for the jungle). Their statues, reliefs and offerings come from CC0 museum scans on
Sketchfab: `tools/Fetch-Sketchfab.ps1` reads `tools/sketchfab-assets.json`, refuses any model whose licence is not CC0
(the API token lives in `SKETCHFAB_API_TOKEN`, never in the repository), downloads the glb to a temporary folder and runs
`tools/blender/sketchfab_to_fbx.py` headless (Blender 4.2 or later): the scan's chunks are joined, calibration cards
dropped, the mesh turned upright when the manifest says so, scaled to 1 m with its origin at the bottom centre,
decimated (8k to 40k triangles), unwrapped once, and its relief, colour and roughness are baked from the full scan onto
the reduced mesh (most museum scans are unlit, so colour is baked through an emission of its source). The result is laid
out like a Poly Haven model (`<id>_<res>.fbx`, `textures/<id>_diff|nor_gl|rough_<res>.jpg`), so `NatureAssetImporter`
and `NatureMaterialBuilder.Prop` treat both sources alike. The script rewrites the Sketchfab block of `CREDITS.md`.
A Poly Haven model with several texture sets (the barrels, the pier, the pachira) gets one `Prop` surface per set.

**Shaders:**

- `HotPatata/Nature` (`HotPatataNature.shader`, `HotPatataNatureInput.hlsl`): a fork of the industrial light model with
  triplanar mapping in metres (`_MAPPING_TRIPLANAR`: rough slabs, cliffs, ground; it sticks to moving platforms) or mesh
  UVs in metres (logs, planks, props, foliage), Poly Haven roughness and AO maps, hex tiling on the floor projection
  (`_ANTITILE`), a top layer of moss or grass on faces that look up (`_TOPLAYER`), cut-out two-sided foliage with light
  through the leaves (`_ALPHATEST_ON`, `_Cull`, `_Translucency`), wind sway in every pass so shadows and SSAO follow
  (`_WIND`, driven by the global `_HP_Wind`; weights from vertex colour or height), a dithered distance fade for instanced
  ground cover (`_FadeStart`/`_FadeEnd`), the sky's reflection (`unity_SpecCube0`), and the same `_BaseColor`,
  `_EmissionColor`, `_PatternScroll` and `_Stripe*` that the kit's presentation code drives.
- `HotPatata/Water`: transparent, no depth write; two normal maps scrolling with the flow, the scene seen through it
  (opaque texture) tinted by depth (depth texture), foam in the shallows, the sky's reflection with a Fresnel, a sun glint.
- `HotPatata/SkyBlend`: two HDRI cubemaps blended, each with its exposure and rotation; the horizon melts into the fog.

**Materials** (`NatureMaterialBuilder`, **HotPatata/Nature/Build Nature Materials** and **Assign Nature Textures**,
`Assets/Art/Materials/Nature`): ground (forest floor, trail, rock path, gravel, river bed, grass, leaf litter, mud), rock
and cliff (with a moss or grass top layer), bark and wood, the props' own textures, foliage, and the gameplay variants:
`Nature_Hazard` (rust-stained wood with charred bands, spec §19), `Nature_Falling` (pale rotten planks, always the same),
`Nature_LogDrive`, `Nature_Pipe_1..3` (the tube slot colours), `Nature_LanternGlass` and `Nature_Ember` (emissive).
`IndustrialMaterialBuilder.FindSet(root, folder)` is shared.

**Kit shapes** (`NatureShapes`, drawn by `KitSkin` like the bevel box, no palette). Every displacement is computed from the
position alone, along the inward sum of the faces a vertex lies on, so faces meet without cracks; UVs in metres, tangents.
The rule that keeps rock fair: **nothing rises above a walked-on top, and a side may bulge out of its collider by at most
30 cm, sideways only** (less than a player's half width: the camera never enters rock, a thrown bomb never meets rock it
cannot see by more than a few centimetres); the edges of windows and doorways (the ends of thin walls) stay within 5 cm.

- `RoughBox` (floors, ledges, steps, cliffs): broad low-frequency bulges and strata, edges rounded up to 45 cm, a rim that
  only sinks, the top sinking at most 5 cm.
- `Crag` (buttresses, never walked on): edges rounded up to 1.4 m, domed top, hollowed sides.
- `Boulder` (rocks, stepping stones): a superellipsoid knocked in by noise, flat top and foot, always inside its box.
- `Skirt` (decoration under raised level slabs, `NatureDressing`): hidden under the slab at the top, then widening with
  depth (up to 1.4 m out at 4 m down) and narrowing at its foot: platforms read as outcrops.
- `Logs` and `Planks` as before (inside the box). `CourseKit.NatureLook(role)` maps every `KitRole`; a restyled visual that
  carries its own collider (a cannon's basket) takes planks, never bulging rock.

**Vegetation** (`NatureTreeBuilder`, **HotPatata/Nature/Build Vegetation**): pines and firs (whorls of branches dressed in
needle cards), broadleaf trees (limbs ending in leaf clusters from Poly Haven's shrub atlas), dead snags, bushes, grass and
fern clumps (one frond of the fern atlas per card) and mossy rocks; three variants each, three levels of detail (at most
10k, 2.5k and 600 triangles), wind weights in the vertex colour (red trunk, green branch, blue leaf), saved as mesh assets
in `Assets/Art/Models/Nature/Generated` (and tree prefabs with an LOD group in `Assets/Prefabs/Nature`).

**Dressing** (`NatureDressing`, after the course is built):

- **Terrain:** a heightfield of 6 m cells over the course and 150 m beyond, at the top of the nearest cliffs and rising
  into hills away from them; a vertex inside a section is pulled onto its cliff line and no triangle is kept over a section.
  No collider; mesh assets in `Assets/Art/Nature/Generated` (`NatureTerrain`). The material follows the act.
- **Scatter:** a `FoliageSet` (8 bytes an instance, by 32 m cell) drawn by a `FoliageInstancer` with
  `Graphics.RenderMeshInstanced` per camera (frustum culling per cell, a level of detail per cell, shadows only from near
  trees, never from grass). Forests, bushes and rocks on the terrain (species by act), grass and ferns on walkable floors,
  bushes and small rocks at the foot of the cliffs, all kept off checkpoints, zones, pads, spawns, transits, movers and the
  pass arcs (3D clearance). About 16,000 instances, no scene object each.
- **Scanned models, instanced:** 16 more Poly Haven scans (boulders, rock sets, rock faces, pebbles, shrubs, weeds,
  nettles, moss, dry branches, stumps, a dead trunk, roots). Each mesh of a scan is a variant (`FoliageSet.Batch.meshTransform`
  stands it on its base), drawn at its own reduced Unity mesh LOD (`meshLods`, `RenderParams.forceMeshLod`): pebbles, moss,
  undergrowth and branches on the floors, rocks, moss and deadwood at the foot of the cliffs, boulders, logs and roots in the
  forest, rock faces along the cliff rim (never overhanging a section) and sunk into the cliffs where a cliff stands behind
  (their relief at most 30 cm in front of it). No instance comes within the decoration clearance of a pass arc (checked
  per instance against a 2 m grid of arc samples).
- **Scanned props:** a few Poly Haven boulders, cliff faces, stumps and dead trunks on the terrain near the cliffs
  (`CourseDecoration`).

**Time of day** (`NatureDayLook`, applied by `LookBuilder` after the shared look with each course's table: `PatataWildsLook`,
`PatataCanopyLook`; never set by hand): five `TimeOfDayPreset` assets per course (`Assets/Settings/Look/TimeOfDay`, and its
`Canopy` folder: sun, trilight, haze, mist, light shafts, sky, a grade volume profile, wind), each
sky's photographed sun measured once (`MeasureSun`, a latitude-longitude unwrap) so the sky turns to put it where the
preset's sun is; the scene shows dawn and gets a `TimeOfDayBlender` rig with one global grade volume per preset.
`TimeOfDayBlender` (presentation only) keys the time on `RunManager.CurrentCheckpoint` (decided by the host, mirrored):
dawn at the start, dusk at checkpoint 25, a step forward blended over `GameTuning.timeOfDayBlendSeconds` per act, a step
back or a jump bigger than `timeOfDaySnapActs` (a restart, a practice start, a late join) shown at once; a section reset
keeps the checkpoint, so it never changes the light. It interpolates the sun, the ambient, the fog, the two-cubemap sky,
the grade weights and the wind, and switches the reflected sky at mid-blend. No baking, no realtime probe.

**Height mist** (`HotPatataFog.hlsl`, every HotPatata shader calls `HP_MixFog` in place of `MixFog`): under URP's linear haze,
an exponential mist that pools on a floor and thins with height, integrated in closed form along the view ray. The floor
follows the ground: a height texture over the course (a `MistField` baked by the course builder: PatataWilds' section beds and
terrain, PatataCanopy's forest floor). The field pushes the shader globals (and clears them when disabled, so a scene without
one is drawn exactly as before); `TimeOfDayBlender` pushes each frame the blended moment's colour, density, falloff, glow towards
the sun and light-shaft strength; in the editor the field shows the first moment. Additive particles are dimmed by the mist,
never tinted. Readability: at deck height, 40 m over PatataCanopy's floor, the mist veils a 14.5 m pass by less than 15 %, and
the haze starts beyond 30 m.

**Campfires** (`CampfirePresentation`): read `Checkpoint.Activated` only; catch over `campfireIgniteSeconds` (flames,
an ember burst times 1 - flash reduction, a flickering light also scaled by flash reduction, smoke, a crackle); a checkpoint
already reached is simply burning; a new run puts it out.

**Budgets and limits:** scene file small (the scatter and terrain are assets), 60 fps at 1080p with four players is a human
gate (M12.8). Known limits: the 108 m cave is lit by the sun beyond the 75 m shadow distance at its far end (torches carry
it); the water has no planar reflection; the trees are cards, not scans; the synthesised ambience is a placeholder for
recordings. Tests: `NatureLookTests`, `TimeOfDayTests`, `PatataWildsTests`. Views at dawn (as the editor shows the scene):
[`images/patata-wilds`](images/patata-wilds). Decision: pending the owner's play-test.

**PatataCanopy (M13, atmosphere M13.7)** uses the same nature look with its own, cooler day (`PatataCanopyLook`: a
green-teal haze and mist, a softer sun, a paler sky, `_Saturation` on its `HotPatata/SkyBlend` copy): decks are planks,
cross walls upright log walls (`Nature_LogWall`), leaf roofs a mat of leaves seen from below that casts no shadow, the oak
bark; the gameplay cues (brambles, lasers, plates, spores, rings) keep their own materials. The sun carries a tiling leaf
cookie (`LeafDapple.png`, 26 m, the deepest shade at 72 % light; `_LIGHT_COOKIES` in `HotPatata/Nature` and
`HotPatata/Stylized`), swayed by `LeafDapple` with the wind. Its forest is one instanced `FoliageSet` over a terrain that
follows the course (§4). Light shafts (`HotPatata/LightShaft`): a unit quad laid along the sun's direction bent towards the
vertical (`_Vertical`) and turned to the camera, additive, faded near the camera, against geometry and far away, strength =
the moment's `shaftStrength`. Known limits: the giants are generated cards and tubes, not scans; the shafts and cookie are
not real volumetric light; 60 fps at 1080p with four players is to measure on a player build (M13.7).

### 25.4 Temple look (PatataTemple, M15)

The nature look (§25.3) in stone: paved floors (`Nature_TempleFloor`, Poly Haven grassy cobblestone), masonry walls, divides,
lintels, columns and the pyramid (`Nature_TempleWall`, castle wall, moss and grass on the faces that look up), mossy brick roofs
(`Nature_TempleRoof`) and stone slabs for the movers (`Nature_TempleSlab`), all carved boxes (`KitShape.BevelBox`), never rough
rock. Gameplay cues keep their materials; the four systems for three have their own, kept in every look: the heavy slab is
terracotta with two footprints and the "two bodies" glyph, the hourglass plate the hands-free blue with a ring of amber sand
lamps, the sun beam a gold shaft and a stone eye, the pivot a stone slab on a drum.

Its day (`PatataTempleLook`, through `NatureDayLook`, presets in `Assets/Settings/Look/TimeOfDay/Temple`): a misty golden dawn,
a morning, a white noon, a heavy afternoon and a **tropical storm** (Poly Haven pure skies: Kloofendal misty morning, 28d and
48d, Kloppenheim 06 and 01), the mist in the valley thick at dawn and in the storm. **The storm** (`TempleStorm`, presentation
only) grows with `TimeOfDayBlender.Current` from the heavy afternoon: rain round the camera whose drops die on any solid (never
under a roof) and lightning, a cold directional light whose flash rises and falls over 0.6 s (`TempleStorm.Pulse`, never a
step), scaled by `viewEffectsStrength` and `1 - flashReduction` (none at full reduction). No wind bends a flight.

Known limits: the canopy is the generated broadleaf at 1.2–2.3x, not tropical species; the pyramid is one stepped mesh without
stairs or carvings; the rain is particles, not a screen effect. Tests: `PatataTempleTests`, `TrioSystemTests` (the flash).

## 26. Audio (M16)

Every sound is a file dropped in a folder of `Assets/Audio`; no Inspector wiring. A folder holding sound files is a
**cue**: its key is its path under `Assets/Audio` (`SFX/Player/Footsteps/Grass`), every file in it is a variation picked
at random, never the same twice in a row, with the cue's volume and pitch jitter. An empty or missing folder is silent
(the bomb then plays its `ProceduralSfx` placeholder; the builders' emitters their synthesised loop). The drop guide for
people adding sounds is `Assets/Audio/README.md`.

**Pipeline (editor).**

- `AudioImportRules` sets a new file's import settings by folder, once (a change by hand is kept): music streamed,
  ambience loops compressed in memory, short effects decompressed on load; sounds placed in the world forced to mono.
- `SoundBankBuilder` rebuilds `Assets/Audio/Resources/HotPatataSoundBank.asset` a moment after any sound file under
  `Assets/Audio` is added, moved or removed (menu `HotPatata/Audio/Rebuild Sound Bank` forces it). A cue's settings
  (`CueSettings`: volume, jitter, cooldown, voices, distances, scatter timing) start from `CueDefaults` and are kept from
  the bank's Inspector afterwards. Music files `X_intro` + `X_loop` make one track. It also maps every material to a
  footstep surface from its name (kept once set, so a fix by hand stays).
- `AudioFolderSkeleton` (menu `HotPatata/Audio/Create Folder Skeleton`) creates every folder the code listens to, from
  `CueKeys`, the level list (one folder per act) and every `IObstacleState` type.

**Runtime.** `AudioDirector` starts itself before the first scene when the bank exists (never in batch mode), survives
scene loads and holds the components below, all presentation (they read state, never change it, and work the same on
clients since they read mirrored state). One-shots and loops share a pool of `GameTuning.audioVoices` sources, each on its
cue's group (`SoundBank.GroupFor`, §6.1); time is unscaled. Code plays a cue by its `CueKeys` key through `Sfx.Play` /
`Sfx.Loop`, or `Sfx.Pick` for a component that keeps its own source (the bomb).

| Component | Folders | Rule |
|---|---|---|
| `MusicDirector` | `Music/Menu`, `Music/Lobby`, `Music/Courses/<scene>[/Act<n>]`, `Music/Results` | `MusicContext` picks the place: menu; lobby (else menu); silence while a level loads (Netcode's load event fades the lobby out); the act's folder, else the course's; results once the run is complete. Two decks crossfade (`musicCrossfadeSeconds`); several tracks are a shuffled playlist; an intro hands over to its loop sample-exact (`PlayScheduled`). Each beep dips the music (`musicBeepDuck`). |
| `AmbienceDirector` | `Ambience/Menu`, `Ambience/Courses/<scene>[/Act<n>[/Replace]]`, `Ambience/Environments/<env>` | Layers of `Beds` (loops, all layered, flat) and `Scatter` (one-shots at random spots around the listener, `CueSettings.scatter*`). A course plays its own layer, its environment (the section's from `CourseAudio`, or the `AmbienceZone` the listener is in) and its act's layer; an act's `Replace` takes the environment's place. Layers fade over `ambienceCrossfadeSeconds`. |
| `CourseSounds` | `SFX/Course/{Start,Checkpoint,ActChange,Reset,Finish}` | Watches the run's state and checkpoint (host-decided, mirrored); what it sees first is never played. |
| `ObstacleSounds` | `SFX/Obstacles/<type>/{On,Off,Loop}` | Every `IObstacleState` of the scene (but checkpoints and tubes), by component type: On / Off when `Active` flips, Loop while `Progress` moves, within `obstacleAudibleDistance`. |
| `UiSounds` | `SFX/UI/*` | `UIScreen.Navigable` attaches hover, click and change sounds; `ScreenStack` plays Open and Back; lobby join / leave from the lobby's player count. |
| `PlayerSounds` (added to every player) | `SFX/Player/{Footsteps/<surface>,Jump,Land/{Soft,Hard},Slide/{Start,Loop}}` | A footstep every `stepLength` metres on the ground; the surface is an `AudioSurface` on the collider, else the material's (bank), else `Default`. Hard landing from `hardLandingSpeed`; none under `landingMinAirSeconds`. Your own are flat at `ownMovementVolume`; teammates' are 3D. Remote players are judged from their mirrored motion. |
| `BombAudio`, `TransitPresentation` | `SFX/Bomb/*` | §8.5. |
| `AmbientLoop`, `CampfirePresentation` | `Ambience/Emitters/{River,Waterfall,Fire}` | Builder-placed loops play their emitter folder, else the synthesised fallback. |
| `TempleStorm` | `Ambience/Thunder` | A thunder `thunderDelay` seconds after each flash, at the storm's strength, flat; whatever the flash reduction (audio only). Else `ProceduralSfx.Thunder` (`AudioDirector.PlayFallback`). |

**Placeholders.** The nature builders seed each act's `Beds` with the synthesised loops of `NatureAudioFactory`
(`SeedActBeds`: birds by day, crickets and wind at dusk) as `placeholder_*` files; `SoundBankBuilder` drops a folder's
placeholders as soon as it holds any other sound, so a dropped recording replaces them with nothing to delete.

**Course map.** Each course builder writes a `CourseAudio` on the course root (`CourseKit.WriteCourseAudio`): per
checkpoint id, the act and the environment of the section run from it (PatataWilds by theme: Forest, River, Cliffs, Cave,
Summit; PatataCanopy: Canopy, HollowOak in act 3; PatataTemple by act: Jungle, Temple, Storm; the plant: Factory). A scene
without one is a single act with no environment.

**Mix.** Beeps have the highest priority and their own group; the pause menu's `Paused` snapshot low-passes music,
ambience, movement and world but never the bomb or the menus. Tests: `AudioTests`, `SettingsTests`.

## 27. Authored levels (M18)

The courses above are generated by code. An **authored level** is the other kind: a scene in `Assets/Scenes/Levels/` that
the owner builds by hand in the Unity Editor (Scene view, Inspector, the `HotPatata/Level` menus), with no builder. The
how-to is [`LEVEL_AUTHORING.md`](LEVEL_AUTHORING.md). It is built in three steps (M18): the level and its timber (done),
the nature (terrain, giant trees, forest, mist, sound; done), then the game (3a: the kit's handles and wiring, contracts,
checkpoints, the checks; done) and the look (3b: the kit's forest looks, props, the guide's example level; done).

**Data, then geometry.** A piece of a level is a component holding only data (`Assets/Scripts/Authoring`, runtime
assembly, read by no gameplay code): `TimberDeckShape` (an outline, a height, a slope, rails, openings, the trunk it hugs),
`TimberWalkShape` (points; steps where it climbs), `RopeBridgeShape` (two ends, a sag), `RailingShape`, `HouseShape`
(plank walls that collide, doors and windows, a pitched roof over a `CourseCeiling`) and `TrestleShape`; each with a
support choice (`TimberSupport`: the kit's own, or posts, a tower, braces, hangers, none) and a wear (`TimberWear`: new or
aged; never rotten, that look is the falling planks', PROJECT_SPEC §19). The editor (`Assets/EditorTools/Authoring`) draws
each piece with the timber kit (`TimberKit`, `TimberBuildings`, the same code as PatataRanger) into a child it owns
(`LevelPieceOutput`, never edited by hand). Points are stored in the piece's space, so moving or turning a piece carries
its shape.

**Where a piece stands.** `SceneSite` gives the kit its site from the scene: the ground is the highest Environment collider
under the piece that is not timber (a post stands on the terrain or a rock, never on another deck), the trunks and anchors
come from the scene's `ITrunkSource`s (the giant trees, M18 step 2).

**Ends that meet.** A walkway's or a bridge's end dragged onto a deck's edge or onto another walkway's or bridge's end (drawn
within `PieceHandles.SnapPixels` of it on screen; `TimberLinks.SnapDistance` in the world for code) attaches to it
(`TimberAttach`: the deck and how far round its outline, or the joined piece and which end): the end follows what it is
attached to, and a deck's railing opens there (its width plus `OpeningMargin`). A joined end reads the other piece's own end,
never its joins, so two ends joined both ways never loop. Shift places an end freely. A
deck's own openings slide along its edge. A ring round a trunk keeps a seam (its outline doubles back on itself); no rail
runs along a seam (`LevelPieceBuilder.SeamSkip`).

**Rebuilds.** `LevelPieceWatcher` hears every edit (`ObjectChangeEvents`, undo and redo) and, once no handle is held,
rebuilds the pieces whose inputs changed: `LevelPieceBuilder.Signature` is everything a piece is built from (its name, which
seeds the kit's randomness, its data, its place, its ends' decks, the openings cut in it), stored on its output. A
duplicated piece gets its own id. The ground is not in the signature: after reshaping the ground, Rebuild All Pieces.

**Meshes.** While a level is edited, the pieces' meshes live in memory (`OrganicAssets.BeginInMemory`); on save,
`LevelMeshStore` writes them into the mesh catalogue (`MeshCatalog`, §27.1: found again by their content, shared with the frozen
timber) and the forests' plantings into `Assets/Art/Generated/<Level>/Foliage/<piece id>.asset` (Git LFS), deleting the
plantings of pieces no longer in the scene. A level folder holds the level's own data only (its terrain, plantings, mist); the
pieces' old per-piece folders (`Pieces/`) are emptied into the catalogue the next time the level is saved.

**On save** the level also gathers its walk lines (every walkway and bridge, on its walking surface) into its
`RouteWalks` (`LevelRoutes`), and the quick checks (`LevelQuickCheck`: a piece not built, a hanger with nothing to tie to, a
scaled piece) are logged.

**The level.** `HotPatata/Level/New Level` (`LevelScenes.Create`) copies the courses' template (`CourseKit.PrepareCourseScene`:
the run, spawns, bomb, camera and networking), adds a `Level` group under the section root with its `LevelSettings` (the
name the menu shows, the players it needs, the day it borrows, the groups new pieces go in), a flat Unity terrain under the
start (`Assets/Art/Generated/<Level>/Terrain/`), a safety net (`LevelSafetyNet`), the forest brushes, the mist and its day. `LookBuilder` lights a scene with
`LevelSettings` with the chosen nature course's day (`LookBuilder.ApplyDay`: the same presets and sky, the level's own
`TimeOfDayBlender`, dawn at the start, the last moment at the last checkpoint), and `Apply Look To All Scenes` covers
`Assets/Scenes/Levels`. `HotPatata/Level/Register In Menu` writes the whole level list at once (`CourseKit.ReadMenu`,
`WriteMenu`): the level dragged where it goes, its name and players; the other levels keep their order. The menu shows a
level's display name (`NetworkBootstrap.DisplayNameOf`, the scene's name when it has none).

**Presets.** A `TimberPreset` keeps a piece's settings, never its shape (outline, points, ends, openings): applied from a
piece's inspector, or dragged from the Project window into the Scene view to make a new piece there.

**The nature (M18.2).**

- **Terrain.** A level's ground is a Unity Terrain sculpted with Unity's own brushes and drawn with the nature material
  `Nature_ValleyGround` (`LevelTerrain.ApplyLook`: HotPatata/Nature, triplanar rock with its top layer of leaf litter on what
  looks up, fogged through `HP_MixFog`; no instancing, no base map, so one shader path). No terrain shader of its own was
  needed: the nature shader's triplanar mapping and top layer read the world position and normal a terrain gives. Its
  `TerrainCollider` is the ground (on Environment) the posts and the forest stand on. `RaiseBorder` lifts its edge into
  valley walls (smoothstep and noise, never lowering).
- **Water.** `WaterShape`: the nature water surface (`NatureKit` plane and material) turned with the piece, and a
  `KillZone` from `KillBelowWater` under it down to its depth (PatataWilds' rule: no water without a kill zone).
- **Giant trees.** `HeroTreeShape` holds a hero tree's spec (kind, height, radii, lean, crown, hand-placed limbs); the editor
  grows it (`NatureTreeBuilder.BuildHero`, `PlaceHero`: three levels of detail, capsules on the trunk and the big limbs on
  Environment) and records its measured axis, radii and limb anchors, so the tree is the scene's `ITrunkSource` without
  growing again. Its sawn bands are not set by hand (`LevelNature.SawnBands`): every deck that names it or comes within
  `WrapReach` of its trunk saws the crown's random limbs off from `SawBelow` under to `SawAbove` over its surface.
  `LevelPieceBuilder.Refresh` rebuilds trees first, then the timber, then the forests; a deck's signature holds the trunks
  near it and a tree's holds its sawn bands, so raising a deck re-saws its tree and a moved tree rebuilds the decks on it.
- **Forest.** `ForestArea` (an outline and densities) is planted by `LevelForest.Plant` the way PatataRanger's forest is
  (giants in groves by a slow noise, understory, ground cover; the same `FoliageSet` batches), drawn by a
  `FoliageInstancer` and never colliding. It keeps off the level's paths (walkways, bridges, deck and house outlines, by
  its path clearance), water, solids (`HitsTheLevel`), pass arcs (`NatureDressing.TouchesAnArc`) and giant trees. Its
  signature holds the signatures of the pieces near it (`ForestReach`): a deck moved into it replants it. Its planting is an
  in-memory `FoliageSet` until the save writes it as `Foliage.asset` in its piece folder. Trees and ferns can also be
  painted with the terrain's own brushes (`SetupBrushes`: the `Tree_*` prefabs and generated detail prefabs in
  `Assets/Prefabs/Nature/Details`, all decoration); `ClearPaths` takes off what was painted on a path, a solid, water or a
  pass arc.
- **On save** (`LevelTerrain.OnSave`): the safety net refitted under the terrain, the mist baked (`BakeMist`: the terrain
  less the level's mist depth, raised to the top of each `MistZone` inside it, into `MistFloor.asset` for the level's
  `MistField`, whose density the level scales with `MistField.densityScale`), and the sections' ambience written on the
  section root's `CourseAudio`. Sound sources are `NatureKit.Ambient` loops whose emitter is picked from the sound
  library's folders; ambience zones are `AmbienceZone`s.

**The game (M18.3a).**

- **Course.** `LevelCourse`: a checkpoint is the kit's checkpoint with its campfire as its child (`NatureKit.CampfireCheckpoint`,
  its position local), the finish the kit's `FinishZone`. The fire goes `FireDistance` (3.4 m) beside the spawns, on the first
  side (right, left, ahead, behind, the diagonals) with floor at the checkpoint's height under it and round it and nothing in
  the way, the kit's trigger volumes included (`FireSpot`); the checkpoint's zone is fitted from the prefab's own round the fire
  (`NatureKit.FitCampfireZone`), when its handle moves it (`CheckpointEditor`) and on every save (`FitZones`). `Number` gives the checkpoints ids 1..n in walking order: `WalkDistances` is a shortest path
  from the start over a graph of the level's walk lines (consecutive points), deck outlines and a ground grid every
  `GroundStep` metres, any two points within `Stride` on the flat and `Climb` in height joined; a checkpoint no walk reaches
  comes after, by distance. A contract (`SectionContract`) belongs to the section of the checkpoint it sits under (`SectionOf`),
  the start's sits under the level.
- **Kit from the menu.** `KitMenu` (GameObject > HotPatata Level > Kit): every kit prefab placed on the floor the Scene view
  looks at (`LevelNature.ViewFloor`: the first upward face on Environment along the view's centre that is the terrain, a walking
  surface or a kit object, never a rail's top; else the ground under the pivot), its solid colliders' bottom (else its
  renderers') on it, what hangs raised, in the Kit group; a sliding door is the kit's door with its open position moved aside
  by its width. Checkpoints and the finish are placed the same way.
- **Kit in the Scene view.** `KitSceneTools` (authored levels only): size handles on the selected kit object through the
  kit's resize functions (`BombObstacleKitBuilder.ResizeActuator`, `ResizeZone`, `ResizeCurtain`, `ResizeScreen`,
  `ResizeSunBeam`, `ResizePlate`), the size read back from its collider; an actuator's open position as a ghost moved with a
  handle (its travel); the wiring drawn source to actuator (a switch to its targets) with `Describe` (what drives it, how
  long it holds); the Link tool (`SignalActuatorEditor`): a click picks an actuator's `source` or adds a switch's target. One
  source per actuator, as the kit has it. Hinged actuators (drawbridge, pivot) keep their inspector.
- **Contracts in the Scene view.** `SectionContractEditor`: each lock drawn by kind, its points dragged onto the level, locks
  added from the inspector, the contract checked by `CourseContractCheck` on demand.
- **Verify Level.** `LevelVerifier.Run` lists `LevelFinding`s (error, warning, info, each with its object or place) in
  `LevelCheckWindow`: the course (start, ids 1..n, campfires 3 to 6 m from their checkpoint on its floor, every spawn on a
  floor with room for a body (`Spawns`), one finish, order), the contracts (one per section, the check's
  findings mapped back to their objects), the walks (`WalkProbe`, shared with PatataRanger's build), the passes, the falls
  (safety net, water over a kill zone), the rules (kit under the section root, pieces built, plants off the paths), the menu
  (listed with its checkpoints and players) and the budget (`TriangleBudget`, `PlantBudget`, `MeshBudgetMb`).
- **Play.** The route runner is shared (`RouteRunner`, HotPatata.DebugTools): `AuthoredLevelTests` (one
  fixture per level of `Assets/Scenes/Levels`) and `HotPatata/Level/Run Route Runner` (a `RouteRunSession` in Play) drive
  player 1 along every `RouteWalks` line. Play From Here and Run Route Runner leave a request in the editor's session state;
  `EditorPlayRequests` reads it before the first scene loads (`RunOptions.StartCheckpoint`, else 0).

Tests: `LevelAuthoringTests`, `LevelNatureTests`, `LevelGameTests`; `AuthoredLevelTests` (PlayMode).

**The look (M18.3b).**

- **Forest looks.** `ForestKitBuilder` (menu `HotPatata/Kit/Build Forest Looks`) makes ten Prefab Variants of kit prefabs in
  `Assets/Prefabs/Variants/Forest/` (winch, lever, drawbridge, cage, signal ring, bell arch, chute, net, embers, sparks;
  docs/OBSTACLES.md §4.1). Each keeps its base's system, colliders and gameplay cues; the kit's construction (KayKit rims,
  posts, lintels, boxes) is hidden (an object holding only renderers switched off, a collider's renderer disabled) and drawn
  again with the shared timber shapes (`TimberStructures`: logs, plank rims and decks, the winch drum; meshes in the catalogue,
  §27.1) under a `CustomVisual`, on the moving part where the
  part moves. The chute only changes its pipe's materials (a tube lays its pipe per level). Their particles use their own
  materials (`Nature_EmberZone`, `Nature_SparkCurtain`). `KitSceneTools`' size handles scale a variant's `CustomVisual` by the
  change of size (a deck's thickness never stretches its posts).
- **Props.** `PropLibraryBuilder` (menu `HotPatata/Level/Build Prop Library`) makes a prefab per CC0 model of
  `Assets/Art/Models/Nature/` at its real size (scans brought to a unit height are set to their real height), on its base,
  drawn with the nature library's material (`MaterialFor`), in `Assets/Prefabs/Props/<kind>/`, plus three sets of timber and
  props (`Sets/`; their timber is made of elements, §27.1). `PropSolid` (runtime, read by no gameplay code) holds a
  prop's choice: decoration, or a box on Environment round what it draws (`PropSolidEditor.SetSolid`). `PropBrushWindow`
  paints small props from the library on the ground and the decks, off the level's paths, into `Level/Props`; its eraser takes
  only the brushes' prefabs.
- **Rings.** A whole ring's outline closes on a seam; `TimberDeckShape.NearestAlong` never joins a walkway there
  (`IsSeam`), so its end leaves the outer edge.
- **The example.** "The Old Oak" (`OldOak`): made with the tools, the guide's model (LEVEL_AUTHORING §16); archived at the fresh
  start (`MAP_ARCHIVE.md` §6.6).

Tests: `LevelLookTests` (the variants and their visuals, the props, Solid), `ObstaclePrefabTests` (the variants keep the
obstacle contract), `AuthoredLevelTests` on every level.

### 27.1 Frozen timber and the structure library

A piece of timber is parametric: it is redrawn from its data, so a single board cannot be moved by hand. **Freezing** turns
it into its elements, one GameObject each, moved by hand from then on (one way: Ctrl+Z only).

- **Elements** (`Assets/Scripts/Structures`, runtime assembly, read by no gameplay code): `TimberBoard` (a sawn piece: size
  across, thick and long, ends cut on a slant; a board, a post, a beam, a brace, an iron strap), `TimberPath` (along a line:
  round, a rope or a log; square, a rim board, a stringer, a rail; a two-point path sags; each end may hang on an object and
  follows it), `TimberTurned` (a pulley, a drum) and `TimberWall` (an invisible wall along a line: a rail's, a bridge side's).
  Each element says what it is (`TimberElementKind`), its wood (`TimberWood`: the kit's material slots), a grain variant,
  whether it is solid (a box for a board, its own mesh for the others, on Environment) and its walking role (`TimberWalk`).
- **Recording.** `TimberKit` and `TimberBuildings` draw through `TimberKit.Visual()`; while an `ElementRecorder` listens, each
  box, sweep, tube and lathe they lay is written down with the tags the kit gives it (group, kind, walking role, run and
  order, solid). `TimberFreezer` draws the piece once more under the recorder and makes one element per record under a
  `FrozenStructure` (groups: boards, frame, supports, each rail...): the structure looks exactly like the piece. Colliders the
  elements stand for (a wall's boxes, now its boards'; the rail walls, now `TimberWall`s) are dropped; others (a house's
  `CourseCeiling`) are kept. Walkways and bridges attached to a frozen piece keep their ends where they were, as free points.
- **The catalogue.** `MeshCatalog` is the one store of the meshes the level tools make (the elements, the pieces' drawn timber
  and surfaces, giant trees, water, the kit's forest looks), in `Assets/Art/Generated/Catalog/`: one mesh per shape, named after
  its shape's key or its content's hash, shared by every level and prefab. Small meshes are sub-assets of sixteen `MeshBank`
  files, large ones (over `LargeVertices`) files of their own in `Meshes/` (a bank is rewritten whole when it changes). A board
  is drawn long to the centimetre (`TimberBoard.DrawnSize`; its collider keeps the exact size) and only boards keep a grain variant
  of their own (`DrawnGrain`, four), so boards of a deck share meshes. Meshes are written as they are made (a prefab may hold
  them) and removed once no saved scene, prefab or generated asset, and no open object, uses them: on every scene or prefab save
  (`MeshCatalog.CleanSoon`; each file is read again only when it changed), or by `HotPatata/Level/Clean Mesh Catalog`.
- **Where the code is.** The timber code is one folder, `Assets/EditorTools/Timber` (the kit, the buildings and structures, the
  elements, freezing, the catalogue); the authored levels' pieces, handles and checks stay in `Assets/EditorTools/Authoring`.
- **The walking surface.** A frozen structure's boards have no collider of their own: `WalkSurfaceBuilder` lays one closed
  collider (a `WalkableProxy`) from its walkable boards, so a walker never catches a board's edge. A slab board gets a slab a
  little wider than itself (the gaps close); the boards of a run (a walkway's boards and treads, a bridge's slats) get one
  ramp swept through the middles of their tops, broken where a board is missing (a gap over `RunBreak` times the spacing round
  it). A stair stays a smooth ramp; a board taken away leaves a real hole. The runs also give the structure's walk line
  (`FrozenStructure.WalkLine`), gathered with the pieces' (route runner, walk probe, walking distances).
- **Keeping in step.** `TimberElementWatcher` redraws what changed once the mouse lets go (an element whose shape key
  changed, a rope whose anchor moved, a surface whose boards moved).
- **Checks.** Verify Level reports ropes that hang on nothing and posts that stand on nothing, and counts the elements'
  triangles in the budget.
- **Structures.** `TimberStructures` draws a shelter, a gantry, a sign (its glyph painted: `TimberPaint`) and a pulley beam
  from a place, a turn and settings only (no level or course constant), with the timber kit; what a body must not pass (posts,
  legs, low walls) collides as boxes the frozen elements replace. The level pieces `ShelterShape`, `GantryShape`, `SignShape`
  and `PulleyBeamShape` hold those settings, like the other pieces.
- **The library.** A frozen structure saved with *Save to library* is a prefab in `Assets/Prefabs/Structures/<kind>/`: it
  belongs to the author (edit it in Prefab Mode, make variants), nothing regenerates it. `StructureLibraryBuilder` (menu
  `HotPatata/Level/Build Structure Library`) makes the first ones from the structure pieces, only those missing; one is drawn
  again only when asked (Assets > HotPatata > Regenerate Library Structure). The prop library's sets are made of elements too. It is scenery and level geometry
  only: a gameplay prefab of the kit is placed beside or in it in the level, never built into it.

- **Handing a course over.** `ElementRecorder.FreezeInPlace` makes the kit draw every piece of timber frozen, in place: elements
  under the piece's root (a `FrozenStructure` of its own, its surface laid from its boards), walls as `TimberWall`s, the boxes they
  stand for never made. PatataRanger was handed over this way (M18.4c, `RangerConversion`, run once): its builder drew it one
  last time frozen, the scene got its `LevelSettings` (menu name, two players, the Ranger day, each section's music act and
  ambience: `LevelSettings.SectionActs`), and the builder was removed. Its land, giant trees, forest and mist stayed as they were
  baked (meshes in `Assets/Art/Generated/PatataRanger/`); the scene was the source from then on, until it was archived at the fresh
  start (`MAP_ARCHIVE.md` §6.5).

Tests: `TimberElementTests` (a frozen deck, stair, bridge and house look and walk like their pieces, holes, attached ends,
ropes on anchors, the catalogue, solid elements, the four structures built and frozen with solid posts and painted glyphs).
