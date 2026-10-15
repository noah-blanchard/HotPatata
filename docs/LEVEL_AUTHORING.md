# HotPatata — LEVEL_AUTHORING.md

How to make a level by hand in the Unity Editor: no code, no builder. The design behind the tools is ARCHITECTURE §27.

The tools come in three steps (M18). This guide covers what is built:

| Step | What you get | State |
|---|---|---|
| 1. The level and its timber | a new level scene, decks, walkways and stairs, rope bridges, railings, houses, trestle towers; the menu; the day | built |
| 2. The nature | a sculpted terrain with the nature look, valley walls, water, giant trees, forest, mist, sound | next |
| 3. The game | forest looks for the kit, size handles, wiring, contracts, numbered checkpoints, Verify Level, the route runner, Play from here, props | after |

Until step 3, place the kit (`Assets/Prefabs/Gameplay`, `Obstacles`, `Platforms`) and the checkpoints by hand, as in
`docs/OBSTACLES.md`, and check your level with `HotPatata/Course/Check Section Contracts`.

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

You can press Play at once: the players start on a flat forest floor. Everything you add goes under
`SectionRoot/Level` (its groups: `Timber`, `Trees`, `Kit`, `Props`).

The level's settings (menu name, players, day) are on the `Level` object (`LevelSettings`). After changing the day:
`HotPatata/Level/Apply Look`.

## 2. Add timber

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
Braces, Hangers or None). Hangers need something above to tie to (a giant tree's limb, step 2).

## 3. Shape it in the Scene view

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

## 4. Join pieces

- **Select an end dot** (the first or last point of a walkway, either end of a bridge) and **drag it onto a deck's edge or
  onto another walkway's or bridge's end**: when it is close enough on screen a green ring shows; let go and it is
  attached (the label and the Inspector say to what). On a deck the railing opens there. The end follows what it is
  attached to when that moves. **Hold Shift** while dragging to place an end freely.
- **Attach ends** in a walkway's or bridge's Inspector attaches each free end to the nearest deck edge or end within 4 m.
  **Detach start / end** frees it.
- **A deck's own openings** (Add opening in its Inspector) are cyan dots on its edge: slide them where a gap should be.
- A bridge shows its length and the slope at its ends; keep it at 18° or less.

## 5. Presets

**Save As Preset** in a piece's Inspector saves its settings (width, rails, supports, wear...), never its shape. Use it
again with **Apply Preset**, or drag the preset from the Project window into the Scene view: a new piece of that kind
appears there with those settings.

## 6. Save

Saving the scene (Ctrl+S) writes the pieces' meshes to `Assets/Art/Generated/<level>/Pieces/` (one folder per piece;
removed pieces' folders are deleted) and gathers the level's walk lines. The Console then says how many pieces and meshes,
and lists anything a piece cannot be built as drawn (a hanger with nothing above, a scaled piece...).

If you reshape the ground under pieces: `HotPatata/Level/Rebuild All Pieces`.

## 7. Put it in the menu

`HotPatata/Level/Register In Menu...` with your level open:

- its **name in the menu** and the **players needed**;
- the list of the menu's levels: **drag yours** where it goes (the others keep their order);
- **Save to the menu**. The level is added to the build and shows in Bootstrap's level list, with a spawn choice per
  checkpoint. **Remove from the menu** takes it out.

A level is official (shipped in the menu) only once it passes Verify Level (step 3) with nothing reported.

## Rules

- Never edit a piece's `Generated` child: change the piece and it is rebuilt.
- Do not scale pieces: draw their size with their points.
- The rules of AGENTS.md hold for authored levels as for the courses: collisions by layers, the kit's prefabs as they are,
  water over a kill zone, decoration out of the passes, a contract per section.
