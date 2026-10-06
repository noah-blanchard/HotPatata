# HotPatata — OBSTACLES.md

The catalogue of every obstacle system in the game, how each behaves, how it is dressed in each look, and where the
courses used it, including the courses that were removed. Rules live in [`PROJECT_SPEC.md`](PROJECT_SPEC.md) (§13
level grammar, §20 tuning); the components in [`ARCHITECTURE.md`](ARCHITECTURE.md) §10. This file is the reference
for designing new maps from the kit.

Kept maps: `PassSandbox` (the kit demo and the pass range), `IndustrialPlant`, `PatataWilds` and `PatataCanopy`. `Bootstrap`
is the menu.

---

## 1. The systems

Each system is a logic component on a prefab (`Assets/Prefabs/{Platforms,Obstacles,Gameplay}`) with a `Collision`
part (box colliders on a gameplay layer) and a `Visual` part (renderers only, never a collider). The logic never reads
the visual, so the same logic takes any look (§2, §4).

### 1.1 Platforms

| System | Prefab | Component | Behaviour | Main settings |
|---|---|---|---|---|
| Static platform | `Platform_Basic`, `Platform_Narrow` | none | a floor block on `Environment` | size |
| Moving platform | `Platform_Moving` | `MovingPlatform` | goes back and forth between `waypointA` and `waypointB`; position is a pure function of `SectionClock`, so it is the same on every machine and back in phase after a reset; carries riders (`IPlatformCarrier`) | speed, start phase, `PingPong` (constant speed) or `Dwell` (waits at each end for `dwellFraction`, then eases) |
| Elevator | `Platform_Elevator` | `MovingPlatform` (`Dwell`, vertical) | lifts riders between two landings | travel, speed, dwell |
| Piston | `Obstacle_Piston` | `MovingPlatform` (`Dwell`) | a floor tile or gate that rises and falls; in a row with phase offsets it makes a wave | travel, dwell, phase |
| Falling platform | `Platform_Falling` | `FallingPlatform` (+ `NetworkFallingPlatform`) | a player stepping into its trigger starts a shake of `warningDelay`, then it drops and switches off; the host decides the trigger time and replicates it; restored on reset (`IResettable`) | warning delay, fall acceleration and distance, shake |
| Conveyor | `Platform_Conveyor` | `Conveyor` | carries idle riders along its axis (forward or back), scrolling stripes show the direction | speed (signed) |
| Launch pad | `LaunchPad` | `LaunchPad` | throws a player standing on it straight up to a set height (only the machine owning the player launches it) | height |

### 1.2 Hazards (lethal to players, `Hazard` layer, striped, child `KillZone`)

| System | Prefab | Component | Behaviour | Rule |
|---|---|---|---|---|
| Rotating bar | `Obstacle_RotatingBar` | `RotatingObstacle` | a bar turning on an axis; angle = phase + speed × `SectionClock`. The prefab itself is on `Environment` with no `KillZone`: it blocks and pushes, it does not kill (a lethal variant adds a `KillZone`) | deterministic |
| Sweeper | `Obstacle_Sweeper` | `RotatingObstacle` | a knee-high lethal bar turning around a hub: jump it | knee-high, jumpable |
| Windmill | `Obstacle_Windmill` | `RotatingObstacle` | lethal blades turning in a wall's plane; guards the only bomb window through the wall | the bomb question stays central (§13.11) |
| Crusher | `Obstacle_Crusher` | `MovingPlatform` (`Dwell`, vertical) | a slab that drops and rises; lethal underside | never closes below 1.45 m: crouching or sliding under it is always safe; when down it makes a low-throw tunnel (§13.5) |
| Kill zone | `KillZone` | `KillZone` | fails the section for a player and explodes a bomb; under every pit, water surface and the whole course | — |

### 1.3 Bomb zones (`Zone` + effect components, `Trigger` layer, swept by `BombZoneSweep`)

| System | Prefab | Components | Behaviour |
|---|---|---|---|
| Forbidden strip | `Zone_Forbidden` | `Zone` + `FuseZone` (forbidden) | the carrier may not enter: the bomb explodes in a carrier's hands inside it, a catch inside it explodes; walk over without the bomb, throw across (§13.13) |
| Hot zone | `Zone_Hot` | `Zone` + `FuseZone` (×2) | the fuse burns twice as fast (§13.14) |
| Cold zone | `Zone_Cold` | `Zone` + `FuseZone` (×0.5) | the fuse burns half as fast; small pockets beside the line, never a whole beat |
| Laser curtain | `LaserCurtain` | forbidden `FuseZone` + `BombBarrier` | runners walk through, a thrown bomb crossing it explodes: framing a window makes the window mandatory (§13.13) |
| Body screen | `Obstacle_BodyScreen` | `BodyScreen` (its box on the `BodyScreen` layer) | the mirror of a curtain: every player is stopped, the bomb, the aim and the catch go through; nobody mantles onto it; resize with `BombObstacleKitBuilder.ResizeScreen`; a switch targets its `Screen` child so the frame stays (§13.18) |

The most severe zone wins (forbidden > hot > cold). Trigger events are never used for the bomb.

An opening in a wall is always one of three: **bomb only** (a body screen in it), **runners only** (a laser curtain in it),
or **both** (empty). A plain 2.4 × 2 m window is "both": a player climbs through it.

### 1.4 Signals: sources and actuators (one source drives exactly one actuator, no AND/OR)

| System | Prefab | Component | Behaviour |
|---|---|---|---|
| Bomb gate (ring) | `BombGate_Ring` | `BombGate` (+ `NetworkBombGate`) | active for `holdSeconds` after the thrown bomb flies through it (0 = latched until reset); carrying the bomb through does not count |
| Checkpoint arch | `BombGate_Arch` | latched `BombGate` | a checkpoint with `claimGate` activates only once a pass went through its arch this section (§13.17) |
| Pressure plate | `PressurePlate` | `PressurePlate` | active while any player stands on it, carrier included |
| Hands-free plate | `Variants/PressurePlate_HandsFree` | `PressurePlate` (`countCarrier` off) | the carrier does not count: the holder must have passed the bomb first, and a catch on the plate lets it go (§13.19); blue, with a "throw first" glyph |
| Switch | `Actuator_Switch` | `SignalSwitch` (+ `NetworkSignalActuator`) | an actuator that turns its targets (a curtain, a screen, a fuse zone: any object) on or off at half travel; `activeWhenOpen` picks which way; one source, reset like any actuator (§13.19) |
| Door | `Actuator_Door` | `SignalActuator` (+ `NetworkSignalActuator`) | a portcullis that closes completely; its lower edge is lethal only while it closes |
| Bridge | `Actuator_Bridge` | `SignalActuator` | extends over a gap while its source is active |
| Lift | `Actuator_Lift` | `SignalActuator` | rises to its open waypoint while its source is active; carries riders |

The actuator's motion is a pure function of the server clock and of the last direction change (time, progress,
direction): the host records only that. `SignalIndicator` lights sources (pulsing before a timed gate closes).

### 1.5 Transit (the bomb goes `InTransit`: no fuse, nothing can catch or explode it)

| System | Prefab | Components | Behaviour |
|---|---|---|---|
| Tube | `Obstacle_Tube` | `BombTransit` + `TransitMouth` per route (+ `NetworkBombTransit`, `TransitPresentation`) | a thrown bomb entering a mouth is hidden for `delay`, then leaves the linked exit on a fixed arc reaching catch height above that exit's receiver pad after `flightTime`; up to three routes, each mouth and exit share a colour **and** a pip count; the exit lamp ramps up and a rising tone plays before release |
| Cannon | `Obstacle_Cannon` | `BombTransit` (one exit) | the bomb shows in its basket, then is fired far and high after a short delay |
| Hoop | `Obstacle_Hoop` | `RotatingObstacle` | a spinning ring: a moving pass window |

### 1.6 Run pieces

| System | Prefab | Component | Behaviour |
|---|---|---|---|
| Checkpoint | `Checkpoint` | `Checkpoint` | activates when every player is inside; the reset target from then on; `holdFuseOverride` changes the hold fuse from it on; `claimGate` makes it an arch checkpoint |
| Finish | `FinishZone` | `FinishZone` | completes the run once every player is inside, exactly once |
| Spawn | `PlayerSpawn` | `PlayerSpawn` | start positions (on `SectionRoot`) and per-checkpoint respawns |

---

## 2. Looks

The same prefab is drawn three ways. Colliders, sizes and layers never change with the look; gameplay cues (zones,
laser beams, pads, icons, plates, launch pads, tube slot lamps) are never restyled.

| System | KayKit (toy kit, §25.1) | Industrial (`IndustrialPlant`, §25.2) | Nature (`PatataWilds`, §25.3) |
|---|---|---|---|
| Floor / wall | KayKit blocks | bevelled steel, concrete, brick per room theme | rough rock (`RoughBox`, crags, boulders) |
| Moving platform | coloured block | steel crane platform | log raft |
| Falling platform | cracked block | falling grate | rotten mossy planks |
| Conveyor | belt | steel belt with scrolling stripes | log drive |
| Piston | block | steel tile | rising rock pillar / raft |
| Crusher | striped slab | press | log ram or stamp |
| Sweeper | striped bar | striped bar | swinging log with burnt bands |
| Windmill | striped blades | fan blades | water wheel |
| Hoop | ring | ring | woven willow |
| Tube | coloured pipe | pipe | blazed hollow log in the slot colour |
| Cannon | basket and barrel | basket and barrel | stump catapult |
| Bomb gate / arch | ring / arch | ring / arch | vine ring / wooden arch with lanterns |
| Laser curtain | posts and beams | posts and beams | cairns, beams kept |
| Body screen | vine strands, field, sign (cue kept in every look) | same | same, frame of rough logs |
| Hands-free plate | blue checker, "throw first" glyph (cue kept) | same | same |
| Door | portcullis | portcullis | palisade of upright logs |
| Checkpoint | square pad | square pad | campfire that lights up (pad hidden) |
| Water | — | — | lethal surface, kill zone 0.3 m below |

Lethal parts always carry stripes or bands, never colour alone (§19).

---

## 3. Map usage

### 3.1 Kept maps

- **`PassSandbox`.** One bomb, a `KitDemo` group with every kit prefab, a pass range with marks at 4, 8, 12 and 16 m, and a
  `KitDemo/BombObstacles` corner (zones, curtains, a ring-driven lift). Tests: `PrefabKitTests`, `BombObstacleTests`,
  `ObstacleKitTests`, movement and pass-feel suites.
- **`IndustrialPlant`.** Eleven rooms, about 680 m, nine checkpoints, closed and lamp-lit (ARCHITECTURE §25.2):
  - **Gatehouse:** a ring opens the door, warm-up passes.
  - **Sorting line:** opposing belts, no-carry strips, a two-route tube (colour and pips), a timed gate, a piston.
  - **Atrium** (climbs 14 m): stair ramps, a plate lift held by the receiver, a return gate and door.
  - **Void catwalks:** falling grates, a crane platform, a low-throw deck.
  - **Cold storage:** a cold lane against a hot lane, a laser wall.
  - **Chute:** a 28 m mega slide; the bomb takes a tube.
  - **Furnaces:** the intake has a hot zone and a crusher; the loop has a windmill wall, a crusher and a cold respite.
  - **Boiler approach:** a sweeper.
  - **Boiler shaft** (climbs 34 m): rising platforms and a cannon to the upper pad.
  - **Control room:** an arch checkpoint and the finish.
- **`PatataWilds`.** Five acts, 25 sections, 25 checkpoints, from dawn to dusk (PROJECT_SPEC §15c). Every kit system,
  dressed as nature. Its signature puzzles:
  - **Two Banks, One Bomb:** two hollow logs to two banks.
  - **Hold the Rope:** a plate holds the rope lift; the plate holder is the receiver.
  - **Up the Cliff:** a catapult fires the bomb while the receiver rides rising rafts.
  - **Down the Rapids:** the runners take the slide, the bomb a hollow log.
  - **Mill Race:** a water wheel guards the bomb window; the runners go under a stamp.

  The fuse drops to 5.0 s from CP21 and to 4.5 s from CP24. Its sections have no contract yet: the M13 review found
  shortcuts in about 15 of them (MVP_TASKS M14).
- **`PatataCanopy`.** Five acts, 25 sections in the tree tops (PROJECT_SPEC §15d), every one with a contract (§5 below).
  Built on the void; puzzle sections under a leaf roof that every cross wall reaches. How it uses the kit:
  - **Body screens with curtains** in the same wall: the bomb through the brambles, the runners through the lasers (Le
    Filet, L'Écluse, Couloir de ronces, La Haie, L'Écluse finale); in a divide between two branches (Les Deux Branches,
    Les Galeries, Pont-levis croisé, where a ring sits in the bramble window); raised 1.3 m over a slide, a slider's gap in
    spores (Glissade).
  - **Hands-free plates**: a holder raises the carrier's bridge (La Plaque), raises a lift (La Poulie), raises the other
    branch's bridge (Pont-levis croisé), cuts lasers (Les Lucioles), clears spores (Le Pont des spores).
  - **Switches**: a plate cuts a laser curtain, a ring parts a bramble hedge for 6 s, a plate clears a spore zone.
  - **Rings** raise rising bridges from under the kill plane (14 m gaps), one hangs over the gap on the line of the throw
    back; **spores** (forbidden zones) wrap launch pads and ride a shuttle, so only empty hands fly or ride; a seed
    catapult with a 5 s delay crosses 70 m under a branch roof no throw gets past.
  The fuse drops to 5.0 s from CP15 and to 4.5 s from CP20.

### 3.2 Removed maps (archive)

These courses were removed in the project cleanup (they live in git history before that commit). Their layouts are
kept here because they are proven combinations of the kit.

**`PrototypeCourse`** was the first course: a straight line along +Z, ~680 m, 7 checkpoints, 3 acts.

| Act | Beat | What it asked |
|---|---|---|
| 1 Training Grounds | A Safe Court | flat and wide: learn charge, throw, timed catch at 6 m |
| | B First Gap | 4.5 m pit, broad landing |
| | C Stair Relay | two 1.2 m steps, a 1.8 m step to jump and mantle; relay the bomb upward |
| | D Moving Pair | two slow platforms that line up now and then |
| | E Split Lanes | centre wall with two openings, a blocker per lane: the bomb crosses between lanes |
| | F Vertical Catch | a launch pad throws the receiver 6 m up; catch near the apex, land on an 8.4 m ledge |
| | G Final Sprint | 7.6 m sprint-jump gap, narrow hops, a 1.3 m low bar (slide under), three falling platforms, a faster moving pair |
| 2 Patata Factory | H Conveyor Hall | three belts 1.5 m apart (sides forward at 3 m/s, centre back at 4 m/s), hurdles; throws must lead |
| | I Piston Alley | five piston tiles in a 3 m wave; a raised tile blocks the pass |
| | J Windmill Wall | an 11 m wall, the bomb through the windmill's hole, runners on elevators over the top |
| | K Sweeper Pit | two knee-high sweepers turning opposite ways; exit under a crusher (1.45 m clearance) |
| 3 The Climb & The Drop | L Elevator Tower | 9.6 → 30.6 m: a launch pad or an elevator, an elevator with a sweeper on it, then a mantle staircase; fuse 5 s from CP6 |
| | M Mega Slide | 18° downhill, two lanes split by a low divider, three spinning hoops; pass sideways at 15–18 m/s; fuse 4.5 s from CP7 |
| | N Factory Finale | run-out under a crusher, a belt through three piston gates, the podium |

**`PlaytestCourse`** (M10, #68) was the playtest course for the bomb obstacles: ~820 m, 9 checkpoints, 4 acts. Each new
obstacle was taught alone, then combined, then twisted.

| Act | Beats |
|---|---|
| 1 Warm-up | start court, first gap, stair relay with a 1.4 m mantle, a moving pair in opposite phase, a conveyor hall, a sweeper pit |
| 2 Hot & Cold | forbidden strips of 4 and 6 m (walk over, throw across); a laser window; a 32 m hot corridor with two sweepers and cold pockets on side ledges; a laser slalom of three walls whose last window is a spinning hoop |
| 3 Switchboard | a ring extends a bridge over a 12 m gap for 8 s; **The Lock** (a plate holds a door whose doorway is a curtain, the bomb takes the window, the plate holder climbs over the wall while the catcher waits in a cold pocket); a rhythmic shutter and a crusher tunnel; an arch checkpoint across a 4 m gap; a switch chain (ring 1 raises a lift up a 5 m cliff for 12 s, ring 2 extends the next bridge for 8 s) |
| 4 Grand Finale | a tube intro (bomb through the tube, runners through a curtain); a **tube junction** (three lanes the bomb cannot cross: falling platforms, a sweeper, a reverse belt; one mouth per lane); **Cannon Canyon** (a 44 m shot onto the far pad while the team rides two shuttles); a mega slide with a hot lower half; a finale belt through piston gates, an arch checkpoint, a last ring opening the podium door. Fuse 5 s from CP8 |

**`PatataPark`** was the KayKit course: ~680 m, 9 checkpoints, 4 acts, laid out on the KayKit metre grid.

| Act | Beats |
|---|---|
| 1 Block Hop | three 6 m islands zig-zagging up 1 m each; an 18° ramp; four falling platforms zig-zagging over a 24 m pit; a five-tile piston wave; a sweeper, then an 8 m wall (bomb through the windmill's hole, runners through a tunnel under a crusher) |
| 2 Hot & Cold | two bridges over a pit with staggered no-carry strips (the bomb crosses the 6 m gap twice); a laser slalom ending in a hoop; a hot climb up three 1 m steps with a cold pocket at the top and a sweeper |
| 3 Switchboard | a ring bridge over a 12 m gap (8 s); The Lock (as in PlaytestCourse, 1 m stairs and a mantle); an arch checkpoint across a 4 m gap; a lift chain (ring 1 raises a lift up a 6 m cliff for 12 s, ring 2 extends a bridge for 8 s) |
| 4 Sky Finale | a three-lane tube junction; Cannon Canyon (44 m shot, two shuttles, a cold pocket); a sky slide with hoops and a hot lower half; a finale belt through two piston gates, an arch checkpoint, a ring opening the podium door. Fuse 5 s from CP8 |

**`PatataWorks`** (M11, #83) was an enclosed potato factory at sunset: 9 checkpoints, it climbed 52 m and dropped 28 m. Its
layouts became the industrial plant's.

| Section | Bomb question |
|---|---|
| Loading dock | crate warm-up; a ring opens the dock door |
| Atrium (climbs 14 m) | two ramp flights; **Hold the way**: the receiver's plate on the middle landing raises a lift, a cross-landing pass opens the return door |
| Sorting hall | opposing belts behind interior windows; **Two routes, one bomb**: two tube routes (colour and pips) to two rooms; no-carry inspection strips; a piston and a timed door |
| Silo catwalks | a full-power low pass under a ceiling, falling grates, a crane platform |
| Cold storage | **Buy time, spend time**: a cold lane around shelves against a straight hot lane; a laser window |
| Chute | a 28 m slide with no-carry strips; **Down the chute**: the bomb takes its own tube |
| Basement furnaces | a hot press, a windmill window, a low crusher passage, a cold respite, a sweeper |
| Boiler shaft (climbs 34 m) | **Up the shaft**: four rising platforms; a cannon from the penultimate landing reaches the upper pad |
| Roof | chimneys, a final relay, the checkpoint arch, the finish under the PatataWorks board |

**`IndustrialLab`** was a short open test map for the industrial look: two checkpoints, a covered hall and a calibration
wall (1, 4 and 12 m panels per material, KayKit on one side, industrial on the other) to judge texel density.

---

## 4. Making your own obstacle prefab

Every system above is generic: its component reads **references** set in the Inspector, never child names, renderers or
materials. So any prefab, with any model, behaves as a moving platform, a door, a cannon or a zone as long as it carries the
component and its references. The logic stays one implementation; maps differ by their prefabs (ARCHITECTURE §10.7).

### 4.1 A new look for an existing system (no code)

1. In the Project window, right-click a kit prefab (for example `Platforms/Platform_Moving`) → **Create → Prefab Variant**,
   and save it in `Assets/Prefabs/Variants/`.
2. Open the variant. Switch off the kit's `Visual` (untick the GameObject).
3. Under the **moving part** (`Platform`, `Body`, ...), add an empty child, give it a **`CustomVisual`** component and put
   your model(s) under it. Renderers only: no collider in there. The builders' look passes (`NatureRestyle`,
   `IndustrialRestyle`) never touch what is under a `CustomVisual`.
4. Keep the collider (`Collision`) the size the gameplay needs. The visual may be any shape, but keep it inside the collider
   where players stand and where the bomb flies (what you see is what explodes the bomb).
5. Run **HotPatata/Kit/Validate Selected Prefabs**. `ObstaclePrefabTests` also checks every prefab of the project.

Example: `Variants/Platform_Moving_Boulder` (a moving platform drawn as a scanned boulder).

### 4.2 Another motion for an existing system (no code)

- **Moving platform:** move `Waypoint_A` / `Waypoint_B`; set speed, phase, `PingPong` or `Dwell`.
- **Door, bridge, lift (`SignalActuator`):** tick **Rotate With Waypoints** and give the two waypoints different rotations.
  The part then turns from the closed pose to the open one: a swinging gate, a drawbridge. Put the moving part's pivot on
  the hinge (offset its children). Example: `Variants/Actuator_Bridge_Drawbridge`.
- **Rotating obstacle:** axis, speed, phase.
- **Tube or cannon:** a `BombTransit` with any number of exits (hold, muzzle, pad, flight time), and one `TransitMouth` zone
  per entrance. A tube and a cannon differ only by these data and their look.
- **Zones:** a `Zone` (trigger box on the `Trigger` layer) plus effect components: `FuseZone`, `BombBarrier`, `BombGate`,
  `TransitMouth`.

### 4.3 Animate the visual from the logic (no code)

Add an **`ObstacleVisualDriver`** to the visual. It reads the system's state (`IObstacleState`) on every machine and:

- writes **`Progress`** (float 0..1) and **`Active`** (bool) into an Animator, if you give it one (parameter names can be
  changed);
- raises **`onActivated`**, **`onDeactivated`** and **`onProgress(float)`** UnityEvents, for sounds, particles or lights.

What `Progress` and `Active` mean per system:

| System | Progress | Active |
|---|---|---|
| `MovingPlatform` | along A→B | heading to (or waiting at) B |
| `SignalActuator` | open amount | opening |
| `FallingPlatform` | share of the warning shake | triggered |
| `RotatingObstacle` | share of a full turn | spinning |
| `BombTransit` | share of the delay before release | holding the bomb |
| `BombGate` | share of the hold time left | active |
| `PressurePlate` | 1 while held | held |
| `SignalSwitch` | switch travel (the targets turn over at 0.5) | opening |
| `BodyScreen` | 1 while it stands | standing (a switch may take it away) |
| `Checkpoint` | 1 once reached | reached |

The driver only reads. Presentation never changes gameplay state.

### 4.4 Override the behaviour in code

Subclass the system and override its pose:

| System | Virtual method |
|---|---|
| `MovingPlatform` | `PositionAt(a, b, u)` |
| `SignalActuator` | `PositionAt(closed, open, progress)`, `RotationAt(...)` |
| `FallingPlatform` | `ApplyPose(elapsed)` |
| `RotatingObstacle` | `RotationAt(rest, angle)` |

Keep the override a **pure function** of the value it is given (derived from the shared clock), so every machine shows the
same thing. Riders are carried by translation only.

Example (from `ObstacleKitTests`), a platform that arcs 2 m up between its waypoints:

```csharp
class ArcPlatform : MovingPlatform
{
    protected override Vector3 PositionAt(Vector3 a, Vector3 b, float u) =>
        Vector3.Lerp(a, b, u) + Vector3.up * (Mathf.Sin(u * Mathf.PI) * 2f);
}
```

A new kind of zone effect implements `IBombZoneEffect`. A new kind of switch implements `ISignalSource` (`Active`) and drives
any `SignalActuator`. One source drives exactly one actuator (PROJECT_SPEC §13.15).

### 4.5 The contract (what the validator checks)

- Every reference the system needs is set, and it points inside the prefab.
- The moving part is a child, never the object carrying the system. Its waypoints never ride on it. It has a kinematic
  `Rigidbody`.
- Colliders are box, sphere or capsule colliders (never `MeshCollider`), on a gameplay layer (`Player`, `PlayerCatch`,
  `Bomb`, `Environment`, `Hazard`, `Trigger`, `BodyScreen`), and never under a `CustomVisual`. A body screen's colliders
  are solid and on `BodyScreen`; nothing else uses that layer.
- A switch's targets are set, and none of them carries the switch (switching it off would switch the switch off).
- A part on the `Hazard` layer has a `KillZone` trigger. A door's lethal edge moves with it.
- Zones are trigger boxes on the `Trigger` layer.
- Replicated systems keep their network companion and a `NetworkObject`:
  - `SignalActuator` + `NetworkSignalActuator`;
  - `FallingPlatform` + `NetworkFallingPlatform`;
  - `BombTransit` + `NetworkBombTransit`;
  - `BombGate` + `NetworkBombGate`;
  - `SignalSwitch` + `NetworkSignalActuator`.

  A Prefab Variant inherits them.
- Gameplay cues stay readable (PROJECT_SPEC §19): lethal parts striped or banded; a tube's mouth, exit and pad share a colour
  **and** a pip count; zones keep their field and icon.

---

## 5. Designing a section: the contract

A section is not finished when it can be solved, but when it can only be solved the intended way (PROJECT_SPEC §13.20).
Write its contract first, in the builder:

```csharp
Contract(p, "One runner per branch: the bomb crosses through a bramble window before each laser curtain.",
         GapLock("west gap", from, to),            // must be beyond a slide-jump with a mantle (12.75 m flat): use 14 m
         ClimbLock("the face", floor, ledge),        // must be above a jump and a mantle (3.0 m): use 5 m
         LobLock("over the wall", thrower, wallTop)); // the wall top must touch a roof: a lob climbs 38 m
```

Then check every shortcut a team will try:

- **Around it.** A gating obstacle spans the whole walkable width; a wall runs past the floor's edge over the void (3 m),
  or meets another wall. A laser wall that stops 1 m short of a cliff is a door.
- **Through it.** Every opening is typed: bomb only (body screen), runners only (laser curtain), or both. A window is not
  bomb-only.
- **Over it.** No free-standing wall stops the bomb: it reaches a roof, and that roof runs on long enough that no lob goes
  over the whole covered stretch (PatataCanopy covers each puzzle section and its two junction decks).
- **Without it.** A plate, a ring or a mover is required only if every other way across is a 14 m gap or a 5 m face. If two
  players can walk past side by side, the bomb question is gone.
- **With one player.** A tube or cannon lets a lone carrier catch their own bomb, and every checkpoint refills the fuse: say
  so in the contract if it matters.

`CourseContractCheck` (menu **HotPatata/Course/Check Section Contracts**, and `CourseContractTests`) measures the declared
shortcuts and scans every body screen (walked round, hopped over) and laser curtain (flown round, flown over). It warns; it
never fails a build.
