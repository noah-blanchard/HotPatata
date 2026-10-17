# HotPatata — LEVEL_AUTHORING.md

How to make a level by hand in the Unity Editor: no code, no builder. The design behind the tools is ARCHITECTURE §27.

The tools come in three steps (M18). This guide covers what is built:

| Step | What you get | State |
|---|---|---|
| 1. The level and its timber | a new level scene, decks, walkways and stairs, rope bridges, railings, houses, trestle towers; the menu; the day | built |
| 2. The nature | the terrain in the nature look, valley walls, water, giant trees, forests (planted or painted), mist, sound | built |
| 3a. The game | checkpoints numbered along the course, size handles, wiring, contracts, Verify Level, the route runner, Play from here | built |
| 3b. The look | forest looks for the kit, props, the example level (`OldOak`, archived since) | built |
| 4. Timber by hand | structures (shelter, gantry, sign, pulley beam), freezing into elements, the structure library | built |
| 5. Rock and mine | caves (tunnels and caverns), cliffs, mine timber, lane markers (M20) | built |

A good order: make the level (§1), shape the land (§2), plant the giant trees (§3), hang the timber on them (§4 to §7),
plant the forest round it (§8), the mist and the sound (§9), then the game (§10), checked and played (§11), and dress it
(§14, §15). The example level, "The Old Oak" (§16), was made this way; it is archived with the other maps since the fresh start
(`docs/MAP_ARCHIVE.md`, branch `archive/maps-v1`).

---

## 1. Make the level

1. `HotPatata/Level/New Level...`
2. Fill in:
   - **Scene name**: letters and digits, no spaces (`TreetopRun`). The menu loads the level by this name.
   - **Name in the menu**: what players see (`Treetop Run`). Empty = the scene name.
   - **Players needed**: 2 for most levels; 3 if a puzzle needs three (PROJECT_SPEC §15e).
   - **Day**: the light the level borrows: Wilds, Canopy, Temple or Ranger (named after the archived courses they were made
     for: a misty dawn to dusk, a cooler forest day, a jungle day ending in a storm, a morning climbing out of the mist). It moves from dawn at the start to its last
     moment at the last checkpoint.
3. **Create Level**. The scene opens: `Assets/Scenes/Levels/<name>.unity`, copied from `Assets/Scenes/Templates/CourseTemplate.unity`
   (a run, spawns, a bomb, a camera and networking, no geometry).

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
- **The ground opens over a cave** on every save: where the terrain would stand in the clear space of a cave that has its own
  **Floor** (a tunnel's mouth in a hillside, a gallery under a hill), it is cut out. Raise a hill over a cave freely; put rock over
  its mouth (a cliff as a lintel) where the slope crosses it, the hole's edge is square.

After reshaping the ground under timber: `HotPatata/Level/Rebuild All Pieces` (the posts find the new ground).

## 3. Giant trees

`GameObject > HotPatata Level > Giant Tree` grows a hero tree (the kind the archived PatataRanger's post was built round), 40 to 70 m.

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
  the pencil). Inside it the forest is planted by itself, like the archived PatataRanger's: giants in groves with clearings, an
  understory, ferns, grass, bushes, rocks, logs and moss. Set how dense each layer is, how open the clearings are, and how
  far it keeps from the paths. Nothing grows on a walkway, a bridge, a deck, a pass arc, on water, on rock too steep, or
  too near a giant tree, and it replants by itself when a piece near it changes (**Replant now** forces it). Nothing in it
  collides: it is decoration.
- **On the way**: tick **Walked** on a Forest Area the team walks across (a clearing, a trail, a gorge's floor): only what a player
  walks through grows there (ferns, grass, moss, low bushes), never a tree, a rock or a log. Keep the woods' outlines off the way.
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

**Checkpoints and the finish.** Look at the floor where it goes (a deck, a walkway, the ground: the middle of the Scene view)
and `GameObject > HotPatata Level > Checkpoint (campfire)`: the checkpoint stands there, its four spawns round it, and its
campfire (its child) 3.4 m beside them, on the first side with floor and room (its right, left, ahead, behind, then the
diagonals): never off the deck, in a rail or on a plate. `Finish` places the end the same way. Place them in any order, then
**Number checkpoints along the course** (on the `Level` object): they are numbered 1, 2, 3... in the order a team reaches
them walking from the start (along the walkways and bridges, across the decks and the ground).

A checkpoint needs room: its spawns stand 1.2 m round it and its fire 3.4 m away, so put it on a deck about 6 m by 9 m or more
(or the ground). Select it to see, in the Scene view, its **zone** (orange box: the team reaches the checkpoint there), its
**spawns** (blue rings) and its **campfire** (orange ring, where the team gathers): drag the ring to move the fire (3 to 6 m
away, on the same floor); the zone follows it. **Place the campfire beside it** puts it back; the zone is also fitted round the
fire on every save. Verify Level says when a spawn stands in a rail or in the air, or a fire is too near or too far.

**The kit.** `GameObject > HotPatata Level > Kit` (or right click in the Hierarchy) places any kit element on the floor the Scene
view looks at (a deck, a walkway or the ground; raised where it hangs: a bridge, a hoop), in `Level/Kit`, selected: doors (one that rises, one
that slides aside), bridges, drawbridge, pivot, lift, moving, falling and plain platforms, conveyor; plates (plain,
hands-free, heavy, hourglass), hoop, arch, sun beam, switch; hot, cold and forbidden zones, laser curtain, body screen, tube,
cannon, rotating bar, sweeper, piston, crusher, windmill, launch pad, kill zone (what each does: docs/OBSTACLES.md), in the
kit's own look or, under `Kit/Forest`, in the forest's (§14). Select one in the Scene view:

- a **door, bridge or lift**: drag its yellow box to size it; its open position is the blue ghost box: drag the blue ball to
  move where it opens to (and how far): up for a door that rises, aside for one that slides;
- a **zone** (hot, cold, forbidden), a **laser curtain**, a **body screen**: drag its box to size it;
- a **sun beam**: drag the square at its end to lengthen it; a **plate**: drag the square at its edge to size it.

Every part follows (visual, collision, lethal edge; a forest look's own visual is scaled with it). Ctrl+Z undoes it. A
drawbridge or a pivot turns on a hinge: size it in its inspector. A drawbridge lies flat when open; to make it climb to a
higher deck, turn its `Waypoint_Open` child up (a negative X rotation: an 8 m bridge climbing 3.5 m is -26°), as the example's.

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
  does not hold, checkpoint ids that skip, no finish, a spawn in a rail or in the air, a campfire too near, too far or on
  another floor, water without a kill zone, a pass out of reach or under a ceiling, kit outside the level's section, a piece
  not built, the menu out of date. Warnings are worth a look: something in a
  walkway's way, a step past its end, plants on a path, checkpoints out of order. It also gives the level's budget
  (triangles, plants, meshes).
- **Run Route Runner** (on the `Level` object, or the `HotPatata/Level` menu): Play starts and player 1 walks every
  walkway, stair and bridge on its own (the bomb kept from exploding); the Console says which ones it fell off or got
  stuck on, and how long each took.
- **Play from here**: on a checkpoint (or `HotPatata/Level/Play From Selected Checkpoint`), Play starts with the run at that
  checkpoint; **Play from the start** on the `Level` object.

## 12. Save

Saving the scene (Ctrl+S) writes the pieces' meshes to the shared mesh catalogue (`Assets/Art/Generated/Catalog/`) and the
forests' plantings to `Assets/Art/Generated/<level>/Foliage/` (removed pieces' plantings are deleted, and meshes nothing uses
any more leave the catalogue), gathers the level's walk lines, fits each checkpoint's zone round
its campfire, refits the safety net, bakes
the mist and writes the sections' ambience. The Console then says what was written, and lists anything a piece cannot be
built as drawn (a hanger with nothing above, a scaled piece...).

## 13. Put it in the menu

`HotPatata/Level/Register In Menu...` with your level open:

- its **name in the menu** and the **players needed**;
- the list of the menu's levels: **drag yours** where it goes (the others keep their order);
- **Save to the menu**. The level is added to the build and shows in Bootstrap's level list, with a spawn choice per
  checkpoint. **Remove from the menu** takes it out.
- `PassSandbox` stays in the list as the test map: the Editor and development builds show it, a release build never does.

A level is official (shipped in the menu) only once Verify Level reports no error.

## 14. The kit in the forest look

`GameObject > HotPatata Level > Kit > Forest` places the kit in timber, rope and stone (Prefab Variants of the kit in
`Assets/Prefabs/Variants/Forest/`, docs/OBSTACLES.md §4.1): the same systems, colliders and gameplay cues (a plate's icon, a
zone's colour, an hourglass's sand, a curtain's field), drawn for the forest.

| Forest look | Kit element | Note |
|---|---|---|
| Winch | hourglass plate | a plank rim and a drum on its frame behind the pad; its sand counts down as the kit's |
| Lever | hands-free plate | a plank rim and a lever on its block at the pad's edge |
| Drawbridge | drawbridge | planks on two stringers with iron straps |
| Cage | lift | a plank floor, four corner posts, a frame and its rope (open sides) |
| Signal ring | hoop | a bent branch bound with rope, hung on two ropes |
| Bell arch | arch | two log posts, a log lintel with braces and its bell |
| Chute | tube | the kit's tube in wood (its pipe follows its path, so only its material changes) |
| Net | body screen | a rope net between two log posts |
| Embers | hot zone | charred logs and coals on its floor, sparks rising; its colour stays |
| Sparks | laser curtain | a timber frame with an iron rail, sparks streaming down; its field stays |
| Palisade gate | door | sharpened logs side by side, two cross beams and iron straps; its lethal lower edge stays |
| Gangway | bridge (slides out) | planks on two stringers with iron straps |
| Log raft | moving platform | logs lashed side by side under two cross poles |
| Rotten planks | falling platform | grey planks with gaps and a broken one, on two stringers |
| Swinging log | sweeper | a trunk with iron bands on an iron hub; its lethal reach stays the bar's |

Size them with the same handles (§10). `HotPatata/Kit/Build Forest Looks` rebuilds them from their builder; never edit
them by hand.

## 15. Props

**The library.** `Assets/Prefabs/Props/` holds the CC0 models at their real size, sorted by kind (Rocks, Wood, Plants,
Containers, Household, Lanterns, Ruins, Wildlife), drawn in the nature look, plus ready sets in `Sets/` (a workbench, a
woodpile, a rope corner). Drag one from the Project window into the Scene view, under `Level/Props`. A prop is decoration
(walked through) until its **Solid** box is ticked (inspector): tick it for anything that looks walkable or in the way (the
workbench and the woodpile are solid already). Mind a prop's size: some are groups (`wooden_barrels_01` is three barrels and
their staves, 4 m wide). Keep props out of walkways, passes and checkpoint clearings. `HotPatata/Level/Build Prop Library`
rebuilds the library.

**The brush.** `HotPatata/Level/Props Brush`: choose pebbles, twigs, roots, moss or ferns, its radius and density, press
**Paint**, then click and drag on the ground or a deck. It never paints on a walkway or a bridge's line. Shift erases what the
brush painted (nothing else); Escape stops.

## 16. The example level

"The Old Oak" (`Assets/Scenes/Levels/OldOak.unity` on the branch `archive/maps-v1`, archived at the fresh start): three
checkpoints, about three minutes for two players, made with these tools only. Check it out from the archive to open it.

| Section | What it forces | Tools |
|---|---|---|
| Start to CP1 | the forest path, the oak's stair, the oak walk to the winch landing | ground walkway, giant tree, ring deck, stair, walkway, forest |
| CP1 to CP2 | one holds the winch; the drawbridge climbs 3.5 m to the ridge; its sand lets the last one cross | hourglass winch wired to the drawbridge, a gap lock |
| CP2 to CP3 | sparks in the hut's far door burn the bomb: one walks through, the other lobs it over the roof | house with a ceiling, laser curtain, a climb lock |
| CP3 to the finish | a rope bridge over a fire pit whose embers burn the fuse fast | rope bridge, hot zone, a gap lock, the finish on the crow's nest |

![The oak stair up to the terrace](images/old-oak/1-oak-stair.jpg)
![The winch landing: CP1 and its campfire, the winch, the drawbridge raised](images/old-oak/2-winch-landing.jpg)
![The ridge, the hut walk and the spark hut](images/old-oak/3-spark-hut.jpg)
![The pine lookout and the rope bridge over the embers to the crow's nest](images/old-oak/4-rope-bridge.jpg)

Around it: the valley's walls, a pond with its mist, a forest kept off the paths, props (the camp at the start, lanterns,
barrels). A ring deck's seam (where its outline closes) is turned away from its joins. Verify Level reports nothing on it and
the route runner walks it (`AuthoredLevelTests`).

## 17. Freeze timber into elements

A piece is redrawn from its settings, so you cannot move one of its boards. When its shape is right, **freeze** it: select
it and click **Freeze into elements** in its Inspector. It becomes its boards, beams, posts, ropes and iron, each a
GameObject you move, turn or delete as usual, sorted in groups (Boards, Frame, Supports, each rail). It looks exactly the
same.

- **One way.** The piece's handles and settings are gone (Ctrl+Z only). Freeze when the shape is settled.
- **Walking.** The boards have no collider of their own: the structure lays a smooth walking surface under its walkable
  boards when you let go of the mouse. Move a board up a little, the surface follows; **delete boards and it is a real hole**.
- **What collides.** A board marked **Solid** stops players and the bomb (a wall's boards, a hut's logs, a tower's legs); the
  rest is drawn only. The invisible walls of rails and bridge sides are **Wall** objects: drag their points (cyan cubes).
- **Ropes.** Drag a rope's points (white) or its ends (yellow). An end let go on another element (or anything solid) hangs on
  it (green) and follows it when it moves. **Attach** and **Detach** in the rope's Inspector do the same.
- **Boards.** Drag a board's yellow end dots to make it longer or shorter (the other end stays). Change its size, its cuts or
  its wood in the Inspector.
- A walkway or bridge attached to a deck you freeze stays where it is, its end now a free point.

## 18. Elements by hand

`GameObject > HotPatata Level > Element` adds one **Plank**, **Post**, **Beam**, **Rope** or **Iron Strap** where the Scene
view looks. With an element selected it goes beside it, in its structure (a plank there is part of the walking surface).

With several elements selected, the Inspector's buttons work on all of them:

| Button | What it does |
|---|---|
| New boards, Aged boards, Timber | their wood |
| Solid, Drawn only | whether they stop players and the bomb |
| Jitter | moves and turns each a little at random: hand-laid, not machined |
| Set down | each onto what is under it (the ground, a deck, other timber) |
| Align | each board level with the boards beside it |

Verify Level warns about a rope that hangs on nothing and a post that stands on nothing.

## 19. The structure library

`GameObject > HotPatata Level > Structure` adds a **Shelter** (four posts, a roof with two slopes or one, low walls, a winch
drum), a **Gantry** (braced legs, a head, a pulley and its rope, a little roof), a **Sign** (a board on one or two posts, an owl,
a leaf or an axe painted on it) or a **Pulley Beam** (on its own post, or bolted to the trunk or wall the piece is set against).
Set them in the Inspector, move and turn the piece; freeze them to move their boards by hand.

`HotPatata/Level/Build Structure Library` makes the library's first prefabs (Shelters: Winch Booth, Woodshed, Lean-to;
Gantries: Gate Tower, Lift Gantry; Signs: three clue boards and a signpost; Pulleys: Pulley Beam, Pulley Post). It only
makes the missing ones: once a prefab exists it is yours. To start one over, right click it in the Project window,
**HotPatata > Regenerate Library Structure** (your changes to it are lost).

Select a frozen structure and click **Save to library...** (its Inspector): it becomes a prefab in
`Assets/Prefabs/Structures/` (pick the folder for its kind: Shelters, Gantries, Signs, Pulleys...). Drag it from the Project
window into any level. It is yours: open it in Prefab Mode to change it, or make a Prefab Variant. A structure is scenery:
place the kit's gameplay prefab (a winch plate, a gate) in it in the level, never inside the prefab.

Meshes no scene or prefab uses any more leave the catalogue by themselves when you save (`HotPatata/Level/Clean Mesh Catalog`
does it at once).

**PatataRanger** (archived, `docs/MAP_ARCHIVE.md` §6.5) was a level made of frozen timber: every deck, stair, bridge, hut, booth
and tower a structure of elements. The library's booths, gantries, pulleys and signs come from it.

## 20. Caves

`GameObject > HotPatata Level > Rock > Cave Tunnel` or `Cavern` (in `Level/Props`).

- **A tunnel**: its dots are the floor's centre line (dots, +, Ctrl+click, or the pencil). **Width** and **Height** are its clear
  space inside: the rock only bulges outward (**Roughness**), so what you draw is what a player has. **Thickness** is the rock's.
  Its ends are mouths; tick **Close start** or **Close end** for a dead end.
- **A cavern**: its dots are its outline on the floor (keep it round: the dome rises from it to its middle, **Height** high).
- **Floor**: the terrain is the floor; tick **Floor** for a rock floor at the piece's height (a cave above the ground, or over a
  dug river).
- **Skylights**: **Add skylight** in the inspector, then drag its cyan dot: a hole in the roof (its radius in the list). A sun
  beam fits under one.
- Its walls and roof stop players and the bomb, and **its roof is a ceiling**: nothing is lobbed over it (a Lob lock holds under
  it), and a pass drawn under it is reported.
- No rock is drawn in another cave's clear space: a tunnel's mouth opens in a cavern's wall, through a gallery's dead end or a
  cliff's face; overlap them and the way is clear. Moved one? **Rebuild All Pieces** (the others are cut again).
- Dress it with the props (§15): the rock formations, roots, mushrooms, the calcite druse, lanterns.
- No height mist fills a cave from the ground above it: under its footprint, the mist lies on the cave's own floor.

## 21. Cliffs

`GameObject > HotPatata Level > Rock > Cliff`: a block of rock over its outline (dots, or **Rectangle** / **Oval**), from the
piece's height up **Height** (the white handle on top). Each **Ledge** steps its face back by its inset at its height: a shelf one
walks on. Its top is walked on too; timber stands on it (a deck's posts find it).

- **Rock**: granite (grey, lichen), sandstone (layered) or mossy. **Batter** leans its faces back; **Sink** buries its foot;
  **Crest** raises its top edge in a ragged skyline (only ever higher, so a lock holds; keep 0 for a top that is walked on).
- For a natural face, draw an irregular outline (many dots), bury its back in a hill, and overlap two or three cliffs of different
  rocks and crests rather than one long block.
- The inspector lists its faces from the foot: a face under 3.4 m is climbed by a jump and a mantle. To stop a climb, keep it
  5 m and declare the Climb lock in the section's contract (§10).
- A spine between two lanes is a long thin cliff; a wall round a hollow is a ring of them.

## 22. Mine timber

- **Mine Frames** (`GameObject > HotPatata Level > Mine > Mine Frames`): frames along its line every **Spacing**, two posts on the
  ground (or rock) under each, a cap beam **Height** up, **Lagging** boards over the caps, a lantern under every few caps. Its
  posts stop players and the bomb. Put it inside a cave tunnel, its line along the tunnel's.
- **Mine Track** (`Mine > Mine Track`): sleepers and two rails on the ground along its line, some rotted (**Decay**). Decoration.
- Both freeze into elements (§17); the lanterns go with them.
- The library's `Structures/Mine/` holds an entrance, a 9 m gallery and an 8 m track.

## 23. Lane markers

Where a team splits onto two lanes (PROJECT_SPEC §15g), name each lane: `GameObject > HotPatata Level > Structure > Lane Marker`.
**Lane** Sun (gold) or Moon (blue), **Form** a post beside the path or an arch over it (**Span**), a **Lantern**. Its face is the
piece's forward (the arrow): turn it toward the team coming. The library's `Structures/Lanes/` holds the four of them.

## Rules

- Never edit a piece's `Generated` child: change the piece and it is rebuilt. A frozen structure's elements are yours to
  edit; never its `Walk surface` (it is laid from its boards).
- Do not scale pieces: draw their size with their points.
- The rules of AGENTS.md hold for authored levels as for the courses: collisions by layers, the kit's prefabs as they are,
  water over a kill zone, decoration out of the passes, a contract per section.
