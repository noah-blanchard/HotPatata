# HotPatata — MVP_TASKS.md

> Ordered execution plan for a coding agent.  
> Read [`PROJECT_SPEC.md`](PROJECT_SPEC.md) and [`ARCHITECTURE.md`](ARCHITECTURE.md) before starting.  
> Do not skip acceptance criteria.  
> Do not add out-of-scope gameplay while completing these tasks.

---

# Status (last updated 2026-10-10)

A ticked box is verified by the automated tests (see [`TESTING.md`](TESTING.md)), by recorded bot/latency runs, or
by the owner's own play. An unticked box in a finished milestone is a **human gate**: it needs a group or external
playtest and cannot be closed by code.

| Milestone | State | Still open |
|---|---|---|
| M0 Project baseline | done | |
| M1 Local pass sandbox | done | M1.11 proof-of-fun gate: owner found it fun, external playtest pending (M7.4) |
| M2 Greybox prefab kit | done | |
| M3 Network proof | done (host authority, lag-compensated catch) | M3.7: test on two physical machines |
| M4 Session and lobby | done, verified with real Relay sessions | |
| M5 First greybox course | done, then extended to Acts 2–3 (~680 m, 7 checkpoints); the course was removed in the cleanup (layout archived in OBSTACLES.md §3.2) | |
| M6 UX and readability | partly covered by earlier work (see each task); UI Toolkit base (#14), main menu and lobby (#15), pause menu (#16), settings screen (#17) and results screen (#23) in place, their layouts in UXML editable in UI Builder (#76), the "Sunny toy box" look with a living 3D menu backdrop, the in-world menu (#79: stations with camera travel, the lobby on the island) | verify M6.1/M6.2 at range, player-facing progress UI, ping only if needed |
| M7 Test and harden | not started (bot latency runs cover part of M7.3) | all |
| M8 Post-validation polish | gated on M7.4; some pulled forward through M9 | |
| M9 Movement and look pass | done (pulled forward by the owner) | |
| M10 Bomb obstacles (#68) | done (pulled forward by the owner, M7.4 gate lifted for it); `PlaytestCourse` removed in the cleanup, the obstacles live on in the kept courses | group playtest of the obstacles (now in PatataWilds and the plant) |
| Spike #87 industrial look | the full closed `IndustrialPlant` (11 rooms, 9 checkpoints, every obstacle) built in the industrial look, with a material library, anti-tiling and decals (ARCHITECTURE §25.2) | play-test, real textures per surface (optional), performance on a player build, adopt/revise/reject decision |
| M11 Second course (#83) | factory built, then removed in the cleanup; its layouts became the industrial plant's | |
| M12 Third course: PatataWilds | built: 25 sections in 5 acts, nature look from Poly Haven, campfire checkpoints, dawn to dusk; automated verification in M12.8 | 2-player 20+ min clear, host + 3 clients, 1080p/60 fps, readability review |
| M13 Fourth course: PatataCanopy | built: 25 sections in 5 acts in the tree tops, every section with a contract; three new generic systems (body screen, hands-free plate, switch) and the section-contract check; automated verification in M13.6; atmosphere pass built (M13.7: its own cooler day, a height mist, giant trees, a forest floor that follows the course, leaf roofs, god rays, fireflies) | 2-player clear, every puzzle forced and fun at 2 and at 4, host + 3 clients, 1080p/60 fps, readability review (in the mist) |
| M14 PatataWilds contracts | not started | give every PatataWilds section a contract and close its shortcuts (M14) |
| M15 Fifth course: PatataTemple | built: 15 sections in 3 acts for **three players**, every section with a contract that says why two fail; four systems for three (heavy plate, hourglass plate, sun beam, pivot carrying its riders), a level's minimum players in the menu; its own valley, pyramid, day and storm; automated verification in M15.6 | a clear by three, every puzzle failing with two and fun with four, host + 2 clients, 1080p/60 fps, readability review (the storm) |
| Cleanup and generic obstacles | done: only `PatataWilds`, `IndustrialPlant` and `PassSandbox` (plus `Bootstrap`) remain; every obstacle system is generic (references, `IObstacleState`, `ObstacleVisualDriver`, `CustomVisual`, overridable poses, `ObstaclePrefabValidator`), with two example variants; the obstacle catalogue is `docs/OBSTACLES.md` | |

**Next up:** the group playtest of PatataCanopy (M13.6) and of PatataTemple with three (M15.6), then M14 (PatataWilds contracts), the group playtest of PatataWilds
and the plant (M12.8, M10.4), then M6 and M7 (M7.4 external playtest gates any other
new mechanic). M3.7 is the oldest open gate.

---

# Agent execution rules

For every task:

1. inspect existing implementation before changing files;
2. make the smallest coherent change;
3. compile;
4. inspect Unity Console;
5. fix compilation/setup errors caused by the change;
6. perform the stated validation when possible;
7. save affected scenes/prefabs;
8. only then proceed.

If Unity Editor / MCP tooling is available:

- use it for scene creation;
- use it for prefab creation;
- use it for component assignment;
- use it to inspect hierarchy and Console;
- prefer it over raw scene YAML editing.

---

# Milestone 0 — Project baseline

## M0.1 — Validate project version and packages

**Goal:** Establish a clean Unity project.

Required:

- Unity 6.3 LTS project;
- Input System;
- no compile errors.

Networking packages may be installed now, but networking implementation waits until Milestone 3.

**Acceptance criteria**

- [x] project opens without compilation failure;
- [x] Input System package is available;
- [x] project can enter Play Mode;
- [x] Console has no recurring exceptions in an empty scene.

---

## M0.2 — Create folder structure

Create:

```text
Assets/
├── Prefabs/
│   ├── Player/
│   ├── Bomb/
│   ├── Gameplay/
│   ├── Platforms/
│   └── Obstacles/
├── Scenes/
├── Scripts/
│   ├── Core/
│   ├── Networking/
│   ├── Player/
│   ├── Bomb/
│   ├── Run/
│   ├── Obstacles/
│   ├── UI/
│   └── Debug/
├── ScriptableObjects/
│   └── Tuning/
└── Tests/
    ├── EditMode/
    └── PlayMode/
```

**Acceptance criteria**

- [x] folders exist;
- [x] no unnecessary framework folders are added.

---

## M0.3 — Create tuning asset

Create a ScriptableObject-based tuning configuration.

Minimum values:

- move speed;
- acceleration;
- braking;
- air control;
- jump;
- coyote time;
- jump buffer;
- hold fuse = 6.0 s;
- warning phase = 2.0 s;
- catch grace = 0.35 s;
- throw speed;
- catch radius;
- reset delay = ~1.0 s.

**Acceptance criteria**

- [x] all values can be changed from Inspector;
- [x] gameplay scripts do not need duplicated hard-coded tuning constants.

---

# Milestone 1 — Local pass sandbox

**Milestone exit condition:** Two players/instances can run, jump, throw and catch one bomb; the bomb explodes on environment contact or fuse expiry; reset is fast.

---

## M1.1 — Create `PassSandbox` scene

Create a greybox scene containing:

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
└── Lighting
```

Geometry requirements:

- large flat starting area;
- two raised platforms;
- at least one 6–8 m pass opportunity;
- no decorative art needed.

**Acceptance criteria**

- [x] scene is saved as `Assets/Scenes/PassSandbox.unity`;
- [x] geometry has appropriate colliders;
- [x] spawn markers are visible in Scene view;
- [x] no scene errors.

---

## M1.2 — Implement basic first-person player movement

Create a reusable player prefab.

Required:

- move;
- look;
- jump;
- coyote time;
- jump buffer;
- moderate air control.

Do not implement:

- sprint stamina;
- crouch;
- combat;
- ragdoll movement.

**Acceptance criteria**

- [x] player can traverse the sandbox reliably;
- [x] jump feels responsive;
- [x] player does not slide excessively after releasing movement;
- [x] player can adjust modestly in air;
- [x] no obvious collider snagging on flat geometry.

---

## M1.3 — Create Player prefab hierarchy

Target structure:

```text
Player
├── Visual
├── CatchVolume
├── NameplateAnchor
└── CameraTarget
    ├── ThrowOrigin
    └── HandAnchor
```

The camera sits at `CameraTarget` (first person). `ThrowOrigin` and `HandAnchor` are children of it so the
holder sees the bomb in their hand; no hand models yet.

Attach required movement/input components.

**Acceptance criteria**

- [x] prefab exists under `Assets/Prefabs/Player/`;
- [x] all anchor references are assigned;
- [x] prefab can be instantiated without missing references.

---

## M1.4 — Create Bomb prefab and state machine

Create:

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

Required states:

- Held;
- Thrown;
- CaughtGrace;
- Exploding;
- Resetting.

**Acceptance criteria**

- [x] state is inspectable/debuggable;
- [x] bomb can attach to a player hand anchor;
- [x] bomb can detach into physics;
- [x] no world-contact failure while correctly held;
- [x] state transitions are logged in development builds/editor.

---

## M1.5 — Implement throw

Required:

- carrier can release bomb;
- throw originates from `ThrowOrigin` or hand;
- default pass should comfortably reach approximately 8–12 m;
- throw works while moving;
- throw works while airborne.

Throw is chargeable (decided after the first playtest): hold to charge, release to throw, tap = shortest pass;
speed runs from `throwSpeedMin` to `throwSpeedMax` over `throwChargeTime`. Show a charge bar UI indicator.

**Acceptance criteria**

- [x] bomb leaves hand cleanly;
- [x] bomb does not collide instantly with carrier;
- [x] throw direction follows aim predictably;
- [x] the same input gives repeatable results.

---

## M1.6 — Implement catch volume and catch resolver

Required:

- receiver has front-biased catch volume;
- thrown bomb can be accepted;
- valid catch changes carrier;
- bomb snaps to receiving hand;
- fuse refreshes;
- CaughtGrace begins.

Starting values:

- catch radius roughly 0.5–0.7 m (starting value 0.6 m);
- catch is timed, not automatic: the receiver presses catch and the bomb must reach them inside the
  `catchWindowDuration` (0.25 s) window; `catchCooldown` (0.5 s) stops button mashing;
- caught grace 0.35 s.

**Acceptance criteria**

- [x] moving receiver can catch;
- [x] jumping receiver can catch;
- [x] successful catch is obvious;
- [x] same catch cannot resolve twice;
- [x] catch does not immediately produce an environment explosion from overlapping geometry.

---

## M1.7 — Implement lethal world contact

Critical rule:

> While `Thrown`, contact with invalid environment means immediate failure.

Required:

- floor contact explodes;
- wall contact explodes;
- platform contact explodes;
- obstacle contact explodes;
- kill zone explodes.

No bounce.

**Acceptance criteria**

- [x] deliberately throw at floor → explosion;
- [x] deliberately throw at wall → explosion;
- [x] miss receiver and hit platform → explosion;
- [x] valid player catch does not explode;
- [x] failure reason is logged.

---

## M1.8 — Implement hold fuse

Starting values:

- 6.0 s total;
- warning phase final 2.0 s.

Required:

- timer active while held;
- catch refreshes timer;
- expiry triggers explosion;
- timer normalized on reset.

**Acceptance criteria**

- [x] holding bomb continuously causes failure;
- [x] successful catch restores full hold window;
- [x] no stale fuse continues after reset;
- [x] fuse cannot expire during reset state.

---

## M1.9 — Implement beep and bomb visual pulse

Audio stages:

- calm;
- medium;
- urgent;
- critical.

Visual pulse must correspond to urgency.

Placeholder audio is acceptable.

**Acceptance criteria**

- [x] player can estimate urgency without a number;
- [x] warning becomes clearly stronger near expiry;
- [x] catch sound is distinct;
- [x] explosion sound is distinct.

---

## M1.10 — Implement local section reset

Required flow:

1. explosion;
2. short lockout;
3. reset players;
4. reset bomb;
5. clear velocities;
6. resume.

Target interruption:

- approximately 1 second.

**Acceptance criteria**

- [x] all players return to configured spawn points;
- [x] bomb has zero stale velocity;
- [x] bomb has valid carrier or configured start state;
- [x] fuse is full;
- [x] player control returns;
- [x] repeated failures do not corrupt state.

---

## M1.11 — Proof-of-fun gate

Do not proceed to course content until this test is possible:

> Two players stand 6–8 m apart and pass the bomb for 30 seconds while moving and jumping.

Evaluate:

- fairness of misses;
- throw readability;
- catch forgiveness;
- fuse stress;
- reset speed.

**Exit criteria**

- [ ] passing is understandable;
- [ ] catches do not feel random;
- [ ] floor-contact rule is immediately understood;
- [ ] reset is fast enough to encourage retry.

If these fail, tune before proceeding.

> **Status (open, human gate):** the owner's own playtests were positive and led to the throw/catch rework
> (charged throw, timed catch, receiver-side forgiveness, `PassFeelTests`). Close with the external playtest (M7.4).

---

# Milestone 2 — Reusable greybox prefab kit

**Milestone exit condition:** The agent can assemble course sections using reusable gameplay prefabs rather than one-off scene objects.

---

## M2.1 — Create `Platform_Basic`

Requirements:

- clean root;
- `Visual` child;
- collider;
- scalable dimensions;
- no unnecessary script.

**Acceptance criteria**

- [x] saved under `Assets/Prefabs/Platforms/`;
- [x] scaling does not create broken collision.

---

## M2.2 — Create `Platform_Narrow`

Variant intended for later precision sections.

Keep initial default forgiving.

**Acceptance criteria**

- [x] reusable;
- [x] visually distinguishable in greybox if useful.

---

## M2.3 — Create `Platform_Moving`

Structure:

```text
Platform_Moving
├── Visual
├── Collision
├── Waypoint_A
└── Waypoint_B
```

Expose:

- speed;
- endpoints;
- optional start phase.

**Acceptance criteria**

- [x] moves repeatedly between endpoints;
- [x] player can stand on it without obvious instability;
- [x] movement can be reset;
- [x] values editable in Inspector.

---

## M2.4 — Create `Obstacle_RotatingBar`

Expose:

- axis;
- speed;
- phase.

**Acceptance criteria**

- [x] deterministic continuous rotation;
- [x] collider matches visible bar;
- [x] reset is consistent.

---

## M2.5 — Create `Platform_Falling`

Required:

- trigger;
- warning delay;
- collapse;
- reset support.

**Acceptance criteria**

- [x] first qualifying interaction starts collapse;
- [x] platform restores correctly after section reset.

---

## M2.6 — Create `KillZone`

Required:

- player entering it reports player fall/reset behavior;
- bomb entering it causes authoritative bomb failure.

**Acceptance criteria**

- [x] bomb cannot disappear forever below level;
- [x] player fall behavior is deterministic.

---

## M2.7 — Create `Checkpoint`

Structure:

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

**Acceptance criteria**

- [x] stores/identifies player respawn slots;
- [x] activates only when required condition is met;
- [x] RunManager can reset to it.

---

## M2.8 — Create `FinishZone`

Required:

- tracks required players;
- reports course completion.

**Acceptance criteria**

- [x] one player alone cannot finish a multi-player run;
- [x] completion fires once.

---

# Milestone 3 — Network proof

**Milestone exit condition:** Host + client on separate instances can perform the core pass loop reliably.

---

## M3.1 — Install/configure NGO and Multiplayer Services

Required packages:

- Netcode for GameObjects;
- Multiplayer Services;
- local multi-instance testing package/tooling if useful.

**Acceptance criteria**

- [x] project compiles;
- [x] networking initialization succeeds;
- [x] no obsolete duplicate Lobby/Relay package architecture is introduced unnecessarily.

---

## M3.2 — Create network bootstrap

Implement minimal session/network startup architecture.

Do not build polished menu yet.

**Acceptance criteria**

- [x] one instance can host;
- [x] second instance can connect;
- [x] disconnect is handled without permanent editor breakage.

---

## M3.3 — Network player spawning

Required:

- one player object per connection;
- local player owns local input;
- remote players are visible.

**Acceptance criteria**

- [x] host and client see both players;
- [x] each controls only their own character;
- [x] no duplicate camera/input activation on remote players.

---

## M3.4 — Network bomb authority

Move bomb state authority to host/server path.

Required:

- one current carrier;
- authoritative throw;
- authoritative catch;
- authoritative fuse;
- authoritative explosion.

**Acceptance criteria**

- [x] clients cannot independently assign carrier;
- [x] both clients agree on carrier;
- [x] both clients agree on explosion;
- [x] repeated passes do not duplicate bomb.

---

## M3.5 — Network catch validation

Test specifically under latency.

Required:

- host resolves catch once;
- client receives replicated result;
- stale collision must not produce obviously unfair post-catch explosion.

**Acceptance criteria**

- [x] host→client pass works;
- [x] client→host pass works;
- [x] client→client via host authority works if 3+ players available;
- [x] no common “caught then exploded anyway” race.

---

## M3.6 — Network reset

Required:

- explosion causes one authoritative section reset;
- all clients teleport/reset consistently;
- bomb normalizes once.

**Acceptance criteria**

- [x] no duplicate reset calls;
- [x] all clients resume same section;
- [x] bomb carrier/fuse match after reset.

---

## M3.7 — Remote-machine validation gate

Before substantial course work:

- test on two physical machines or realistically separated network conditions.

Observe:

- catch fairness;
- throw latency;
- state races;
- reset consistency.

**Exit criteria**

- [ ] core loop remains playable online;
- [ ] no critical authority race remains unresolved.

> **Status (open):** verified only with several processes on one machine, with bots and the Network Simulator
> (see [`TESTING.md`](TESTING.md)). Catch lag compensation (M3.5) holds 20/20 and 22/22 catches at ~230 ms RTT.
> Two physical machines are still to be tested.

---

# Milestone 4 — Session and lobby

---

## M4.1 — Create `Bootstrap` scene

Responsibilities:

- initialize services;
- preserve session manager;
- route scenes.

**Acceptance criteria**

- [x] clean startup;
- [x] no duplicate persistent managers after scene changes.

---

## M4.2 — Create session

Host flow:

- create;
- receive/display join code;
- enter lobby.

**Acceptance criteria**

- [x] host can create session repeatedly after returning to menu.

---

## M4.3 — Join by code

Client flow:

- enter code;
- join;
- enter lobby.

**Acceptance criteria**

- [x] invalid code gives readable error;
- [x] valid code connects.

---

## M4.4 — Lobby player list

Display:

- connected players;
- host marker;
- optional ready state.

Keep UI utilitarian.

**Acceptance criteria**

- [x] list updates on connect/disconnect.

---

## M4.5 — Host start

Host launches `PassSandbox` or `PrototypeCourse`.

**Acceptance criteria**

- [x] all connected clients load intended scene;
- [x] all spawn once;
- [x] bomb starts once.

---

> **Status:** Milestone 4 is implemented and verified: online session with a shareable code (Relay), join by code with readable errors, lobby player list (updates on join/leave), host Start, repeated create/leave.

# Milestone 5 — First complete greybox course

**Milestone exit condition:** 2–4 players can complete a 3–5 minute course with checkpoints.

---

## M5.1 — Beat A: Safe Court

Build from reusable prefabs.

Required:

- safe 6 m pass;
- clear carrier/receiver setup;
- low failure cost.

**Acceptance criteria**

- [ ] new player can understand a basic pass.

---

## M5.2 — Beat B: First Gap

Required:

- receiver crosses;
- pass across a short pit;
- broad receiving platform.

**Acceptance criteria**

- [ ] pass, not jump precision, is main difficulty.

---

## M5.3 — Beat C: Stair Relay

Required:

- at least three vertical levels;
- alternating receiver positions.

**Acceptance criteria**

- [ ] multiple players must reposition;
- [x] bomb progresses upward through passes.

---

## M5.4 — Checkpoint 1

Place checkpoint after initial teaching beats.

**Acceptance criteria**

- [x] full team activates;
- [x] reset returns here after later failure.

---

## M5.5 — Beat D: Moving Pair

Use moving-platform prefab.

**Acceptance criteria**

- [ ] players can wait for safe window or attempt riskier timing;
- [x] movement is network-consistent enough to catch.

---

## M5.6 — Beat E: Split Lanes

Required:

- parallel paths;
- visual line of sight;
- one or more openings for cross-pass.

**Acceptance criteria**

- [x] bomb must cross between lanes;
- [ ] all players remain involved.

---

## M5.7 — Checkpoint 2

Same acceptance requirements as previous checkpoint.

---

## M5.8 — Beat F: Vertical Catch

Required:

- receiver performs jump/launch movement;
- bomb can be caught around apex;
- landing area remains forgiving.

**Acceptance criteria**

- [ ] success requires moving catch;
- [x] failure is readable.

---

## M5.9 — Beat G: Final Sprint

Required:

- several fast handoffs;
- escalating pressure;
- no new complicated mechanic.

Optional after testing:

- local section fuse override around 4.5 s.

**Acceptance criteria**

- [ ] ending feels faster/more urgent than opening;
- [ ] difficulty comes from learned mechanics.

---

## M5.10 — Finish zone and replay

Required:

- all players finish;
- show completion;
- show run time;
- optional reset count;
- replay/rematch.

**Acceptance criteria**

- [x] same group can restart without recreating project/session manually.

---

> **Status:** Milestone 5 is implemented (course beats A-G, three checkpoints, finish and rematch) and was later
> extended with Acts 2–3 (~680 m, 7 checkpoints, see [`ARCHITECTURE.md`](ARCHITECTURE.md) §4). The unticked boxes need a
> group playtest of the full course.

# Milestone 6 — UX and readability

---

## M6.1 — Carrier indicator

Show current holder clearly.

> **Status (implemented, to verify):** `PlayerPresentation` shows a bobbing, spinning indicator above the carrier
> that pulses on catch, shaped like the carrier's slot (● ▲ ■ ◆, `ARCHITECTURE.md` §25). It is driven by
> `BombController.CarrierChanged`, so it also works on remote mirrors.

**Acceptance criteria**

- [ ] visible at useful gameplay distance;
- [ ] updates immediately after catch.

---

## M6.2 — Catchable/receiver indicator

Prototype visual feedback for valid receiver / catch-ready state.

> **Status (implemented, to verify):** `AimReticle` shows a "CATCH!" marker to the intended receiver
> (`BombController.IntendedReceiver`), catch-window brackets and cooldown, and `PlayerCatcher.Hint` ("Too late by N ms").

**Acceptance criteria**

- [ ] helps passing without making throws automatic;
- [ ] no confusing false-positive state.

---

## M6.3 — Checkpoint/course progress

Minimal UI only.

> **Status (open):** only the dev `DebugHud` shows the checkpoint; `RunResultsUI` shows time and resets at the finish.

**Acceptance criteria**

- [ ] players know section progression without HUD clutter.

---

## M6.4 — Ping / “throw to me”

Only implement if silent playtests show need.

**Acceptance criteria**

- [ ] visible/audible but not spam-heavy;
- [ ] identifies requesting player.

---

## M6.5 — Basic accessibility settings

At minimum prepare:

- camera shake amount;
- flash/reduced-flash option;
- bomb warning volume;
- visual warning independent of color alone.

> **Status (done, to check in play):** in `GameTuning`, `viewEffectsStrength` scales every camera effect (shake, bob,
> roll, FOV), `flashReduction` dims every flash, and `beepVolume` sets the bomb warning volume. The fuse warning uses
> pulse speed, sparks and beep cadence, and hazards are striped, so neither relies on colour alone. The per-player layer
> (`Settings`, ARCHITECTURE §6.1) saves the values per machine and is read instead of the shared asset. The settings
> screen (#17, ARCHITECTURE §6.2), reachable from the main menu and the pause menu (#16), exposes all of them plus
> look, audio (master, effects through the `HotPatataMixer` SFX group) and display: each change applies at once and is
> saved when the screen closes. Still to check by hand: effects 0 = a steady camera and flash reduction 1 = dimmed
> flashes, in play; every setting survives a restart.

---

# Milestone 7 — Test and harden vertical slice

---

## M7.1 — Repeated reset stress test

Perform many consecutive failures.

**Acceptance criteria**

- [ ] no stale velocity;
- [ ] no duplicate bomb;
- [ ] no missing carrier;
- [ ] no frozen controls;
- [ ] no accumulating exceptions.

---

## M7.2 — Player-count test

Test:

- 2 players;
- 3 players;
- 4 players.

**Acceptance criteria**

- [ ] course remains completable;
- [ ] spawn/checkpoint logic handles each count;
- [ ] finish condition uses active required players.

---

## M7.3 — Network instability test

Simulate or test higher latency if tooling permits.

Observe:

- catch;
- ownership;
- thrown motion;
- reset.

**Acceptance criteria**

- [ ] no common catastrophic desync;
- [ ] critical gameplay decisions remain authoritative.

> **Status (partial):** bot runs under the Network Simulator: RTT 5 ms 32/32 catches, 90 ms 26/26, ~230 ms 20/20
> (22/22 incl. client→client). About 450 ms RTT still fails (0/14): it is beyond the `catchLagCompensation` cap
> (0.35 s). Ownership, thrown motion and reset under latency still need a dedicated pass.

---

## M7.4 — First external playtest

Owner playtest note (2026-09-27): the owner played Act 1 and found it fun, and approved new obstacles and a longer
course (Acts 2–3: conveyors, pistons, windmill, sweepers, crushers, elevators, mega slide with hoops). The external
playtest questions below still apply to the whole course.

Questions:

1. Did players understand world-contact failure after one mistake?
2. Did they naturally call for passes?
3. Could they tell when a catch succeeded?
4. Did missed catches feel fair?
5. Was the fuse exciting or annoying?
6. Was anyone idle for long stretches?
7. Did players want another run?

Record:

- failure causes;
- repeated confusing moments;
- sections where only one player matters;
- latency complaints;
- desired tuning changes.

Do not add new mechanics until these observations are reviewed.

Exception (owner decision, #68): the bomb obstacles of M10 are built before this playtest and may go into a course
(`PlaytestCourse`), so the external playtest covers them too.

---

# Milestone 8 — Only after core validation

Do not begin this milestone unless the greybox is repeatedly fun.

Possible work:

- character art;
- polished bomb model;
- animation;
- VFX;
- environment theme;
- final audio;
- lobby polish;
- rematch polish;
- Steam integration;
- additional courses;
- alternate modes.

These are deliberately outside the initial execution path.

---

# Milestone 9 — Movement & look pass (pulled forward by the owner)

Deliberately taken on before the M6/M7 gates, at the owner's request: fluid movement first, then a cartoon look pass.

## M9.1 — Fluid movement (sprint, slide, mantle)

Acceptance criteria:

- turning carves instead of snapping; sharp reversals still brake crisply;
- hold sprint (forward-ish) reaches 11 m/s; letting go bleeds speed off smoothly;
- crouch at speed slides with a boost that decays in about a second; boosts cannot be chained (cooldown, cap);
- slide-jump keeps the slide speed; landings keep momentum; no bunny-hop gain;
- a low ceiling keeps the player crouched until clear;
- moving into a ledge up to 1.4 m above the feet while airborne mantles onto it; taller walls and hazards are never mantled;
- slide/crouch posture (capsule, eye, catch sphere, pose) is identical on every machine;
- throws inherit at most 11 m/s of run speed (passes stay predictable);
- `SprintSlideTests` and `MovementTests` pass.

## M9.2 — Course retune for the new movement

- every beat is re-measured against run / sprint / slide-jump reach;
- at least one beat needs a slide (low bar) and one offers a mantle ledge;
- pass paths stay clean (spec §3).

## M9.3 — Cartoon look, speed feel, potato VFX, audio

Done: toon shader, sky, post-processing, speed lines, trail, fuse sparks, cartoon explosion. Movement sounds were
removed on purpose afterwards; only the bomb sounds remain. Details in [`ARCHITECTURE.md`](ARCHITECTURE.md) §8.6 and §25.

> **Status:** M9.1–M9.3 are done (PRs #7–#10).

---

# Milestone 10 — Bomb obstacles (#68, pulled forward by the owner)

Obstacles that use the pass itself, decided in the #68 brainstorm. Rules: PROJECT_SPEC §5 (`InTransit`), §7.3,
§12.3, §13.13–§13.17. The M7.4 gate is lifted for this milestone. Architecture: ARCHITECTURE §10.6; course: §4.

> **Status:** M10.1–M10.4 are built and covered by `ZoneRuleTests`, `BombObstacleTests` and `PlaytestCourseTests`.
> Open: the group playtest of the course, and a two-machine check of the replicated gates, actuators and transits.

## M10.1 — Zones: fuse zones and laser curtains

Acceptance criteria:

- a carrier entering a forbidden zone explodes the bomb; a player without the bomb crosses it freely;
- a hot zone burns the fuse twice as fast, a cold zone half as fast; overlapping zones apply the most severe;
- a catch inside a forbidden zone explodes;
- a laser curtain also explodes a thrown bomb crossing it; a window between two curtains lets the pass through;
- every zone reads by pattern and icon, not colour alone; the bomb shows the rate (sparks, frost, beep).

## M10.2 — Signals: bomb gates, pressure plates, actuators, checkpoint arch

Acceptance criteria:

- a bomb flying through a gate opens its actuator for the gate's hold time, then it closes;
- a pressure plate keeps its actuator open while any player stands on it;
- a closing door kills a player under it; a thrown bomb hitting a door explodes;
- an arch checkpoint activates only after a pass through its arch during the section, with every player inside;
- actuators and gates look the same on every machine and are restored by a section reset.

## M10.3 — Transit: tubes and cannons

Acceptance criteria:

- a thrown bomb entering a mouth goes `InTransit` (no fuse, no collision, not catchable) and leaves the linked exit
  after the delay, on a fixed arc landing on the exit's receiver pad;
- the exit warns (light + tone) before the bomb comes out; the exit arc is caught like any pass;
- a tube with several mouths sends the bomb to the exit of the mouth it entered;
- a section reset during a transit restores the bomb normally; host and clients agree on the state.

## M10.4 — `PlaytestCourse`

Acceptance criteria:

- a new scene of about 10 minutes, generated by `PlaytestCourseBuilder`, mixing the classic kit and every M10
  obstacle (each taught alone before it is combined);
- first in the Bootstrap level list; `PrototypeCourse` is kept;
- `PlaytestCourseTests` pass (structure, checkpoints, completion, rematch);
- [ ] group playtest of the whole course (human gate).

---

# Milestone 11 — Second course: PatataWorks (#83)

An enclosed factory course that climbs, turns and drops (PROJECT_SPEC §15b). No new mechanic: only the existing kit
and the M10 elements. The spec amendment (§15b, §17.2) comes first. Architecture: ARCHITECTURE §4, §25.

The spec amendment is merged in PR #84. The scene, interior palette, practical lighting, menu registration, route
metadata and course tests are implemented. The three human gates in M11.6 remain open; first-clear duration and
checkpoint spacing have not been measured. Automated checkpoint tests use teleports and do not prove a full clear.

Automated validation (2026-10-04, Unity 6000.3.25f1): `PatataWorksTests` (7), existing course suites (22),
`PatataParkTests` (3), `BombObstacleTests` (13), `BombStateTests` (22), `PassFeelTests` (17), `ZoneRuleTests` (10),
and `KitSkinTests` (19) pass. The existing PrototypeCourse sprint-gap test failed once and passed on an isolated
retry; no movement or throw tuning changed. Two successive factory rebuilds match in transforms, collider geometry,
component types, actuator sources and transit exits. Nine section screenshots are captured locally, outside Assets;
their attachment to the review PR remains open.

## M11.1 — Spec amendment

Acceptance criteria:

- PROJECT_SPEC §17.2 allows the second course; §15b holds its blueprint (sections, questions, intended difficulty);
  §20 its length and the ceiling clearance margin. Nothing renumbered.

## M11.2 — Interior kit

Acceptance criteria:

- new `KitRole`s for floors, walls, ceilings, window and door frames, stairs and ramps, pillars, trusses and
  railings, drawn by `KitSkin` from the KayKit Platformer pack (ARCHITECTURE §25.1);
- stairs and ramps stay within the motor's step offset and slope limit;
- walls and ceilings are on `Environment`; the first-person camera never sees through a wall.

## M11.3 — `PatataWorks` scene and builder

Acceptance criteria:

- `Assets/Scenes/PatataWorks.unity`, generated entirely by `PatataWorksBuilder` (menu **HotPatata/Course/Build
  PatataWorks**), part of **Rebuild All Courses**; rebuilding twice gives the same scene;
- first in the Bootstrap level list, its checkpoints counted for the spawn choice;
- the route: at least 6 direction changes (left and right), a bounding box over 60 m in X and Z, at least 30 m of
  total climb and one descent of at least 20 m, start and finish at different heights;
- a closed building: walls and a ceiling over every section except the roof; nothing floats in an open sky outside
  the shaft, the atrium void and the roof;
- 9 checkpoints; every section asks a §13 question named in the blueprint; at least three puzzles chain two
  elements through the geometry;
- every moving part runs on `SectionClock`, every stateful part is `IResettable`.

## M11.4 — Enclosed-course rules

Acceptance criteria:

- the builder declares each intended pass as a corridor volume; decoration never intersects one;
- the ceiling over every pass corridor clears the intended arc plus the §20 margin (computed from the throw tuning);
- openings that passes go through are larger than the catch radius with a margin; pits have kill zones.

## M11.5 — Interior light

Acceptance criteria:

- `LookBuilder` lights `PatataWorks`: golden hour through windows, skylights and the roof, warm practical lights,
  interiors darker than the open sky (real time, no bake);
- the potato, hazard stripes, rings and tube colours and symbols read in every room; flashes honour `flashReduction`.

## M11.6 — Tests and playtests

Acceptance criteria:

- `PatataWorksTests` pass: the scene builds and loads, 9 checkpoints in order and a reachable finish, ceiling
  clearance, clean pass corridors, one actuator per source, every transit mouth has an exit and a pad, every
  section resets cleanly, the level is in the Bootstrap list;
- the existing course, bomb-obstacle and pass-feel suites still pass;
- [ ] a 2-player playtest solves every puzzle; a first clear takes 10–14 min (human gate);
- [ ] host + 3 clients (MPPM) finish the course, doors, lifts, tubes and resets in sync (human gate);
- [ ] 60 fps at 1080p on the target PC with 4 players (human gate).

---

# Milestone 12 — Third course: PatataWilds

A realistic outdoor course (PROJECT_SPEC §15c), asked for by the owner: a forest, a river and granite cliffs climbed from
dawn to dusk, 20+ minutes, 25 checkpoints in 5 acts, first in the Bootstrap level list. No new mechanic: every element of
the kit and of M10 is re-dressed as nature with its logic and colliders unchanged. Architecture: ARCHITECTURE §4
(`PatataWilds`) and §25.3 (nature look).

Automated validation (2026-10-06, Unity 6000.3.25f1): `PatataWildsTests` (15), `NatureLookTests` (18), `TimeOfDayTests`
(4), `KitSkinTests` (19), `IndustrialLookTests` (34), `PatataWorksTests` (7), `IndustrialPlantTests` (14),
`IndustrialLabTests` (4), `MenuStationTests` (10), `BombObstacleTests` (13), `PrefabKitTests` (9), `CourseTests` (11),
`FactoryCourseTests` (8) and `PatataParkTests` (3) pass. Teleport-based checkpoint tests do not prove a full clear; the
first-clear time and checkpoint spacing in seconds are unmeasured.

## M12.1 — Spec amendment

Acceptance criteria:

- [x] PROJECT_SPEC §17.2 lists PatataWilds as an owner-decided exception, says its generated trees, plants and terrain
  are art (not procedural levels) and that its wind only sways plants;
- [x] §15c holds the blueprint (target, outdoor-course rules, route by act, signature puzzles, intended difficulty); §19
  the campfire and the banded hazards; §20 the length, the fuse override and the lethal water. Nothing renumbered.

## M12.2 — Assets and credits

Acceptance criteria:

- [x] `tools/Fetch-PolyHaven.ps1` fetches the CC0 textures, models and skies listed in `tools/nature-assets.json`
  incrementally, never `.blend`, packed ARM or DirectX normal files, and rewrites the Poly Haven block of `CREDITS.md`;
- [x] `NatureAssetImporter` sets every asset's import on first import (colour, normal, linear, alpha masks, sky cubemaps,
  models without colliders and with mesh LODs, compressed ambient loops);
- [x] generated meshes and scatter data are LFS-tracked; the Poly Haven extras are git-ignored; the downloads stay
  near 300 MB.

## M12.3 — Nature shader and materials

Acceptance criteria:

- [x] `HotPatata/Nature` maps rock and ground triplanar in metres and wood, props and foliage by UV, reads Poly Haven
  roughness and AO, has a moss/grass top layer, cut-out two-sided foliage, wind in every pass and a distance fade;
- [x] `HotPatata/Water` (depth tint, refraction, foam, sky reflection) and `HotPatata/SkyBlend` (two skies blended);
- [x] `NatureMaterialBuilder` builds the library and the gameplay variants (banded hazard, rotten planks, slot-coloured
  logs, lantern glass, embers).

## M12.4 — Kit look

Acceptance criteria:

- [x] `KitShape.RoughBox`, `Crag`, `Boulder`, `Skirt`, `Logs` and `Planks` are generated by `KitSkin`, deterministic,
  with tangents; rock never rises above a walked-on top and bulges at most 30 cm out of a side; `CourseKit.LookSet.Nature`
  maps every `KitRole`;
- [x] organic course edges: rock buttresses and fallen boulders along the cliffs (clear of passes, triggers and movers),
  rock skirts under raised slabs, scanned rocks, plants and deadwood instanced at their own mesh LODs;
- [x] `NatureRestyle` redraws the placed KayKit prefabs and leaves every gameplay cue alone; colliders unchanged;
- [x] `CourseKit.RegisterInMenu` is the one place that orders the level list (PatataWilds first, PatataWorks kept,
  the plant last).

## M12.5 — Time of day and sky

Acceptance criteria:

- [x] five presets (dawn, noon, late afternoon, sunset, dusk) written by `LookBuilder` through `PatataWildsLook`, each
  sky turned so its photographed sun matches the scene's sun;
- [x] `TimeOfDayBlender` follows the current checkpoint only (host-decided, mirrored), blends a step forward, shows a
  step back or a big jump at once, and never changes on a section reset; tuning in `GameTuning`.

## M12.6 — Vegetation and terrain

Acceptance criteria:

- [x] generated pines, firs, broadleaf trees, snags, bushes, grass, ferns and rocks, three LODs within budget, wind
  weights in the vertex colour;
- [x] a collider-free terrain around the course, never over a section; the scatter drawn instanced from a `FoliageSet`
  asset, clear of every pass arc, trigger and mover.

## M12.7 — Course builder, water and campfires

Acceptance criteria:

- [x] `PatataWildsBuilder` builds 25 sections in 5 acts with no overlap, closed by cliffs, with every intended pass
  declared and validated on every build;
- [x] every water surface has a kill zone 0.3 m under it; every pit has one; a safety net lies under the course;
- [x] checkpoints show campfires (no pad) that catch when reached and go out at a new run, honouring flash reduction;
  the summit beacon lights at the finish;
- [x] fixed 3D ambience (river, falls, birds, crickets, wind) on the SFX group, under the beeps; no movement sound.

## M12.8 — Tests and playtests

Acceptance criteria:

- [x] `PatataWildsTests`, `NatureLookTests` and `TimeOfDayTests` pass, and the touched suites still pass;
- [ ] a first clear by two players takes 20+ minutes and every puzzle is solvable with two players;
- [ ] host plus three clients online, through the whole course;
- [ ] 60 fps at 1080p on a mid-range PC, four players;
- [ ] readability review: hazards, tubes, gates, falling planks and campfires read at a glance in every act's light.

---

# Milestone 13 — Fourth course: PatataCanopy

Asked for by the owner after a review of PatataWilds: its puzzles had the intent but not the constraint (moving
platforms with an easy path beside them, windows a player jumps through, laser walls with a slot at each end, walls the
bomb is lobbed over). PatataCanopy (PROJECT_SPEC §15d) is built on the void, in the tree tops, in PatataWilds' nature
look: 25 sections in 5 acts, every one with a contract (§13.20) that names what it forces and the shortcuts its geometry
locks. Three generic systems make "bomb only" and "runner only" openings and empty-handed roles possible (§13.18,
§13.19). Architecture: ARCHITECTURE §4 (`PatataCanopy`) and §25.3.

Automated validation (2026-10-06, Unity 6000.3.25f1): `PatataCanopyTests` (12), `CourseContractTests` (7),
`BombObstacleTests` (16), `ZoneRuleTests` (12), `ConfigurationTests` (12), `ObstaclePrefabTests` (11),
`IndustrialPlantTests` (14) and `PatataWildsTests` (15) pass; the contract check finds nothing to report on the three
courses (PatataWilds is not yet declared, see M14). Teleport-based tests do not prove a clear.

## M13.1 — Spec amendment

Acceptance criteria:

- [x] PROJECT_SPEC §13.18 (body screen), §13.19 (hands-free plate, switch), §13.20 (section contract) and §15d (the
  blueprint); §20 the fuse overrides. Nothing renumbered.

## M13.2 — Body screen

Acceptance criteria:

- [x] a `BodyScreen` layer that collides with `Player` only: the bomb, the aim and the catch pass through it, and the
  motor never mantles onto it;
- [x] `Obstacle_BodyScreen` (vine strands, a faint field, "bomb through" signs, a frame of posts) resizable by
  `BombObstacleKitBuilder.ResizeScreen`; checked by `ObstaclePrefabValidator`.

## M13.3 — Hands-free plate and switch

Acceptance criteria:

- [x] `PressurePlate.countCarrier` (default on, so no existing plate changes); the variant
  `PressurePlate_HandsFree` is drawn blue with a "throw first" glyph;
- [x] `SignalSwitch`, a `SignalActuator` that turns objects (a curtain, a screen, a fuse zone) on or off at half travel,
  replicated and reset like any actuator; one source, no AND/OR; `Actuator_Switch` with no look of its own.

## M13.4 — Section contracts

Acceptance criteria:

- [x] `SectionContract` (metadata: the force, the locked gaps, climbs and lobs) and `CourseContractCheck` (reach from
  `GameTuning`: a slide-jump with a mantle reaches 12.75 m on the flat, a jump and a mantle 3.0 m, a lob 38 m up, so only a
  roof seals a wall); scans for a body screen a player walks round or hops over and a curtain the bomb flies round or over;
- [x] warnings only (`CourseContractTests`, menu HotPatata/Course/Check Section Contracts), never a CI gate.

## M13.5 — Course builder

Acceptance criteria:

- [x] `PatataCanopyBuilder` builds 25 sections with alternating turns and no overlap, junction decks, a kill plane under
  every section and a safety net, a leaf roof over every puzzle section (every cross wall reaches it), the forest below;
- [x] every section declares its contract and its intended passes; the build validates the passes and logs the contract
  findings (none);
- [x] the level list: PatataWilds, PatataCanopy, IndustrialPlant, PassSandbox; the day from dawn to dusk
  (`PatataWildsLook`); campfire checkpoints, an arch at checkpoints 15 and 25, the summit beacon.

## M13.6 — Tests and playtests

Acceptance criteria:

- [x] `PatataCanopyTests` (structure, fuse overrides, passes, signals, screens, campfires, resets at every checkpoint,
  transit catches, the plate/ring, switch and spore puzzles) and `CourseContractTests` pass;
- [ ] a first clear by two players; every puzzle is solvable and forced with two players, and still fun with four;
- [ ] host plus three clients online, through the whole course;
- [ ] 60 fps at 1080p on a mid-range PC, four players (the forest is instanced since M13.7; see its budget there);
- [ ] readability review: brambles versus lasers, hands-free plates, rings over the void, spores, in every act's light.

## M13.7 — Atmosphere

The owner found the canopy "floating in the sky with the trees underneath": a chill, slightly foggy, packed forest with the
course in the middle. Decoration and presentation only: no rule, collider, layer or pass changes (ARCHITECTURE §4, §25.3).

Acceptance criteria:

- [x] PatataCanopy has its own cooler day (`PatataCanopyLook` through `NatureDayLook`, presets in
  `Assets/Settings/Look/TimeOfDay/Canopy`); PatataWilds keeps its warm day;
- [x] a height mist (`HotPatataFog.hlsl`, `MistField`) pools on the forest floor: thick at dawn, thin at noon, back at sunset and
  dusk; the floor shows faintly straight down; at deck height it veils the longest pass by less than 15 %; PatataWilds gets
  a morning mist in its gorges and over the river; every other scene is drawn as before;
- [x] the forest floor follows the course (about 40 m under each section's lowest floor); the posts under the decks are trunks
  that reach it; the safety net lies under it;
- [x] giant oaks and redwoods (70-95 m) beside the course, their crowns at least 12 m over its highest roof or floor and a strip
  of sky left over the open sections; more giants, an understory and the ground cover out to the haze, all instanced
  (`FoliageSet`); none within the decoration clearance of a pass arc;
- [x] leaf roofs read as a ceiling of leaves from below (nothing under their underside, nothing under another roof's);
- [x] god rays (`HotPatata/LightShaft`) clear of every pass arc whatever the time of day, dust motes, falling leaves, fireflies
  from sunset (`AtmosphereFade`), a subtle leaf cookie on the sun (`LeafDapple`);
- [x] `PatataCanopyTests`, `PatataWildsTests`, `NatureLookTests`, `TimeOfDayTests` and `CourseContractTests` pass;
- [ ] the owner's look review in play, every act's light; readability of throws and teammates in the mist (with M13.6);
- [ ] 60 fps at 1080p on a mid-range PC with four players (measured in the editor at dawn, act 1: about 7,000 draw calls,
  9.5M triangles, 15 ms CPU and 10 ms GPU on the dev machine, editor overhead included).

---

# Milestone 14 — PatataWilds contracts

Close the shortcuts the M13 review found in PatataWilds with the same tools: laser walls that reach the cliffs, a body screen
in every throw window, palisades out of the turn squares, obstacles that span the walkable width, the Hold the Rope plate
made necessary, roofs where a wall must stop the bomb; then a `SectionContract` for each of its 25 sections.

Acceptance criteria:

- [ ] every PatataWilds section declares a contract and `CourseContractCheck` reports nothing;
- [ ] `PatataWildsTests` pass; a group playtest confirms each signature puzzle is forced.

---

# Milestone 15 — Fifth course: PatataTemple

Asked for by the owner: in the vein of PatataCanopy (built for two), a course whose every section needs three players, with
real challenges to think through as three; a jungle temple in ruins, its own layout (not Canopy's staircase over a forest). The
design was grilled with the owner first (three minimum, four new systems, ruins in the jungle, 15 sections, coordination, voices
assumed, short holds, a pivot that carries its riders, dawn to storm). PROJECT_SPEC §13.21–13.24 and §15e; ARCHITECTURE §4,
§10.6 and §25.4.

Automated validation (2026-10-10, Unity 6000.3.25f1): `PatataTempleTests` (9), `TrioObstacleTests` (3), `TrioSystemTests` (8),
`CourseContractTests` (9), `ObstaclePrefabTests` (11), `ZoneRuleTests` (12), `RiderReconstructionTests` (5),
`CarrierRegistryTests` (6), `ConfigurationTests` (12), `BombObstacleTests` (16), `ObstacleKitTests` (11), `PrefabKitTests` (9),
`IndustrialPlantTests` (14) and `PatataCanopyTests` (18) pass; the contract check finds nothing on the temple.

## M15.1 — Spec amendment

- [x] PROJECT_SPEC §13.19 (a bomb in flight is still its thrower's), §13.20 (a section for three, the spread), §13.21 heavy
  plate, §13.22 hourglass plate, §13.23 sun beam, §13.24 pivot, §15e (the blueprint and the grammar for three), §16, §17.2 and
  §20. Nothing renumbered.

## M15.2 — The systems for three

- [x] `PressurePlate.requiredBodies` and `memorySeconds` (replicated by `NetworkPressurePlate`), the thrower excluded while the
  bomb flies on every hands-free plate; `SunBeam`; turning carriers (`IPlatformCarrier.FrameRotation`/`AnchorRotation`,
  `PlayerMotor.Carry`, the rider's offset in the carrier's frame);
- [x] prefabs `PressurePlate_Heavy`, `PressurePlate_Hourglass`, `Actuator_Bridge_Pivot` (variants) and `SunBeam`, their cues
  (`PlateGauge`, `SunBeamVisual`, `SignalIndicator`'s sand pulse), all passing `ObstaclePrefabValidator`; PassSandbox's
  `KitDemo/TrioObstacles`.

## M15.3 — A level's minimum players and contracts for three

- [x] `NetworkBootstrap.sceneMinPlayers` (written by `CourseKit.RegisterInMenu`): "(3+ players)" in the level list, the lobby's
  Start disabled and `StartLevel` refusing below it;
- [x] `SectionContract.minPlayers`, `twoFail` and the `Spread` lock (the walk between two sources through `via` points);
  `CourseContractCheck` warns when a section for three says nothing of why two fail.

## M15.4 — Course builder

- [x] `PatataTempleBuilder` builds 15 sections turned by their own angles (a causeway, a ring at 60° round the pyramid, the
  well, a skyway 22 m over the ring), octagonal courts, stone roofs, kill planes; `CheckFootprints` tests turned footprints and
  heights;
- [x] every section declares its contract for three and its passes; the build validates the passes and logs no contract finding;
- [x] the level list: PatataWilds, PatataCanopy, PatataTemple, IndustrialPlant, PassSandbox; fires at the checkpoints, arches at
  5, 10 and 15, the altar's beacon; `Rebuild All Courses` builds it.

## M15.5 — The valley and the look

- [x] the valley (no forest of trunks): its floor, the river, the canopy carpet under every floor, emergent giants far off, the
  stepped pyramid under the kill planes, ruined towers, jungle plants (Poly Haven calathea and anthurium), stone columns that
  never cross a lower section; Poly Haven masonry and five pure skies, credited;
- [x] `PatataTempleLook` (misty dawn to storm) and `TempleStorm` (rain that dies on roofs, lightning scaled by
  `viewEffectsStrength` and `flashReduction`).

## M15.6 — Tests and playtests

- [x] `PatataTempleTests`, `TrioObstacleTests`, `TrioSystemTests` and `CourseContractTests` pass;
- [ ] a first clear by three players; every puzzle fails with two (as its contract says) and stays fun with four;
- [ ] host plus two clients online, through the whole course (the pivots' riders under latency);
- [ ] 60 fps at 1080p on a mid-range PC, four players (the canopy is instanced);
- [ ] readability review: the heavy slab's footprints, the sand rings, the beams and eyes, the shrines, in every act's light and
  in the storm; the owner's look review.

---

# Master MVP definition of done

The MVP is complete when all of the following are true:

- [x] host can create a session;
- [x] 1–3 friends can join by code;
- [ ] 2–4 players spawn correctly;
- [x] movement is responsive;
- [x] one bomb exists;
- [x] bomb can be thrown;
- [x] bomb can be caught while players move/jump;
- [x] successful catch refreshes the fuse;
- [x] bomb beeps/pulses with urgency;
- [x] bomb hitting environment causes authoritative explosion;
- [x] holding too long causes authoritative explosion;
- [x] explosion resets current section quickly;
- [x] checkpoints work;
- [x] one 3–5 minute greybox course exists;
- [x] all required players must finish;
- [x] run can be replayed;
- [x] host/client agree on bomb state;
- [ ] no recurring Console errors;
- [ ] playtesters understand failures;
- [ ] playtesters voluntarily want another attempt.

Open: 4-player spawning (M7.2), a clean Console over a long session (M7.1), and the two playtest items (M7.4).

---

# First command for the coding agent

Original bootstrap instruction (Milestones 0–1 are done; start from the status table at the top instead):

> Read `docs/PROJECT_SPEC.md`, `docs/ARCHITECTURE.md`, and `docs/MVP_TASKS.md`. Inspect the existing Unity project before changing anything. Start at the first open milestone in the status table and proceed in order. Use Unity Editor/MCP tools for scenes, prefabs, component assignment, hierarchy inspection, Console inspection, and Play Mode validation when available. Do not invent features outside the specification.
