# SailwindCantingFoils

A minimal BepInEx 5 plugin foundation for Sailwind. Phase 1 only writes a startup message to the BepInEx log; it does not add foil parts, change boat physics, or install files into the game.

## Prerequisites

- Windows and a Sailwind installation using Unity Mono.
- BepInEx 5 installed in that Sailwind folder. BepInEx 5.4.23.5 was present when this project was prepared.
- The game assemblies from that installation: `Sailwind_Data\Managed\UnityEngine.dll` and `Assembly-CSharp.dll`, plus `BepInEx\core\BepInEx.dll`.
- .NET SDK with .NET Framework 4.6 targeting/reference assemblies available. The checked Sailwind install reports Unity 2019.1.10 and CLR 4, contains `mscorlib.dll`, and does not contain `netstandard.dll`; this project therefore targets `net46`, following BepInEx's target-framework guidance for this type of Unity Mono game.

The game and BepInEx assemblies are referenced in place and are not copied into this repository or output. Do not commit those files.

## Build

Open PowerShell in the repository root (the folder containing this README) and run:

```powershell
$SailwindDir = "C:\Program Files (x86)\Steam\steamapps\common\Sailwind"
dotnet build .\SailwindCantingFoils\SailwindCantingFoils.csproj -c Release "-p:SailwindDir=$SailwindDir"
```

If Sailwind is installed elsewhere, change only the value of `$SailwindDir` to the folder containing `Sailwind.exe`. The compiled plugin is written to:

```text
build\manual-test\SailwindCantingFoils.dll
```

This is a separate build output folder, not the game plugins folder.

## Manual test with BepInEx 5

1. Build the DLL using the command above.
2. In your Sailwind installation, open `BepInEx\plugins`. Create a folder named `SailwindCantingFoils` if it does not exist.
3. Copy `build\manual-test\SailwindCantingFoils.dll` into that folder.
4. Start Sailwind through Steam and then exit the game.
5. Open `BepInEx\LogOutput.log` in the game folder and search for `SailwindCantingFoils loaded successfully`.

For a later rebuild, close the game, build again, and copy the new DLL over the manually installed one. This project never copies files into the game installation automatically.

## Troubleshooting

- **`BepInEx.dll` or game assembly not found:** confirm `$SailwindDir` points to the folder containing `Sailwind.exe`, and that BepInEx 5 is installed there.
- **Missing .NET Framework reference assemblies during build:** install the .NET Framework 4.6 developer/targeting pack or use a Visual Studio installation that includes .NET Framework targeting packs. The .NET 10 runtime alone is not the project's target framework.
- **Plugin is not listed in the log:** verify the DLL is inside `BepInEx\plugins\SailwindCantingFoils`, start the BepInEx-enabled Sailwind executable, and check that the install uses BepInEx 5 rather than another loader.
- **Plugin load error:** inspect the first relevant exception in `BepInEx\LogOutput.log`; ensure the project was built against the same Sailwind install used for testing.

## References

- [BepInEx 5 plugin tutorial](https://docs.bepinex.dev/v5.4.11/articles/dev_guide/plugin_tutorial/index.html) — plugin classes derive from `BaseUnityPlugin`, use `BepInPlugin`, and log through BepInEx.
- [BepInEx target framework guidance](https://docs.bepinex.dev/v5.4.11/articles/dev_guide/plugin_tutorial/1_setup.html) — select a framework based on the game's managed runtime assemblies.
- [Sailwind modding notes](https://github.com/ai-pop/sailwind-modding-notes) — community technical reference based on Sailwind assemblies and runtime analysis. This foundation does not call game-specific APIs.
