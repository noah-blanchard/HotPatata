# UI icons

White line icons for the buttons and badges (ARCHITECTURE §6.2), drawn for HotPatata as plain SVG and imported by
Unity's built-in vector graphics module as UI Toolkit `VectorImage`s. `HotPatata.uss` puts them on `hp-icon--<name>`
and tints them (`-unity-background-image-tint-color`): white on buttons, navy with `hp-icon--ink`.

- Buttons and badges: `play`, `globe`, `join`, `local`, `gear`, `door`, `copy`, `back`, `reset`, `crown`, `star`, `info`.
- Slot shapes (filled, tinted by `UIParts.Chip`): `slot-circle`, `slot-triangle`, `slot-square`, `slot-diamond`.
- `potato.svg`: the coloured potato of the working spinner and the waiting notes.
