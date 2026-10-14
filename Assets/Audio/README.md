# Sounds and music: drop files, they play

Every sound in HotPatata is a **folder**. Put `.wav`, `.ogg` or `.mp3` files in a folder and the game plays them. You don't
need to drag anything in the Inspector. How it works is in `docs/ARCHITECTURE.md` §26.

- **More files in a folder means more variety.** Each play picks one at random, never the same twice in a row, with a
  little volume and pitch variation. Ten footsteps sound better than one.
- **An empty folder is silent.** The bomb is the exception: it falls back to its generated placeholder beeps. The builders'
  river, waterfall, campfire, wind, birds and crickets fall back to their generated loops.
- **First time:** run `HotPatata > Audio > Create Folder Skeleton`. It creates every folder the game listens to, so you can see
  every slot. Running it again is safe.
- The sound bank (`Resources/HotPatataSoundBank`) **rebuilds itself** a moment after you add, move or delete a file. If you
  ever need to force it, use `HotPatata > Audio > Rebuild Sound Bank`.
- **Licences:** add every file you did not make to `CREDITS.md` (source and licence).

## Music: `Music/`

| Folder | Plays |
|---|---|
| `Menu/` | the main menu |
| `Lobby/` | an online lobby. It crossfades in when you host or join, and back to the menu music when you leave. Empty = the menu music continues |
| `Courses/<Level>/` | a level, for every act that has no folder of its own (`<Level>` = scene name: `PatataWilds`, `PatataCanopy`, `PatataTemple`, `IndustrialPlant`, `PassSandbox`) |
| `Courses/<Level>/Act1/` … `Act5/` | that act only. It crossfades when the team reaches the act's first checkpoint |
| `Results/` | the results screen once the run is complete. Empty = the level's music continues |

- **Several tracks in one folder** form a shuffled playlist. Each crossfades into the next, never the same track twice in a row.
- **Intro + loop:** name them `Theme_intro.ogg` and `Theme_loop.ogg`. The intro plays once and the loop takes over
  sample-exact, then loops forever. A track without `_intro` simply loops (or moves on in a playlist).
- While a level loads, the music fades to silence, and the level's music starts once the run is ready.
- The pause menu muffles the music. Each beep of the bomb dips it a little, so the fuse is always heard.
- Export music as `.ogg` (or `.wav`). It is streamed from disk, so long tracks cost no memory.

## Ambience: `Ambience/`

Ambience is built in **layers**. Each layer has two folders:

- `Beds/`: **loops**, all played together, flat (wind bed, forest tone, factory hum). Make them seamless loops.
- `Scatter/`: **one-shots** (a bird call, a branch cracking, a distant drip). They play now and then at random spots around
  the player. Drop as many as you like. How often and how far is set per folder in the sound bank (see Tuning below).

| Folder | Plays |
|---|---|
| `Menu/` | the main menu and the lobby (the misty clearing) |
| `Courses/<Level>/` | everywhere in that level |
| `Environments/<Env>/` | where the team is. The level builders name each section's environment: **PatataWilds** `Forest`, `River`, `Cliffs`, `Cave`, `Summit`; **PatataCanopy** `Canopy`, `HollowOak` (act 3); **PatataTemple** `Jungle` (act 1), `Temple` (act 2), `Storm` (act 3); **IndustrialPlant** `Factory`. `Meadow` is free for a new section |
| `Courses/<Level>/Act<N>/` | added on top during that act (crickets at dusk) |
| `Courses/<Level>/Act<N>/Replace/` | replaces the environment's layer during that act |
| `Emitters/<Kind>/` | loops fixed in the world by the builders: `River`, `Waterfall`, `Fire` (every campfire), `Wind`, `Birds`, `Crickets` |

Layers fade in and out as the team moves on.

## Sound effects: `SFX/`

| Folder | When |
|---|---|
| `Bomb/Beep/Calm`, `Medium`, `Urgent`, `Critical` | the fuse beep, by stage. **Gameplay-critical**: make the four stages clearly different |
| `Bomb/Throw`, `Catch`, `Explosion` | a throw, a catch, the explosion |
| `Bomb/FuseLoop` | a hiss looping while the fuse burns (at the fuse warning volume) |
| `Bomb/TransitIn`, `TransitWarn` | swallowed by a tube or cannon; the warning at the exit just before it fires |
| `Player/Footsteps/<Surface>` | each step. Surfaces: `Default`, `Grass`, `Dirt`, `Gravel`, `Mud`, `Leaves`, `Rock`, `Stone`, `Wood`, `Metal`, `Concrete`, `Water`. An empty surface uses `Default` |
| `Player/Jump`, `Land/Soft`, `Land/Hard` | jump; landing (hard from a big fall) |
| `Player/Slide/Start`, `Slide/Loop` | a slide starting; its scrape, fading as it slows |
| `Course/Start`, `Checkpoint`, `ActChange`, `Reset`, `Finish` | the run's moments (an act's first checkpoint plays `ActChange` if it has a sound, else `Checkpoint`) |
| `Obstacles/<Type>/On`, `Off`, `Loop` | an obstacle system switching on or off, and a loop while it moves. `<Type>` is the component: `PressurePlate`, `SignalActuator` (doors, lifts), `MovingPlatform`, `RotatingObstacle`, `FallingPlatform`, `BombGate`, `SunBeam`, `BodyScreen`, `SignalSwitch` |
| `UI/Hover`, `Click`, `Back`, `Slider`, `Open`, `LobbyJoin`, `LobbyLeave` | menus |

Your own movement sounds are flat and a bit quieter. Teammates' play in 3D at their feet, so you can hear where they are.
Short effects are imported mono (they are placed in the world). Keep them tight: no silence before the sound starts.

## Which surface is under my feet?

The bank maps every material to a surface from its name (`Nature_Planks` is Wood, `Nature_RockPath` is Rock...). To fix one,
open the bank, find the material under **Surfaces** and change its surface; your fix is kept on every rebuild. For a single
object, add an `AudioSurface` component to its collider.

## Tuning

Select `Resources/HotPatataSoundBank` to see every folder holding sounds. Each one has its own **volume**, **volume and pitch
jitter**, **cooldown**, **max voices**, **3D distances** and, for `Scatter`, **interval / distance / height** ranges. Your changes
are kept on every rebuild. Game-wide timings (crossfades, the beep dip, the hard landing speed, step length) are in `GameTuning`
under **Audio**. Players set Master / Effects / Music / Ambience / Menu volumes in the settings.
