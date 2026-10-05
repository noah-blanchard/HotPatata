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
| `CourseTests` | PlayMode | M5 course Act 1: structure, checkpoints, finish, rematch, start checkpoint, launch pad; #23 the results screen opens on finish with the run time and its Rematch button restarts the run |
| `FactoryCourseTests` | PlayMode | course Acts 2–3: belts, elevators, sweepers, crushers, mega slide |
| `ZoneRuleTests` | EditMode | #68 pure rules: fuse-zone severity, flight-sweep geometry, actuator motion, transit exit arc |
| `BombObstacleTests` | PlayMode | #68 kit on the PassSandbox floor: fuse zones, curtains and windows, gates, plates, doors, arch checkpoint, tubes |
| `PlaytestCourseTests` | PlayMode | M10.4 `PlaytestCourse`: every beat wired, nine checkpoints with two arches, the podium gate, finish and rematch |
| `KitSkinTests` | EditMode | KayKit kit (ARCHITECTURE §25.1): palette families, exact box tiling, unstretched pieces on the grid, edge-standing platforms as blocks |
| `PatataParkTests` | PlayMode | `PatataPark`: every beat wired and drawn in KayKit pieces, nine checkpoints with two arches, the podium gate, finish and rematch |
| `IndustrialLookTests` | EditMode | industrial look (ARCHITECTURE §25.2): the bevelled box stays inside its box and faces outward, the material library (every surface, anti-tiling keyword, a texture each), grunge and decal placeholders and their import, the decal material, themes, texture file names and folders, the look set's scope |
| `IndustrialLabTests` | PlayMode | `IndustrialLab`: checkpoints and finish, passes clear ceilings, solids and decoration, bevelled visuals inside their colliders, every industrial material drawn |
| `IndustrialPlantTests` | PlayMode | `IndustrialPlant`: nine checkpoints, climbs, drops and spread, every kit obstacle present, the building closed in 26 directions from every route point, passes clear, signals one-to-one, no sun and many warm lamps, diverse floors/walls/ceilings and decals, every checkpoint resets then finish and rematch, the atrium plate pass, every transit flight reaches its pad |
| `PatataWorksTests` | PlayMode | enclosed route and ceilings, sampled pass clearance and decoration, signal/transit wiring, explicit catches through the atrium ring and transit exits, nine checkpoint resets, finish/rematch and menu order |

EditMode tests live in `Assets/Tests/EditMode/`. PlayMode tests live in `Assets/Tests/PlayMode/`: they load
`PassSandbox` (or `PrototypeCourse`, `PlaytestCourse`) through `SandboxTestBase` and drive players through
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
change, run `CourseTests` / `FactoryCourseTests`. After a kit look change (`KitSkin`, `KayKitKitBuilder`), run
`KitSkinTests` and the course suites; after an industrial look change (shader, materials, `IndustrialLabBuilder`), run
`IndustrialLookTests`, `IndustrialLabTests` and `IndustrialPlantTests`.

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
- Logs use the `[Bomb]` / `[Run]` / `[Throw]` prefixes (`PatataLog`). Mirror lines on clients are tagged `(mirror)`.
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
