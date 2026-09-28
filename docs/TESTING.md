# HotPatata — TESTING.md

> How to test HotPatata: the automated suites, the dev tools, multiplayer bot sessions, latency simulation, and CI.
> There are no build or lint scripts: everything runs through the Unity Editor, a Development build, or GitHub Actions.

---

## 1. Automated tests (Unity Test Framework)

| Suite | Mode | Covers |
|---|---|---|
| `ConfigurationTests` | EditMode | M0 baseline: tuning defaults, layers, collision matrix |
| `BombFuseTests` | EditMode | fuse maths: refresh, expiry, warning phase, stage bands |
| `AimAssistTests` | EditMode | release-time aim assist and arc maths (spec §8.3) |
| `FlightHistoryTests` | EditMode | the host's flight record used for lag-compensated catches |
| `MovingPlatformTests` | EditMode | `MovingPlatform.Evaluate`, the pure clock-to-position maths |
| `SessionServiceTests` | EditMode | M4: code cleanup and player-facing error wording |
| `BombStateTests` | PlayMode | bomb state machine plus catch / world-contact / fuse rules (M1.4–M1.8) |
| `RunAndThrowTests` | PlayMode | throw by input (M1.5) and fast, clean section reset (M1.10) |
| `MovementTests` | PlayMode | accel/brake, jump, coyote time, jump buffer (M1.2) |
| `SprintSlideTests` | PlayMode | M9 movement: sprint, carving, slide, slide-jump, crouch, mantle |
| `ViewFeelTests` | PlayMode | first-person speed feel, post effects, accessibility scaling |
| `PassFeelTests` | PlayMode | **pass-feel regression suite**: catches at 4/8/12 m moving and jumping, the assist never creates range |
| `PrefabKitTests` | PlayMode | M2 greybox kit (moving/falling platforms, rotating bar, kill zone, checkpoint, finish) |
| `CourseTests` | PlayMode | M5 course Act 1: structure, checkpoints, finish, rematch, launch pad |
| `FactoryCourseTests` | PlayMode | course Acts 2–3: belts, elevators, sweepers, crushers, mega slide |

EditMode tests live in `Assets/Tests/EditMode/`. PlayMode tests live in `Assets/Tests/PlayMode/`: they load
`PassSandbox` (or `PrototypeCourse`) through `SandboxTestBase` and drive players through `PlayerInputReader.Scripted`.

### Running them

With a live Editor (the `unity` CLI):

```text
unity command run_tests --mode EditMode
unity command run_tests --mode PlayMode --async_tests true    # then poll:
unity command test_status                                      # results also in Temp/pipeline_test_status.json
```

Run only the suites for the system you touched, not the whole project every time. After any throw/catch tuning
change, run `PassFeelTests`. After a movement change, run `SprintSlideTests` and `MovementTests`. After a course
change, run `CourseTests` / `FactoryCourseTests`.

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
   `HotPatata.PlayerBot.Enabled = true; HotPatata.NetworkBootstrap.Instance.StartHost();`.
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

`.github/workflows/build-windows.yml` builds StandaloneWindows64 (Mono) with GameCI on GitHub-hosted Ubuntu.

- A push to `main` uploads a zipped build as a workflow artifact.
- A `v*` tag on a commit that is on `main` publishes a GitHub Release with `HotPatata-Windows-<version>.zip`. The
  version comes from the tag: `git tag v0.2.0 && git push origin v0.2.0`.
- A pull request only builds when it changes the workflow itself.
- Required repo secrets: `UNITY_LICENSE` (the contents of `C:\ProgramData\Unity\Unity_lic.ulf`), `UNITY_EMAIL`,
  `UNITY_PASSWORD`.

CI does not run the test suites. Run the relevant suites locally (section 1) before opening a PR.
