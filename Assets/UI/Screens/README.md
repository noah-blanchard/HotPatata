# UI screens

The UXML layout of each UI Toolkit screen, one file per screen, edited in UI Builder (canvas theme
`HotPatataTheme`, canvas 1920x1080). The convention is in ARCHITECTURE §6.2.

| File | Screen (`Assets/Scripts/UI/`) |
|---|---|
| `MainMenu.uxml` | `MainMenuScreen` (`MenuView.cs`) |
| `Working.uxml` | `WorkingScreen` (`MenuView.cs`) |
| `Lobby.uxml` | `LobbyScreen` (`MenuView.cs`) |
| `Pause.uxml` | `PauseScreen` (`PauseMenu.cs`) |
| `Confirm.uxml` | `ConfirmScreen` |
| `Settings.uxml` | `SettingsScreen` |
| `UISample.uxml` | `UISampleScreen` (dev only, `Assets/Scripts/DebugTools/`) |

- **Styling.** Use only the shared `hp-*` classes of `Assets/UI/Styles/HotPatata.uss`. Do not use inline styles.
- **Names.** Elements the code needs have a kebab-case `name`. Renaming or deleting one breaks its screen, with a
  clear error.
- **Free to change.** Order, nesting, static text and classes can change freely.
- **A new screen.** Add its field to `UIScreenCatalog` and assign the file in `Assets/UI/Resources/HotPatataScreens.asset`.
