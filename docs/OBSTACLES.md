# HotPatata — OBSTACLES.md

The catalogue of every obstacle system in the game, how each behaves, how it is dressed in each look, and how to make your
own. Where the maps used each system, map by map and section by section, is in [`MAP_ARCHIVE.md`](MAP_ARCHIVE.md). Rules live in [`PROJECT_SPEC.md`](PROJECT_SPEC.md) (§13
level grammar, §20 tuning); the components in [`ARCHITECTURE.md`](ARCHITECTURE.md) §10. This file is the reference
for designing new maps from the kit.

Maps: only `PassSandbox` (the kit demo and the pass range, dev builds only) since the fresh start of 2026-10-16; every earlier
map is archived (`MAP_ARCHIVE.md`, branch `archive/maps-v1`). `Bootstrap` is the menu.

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
| Hands-free plate | `Variants/PressurePlate_HandsFree` | `PressurePlate` (`countCarrier` off) | the carrier does not count, nor the thrower while the bomb flies: the holder must have passed the bomb first, and a catch on the plate lets it go (§13.19); blue, with a "throw first" glyph |
| Heavy plate | `Variants/PressurePlate_Heavy` | `PressurePlate` (`requiredBodies` 2, hands-free) + `PlateGauge` | needs two empty-handed players at once (§13.21); 3.6 m, terracotta, two footprints that light one per body, the "two bodies" glyph; `MakeHourglass` adds a sand ring and its replication to a placed one |
| Hourglass plate | `Variants/PressurePlate_Hourglass` | `PressurePlate` (`memorySeconds` 5, hands-free) + `NetworkPressurePlate` + `PlateGauge` | stays active for its sand after the last body steps off (§13.22); a ring of amber lamps empties, the pad pulses meanwhile |
| Sun beam | `Gameplay/SunBeam` | `SunBeam` (+ `SunBeamVisual`) | a source: active while a body cuts the light anywhere along it (or, with `activeWhileWhole`, while it reaches the eye) (§13.23); the bomb never cuts it; resize with `ResizeSunBeam`; a gold shaft that stops at the body, a stone eye lit while whole |
| Pivot | `Variants/Actuator_Bridge_Pivot` | `SignalActuator` (`rotateWithWaypoints`, waypoints at one point) | a slab that turns about its centre while its source is active (a quarter turn in 6 s by default, 12 s on the temple), carrying its riders round and turning their view (§13.24); `ConfigurePivot` sets its size, turn and time |
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

| System | KayKit (toy kit, §25.1) | Industrial (§25.2) | Nature (§25.3) |
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
| Heavy plate | terracotta checker, two footprints, "two bodies" glyph (cue kept) | same | same |
| Hourglass plate | the hands-free plate and a ring of amber sand lamps (cue kept) | same | same |
| Sun beam | gold shaft, stone slit and eye (cue kept) | same | same; carved masonry on the temple |
| Pivot | slab on a drum | slab | log raft on a drum; a stone slab on the temple |
| Door | portcullis | portcullis | palisade of upright logs |
| Checkpoint | square pad | square pad | campfire that lights up (pad hidden) |
| Water | — | — | lethal surface, kill zone 0.3 m below |

Lethal parts always carry stripes or bands, never colour alone (§19).

---

## 3. Map usage

### 3.1 Kept maps

- **`PassSandbox`.** One bomb, a `KitDemo` group with every kit prefab, a pass range with marks at 4, 8, 12 and 16 m, and a
  `KitDemo/BombObstacles` corner (zones, curtains, a ring-driven lift). Tests: `PrefabKitTests`, `BombObstacleTests`,
  `ObstacleKitTests`, movement and pass-feel suites. Listed in the menu in the Editor and development builds only.

### 3.2 Archived maps

Every map built before the fresh start (`PatataWilds`, `PatataCanopy`, `PatataTemple`, `IndustrialPlant`, `PatataRanger`,
`OldOak`, and the older `PrototypeCourse`, `PlaytestCourse`, `PatataPark`, `PatataWorks`, `IndustrialLab`) is described in
[`MAP_ARCHIVE.md`](MAP_ARCHIVE.md): which systems each used (§3 there), the puzzle patterns (§4), every section (§6, §7), and
how to bring one back (§9).

---

In an authored level (ARCHITECTURE §27, docs/LEVEL_AUTHORING.md §10) a placed kit object is sized with handles in the Scene
view (the resize functions below), its source picked with the Link tool, and its wiring drawn.

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

**The forest looks** (`Assets/Prefabs/Variants/Forest/`, M18.3b) are made this way by a builder, `ForestKitBuilder` (menu
`HotPatata/Kit/Build Forest Looks`; never edit them by hand): `Forest_Winch` (hourglass plate), `Forest_Lever` (hands-free
plate), `Forest_Drawbridge`, `Forest_Cage` (lift), `Forest_SignalRing` (hoop), `Forest_BellArch` (arch), `Forest_Chute` (tube:
only its pipe's materials change, a tube lays its pipe per level), `Forest_Net` (body screen), `Forest_Embers` (hot zone),
`Forest_Sparks` (laser curtain), `Forest_Gate` (door: a palisade), `Forest_Gangway` (bridge), `Forest_Raft` (moving platform),
`Forest_RottenPlank` (falling platform), `Forest_SwingingLog` (sweeper). Each keeps its base's colliders and gameplay cues (icons, a zone's colour, the hourglass's
sand, a curtain's field); only the kit's construction is hidden and drawn again in timber under a `CustomVisual`. Authored
levels place them from `GameObject > HotPatata Level > Kit > Forest` (docs/LEVEL_AUTHORING.md §14); the size handles scale
their visual with them.

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

**Sounds need no driver.** `ObstacleSounds` (ARCHITECTURE §26) hears every system above but `Checkpoint` and `BombTransit`
through folders named after its component: drop files in `Assets/Audio/SFX/Obstacles/<Type>/On` (`Active` turns on),
`.../Off` (turns off) and `.../Loop` (plays while `Progress` moves). A new system implementing `IObstacleState` gets its
folders from `HotPatata/Audio/Create Folder Skeleton`. Use the driver's events only for a sound one prefab needs and the
others of its type do not.

### 4.4 Override the behaviour in code

Subclass the system and override its pose:

| System | Virtual method |
|---|---|
| `MovingPlatform` | `PositionAt(a, b, u)` |
| `SignalActuator` | `PositionAt(closed, open, progress)`, `RotationAt(...)` |
| `FallingPlatform` | `ApplyPose(elapsed)` |
| `RotatingObstacle` | `RotationAt(rest, angle)` |

Keep the override a **pure function** of the value it is given (derived from the shared clock), so every machine shows the
same thing. Riders are carried by translation, plus rotation for a turning carrier (`FrameRotation`, the pivot).

The base classes already do the network part for a subclass, through the same virtual methods. A brand-new mover (not a
subclass) must do it itself (netcode plan §2.2–2.4):

- **Carrier** (anything a player rides): implement `IPlatformCarrier`:
  - `FrameDelta`; a carrier that moves calls `CarrierMotion.Moved(this, part, body)` in `Update`, right after writing its
    part's pose. It carries the riders and puts the collider where the part is drawn (#97); without it, riders stand on
    last fixed step's deck and lose their footing on anything that rises faster than 2 m/s;
  - `Moves`;
  - `AnchorPosition` (the carried part's position);
  - `CarrierId`, with `CarrierRegistry.Register(this)` in `OnEnable` and `Unregister` in `OnDisable`. The id comes from
    the scene and the sibling path; never give two carriers the same transform.
- **Time-driven** (moves with the clock or a replicated event time): implement `ITimePosed`:
  - `PosedTransform`;
  - `TryPoseAt(serverTime)`: the pose at any time, from the same pure function `Update` uses;
  - `LethalAt(serverTime, zone)`.

  A child `KillZone` then becomes a moving hazard. Online, the host judges remote players against it at the time they
  saw it (`HazardRewind`), not from its own lagging copy.

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

**A section for three** (PROJECT_SPEC §15e) also says why two players fail (`Contract(p, force, 3, "With two: ...", locks)`, or the
temple's `Trio`). Two players fail when the section needs two tasks at once; the usual ways to lock it:

- **The pair and the third.** One holds the way while the bomb must stay alive longer than a fuse (a ride, a chamber behind a
  wall): nobody catches their own throw, so a lone carrier explodes. Put the holder where no throw reaches them (a `Shrine`, an
  alcove, behind lasers), or the holder and the carrier juggle: catch, throw back, step on again.
- **The heavy plate.** Two empty hands: a throw in the air is still the thrower's, so two players never hold it by juggling.
- **The cycle.** Three ways, each opened from another place; two players always leave one empty.
- **The spread.** Two sources one body might hold in turn: `SpreadLock(name, a, b, via...)` measures the walk between them
  (round the walls) against a sprint through the first one's hourglass.
- **The holder left behind** is let through by someone already across (a ring thrown through, an hourglass tapped, a pivot that
  turns back): that way must need someone across, or two players solve it.
