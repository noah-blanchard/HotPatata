# HotPatata — TESTING.md

> How to test HotPatata: the automated suites, the dev tools, multiplayer bot sessions, latency simulation, and CI.
> There are no build or lint scripts: everything runs through the Unity Editor, a Development build, or GitHub Actions.

---

## 1. Automated tests (Unity Test Framework)

| Suite | Mode | Covers |
|---|---|---|
| `ConfigurationTests` | EditMode | M0 baseline: tuning defaults, layers, collision matrix, player colours/shapes and their indicator meshes and flat ring glyphs; every slot's character is Humanoid on the shared controller with a right-hand socket; the throw hold point is where the hand is back |
| `BombFuseTests` | EditMode | fuse maths: refresh, expiry, warning phase, stage bands |
| `AimAssistTests` | EditMode | release-time aim assist and arc maths (spec §8.3) |
| `NetcodeFlightTests` | EditMode | #117 flight segments (the same arc at the same server time everywhere), the back-date clamp, a past chord carried into a mover's present frame, the stamp history the host checks claims against (interpolation, never across a teleport), rolling percentiles, the view delay, lost and recovered stamps, the simulator presets |
| `MovingPlatformTests` | EditMode | `MovingPlatform.Evaluate`, the pure clock-to-position maths |
| `SimulationClockTests` | EditMode | #92 the client clock: in step it follows the frame, NGO corrections are slewed (never backwards, at most `clockMaxSlew`), a real desync snaps; offline it is the local clock |
| `CarrierRegistryTests` | EditMode | #92 carrier ids: stable FNV-1a hash, follows the sibling path not the name, unique in PassSandbox (add a level's scene to its list) |
| `CarrierMotionTests` | EditMode | #97 the velocity of a point on a sliding or turning carrier, what a player leaving one keeps (all of it, capped), the aim assist solving a pass between two riders of one lift as on still ground |
| `RiderReconstructionTests` | EditMode | #92 a remote rider glued to the carrier as drawn locally whatever the lagging world position, board and leave blends without a pop, the offset smoothed, a teleport snaps |
| `HazardRewindTests` | EditMode | #92 the host's rewound hazard verdict: capsule vs (rotated) box, a box carried with its mover to another time, the verdict at the claimed time and not now, the claim cap, a door's edge armed only while closing, a moving platform posed at any time by the formula it moves by |
| `SessionServiceTests` | EditMode | M4: code cleanup and player-facing error wording |
| `SettingsTests` | EditMode | M6.5: player settings layer (tuning defaults, JSON parsing, clamping, fallback, mixer decibels; M16's music, ambience and menu volumes) |
| `AudioTests` | EditMode | M16: the sound folders' rules (a variation never repeats, jitter, a cue's mixer group by folder, the music's place and its act → course fallback, folder keys, intro/loop pairs, a material's footstep surface, the skeleton's keys, cue defaults) |
| `InputRebindingTests` | EditMode | #18: rebindable bindings per device, conflicts, saved overrides reaching a player's copy, reset |
| `CursorPolicyTests` | EditMode | #14: the cursor is locked only while gameplay wants it and no screen needs the mouse |
| `PlayerNamesTests` | EditMode | M6: player-name cleanup (trim, length, control characters, "Player N" fallback) |
| `DisplayOptionsTests` | EditMode | #17: settings display choices (window modes, resolutions without duplicates, frame caps) |
| `MenuFlowTests` | EditMode | #79: in-world menu navigation (Title, Play, Level, Lobby; Back; the session flow wins: lobby shows the Lobby, a failed or left session lands on Play) and the camera blend (eased, a cut at camera effects 0) |
| `CodeDialsTests` | EditMode | #79: game code dials (turn and wrap, typing moves on, Backspace, pasted codes normalised) |
| `FontAssetTests` | EditMode | UI fonts use bundled sources, font/material/atlas dependencies are buildable, fallback chains are valid and code-dial arrow glyphs exist |
| `PauseAndSettingsTests` | PlayMode | #16/#17: pause freezes offline, takes the input and frees the cursor; a carrier keeps the bomb and loses the charge; Esc never closes and reopens on one frame; Leave asks first; settings preview live, save on close, reset, never write `GameTuning` |
| `MenuStationTests` | PlayMode | #79: the backdrop's four stations build their boards; only the shown board takes input and the pointer; Submit flies to the next station and Back flies back; a cut at camera effects 0, a glide otherwise; the Level board's purpose; Settings over the scene pauses the board and gives its focus back; gamepad code entry; the station's framing (vignette) follows the camera effects (0 = none); lobby guests step onto the stage with nameplates and the show resumes |
| `ScreenStackTests` | PlayMode | #14 UI Toolkit base: themed panel and navigation module, stack/focus/back, navigation and submit events, cursor hand-over; #76 every screen layout in the catalog, a missing UXML element fails naming the screen and the element, up / down follow the layout order; the entrance pops a pushed screen in and cascades its buttons, then settles without moving the focus |
| `BombStateTests` | PlayMode | bomb state machine plus catch / world-contact / fuse rules (M1.4–M1.8) |
| `RunAndThrowTests` | PlayMode | throw by input (M1.5) and fast, clean section reset (M1.10) |
| `MovementTests` | PlayMode | accel/brake, jump, coyote time, jump buffer (M1.2) |
| `SprintSlideTests` | PlayMode | M9 movement: sprint, carving, slide, slide-jump, crouch, mantle |
| `ViewFeelTests` | PlayMode | first-person speed feel, post effects, accessibility scaling |
| `PlayerCharacterTests` | PlayMode | each slot shows its character and its ring (colour + shape); a slot change swaps the character and the animator follows; the first-person player sees only the shadow of theirs; in third person the potato rides the right palm; a held throw waits with the arm cocked and throws on release |
| `PassFeelTests` | PlayMode | **pass-feel regression suite**: catches at 4/8/12 m moving and jumping, the assist never creates range |
| `PrefabKitTests` | PlayMode | M2 greybox kit (moving/falling platforms, rotating bar, kill zone, checkpoint, finish) |
| `ObstacleKitTests` | PlayMode | the obstacle systems as reusable pieces (docs/OBSTACLES.md), each kit prefab alone on a test floor beside PassSandbox: belts carry both ways, elevators and pistons lift, a sweeper hits a standing player and can be jumped, a crusher catches a standing player and spares a crouched one; and the logic ignores the look and the names: the boulder variant moves and carries, the drawbridge variant stands up and lies across while its plate is held, a subclass overrides the path, and a visual driver follows the state |
| `ObstaclePrefabTests` | EditMode | the obstacle prefab contract (docs/OBSTACLES.md §4): every obstacle prefab of the project (kit and variants) passes `ObstaclePrefabValidator`, the validator catches each break (moving part is the root, missing waypoint, collider in a hand-made visual, mesh collider, non-gameplay layer, no kinematic body, hazard without kill trigger, replicated system without its network companion), the example variants are Prefab Variants of the kit, the look passes leave hand-made visuals alone |
| `ZoneRuleTests` | EditMode | #68 pure rules: fuse-zone severity, flight-sweep geometry, actuator motion, transit exit arc; a hands-free plate is not held by its carrier alone; a switch turns its targets over at half travel |
| `BombObstacleTests` | PlayMode | #68 kit on the PassSandbox floor: fuse zones, curtains and windows, gates, plates, doors, arch checkpoint, tubes; a body screen stops a runner (jumping into it too) and lets the pass through; a hands-free plate ignores its carrier; a plate's switch cuts a curtain and the curtain comes back |
| `CourseContractTests` | EditMode | section contracts (PROJECT_SPEC §13.20): the reach envelopes (climb = jump + mantle, gaps shrink uphill and end out of reach, a lob clears any roofless wall), the scan finds a body screen walked round until walls close its ends and a curtain flown round and over, then every level of `Assets/Scenes/Levels` checked; findings are warnings (logged, the test passes) |
| `KitSkinTests` | EditMode | KayKit kit (ARCHITECTURE §25.1): palette families, exact box tiling, unstretched pieces on the grid, edge-standing platforms as blocks |
| `IndustrialLookTests` | EditMode | industrial look (ARCHITECTURE §25.2): the bevelled box stays inside its box and faces outward, the material library (every surface, anti-tiling keyword, a texture each), grunge and decal placeholders and their import, the decal material, themes, texture file names and folders (displacement maps are linear, the signs folder is decals), relief (a height map and a shallow depth on every textured surface), signs as decals without hazard red, the look set's scope |
| `NatureLookTests` | EditMode | nature look (ARCHITECTURE §25.3): every library material on `HotPatata/Nature` with its maps and mapping, foliage cut-out/two-sided/wind, the gameplay variants (banded hazard, rotten planks, slot logs, lantern), Poly Haven file names, the skies as cubemaps, generated shapes inside their box (rock may bulge 30 cm out of its sides, never above its top) and identical on every build, the rock skirt hidden under its slab and widening below, trees deterministic and within their LOD budgets, PatataCanopy's giants (70 m and more, crowns from 45 m, a tight bole, roots on the ground) within their budgets, leaf-roof cards never under their origin |
| `TimeOfDayTests` | EditMode | the nature courses' days: the blender's step (blend forward, snap back and over big jumps), segments and checkpoint times, each course's five presets in order (dawn low, noon high, dusk low and cool), the mist thick at dawn, thin at noon, back at dusk, PatataCanopy's own cooler day with its haze beyond the longest pass |
| `VerticalCarrierRideTests` | PlayMode | #97 at a pinned frame rate (30 / 60 / 144, `Time.captureFramerate`), a rider stays grounded and on a lift on every frame, within 5 cm of the drawn deck, up at 4 / 7 / 9 m/s, down, and through reversals mid-travel, never falling through; a jump off a rising lift keeps its rise; a tap pass between two riders of a 9 m/s lift is caught |
| `CarrierFallZoneTests` | PlayMode | #97 on PassSandbox (add a level when it has carriers): no kill zone lies across the ride of a lift, bridge or door a player stands on (a bridge that retracts into the void may cross one); the archived courses' pulleys and well climbed through the next section's fall zone |
| `DevCheatsTests` | PlayMode | the map-testing cheats: noclip flies through the floor and walks again when off, invincible sends a falling player back to their spawn without a reset, no explosions hands the bomb back (world contact or fuse) |
| `TrioObstacleTests` | PlayMode | the systems for three on PassSandbox's `KitDemo/TrioObstacles` (PROJECT_SPEC §13.21–13.24): the heavy plate needs two empty hands (a lob does not free its thrower), the hourglass plate runs on after release, a body in the sun beam turns the pivot, which carries its rider a quarter round and turns their view |
| `TrioSystemTests` | EditMode | the pure rules for three: counted bodies, the player a hands-free plate ignores in each bomb state, the hourglass end, the pivot's carry and yaw, the rider rebuilt in the carrier's frame, the beam's cut point, the lightning's pulse (never a step) |
| `LevelAuthoringTests` | EditMode | M18 authored levels (ARCHITECTURE §27): a deck is boards on a walkable proxy at the piece's height with rails; an attached stair opens the deck's rail where it arrives and lands on it; an end snaps to a deck edge only when near it; moving a deck rebuilds it and the ends on it only, nothing rebuilds when nothing changed; undo brings back the piece and its geometry; a duplicate gets its own id; forced supports change what holds a deck; a ring's seam has no rail and never takes a walkway's end; a house's walls stop, its door is open, its roof a ceiling; meshes in memory until saved, then one folder per piece, refilled on rebuild, orphans deleted; the menu's lists stay together with display names; level names |
| `LevelNatureTests` | EditMode | M18.2 authored levels' nature: a giant tree collides on Environment, is a trunk a deck leans on and is sawn round the deck that wraps it (raising the deck re-saws it); water has its kill zone under its surface, turned with it; a forest keeps off a walkway and a pond, replants when the path moves, and is written as Foliage.asset on save; valley walls rise at the terrain's edge only and the safety net stays under it; a mist zone raises the mist to its top and blends out |
| `LevelGameTests` | EditMode | M18.3a authored levels' game tools: walking distances follow a stair to a deck rather than the straight line, a contract's section is its checkpoint's (the start's under the level; a level for three asks why two fail), the wiring says what drives and how long it holds, a resized door and plate read back their size, the walk probe finds a post in a walkway; a checkpoint's campfire stands 3 to 6 m beside it on its own deck (not off its edge),
inside its zone, and the zone follows the fire when it is moved; Verify finds a spawn in a rail or in the air |
| `LevelLookTests` | EditMode | M18.3b: the fifteen forest looks are Prefab Variants of their kit prefab that keep the obstacle contract and the kit's colliders, with a renderers-only `CustomVisual` (the chute: its pipe in wood); the props are decoration drawn in the nature look until made solid; a scanned ruin stands at its real height; Solid fits a box on Environment round a prop and takes it away; the workbench and the woodpile are solid |
| `LevelRockTests` | EditMode | M20 rock, mine and lane pieces (ARCHITECTURE §27.2): a tunnel is solid rock with a ceiling and its clear space, a skylight is a hole and a dead end is closed; a cavern domes over its outline over its floor; a cliff's top and shelves are walked on and its faces stop a body; caves open into each other (a tunnel through a cavern's wall and a gallery's dead end), a tunnel opens a cliff and a crest only rises; the ground opens only in a floored cave's clear space; mine frames stand on the rock under them, their posts solid, and freeze with their lanterns; lane markers paint their glyph; a track lies on the ground, no collider |
| `LevelPaletteTests` | EditMode | M21 L0 the Level Palette (ARCHITECTURE §27.3): the 80 old menu items are catalogue entries and create what they did, in their group, at the same offsets; the generated menus match the catalogue one to one, no shared priority in a submenu, the file is not stale; the Props Brush is gone and its brushes are burst presets; a burst never lands on a walkway or water and Shift erases what it placed; favourites and recent persist; a walkway placed on a deck's edge attaches to it |
| `LevelGroundTests` | EditMode | M21 L1 the layered ground (ARCHITECTURE §27.4): the eight layers in order, the material and its arrays, `GroundSplat`; the shader's four passes and `HP_MixFog`; the rules' bake (rock on a 60° bank, riverbed under a pond, mud on its shore, litter in a dry hollow, trail on a walk line); a re-bake keeps a hand-painted patch and Reset Painted gives it back; resampling to Fine keeps the heights within 2 cm at the old samples, trees and details where they stood, and Undo restores; resolutions by size and fineness; L2: a sculpt beside a hill survives the hill moving and being deleted, a trail on a 40 % slope holds its 30 % grade and its banks meet the ground and it is walked, a river carves its bed with its water under the banks over kill zones all along, a ravine has none |
| `LongClimbTests` | EditMode | M20 The Long Climb (PROJECT_SPEC §15g): Verify Level reports no error; every section's locks hold (seven contracts); six checkpoints in order with the fuse curve and one finish; exactly two players and first in the menu; every actuator worked by a source of its own; each split marked for both lanes; nothing it draws lost its mesh |
| `AuthoredLevelTests` | PlayMode | every scene of `Assets/Scenes/Levels` (one fixture each, none until there is a level): a run, a bomb and one finish; the route runner (`RouteRunner`) walks every walkway, stair and bridge |

EditMode tests live in `Assets/Tests/EditMode/`. PlayMode tests live in `Assets/Tests/PlayMode/`: they load
`PassSandbox` (or an authored level) through `SandboxTestBase` and drive players through
`PlayerInputReader.Scripted`. After touching the bomb obstacles run `ZoneRuleTests`, `BombObstacleTests` and
`BombStateTests`; after a level change, `AuthoredLevelTests` and `CourseContractTests`.

### Running them

With a live Editor (the `unity` CLI):

```text
unity command run_tests --mode EditMode
unity command run_tests --mode PlayMode --async_tests true    # then poll:
unity command test_status                                      # results also in Temp/pipeline_test_status.json
```

Run only the suites for the system you touched, not the whole project every time. After any throw/catch tuning
change, run `PassFeelTests`. After a movement change, run `SprintSlideTests` and `MovementTests`. After a level
change, run `AuthoredLevelTests` and `CourseContractTests`. After an obstacle system or kit prefab change (`MovingPlatform`, `SignalActuator`,
`FallingPlatform`, `RotatingObstacle`, `BombTransit`, zones, `ObstacleVisualDriver`, `ObstaclePrefabValidator`, a prefab or a
variant), run `ObstaclePrefabTests`, `ObstacleKitTests`, `PrefabKitTests` and `BombObstacleTests`. After a kit look change
(`KitSkin`, `KayKitKitBuilder`), run `KitSkinTests`; after an industrial look change (shader,
materials), run `IndustrialLookTests`. After a nature change (`HotPatata/Nature`, the
nature materials, `NatureShapes`, `NatureTreeBuilder`, a day's look table, `NatureDressing`), run `NatureLookTests`,
`TimeOfDayTests`, `KitSkinTests`, `LevelLookTests` and `LevelNatureTests`; after a change to the level list (`CourseKit.WriteMenu`,
`NetworkBootstrap`'s dev-only scenes), `LevelAuthoringTests` and `MenuStationTests`. After a change to the
body screen, hands-free plate or switch, `ZoneRuleTests`, `ObstaclePrefabTests`, `ConfigurationTests` and
`BombObstacleTests`; after `SectionContract` or `CourseContractCheck`, `CourseContractTests`. After a
change to the plates, the sun beam, the pivot or the carrier rotation (`PlayerMotor.Carry`, `RiderReconstruction`),
`TrioSystemTests`, `TrioObstacleTests`, `ZoneRuleTests`, `RiderReconstructionTests`, `ObstaclePrefabTests` and
`BombObstacleTests`.

After a font asset change, run `FontAssetTests` and a local Player build. Editor compilation and PlayMode tests do
not exercise the asset serialization step that rejects `DontSave` font dependencies.

### Rules for writing tests

- Catching is never automatic, so a test that expects a catch must make the receiver press catch (`CatchWhenNear`).
- Movement tests drive `ScriptedInput.Sprint` / `ScriptedInput.Crouch`.
- Tests that measure raw throw physics must set `tuning.assistStrength = 0` and restore it in `finally`.
- An unfocused Editor freezes Play Mode unless `Application.runInBackground` is true. It is on in the project
  settings, and the PlayMode base fixture sets it as well.
- Use `ScriptedInput` and `LocalPlayerSwitcher.SuppressAutoFocus`, never simulated keys.
- **Never** use the CLI `simulate_key` or `InputTestFixture` against a live Editor. They add virtual Keyboard/Mouse
  devices that survive domain reloads. Past ~80 of them, every action binding fails with
  `Control count per binding cannot exceed byte.MaxValue=255` and the game becomes unplayable. To recover, call
  `InputSystem.RemoveDevice` on every device where `!device.native`, then check that `InputSystem.devices` has one
  keyboard and one mouse.

---

## 2. In-game dev tools (Editor and Development builds)

- **F6** or gamepad **Select** opens the `UISampleScreen` (UI Toolkit base, #14) to check the theme, focus and
  mouse / keyboard / gamepad navigation in any scene.
- The main menu and the lobby are UI Toolkit screens (#15), navigable with the mouse, the keyboard and a gamepad.
  Batch-mode runs (the bots below) create no UI: the command line drives the session flow.
- **Esc** or gamepad **Start** opens the pause menu (#16; offline it freezes the game), with Settings (#17) inside;
  the main menu has a Settings button too. **F10** still leaves a networked game at once.
- **F3** toggles `ThrowDebugOverlay`: raw and assisted aim, assist cone, arcs, catch reach, last flight.
- Each throw logs one `[Throw]` line (`ThrowTelemetry`, `HotPatata.DebugTools` assembly).
- **Offline only:** F4 turns the idle player into a catch/throw-back bot (`PassPartner`), and F5 cycles its movement.
  `PassSandbox` has a 4 / 8 / 12 / 16 m range lane on its east side.
- The debug HUD (`DebugHud`) shows run state, resets, checkpoint, time and ping (`NetMode.RttMs`).
- **F8** opens the map-testing tools (`DevPanel`, in the game's assembly; the flags live in `DevCheats`), in every build,
  release included (owner decision, 2026-10-17). While it is open, the number keys use it:
  - **1** noclip (also **F9** at any time): fly where you look, through everything, Space up, Crouch down, Sprint ×3;
  - **2** invincible: a fall into a static kill zone (void, water) sends only that player back to their spawn, moving
    hazards never kill; no team reset;
  - **3** no explosions: whatever would blow the bomb up (world contact, kill zone, fuse) hands it back to its carrier or
    thrower with a fresh fuse;
  - **4** show zones: kill zones (red), checkpoint triggers (green) and carriers (blue), through walls;
  - **5 / 6** previous / next checkpoint: restarts the run there (the `-patataCheckpoint` path);
  - **7** give me the bomb.

  Noclip is the local player's own; the rest are rules, so they act offline or on the host (a client's panel says
  "host only"). A yellow `DEV CHEATS` banner stays on screen while any cheat is on. `-patataCheats` starts a build
  invincible with no explosions.
- **F7** toggles `NetSyncProbe` (netcode plan stage 0, online). It shows:
  - each remote rider's gap to the moving carrier under them, tagged when rebuilt on the carrier, also while they blend
    out after the owner stopped naming it;
  - the host's moving-hazard verdicts (the ignored trigger vs the rewound one);
  - the client's throw prediction against the host's verdict;
  - the local player's own ride (#97): carrier changes (a clean ride is 2) and ground/air flips on a carrier (0), and
    remote riders' pops (> 0.3 m in a frame relative to their carrier, 0).

  It logs `[Sync]` lines: `rider slot=N carrier=id gap mean/min/max` and `ride owner carrierChanges=... groundFlips=...
  | viewer riderPops=...` every 2 s, `hazard ...` and `throw predicted=... host=...`.
- Logs use the `[Bomb]` / `[Run]` / `[Throw]` / `[Sync]` prefixes (`PatataLog`). Mirror lines on clients are tagged `(mirror)`.
  Do not grep logs for "error": the name `ApplyMirror` contains it.

---

## 3. Multiplayer testing

### Command-line flags

| Flag | Effect |
|---|---|
| `-patataHost` / `-patataJoin <ip>` / `-patataLocal` | direct-IP host, join (port 7777), or local play |
| `-patataHostOnline` / `-patataJoinCode <code>` | Relay session host / join by code |
| `-patataScene <name>` | level to load (e.g. `PassSandbox`) |
| `-patataAutoStart <n>` | session host starts the level when n players are in |
| `-patataCheckpoint <id>` | the run starts at that checkpoint (host / local; `RunOptions.StartCheckpoint`) |
| `-patataName <name>` | player name for this process instead of the saved one (two instances on one PC share `PlayerPrefs`); bots keep "Player N" without it |
| `-patataBot` | the local player is a bot (`PlayerBot`) |
| `-patataBotMove <pattern>` | the bot's movement: `Stand`, `Strafe`, `Jump`, `RunAcross`, `Ride` (hops onto the nearest moving platform that is not a crusher, after each reset, and stays on it) or `RideFast` (the same onto the fastest-rising carrier of the scene, #97) |
| `-patataCheats` | start invincible with no explosions (map testing; the F8 panel, §2), any build |
| `-patataLatency <ms>` | Network Simulator delay on every packet this side sends (Editor / dev builds only); jitter defaults to delay / 5 |
| `-patataJitter <ms>` / `-patataLoss <%>` / `-patataLossInterval <n>` | jitter (± ms), random loss, and one packet dropped in every n (#117 P0) |
| `-patataSpike <every s>:<ms>` | a lag spike of ms every so many seconds (e.g. `20:500`) |
| `-patataNet <preset>` | `lan`, `good`, `wifi`, `relay`, `bad`, `awful`, `spiky` (`NetSimSettings.TryPreset`); the flags above refine it |
| `-patataQuit <s>` / `-patataLeaveAfter <s>` | quit / leave the session after s seconds |

From the Editor: `_ = HotPatata.NetworkBootstrap.Instance.HostOnlineAsync();`, then read `.SessionCode`, call
`StartLevel()`, and later `LeaveAsync(null)`. Never call `NetworkManager.Shutdown` in session mode. A hard-killed
client stays listed by the service for a while, but the host lobby shows only players who are really connected.

### Two-process smoke test (no second machine)

1. Build a Development player:
   `unity command build --target StandaloneWindows64 --outputPath <abs>/Builds/HotPatata/HotPatata.exe --options '["Development"]' --confirm true`,
   then poll `build_status`.
2. Open `Bootstrap` in the Editor, press Play, then `eval`
   `HotPatata.PlayerBot.Enabled = true; HotPatata.NetworkBootstrap.Instance.StartHostDirect();`.
3. Launch `HotPatata.exe -batchmode -nographics -patataJoin 127.0.0.1 -patataBot -patataQuit 60 -logFile <abs>/client.log`.
4. The bots pass the bomb back and forth. Compare the `[Bomb]` / `[Run]` logs on both sides.

### Headless bot sessions (no Editor)

```text
HotPatata.exe -batchmode -nographics -patataHost -patataBot -patataScene PassSandbox -patataLatency <ms> -patataQuit <s> -logFile host.log
HotPatata.exe -batchmode -nographics -patataJoin 127.0.0.1 -patataBot -patataLatency <ms> ...     (x N clients)
```

Bots pass to the next slot (round robin), so 3+ players exercise client→client passes. In the host log, count
`Catch accepted`, `Thrown -> Exploding` and `lag-compensated`.

### Latency simulation

`UnityTransport.SetDebugSimulatorParameters` is obsolete and does **nothing**. Because of that, the early M3
"31/31 catches at 100 ms" run was not actually under latency.

Real simulation uses Unity's Network Simulator (`LatencySimulator`, `HotPatata.DebugTools`, Editor and dev builds
only). Set it with the flags of the table above (`-patataLatency`, `-patataJitter`, `-patataLoss`, `-patataSpike`,
`-patataNet`) or `NetworkBootstrap.Instance.SimulateNetwork(settings)` BEFORE hosting or joining, on **every** side.
Always confirm it with the measured round trip: the delay applies to what each side sends, and with both sides
simulating, `-patataLatency 38 -patataJitter 8 -patataLoss 1` measures about 150 ms RTT, and `-patataNet bad` about 400.

**`[NetReport]`** (#117 P0). Every session ends with one block in the log (leave, disconnect or quit), and the F7 panel
shows the same numbers live:

| Line | What it says | Look for |
|---|---|---|
| `rtt to N` | round trip to each connection, p50 / p95 / max | p95 far above p50: jitter or resends |
| `snapshots from slot N` | that player's stamps here: jitter (p95 − median of transit), packet gaps, freezes (gaps over 100 ms), stamps lost for good and recovered from the redundant copies | freezes and lost > 0: their copy stalled |
| `frames` | this machine's frame times; on the host, everyone feels its spikes | spikes on the host |
| `clock` | NGO hard resets of its server-time estimate, and level clock snaps (the level jumping) | any snap |
| `caps hit (host)` | stamps, catch claims and throws older than their cap: judged later than the player saw them | rising with the round trip |
| `catches` | claims accepted / refused (host); catches shown at once then confirmed / refused (client) | refusals, and their reason in the `[Bomb]` lines |
| `throws (client)` | the thrower's prediction against the host's verdict, and how far its arc was corrected when the host's arrived | arc pops above a few cm: the release was clamped |
| `fuse (host)` | fuse expiries held for a throw on its way, and the ones it saved | |

Bot runs on `PassSandbox` (host + 2 clients unless noted, 135 s, #117):

| Condition (per side) | Measured RTT | Catches / explosions | Claims accepted | Throw prediction agrees | Level clock snaps |
|---|---|---|---|---|---|
| none | 2 ms | 79 / 1 | 53 / 53 | 52 / 52 | 0 |
| `-patataLatency 38 -patataJitter 8 -patataLoss 1` | ~150 ms | 72 / 1 | 48 / 50 (the 2 refused: claims older than the 0.35 s cap) | 46 / 46 | 0 host, 0 and 1 on clients |
| `-patataNet bad` (host + 1 client, before the clock fix) | ~400 ms | 20 / 15 | 10 / 10 | 8 / 8 | 126 |

Found while measuring (fixed in #117): the client's level clock advanced by `Time.unscaledDeltaTime`, which is the
physics step when the first sample of a frame comes from a `FixedUpdate` (actuators, falling platforms and plates read
server time there): it ran ahead of the server and snapped back by about 0.5 s every second or so (180 snaps in two
minutes). It now measures real time between samples.

Before #117 (M3.5 lag compensation), for reference:

| RTT | Before lag compensation | With lag compensation (M3.5) |
|---|---|---|
| 5 ms | 32/32 | |
| 90 ms | 26/26 | |
| ~230–240 ms | 0/16, 0/15 (all "Too late") | 20/20 (1 client), 22/22 incl. client→client (2 clients) |
| ~390 ms | 1/20 | |
| ~450 ms | | 0/14: beyond the `catchLagCompensation` cap (0.35 s) |

How the compensation works: [`ARCHITECTURE.md`](ARCHITECTURE.md) §13.2; the flight segments and the transport §13.3–13.4.

Still to measure on real networks (M3.7): two PCs on a LAN with Clumsy (Windows) adding loss and jitter, and one
internet session over Relay with Wi-Fi on one side. Keep both `[NetReport]` blocks; the targets are those of #117 §7
(throw prediction ≥ 99 %, predicted catches confirmed ≥ 98 %, no remote player frozen over 100 ms at 2 % loss).

### Remote players vs moving level objects (#92)

What to measure: [`netcode-deterministic-plan.md`](netcode-deterministic-plan.md) §4. Run it with bots at
`-patataLatency` 0 / 50 / 100 / 150 (one way, so 0–300 ms RTT), host + client and host + 2 clients (client → client):

```text
HotPatata.exe -batchmode -nographics -patataHost -patataBot -patataBotMove Ride -patataScene PassSandbox -patataLatency <ms> -patataQuit 90 -logFile host.log
HotPatata.exe -batchmode -nographics -patataJoin 127.0.0.1 -patataBot -patataBotMove Ride -patataLatency <ms> -patataQuit 85 -logFile client.log
```

- **Riders:** `[Sync] rider` lines on every peer. The gap should stay within ±5 cm (it was about speed × (RTT + 0.1 s)
  before stage 1). `PassSandbox` has two moving carriers and its lifts are slow (1.9 m/s); for fast lifts use
  `-patataBotMove RideFast` (the fastest-rising carrier of the scene) with `-patataScene <a level>`
  and `-patataCheckpoint`. The `ride` line must show no ground flips and no rider pops (#97).
- **Hazards:** with `-patataBotMove RunAcross` near a rotating bar, the host logs each ignored trigger and each rewound
  hit (`[Sync] hazard ...`). The two should agree almost always; a disagreement is the lag that stage 3 removes.
- **Throws:** `[Sync] throw ... agree|DISAGREE` on clients (stage 4 will act on these).

### Still open

Testing on two physical machines (MVP_TASKS M3.7), and the M7 hardening tests (reset stress, 2/3/4 players,
instability).

---

## 4. CI and releases

`.github/workflows/build.yml` builds the player for Windows, Linux and macOS (Mono) with GameCI on GitHub-hosted
Ubuntu, one job per platform, and releases only from tags. Pushes to `main` do not build.

| Tag (on a commit on `main`) | Builds |
|---|---|
| `v0.4.0` (global) | Windows, Linux and macOS |
| `v0.4.0-linux` | Linux only |
| `v0.4.0-linux-macos` | several platforms (tokens: `windows`, `linux`, `macos`) |
| `v0.4.0-rc1-linux` | other tokens stay in the version (`0.4.0-rc1`); an unknown word such as `html5` is part of the version, not a platform |

- Each tag publishes one GitHub Release named after the tag, with `HotPatata-<Windows|Linux|macOS>-<version>.zip`:
  `git tag v0.4.0 && git push origin v0.4.0`.
- When the repo variable `ITCH_TARGET` is set (`<itch user>/<game>`), each zip is also pushed to its itch.io channel
  (`windows`, `linux`, `osx`; user version = the tag's version). It needs the secret `BUTLER_API_KEY`
  (itch.io → Settings → API keys). Without `ITCH_TARGET` the step is skipped; with it but no key, the publish job fails
  after the GitHub Release. butler is downloaded from itch.io, not from a third-party action.
- A platform that fails does not stop the others: the release publishes what built, and the failed job keeps the
  run red.
- macOS builds are **not signed or notarized** (owner decision, for now): players open the app with right-click → Open
  the first time. Linux builds also run on the Steam Deck.
- **Actions → Build → Run workflow** builds on demand (input `platforms`: `all` or e.g. `windows linux`) and keeps the zips
  as workflow artifacts for 7 days, without publishing.
- A pull request builds every platform when it changes the workflow itself.
- Required repo secrets: `UNITY_LICENSE` (the contents of `C:\ProgramData\Unity\Unity_lic.ulf`), `UNITY_EMAIL`,
  `UNITY_PASSWORD`.

CI does not run the test suites. Run the relevant suites locally (section 1) before opening a PR.
