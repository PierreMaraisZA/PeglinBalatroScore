# Building PeglinBalatroScore

## Requirements

- Windows with **Peglin** installed (the build references the game's own DLLs, which can't be shared in this repo).
- **BepInEx 5** installed in the game folder ([BepInExPack_Peglin](https://thunderstore.io/c/peglin/p/BepInEx/BepInExPack_Peglin/)).
- **.NET 8 SDK** (`winget install Microsoft.DotNet.SDK.8`).

The project expects the game at the default Steam location,
`C:\Program Files (x86)\Steam\steamapps\common\Peglin`. If yours is elsewhere, add `-p:GamePath="D:\Games\Peglin"`
to the build commands (or `-GamePath` to `pack.ps1`).

## Build

```
dotnet build PeglinBalatroScore.csproj -c Release
```

The build copies the DLL into the game's `BepInEx\plugins\PeglinBalatroScore\` folder. Restart Peglin after building.

## Tests

```
dotnet test tests/PeglinBalatroScore.Tests.csproj
```

The score rules, flame curve, number formatting, boss HP rules, tooltip text, fire simulation, bomb-chest timeline
and bomb physics live in `src/Core` with no Unity references, so they can be tested without the game.

## Project layout

| Path | What's there |
|---|---|
| `src/Core/` | Pure score, flame, boss HP, fire, bomb-chest and bomb-physics rules, unit tested |
| `src/Plugin.cs` | Entry point and all config settings |
| `src/ScoreHud.cs` | The widget: reads each shot from the game, places it, animates it, moves the boss bar and scroll |
| `src/ScoreBox.cs` | One number box with its flame |
| `src/PixelFlame.cs` | Draws the fire simulation into a pixel-art texture that grows as needed |
| `src/OverkillStamp.cs` | The OVERKILL / OVERHEAL stamp |
| `src/ScoreTooltip.cs` | The hover tooltip |
| `src/ShotDamage.cs` | Reads the numbers `Attack.GetDamage` uses from the battle |
| `src/BossTarget.cs` | Finds the act boss's max HP from the map |
| `src/ScreenShift.cs` | Moves a game object down the screen by a number of pixels |
| `src/BombChestHud.cs` | The bomb chest: catches each lit bomb, bursts with the last one and shows the total |
| `src/ChestArt.cs` | Draws the chest, planks and splinters as pixel-art textures |
| `src/DamagePop.cs` | The bomb total that pops out of the wreckage and shatters |
| `src/DevTools.cs` | Dev hotkeys (Debug builds only) |
| `tests/` | xUnit tests for `src/Core` |
| `media/` | GIFs used on GitHub, Thunderstore and Steam |
| `tools/make-icon.ps1` | Draws `icon.png` |
| `thunderstore/manifest.json` | Thunderstore package metadata |
| `pack.ps1` | Builds the Thunderstore zip |

## Releasing a new version

1. Bump the version in `src/Plugin.cs` (`Version`), `PeglinBalatroScore.csproj` (`<Version>`) and
   `thunderstore/manifest.json` (`version_number`).
2. Add the changes to `CHANGELOG.md` under a `## <version>` heading (`pack.ps1` checks for it).
3. Run `powershell -ExecutionPolicy Bypass -File pack.ps1` and upload `dist\PierreMarais-PeglinBalatroScore-<version>.zip`.
