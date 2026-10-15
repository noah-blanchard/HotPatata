# HotPatata — LEVEL_AUTHORING.md

How to make a level by hand in the Unity Editor: no code, no builder. The design behind the tools is ARCHITECTURE §27.

The tools come in three steps (M18). This guide covers what is built:

| Step | What you get | State |
|---|---|---|
| 1. The level and its timber | a new level scene, decks, walkways and stairs, rope bridges, railings, houses, trestle towers; the menu; the day | built |
| 2. The nature | the terrain in the nature look, valley walls, water, giant trees, forests (planted or painted), mist, sound | built |
| 3a. The game | checkpoints numbered along the course, size handles, wiring, contracts, Verify Level, the route runner, Play from here | built |
| 3b. The look | forest looks for the kit, props, the example level | next |

A good order: make the level (§1), shape the land (§2), plant the giant trees (§3), hang the timber on them (§4 to §7),
plant the forest round it (§8), the mist and the sound (§9), then the game (§10), checked and played (§11).

---

## 1. Make the level

1. `HotPatata/Level/New Level...`
2. Fill in:
   - **Scene name**: letters and digits, no spaces (`TreetopRun`). The menu loads the level by this name.
   - **Name in the menu**: what players see (`Treetop Run`). Empty = the scene name.
   - **Players needed**: 2 for most levels; 3 if a puzzle needs three (PROJECT_SPEC §15e).
   - **Day**: the light the level borrows: Wilds, Canopy, Temple or Ranger. It moves from dawn at the start to its last
     moment at the last checkpoint.
3. **Create Level**. The scene opens: `Assets/Scenes/Levels/<name>.unity`.

You can press Play at once: the players start on a flat forest floor (a 256 m terrain) with a safety net far under it.
Everything you add goes under `SectionRoot/Level` (its groups: `Timber`, `Trees`, `Kit`, `Props`).

The level's settings are on the `Level` object (`LevelSettings`): menu name, players, day (**Apply the day** after
changing it), and the terrain, mist and sound tools below.

## 2. Shape the land

The level's ground is a Unity **Terrain** (`Level/Terrain`). Select it and sculpt it with its own brushes (Raise or Lower,
Set Height, Smooth, Stamp): dig a ravine, raise a hill, flatten a clearing. It is drawn as the forest floor of the nature
courses by itself: leaf litter where it is flat, rock where it is steep, in the height mist. (Its Paint Texture tool is not
used.)

- **Valley walls**: on the `Level` object, **Raise valley walls round the edge** lifts the terrain's border into rock walls
  (choose their width and height). Ctrl+Z undoes it.
- **Water**: `GameObject > HotPatata Level > Water`. A surface at the piece's height (drag the yellow squares to size it,
  turn the piece to turn its flow) with a kill zone under it down to its depth: falling in, or throwing the bomb into it,
  fails the section. Dig the ground under it first.
- The safety net follows the terrain on every save (always under its lowest point).

After reshaping the ground under timber: `HotPatata/Level/Rebuild All Pieces` (the posts find the new ground).

## 3. Giant trees

`GameObject > HotPatata Level > Giant Tree` grows a hero tree (the kind PatataRanger's post is built round), 40 to 70 m.

- **Start from** an old oak, a tall fir, a pine or a snag (Inspector), then adjust its height, girth, crown and seed.
- **The white handle at the top of the trunk** leans the tree and sets its height.
- **Add a carrying limb**: a dot on the trunk (drag it up or down) and a handle at its tip (drag it to aim it, lengthen it,
  raise it). Limbs hold hangers (a deck far from the ground hangs from them) and can carry leaves.
- The trunk and the big limbs always collide: players stop at them, a thrown bomb explodes on them.
- A tree takes a second or two to grow: it regrows when you let go of a handle.

## 4. Add timber

`GameObject > HotPatata Level` (or right click in the Hierarchy) adds a piece where the Scene view looks, built at once:

| Piece | What it is |
|---|---|
| **Deck** | boards on joists inside an outline, at the piece's height; posts, a braced tower, knee braces to the trunk it hugs, or hangers hold it |
| **Walkway or Stair** | boards along a line; steps where it climbs (keep flights under 35°) |
| **Rope Bridge** | slats on ropes between two ends, sagging; side walls stop players stepping off |
| **Railing** | a free rail, colliding up to the hand rail |
| **House** | a plank floor, four plank walls (they stop players and the bomb), doors and windows, a pitched roof with a ceiling under it (nothing is lobbed in) |
| **Trestle Tower** | four braced legs from the ground up to the piece's height |

Every piece has a **Wear** (new, or aged boards) and most a **Support** (Auto lets the kit choose, or force Posts, Tower,
Braces, Hangers or None). Hangers tie to a giant tree's limbs or trunk above them.

**A deck round a giant tree**: set its **Trunk** to the tree, raise the deck to its height, then **Ring round the trunk**
(or **Arc**) in its Inspector. It is cut round the trunk and braced to it, and the tree's crown limbs are sawn off round
the deck by themselves (stubs only, so no limb runs through it). A deck near a trunk is braced to it even without the field.

## 5. Shape it in the Scene view

Select a piece. Its points show as yellow dots:

- **Click a dot** to select it, then drag its move handle.
- **Click a +** (in the middle of a segment) to add a point there.
- **Ctrl+click a dot** to delete it.
- **Move the piece** (the usual move tool) to move it whole; a deck's height is the piece's height.
- **Draw points (pencil)** in the Inspector: the points are cleared, then each click in the Scene view adds one (on the
  ground or a deck under the mouse). Enter, Escape or a right click ends it.

The geometry follows when you let go of the mouse (the line is drawn live while you drag). Ctrl+Z undoes as usual.

A deck's Inspector has ready shapes (Rectangle, Rounded, Oval and, once it has a Trunk, a Ring or an Arc round the trunk)
to start from and then pull into shape.

## 6. Join pieces

- **Select an end dot** (the first or last point of a walkway, either end of a bridge) and **drag it onto a deck's edge or
  onto another walkway's or bridge's end**: when it is close enough on screen a green ring shows; let go and it is
  attached (the label and the Inspector say to what). On a deck the railing opens there. The end follows what it is
  attached to when that moves. **Hold Shift** while dragging to place an end freely.
- **Attach ends** in a walkway's or bridge's Inspector attaches each free end to the nearest deck edge or end within 4 m.
  **Detach start / end** frees it.
- **A deck's own openings** (Add opening in its Inspector) are cyan dots on its edge: slide them where a gap should be.
- A bridge shows its length and the slope at its ends; keep it at 18° or less.

## 7. Presets

**Save As Preset** in a piece's Inspector saves its settings (width, rails, supports, wear...), never its shape. Use it
again with **Apply Preset**, or drag the preset from the Project window into the Scene view: a new piece of that kind
appears there with those settings.

## 8. Forest

Two ways, together:

- **Forest Area** (`GameObject > HotPatata Level > Forest Area`): draw its outline like a deck's (dots, +, Ctrl+click, or
  the pencil). Inside it the forest is planted by itself, like PatataRanger's: giants in groves with clearings, an
  understory, ferns, grass, bushes, rocks, logs and moss. Set how dense each layer is, how open the clearings are, and how
  far it keeps from the paths. Nothing grows on a walkway, a bridge, a deck, a pass arc, on water, on rock too steep, or
  too near a giant tree, and it replants by itself when a piece near it changes (**Replant now** forces it). Nothing in it
  collides: it is decoration.
- **Painted**, one by one: select the terrain, **Paint Trees** (pines, firs, broadleaves, snags, bushes) and **Paint
  Details** (ferns, grass). If the terrain has none, **Set up the forest brushes** on the `Level` object. Then **Clear
  painted paths** takes off what was painted on a path, in a solid, on water or on a pass arc.

## 9. Mist and sound

- **Mist**: it lies a little under the ground everywhere (the team walks in its veil), thick at dawn, as the day says.
  On the `Level` object: **Mist density** (thinner or thicker than the day) and **Mist depth**. A **Mist Zone**
  (`GameObject > HotPatata Level > Mist Zone`) fills its box with mist up to its top: a ravine, a hollow, a clearing at
  dawn. The mist is baked again on every save (or **Bake the mist now**).
- **Ambience**: on the `Level` object, each section's ambience (Forest, River, Canopy, Cave...): from the start, then from
  each checkpoint. Written on save.
- **Sound Source**: a river, a waterfall, a fire, wind... at a spot (pick its sound and how far it is heard). The sounds
  are the folders of `Assets/Audio/Ambience/Emitters`; drop files there (`Assets/Audio/README.md`).
- **Ambience Zone**: a box where the ambience is another one (a cave in the forest).

## 10. The game: checkpoints, kit, wiring, contracts

**Checkpoints and the finish.** `GameObject > HotPatata Level > Checkpoint (campfire)` places a checkpoint with its campfire
beside it (the nature courses' checkpoints are campfires); `Finish` places the end. Place them in any order, then
**Number checkpoints along the course** (on the `Level` object): they are numbered 1, 2, 3... in the order a team reaches
them walking from the start (along the walkways and bridges, across the decks and the ground).

**The kit.** Drag the kit's prefabs from `Assets/Prefabs/Gameplay`, `Obstacles`, `Platforms` and `Variants`
(docs/OBSTACLES.md) under `Level/Kit`. Select one in the Scene view:

- a **door, bridge or lift**: drag its yellow box to size it; its open position is the blue ghost box: drag the blue ball to
  move where it opens to (and how far);
- a **zone** (hot, cold, forbidden), a **laser curtain**, a **body screen**: drag its box to size it;
- a **sun beam**: drag the square at its end to lengthen it; a **plate**: drag the square at its edge to size it.

Every part follows (visual, collision, lethal edge). Ctrl+Z undoes it. A drawbridge or a pivot turns on a hinge: size it
in its inspector.

**Wiring.** One source drives one actuator (no AND, no OR). Select the door (bridge, lift, switch) and press **Link: pick
its source** in its inspector, then click the plate, the hoop or the sun beam that drives it. A switch's **Link: add a
target** adds what it turns on and off (a curtain, a screen). Dotted yellow lines show every link in the Scene view,
with what drives it and how long it holds ("hourglass plate, holds 6 s"); `HotPatata/Level/Show Wiring` hides them.

**Contracts.** Every section says what it forces and locks its shortcuts (PROJECT_SPEC §13.20, docs/OBSTACLES.md §5): on
a checkpoint, **Add this section's contract**; on the `Level` object, **Add the start's contract**. Write what the section
forces; for a level for three, why two fail. Then **lock each shortcut**: **Gap** (a jump that must not be made: from
its take-off to its landing), **Climb** (a face that must not be climbed: its foot and top), **Lob** (a wall the bomb must
not be lobbed over: the thrower and the wall's top), **Spread** (two plates one body cannot hold in turn). Each lock is a
coloured line in the Scene view: drag its balls onto the places (they land on what is under the mouse). **Check this
contract** says whether each lock holds.

## 11. Check and play

- **Verify Level** (on the `Level` object, or `HotPatata/Level/Verify Level`): everything a level must hold, in a list.
  Click a line to select what it is about. Errors keep a level out of the menu: a section without a contract, a lock that
  does not hold, checkpoint ids that skip, no finish, water without a kill zone, a pass out of reach or under a ceiling,
  kit outside the level's section, a piece not built, the menu out of date. Warnings are worth a look: something in a
  walkway's way, a step past its end, plants on a path, checkpoints out of order. It also gives the level's budget
  (triangles, plants, meshes).
- **Run Route Runner** (on the `Level` object, or the `HotPatata/Level` menu): Play starts and player 1 walks every
  walkway, stair and bridge on its own (the bomb kept from exploding); the Console says which ones it fell off or got
  stuck on, and how long each took.
- **Play from here**: on a checkpoint (or `HotPatata/Level/Play From Selected Checkpoint`), Play starts with the run at that
  checkpoint; **Play from the start** on the `Level` object.

## 12. Save

Saving the scene (Ctrl+S) writes the pieces' meshes and the forests' plantings to `Assets/Art/Generated/<level>/Pieces/`
(one folder per piece; removed pieces' folders are deleted), gathers the level's walk lines, refits the safety net, bakes
the mist and writes the sections' ambience. The Console then says what was written, and lists anything a piece cannot be
built as drawn (a hanger with nothing above, a scaled piece...).

## 13. Put it in the menu

`HotPatata/Level/Register In Menu...` with your level open:

- its **name in the menu** and the **players needed**;
- the list of the menu's levels: **drag yours** where it goes (the others keep their order);
- **Save to the menu**. The level is added to the build and shows in Bootstrap's level list, with a spawn choice per
  checkpoint. **Remove from the menu** takes it out.

A level is official (shipped in the menu) only once Verify Level reports no error.

## Rules

- Never edit a piece's `Generated` child: change the piece and it is rebuilt.
- Do not scale pieces: draw their size with their points.
- The rules of AGENTS.md hold for authored levels as for the courses: collisions by layers, the kit's prefabs as they are,
  water over a kill zone, decoration out of the passes, a contract per section.
