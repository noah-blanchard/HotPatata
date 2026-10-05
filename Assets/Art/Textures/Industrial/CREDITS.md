# Industrial textures: sources and licences

Every texture set dropped in this folder (one sub-folder per material: `Concrete`, `PaintedMetal`, `RawMetal`, `Rubber`)
must be listed here with its source, licence and date before it is committed (issue #87). How to import and assign them:
`docs/ARCHITECTURE.md` §25.2 (menu **HotPatata/Course/Assign Industrial Textures**).

All four sets come from [ambientCG](https://ambientcg.com), released under the
[Creative Commons CC0 1.0 Universal](https://creativecommons.org/publicdomain/zero/1.0/) licence (no attribution required;
credit is welcome). Licence and set names as shown on each asset page at download time.

| Material | Set | Source | Licence | Maps used |
|---|---|---|---|---|
| Concrete | Concrete031 (2K PNG) | https://ambientcg.com/view?id=Concrete031 | CC0 1.0 | Color, NormalGL, Roughness, AmbientOcclusion |
| PaintedMetal | PaintedMetal004 (2K PNG) | https://ambientcg.com/view?id=PaintedMetal004 | CC0 1.0 | Color (greyed, recoloured by the tint), NormalGL, Roughness, Metalness |
| RawMetal | Metal046B (2K JPG) | https://ambientcg.com/view?id=Metal046B | CC0 1.0 | Color, NormalGL, Roughness, Metalness |
| Rubber | Rubber004 (2K PNG) | https://ambientcg.com/view?id=Rubber004 | CC0 1.0 | Color, NormalGL, Roughness |

Unused files of the zips (DirectX normals, displacement, opacity, previews) are kept next to the maps; the Blender, USD,
MaterialX and Godot files are git-ignored.
