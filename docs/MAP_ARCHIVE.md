# HotPatata — MAP_ARCHIVE.md

Everything the maps of HotPatata were made of, up to the fresh start of 2026-10-16: every obstacle, every puzzle and every
parkour beat of every course ever built, and where to find each map again. This is the design memory for the next maps.

- **The maps themselves** (scenes, builders, tests, generated art) are kept intact on the branch **`archive/maps-v1`**
  (cut from `main` at `15c8c9c`, PR #114). Nothing on it is deleted. §9 says how to bring a map back.
- **The older courses** removed before that (`PrototypeCourse`, `PlaytestCourse`, `PatataPark`, `PatataWorks`,
  `IndustrialLab`) are in git history before the project cleanup; their layouts are in §7.
- **The kit stays.** Every obstacle system, prefab, look and level tool below still exists on `main`; only the maps went.
  The rules of the kit are in `PROJECT_SPEC.md` §13 (level grammar), `OBSTACLES.md` (systems, looks, own prefabs, contracts)
  and `LEVEL_AUTHORING.md` (building a level by hand).

Contents: 1 the parkour kit · 2 the bomb rules a puzzle plays with · 3 the obstacles · 4 the puzzle patterns · 5 the shortcut
locks · 6 the maps, section by section · 7 the removed courses · 8 lessons · 9 restoring a map.

---

## 1. The parkour kit (what a body can do)

The numbers every map was measured against (`PROJECT_SPEC` §11, §20). A map that wants to stop a player must beat them; a map
that wants a player through must stay inside them.

| Move | What it does | Numbers |
|---|---|---|
| Run / sprint | sprint is forward-ish only; speed above the target bleeds off gently | 8 m/s, sprint 11 m/s |
| Jump | single jump, moderate air control, extra drag above sprint speed (no bunny-hop) | coyote 0.1 s, buffer 0.1 s |
| Momentum slide | crouch at 7 m/s or more; boost, then friction ends it in about a second; faster downhill | +3.5 m/s, exit 5 m/s |
| Slide-jump | keeps the slide's full horizontal speed; landings keep momentum | the longest reach on the flat |
| Crouch-walk | under any ceiling too low to stand | 4 m/s, body 1.0 m |
| Mantle | moving into a ledge while airborne climbs it automatically (never onto hazards or body screens) | ledge 0.1–1.4 m above the feet, 0.28 s |
| Ride | lifts, movers, pivots, belts carry riders; jumping off keeps the carrier's velocity | up to 12 m/s kept |
| Launch pad | throws a player straight up to a set height | 6 m in the first course |

**The reach a map is locked against** (`PROJECT_SPEC` §13.20):

- **Gap**: a slide-jump with a mantle crosses 12.75 m on the flat (more downhill). A gap that must be bridged is **14 m**.
- **Face**: a jump and a mantle climb 3.0 m. A face that must not be climbed is **3.4 m or more** (the courses used 5 m).
- **Lob**: a full-charge throw climbs about 38 m. A wall only stops the bomb if it **reaches a roof**.
- **Crusher**: never closes below **1.45 m**, so a crouch or a slide always gets under.
- **Body**: 1.8 m standing, 1.0 m crouched or sliding (a hedge raised 1.3 m lets only a slider under).

No wall-run, dash, double jump, grab, stamina or combat (out of scope, spec §17.2).

---

## 2. The bomb rules a puzzle plays with

Every obstacle asks a question about the bomb (`PROJECT_SPEC` §13: "if it plays the same without the bomb, it is filler").

- **One bomb**, a **hold fuse** of 6 s (each course dropped it to 5 s, then 4.5 s near its end); a catch refills it, a
  checkpoint refills it. Warning phase 2 s.
- **Nobody catches their own throw.** So keeping the bomb alive longer than a fuse takes **two players**: that is the root of
  every "pair" puzzle (a ride longer than a fuse, a chamber behind a wall).
- **Catching is never automatic** (a timed catch, window 0.4 s). A plain ballistic arc, no homing. Normal pass 8–12 m.
- **A thrown bomb touching anything explodes**; a held bomb never does. Walls and ceilings are environment.
- **Any explosion resets the section** for the whole team (~1 s).
- **A bomb in flight is still its thrower's**: a hands-free plate does not count the thrower while it flies.
- **In transit** (tube, cannon) the fuse stops and nothing can catch or explode it.

---

## 3. The obstacles

Every system in the kit, what question it asks, how it was tuned, and which maps used it. Prefabs are in
`Assets/Prefabs/{Platforms,Obstacles,Gameplay,Variants}`; the full behaviour tables are `OBSTACLES.md` §1.

Map keys: **W** PatataWilds · **C** PatataCanopy · **T** PatataTemple · **I** IndustrialPlant · **R** PatataRanger · **O** OldOak ·
**S** PassSandbox · **old** the removed courses of §7.

### 3.1 Ground and movers (parkour)

| Obstacle | Prefab | Question / use | Typical tuning | Maps |
|---|---|---|---|---|
| Floor, ledge, step, island | `Platform_Basic`, `Platform_Narrow` | gap passes (§13.1), relays up steps (§13.3) | steps of 1.2 m (one mantle each); islands 4–4.5 m apart for the first jumps | all |
| Ramp / stair | builder ramps, timber stairs | climbs between floors; a relay up a rise | 6–8 m wide | W C T I R O |
| Mega slide | a steep ramp marked slide | pass sideways at 15–18 m/s (§13.12); no-carry strips on it; a hedge only a slider fits under | 18° to 30°, 14 m wide, drops of 18–28 m | W C I old |
| Moving platform (shuttle, raft) | `Platform_Moving` | sync thrower and receiver (§13.2); a ride longer than the fuse forces passing while moving | 2–2.5 m/s, dwell 0.25–0.3, 8–16 m strokes; 11 m/s over 64 m for a long shuttle | W C T I R old |
| Rising platforms | `Platform_Moving` (vertical) | catch under movement pressure (§13.8): ride up while the bomb comes by cannon or tube | four rafts of 8.5 m each up a 34 m shaft, 4 m/s | W I old |
| Elevator | `Platform_Elevator` | vertical relay; runners over a wall while the bomb goes through | — | old |
| Piston | `Obstacle_Piston` | pass when the line opens, onto a moving target (§13.10); a wave in a row | travel 3.3 m, dwell 0.4, phases 0 / 0.5 | W I old |
| Falling platform (rotten planks, grates, twigs) | `Platform_Falling` | commit, don't wait (§13.7); one use each, so each runner takes a line | rows of 3, 6 m apart; four lines of six in Branches pourries | W C I R old |
| Conveyor (belt, log drive, rolling log) | `Platform_Conveyor` | lead a receiver who is carried or held back (§13.9) | ±2–3 m/s, opposite lanes side by side | W C I old |
| Launch pad (moss mound, leaf trampoline) | `LaunchPad` | catch at the apex (§13.8); in spores only empty hands fly | to the next tier, 5 m | W C old |
| Pivot | `Variants/Actuator_Bridge_Pivot` | where does the holder send the team (§13.24); riders carried round | 3 × 32 m slab, a quarter turn in 12 s | T |
| Rope bridge, walkway, deck round a trunk | timber pieces (`LevelPiece`) | the organic route of the forest levels; walked on through a smoothed surface | — | R O |

### 3.2 Hazards (lethal to players)

| Obstacle | Prefab | Question / use | Typical tuning | Maps |
|---|---|---|---|---|
| Kill zone, pit, water | `KillZone` | under every pit, under every water surface 0.3 m down, 10 m under each section of the void courses | — | all |
| Sweeper (swinging log, sweeping branch) | `Obstacle_Sweeper` | keep passing while jumping a knee-high lethal bar (§13.11) | 12–18 m long, 70–80°/s; three levels swept wall to wall in La Spirale | W C T I R old |
| Rotating bar | `Obstacle_RotatingBar` | blocks and pushes, not lethal | — | S old |
| Windmill (water wheel, fan) | `Obstacle_Windmill` | its blades guard the only bomb window through a wall | — | W I old |
| Crusher (log ram, stamp, press) | `Obstacle_Crusher` | lethal underside; when down a low tunnel to slide under and throw low through (§13.5) | 1.45 m clearance down; a 10 m ram over a hot zone | W I old |
| Door / herse / palisade (closing edge) | `Actuator_Door` | a portcullis that closes completely; lethal lower edge while it moves | — | W C T I R |

### 3.3 Bomb zones

| Obstacle | Prefab | Question / use | Typical tuning | Maps |
|---|---|---|---|---|
| Forbidden strip (no-carry weir, scree, riffle, spores) | `Zone_Forbidden` | the carrier may not cross: throw across (§13.13); on a slide; riding a shuttle so only empty hands ride; around launch pads so only empty hands fly | 2–6 m deep, the full width | W C I old |
| Hot zone (steam vent, sap, embers, sun-baked stones) | `Zone_Hot` | the fuse burns ×2: keep the rhythm (§13.14) | 7–26 m long; a whole short gallery | W C I O old |
| Cold zone (spring, moss, larder, respite) | `Zone_Cold` | the fuse burns ×0.5: a pocket beside the line where a lone carrier waits for a teammate | 6 × 6 m pockets; one whole long gallery in Les Galeries | W C I R old |
| Laser curtain (cairns, sparks) | `LaserCurtain` | runners walk through, a thrown bomb crossing it explodes: **runners only** | doorways 3.5–4 m wide, 4 m tall | W C T I R O old |
| Body screen (brambles, net) | `Obstacle_BodyScreen` | stops every body, lets the bomb, the aim and the catch through: **bomb only** (§13.18) | windows 3 × 2.6 m from 1 m up; a whole hedge; raised 1.3 m over a slide | C T R |

An opening is always typed: **bomb only** (a screen in it), **runners only** (a curtain in it) or **both** (empty).

### 3.4 Signals: sources and what they drive

One source drives exactly one actuator. No AND/OR.

| Source | Prefab | Question / use | Typical tuning | Maps |
|---|---|---|---|---|
| Ring (vine ring, signal hoop) | `BombGate_Ring` | the bomb must fly through it; opens something for a time (§13.15) | holds 6–13 s; 0 = latched | W C T I R old |
| Arch checkpoint (bell arch) | `BombGate_Arch` | the section only counts after one clean pass through the arch (§13.17) | — | W C T I R old |
| Pressure plate | `PressurePlate` | any body, carrier included, holds the way | — | W C I R old |
| Hands-free plate (lever) | `Variants/PressurePlate_HandsFree` | the carrier does not count: the holder must have passed first; a catch on it lets go (§13.19) | — | C T R |
| Heavy plate | `Variants/PressurePlate_Heavy` | two empty-handed bodies at once (§13.21) | 3.6 m slab | T |
| Hourglass plate (winch) | `Variants/PressurePlate_Hourglass` | stays active for its sand after the last body leaves: who relieves the holder (§13.22) | 4–6 s of sand | T R O |
| Sun beam | `Gameplay/SunBeam` | active while a body cuts it anywhere along it, carrier included; the bomb never cuts it (§13.23) | 15–48 m shafts | T |

| Actuator | Prefab | Use | Maps |
|---|---|---|---|
| Door (herse, palisade, gate) | `Actuator_Door` | opens while its source is active | W C T I R |
| Bridge (rising bridge, drawbridge) | `Actuator_Bridge`, `Variants/Actuator_Bridge_Drawbridge` | spans a 14 m gap while its source is active; the void courses raised it from under the kill plane | C T R O old |
| Lift (rope lift, cage, pulley) | `Actuator_Lift` | rises while its source is active, carries riders | W C T I R old |
| Pivot | `Variants/Actuator_Bridge_Pivot` | turns about its centre while its source is active | T |
| Switch | `Actuator_Switch` | turns targets on or off: cuts a laser curtain, parts a bramble hedge, clears spores | C |

### 3.5 Transit (the bomb goes `InTransit`)

| Obstacle | Prefab | Question / use | Typical tuning | Maps |
|---|---|---|---|---|
| Tube (hollow log, stone head, hollow branch, chute) | `Obstacle_Tube` | throw into the right mouth; is the receiver at the exit (§13.16)? Mouth, exit and pad share a colour **and** a pip count | up to three routes; delay ~1.2 s (4 s down the rapids) | W C T I R old |
| Cannon (stump catapult, seed catapult) | `Obstacle_Cannon` | the bomb shown in its basket, fired far and high | 0.35 s; 5 s delay to let a receiver ride 70 m | W C I old |
| Hoop | `Obstacle_Hoop` | a spinning ring: a moving pass window | — | W old |

### 3.6 Run pieces

| Piece | Prefab | Use |
|---|---|---|
| Checkpoint | `Checkpoint` | activates once every player is inside; the reset target; may change the hold fuse from it on (`holdFuseOverride`); a nature checkpoint is a campfire that lights |
| Finish | `FinishZone` | the run ends once every player is inside |
| Spawn | `PlayerSpawn` | start positions and per-checkpoint respawns |
| Shrine / alcove | walls (Temple) | a niche round a plate: the holder is out of any throw |
| Low ceiling / roof | `CourseCeiling` blocks, leaf and stone roofs | forces low throws (§13.5); a roof over a puzzle so no wall is lobbed over |

---

## 4. The puzzle patterns

The recipes the courses used, from the simplest. Each names the systems it chains and where it was built. All were solvable by
two players except §4.4 (three).

### 4.1 Movement and passing

| Pattern | How it works | Built in |
|---|---|---|
| **Gap pass** | the receiver crosses first, turns, catches across the gap | Premier pont (C1), Trailhead (W1), every first section |
| **Relay up** | steps of 1.2 m, a pass up each rise; the carrier never climbs with the bomb at full fuse | Fern Terraces (W2), Cliff Base Relay (W11), Stair Relay (old) |
| **Moving pair** | two platforms that line up now and then; pass at the alignment | Brook Crossing (W3), Rapids Rafts (W7), Moving Pair (old) |
| **Ride longer than the fuse** | a ride over void longer than 6 s: the bomb must change hands while moving | Branches balançoires (C8), Heart shuttle (C15) |
| **Commit lines** | rotten planks that fall once used: each runner takes a line, the bomb passes between lines | Branches pourries (C9), Rotten Boardwalk (W4) |
| **Low tunnel** | a low ceiling or a down crusher: throw low and flat | Rotten Boardwalk (W4), Behind the Falls (W17), Void catwalks (I) |
| **Lead the belt** | opposite conveyors: the throw must lead a receiver who is carried | Log-Drive Lanes (W6), Branches mouvantes (C17), Sorting line (I) |
| **Apex catch** | a launch pad throws the receiver up; catch at the top of the jump | Alpine Meadow (W21), Vertical Catch (old) |
| **Sweeper relay** | jump a lethal bar while the bomb keeps moving | Ledge Traverse (W14), La Spirale (C13, three levels) |
| **Mega slide** | two sliders pass sideways at speed; no-carry riffles on the way | Mudslide (W9), Glissade (C23), the Chute (I) |

### 4.2 Zones

| Pattern | How it works | Built in |
|---|---|---|
| **Throw across** | a no-carry strip on the floor: walk over empty-handed, throw over it | Log-Drive Lanes (W6), Switchbacks (W22) |
| **Laser window** | a wall whose side passages are laser curtains: the window is the only bomb way | Ember Cave (W13), Wheel Gorge (W18), Cold storage (I) |
| **Bomb way / runner way** | in one wall a body screen (bomb only) beside a laser curtain (runners only); both reach the roof | Le Filet (C2), Couloir de ronces (C18, a four-wall slalom), La Haie (C21) |
| **Hot lane vs cold lane** | the same corridor split: a short hot lane, a long cold one | Ember Cave (W13), Cold storage (I), Les Galeries (C14, a real choice) |
| **Cold pocket wait** | a lone carrier waits on cold moss while the teammate gets in position for a tube, a cannon or a ring | Pont-levis croisé (C7), Tronc creux (C12), Canon à graines (C19), Winch house larder (R5) |
| **Spores on a mover** | a forbidden zone riding a shuttle, or wrapping launch pads: only empty hands ride or fly, the bomb goes another way | Feuilles-trampolines (C16), Canon à graines (C19) |

### 4.3 Signals (two players)

| Pattern | How it works | Built in |
|---|---|---|
| **Ring opens the gate** | a pass through the ring opens a palisade / door for 8 s; the receiver must already be running | Trailhead (W1), Gatehouse (I), every Wilds act |
| **Ring raises the bridge** | a pass through the ring raises a bridge over a 14 m gap for 8 s | L'Anneau de lianes (C3) |
| **Race the ring** | the ring opens a gate 50 m away for 7 s: sprint, jump, keep passing | Course contre l'anneau (C20), the Ranger's lookout race (R6, 13 s) |
| **Hold the way** | a runner holds a plate to keep a lift up while the carrier passes; the plate sits where the pass lands, so the holder is the receiver; a ring on the far side opens the door that brings the holder back | Hold the Rope (W12), the Atrium (I) |
| **Throw back through the ring** | an empty-handed holder raises the carrier's bridge; the carrier crosses and throws back through a ring over the gap, which raises the holder's bridge | La Plaque (C4), La Dalle lourde (T1, heavy) |
| **Crossed drawbridges** | two branches: the east plate raises the west bridge, the ring in the divide raises the east one | Pont-levis croisé (C7) |
| **Cut the lasers** | a hands-free plate beyond a laser curtain switches it off; the catch on the plate brings it back | Les Lucioles (C5) |
| **Clear the spores** | a far hands-free plate switches off 48 m of spores under a low branch; the catch brings them back | Le Pont des spores (C22) |
| **Part the hedge** | a ring parts a bramble hedge for 6 s | La Haie qui s'ouvre (C21) |
| **The pulleys** | a hands-free plate below lifts the carrier; they throw down and stand on a plain plate above to lift the holder | La Poulie (C11), the Ranger's oak cages (R3) |
| **The winch and its sand** | an empty-handed body cranks an hourglass winch; its sand holds the gate or bridge while the cranker follows | the Ranger's yard gate (R1), ravine (R2), twin fir (R6); OldOak's drawbridge |

### 4.4 Signals for three (PatataTemple)

Two tasks at once: one holds the way while the pair keeps the bomb alive longer than a fuse.

| Pattern | How it works | Built in |
|---|---|---|
| **The heavy slab** | two empty hands on a heavy plate, the third carries | La Dalle lourde (T1), Le Seuil (T5), La Réunion (T10), Le Puits (T11), L'Autel (T15) |
| **The relay** | the holder in an alcove raises the herse for the pair; inside, an empty hand taps an hourglass plate whose herse lets the holder in | La Relève (T2), three times in Le Couloir des pièges (T12) |
| **The shrine pivot** | the holder in a shrine turns the slab to a dock; the pair boards; let go, it turns back for 12 s carrying them; their ring throw raises the holder's bridge | Le Pivot (T3), La Rose (T9), L'Autel (T15) |
| **Cut the beam from a ride** | a rider on a shuttle cuts the sun beam over the chasm; the herse rises for the pair; the rider joins through lasers | L'Œil du soleil (T4), Le Gué des trois pierres (T13) |
| **The cycle of three wings** | three wings side by side; each wing's herse is opened from another wing (hourglass or beam); all hold, juggle the bomb through bramble windows, let go together | Le Partage (T6), Les Rayons croisés (T8), La Croisée (T14) |
| **Stone heads** | the nave is shut by a herse with lasers behind it; the bomb crosses only by two tubes; the plate holder is 26 m from the exit, so the third waits there | Les Têtes de pierre (T7) |
| **The let-go sprint** | a heavy slab with a 4 s hourglass raises a herse 26 m away: everyone lets go together and runs | Le Seuil (T5) |

### 4.5 Transit

| Pattern | How it works | Built in |
|---|---|---|
| **Two routes, one bomb** | the team splits round a wall; two tube mouths (green one pip, violet two pips) lead to two banks; the thrower picks the one the receiver ran to | Hollow Log Junction (W5), Two Banks (W8), Sorting line (I) |
| **The bomb takes its own way down** | the runners slide down, the bomb takes a tube to the pad at the bottom | Down the Rapids (W19), the Chute (I) |
| **Up the shaft** | a cannon or a tube fires the bomb to the top while the receiver rides rising platforms | Up the Cliff (W15), Gorge Lock (W20), Boiler shaft (I) |
| **The only way up** | the climb is behind lasers and the hall is roofed: the bomb goes up only by the tube | Le Tronc creux (C12), L'Écluse finale (C24) |
| **Delayed catapult** | no throw crosses 70 m under a branch roof and the shuttle carries spores: throw in the catapult as the receiver boards, it fires 5 s later | Canon à graines (C19) |
| **Decoy chutes / hoops (observation)** | three chutes or hoops, only one leads on; the runner sees which and calls it; a wrong one costs time, never the run | the Ranger's oak hoops (R3), winch house chutes (R5) |

---

## 5. The shortcut locks (contracts)

From PatataCanopy on, every section declared a `SectionContract`: what it forces, and each shortcut with the geometry that
locks it. `CourseContractCheck` measured them. The lock kinds:

- **GapLock**: the gap is 14 m or more.
- **ClimbLock**: the face is 5 m (3.4 m minimum).
- **LobLock**: the wall reaches a roof that runs long enough that no lob passes the covered stretch.
- **SpreadLock** (three players): two sources one body cannot hold in turn: the walk between them round the walls is longer than
  a sprint through the first one's hourglass.
- **"With two"** (three players): why two players fail.

What the checks found worth remembering:

- **Around it**: a gating obstacle spans the whole walkable width; a laser wall 1 m short of a cliff is a door.
- **Through it**: a 2.4 × 2 m window is not bomb-only (a player climbs through).
- **Over it**: no free-standing wall stops a lob.
- **Without it**: if two players can walk past side by side, the bomb question is gone.
- **With one player**: a tube or cannon lets a lone carrier catch their own bomb; every checkpoint refills the fuse.

PatataWilds had no contracts; the review found shortcuts in about 15 of its 25 sections (that was M14, never done).

---

## 6. The maps, section by section

Distances are metres along the section (+Z forward); "CP" is the checkpoint that ends the section.

### 6.1 PatataWilds (M12) — the nature course

A climb through a forest, along a river and up granite cliffs to a summit, from dawn to dusk. 5 acts, 25 sections, 25
checkpoints (campfires), about 100 m of climb, 20+ minutes. Each section a 24 m corridor between cliffs 7 m above its floor;
water is lethal. Two players. Fuse 5.0 s from CP21, 4.5 s from CP24. No contracts. Built by `PatataWildsBuilder` (+ `.Acts`),
terrain by `NatureDressing`, trees by `NatureTreeBuilder`. Many sections reuse the industrial plant's proven layouts, re-dressed.

![PatataWilds, act 1](images/patata-wilds/w01.png)

| # | Section | What is there | What it asks |
|---|---|---|---|
| **Act 1** | **Misty Hollow** (dawn) | | |
| 1 | Trailhead Glade | four boulders, a vine ring (8 s) opening a palisade | warm-up passes; first ring |
| 2 | Fern Terraces | rock terraces in 1.2 m steps up to 7 m | relay up each rise |
| 3 | Brook Crossing | three stepping stones, a log raft (8 m stroke) over a pool, one more stone | receiver crosses first; pass to and from the raft |
| 4 | Rotten Boardwalk | a ramp to a boardwalk 4 m up under a fallen trunk canopy (ceiling 8.5 m), three rotten planks, a bog raft, bog water | low throw; commit on the planks; time the raft |
| 5 | Hollow Log Junction | a rock spine splits the floor; a two-route hollow log crosses lanes; a rising stone; a vine ring and palisade | split lanes; pick the log to the receiver's lane |
| **Act 2** | **River Run** (noon) | | |
| 6 | Log-Drive Lanes | two log drives running opposite ways (±2 m/s) over the river, a no-carry weir on each, a window between lanes, a ring and palisade | lead the receiver; throw across the weirs |
| 7 | Rapids Rafts | four rafts out of step over the rapids | jump and catch on moving rafts |
| 8 | Two Banks, One Bomb | a river channel splits two banks; two hollow logs (green/1 pip, violet/2 pips) | the thrower picks the bank the receiver ran to |
| 9 | Mudslide | an 18 m mud chute, two no-carry riffles | pass down the slide, over the riffles |
| 10 | Mill Race | a 10 m wall: the water wheel guards the bomb window, a log stamp (1.45 m) over the runners' tunnel; a cold spring after | the bomb through the wheel, the runners under the stamp |
| **Act 3** | **Granite Cliffs** (late afternoon) | | |
| 11 | Cliff Base Relay | granite ledges up to 10 m, a rope lift on the right | relay up the ledges |
| 12 | Hold the Rope | a stone plate on a 7 m middle ledge holds a rope lift; a return vine ring opens the gate that brings the holder back | the holder is the receiver |
| 13 | Ember Cave | a torch-lit 130 m hall: three cold springs against three steam vents along a spine; a cairn laser window at the end | cold lane vs hot lane; the window |
| 14 | Ledge Traverse | a swinging log (18 m, 75°/s) sweeps the ledge | jump it, keep passing |
| 15 | Up the Cliff | four rising rafts up a 34 m chimney; a stump catapult fires the bomb to the cliff top | ride up while the bomb flies |
| **Act 4** | **Waterfall Gorge** (sunset) | | |
| 16 | Spray Bridges | two plank bridges over a gorge, one with two rotten planks; a timed vine ring and palisade | pass bridge to bridge; commit |
| 17 | Behind the Falls | a ledge under a 5 m overhang behind a waterfall, cold spray, a plunge pool | low throws; breathe in the spray |
| 18 | Wheel Gorge | a cairn laser window, then a willow hoop turning in a wall; a 6 m rise | the window; time the hoop |
| 19 | Down the Rapids | a 28 m wet chute with three no-carry riffles; the bomb takes a hollow log (4 s delay) to the pool court | slide while the bomb rides the log |
| 20 | Gorge Lock | four rising rafts up the lock; a hollow log up to the top ledge | climb while the bomb goes up |
| **Act 5** | **The Summit** (dusk, 5 s fuse) | | |
| 21 | Alpine Meadow | two moss mounds (launch pads) to a 4 m terrace; side steps the slow way | apex catch |
| 22 | Switchbacks | two paths up 16 m across a gully, scree no-carry strips | pass across the gully |
| 23 | Boulder Run | a 10 m log ram over sun-baked stones (hot), two rising pillars out of phase | time the ram on a fast fuse |
| 24 | Knife Edge | a 6 m wide ridge climbing 12 m with drops both sides, a timed ring and the last palisade (4.5 s fuse from here) | relay on the ridge |
| 25 | Summit Arch | the arch checkpoint, a last relay, the finish beacon | one clean pass through the arch |

### 6.2 PatataCanopy (M13) — the tree tops, every section with a contract

Decks, bridges and branches over a forest floor 40 m down, the void as the default; puzzle sections under a leaf roof 7 m up
that every cross wall reaches; 20 × 20 m junction decks between sections. 5 acts, 25 sections, about 2 km. Short puzzles
(read in ten seconds), hard execution. Two roles that scale. Fuse 5.0 s from CP15, 4.5 s from CP20. Built by
`PatataCanopyBuilder` (+ `.Acts`, `.Forest`, `.Atmosphere`, `.Props`).

| # | Section | What is there | Contract (what it forces) |
|---|---|---|---|
| **Act 1** | **L'Orée** (dawn): one system at a time | | |
| 1 | Premier pont | a rope bridge, three islands with 4 and 4.5 m gaps | jump the gaps: the receiver crosses first, turns, catches |
| 2 | Le Filet | two walls to the roof, each with brambles (bomb) beside lasers (runners), the second a bramble window | the bomb through the brambles, the runners through the lasers |
| 3 | L'Anneau de lianes | a vine ring raises a bridge over 14 m for 8 s | pass through the ring, cross within 8 s |
| 4 | La Plaque | a hands-free plate raises the carrier's bridge; a ring over the gap raises the holder's | an empty-handed holder; the carrier throws back through the ring |
| 5 | Les Lucioles | a wall with a laser doorway; a hands-free plate beyond switches the lasers off | the runner crosses and stands on the plate; the bomb flies through; the catch on the plate brings the lasers back |
| **Act 2** | **Les Ponts suspendus** (noon): combined | | |
| 6 | Les Deux Branches | two branches split by a bark wall to the roof, three bramble windows in it, lasers on one branch where the other has none, rotten twigs | one runner per branch; the bomb changes branch before each curtain |
| 7 | Pont-levis croisé | two branches, a 13.5 m gap on each; the east hands-free plate raises the west bridge; a ring in the divide (in a bramble window) raises the east one; cold moss | the east runner holds; the west carrier throws through the divide ring |
| 8 | Branches balançoires | two swinging branches over 22 and 18 m of void, out of phase | a ride outlasts the fuse: pass while moving |
| 9 | Branches pourries | four lines of six rotten twigs over 32 m of void | each runner commits to a line; the bomb passes between lines |
| 10 | L'Écluse | a ring opens a gate (8 s); a sap chamber ×2; lasers for the runners, a ring in a bramble window for the bomb, which raises the last bridge | everything of act 2 in a row |
| **Act 3** | **Le Grand Chêne** (late afternoon): inside the hollow oak | | |
| 11 | La Poulie | a 14 m inner wall; a hands-free plate below lifts lift A, a plain plate above lifts lift B | the holder lifts the carrier, the carrier throws down and lifts the holder |
| 12 | Le Tronc creux | a roofed hall, a climb behind lasers, a hollow branch tube up 12 m, cold moss | the bomb goes up only through the branch; the carrier waits on the moss |
| 13 | La Spirale | three levels 2.5 m apart, each swept wall to wall by a sweeping branch | jump, climb, keep passing |
| 14 | Les Galeries | two galleries split by a wall with bramble windows: a short ember one (×2), a long dew one (×0.5) round six baffles | a real choice |
| 15 | Le Cœur du chêne | a shuttle over a 14 m pit, the arch checkpoint (5 s fuse from here) | ride; one clean pass through the arch |
| **Act 4** | **La Cime dans le vent** (sunset, 5 s fuse): speed | | |
| 16 | Feuilles-trampolines | leaf trampolines up two 5 m faces, standing in spores | only empty hands fly; the bomb is thrown up each tier |
| 17 | Branches mouvantes | a rolling log against you (−3 m/s), a shuttle over 14 m, two log drives running opposite ways | lead the receiver on moving logs |
| 18 | Couloir de ronces | a slalom of four walls: lasers alternating sides, brambles in the middle | leapfrog: the runner through the lasers, the bomb straight through the brambles, four times |
| 19 | Canon à graines | 70 m of void under a low branch roof; a shuttle (11 m/s) carrying spores; a seed catapult with a 5 s delay; cold moss | only the catapult crosses with the bomb |
| 20 | Course contre l'anneau | a ring opens a gate 50 m away for 7 s over branches and steps (4.5 s fuse from here) | sprint, jump, keep the bomb moving |
| **Act 5** | **Le Sommet** (dusk, 4.5 s fuse): twisted | | |
| 21 | La Haie qui s'ouvre | a ring parts a full bramble hedge for 6 s (a switch); then lasers and a bramble window | everyone through in 6 s; then runner way / bomb way |
| 22 | Le Pont des spores | a 48 m bridge of spores under a 4.5 m branch; a far hands-free plate clears them; cold moss | a runner crosses to the plate; the carrier runs the cleared bridge and throws before the catch brings the spores back |
| 23 | Glissade de la grande branche | a 24 m slide; two hedges raised 1.3 m, their gap full of spores | only a slider fits under, empty-handed: the bomb flies to a slider ahead |
| 24 | L'Écluse finale | bramble window and lasers, a sap chamber, spore trampolines up two faces, a hollow branch, a ring raising the last bridge for 6 s | everything at once on 4.5 s |
| 25 | Arche du sommet | a last shuttle, the arch, the finish, the beacon | one clean pass through the arch |

### 6.3 PatataTemple (M15) — three players

A temple in ruins over a jungle valley: a causeway up the valley (act 1), a ring of three-wing sections round the great stepped
pyramid (act 2), the well up 30 m to a skyway ending on the altar over the summit (act 3). Stone decks over the canopy, puzzle
sections under a stone roof 7 m up, octagonal courts between sections. 3 acts, 15 sections, about 1.4 km, 15–20 minutes.
**Three players minimum** (the lobby waits for three). Fuse 5.0 s from CP8, 4.5 s from CP13. Every contract says why two fail.
Built by `PatataTempleBuilder` (+ `.Acts`, `.Ruins`, `.Props`).

| # | Section | What is there | Contract / "with two" |
|---|---|---|---|
| **Act 1** | **La Chaussée** (misty dawn) | | |
| 1 | La Dalle lourde | a heavy slab raises a bridge over 14 m; a ring over the gap raises the second | two hold, the carrier crosses and throws back. With two: only one is empty-handed |
| 2 | La Relève | an alcove plate raises a herse; inside, a 6 s hourglass plate raises the return herse | the relay. With two: the holder is out of any throw, the carrier is alone behind the wall |
| 3 | Le Pivot | a shrine plate turns a 32 m pivot (12 s) to the east dock; the pair rides it into the enclosure; a ring throw raises the holder's bridge | With two: the carrier rides alone longer than a fuse |
| 4 | L'Œil du soleil | a shuttle over a 44 m chasm; a sun beam over the shuttle's path raises the herse; the rider joins through lasers | With two: the rider is the only one who can cut the beam |
| 5 | Le Seuil | a heavy slab with a 4 s hourglass raises the great herse 26 m away; the arch checkpoint | let go together and run. With two: two empty hands are never free |
| **Act 2** | **Les Trois Ailes** (white noon) | | |
| 6 | Le Partage | three wings, three 5 s hourglass plates, each raising another wing's herse; bramble windows in the divides | stand on all three, juggle, let go together. With two: a plate is empty (76 m apart round the divides) |
| 7 | Les Têtes de pierre | the nave shut by a herse with lasers behind; two stone-head tubes to the west and east wings; the plate 26 m beyond the west lasers | the third waits at the exit. With two: nobody at the exit |
| 8 | Les Rayons croisés | a sun beam down each wing, each opening another wing's herse; pillars where the light ends | nave, then east, then west, each staying in the light. With two: one wing has nobody in its light |
| 9 | La Rose | the wings end round a pit; the nave's holder behind lasers turns a pivot to the west and east docks | the pair rides it to the exit. With two: the carrier rides alone |
| 10 | La Réunion | two wings end at a wall; a heavy slab with a 5 s hourglass raises the east wing's bridge; the arch | With two: two empty hands are never free |
| **Act 3** | **Le Sanctuaire** (storm) | | |
| 11 | Le Puits | a heavy slab with a 5 s hourglass brings a lift down a 30 m shaft (5 s travel) | all board and ride up. With two: nobody is up there to throw to |
| 12 | Le Couloir des pièges | three walls, each a shrine plate (herse) and a 4 s hourglass plate (gate); a sweeper between the last two | three relays, roles turning |
| 13 | Le Gué des trois pierres | three stones crossing 42 m in step; a beam over the east stone's last stretch raises the west herse; east lasers | With two: riding together leaves nobody in the light |
| 14 | La Croisée | the wings again: the west hourglass raises the nave bridge, the nave hourglass the east herse, the east beam the west herse | 4 s of sand. With two: one wing is empty |
| 15 | L'Autel | a heavy slab (4 s) raises the forecourt bridge; a beam behind lasers turns the last pivot to the docks; the arch, the beacon | With two: the light is behind lasers the bomb never crosses |

### 6.4 IndustrialPlant — the closed factory

A closed, lamp-lit factory: eleven rooms, about 680 m, nine checkpoints. It climbs (an atrium, a 34 m boiler shaft), drops (a
28 m slide), turns six times. PatataWorks' layouts in a new shell. Fuse 5 s at the control room's arch. Built by
`IndustrialPlantBuilder` (industrial look: `IndustrialKit`, `IndustrialDetails`, `IndustrialDecals`, `IndustrialSigns`).

![The sorting line](images/industrial-plant/a01_sorting.png)

| Room | What is there | What it asks | CP |
|---|---|---|---|
| 00 Gatehouse | a loading dock with cargo crates; a ring (8 s) opens the dock door | warm-up; first ring | 1 |
| 01 Sorting line | opposing belts (±2 m/s), no-carry inspection strips, a two-route tube, a timed ring gate, a press | lead; throw across; pick the route | 2 |
| 02 Atrium (climbs 14 m) | stair ramps, a plate lift held by the receiver, a return ring and door | hold the way | 3 |
| 03 Void catwalks | a low-throw deck under a 8.5 m ceiling, three falling grates, a crane platform | low throw; commit; time the crane | 4 |
| 04 Cold storage | three cold pockets against three hot pipes along a lane wall; a laser window | cold vs hot; the window | 5 |
| 05 Chute | a 28 m mega slide with no-carry strips; the bomb takes a tube | slide while the bomb rides | 6 |
| 06 Furnace intake | a hot zone under a 10 m press | time the press on a fast fuse | — |
| 07 Furnace loop | a windmill window for the bomb, a tunnel press for the runners, a cold respite | split the bomb and the runners | — |
| 08 Boiler approach | a sweeper (18 m, 75°/s) between boilers | jump it | 7 |
| 09 Boiler shaft (climbs 34 m) | four rising platforms, a cannon to the upper catch court | ride up while the bomb flies | 8 |
| 10 Control room | the arch checkpoint, consoles, the finish | one clean pass | 9 |

### 6.5 PatataRanger (M17, handed over in M18.4c) — the organic ascent

An old forest ranger post and its network in the trees, climbed from the valley floor to a lookout in the crowns. Two players.
Six acts, one checkpoint each, every act a chain of trials under one contract. Organic timber (decks round trunks, stairs, rope
bridges) built with the level tools, the kit drawn in the ranger's forest looks (winch, lever, cage, drawbridge, signal ring,
chute, bell arch, sparks). Fuse 5 s from CP3, 4.5 s from CP5. Its builder was removed once its timber was frozen into elements:
the scene is its own source (`Assets/Scenes/PatataRanger.unity`, meshes in `Assets/Art/Generated/PatataRanger/`).

| Act | Name | What is there | Contract |
|---|---|---|---|
| A (CP1) | Le Camp | the ranger's camp in a clearing at dawn, a trail between boulders, the yard gate in the ridge under its tower; a 5 s hourglass winch in a booth | an empty-handed player cranks the winch; its sand holds the gate while the cranker follows; the bomb passed over or carried through. The ridge either side is a climb lock |
| B (CP2) | Le Ravin | a ravine with a stream: a south stair to a ledge, an old footbridge low over the stream, a cage on the far ledge hoisted by a 4 s hourglass winch on the south rim; a 6 s far winch lowers a drawbridge | one goes low (stair, footbridge, cage the other hoists), then the far winch lowers the drawbridge for the one left on the rim; the bomb crosses by passes the whole way |
| C (CP3) | Le Pied du Vieux Chêne | a stair round a boulder to the old oak's lower terrace; two pulley cages (a hands-free counterweight below, a plain winch above); three signal hoops (11 s), three bridges, only one leads on; a clue board shows its glyph to the catcher | two cages, one each way, the bomb thrown down; then the catcher reads the glyph, the thrower sends the bomb through that hoop. A wrong hoop leads to a dead end and costs time |
| D (CP4) | Les Passerelles | two walks from the owl landing to the leaning pine: a low one round the crag (a swinging log) and a high one over it; a hands-free plate on the low walk raises the high gate, a 6 s hourglass on the high walk raises the low gate; rotten planks above, a stair below | one walker each way, the bomb passed between them |
| E (CP5) | Le Poste de Treuil | a two-storey roofed winch house round the leaning pine; a porch hoop opens the stairwell door for 6 s; a curtain of sparks on the stair; a cold larder with three log chutes, only one comes out upstairs; an upper winch lifts the carrier's cage | the runner climbs behind the sparks, sees which chute comes out and calls it; the house is roofed, nothing is lobbed in |
| F (CP6, finish) | La Canopée | a long rope bridge into the crowns under a swinging log, rotten planks to the twin fir, its 6 s hourglass winch bridge, the old signal tower; a race hoop lowers the lookout's drawbridge for 13 s; the bell arch | the race, then the bomb through the bell arch and the whole team aboard |

### 6.6 OldOak — the authoring guide's example level (M18.3b)

"The Old Oak", made with the level tools only: three checkpoints, about three minutes for two players
(`docs/LEVEL_AUTHORING.md` §16 described it).

| Section | What it forces | Built with |
|---|---|---|
| Start → CP1 | the forest path, the oak's stair, the oak walk to the winch landing | ground walkway, giant tree, ring deck, stair, walkway, forest |
| CP1 → CP2 | one holds the winch; the drawbridge climbs 3.5 m to the ridge; its sand lets the last one cross | hourglass winch wired to a drawbridge, a gap lock |
| CP2 → CP3 | sparks in the hut's far door burn the bomb: one walks through, the other lobs it over the roof | house with a ceiling, laser curtain, a climb lock |
| CP3 → finish | a rope bridge over a fire pit whose embers burn the fuse fast; the finish on the crow's nest | rope bridge, hot zone, a gap lock |

![The winch landing](images/old-oak/2-winch-landing.jpg)

### 6.7 PassSandbox — kept

One bomb, a `KitDemo` group with every kit prefab, a pass range with marks at 4, 8, 12 and 16 m (used by `PassFeelTests` and
the practice bot), and a `KitDemo/BombObstacles` corner (zones, curtains, a ring-driven lift). It stays on `main` as the test
map; since the fresh start it is shown in the menu only in the Editor and in development builds.

---

## 7. The removed courses (before PatataWilds)

In git history before the project cleanup (`OBSTACLES.md` §3.2 kept the same tables).

**`PrototypeCourse`** — the first course: a straight line along +Z, ~680 m, 7 checkpoints, 3 acts.

| Act | Beat | What it asked |
|---|---|---|
| 1 Training Grounds | A Safe Court | flat and wide: charge, throw, timed catch at 6 m |
| | B First Gap | 4.5 m pit, broad landing |
| | C Stair Relay | two 1.2 m steps, a 1.8 m step to jump and mantle; relay the bomb upward |
| | D Moving Pair | two slow platforms that line up now and then |
| | E Split Lanes | a centre wall with two openings, a blocker per lane: the bomb crosses between lanes |
| | F Vertical Catch | a launch pad throws the receiver 6 m up; catch near the apex, land on an 8.4 m ledge |
| | G Final Sprint | 7.6 m sprint-jump gap, narrow hops, a 1.3 m low bar (slide under), three falling platforms, a faster moving pair |
| 2 Patata Factory | H Conveyor Hall | three belts 1.5 m apart (sides forward at 3 m/s, centre back at 4 m/s), hurdles; throws must lead |
| | I Piston Alley | five piston tiles in a 3 m wave; a raised tile blocks the pass |
| | J Windmill Wall | an 11 m wall, the bomb through the windmill's hole, runners on elevators over the top |
| | K Sweeper Pit | two knee-high sweepers turning opposite ways; exit under a crusher (1.45 m) |
| 3 The Climb & The Drop | L Elevator Tower | 9.6 → 30.6 m: a launch pad or an elevator, an elevator with a sweeper on it, a mantle staircase; fuse 5 s from CP6 |
| | M Mega Slide | 18° downhill, two lanes split by a low divider, three spinning hoops; pass sideways at 15–18 m/s; fuse 4.5 s from CP7 |
| | N Factory Finale | run-out under a crusher, a belt through three piston gates, the podium |

**`PlaytestCourse`** (M10, #68) — the bomb obstacles taught alone, then combined, then twisted: ~820 m, 9 checkpoints.

| Act | Beats |
|---|---|
| 1 Warm-up | start court, first gap, stair relay with a 1.4 m mantle, a moving pair in opposite phase, a conveyor hall, a sweeper pit |
| 2 Hot & Cold | forbidden strips of 4 and 6 m; a laser window; a 32 m hot corridor with two sweepers and cold pockets on side ledges; a laser slalom of three walls whose last window is a spinning hoop |
| 3 Switchboard | a ring extends a bridge over 12 m for 8 s; **The Lock** (a plate holds a door whose doorway is a curtain, the bomb takes the window, the plate holder climbs over while the catcher waits in a cold pocket); a rhythmic shutter and a crusher tunnel; an arch checkpoint across 4 m; a switch chain (ring 1 raises a lift up a 5 m cliff for 12 s, ring 2 extends the next bridge for 8 s) |
| 4 Grand Finale | a tube intro (bomb through the tube, runners through a curtain); a **tube junction** (three lanes the bomb cannot cross: falling platforms, a sweeper, a reverse belt; one mouth per lane); **Cannon Canyon** (a 44 m shot onto the far pad while the team rides two shuttles); a mega slide with a hot lower half; a finale belt through piston gates, an arch, a ring opening the podium door. Fuse 5 s from CP8 |

**`PatataPark`** — the KayKit course on the metre grid: ~680 m, 9 checkpoints, 4 acts.

| Act | Beats |
|---|---|
| 1 Block Hop | three 6 m islands zig-zagging up 1 m each; an 18° ramp; four falling platforms zig-zagging over a 24 m pit; a five-tile piston wave; a sweeper, then an 8 m wall (bomb through the windmill, runners under a crusher) |
| 2 Hot & Cold | two bridges over a pit with staggered no-carry strips (the bomb crosses the 6 m gap twice); a laser slalom ending in a hoop; a hot climb up three 1 m steps with a cold pocket at the top and a sweeper |
| 3 Switchboard | a ring bridge over 12 m (8 s); The Lock; an arch across 4 m; a lift chain (ring 1 raises a lift up 6 m for 12 s, ring 2 extends a bridge for 8 s) |
| 4 Sky Finale | a three-lane tube junction; Cannon Canyon (44 m, two shuttles, a cold pocket); a sky slide with hoops and a hot lower half; a finale belt through two piston gates, an arch, a ring opening the podium door. Fuse 5 s from CP8 |

**`PatataWorks`** (M11, #83) — an enclosed potato factory at sunset; climbed 52 m, dropped 28 m; its layouts became the plant's.

| Section | Bomb question |
|---|---|
| Loading dock | crate warm-up; a ring opens the dock door |
| Atrium (climbs 14 m) | two ramp flights; **Hold the way** |
| Sorting hall | opposing belts behind interior windows; **Two routes, one bomb**; no-carry strips; a piston and a timed door |
| Silo catwalks | a full-power low pass under a ceiling, falling grates, a crane platform |
| Cold storage | **Buy time, spend time**: a cold lane round shelves against a straight hot lane; a laser window |
| Chute | a 28 m slide with no-carry strips; **Down the chute** |
| Basement furnaces | a hot press, a windmill window, a low crusher passage, a cold respite, a sweeper |
| Boiler shaft (climbs 34 m) | **Up the shaft**: four rising platforms, a cannon to the upper pad |
| Roof | chimneys, a final relay, the checkpoint arch, the finish |

**`IndustrialLab`** — a short look test: two checkpoints, a covered hall and a calibration wall (1, 4 and 12 m panels per
material) to judge texel density.

---

## 8. Lessons from these maps

- **The constraint, not the intent.** PatataWilds' puzzles had the right idea and no lock: a team skipped about 15 of 25. From
  PatataCanopy on, a section was finished only when it could be solved **only** the intended way (§5).
- **The void is the best lock.** Canopy and Temple put every walkable metre on purpose over the void; gaps of 14 m and faces of
  5 m did what walls could not. Free-standing walls never stop a lob: roof them.
- **Checkpoints too close kill the challenge** (owner, after the playtests): a 30-second section with one beat is too easy to
  retry. Prefer long sections that chain several trials under one contract (as the Ranger did, one checkpoint per act).
- **Each course its own shape.** A new course gets its own route and scenery, never Canopy's staircase over a forest again.
- **Read in ten seconds** (two players), twenty (three), then execution. No puzzle that needs a long discussion.
- **Every role reads in the scenery**: glyphs, colours, pips, lamps; a tube's mouth and exit share a colour **and** a pip count.
- **Holders never stand still more than about eight seconds** (hourglasses relieve them, roles turn).
- **Organic beats boxy** (the Ranger brief): no blocky shapes, no perfectly flat floors; generated timber and scanned props.
- **A wrong choice costs time, never the run** (decoys in the Ranger's observation puzzles).

---

## 9. Bringing a map back

Everything is on the branch `archive/maps-v1`:

```text
git fetch origin
git switch archive/maps-v1          # the whole project as it was, every map playable
```

To lift one map into a new branch of the fresh project, check out its files from the archive and follow what it needs:

| Map | Files on the archive branch |
|---|---|
| PatataWilds | `Assets/Scenes/PatataWilds.unity`, `Assets/EditorTools/PatataWildsBuilder*.cs`, `Assets/Tests/PlayMode/PatataWildsTests.cs`, its terrain and mist in `Assets/Art/Nature/Generated/` |
| PatataCanopy | `Assets/Scenes/PatataCanopy.unity`, `Assets/EditorTools/PatataCanopyBuilder*.cs`, `Assets/Tests/PlayMode/PatataCanopyTests.cs` |
| PatataTemple | `Assets/Scenes/PatataTemple.unity`, `Assets/EditorTools/PatataTempleBuilder*.cs`, `Assets/Tests/PlayMode/PatataTempleTests.cs`, `Assets/Scripts/Nature/TempleStorm.cs` |
| IndustrialPlant | `Assets/Scenes/IndustrialPlant.unity`, `Assets/EditorTools/IndustrialPlantBuilder.cs`, `Assets/Tests/PlayMode/IndustrialPlantTests.cs` |
| PatataRanger | `Assets/Scenes/PatataRanger.unity`, `Assets/Art/Generated/PatataRanger/`, `Assets/Tests/PlayMode/PatataRangerTests.cs` |
| OldOak | `Assets/Scenes/Levels/OldOak.unity`, `Assets/Art/Generated/OldOak/` |

```text
git checkout archive/maps-v1 -- Assets/Scenes/PatataCanopy.unity Assets/Scenes/PatataCanopy.unity.meta
```

Then register it (`HotPatata/Level/Register In Menu`, or the builder's own registration) and run Verify Level. A builder may
call helpers that changed since; the archive branch itself always compiles and plays.
