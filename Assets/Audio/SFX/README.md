# Real sound effects

Drop audio files here (.wav or .ogg). Until a slot is filled, the game uses the procedural
placeholder from `ProceduralSfx`, so nothing breaks while this folder is empty.

Where to plug each sound (drag the clip onto the field in the Inspector):

| Sound | Component | Field | Where it lives |
|---|---|---|---|
| Beeps (calm, medium, urgent, critical) | `BombAudio` | `Beeps` (4 entries) | `Assets/Prefabs/Bomb/Bomb.prefab` |
| Catch | `BombAudio` | `Catch Clip` | Bomb prefab |
| Throw | `BombAudio` | `Throw Clip` | Bomb prefab |
| Explosion | `BombAudio` | `Explosion Clip` | Bomb prefab |
| Wind loop (must loop cleanly) | `SpeedEffects` | `Wind Clip` | the camera in each gameplay scene |
| Footsteps (several variations) | `SpeedEffects` | `Step Clips` | the camera in each gameplay scene |
| Landing | `SpeedEffects` | `Land Clip` | the camera in each gameplay scene |

Volumes: `GameTuning` (`beepVolume`, `windVolume`, `footstepVolume`; 0 = off).
Short sounds: import with Load Type "Decompress On Load"; the wind loop: "Compressed In Memory".
