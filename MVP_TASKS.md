# HotPatata — MVP_TASKS.md

> Ordered execution plan for a coding agent.  
> Read `PROJECT_SPEC.md` and `ARCHITECTURE.md` before starting.  
> Do not skip acceptance criteria.  
> Do not add out-of-scope gameplay while completing these tasks.

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

- [ ] project opens without compilation failure;
- [ ] Input System package is available;
- [ ] project can enter Play Mode;
- [ ] Console has no recurring exceptions in an empty scene.

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

- [ ] folders exist;
- [ ] no unnecessary framework folders are added.

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

- [ ] all values can be changed from Inspector;
- [ ] gameplay scripts do not need duplicated hard-coded tuning constants.

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

- [ ] scene is saved as `Assets/Scenes/PassSandbox.unity`;
- [ ] geometry has appropriate colliders;
- [ ] spawn markers are visible in Scene view;
- [ ] no scene errors.

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

- [ ] player can traverse the sandbox reliably;
- [ ] jump feels responsive;
- [ ] player does not slide excessively after releasing movement;
- [ ] player can adjust modestly in air;
- [ ] no obvious collider snagging on flat geometry.

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

- [ ] prefab exists under `Assets/Prefabs/Player/`;
- [ ] all anchor references are assigned;
- [ ] prefab can be instantiated without missing references.

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

- [ ] state is inspectable/debuggable;
- [ ] bomb can attach to a player hand anchor;
- [ ] bomb can detach into physics;
- [ ] no world-contact failure while correctly held;
- [ ] state transitions are logged in development builds/editor.

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

- [ ] bomb leaves hand cleanly;
- [ ] bomb does not collide instantly with carrier;
- [ ] throw direction follows aim predictably;
- [ ] the same input gives repeatable results.

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

- [ ] moving receiver can catch;
- [ ] jumping receiver can catch;
- [ ] successful catch is obvious;
- [ ] same catch cannot resolve twice;
- [ ] catch does not immediately produce an environment explosion from overlapping geometry.

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

- [ ] deliberately throw at floor → explosion;
- [ ] deliberately throw at wall → explosion;
- [ ] miss receiver and hit platform → explosion;
- [ ] valid player catch does not explode;
- [ ] failure reason is logged.

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

- [ ] holding bomb continuously causes failure;
- [ ] successful catch restores full hold window;
- [ ] no stale fuse continues after reset;
- [ ] fuse cannot expire during reset state.

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

- [ ] player can estimate urgency without a number;
- [ ] warning becomes clearly stronger near expiry;
- [ ] catch sound is distinct;
- [ ] explosion sound is distinct.

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

- [ ] all players return to configured spawn points;
- [ ] bomb has zero stale velocity;
- [ ] bomb has valid carrier or configured start state;
- [ ] fuse is full;
- [ ] player control returns;
- [ ] repeated failures do not corrupt state.

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

- [ ] saved under `Assets/Prefabs/Platforms/`;
- [ ] scaling does not create broken collision.

---

## M2.2 — Create `Platform_Narrow`

Variant intended for later precision sections.

Keep initial default forgiving.

**Acceptance criteria**

- [ ] reusable;
- [ ] visually distinguishable in greybox if useful.

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

- [ ] moves repeatedly between endpoints;
- [ ] player can stand on it without obvious instability;
- [ ] movement can be reset;
- [ ] values editable in Inspector.

---

## M2.4 — Create `Obstacle_RotatingBar`

Expose:

- axis;
- speed;
- phase.

**Acceptance criteria**

- [ ] deterministic continuous rotation;
- [ ] collider matches visible bar;
- [ ] reset is consistent.

---

## M2.5 — Create `Platform_Falling`

Required:

- trigger;
- warning delay;
- collapse;
- reset support.

**Acceptance criteria**

- [ ] first qualifying interaction starts collapse;
- [ ] platform restores correctly after section reset.

---

## M2.6 — Create `KillZone`

Required:

- player entering it reports player fall/reset behavior;
- bomb entering it causes authoritative bomb failure.

**Acceptance criteria**

- [ ] bomb cannot disappear forever below level;
- [ ] player fall behavior is deterministic.

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

- [ ] stores/identifies player respawn slots;
- [ ] activates only when required condition is met;
- [ ] RunManager can reset to it.

---

## M2.8 — Create `FinishZone`

Required:

- tracks required players;
- reports course completion.

**Acceptance criteria**

- [ ] one player alone cannot finish a multi-player run;
- [ ] completion fires once.

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

- [ ] project compiles;
- [ ] networking initialization succeeds;
- [ ] no obsolete duplicate Lobby/Relay package architecture is introduced unnecessarily.

---

## M3.2 — Create network bootstrap

Implement minimal session/network startup architecture.

Do not build polished menu yet.

**Acceptance criteria**

- [ ] one instance can host;
- [ ] second instance can connect;
- [ ] disconnect is handled without permanent editor breakage.

---

## M3.3 — Network player spawning

Required:

- one player object per connection;
- local player owns local input;
- remote players are visible.

**Acceptance criteria**

- [ ] host and client see both players;
- [ ] each controls only their own character;
- [ ] no duplicate camera/input activation on remote players.

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

- [ ] clients cannot independently assign carrier;
- [ ] both clients agree on carrier;
- [ ] both clients agree on explosion;
- [ ] repeated passes do not duplicate bomb.

---

## M3.5 — Network catch validation

Test specifically under latency.

Required:

- host resolves catch once;
- client receives replicated result;
- stale collision must not produce obviously unfair post-catch explosion.

**Acceptance criteria**

- [ ] host→client pass works;
- [ ] client→host pass works;
- [ ] client→client via host authority works if 3+ players available;
- [ ] no common “caught then exploded anyway” race.

---

## M3.6 — Network reset

Required:

- explosion causes one authoritative section reset;
- all clients teleport/reset consistently;
- bomb normalizes once.

**Acceptance criteria**

- [ ] no duplicate reset calls;
- [ ] all clients resume same section;
- [ ] bomb carrier/fuse match after reset.

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

---

# Milestone 4 — Session and lobby

---

## M4.1 — Create `Bootstrap` scene

Responsibilities:

- initialize services;
- preserve session manager;
- route scenes.

**Acceptance criteria**

- [ ] clean startup;
- [ ] no duplicate persistent managers after scene changes.

---

## M4.2 — Create session

Host flow:

- create;
- receive/display join code;
- enter lobby.

**Acceptance criteria**

- [ ] host can create session repeatedly after returning to menu.

---

## M4.3 — Join by code

Client flow:

- enter code;
- join;
- enter lobby.

**Acceptance criteria**

- [ ] invalid code gives readable error;
- [ ] valid code connects.

---

## M4.4 — Lobby player list

Display:

- connected players;
- host marker;
- optional ready state.

Keep UI utilitarian.

**Acceptance criteria**

- [ ] list updates on connect/disconnect.

---

## M4.5 — Host start

Host launches `PassSandbox` or `PrototypeCourse`.

**Acceptance criteria**

- [ ] all connected clients load intended scene;
- [ ] all spawn once;
- [ ] bomb starts once.

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
- [ ] bomb progresses upward through passes.

---

## M5.4 — Checkpoint 1

Place checkpoint after initial teaching beats.

**Acceptance criteria**

- [ ] full team activates;
- [ ] reset returns here after later failure.

---

## M5.5 — Beat D: Moving Pair

Use moving-platform prefab.

**Acceptance criteria**

- [ ] players can wait for safe window or attempt riskier timing;
- [ ] movement is network-consistent enough to catch.

---

## M5.6 — Beat E: Split Lanes

Required:

- parallel paths;
- visual line of sight;
- one or more openings for cross-pass.

**Acceptance criteria**

- [ ] bomb must cross between lanes;
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
- [ ] failure is readable.

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

- [ ] same group can restart without recreating project/session manually.

---

> **Status:** Milestone 5 is implemented (course beats A-G, three checkpoints, finish and rematch); the human playtest of the full course is still open.

# Milestone 6 — UX and readability

---

## M6.1 — Carrier indicator

Show current holder clearly.

**Acceptance criteria**

- [ ] visible at useful gameplay distance;
- [ ] updates immediately after catch.

---

## M6.2 — Catchable/receiver indicator

Prototype visual feedback for valid receiver / catch-ready state.

**Acceptance criteria**

- [ ] helps passing without making throws automatic;
- [ ] no confusing false-positive state.

---

## M6.3 — Checkpoint/course progress

Minimal UI only.

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

See the plan in the PR description (toon shader, sky, post, speed lines, trail, fuse sparks, cartoon explosion, CC0 audio).

---

# Master MVP definition of done

The MVP is complete when all of the following are true:

- [ ] host can create a session;
- [ ] 1–3 friends can join by code;
- [ ] 2–4 players spawn correctly;
- [ ] movement is responsive;
- [ ] one bomb exists;
- [ ] bomb can be thrown;
- [ ] bomb can be caught while players move/jump;
- [ ] successful catch refreshes the fuse;
- [ ] bomb beeps/pulses with urgency;
- [ ] bomb hitting environment causes authoritative explosion;
- [ ] holding too long causes authoritative explosion;
- [ ] explosion resets current section quickly;
- [ ] checkpoints work;
- [ ] one 3–5 minute greybox course exists;
- [ ] all required players must finish;
- [ ] run can be replayed;
- [ ] host/client agree on bomb state;
- [ ] no recurring Console errors;
- [ ] playtesters understand failures;
- [ ] playtesters voluntarily want another attempt.

---

# First command for the coding agent

Use this as the initial execution instruction:

> Read `PROJECT_SPEC.md`, `ARCHITECTURE.md`, and `MVP_TASKS.md`. Inspect the existing Unity project before changing anything. Start at Milestone 0 and proceed in order. Use Unity Editor/MCP tools for scenes, prefabs, component assignment, hierarchy inspection, Console inspection, and Play Mode validation when available. Do not implement networking or substantial level content until the local PassSandbox throw/catch/fuse/reset loop satisfies Milestone 1 acceptance criteria. Do not invent features outside the specification.
