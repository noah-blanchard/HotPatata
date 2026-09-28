# Real sound effects

Drop audio files here (.wav or .ogg). Until a slot is filled, the game uses the procedural placeholder from
`ProceduralSfx`, so nothing breaks while this folder is empty.

Only the bomb makes sounds. Movement is silent on purpose: there are no footsteps, landings, wind or slide scrape, so
do not add slots for them.

Where to plug each sound (drag the clip onto the field in the Inspector):

| Sound | Component | Field | Where it lives |
|---|---|---|---|
| Beeps (calm, medium, urgent, critical) | `BombAudio` | `Beeps` (4 entries) | `Assets/Prefabs/Bomb/Bomb.prefab` |
| Catch | `BombAudio` | `Catch Clip` | Bomb prefab |
| Throw | `BombAudio` | `Throw Clip` | Bomb prefab |
| Explosion | `BombAudio` | `Explosion Clip` | Bomb prefab |

Beep volume: `GameTuning.beepVolume` (0 = off). Import these short sounds with Load Type "Decompress On Load".
