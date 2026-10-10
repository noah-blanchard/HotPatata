# Netcode plan: remote players vs. time-driven level objects (moving platforms and beyond)

> Status: **proposal, nothing implemented**. This file is the diagnosis and the staged plan behind the issue
> "Désynchro joueur / plateformes mobiles: passer à des plateformes déterministes par tick". It changes no gameplay
> rule. When a stage ships, move what it settles into `ARCHITECTURE.md` §13 and `OBSTACLES.md`, and update this file.

## 0. Summary

- **Symptom.** Online, a teammate standing on an elevator or moving platform looks offset from it: sunk into it or
  floating above it, sliding along it.
- **The "platform = f(shared time)" half already exists.** Every moving level object computes its pose from one
  shared clock (`SectionClock`, i.e. NGO `ServerTime` minus a host-replicated section start). Nothing about a
  platform's position goes over the network, and every machine computes the same pose for the same time.
- **The missing half is the rider.** Remote players are synced in **world space** by an interpolated
  `NetworkTransform`. The viewer draws the platform at **its own current section time**. It draws the remote player at
  a world position the owner computed against the platform at an **earlier** section time (one-way latency of the
  owner, the host relay hop and the interpolation buffer). Nothing ties the two together. The visible gap is about
  `platform speed × (≈ RTT + interpolation delay)`.
- **The same root cause shows up elsewhere** (§1.7):
  - the host judges lethal hazards (rotating bars, crushers) against remote players drawn in the past;
  - the thrower's local throw prediction samples moving obstacles at the wrong time;
  - pressure-plate lights on clients show a local guess;
  - catches on moving carriers are offset;
  - a reset can teleport a client before the new section start arrives.
- **Recommended fix (stage 1):** sync a rider's position **relative to its carrier** (carrier id + offset) and rebuild
  it on each viewer against the platform pose of the viewer's own clock. Then (stages 2–4), stamp player state with
  the section tick, so the host can judge a remote player against the level **at the tick that player saw**. The level
  is a pure function of time, so rewinding it is free. Full deterministic lockstep or rollback is
  **not** needed for this bug. For this game it would be a rewrite of movement, physics and the authority model
  (stage 7, not recommended).

## 1. Diagnosis

### 1.1 Network stack and authority model

| Item | What the repo does | Where |
|---|---|---|
| Library | Unity **Netcode for GameObjects 2.13** over Unity Transport, Relay sessions (Multiplayer Services) | `Packages/manifest.json`, `Assets/Scripts/Networking/SessionService.cs` |
| Topology | Not a mesh. One player is **host** (server + client); the others connect to it via Relay. No dedicated server. | `docs/ARCHITECTURE.md` §13.1 |
| Tick | NGO `NetworkTickSystem`, `TickRate: 30` | `Assets/Prefabs/Network/NetworkManager.prefab:58` |
| Rules | Host-authoritative: carrier, catch, fuse, explosion, checkpoint, reset, finish. `NetMode.IsAuthority` is true offline and on the host. | `Assets/Scripts/Core/NetMode.cs:15`, `AGENTS.md` |
| Player movement | **Owner-authoritative** `NetworkTransform` (`AuthorityMode: 1`): each client simulates its own player and pushes its transform | `Assets/Scripts/Networking/NetworkPlayer.cs:8-9`, `Assets/Prefabs/Player/Player.prefab:680` |
| Bomb | Host physics, server-authoritative `NetworkTransform` plus a state snapshot (`NetworkBomb`) | `ARCHITECTURE.md` §13.1 |
| Run state / section clock | `NetworkRunState` replicates `sectionStart` (a server time), run state, checkpoint | `Assets/Scripts/Networking/NetworkRunState.cs:15,29-34` |

### 1.2 Moving level objects today

| Script | How it moves | Clock | Networked? |
|---|---|---|---|
| `MovingPlatform` (platforms, elevators, pistons, crushers) | `Update()` (execution order −50) writes `platform.position = PositionAt(A, B, Evaluate(SectionClock.Now, …))`. `Evaluate` is a static pure function (PingPong or Dwell+SmoothStep). Exposes `FrameDelta` to carry riders. | `SectionClock` | No: pure function of the shared clock |
| `RotatingObstacle` (bars, sweepers) | `Update()` sets `localRotation` from `phaseDegrees + degreesPerSecond * SectionClock.Now` | `SectionClock` | No |
| `SignalActuator` (doors, bridges, lifts driven by plates/switches) | `Progress(changeTime, fromProgress, opening, travelSeconds, ServerTime)`, a pure function of a replicated change time. Posed in `Update()`, state change detected in `FixedUpdate()`. Exposes `FrameDelta`. | `NetMode.ServerTime` | Only the change event (`NetworkSignalActuator.state`) |
| `FallingPlatform` | `ServerTime − triggerTime`. Trigger time decided on the host, replicated once. | `NetMode.ServerTime` | Only the trigger time (`NetworkFallingPlatform`) |
| `Conveyor` (belts) | Static. `FrameDelta = Velocity * Time.deltaTime` | real frame time | No (static, no visual offset possible) |
| `MenuFloat` | Menu-only decoration, offline | unscaled time | n/a |

Evidence:

- `Assets/Scripts/Obstacles/MovingPlatform.cs:73-81`: `Evaluate` is static and pure.
- `MovingPlatform.cs:93-113`: `Update` samples `SectionClock.Now`, writes the transform, computes `FrameDelta`.
- `Assets/Scripts/Obstacles/RotatingObstacle.cs:18,29`.
- `Assets/Scripts/Zones/SignalActuator.cs:46,81-99`.
- `Assets/Scripts/Obstacles/FallingPlatform.cs:35`.
- `Assets/Scripts/Obstacles/Conveyor.cs:32`.

No moving level object uses an `Animator`, a `NetworkTransform`, a non-kinematic `Rigidbody` or PhysX forces to move.
The `Transform` is already only an output of a function of time.

### 1.3 Is there a shared simulation tick?

**Yes, as a continuous clock, not as an integer tick owned by the game.**

- `NetMode.ServerTime` = `NetworkManager.ServerTime.Time` online, `Time.timeAsDouble` offline
  (`Assets/Scripts/Core/NetMode.cs:47`).
- `RunManager.SectionTime = ServerTime − sectionStart` (`Assets/Scripts/Run/RunManager.cs:53`). `sectionStart` is
  set by the host at the start (`:121-122`) and on every reset (`:166`), and pushed to clients (`:116`).
- `SectionClock.Now` wraps it (`Assets/Scripts/Core/SectionClock.cs:12`).
- `AGENTS.md` already makes it a rule: "Level motion derives from `SectionClock` (server time), never `Time.time`."

What is missing compared with an explicit tick:

1. No integer `SectionTick`. Platforms sample a `float` time at render rate (`Update`), so two peers never sample the
   *same instant*. They only sample the same *function*.
2. `NetworkManager.ServerTime` on a client is an *estimate*: about one-way latency behind the host. NGO's time sync
   adjusts it, so it can be corrected by small steps.
3. Player state is not stamped with the section time/tick at which it was produced.

### 1.4 How player positions are synced and interpolated

- **Local player:** `PlayerMotor.Update()` with `Time.deltaTime`, through a `CharacterController`
  (`Assets/Scripts/Player/PlayerMotor.cs:93-201`). It rides a carrier by adding `ridingPlatform.FrameDelta` to its
  `controller.Move` (`:189-192`). The carrier is found in `OnControllerColliderHit` (`:415-419`) through
  `IPlatformCarrier`, with no parenting.
- **Remote copy:** `PlayerMotor` returns early (`:98-104`, "Remote copies are moved by their NetworkTransform; only
  mirror the posture"). It never looks at carriers.
- **`NetworkTransform` on `Player.prefab`:**
  - `AuthorityMode: 1` (owner, `:680`);
  - `Interpolate: 1` (`:700`);
  - `PositionInterpolationType: 0` (`:671`);
  - `PositionMaxInterpolationTime: 0.1` (`:675`);
  - `InLocalSpace: 0` (`:698`): **world space**.
- **No parenting:** no `NetworkObject` reparenting, no local-space sync, no "relative to platform" field anywhere
  (`NetworkPlayer` replicates slot, lock, name, pitch and `moveState` only: `NetworkPlayer.cs:23-29`).
- Teleports are flagged so they are not interpolated (`NetworkPlayer.SyncTeleport`, `:104-109`).

### 1.5 Why the teammate goes through the platform

Notation:

- `P(t)`: the platform pose at section time `t`, the same function on every machine;
- `d_X`: the one-way latency between peer X and the host (`d_host = 0`);
- `β`: the viewer's interpolation delay (at least one tick, 33 ms at 30 Hz, smoothed up to
  `PositionMaxInterpolationTime` = 0.1 s).

1. At real time `r`, owner A stands on its copy of the platform. It computes and sends the world position
   `P(r − d_A) + o`, where `o` is A's offset on the platform.
2. Viewer B draws the platform at its own clock, `P(r' − d_B)`.
3. B draws A's world position, which left A at `r ≈ r' − d_A − d_B − β` (owner → host → B, then the buffer).
4. B therefore shows A at `P(r' − 2·d_A − d_B − β) + o` next to a platform at `P(r' − d_B)`.

The gap is `Δt ≈ 2·d_A + β ≈ RTT_A + β`. The visible error is `|P(t) − P(t − Δt)| ≈ v · Δt`. For example, an
elevator at 2 m/s with 150 ms RTT and about 100 ms of interpolation gives about 0.5 m of error. Vertically, that is a
teammate sunk to the knees or floating, which is what you see.

The exact factor depends on how NGO 2.13 timestamps owner-authoritative states. Stage 0 measures it. The structure of
the error does not change: **the rider is drawn in the past, the platform in the present, and both are in world
space.**

This also explains why it is worse:

- client → client (two hops);
- on fast platforms;
- on vertical movers, where the eye reads penetration instantly;
- at direction reversals of `Dwell` platforms (the error vector flips sign).

The **host's** rules also see remote riders at these lagging world positions: checkpoints, plates with
`countCarrier`, kill zones and `FlightHistory`. Today that is masked by tolerances, but it is the same root cause.

### 1.6 Sources of non-determinism (relevant to this problem)

| Source | Where | Impact |
|---|---|---|
| Client `ServerTime` is an estimate, corrected over time | `NetMode.cs:47` | A platform's pose differs between peers by about `d_X`, with small jumps on resync. Harmless for the platform alone. It matters once riders are reconstructed (they must use the same clock as the platform on that viewer). |
| Platforms sampled in `Update()` with float time | `MovingPlatform.cs:100` | Frame-rate dependent *sampling*, not *drift*: the function is pure. OK. |
| `SectionTime` is `(float)` of a double difference | `RunManager.cs:53` | Fine for sections of hours (float has ~1 ms resolution at 2 h). Keep the subtraction in double. |
| Player movement: `Time.deltaTime` + `CharacterController` (PhysX) | `PlayerMotor.cs:96,191` | Not deterministic across machines. Irrelevant while movement stays owner-authoritative. **It blocks lockstep and rollback** (stage 7). |
| Carry by `FrameDelta`, integrated per frame | `PlayerMotor.cs:190`, `Conveyor.cs:32` | Frame-rate dependent on the owner only. Fine. |
| `UnityEngine.Random` | `FallingPlatform.cs:83` (visual shake), VFX, audio, bots | Presentation only. Not a gameplay input. OK. |
| Visibility-dependent logic | none found (no `OnBecameVisible`, `CullingGroup`) | OK: platforms are evaluated every frame whether seen or not. Any future culling must stay presentation-only. |
| Bomb physics (`BombPhysics`, sweeps) | host only | Not replicated as a simulation. Irrelevant here. |

### 1.7 Other desync points (same family, beyond riders)

The general pattern: **a state produced on one machine at section time `t1` is judged or drawn on another machine
against the level at `t2 ≠ t1`.** Because every level object is a pure function of time, each of these can be fixed
by evaluating the level at the right `t` instead of networking more state.

| # | Where | What happens | Severity | Fix direction |
|---|---|---|---|---|
| A | **Lethal moving hazards judged on the host.** `KillZone.OnTriggerEnter` (`Assets/Scripts/Run/KillZone.cs:17-21`) runs on every peer. `FailSection` acts on the host only (`RunManager.cs:141-143`), so the host's trigger decides. It fires on the host's **copy** of a remote player (world-space, `RTT_A + β` old) against a bar (`RotatingObstacle.cs:29`, child `KillZone`) or a closing crusher (`SignalActuator.cs:105`, `lethalWhileClosing`) posed at **host** time. | A client dodges a bar on its screen and the team still resets (or the reverse: the host sees a hit the client never saw). Stage 0 confirms how often the remote copy's `CharacterController` fires these triggers on the host. Error ≈ angular speed × `(RTT_A + β)`. | **High** (a whole-team fail, felt as unfair) | Stage 3: tick-stamped snapshot; the host tests the remote player's position at tick `k` against `Pose(k)` of the hazard, with a tolerance and a cap like §13.2. Alternative: the owner detects the hit and the host validates it. |
| B | **Riders on moving carriers** (§1.5) | Drawn sunk or floating; host sees them offset. | **High** (the reported bug) | Stage 1 |
| C | **Throw prediction vs. moving obstacles.** `NetworkBomb.PredictLocalThrow` (`Assets/Scripts/Networking/NetworkBomb.cs:11-13`) draws the throw against the client's level (`T_client`). The host starts the real flight when the request arrives, at host time ≈ `T_client + RTT`, with every moving bar, door, gate or platform `RTT` further on. | The predicted arc passes a gate or bar and the host explodes it (or the reverse). Visible correction when the verdict arrives. | Medium | Stage 4: the prediction samples moving obstacles at `T_client + RTT` (the time the host will use); or the host starts the flight at the client's claimed tick by rewinding the level for the first segment (capped). |
| D | **Catches on moving carriers.** `FlightHistory` / `CatchResolver` record catch centres from the host's copies of remote players (§13.2). A receiver on a moving platform is offset by `v · (RTT + β)`. | Late or missed compensated catches on lifts and shuttles. | Medium | Stage 1 removes most of it (reconstructed riders). Stage 3 makes it exact. |
| E | **Pressure plates on clients.** `PressurePlate.FixedUpdate` (`Assets/Scripts/Zones/PressurePlate.cs:27-31`) computes `Active` on **every** peer from local copies. Only the host's value drives the actuator (`SignalActuator.cs:81-85`), but `SignalIndicator` (`Assets/Scripts/Zones/SignalIndicator.cs:33-34`) lights from the local value. | A client sees the plate lit while the door stays shut (or lit late or early). The rule is correct, the readout is not. | Low to medium | Stage 5: indicators of plates read the replicated host state (a replicated `Active`, or the actuator's `opening`), never a local guess. |
| F | **Host-decided triggers from remote copies.** Falling platforms (`FallingPlatform.cs:50-55`), checkpoints (`Checkpoint.cs:58-64`), finish (`FinishZone.cs:19-21`) and plates run `PlayerZone.Collect` on the host's lagging copies. | The owner's falling platform starts shaking about `RTT + β` after they step on it. A checkpoint or plate reacts late. Consistent everywhere afterwards (event time replicated as a server time), so this is felt latency, not divergence. | Low | Accept. With stage 3, optionally use the owner's tick stamp to back-date the trigger time (capped). |
| G | **Reset ordering.** A reset sets `sectionStart` (`RunManager.cs:166`, replicated by a `NetworkVariable`, `NetworkRunState.cs:34`) and teleports players by RPC (`TeleportOwner`, `NetworkPlayer.cs:125-128`). NGO does not guarantee that a variable delta arrives before an RPC sent in the same frame. | For a tick or two a client can stand at its respawn with platforms still on the old phase. Then everything snaps (`FrameDelta` ignores snaps over 2 m only; a smaller snap drags a local rider). | Low (to verify) | Stage 5: send the new section start (an epoch / reset count) with the teleport, and apply both together. |
| H | **Posture, aim and position on separate channels.** `moveState` and `pitch` are separate owner `NetworkVariable`s (`NetworkPlayer.cs:26-29`); position comes from `NetworkTransform`. | Slide or crouch posture and aim pitch are a tick off from the position at transitions. Also affects the host's catch-centre height (§13.1, last paragraph). | Low | Stage 6: one snapshot. |
| I | **Clock corrections on clients.** NGO corrects the client's `ServerTime` estimate. Every time-derived object jumps a little, and `FrameDelta` passes the jump to the local rider. | Rare micro-jitter on platforms and riders. | Low | Stage 2: monotonic, slewed section clock. |

Checked and **not** a problem:

- the bomb in flight vs. moving platforms on clients: the bomb is drawn about `β` late and the platform about `d_B`
  late, so they differ by about `β` only;
- bomb transit, gates, fuse: replicated as server times, so they replay consistently;
- the held bomb: attached to the hand locally;
- `UnityEngine.Random`: presentation only.

### 1.8 Verdict

- **"Platforms = f(shared time)": present and correct** (`MovingPlatform.cs:73-113`, `SectionClock.cs:12`,
  `RunManager.cs:53`, `RotatingObstacle.cs:18`, `SignalActuator.cs:46`). Rebuilding platforms on a new simulation
  manager would not fix the bug by itself.
- **Incomplete:**
  - there is no integer tick and no timestamp on player state;
  - above all, there is **no carrier-relative sync of riders** (`Player.prefab:698` `InLocalSpace: 0`, no carrier
    field in `NetworkPlayer`, the remote branch of `PlayerMotor.cs:98-104` ignores carriers).

  That missing piece is the actual cause of the offset.
- **Wider:** the same time-frame mismatch makes host decisions about remote players unreliable near anything that moves
  (§1.7 A, C, D) and makes a few client readouts wrong (§1.7 E, G). These are the "other bits" seen in playtests.

## 2. Target architecture

### 2.1 `SimulationManager`: a fixed-step clock that wraps NGO, not a second clock

Owns the game's notion of "now" for level motion. It replaces the body of `SectionClock` without changing its callers.

- **Source of truth:** NGO's network tick (`NetworkManager.ServerTime`, `TickRate` 30, could be raised to 60) and the
  host-replicated `sectionStart`. There is no second time sync: two clocks would just add a second error.
- **Exposes:**
  - `SectionTick` (int, ticks since section start);
  - `TickAlpha` (0..1, render fraction between ticks);
  - `SectionTime` (double) = `(SectionTick + TickAlpha) · tickDuration`.
- **Monotonic:** when NGO corrects the client's `ServerTime` estimate, the manager slews it over a few frames. The
  section time never jumps backwards, so `FrameDelta` and riders never jitter.
- Offline: driven by a local fixed accumulator, so tests and the sandbox behave the same.
- Reset: `sectionStart` changes → `SectionTick` restarts at 0 (current behaviour, kept).
- `SectionClock.Now` becomes a façade over it, so `AGENTS.md`'s rule still holds word for word.

### 2.2 `DeterministicPlatform`: the existing `MovingPlatform` contract, made explicit

Do **not** add a parallel component. `MovingPlatform` already is the target: a pure `Evaluate`, a virtual `PositionAt`
and the `Transform` as a mirror. The target exposes its parameters in the editor as follows.

| Requested parameter | Existing field / to add |
|---|---|
| origin | `waypointA` (already a reference, never a name) |
| amplitude | `waypointB − waypointA` (keep waypoints: builders and validators place them) |
| period | derived: `CycleDuration = 2·length / speed`. Optionally an explicit `period` override (one of the two). |
| motion type | `Motion.PingPong`, `Motion.Dwell` exist. A one-way `Loop` (A → B, wrap) is an optional addition that needs a spec decision first (no gameplay invention). |
| phase offset | `startPhase` (0..1) exists |

Contract to enforce (a test in `ObstaclePrefabTests` / `ObstaclePrefabValidator`):

- `Pose(t)` is a pure function of `(t, serialized parameters)`: no state, no `Time.*`, no `Random`;
- `Update` / `LateUpdate` only writes the mirror transform from `Pose(SimulationManager.SectionTime)`;
- a new `Pose(double t)` API returns position and rotation for **any** `t`. Rider reconstruction (§2.3) and lag
  compensation need it. `SignalActuator` and `FallingPlatform` get the same API over their replicated event time.
- each carrier has a **stable id** (`CarrierId`, a serialized int written by the course builders, unique per scene,
  checked by a test), so it can be referenced on the wire without making platforms `NetworkObject`s.

### 2.3 Players on a platform: relative position, rebuilt locally

**Owner side** (`PlayerMotor` already knows `ridingPlatform`):

- while grounded on a moving carrier, publish `RideState { carrierId, offset }`, where
  `offset = playerPosition − carrier.Pose(T_owner).position`;
- carriers translate riders only (`OBSTACLES.md`: "riders are carried by translation only"), so the offset is a plain
  world vector, with no rotation frame to sync;
- `carrierId = none` when airborne or on static ground.

**Transport:**

- Stage 1: an owner-written `NetworkVariable<RideState>` on `NetworkPlayer`, next to `moveState`. The world-space
  `NetworkTransform` stays for off-platform motion.
- Stage 3: folded into one tick-stamped player snapshot (atomic, no ordering issue between the two messages).

**Viewer side** (remote copy, after `NetworkTransform` has applied, in `LateUpdate`):

- while `carrierId` is valid, `position = carrier.Pose(T_viewer).position + smoothedOffset`. The viewer uses the
  **same** clock it uses to draw that platform, so the rider is glued to it whatever the latency;
- smooth the offset (it changes slowly while walking on the platform), never the absolute position;
- on boarding or leaving, blend between the relative and the world position over a short window (tuning in
  `GameTuning`, e.g. `riderBlendSeconds`), so a jump off an elevator does not pop.

**Host rules:** the host reconstructs remote riders the same way before its sweeps (plates, kill zones, checkpoints,
`FlightHistory`), so its decisions see the rider on the platform, not 0.5 m inside it.

**Scope:** `MovingPlatform` and the moving part of `SignalActuator`, optionally `FallingPlatform`. Conveyors need
nothing: the belt does not move, so world-space sync already matches.

### 2.4 Host decisions about remote players: rewind the level, not the player

Once player state carries the owner's `SectionTick` (stage 2), the host can ask the right question: "at tick `k`, the
tick this player saw, was the player inside the hazard posed at `k`?"

- Every moving thing exposes `Pose(t)` (§2.2), so the host needs **no history buffer for the level**. It evaluates the
  pose at `k`.
- Only players need a short history on the host (it already keeps one for catches, `FlightHistory`, §13.2). That
  history now stores tick-stamped snapshots, not host-frame samples.
- Lethal moving hazards (rotating bars with a child `KillZone`, crushers with `lethalWhileClosing`) stop deciding
  remote hits from `OnTriggerEnter` on the host's copy. The host runs an explicit overlap test at the snapshot tick:
  - for remote players only (the host's own player keeps today's path);
  - a static `KillZone` (water, void) gives the same answer at any tick and can keep its trigger.
- Same rewind, with a cap equal to `catchLagCompensation`, for catch centres on carriers and, optionally, back-dated
  trigger times of falling platforms and plates.
- Anti-abuse: the tick a client claims is clamped to `[hostTick − cap, hostTick]`. A client can only make itself
  *older*, never skip a hazard by claiming a future tick.

This keeps one decision point on the host (`AGENTS.md`) and keeps the decision fair to what the player saw.

### 2.5 Smaller fixes

- **Throw prediction** (§1.7 C): `PredictLocalThrow` samples moving obstacles (`Pose(t)`) at
  `T_client + RTT` (the host's time when the flight really starts) instead of the client's current time. If that is
  not enough, the host starts the flight at the client's claimed tick (rewind for the first, capped segment).
- **Plate readouts** (§1.7 E): `SignalIndicator` on a client reads the host's replicated state. Either replicate
  `PressurePlate.Active` or show the actuator's `opening`. A client never lights a plate from its own guess.
- **Reset ordering** (§1.7 G): the teleport RPC carries the new section start (or a section epoch), and the client
  applies both together. A client never shows a respawned player against the old platform phase.
- **One snapshot per player** (§1.7 H): position, carrier, yaw, pitch and `moveState` travel together (stage 6).

### 2.6 Determinism rules to respect

1. Level motion is a pure function of `SimulationManager` section time/tick and serialized parameters. Never
   `Time.time`, `Time.deltaTime` or accumulated per-frame state.
2. No PhysX for networked logic decisions about level objects: no forces, no non-kinematic bodies, no `MovePosition`
   integration. Colliders follow the mirror transform.
3. Simulation is never culled by visibility or distance. Culling may only touch renderers, VFX and audio.
4. Floats:
   - keep time differences in `double` until the final subtraction;
   - evaluate from `SectionTick` (int) times the tick duration instead of summing deltas;
   - clamp and normalise phases with `Mathf.Repeat` over the cycle, not over the absolute time;
   - never compare poses for equality across machines (tolerances only).
5. Randomness in gameplay code (if any is ever added) uses a seed from the host (e.g. section start tick). Presentation
   `Random` is fine.
6. A reconstructed rider uses the viewer's **own** clock, the one that drew the platform. Mixing the owner's timestamp
   with the viewer's platform recreates the bug.
7. Teleports keep going through `Player.TeleportTo` / `SyncTeleport` and clear `RideState`.
8. All new constants (blend times, tolerances) live in `GameTuning`.

## 3. Staged plan

| # | Stage | Fixes (§1.7) | Content | Effort | Risks |
|---|---|---|---|---|---|
| 0 | **Measure** | all | Dev overlay / log (`HotPatata.DebugTools`): for each remote rider, the distance between its drawn feet and the carrier's top surface. For each host hazard kill of a remote player, the distance on the owner's side at the same tick (to count false kills). For each thrown bomb, prediction vs. host verdict. Bots ride an elevator, cross a rotating bar and throw through a moving gate in `PassSandbox`, with `-patataLatency 0/100/200`, host→client and client→client. | 1–2 days | Low. `PlayerBot` needs a few scripted modes (idle on carrier, cross a bar). |
| 1 | **Relative riders (minimal fix for the reported bug)** | B, most of D | `IPlatformCarrier.Pose(t)` + stable `CarrierId` (builders write it, a test checks uniqueness). `RideState` owner variable. Remote reconstruction + board/leave blend. The host uses the reconstructed position. Platforms stay as they are (already f(time)). | 2–4 days | **Medium.** `NetworkTransform` and `RideState` arrive in separate messages: boarding or leaving can pop for one tick (mitigated by the blend). Host-rule changes need `PassFeelTests` and the bot catch runs of `TESTING.md`. Builders must be re-run for ids (`Rebuild All Courses`). |
| 2 | **Explicit tick, monotonic clock, tick-stamped player state** | I, prepares A/C/D | `SimulationManager` (§2.1), `SectionClock` as a façade, slewed corrections. The owner's `SectionTick` travels with its state (in `RideState`, or a small owner variable until stage 6). Optional `TickRate` 60. | 2–3 days | Low to medium. Every time-derived object (rotating bars, actuators, falling platforms, bomb transit, gates) shifts to the new clock: run every course test suite. 60 Hz doubles transform bandwidth. |
| 3 | **Host rewinds the level for remote-player decisions** | A, D, (F) | §2.4: overlap tests for lethal moving hazards at the snapshot tick, a tick-stamped `FlightHistory`, a claim cap from `GameTuning`. | 3–5 days | **Medium.** Touches failure rules (the core of the game): EditMode tests for the pure overlap-at-tick function, PlayMode bots, `PassFeelTests`. The cap must stay small enough that no one "survives" a hazard on a stale tick. |
| 4 | **Throw prediction at host time** | C | §2.5: prediction samples moving obstacles at `T_client + RTT`. Fallback: the host starts the flight at the claimed tick. | 1–2 days (3–4 with the fallback) | Low to medium. RTT jitter makes the prediction approximate: keep today's "wait for the verdict" stop. |
| 5 | **Readouts and reset ordering** | E, G | §2.5: plate indicators from host state; teleport RPC carries the section epoch. | 1–2 days | Low. |
| 6 | **Custom player snapshots** | H, B transitions | Replace the player `NetworkTransform` by one tick-stamped snapshot: world or carrier-relative position, yaw, pitch, `moveState`, with its own interpolation buffer. Atomic carrier transitions, delta compression. | 1–2 weeks | **Medium to high.** Re-implements what NGO gives (interpolation, thresholds, teleport flag, late joiners). Needs its own latency and packet-loss tests. Worth it only if stage 1 transitions stay visibly bad. |
| 7 | **Full deterministic lockstep or rollback (GGPO-style)** | (all, in theory) | Input-only networking, all gameplay in fixed ticks on every peer, state save/restore, re-simulation. | **Months: a rewrite** | **Very high, not recommended.** Reasons below. |

Suggested order:

- 0 → 1 → 2 → 3: fixes the reported bug and the unfair hazard kills, about 2 weeks;
- then 4 and 5: polish, about 1 week;
- 6 only if playtests still show transitions;
- 7 not at all.

Why stage 7 is a rewrite and not a step:

- **Movement is not deterministic.** `PlayerMotor` runs on `CharacterController` (PhysX), per-frame `deltaTime`,
  coyote/jump timing on `Time.time`. Lockstep and rollback need bit-identical results on every machine (x64 Mono vs
  IL2CPP, Windows/Linux/macOS builds all ship). That means a custom fixed-point or strictly controlled float kinematic
  collision layer instead of PhysX.
- **The bomb runs on host physics** (`BombPhysics`, zone sweeps, `CatchResolver`). It would also need to be
  deterministic and re-simulatable.
- **Lockstep delays inputs by about one RTT.** In a first-person parkour game that is felt on every jump and turn. It
  defeats the current owner-authoritative movement, which has zero input lag.
- **Rollback** removes the input delay but needs to re-simulate N frames of every player, the bomb and every stateful
  `IResettable` each time a late input arrives. All of it must be serialisable, and rollback visual corrections on
  remote players are what this issue complains about, in another form.
- It contradicts the documented authority model (`AGENTS.md`: "Host authoritative, single decision point") and would
  touch almost every gameplay class and test suite.

The problems in §1.5 and §1.7 are about *drawing and judging* remote players at the right time, not about the
simulation diverging. Stages 1–5 fix them inside the current architecture.

## 4. Validation criteria

Run each with the Network Simulator (`-patataLatency`, `docs/TESTING.md`), in two instances (host + client) and in
three instances (client → client via host), at 0, 100, 200 and 300 ms RTT.

1. **Idle rider:** a player standing still on an elevator (vertical `Dwell`) and on a horizontal `PingPong` platform.
   On every other peer, for a full cycle:
   - the drawn feet stay within **5 cm** of the platform's top surface;
   - the rider never goes through the platform at a direction reversal.
2. **Walking rider:** a player walking across a moving platform stays on it on every peer; no lateral slide relative
   to the platform beyond the walking motion.
3. **Board / leave:** jumping onto and off a moving platform shows no pop larger than the blend allows (no teleport,
   no one-frame jump of more than 0.3 m).
4. **Reset:** after a section reset (platform phase restart), riders and platforms realign within one tick. No rider is
   dragged by the snap (current `FrameDelta` guard kept).
5. **Clock correction:** forcing a client time resync (latency change mid-ride) never moves a platform backwards and
   never makes a rider jitter by more than 5 cm.
6. **Host rules:** a plate with `countCarrier` riding a moving actuator, a checkpoint on a lift and a catch on a moving
   platform give the same results with a remote player as with the host's own player. `PassFeelTests` and the bot
   catch runs of `TESTING.md` keep their current pass rates.
7. **Hazards:** a remote bot crossing a rotating bar or a crusher at the edge of its timing.
   - The host's verdict matches what the owner saw in at least 95% of runs at 200 ms RTT. Today's rate is measured in
     stage 0.
   - No team reset for a hit the owner did not see at 0–100 ms.
8. **Throw prediction:** a throw through a moving gate or past a rotating bar. The predicted arc and the host's
   verdict agree in at least 95% of runs at 200 ms RTT.
9. **Plates:** a client's plate indicator never shows "held" while the host's actuator is closing (and the reverse),
   beyond one tick.
10. **No regressions:**
   - offline play unchanged;
   - `ObstaclePrefabTests` (including the new pure-pose and unique-`CarrierId` checks);
   - every course test suite;
   - `CourseContractCheck` reports nothing.

## 5. Docs to update when a stage ships

- `ARCHITECTURE.md` §13.1: authority table row "Player movement" (rider sync), and the "Level objects are kept in
  sync" paragraph.
- `ARCHITECTURE.md`, the moving platform paragraph near "Must be deterministic enough for networked play": the
  `Pose(t)` contract and `CarrierId`.
- `OBSTACLES.md` §4: a custom carrier must implement `Pose(t)` and get a `CarrierId`.
- `TESTING.md`: the rider-offset overlay, the bot mode, the latency matrix above.
- `ARCHITECTURE.md` §13.2: tick-stamped `FlightHistory`, the hazard rewind and its cap (stage 3).
- `AGENTS.md` / `SectionClock` rule: point to `SimulationManager` once stage 2 lands (keep section numbers).
