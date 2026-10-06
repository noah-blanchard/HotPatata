# HotPatata — PROJECT_SPEC.md

> **Status:** MVP / rapid prototype; progress per milestone is tracked in [`MVP_TASKS.md`](MVP_TASKS.md)  
> **Title:** HotPatata  
> **Genre:** Cooperative first-person parkour / hot-potato party game  
> **Initial target:** Windows PC (primary). Each release also ships Linux and macOS builds, best effort (macOS unsigned).  
> **Target engine:** Unity 6.3 LTS  
> **Initial player count:** 2–4 players  
> **Primary development goal:** Prove that passing the bomb while moving through a parkour course is fun before building content or polish.

---

## 1. One-line pitch

**2–4 players must finish a platforming course while continuously throwing a live potato-bomb between them. Hold it too long or let it touch the environment and it explodes, resetting the current section for everyone.**

---

## 2. Core player fantasy

The intended feeling is:

> “We are barely keeping this thing alive while trying to get everyone to the finish.”

The emotional rhythm should repeatedly move through:

1. confidence,
2. rising pressure,
3. panic,
4. successful catch / save,
5. relief,
6. laughter after failure,
7. immediate retry.

The bomb is not an accessory to the parkour. It is the central mechanic around which the level is built.

---

## 3. Design pillars

### 3.1 Readable chaos

The game may become visually chaotic, but players must understand why a failure happened.

Consequences:

- bomb state must be obvious;
- catches need strong confirmation;
- collision behavior must be deterministic;
- level geometry must be visually clean around intended pass paths;
- the explosion reason should never feel mysterious.

### 3.2 Pass under pressure

The central decision is:

- when to pass;
- where to pass;
- to whom to pass;
- whether to move before passing;
- whether the receiver should move first.

Levels must create safe and risky passing windows.

### 3.3 Cooperative movement

Players progress by positioning for each other.

The game should repeatedly create temporary roles:

- carrier;
- receiver;
- next receiver;
- player advancing to prepare the next catch.

### 3.4 Fast recovery

Failure should be funny and immediately replayable.

Normal mode uses section checkpoints rather than whole-level restarts.

---

## 4. Non-negotiable game rules

These rules are the source of truth for the MVP.

1. A playable section contains exactly **one active bomb**.
2. The bomb can be held by only **one player** at a time.
3. The carrier may move, jump, aim and throw while carrying it.
4. A successful catch immediately transfers the bomb to the receiver.
5. A valid pass is:

   **player hand → free flight → another player's catch**

   A level transit (tube or cannon, §5 `InTransit`, §13.16) may carry the bomb between two free flights:
   **hand → flight → transit → flight → catch** is still one valid pass.

6. While airborne, if the bomb touches **any invalid environment collider**, it explodes immediately.
7. Invalid environment includes by default:
   - floor;
   - wall;
   - platform;
   - obstacle;
   - moving obstacle;
   - hazard;
   - kill volume.
8. No wall bounce is allowed in the MVP.
9. A bomb held beyond the allowed hold window explodes.
10. An explosion fails the **current section** for the whole team.
11. On section reset:
    - players return to checkpoint spawn positions;
    - bomb returns to its normalized checkpoint state;
    - velocities are cleared;
    - the hold fuse is restored;
    - the section becomes playable again quickly.
12. The level is complete only when **all required players** reach the finish condition.
13. The game must remain understandable without voice chat.
14. Voice chat may improve cooperation, but no mechanic may require it.

---

## 5. Bomb state model

The implementation must use explicit bomb states.

### 5.1 Required states

#### `Held`

Entered when:

- the bomb spawns on a player;
- a player successfully catches the bomb;
- a checkpoint reset gives the bomb to a player.

Behavior:

- bomb is attached to the carrier's hand anchor, which sits in front of the camera (lower right), so the carrier sees the bomb in their hand (no hand models in the MVP);
- hold fuse is active;
- world collision failure is disabled while correctly attached;
- carrier can initiate a throw.

Failure:

- hold fuse reaches zero.

#### `Thrown`

Entered when:

- carrier releases the bomb.

Behavior:

- bomb has world-space position and velocity;
- bomb is catchable;
- bomb may use Rigidbody/physics;
- environment contact is lethal.

Failure:

- bomb touches any collider not explicitly recognized as a valid catch interaction or intentionally safe volume.

#### `CaughtGrace`

Entered immediately after a valid catch.

Purpose:

- prevent edge-case immediate re-failure caused by overlapping colliders;
- normalize velocity and attachment.

Starting value:

- **0.35 s**

After grace:

- transition to `Held`.

#### `Exploding`

Entered when:

- hold fuse expires;
- illegal world contact occurs;
- bomb enters a lethal kill volume;
- an authoritative game rule explicitly causes failure.

Behavior:

- prevent further throw/catch interactions;
- play explosion feedback;
- tell the run system to reset the section.

#### `Resetting`

Entered while checkpoint reset is in progress.

Behavior:

- no gameplay interaction;
- clear bomb velocity;
- restore bomb position and holder;
- restore fuse;
- transition to `Held` when section becomes active.

#### `InTransit` (added by #68, tubes and cannons)

Entered when:

- a `Thrown` bomb passes through a transit mouth (§13.16).

Behavior:

- the level carries the bomb: no carrier, **no fuse** (nothing burns), no collision, not catchable;
- a tube hides the bomb; a cannon shows it sitting in its basket;
- after the transit's delay, the bomb leaves the exit linked to the mouth it entered on a fixed, readable arc
  and is `Thrown` again, with the normal catch and collision rules;
- nobody counts as its thrower (anyone may catch the exit arc) and the aim assist does not apply.

Failure:

- none while inside; a section reset clears it like any other state.

---

## 6. Bomb collision contract

This rule is critical.

### 6.1 MVP rule

While the bomb is in `Thrown`:

- collision with a valid player catch volume may produce a catch;
- collision with anything else that counts as environment produces an explosion.

### 6.2 Do not implement in MVP

Do **not** add:

- floor bounce;
- wall ricochet;
- rolling;
- bomb resting on geometry;
- bomb pickup from the floor;
- physically ambiguous “almost caught” states.

If a thrown bomb touches the world, the pass failed.

### 6.3 Safe exceptions

No surface should be bomb-safe in the first sandbox unless required for technical reasons.

If later added, safe surfaces must use an explicit marker/component/layer. Do not infer safety from object names.

The only safe volume so far is a **transit mouth** (§13.16): an explicit `TransitMouth` marker volume, detected by
the host's sweep of the flight, that captures the bomb into `InTransit` instead of exploding it.

---

## 7. Hold fuse

### 7.1 MVP behavior

Use a **per-carrier hold fuse**.

A successful catch refreshes the holder's fuse.

Starting value:

- **6.0 seconds**

Warning phase:

- final **2.0 seconds**

The fuse is not a level-wide timer.

### 7.2 Feedback

Suggested progression:

- 0–50% consumed: calm, spaced beeps;
- 50–75%: faster cadence;
- 75–90%: urgent cadence + visible pulse;
- 90–100%: rapid beeps + strong bomb pulse.

A numerical timer is **not required** for the MVP.

The visual pulse must communicate the same urgency as audio.

### 7.3 Fuse zones (added by #68)

A **fuse zone** is a marked volume that changes how fast the carrier's fuse burns while the carrier is inside it.
Only the carrier is affected; other players cross freely.

| Zone | Fuse rate | Meaning |
|---|---:|---|
| Forbidden | ∞ | the bomb explodes as soon as its carrier is inside |
| Hot | × 2 | pass or get out fast |
| Cold | × 0.5 | a breather, not a shelter: staying still still ends in an explosion |

Rules:

- when zones overlap, **the most severe wins** (forbidden > hot > cold > normal), so nothing can cancel a forbidden
  zone;
- a catch made inside a forbidden zone explodes immediately (the zone decides, the catch rules are unchanged);
- a **laser curtain** is a forbidden zone that is also lethal to the bomb in flight (a thrown bomb crossing it
  explodes, like hitting a wall), while players walk through it. Curtains are how a window becomes mandatory
  (§13.13);
- the zone rate is shown on the bomb (sparks speed up in a hot zone, frost in a cold one) and in the beep; every
  zone reads by pattern and icon, never by colour alone (§19).

---

## 8. Throw system

The throw system is one of the highest-priority pieces of game feel.

### 8.1 Required behavior

The player must be able to:

- aim;
- throw while standing;
- throw while moving;
- throw while jumping.

Starting target:

- comfortable normal pass distance: **8–12 m**

### 8.2 Charged throw (decided after the first playtest, reworked after the pass-feel review)

The throw is **chargeable and immediate**:

- **hold** the throw button to charge, **release** to throw; a quick tap is the shortest pass;
- the bomb leaves the hand **on the release frame**. The throw animation is a follow-through played after the
  launch, never a delay before it;
- charge runs from 0 to 1 over `throwChargeTime` (0.45 s) and is capped at 1;
- launch speed goes linearly from `throwSpeedMin` (tap) to `throwSpeedMax` (full charge): 14 to 28 m/s. Aimed at
  the receiver's chest, a tap is a short pass (about 4 m) and falls short at 8 m; a half charge covers about 8 m and a
  full charge 12 m or more. Aiming a little higher carries further. A normal pass is in the air for about **0.25–0.5 s**;
- pure ballistic flight from the hand/release anchor under the bomb's own gravity (`bombGravityScale` 1.5),
  pitched up slightly (`throwUpAngle` 6°). Nothing bends the flight after release;
- the throw keeps a share of the thrower's run speed **along the aim direction only** (`throwInheritForward` 0.5):
  running forward throws harder, strafing or jumping never pushes the throw off your aim;
- the throw goes where the camera shows: the aim is the rendered view direction, view kicks included;
- while charging, the carrier's fuse keeps burning: charging is a risk, not a pause;
- charging is cancelled if control is locked, the bomb is lost or the section resets.

### 8.3 Release-time aim assist (replaces soft homing)

Soft homing (the flight bending toward a locked receiver, plus an in-flight magnet) was **removed**: it delivered
weak, badly aimed throws to distant players and made passing feel automatic. The assist now acts **once, at
release, on direction only**:

- **Who:** another player in line of sight (no Environment/Hazard in between), within `assistMaxRange` (14 m),
  whose body is within `assistConeDegrees` (6°) of the raw aim (the body counts as a 0.4 m disc, so a close
  receiver is not harder to hit than a far one; aiming where a moving receiver is going counts too). The smallest
  angle wins.
- **What:** the launch direction is turned toward the low-arc solution that reaches that receiver **at the throw's own
  speed**, aimed a little ahead of a moving receiver (`assistLeadFactor` 0.8). Strength fades toward the edge of the
  cone and with distance (`assistStrength` 0.6).
- **Caps:** at most `assistMaxCorrectionDegrees` (3.5°) in total, of which at most `assistMaxElevationDegrees`
  (1°) upward, so the assist can add only a little range (well under a metre for a tap).
- **Never creates power:** if the throw is too weak to reach the receiver, only its heading is corrected and it falls
  short. The assist never changes speed and never touches the bomb in flight.
- **Feedback:** the thrower sees corner brackets on the receiver the assist would help (brighter = stronger); the
  brackets turn red when the current charge is too weak to reach them. The receiver sees a pulsing "CATCH!" marker
  on a bomb heading for them (the assist target, or the player the arc passes close to).

A **UI indicator** is required: a charge bar near the crosshair (only while holding the bomb) that fills
as the charge builds, with the launch speed and an estimated level-ground range. No trajectory preview.

---

## 9. Catch system

Catching is a **timed action, never automatic** (decided after the first playtest). It should reward
positioning and timing while staying fair.

### 9.1 Required behavior

The receiver presses **catch** (right click / left trigger). That opens a short **catch window**
(`catchWindowDuration`, starting value **0.25 s**, a tuning parameter). A catch is valid when:

- bomb is in `Thrown`;
- receiver is eligible (not the player who just threw it, and not the current carrier);
- the receiver's catch window is **open**;
- bomb intersects the receiver's catch volume (either entering it during the window, or already inside it
  when the window opens);
- authoritative resolver accepts the catch.

A bomb that reaches a receiver who did not press catch in time is **not** caught: it flies through and the
normal world-contact rule applies. Pressing too early (the window closes before the bomb arrives) misses too.

After a window closes there is a **cooldown** (`catchCooldown`, starting value 0.35 s) before catch can be
pressed again, so the button cannot be mashed. A successful catch spends the window and clears the cooldown.
A carrier cannot open a catch window.

The catch state is shown to the local player by a UI indicator around the crosshair: faint brackets = ready,
large green brackets = window open, small red brackets with a shrinking bar = cooldown.

Catch reach (forgiveness belongs to the receiver, not to the flight):

- `catchRadius` **1.0 m** around upper torso / hands (it was 0.6 m, then 0.9 m);
- plus `catchFacingBonus` **0.3 m** when the bomb arrives from within `catchFacingAngle` (70°) of where the receiver
  looks: facing the pass makes it easier;
- the reach is shorter vertically (`catchVerticalScale` 0.6): arms reach out to the sides more than down to the
  feet, so a pass arriving beside the shoulder is caught while one arriving at the knees is not;
- the test is **swept**: the bomb's path between two physics steps is checked against the reach, so a fast bomb
  never slips through;
- a press up to `catchLateGrace` (0.06 s) after the bomb was in reach still catches, as long as the bomb has not hit
  anything yet;
- a caught bomb snaps visually into the hands (presentation only).

The catch window is **0.4 s** (it was 0.25 s), the cooldown **0.35 s**. When a catch fails, the receiver is told why ("Too early" / "Too late by N ms")
and the debug HUD shows their ping, so timing problems can be told apart from network latency.

Prefer a front-biased catch region.

### 9.2 Catch assistance

Assistance may widen tolerance but must **never** replace the catch input.

Allowed:

- slightly oversized catch volume (1.0 m, swept);
- small angular correction at release (§8.3);
- a few tens of milliseconds of grace on a late press;
- clearer assistance while receiver is facing the bomb.

Not used any more: in-flight homing and trajectory magnetism.

Not allowed:

- catching from obviously impossible distances;
- teleporting the bomb across large gaps;
- hidden auto-catches that make bad throws look correct.

### 9.3 Catch confirmation

A successful catch must provide immediate confirmation through at least:

- distinct catch sound;
- bomb attachment snap;
- holder indicator change;
- optional small VFX pulse.

Players must never wonder whether the catch registered.

---

## 10. Player controls

Initial controls:

| Action | Keyboard / Mouse | Controller | MVP requirement |
|---|---|---|---|
| Move | WASD | Left stick | Required |
| Look | Mouse | Right stick | Required |
| Jump | Space | South button | Required |
| Sprint (hold) | Left Shift | Left stick press | Required (M9) |
| Crouch / slide (hold) | Left Ctrl or C | East button | Required (M9) |
| Throw | Left click (hold to charge, release to throw) | Right trigger | Required |
| Catch | Right click (timed, see §9) | Left trigger | Required |
| Ping / “throw to me” | Q | Bumper | Later MVP if needed |

Catch is required to receive the bomb; there is no automatic catch.

---

## 11. Player movement

Movement target:

**responsive, forgiving, predictable first-person platforming that flows** (Apex-lite: momentum carries, nothing snaps).

Required characteristics:

- first-person camera at eye height; the body always faces where the player looks, and the player's own body is hidden from their own view (other players still see it);
- quick but **progressive** acceleration (a gentle start that builds to full speed in about a quarter of a second, so a run has momentum) and fast, slightly inertial braking;
- a strong sense of speed in first person: the field of view widens with speed and narrows while charging a throw, the view leans into strafes, bobs with footsteps, dips on hard landings and punches on throws and catches; above run speed, anime speed lines and wind streaks, and a light vignette in slides (all tuned in `GameTuning`, starting run speed 8 m/s); no movement sounds (no footsteps, landings or wind);
- the run direction **carves** toward the stick at a speed-dependent turn rate instead of snapping; sharp reversals brake through zero;
- **sprint** (hold, forward-ish only) raises the run speed from 8 to 11 m/s; speed above the target (letting go of sprint, after a slide) bleeds off gently instead of snapping;
- **momentum slide**: crouch while moving at 7 m/s or more slides, with a boost (+3.5 m/s, at most up to sprint speed + boost, no new boost within 1 s), friction that ends it in about a second, faster downhill, a little steering; release crouch to stand. A landing with crouch held goes straight into a slide;
- **slide-jump** keeps the slide's full horizontal speed; **landings keep momentum**;
- **crouch-walk** (4 m/s) when crouching slowly or when a ceiling is too low to stand; the capsule, eye, hand and catch sphere lower with the body (replicated to every machine);
- **mantle**: moving forward into a ledge 0.1–1.4 m above the feet while airborne climbs it automatically (never onto hazards); a jump that will clear the ledge anyway is not interrupted;
- moderate air control; a jump keeps its momentum, with extra drag above sprint speed so chained jumps cannot keep slide speed (no bunny-hop, no air-strafe gain);
- single jump;
- coyote time around **0.1 s**;
- jump buffer around **0.1 s**;
- no stamina;
- no wall-run, dash or double jump (sprint, slide and mantle are movement, not the "abilities" excluded in §17.2);
- no combat;
- no grabbing;
- no deliberate ragdoll locomotion in MVP.

The bomb already creates chaos. Movement should not be intentionally frustrating.

---

## 12. Failure and checkpoint behavior

### 12.1 Default failure

Any bomb explosion resets the current section for the full team.

### 12.2 Reset target

Starting reset delay:

- about **1.0 s**

Goal:

- blast is readable;
- players understand failure;
- next attempt starts almost immediately.

### 12.3 Checkpoint activation

A checkpoint activates only when all required players satisfy the checkpoint condition.

On checkpoint activation:

- record checkpoint as current;
- store player respawn positions;
- normalize bomb state;
- clear temporary section state.

**Bomb-claimed checkpoint (arch, added by #68).** A checkpoint may have an arch: it then activates only when
all required players are inside **and** the bomb has passed through the arch **in flight** during the current
section attempt. The claim is cleared by a section reset. Checkpoints without an arch keep the rule above.

Recommended normal spacing:

- **30–60 seconds** of gameplay between checkpoints.

---

## 13. Level-design grammar

Every meaningful obstacle should ask a bomb-specific question.

If an obstacle plays almost identically without the bomb, it is probably filler.

### 13.1 Gap pass

Question:

> Can we safely pass across empty space?

Example:

- receiver crosses first;
- turns;
- receives bomb from far platform.

Difficulty levers:

- gap width;
- landing width;
- vertical offset.

### 13.2 Moving-platform pass

Question:

> Can thrower and receiver synchronize movement?

Difficulty levers:

- platform speed;
- phase difference;
- alignment window.

### 13.3 Vertical relay

Question:

> Can the team move the bomb vertically while climbing?

Use:

- ledges at different heights;
- alternating carrier/receiver roles.

### 13.4 Split route

Question:

> Can separated players maintain the pass chain?

Use:

- parallel lanes;
- openings through walls;
- alternating line of sight.

### 13.5 Low tunnel

Question:

> Can the team select an appropriate low throw arc?

### 13.6 Rotating blocker

Question:

> Can players time the throw through a moving safe window?

### 13.7 Falling platform

Question:

> Can the group commit rather than waiting forever?

### 13.8 Catch under movement pressure

Question:

> Can the receiver catch while jumping or landing?

### 13.9 Conveyor lanes

Question:

> Can the thrower lead a receiver who is being carried (or held back)?

Primary levers: belt speed and direction per lane, lane spacing, hurdles that force lane changes.

### 13.10 Piston floor / piston gates

Question:

> Can the team pass through the moment the line opens, onto a target that moves vertically?

Primary levers: travel, dwell share, phase wave between pistons.

### 13.11 Lethal sweepers, windmills, crushers

Question:

> Can the team keep the pass chain going while dodging something that is lethal to players too?

Rules: lethal parts are on the `Hazard` layer, striped (never red alone), and a crusher never closes below the
crouch height, so sliding or crouching under it is always possible. The bomb question stays central: the windmill
guards the only bomb line through a wall, the crusher is a low-tunnel throw (13.5) when it is down.

### 13.12 Mega slide with pass gates

Question:

> Can two sliders at full speed pass sideways, leading the throw, through spinning hoop windows?

Primary levers: slope, lane divider height, hoop speed and spacing, stretches where a tall divider blocks passes.

### 13.13 Forbidden strip and laser window (#68)

Question:

> Can the team get the bomb across a line its carrier is not allowed to cross?

A forbidden strip on the floor is passed over (throw across, catch on the far side). A wall with a window whose
side passages are closed by laser curtains (§7.3) makes the window mandatory: runners walk through the curtains,
the bomb must be thrown through the hole. Variants: a slalom of windows, a spinning hoop as the window.

Primary levers: strip width, window size and height, which side the runners are on, hoop speed.

### 13.14 Hot and cold zones (#68)

Question:

> Can the team keep the pass rhythm when the fuse burns twice as fast, and use a breather wisely?

Hot zones (× 2) sit on the pass line of a corridor or a mover; cold zones (× 0.5) are small pockets beside the
running line, never a whole beat: a place to breathe, or for a lone carrier to wait while a teammate is still on
their way (before a tube, a cannon or a lock), so they help without becoming a shelter.

Primary levers: zone length, what else moves in the zone, where the cold pockets are.

### 13.15 Bomb switches and pressure plates (#68)

Question:

> Can the pass open the way for the runners, or can a runner hold the way open for the pass?

A **bomb gate** is a ring the bomb must fly through; a **pressure plate** is held by any player standing on it,
carrier included (a **hands-free** plate does not count its carrier, §13.19). Each drives exactly **one** actuator (a door, a bridge, a lift): the gate for a fixed time after
the pass, the plate while it is held. No AND/OR logic (§17.2).

A door is a portcullis that closes completely (a door that left crouch room would not block anything). It is a
lethal hazard: striped, on the `Hazard` layer, with a lethal lower edge while it moves.

Primary levers: gate hold time, actuator travel time, distance between the plate and what it opens.

### 13.16 Tubes and cannons (#68)

Question:

> Can the thrower send the bomb into the right mouth, and is the receiver already at the exit?

A thrown bomb entering a transit mouth goes `InTransit` (§5), then leaves the linked exit after the transit's
delay on a fixed arc that lands on a painted receiver pad. A **tube** hides the bomb for its delay; a **cannon**
shows it in its basket and fires after a short delay, far and high. Several mouths may lead to several exits: the
thrower chooses by aiming (each mouth and its exit share a colour **and** a symbol). No randomness. The exit lights
up and a rising tone plays just before the bomb comes out.

Primary levers: delay, exit arc and flight time, how far the receiver must travel to reach the pad.

### 13.17 Checkpoint arch (#68)

Question:

> Can the team end the section with one clean pass through the arch?

The arch stands at the entrance of the checkpoint (§12.3); the usual layout is a throw through it across a short
gap to a teammate already on the pad.

Primary levers: arch size, gap, whether the arch is framed by curtains.

### 13.18 Body screen: brambles, nets (M13)

Question:

> Can the bomb go where the runners cannot?

The mirror image of a laser curtain. A **body screen** stops every player's body (carrier or not) and lets the bomb, the
aim and the catch through: it is solid on its own `BodyScreen` layer, which collides with `Player` only. Nobody mantles
onto it. In a wall, a screen makes an opening **bomb only**, a laser curtain makes one **runners only**, an empty hole
makes one for both: an opening is always one of the three, never ambiguous (a 2.4 × 2 m window is not a bomb-only
opening: a player climbs through it).

Primary levers: where the screens are against the runners' route, their height (a slider fits under a hedge raised 1.3 m),
what the screen frames.

### 13.19 Hands-free plates and switches (M13)

Question:

> Who holds the way open, and who has the bomb?

A **hands-free plate** is a pressure plate that does not count its carrier: the holder must have passed the bomb first,
and catching on the plate lets it go. A **switch** is an actuator that turns things on or off instead of moving them: a
laser curtain cut while a plate is held, a bramble hedge parted for a few seconds after a ring, spores cleared from a
bridge. Like every actuator it has one source (§13.15), travels, replicates, and goes back to rest on a section reset.

Primary levers: which side of the obstacle the plate is on, whether the catch on the plate undoes it, the switch's hold time.

### 13.20 Section contracts (M13)

Every section of a course built after M13 states its **contract**: what it **forces** (the question it asks) and the
**shortcuts it locks**, each with the geometry that locks it. A mechanic that the team can skip is filler (§13), however
it is drawn. The reach a shortcut is measured against comes from the tuning (§20):

- a **gap** is only locked beyond a slide-jump with a mantle (12.75 m on the flat, more downhill): 14 m is the rule;
- a **face** is only locked above a jump and a mantle (3.0 m): 3.4 m or more;
- a **wall** only stops the bomb if it reaches a roof: a lob climbs 38 m, so no free-standing height is enough;
- an obstacle meant to gate the way spans the whole walkable width, and a wall the void beside it;
- the void is the default: every walkable metre is deliberate.

`SectionContract` holds the contract in the scene; `CourseContractCheck` measures it (warnings, never a build gate).

---

## 14. Early-level constraints

Avoid in the first level:

- blind throws;
- precision catches onto tiny beams;
- long waiting sequences;
- one player doing everything while others watch;
- unpredictable physics debris;
- multiple simultaneous bombs;
- puzzle sections requiring long discussions;
- extremely long jumps.

---

## 15. First level blueprint

> **Removed course.** `PrototypeCourse` was removed in the project cleanup; this blueprint stays as the design record and
> its built layout is archived in [`OBSTACLES.md`](OBSTACLES.md) §3.2. The kept courses are PatataWilds (§15c), PatataCanopy
> (§15d) and the industrial plant.

Working level:

**Training Facility**

Target length:

- approximately **3–5 minutes** for the first complete greybox version (Act 1, beats A–G);
- extended to approximately **8–10 minutes** with Act 2 (Patata Factory) and Act 3 (The Climb & The Drop) after
  the owner's playtests of Act 1 (see [`ARCHITECTURE.md`](ARCHITECTURE.md) §4 for the beat layout). Still one course (§17.2).

### Beat A — Safe Court

- flat safe zone;
- approximately 6 m passes;
- teaches throw/catch;
- no dangerous parkour.

### Beat B — First Gap

- two wide platforms;
- short pit;
- receiver crosses first;
- carrier passes across.

### Beat C — Stair Relay

- three height levels;
- players relay the bomb upward;
- large recovery spaces.

### Beat D — Moving Pair

- two slow moving platforms;
- periodically align;
- players choose safe or risky timing.

### Beat E — Split Lanes

- two parallel routes;
- low separating wall;
- openings for cross-lane passes.

### Beat F — Vertical Catch

- receiver launches/jumps;
- bomb is caught near jump apex;
- broad landing area.

### Beat G — Final Sprint

- three short parkour beats;
- faster required handoffs;
- optionally shorter fuse around **4.5 s** for this section only after the base version works (as built, the
  shorter fuse lives in Act 3 instead: `Checkpoint.holdFuseOverride` gives 5 s from checkpoint 6 and 4.5 s from
  checkpoint 7).

### Finish

Finish only when all required players enter the end condition.

Display at minimum:

- completion;
- run time;
- optional explosion/reset count.

---

## 15b. Second level blueprint — PatataWorks (#83)

> **Removed course.** `PatataWorks` was removed in the project cleanup; its layouts became the industrial plant's and its
> sections are archived in [`OBSTACLES.md`](OBSTACLES.md) §3.2. The enclosed-course rules below still apply to the plant.

Added after the three open-sky courses (`PrototypeCourse`, `PlaytestCourse`, `PatataPark`), which are all one straight
lane of floating platforms along +Z. The second full course is a **place**: an old potato factory at sunset, a closed
building whose route climbs, turns right, turns left and drops. It was first in the Bootstrap level list until PatataWilds
(§15c).

Working level:

**PatataWorks**

Target:

- **10–14 minutes** for a first clear by a team that knows the controls; **9 checkpoints**, 30–60 s apart (§20);
- **2 players can solve every puzzle**; 3–4 players make it easier or faster, never possible-only-with-4;
- the same rules as every course: one bomb, the plain ballistic arc, no new mechanic. Only the existing kit and the
  #68 elements (§13.13–§13.17) are used.

### Enclosed-course rules

- **Closed building.** Every section except the roof finale has walls and a ceiling. Nothing floats in an open sky,
  except inside the boiler shaft, the atrium void and on the roof.
- **Walls and ceilings are environment** (§6): a thrown bomb touching them explodes, like any other geometry.
- **Ceiling clearance.** Every room where passes happen has a ceiling higher than the intended arcs plus a margin
  (§20). Low ceilings exist only where a low throw is the question (§13.5).
- **Pass corridors.** Each intended pass is declared as a volume; decoration never enters one (this replaces the
  open courses' |x| ≥ 25 m rule, §3.1).
- **Openings** that passes go through (windows, doorways) are larger than the catch radius with a margin.
- **Pits** inside the building have a kill zone below them.
- **Telegraph first:** coloured lights, signage arrows, the exit pad lighting before a tube fires. No blind throw
  without a window or a telegraph, no puzzle that needs a long discussion (§14).
- **Puzzles chain elements through the geometry, never through logic:** one source drives exactly one actuator
  (§13.15, §17.2).

### Route

Direction is the point: the course climbs, turns both ways and drops, and starts and ends at different heights.

| # | Section | Direction | Height | Bomb questions |
|---|---|---|---|---|
| 0 | Loading Dock (start) | ahead | 0 m | warm-up passes between crates (§13.1); the first door opened by a bomb gate (§13.15) |
| 1 | The Atrium | up | 0 → 14 m | vertical relay up a three-storey void (§13.3); a plate lift held by one runner while the others climb (§13.15) |
| 2 | East Wing: Sorting Hall | right | 14 m | conveyor lanes behind interior windows (§13.9, §13.4); timed doors opened by bomb gates; pistons (§13.10) |
| 3 | Silo Catwalks | ahead | 14 → 18 m | catwalks under a low truss ceiling: low throws (§13.5), falling grates (§13.7), crane platforms (§13.2) |
| 4 | West Wing: Cold Storage | left, back across | 18 m | cold pockets and a hot shortcut (§13.14); laser windows in a long corridor (§13.13) |
| 5 | The Chute | down | 18 → −10 m | mega slide with pass gates (§13.12); the bomb takes its own tube to a pad at the bottom (§13.16) |
| 6 | Basement Furnaces | right, then a loop | −10 m | hot zones (§13.14), crushers and windmills (§13.11), low tunnels (§13.5), a split route around the furnace (§13.4) |
| 7 | The Boiler Shaft | up | −10 → 24 m | rising platforms and a cannon firing the bomb up the shaft to a pad (§13.16); catch under movement pressure (§13.8) |
| 8 | The Roof | out into the sky | 24 m | the one open section: chimneys, a last relay, the checkpoint arch (§13.17), the finish under the PatataWorks sign |

Each section ends with a checkpoint (9 in all). The shorter hold fuse comes near the end, as in `PrototypeCourse`
Act 3 (`Checkpoint.holdFuseOverride`, §20).

### Signature puzzles

Each chains two elements through the geometry. At least these three are built:

- **Hold the way** (the Atrium): a runner holds a plate one floor up to keep a lift in place while the carrier
  passes across the void to the third floor. A bomb gate passed on the far side opens the door that brings the
  plate holder back. With 2 players, the plate holder is also the receiver: the plate sits on the landing the pass
  lands on.
- **Two routes, one bomb** (the Sorting Hall): the team splits around a wall; the bomb goes through the wall by a
  tube. Two mouths lead to two rooms (colour **and** symbol), and the thrower picks the room where the receiver is.
- **Buy time, spend time** (Cold Storage): the long corridor is only doable by carrying through the cold pockets;
  the shortcut through the boiler pipes is shorter but hot. The team chooses.
- **Timed doors** (the Sorting Hall): a bomb gate opens a door for 6–10 s (§20); the receiver must already be running
  when the pass goes through the ring.
- **Down the chute** (the Chute): runners take the slide while the bomb takes the chute tube, tuned so a receiver who
  did not hesitate reaches the pad in time.
- **Up the shaft** (the Boiler Shaft): the cannon fires the bomb up the shaft; the receiver rides the rising platforms
  to be on the pad when it arrives.

### Intended difficulty

Sections 0–1 teach the building (wide rooms, high ceilings, a single element each). Sections 2–4 combine two elements
per room. Sections 5–7 are the hardest (timed transits, hot zones, catch under movement). The roof is a short,
readable victory lap.

---

## 15c. Third level blueprint — PatataWilds (M12)

Added at the owner's request after `PatataWorks`: a **realistic outdoor course**, a climb through a forest, along a river
and up granite cliffs to a summit, from dawn to dusk. It is first in the Bootstrap level list. Built by
`PatataWildsBuilder` (ARCHITECTURE §4); its look is ARCHITECTURE §25.3.

Working level:

**PatataWilds**

Target:

- **20+ minutes** for a first clear by a team that knows the controls; **5 acts, 25 sections, 25 checkpoints**, one per
  section, 30–60 s apart (§12.3, §20);
- **2 players can solve every puzzle**; 3–4 players make it easier or faster;
- the same rules as every course: one bomb, the plain ballistic arc, no new mechanic. Only the existing kit and the #68
  elements (§13.13–§13.17) are used, **re-dressed as nature**: a mover is a log raft, a falling platform rotten planks, a
  conveyor a log drive, a crusher a log ram or stamp, a sweeper a swinging log, a windmill a water wheel, a hoop woven
  willow, a tube a hollow log, a cannon a stump catapult, a ring gate a vine ring, a door a palisade, a laser curtain's
  posts cairns. Logic, sizes and colliders are unchanged.

### Outdoor-course rules

- **Closed by nature.** Every section is a 24 m corridor closed on both sides by cliffs at least 7 m above its highest
  floor (never climbable, mantle 1.4 m); one section (the Ember Cave) has a roof. Players can never leave the course.
- **Every water surface is lethal**: a kill zone starts 0.3 m under it (§20). Touching it fails the section for a player,
  and explodes a thrown bomb, like any pit. No current moves anything (§17.2).
- **Pits** are water or gorges, with a kill zone; a last kill zone lies under the whole course.
- **Pass corridors** are declared for every intended pass; decoration, scattered plants and the terrain never enter
  one (the |x| ≥ 25 m rule of the open courses does not apply, §3.1).
- **Checkpoints are campfires**: a cold fire pit in a clearing that catches (flames, embers, smoke, light, crackle) when
  the checkpoint activates, and goes out at a new run. No square on the ground (§19).
- **Time of day is presentation**: the sky, sun and haze move from dawn (act 1) to dusk (act 5) with the current checkpoint;
  nothing in the rules depends on it.
- **Trees, plants and the terrain are generated art** (fixed seeds, saved assets), not procedural levels: the route,
  the sections and every obstacle are designed by hand in the builder (§17.2).

### Route

| Act (time of day) | Checkpoints | Sections and bomb questions |
|---|---|---|
| 1 Misty Hollow (dawn) | 1–5 | Trailhead Glade (warm-up passes between boulders §13.1, a vine ring opens the palisade §13.15); Fern Terraces (relay up rock terraces §13.3); Brook Crossing (stepping stones and a raft over the first water §13.1, §13.2); Rotten Boardwalk (low throws under fallen trunks §13.5, rotten planks §13.7, a raft §13.2); Hollow Log Junction (split lanes §13.4, the first hollow log §13.16) |
| 2 River Run (noon) | 6–10 | Log-Drive Lanes (opposite log drives §13.9, no-carry weirs §13.13); Rapids Rafts (rafts out of step, catch on the move §13.2, §13.8); Two Banks, One Bomb; Mudslide (a chute with no-carry riffles §13.12); Mill Race |
| 3 Granite Cliffs (late afternoon) | 11–15 | Cliff Base Relay (ledges §13.3, a rope lift); Hold the Rope; Ember Cave (steam vents and cold springs §13.14, cairn laser windows §13.13); Ledge Traverse (a swinging log §13.11); Up the Cliff |
| 4 Waterfall Gorge (sunset) | 16–20 | Spray Bridges (plank bridges and rotten planks over a gorge §13.7, a timed vine ring §13.15); Behind the Falls (low throws under the overhang §13.5, cold spray §13.14); Wheel Gorge (a cairn window §13.13, a willow hoop); Down the Rapids; Gorge Lock (rising rafts, a hollow log up the lock §13.16) |
| 5 The Summit (dusk) | 21–25 | Alpine Meadow (moss mounds launch the runners, catch at the apex §13.8); Switchbacks (scree no-carry strips §13.13, a pass across the gully); Boulder Run (a log ram over sun-baked stones §13.11, §13.14, rising pillars §13.10); Knife Edge (a narrow ridge, a timed vine ring and the last palisade §13.15); Summit Arch (the checkpoint arch §13.17, a last relay, the finish beacon) |

The route climbs about 100 m (from 0 to the summit, the highest point), drops twice (a mudslide and the rapids), and turns
nine times each way. The shorter hold fuse is the summit's: 5.0 s from checkpoint 21, 4.5 s from checkpoint 24 (§20).

### Signature puzzles

Each chains elements through the geometry, one source drives one actuator, and 2 players can always solve it.

- **Two Banks, One Bomb**: the river splits the team; two hollow logs (green with one pip, violet with two) reach the two
  banks, and the thrower picks the bank the receiver ran to.
- **Hold the Rope**: a runner holds a stone plate on the ledge to keep the rope lift up while the carrier passes across the
  gorge; the pass lands on that ledge, so with two players the plate holder is also the receiver; a vine ring on the far
  side opens the gate that brings the plate holder back.
- **Up the Cliff**: the stump catapult fires the bomb to the cliff top while the receiver rides the rising rafts.
- **Down the Rapids**: the runners slide down the wet chute while the bomb takes a hollow log to the plunge-pool court.
- **Mill Race**: the water wheel guards the only window for the bomb; the runners take the tunnel under the log stamp.

### Intended difficulty

Act 1 teaches the outdoors (one element per section, wide floors, the first water). Acts 2–3 combine two elements per
section and add the signature puzzles. Act 4 is the hardest (timed transits, gorges, low throws). Act 5 runs on the shorter
fuse and ends with a short, readable climb to the summit beacon.

---

## 15d. Fourth level blueprint — PatataCanopy (M13)

Asked for by the owner after a review of PatataWilds, whose puzzles had the intent but not the constraint. A course in
the **tree tops**, in PatataWilds' nature look, where every section keeps a contract (§13.20). Second in the Bootstrap
level list. Built by `PatataCanopyBuilder` (ARCHITECTURE §4).

Working level:

**PatataCanopy**

Target:

- **5 acts, 25 sections, 25 checkpoints**, one per section; about 2 km;
- **short puzzles, hard execution**: a puzzle reads in about ten seconds, then asks for speed and clean passes (rings of
  6–8 s, the fuse down to 5 s then 4.5 s);
- **two roles that scale**: every puzzle needs exactly two roles (a holder and a carrier, a runner and a thrower) and is
  always solvable by two; a third and a fourth player relay;
- the kit plus three systems: body screens (§13.18), hands-free plates and switches (§13.19).

### Canopy rules

- **The void is the default.** Decks, bridges and branches over a forest floor 40 m down; a kill plane 10 m under every
  section's lowest floor, a safety net under everything.
- **Puzzle sections stand under a leaf roof** 7 m above the floor, over the decks and the void beside them; every cross
  wall reaches it, so the bomb is never lobbed over a wall. The hollow oak (act 3) has its own roofs.
- **Gaps that must be bridged are 14 m**, faces that must not be climbed 5 m or more (§13.20).
- **Junction decks** of 20 × 20 m join the sections, which turn left and right in turn: no two sections meet elsewhere.
- **Checkpoints are campfires** on the junction decks (§15c); arches at checkpoints 15 and 25 (§13.17).

### Route

| Act (time of day) | Checkpoints | Sections and what each forces |
|---|---|---|
| 1 L'Orée (dawn): one system at a time | 1–5 | Premier pont (jumps of 4–4.5 m, the first gap passes); Le Filet (the bomb through the brambles, the runners through the lasers); L'Anneau de lianes (a ring raises the bridge over 14 m for 8 s); La Plaque (an empty-handed holder raises the carrier's bridge; the carrier throws back through a ring over the gap to raise the holder's); Les Lucioles (the plate beyond the lasers cuts them; the catch on it brings them back) |
| 2 Les Ponts suspendus (noon): combined | 6–10 | Les Deux Branches (two branches split by a wall to the roof, lasers where the other branch has none: the team splits and the bomb changes branch through bramble windows); Pont-levis croisé (the east plate raises the west bridge, the ring in the divide raises the east one, the carrier waits on cold moss); Branches balançoires (rides longer than the fuse); Branches pourries (four lines of rotten twigs, one runner per line); L'Écluse (a ring opens the gate, a sap chamber at ×2, lasers for the runners and a ring in a bramble window for the bomb, which raises the last bridge) |
| 3 Le Grand Chêne (late afternoon): inside the hollow oak | 11–15 | La Poulie (a hands-free plate lifts the carrier, a throw down, a plain plate lifts the holder); Le Tronc creux (the bomb goes up only through the hollow branch, the climb is behind lasers); La Spirale (three levels swept wall to wall); Les Galeries (a real choice: a short gallery at ×2 or a long weaving one at ×0.5); Le Cœur du chêne (a shuttle over a pit, the arch checkpoint) |
| 4 La Cime dans le vent (sunset): speed, 5 s fuse | 16–20 | Feuilles-trampolines (launch pads in spores: only empty hands fly, the bomb is thrown up); Branches mouvantes (rolling logs, a shuttle over 14 m, opposite log drives); Couloir de ronces (a slalom of four walls: the runners zig-zag through lasers, the bomb flies straight through brambles); Canon à graines (70 m of void under a branch roof, a shuttle with spores, a catapult that fires 5 s after the throw); Course contre l'anneau (the ring opens a gate 50 m away for 7 s) |
| 5 Le Sommet (dusk): twisted, 4.5 s fuse | 21–25 | La Haie qui s'ouvre (a ring parts the hedge for 6 s, then lasers and a window); Le Pont des spores (a far hands-free plate clears 48 m of spores under a low branch, the catch on it brings them back); Glissade de la grande branche (hedges only a slider fits under, their gap in spores); L'Écluse finale (everything at once); Arche du sommet (the last shuttle, the arch, the beacon) |

The shorter hold fuse: 5.0 s from checkpoint 15, 4.5 s from checkpoint 20 (§20).

---

## 16. Multiplayer product behavior

Target session:

- one player hosts;
- friends join by code;
- 2–4 players;
- cooperative;
- no public matchmaking required for MVP.

Authoritative gameplay rules:

- bomb owner;
- catch resolution;
- fuse expiry;
- illegal bomb collisions;
- explosion;
- checkpoint activation;
- section reset;
- course completion.

These must have **one authoritative source**.

---

## 17. MVP scope

### 17.1 Must have

- Unity project boots cleanly;
- one playable first-person character;
- 2–4 networked players;
- host + join by code;
- one bomb;
- initial bomb ownership;
- throw;
- catch;
- hold fuse;
- escalating beep;
- bomb explodes on invalid environment contact;
- section reset;
- checkpoints;
- one greybox course;
- finish detection;
- rematch/replay path.

### 17.2 Explicitly out of scope

Do not implement unless this file is changed:

- public matchmaking browser;
- dedicated servers;
- progression;
- cosmetics;
- multiple bomb types;
- multiple courses (exceptions, owner decisions: `PatataWilds`, the outdoor course of §15c, M12, `PatataCanopy`, the
  tree-top course of §15d, M13, and `IndustrialPlant`, the closed factory built on §15b's rules, beside `PassSandbox`; the earlier `PrototypeCourse`, `PlaytestCourse`, `PatataPark`
  and `PatataWorks` were removed in the cleanup, archived in OBSTACLES.md §3.2). Still no campaign, no level
  select beyond the Bootstrap level list, and no procedural levels (PatataWilds' generated trees, plants and terrain are
  art with fixed seeds, not a generated route);
- Steam-specific integration;
- leaderboards;
- procedural levels;
- combat;
- pushing;
- grabbing;
- inventory;
- character classes;
- abilities;
- persistent accounts beyond service requirements;
- advanced ragdoll gameplay;
- two-bomb mode;
- wind, fans or currents that bend a flight in progress (#68: they would break the plain ballistic arc, §8.2);
  PatataWilds' wind only sways its plants (a shader), and its rivers move nothing;
- logic wiring between switches (AND/OR, sequences, several sources on one actuator): one source drives one
  actuator (§13.15).

---

## 18. Anti-grief assumptions

This MVP is designed primarily for friend groups.

The fuse ensures that intentional holding sabotage is fast and obvious.

Do not over-engineer anti-grief systems before core validation.

Later possibilities:

- host kick;
- AFK handling;
- vote kick;
- public-session moderation.

---

## 19. Accessibility / readability

At minimum:

- beep urgency must also have visual feedback;
- do not rely on red/green distinction alone;
- use pulse speed / icon / shape / brightness;
- expose camera shake setting before broader testing (`viewEffectsStrength` scales every first-person camera effect, 0 = perfectly steady; the `GameTuning` value is the default, the player's own value is saved by `Settings`; done: the settings screen, from the main menu and the pause menu, ARCHITECTURE §6.2);
- expose flash reduction before broader testing (`flashReduction` dims every flash; same default/player split; done: the settings screen, MVP_TASKS M6.5);
- lethal hazards are striped, not only red; the fuse stage reads through pulse speed, sparks and beep cadence;
  in PatataWilds a hazard is a stained log with charred bands, and a checkpoint is a campfire that is cold or burning
  (flames and smoke: shape and motion, not colour);
- a body screen shows vine strands over a faint field and a "bomb through" sign, a laser curtain striped posts, beams and a
  "no carrying" sign; a hands-free plate is blue with a "throw first" glyph, never the yellow of a plain plate;
- use the Input System so rebinding remains possible.

---

## 20. Starting tuning values

| System | Starting value |
|---|---:|
| Players | 2–4 |
| Hold fuse | 6.0 s |
| Warning phase | 2.0 s |
| Catch grace | 0.35 s |
| Throw speed (tap → full charge) | 14 → 28 m/s |
| Throw charge time | 0.45 s |
| Throw lift / bomb gravity scale | 6° / 1.5 |
| Run speed kept by the throw (along the aim) | 50 %, of at most 11 m/s |
| Run / sprint / crouch speed | 8 / 11 / 4 m/s |
| Slide entry / boost / friction / exit | 7 m/s / +3.5 m/s / 7 m/s² / 5 m/s |
| Mantle height / duration | 0.1–1.4 m / 0.28 s |
| Normal pass distance | 8–12 m (0.25–0.5 s in the air) |
| Aim assist (range / cone / max turn / max lift) | 14 m / 6° / 3.5° / 1° |
| Catch radius (+ facing bonus), vertical scale | 1.0 m (+0.3 m), 0.6 |
| Late catch grace | 0.06 s |
| Catch lag compensation (online, remote receivers) | 0.35 s |
| Catch window | 0.4 s |
| Catch cooldown | 0.35 s |
| Jump coyote time | 0.1 s |
| Jump buffer | 0.1 s |
| Reset delay | ~1.0 s |
| Hold fuse override (Act 3) | 5.0 s from CP6, 4.5 s from CP7 |
| Fuse zones (hot / cold) | × 2 / × 0.5 (forbidden = explodes) |
| Transit delay (tube / cannon) | ~1.2 s / 0.35 s, exit warning 0.5 s before |
| Bomb gate hold time | per gate, 6–10 s |
| Checkpoint spacing | 30–60 s |
| First course length | 3–5 min (Act 1); 8–10 min with Acts 2–3; `PlaytestCourse` ~10 min |
| Second course length (`PatataWorks`, §15b) | 10–14 min first clear, 9 checkpoints |
| Third course length (`PatataWilds`, §15c) | 20+ min first clear, 25 checkpoints in 5 acts |
| Hold fuse override (`PatataWilds`) | 5.0 s from CP21, 4.5 s from CP24 |
| Water (`PatataWilds`) | lethal: a kill zone 0.3 m under every surface |
| Fourth course (`PatataCanopy`, §15d) | 25 checkpoints in 5 acts, about 2 km |
| Hold fuse override (`PatataCanopy`) | 5.0 s from CP15, 4.5 s from CP20 |
| Locked gap / locked face / roof over a puzzle (§13.20) | 14 m / 5 m (3.4 m minimum) / 7 m above the floor |
| Ceiling clearance over a pass corridor (§15b) | intended arc apex + 1.5 m |
| Target frame rate | 60 fps |

These are **starting values**, not final design decisions.

---

## 21. Open tuning decisions

The coding agent must expose these cleanly for playtesting rather than hard-code assumptions everywhere:

- exact fuse duration;
- charge time and the min / max throw speed (charged throw and timed catch are decided, see §8.2 and §9.1);
- catch window duration and cooldown;
- catch radius;
- aim assist strength;
- bomb speed;
- gravity / arc;
- final-section fuse modifier (built as per-checkpoint overrides, see §20; values still open);
- checkpoint frequency;
- visible fuse meter vs audio/visual-only feedback (currently audio/visual only: pulse, sparks, beep cadence).

All of these are exposed in `GameTuning` (`Assets/ScriptableObjects/Tuning/GameTuning.asset`); the values in §20
match that asset. They are to be settled by the external playtest (MVP_TASKS M7.4).

Prefer a centralized tuning asset/configuration.

---

## 22. Prototype success criteria

The concept is considered promising if:

1. two players can pass while moving/jumping for at least 30 seconds;
2. missed catches usually feel like player mistakes;
3. players immediately understand that environment contact means failure;
4. players naturally position themselves as receivers;
5. players communicate or ping without being instructed;
6. the same greybox course remains entertaining across several attempts;
7. failures create laughter rather than confusion;
8. players voluntarily want another run;
9. network latency does not make catches appear arbitrary.

---

## 23. Critical risks

### Throw/catch feels unfair — Critical

Mitigation:

- forgiving catch volume;
- modest aim correction;
- strong catch confirmation;
- reasonable bomb velocity;
- network testing early.

### Scope expansion — Critical

Mitigation:

- do not add content until the pass sandbox works;
- one bomb;
- one course;
- one movement kit.

### Weak player blocks the team — High

Mitigation:

- frequent checkpoints;
- forgiving movement;
- wide early catch zones;
- optional assists later.

### Network latency breaks catches — High

Mitigation:

- one authoritative catch resolver;
- lag-tolerant design (catch lag compensation, see ARCHITECTURE §13.2);
- avoid extreme projectile speeds;
- test with remote machines before content expansion.

### Repetition — Medium

Mitigation:

- vary spatial relationships rather than add unrelated mechanics:
  - verticality;
  - moving windows;
  - split lanes;
  - receiver movement;
  - cadence changes.

---

## 24. Agent behavioral constraints

When implementing from this specification:

1. **Do not invent new gameplay features.**
2. Prefer the simplest implementation that satisfies acceptance criteria.
3. Keep all gameplay systems modular and easy to tune.
4. Do not optimize prematurely.
5. Do not build polished art before gameplay validation.
6. Do not hand-edit Unity scene/prefab YAML when an Editor/MCP operation can safely create the object.
7. Keep the Unity Console free of compilation errors before moving to the next task.
8. Preserve working gameplay while refactoring.
9. Do not add third-party packages unless clearly necessary.
10. If a task conflicts with this file, this file wins unless explicitly updated.

---

## 25. First proof-of-fun test

Before networking or a complete level, build:

- one flat room;
- two player characters;
- one bomb;
- two raised platforms.

The prototype is acceptable when two local players/instances can:

- move;
- jump;
- throw;
- catch;
- fail by touching the world;
- fail by holding too long;
- reset rapidly.

**Do not move on to substantial level content until this loop is fun.**
