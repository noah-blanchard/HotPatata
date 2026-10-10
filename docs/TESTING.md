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
| `FlightHistoryTests` | EditMode | the host's flight record used for lag-compensated catches |
| `MovingPlatformTests` | EditMode | `MovingPlatform.Evaluate`, the pure clock-to-position maths |
| `SimulationClockTests` | EditMode | #92 the client clock: in step it follows the frame, NGO corrections are slewed (never backwards, at most `clockMaxSlew`), a real desync snaps; offline it is the local clock |
| `CarrierRegistryTests` | EditMode | #92 carrier ids: stable FNV-1a hash, follows the sibling path not the name, unique in every course scene and PassSandbox |
| `RiderReconstructionTests` | EditMode | #92 a remote rider glued to the carrier as drawn locally whatever the lagging world position, board and leave blends without a pop, the offset smoothed, a teleport snaps |
| `HazardRewindTests` | EditMode | #92 the host's rewound hazard verdict: capsule vs (rotated) box, a box carried with its mover to another time, the verdict at the claimed time and not now, the claim cap, a door's edge armed only while closing, a moving platform posed at any time by the formula it moves by |
| `SessionServiceTests` | EditMode | M4: code cleanup and player-facing error wording |
| `SettingsTests` | EditMode | M6.5: player settings layer (tuning defaults, JSON parsing, clamping, fallback, mixer decibels) |
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
| `CourseContractTests` | EditMode | section contracts (PROJECT_SPEC §13.20): the reach envelopes (climb = jump + mantle, gaps shrink uphill and end out of reach, a lob clears any roofless wall), the scan finds a body screen walked round until walls close its ends and a curtain flown round and over, then every course scene checked; findings are warnings (logged, the test passes) |
| `KitSkinTests` | EditMode | KayKit kit (ARCHITECTURE §25.1): palette families, exact box tiling, unstretched pieces on the grid, edge-standing platforms as blocks |
| `IndustrialLookTests` | EditMode | industrial look (ARCHITECTURE §25.2): the bevelled box stays inside its box and faces outward, the material library (every surface, anti-tiling keyword, a texture each), grunge and decal placeholders and their import, the decal material, themes, texture file names and folders (displacement maps are linear, the signs folder is decals), relief (a height map and a shallow depth on every textured surface), signs as decals without hazard red, the look set's scope |
| `IndustrialPlantTests` | PlayMode | `IndustrialPlant`: nine checkpoints, climbs, drops and spread, every kit obstacle present, the building closed in 26 directions from every route point, passes clear, signals one-to-one, no sun, many warm lamps, a bright neutral fill and its own exposure volume, diverse floors/walls/ceilings and decals (never on moving surfaces), mouldings and props in every room (collider-free, never on a mover), no two rooms' floors overlapping at one height, the level list (PatataWilds, PatataCanopy, the plant with nine spawn choices, PassSandbox) and nothing else in the build, every checkpoint resets then finish and rematch, the atrium plate pass, every transit flight reaches its pad |
| `NatureLookTests` | EditMode | nature look (ARCHITECTURE §25.3): every library material on `HotPatata/Nature` with its maps and mapping, foliage cut-out/two-sided/wind, the gameplay variants (banded hazard, rotten planks, slot logs, lantern), Poly Haven file names, the skies as cubemaps, generated shapes inside their box (rock may bulge 30 cm out of its sides, never above its top) and identical on every build, the rock skirt hidden under its slab and widening below, trees deterministic and within their LOD budgets, PatataCanopy's giants (70 m and more, crowns from 45 m, a tight bole, roots on the ground) within their budgets, leaf-roof cards never under their origin |
| `TimeOfDayTests` | EditMode | the nature courses' days: the blender's step (blend forward, snap back and over big jumps), segments and checkpoint times, each course's five presets in order (dawn low, noon high, dusk low and cool), the mist thick at dawn, thin at noon, back at dusk, PatataCanopy's own cooler day with its haze beyond the longest pass |
| `PatataWildsTests` | PlayMode | `PatataWilds` (PROJECT_SPEC §15c): route spread, turns, climb and drop, 25 checkpoints and the summit finish, the summit fuse overrides, passes clear of solids, ceilings, decoration and scattered plants (normal passes at most 14 m), signals one-to-one, campfires instead of pads (catch when reached, out on restart), water always over a kill zone, the time of day following the checkpoint, no mesh collider and generated visuals inside their colliders, banded hazards, 3D ambience on the SFX group, first in the menu with 25 spawn choices, Hold the Rope with two players, every checkpoint resets then finish and rematch, every transit flight reaches its pad |
| `PatataTempleTests` | PlayMode | `PatataTemple` (PROJECT_SPEC §15e): 15 checkpoints, one finish, a contract for three per section with its "With two" line, three arches, the fuse overrides, the heavy and hourglass plates, beams and pivots present and wired (hourglasses replicated), passes clear of ceilings and solids, the altar over the pyramid and above every floor, the canopy under every floor near it; on the course: the heavy slab ignores its carrier and two empty hands raise the bridge, a body in a wing's light raises another wing's herse, the rose's plate turns its pivot |
| `TrioObstacleTests` | PlayMode | the systems for three on PassSandbox's `KitDemo/TrioObstacles` (PROJECT_SPEC §13.21–13.24): the heavy plate needs two empty hands (a lob does not free its thrower), the hourglass plate runs on after release, a body in the sun beam turns the pivot, which carries its rider a quarter round and turns their view |
| `TrioSystemTests` | EditMode | the pure rules for three: counted bodies, the player a hands-free plate ignores in each bomb state, the hourglass end, the pivot's carry and yaw, the rider rebuilt in the carrier's frame, the beam's cut point, the lightning's pulse (never a step) |
| `PatataCanopyTests` | PlayMode | `PatataCanopy` (PROJECT_SPEC §15d): 25 checkpoints, one finish, a contract per section, the fuse overrides, passes clear of ceilings, solids and decoration (normal passes at most 14.5 m), signals one-to-one, three switches with targets, hands-free plates, body screens on their layer, campfires, second in the menu with 25 spawn choices, Les Lucioles, Pont-levis croisé, La Plaque and the spore bridge, every checkpoint resets then finish and rematch, every transit flight reaches its pad; the forest (M13.7): giants and plants clear of every pass, deck trunks from the forest floor to their decks, the floor about 40 m under every checkpoint and the safety net under it, nothing under a leaf roof's underside, the mist on the floor and light at deck height, light shafts clear of every pass at every moment of the day |

EditMode tests live in `Assets/Tests/EditMode/`. PlayMode tests live in `Assets/Tests/PlayMode/`: they load
`PassSandbox` (or `IndustrialPlant`, `PatataWilds`, `PatataCanopy`) through `SandboxTestBase` and drive players through
`PlayerInputReader.Scripted`. After touching the bomb obstacles run `ZoneRuleTests`, `BombObstacleTests` and
`BombStateTests`; after a course builder change, the tests of that course.

### Running them

With a live Editor (the `unity` CLI):

```text
unity command run_tests --mode EditMode
unity command run_tests --mode PlayMode --async_tests true    # then poll:
unity command test_status                                      # results also in Temp/pipeline_test_status.json
```

Run only the suites for the system you touched, not the whole project every time. After any throw/catch tuning
change, run `PassFeelTests`. After a movement change, run `SprintSlideTests` and `MovementTests`. After a course
change, run that course's suite. After an obstacle system or kit prefab change (`MovingPlatform`, `SignalActuator`,
`FallingPlatform`, `RotatingObstacle`, `BombTransit`, zones, `ObstacleVisualDriver`, `ObstaclePrefabValidator`, a prefab or a
variant), run `ObstaclePrefabTests`, `ObstacleKitTests`, `PrefabKitTests` and `BombObstacleTests`. After a kit look change
(`KitSkin`, `KayKitKitBuilder`), run `KitSkinTests` and the course suites; after an industrial look change (shader,
materials, `IndustrialPlantBuilder`), run `IndustrialLookTests` and `IndustrialPlantTests`. After a nature change (`HotPatata/Nature`, the
nature materials, `NatureShapes`, `NatureTreeBuilder`, `PatataWildsLook`, `PatataWildsBuilder`, `NatureDressing`), rebuild
PatataWilds and run `NatureLookTests`, `TimeOfDayTests`, `KitSkinTests` and `PatataWildsTests`; after a change to the
level list (`CourseKit.RegisterInMenu`), also `IndustrialPlantTests` and `MenuStationTests`. After a change to
`PatataCanopyBuilder`, rebuild PatataCanopy and run `PatataCanopyTests` and `CourseContractTests`; after a change to the
body screen, hands-free plate or switch, `ZoneRuleTests`, `ObstaclePrefabTests`, `ConfigurationTests` and
`BombObstacleTests`; after `SectionContract` or `CourseContractCheck`, `CourseContractTests`. After a change to
`PatataTempleBuilder` or `PatataTempleLook`, rebuild PatataTemple and run `PatataTempleTests` and `CourseContractTests`; after a
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
- **F7** toggles `NetSyncProbe` (netcode plan stage 0, online only). It shows:
  - each remote rider's gap to the moving carrier under them, tagged when rebuilt on the carrier;
  - the host's moving-hazard verdicts (the ignored trigger vs the rewound one);
  - the client's throw prediction against the host's verdict.

  It logs `[Sync]` lines: `rider slot=N carrier=id gap mean/min/max` every 2 s, `hazard ...` and `throw predicted=... host=...`.
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
| `-patataBotMove <pattern>` | the bot's movement: `Stand`, `Strafe`, `Jump`, `RunAcross` or `Ride` (hops onto the nearest moving platform that is not a crusher, after each reset, and stays on it) |
| `-patataLatency <ms>` | Network Simulator latency (Editor / dev builds only) |
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
only). Set it with `-patataLatency <ms>` or `NetworkBootstrap.Instance.SimulateLatency(ms)` BEFORE hosting or
joining, on **both** sides. Always confirm it with `UnityTransport.GetCurrentRtt`.

Measured with bots:

| RTT | Before lag compensation | With lag compensation (M3.5) |
|---|---|---|
| 5 ms | 32/32 | |
| 90 ms | 26/26 | |
| ~230–240 ms | 0/16, 0/15 (all "Too late") | 20/20 (1 client), 22/22 incl. client→client (2 clients) |
| ~390 ms | 1/20 | |
| ~450 ms | | 0/14: beyond the `catchLagCompensation` cap (0.35 s) |

How the compensation works: [`ARCHITECTURE.md`](ARCHITECTURE.md) §13.2.

### Remote players vs moving level objects (#92)

What to measure: [`netcode-deterministic-plan.md`](netcode-deterministic-plan.md) §4. Run it with bots at
`-patataLatency` 0 / 50 / 100 / 150 (one way, so 0–300 ms RTT), host + client and host + 2 clients (client → client):

```text
HotPatata.exe -batchmode -nographics -patataHost -patataBot -patataBotMove Ride -patataScene PassSandbox -patataLatency <ms> -patataQuit 90 -logFile host.log
HotPatata.exe -batchmode -nographics -patataJoin 127.0.0.1 -patataBot -patataBotMove Ride -patataLatency <ms> -patataQuit 85 -logFile client.log
```

- **Riders:** `[Sync] rider` lines on every peer. The gap should stay within ±5 cm (it was about speed × (RTT + 0.1 s)
  before stage 1). `PassSandbox` has two moving carriers; for elevators and lifts use `-patataScene IndustrialPlant`
  (or a course) with `-patataCheckpoint`.
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
