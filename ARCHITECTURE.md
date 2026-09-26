# BEEP! — ARCHITECTURE.md

> This document defines the intended technical structure for the MVP.  
> `PROJECT_SPEC.md` is the gameplay source of truth.  
> Prefer simple, inspectable Unity components over framework-heavy abstractions.

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

### `Lobby`

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

### `PrototypeCourse`

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

---

## 5. Proposed project folders

```text
Assets/
├── Art/
│   ├── Materials/
│   ├── Models/
│   └── VFX/
├── Audio/
│   ├── Bomb/
│   └── UI/
├── Prefabs/
│   ├── Network/
│   ├── Player/
│   ├── Bomb/
│   ├── Gameplay/
│   ├── Platforms/
│   └── Obstacles/
├── Scenes/
│   ├── Bootstrap.unity
│   ├── Lobby.unity
│   ├── PassSandbox.unity
│   └── PrototypeCourse.unity
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

Keep folder naming boring and predictable.

---

## 6. Core runtime components

Names are recommendations. The agent may adjust exact names only when a clear Unity convention improves the design.

### 6.1 `GameTuning`

Prefer one or several ScriptableObjects for fast tuning.

Suggested fields:

```text
Movement
- moveSpeed
- acceleration
- braking
- airControl
- jumpHeight / jumpVelocity
- coyoteTime
- jumpBuffer

Bomb
- holdFuseDuration
- warningDuration
- caughtGraceDuration
- throwSpeed
- throwChargeMin / Max (only if charge exists)
- bombGravityScale
- catchRadius
- catchFrontBias
- aimAssistAngle
- aimAssistDistance

Run
- resetDelay
```

Do not scatter magic numbers across scripts.

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

Should **not** know bomb game rules.

### 7.2 `PlayerLook` / camera controller

Responsibilities:

- local camera rotation;
- follow target;
- pitch constraints;
- camera collision if needed later.

Network replication of camera is unnecessary.

### 7.3 `PlayerThrower`

Responsibilities:

- determine whether local player may throw;
- aim/release direction;
- send throw request to authoritative bomb system;
- expose release anchor.

Should not directly mutate authoritative bomb ownership on clients.

### 7.4 `PlayerCatchVolume`

Responsibilities:

- describe catchable region;
- expose receiver eligibility;
- optional catch-ready modifier;
- provide hand/catch anchor.

The authoritative catch resolver uses this information.

### 7.5 `PlayerPresentation`

Responsibilities:

- name/color;
- carrier indicator;
- catch-ready indicator;
- local animation hooks.

No authoritative game rules.

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
```

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
- thrown movement;
- environment collision reporting;
- transition between attached and thrown physics modes.

While held:

- Rigidbody should not produce accidental lethal collisions.

While thrown:

- collision is gameplay-critical.

### 8.4 `CatchResolver`

Responsibilities:

- decide whether incoming bomb contact is a valid catch;
- reject ineligible receiver;
- resolve simultaneous/near-simultaneous catch claims;
- change authoritative carrier exactly once;
- enter catch grace state.

Do not duplicate catch decisions inside player scripts.

### 8.5 `BombAudio`

Responsibilities:

- beep cadence;
- warning sounds;
- throw sound;
- catch sound;
- explosion sound.

Should consume bomb state; should not control bomb state.

### 8.6 `BombPresentation`

Responsibilities:

- emissive pulse;
- warning intensity;
- holder visual;
- trail;
- explosion VFX hooks.

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

### 10.2 Reusable MVP prefabs

Create these as needed:

```text
Platforms/
- Platform_Basic
- Platform_Narrow
- Platform_Moving
- Platform_Falling

Obstacles/
- Obstacle_RotatingBar
- Obstacle_MovingBlocker
- Obstacle_LowOpening

Gameplay/
- PlayerSpawn
- BombSpawn
- Checkpoint
- KillZone
- FinishZone
- LaunchPad (later in course)
```

### 10.3 `MovingPlatform`

Expose:

- waypoint A;
- waypoint B;
- speed;
- movement mode;
- start phase.

Must be deterministic enough for networked play.

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

---

## 11. Prefab requirements

### Player prefab

Suggested structure:

```text
Player
├── Visual
├── CharacterBody / Motor
├── CatchVolume
├── ThrowOrigin
├── HandAnchor
├── NameplateAnchor
└── CameraTarget
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

Define explicit layers early.

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

## 14. Session flow

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

### 18.1 Edit Mode

Good candidates:

- bomb state transition rules;
- fuse reset/expiry math;
- checkpoint data validation;
- tuning configuration tests.

### 18.2 Play Mode

Priority tests:

1. held bomb expires;
2. thrown bomb hitting environment explodes;
3. thrown bomb entering catch volume is caught;
4. catch refreshes fuse;
5. catch enters grace and does not instantly fail;
6. reset clears bomb velocity;
7. reset returns players to checkpoint;
8. checkpoint activates only under required conditions.

### 18.3 Multiplayer manual tests

Must be tested on at least:

- host + one client;
- ideally two separate machines before building many levels.

Critical network test:

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
- automatic catch vs catch-ready input;
- fixed throw vs charged throw;
- exact network transform strategy;
- advanced client prediction;
- lag compensation details;
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
