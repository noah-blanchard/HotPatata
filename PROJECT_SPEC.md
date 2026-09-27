# HotPatata — PROJECT_SPEC.md

> **Status:** MVP / rapid prototype  
> **Title:** HotPatata  
> **Genre:** Cooperative first-person parkour / hot-potato party game  
> **Initial target:** Windows PC  
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

Working level:

**Training Facility**

Target length:

- approximately **3–5 minutes** for the first complete greybox version (Act 1, beats A–G);
- extended to approximately **8–10 minutes** with Act 2 (Patata Factory) and Act 3 (The Climb & The Drop) after
  the owner's playtests of Act 1 (see ARCHITECTURE.md §4 for the beat layout). Still one course (§17.2).

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
- optionally shorter fuse around **4.5 s** for this section only after the base version works.

### Finish

Finish only when all required players enter the end condition.

Display at minimum:

- completion;
- run time;
- optional explosion/reset count.

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
- multiple courses;
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
- two-bomb mode.

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
- expose camera shake setting before broader testing (`viewEffectsStrength` in `GameTuning` scales every first-person camera effect, 0 = perfectly steady; to be surfaced in a settings menu);
- expose flash reduction before broader testing;
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
| Catch window | 0.4 s |
| Catch cooldown | 0.35 s |
| Jump coyote time | 0.1 s |
| Jump buffer | 0.1 s |
| Reset delay | ~1.0 s |
| Checkpoint spacing | 30–60 s |
| First course length | 3–5 min (Act 1); 8–10 min with Acts 2–3 |
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
- final-section fuse modifier;
- checkpoint frequency;
- visible fuse meter vs audio/visual-only feedback.

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
- lag-tolerant design;
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
