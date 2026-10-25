# SailwindCantingFoils

A BepInEx 5 visual prototype for Sailwind. The previous target, `StartMenu.decoAlAnkh`, was confirmed by the runtime log to be an inactive Al'Ankh scenery group containing an obelisk, palms, shrubs, and menu text—not the dhow. This build instead searches the active main-menu scene for Sailwind's `BoatRefs` component, resolves its `boatModel` transform, and creates two large visibility cubes outside the model's renderer bounds. It does not change the camera, add physics, or modify the original boat model.

Menu discovery lives in `MenuPreviewDhowDiscovery.cs`. It gates on Sailwind's `StartMenu.menuEnabled`, inventories loaded-scene `BoatRefs` objects, and recognizes the dhow family from the runtime hierarchy labels (including every size variant). It measures the union of active renderers included by the menu camera's layer mask, tests that bounds against the camera frustum, and selects a dhow only when exactly one active dhow candidate in the menu scene intersects that frustum. This corrects the earlier layer-only test, which incorrectly counted off-screen ambient boats. Candidate hierarchy paths, parent paths, active state, renderer bounds, camera distance, and frustum intersection are logged when the candidate situation changes. If multiple dhow variants are in view, it does not guess; it logs once for that unchanged ambiguity and continues checking. After successful creation it stops retrying. The geometry builder accepts any model `Transform`, so it can be reused for other ships later.

## Prerequisites

- Windows and a Sailwind installation using Unity Mono.
- BepInEx 5 installed in that Sailwind folder. BepInEx 5.4.23.5 was present when this project was prepared.
- The game assemblies from that installation: `Sailwind_Data\Managed\UnityEngine.dll` and `Assembly-CSharp.dll`, plus `BepInEx\core\BepInEx.dll`.
- Unity module assemblies `UnityEngine.CoreModule.dll` and `UnityEngine.PhysicsModule.dll` from the same Managed folder.
- .NET SDK. The checked Sailwind install reports Unity 2019.1 and includes `Sailwind_Data\Managed\netstandard.dll`; BepInEx's target-framework guidance selects `netstandard2.0` for games on Unity 2021.1 or older when this assembly is present. The project therefore targets `netstandard2.0`. The .NET 10 SDK is the build tool; it does not change the plugin's target framework.

The game and BepInEx assemblies are referenced in place and are not copied into this repository or output. Do not commit those files.
The project also references Sailwind's `UnityEngine.CoreModule.dll`, which contains the `MonoBehaviour` type inherited by BepInEx's plugin base class.

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
4. Start Sailwind through Steam and leave it at the main menu long enough to inspect the cubes. Exit when you are done.
5. Open `BepInEx\LogOutput.log` in the game folder. Search for `Found main-menu dhow BoatRefs`, `Created magenta and yellow visibility cubes`, and the two `VisibilityCube_... diagnostics` lines. Confirm the hierarchy is the visible menu dhow; each diagnostic reports world/local position, local scale, active state, layer, camera layer-mask inclusion, and renderer enabled state. Confirm both cubes appear beside the dhow and follow its transform.

For a later rebuild, close the game, build again, and copy the new DLL over the manually installed one. This project never copies files into the game installation automatically.

## Troubleshooting

- **`BepInEx.dll` or game assembly not found:** confirm `$SailwindDir` points to the folder containing `Sailwind.exe`, and that BepInEx 5 is installed there.
- **NuGet restore fails:** the .NET SDK needs the `NETStandard.Library` reference package for `netstandard2.0`. Check network access to NuGet, or restore the project on a machine with that package available before building offline.
- **Plugin is not listed in the log:** verify the DLL is inside `BepInEx\plugins\SailwindCantingFoils`, start the BepInEx-enabled Sailwind executable, and check that the install uses BepInEx 5 rather than another loader.
- **No suitable dhow candidate appears:** ensure the main menu is open and check `BepInEx\LogOutput.log`. The plugin only selects a dhow whose active renderer bounds intersect the menu camera frustum. If none intersect, the log says so; if more than one intersects, send the `Main-menu BoatRefs candidate diagnostics` block so the target can be distinguished without guessing.
- **The cubes are misplaced or too small:** adjust `CubeSize` and `SideClearance` in `FoilSystem.cs`. Their initial positions are computed from the model's active renderer bounds along its local right axis.
- **Plugin load error:** inspect the first relevant exception in `BepInEx\LogOutput.log`; ensure the project was built against the same Sailwind install used for testing.

## References

- [BepInEx 5 plugin tutorial](https://docs.bepinex.dev/v5.4.11/articles/dev_guide/plugin_tutorial/index.html) — plugin classes derive from `BaseUnityPlugin`, use `BepInPlugin`, and log through BepInEx.
- [BepInEx target framework guidance](https://docs.bepinex.dev/v5.4.11/articles/dev_guide/plugin_tutorial/1_setup.html) — select a framework based on the game's managed runtime assemblies.
- [Sailwind modding notes](https://github.com/ai-pop/sailwind-modding-notes) — community technical reference based on Sailwind assemblies and runtime analysis.
