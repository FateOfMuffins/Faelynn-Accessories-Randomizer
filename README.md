# Faelynn Accessories Randomizer

A Windows overlay for Girls' Frontline 2: Exilium. It makes one roll per game launch. It does not change your in-game outfit.

## Install

1. Download **Faelynn-Accessories-Randomizer-Setup.exe** from [Releases](https://github.com/FateOfMuffins/Faelynn-Accessories-Randomizer/releases/latest). You do not need the source ZIP.
2. Open the setup app. Leave the delay at **30 seconds** or enter a different value.
3. Click **Install**. Leave **Start at Windows sign-in** checked to keep it running automatically.

The installer is unsigned. It runs on Windows 10/11 with .NET Framework 4.8 or later, and does not ask for administrator access.

## Change settings or uninstall

Open **Faelynn Accessories Randomizer** from the Windows Start menu. Set the delay (1-600 seconds) and click **Save settings**. The new value applies to the next GF2 launch. Clear **Start at Windows sign-in** if you only want it to run for your current Windows session.

Click **Uninstall** in the same window to stop the watcher and remove its files and shortcuts.

### Migration from the original PowerShell script

If you used the early script version, run its launcher with the `-Stop` option and remove only the old **GF2 Overlay Watcher** shortcut from the current user's Startup folder (`shell:startup` in the Run dialog). Then use this setup app. It will not install a second watcher while the old one is enabled.

## What happens in game

- The delay starts when `GF2_Exilium.exe` launches, not when the launcher opens. If you switch away, the result waits for you to return to the game.
- Rolls **1-27** select a column in each of the three accessory rows. The last row changes fastest: `1 -> (1,1,1)`, `4 -> (1,2,1)`, `27 -> (3,3,3)`.
- Roll **28** shows the transparent splash art instead.
- Click the artwork or an orange highlight to dismiss it. The overlay is designed not to take focus from the game. Transparent gaps are not click targets.

If you start the watcher after the game's configured delay has already elapsed, it waits for the next launch.

## Source and build

The app is C# and Windows Forms, with no NuGet packages. The two PNGs are embedded in the executable.

| File | Purpose |
| --- | --- |
| `Program.cs`, `SetupForm.cs` | Entry point and settings window |
| `Installation.cs` | Per-user files, settings, Start menu and Startup shortcuts |
| `Watcher.cs` | Process detection, per-launch timer, and random roll |
| `Artwork.cs` | Positions and highlights for the nine accessories |
| `Overlay.cs`, `Native.cs` | Transparent, non-activating overlay and Windows API calls |

Run from the repository directory with Windows PowerShell:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Test
```

The build places the installer in `dist`. Tests cover the 27 grid mappings, timer rules, artwork rendering, and installation lifecycle using a scratch directory. To preview a build without installing it, launch it with `--preview 4` (accessories) or `--preview 28` (splash), then return to GF2. These previews time out after 18 seconds.

The install directory is `%LOCALAPPDATA%\FaelynnAccessoriesRandomizer`. The watcher reads `delay-seconds.txt` when it first detects each launch. Missing or invalid values fall back to 30 seconds. It records errors locally in `errors.log`; it sends no telemetry and does not inject code or input into the game.

This is an unofficial fan tool. The artwork is based on imagery from Girls' Frontline 2: Exilium; the game and its imagery belong to their respective rights holders.
