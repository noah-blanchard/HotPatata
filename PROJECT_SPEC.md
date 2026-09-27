# BEEP! — PROJECT_SPEC.md

> **Status:** MVP / rapid prototype  
> **Working title:** BEEP!  
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

### 8.2 Charged throw (decided after the first playtest)

The throw is **chargeable**:

- **hold** the throw button to charge, **release** to throw; a quick tap is the shortest, slowest pass;
- charge runs from 0 to 1 over `throwChargeTime` (1.0 s) and is capped at 1;
- launch speed goes linearly from `throwSpeedMin` (tap) to `throwSpeedMax` (full charge), so a longer
  charge means a **faster and farther** throw. Starting values: 10 to 24 m/s, i.e. roughly 5 m to 27 m
  on level ground;
- forward-biased ballistic throw from the hand/release anchor, pitched up slightly (`throwUpAngle`);
- optional subtle aim correction toward an eligible receiver (unchanged, aim assist felt fine);
- while charging, the carrier's fuse keeps burning: charging is a risk, not a pause;
- charging is cancelled if control is locked, the bomb is lost or the section resets.

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

After a window closes there is a **cooldown** (`catchCooldown`, starting value 0.5 s) before catch can be
pressed again, so the button cannot be mashed. A successful catch spends the window and clears the cooldown.
A carrier cannot open a catch window.

The catch state is shown to the local player by a UI indicator around the crosshair: faint brackets = ready,
large green brackets = window open, small red brackets with a shrinking bar = cooldown.

Starting catch radius (tightened after the first playtest, since timing now does the gatekeeping):

- roughly **0.5–0.7 m** around upper torso / hands (starting value 0.6 m).

Prefer a front-biased catch region.

### 9.2 Catch assistance

Assistance may widen tolerance but must **never** replace the catch input.

Allowed:

- slightly oversized catch volume;
- small angular correction;
- short final-trajectory magnetism;
- clearer assistance while receiver is facing the bomb.

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
| Throw | Left click (hold to charge, release to throw) | Right trigger | Required |
| Catch | Right click (timed, see §9) | Left trigger | Required |
| Ping / “throw to me” | Q | Bumper | Later MVP if needed |

Catch is required to receive the bomb; there is no automatic catch.

---

## 11. Player movement

Movement target:

**responsive, forgiving, predictable first-person platforming.**

Required characteristics:

- first-person camera at eye height; the body always faces where the player looks, and the player's own body is hidden from their own view (other players still see it);
- fast acceleration;
- fast braking;
- moderate air control;
- single jump;
- coyote time around **0.1 s**;
- jump buffer around **0.1 s**;
- no stamina;
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

- approximately **3–5 minutes** for the first complete greybox version.

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
- expose camera shake setting before broader testing;
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
| Throw speed (tap → full charge) | 10 → 24 m/s |
| Throw charge time | 1.0 s |
| Normal pass distance | 8–12 m (about half charge) |
| Catch radius | 0.6 m |
| Catch window | 0.25 s |
| Catch cooldown | 0.5 s |
| Jump coyote time | 0.1 s |
| Jump buffer | 0.1 s |
| Reset delay | ~1.0 s |
| Checkpoint spacing | 30–60 s |
| First course length | 3–5 min |
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
