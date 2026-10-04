# UI fonts

The UI fonts (ARCHITECTURE §6.2), both under the SIL Open Font License (the `*-OFL.txt` files next to them), free to
ship in the game:

| File | Font | Use |
|---|---|---|
| `LilitaOne-Regular.ttf` | [Lilita One](https://fonts.google.com/specimen/Lilita+One) | titles, buttons, values (`LilitaOne-SDF`) |
| `Nunito-Regular/Bold/Black.ttf` | [Nunito](https://fonts.google.com/specimen/Nunito) | body text (`Nunito-SDF`, `Nunito-Bold-SDF`, the default, `Nunito-Black-SDF`) |

Nunito ships as a variable font; these are static instances cut from it at weights 400, 700 and 900 (fontTools
`instancer`), because the text engine picks a weight by font asset. The `*-SDF.asset` files are dynamic TextCore font
assets, pre-filled with printable ASCII and Latin-1 so they rarely change when the game runs; anything else a player
types is added on demand. `Fallback-SDF` (Unity's built-in font) is every font's fallback, for symbols.

`HotPatata.uss` sets them with `-unity-font-definition` (`:root` uses Nunito Bold).
