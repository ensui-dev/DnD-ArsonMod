# Arson Mode - Build & Release Guide

A step-by-step guide for compiling ArsonMod from source and preparing releases.

---

## Prerequisites

- **.NET SDK 6.0+** — Download from https://dotnet.microsoft.com/download
- **Dale & Dawson** installed via Steam
- **MelonLoader** installed on your game copy (see INSTALL.md)
- **The game must have been launched at least once with MelonLoader** so that the Il2Cpp interop assemblies are generated

---

## Step 1: Locate Your Game Assemblies

After launching the game with MelonLoader once, you'll have the assemblies you need in two places inside your game folder:

```
Dale & Dawson/
├── MelonLoader/
│   ├── net6/
│   │   ├── MelonLoader.dll              ← MelonLoader core
│   │   ├── Il2CppInterop.Runtime.dll    ← Il2Cpp interop runtime
│   │   └── ...
│   └── Il2CppAssemblies/
│       ├── Assembly-CSharp.dll          ← Game code (contains GameManager, LobbyPlayer, etc.)
│       ├── UnityEngine.CoreModule.dll
│       ├── UnityEngine.PhysicsModule.dll
│       ├── UnityEngine.UI.dll
│       ├── Unity.TextMeshPro.dll
│       └── ...                          ← All Il2Cpp interop DLLs
```

**Copy the path to your game folder** — you'll need it for the project file. For example:
- `C:\Program Files (x86)\Steam\steamapps\common\Dale and Dawson\`

To find it: Steam → Library → right-click Dale & Dawson → Manage → Browse local files.

---

## Step 2: Create the Project File

Create a file called `ArsonMod.csproj` in the **project root** (the `dnd/` folder, alongside the `ArsonMod/` and `dd/` directories).

**Replace `GAME_FOLDER_PATH` with your actual game folder path** (the folder containing `DaleAndDawson.exe`).

> **Important:** The `.csproj` must be in the root directory, NOT inside `ArsonMod/`. Since the decompiled game code lives in `dd/` next to it, we explicitly tell the build to only compile files from `ArsonMod/` and ignore everything else.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <AssemblyName>ArsonMod</AssemblyName>
    <RootNamespace>ArsonMod</RootNamespace>
    <LangVersion>latest</LangVersion>

    <!-- Suppress warnings about Il2Cpp reference assemblies -->
    <NoWarn>$(NoWarn);CS0436</NoWarn>

    <!-- Do not copy dependencies to output — MelonLoader provides them at runtime -->
    <CopyLocalLockFileAssemblies>false</CopyLocalLockFileAssemblies>

    <!-- SET THIS to your Dale & Dawson game folder -->
    <!-- Windows example: C:\Program Files (x86)\Steam\steamapps\common\Dale and Dawson -->
    <!-- Linux/WSL example: /mnt/c/Program Files (x86)/Steam/steamapps/common/Dale and Dawson -->
    <GameDir>GAME_FOLDER_PATH</GameDir>
    <MLDir>$(GameDir)/MelonLoader</MLDir>
    <Il2CppDir>$(MLDir)/Il2CppAssemblies</Il2CppDir>
    <MLNetDir>$(MLDir)/net6</MLNetDir>

    <!-- Only compile ArsonMod source files, not the decompiled game code in dd/ -->
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>

  <!-- Explicitly include only ArsonMod source files -->
  <ItemGroup>
    <Compile Include="ArsonMod/**/*.cs" />
  </ItemGroup>

  <!-- MelonLoader Core -->
  <ItemGroup>
    <Reference Include="MelonLoader">
      <HintPath>$(MLNetDir)/MelonLoader.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Il2CppInterop.Runtime">
      <HintPath>$(MLNetDir)/Il2CppInterop.Runtime.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="0Harmony">
      <HintPath>$(MLNetDir)/0Harmony.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>

  <!-- Unity Engine Assemblies -->
  <ItemGroup>
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>$(Il2CppDir)/UnityEngine.CoreModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.PhysicsModule">
      <HintPath>$(Il2CppDir)/UnityEngine.PhysicsModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.UI">
      <HintPath>$(Il2CppDir)/UnityEngine.UI.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Unity.TextMeshPro">
      <HintPath>$(Il2CppDir)/Unity.TextMeshPro.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.AudioModule">
      <HintPath>$(Il2CppDir)/UnityEngine.AudioModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.ParticleSystemModule">
      <HintPath>$(Il2CppDir)/UnityEngine.ParticleSystemModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.TextRenderingModule">
      <HintPath>$(Il2CppDir)/UnityEngine.TextRenderingModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.InputLegacyModule">
      <HintPath>$(Il2CppDir)/UnityEngine.InputLegacyModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.UIModule">
      <HintPath>$(Il2CppDir)/UnityEngine.UIModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>

  <!-- Game Il2Cpp Interop Assemblies -->
  <ItemGroup>
    <Reference Include="Assembly-CSharp">
      <HintPath>$(Il2CppDir)/Assembly-CSharp.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Il2Cppmscorlib">
      <HintPath>$(Il2CppDir)/Il2Cppmscorlib.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Il2CppMirror">
      <HintPath>$(Il2CppDir)/Il2CppMirror.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>

</Project>
```

### Notes on the .csproj

- **`<EnableDefaultCompileItems>false</EnableDefaultCompileItems>`** is critical — without it, the .NET SDK will try to compile every `.cs` file it can find, including the decompiled game code in `dd/`. The explicit `<Compile Include="ArsonMod/**/*.cs" />` ensures only your mod code is built.
- **`<Private>false</Private>`** on every reference prevents the build from copying game/MelonLoader DLLs into your output. You only want `ArsonMod.dll` in the output.
- The exact DLL filenames in `Il2CppAssemblies/` may vary slightly depending on the game version. If you get a "reference not found" error during build, check the folder for the correct filename.
- If your `Il2CppAssemblies` folder has different names for the game assemblies (e.g., `Assembly-CSharp-firstpass.dll`), add those as additional references.

---

## Step 3: Verify Assembly References

Before building, confirm that all the referenced DLLs exist. Open a terminal and check:

**Windows (PowerShell):**
```powershell
$gameDir = "C:\Program Files (x86)\Steam\steamapps\common\Dale and Dawson"

# Check MelonLoader core
Test-Path "$gameDir\MelonLoader\net6\MelonLoader.dll"
Test-Path "$gameDir\MelonLoader\net6\Il2CppInterop.Runtime.dll"
Test-Path "$gameDir\MelonLoader\net6\0Harmony.dll"

# Check Il2Cpp assemblies
ls "$gameDir\MelonLoader\Il2CppAssemblies\Assembly-CSharp*"
ls "$gameDir\MelonLoader\Il2CppAssemblies\UnityEngine.CoreModule*"
```

**Linux / WSL:**
```bash
GAME_DIR="/mnt/c/Program Files (x86)/Steam/steamapps/common/Dale and Dawson"

# Check MelonLoader core
ls "$GAME_DIR/MelonLoader/net6/MelonLoader.dll"
ls "$GAME_DIR/MelonLoader/net6/Il2CppInterop.Runtime.dll"
ls "$GAME_DIR/MelonLoader/net6/0Harmony.dll"

# Check Il2Cpp assemblies
ls "$GAME_DIR/MelonLoader/Il2CppAssemblies/Assembly-CSharp"*
ls "$GAME_DIR/MelonLoader/Il2CppAssemblies/UnityEngine.CoreModule"*
```

If any files are missing, make sure you've launched the game with MelonLoader at least once.

### Handling Missing or Differently Named Assemblies

The `using` statements in the mod reference these Il2Cpp namespaces:

| Namespace | Expected Source DLL |
|-----------|-------------------|
| `Il2CppGameManagement` | `Assembly-CSharp.dll` |
| `Il2CppGameManagement.StateMachine` | `Assembly-CSharp.dll` |
| `Il2CppPlayer.Lobby` | `Assembly-CSharp.dll` |
| `Il2CppPlayer.Scripts.StateMachineLogic` | `Assembly-CSharp.dll` |
| `Il2CppProps.FireAlarm` | `Assembly-CSharp.dll` |
| `Il2CppProps.FireEx` | `Assembly-CSharp.dll` |
| `Il2CppProps.TrashBin` | `Assembly-CSharp.dll` |
| `Il2CppRoom` | `Assembly-CSharp.dll` |
| `Il2CppUI.Tabs.LobbySettingsTab` | `Assembly-CSharp.dll` |
| `Il2CppUMUI` | `Assembly-CSharp.dll` |
| `TMPro` | `Unity.TextMeshPro.dll` |

All the game-specific namespaces (`Il2Cpp*`) come from `Assembly-CSharp.dll`. If your game splits code across multiple assemblies, check what's in `Il2CppAssemblies/` and add any additional references to the `.csproj`.

---

## Step 4: Build the Mod

Open a terminal in the **project root** (the folder containing `ArsonMod.csproj`) and run:

```bash
dotnet build -c Release
```

If successful, you'll see:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Your compiled mod will be at:

```
bin\Release\net6.0\ArsonMod.dll
```

### Common Build Errors

| Error | Fix |
|-------|-----|
| Hundreds of errors from `dd/` files (`CachedScanResults`, `unsafe code`, etc.) | Your `.csproj` is compiling the decompiled game code. Make sure `EnableDefaultCompileItems` is `false` and you have `<Compile Include="ArsonMod\**\*.cs" />` to only compile mod files |
| `Could not locate MelonLoader.dll` | Check that `GameDir` in the .csproj points to the right folder |
| `The type or namespace 'Il2CppGameManagement' could not be found` | Make sure `Assembly-CSharp.dll` reference is correct and the game has been launched with MelonLoader |
| `The type or namespace 'TMPro' could not be found` | Add a reference to `Unity.TextMeshPro.dll` from `Il2CppAssemblies/` |
| `Metadata file 'X.dll' could not be found` | The DLL filename doesn't match — check `Il2CppAssemblies/` for the actual name and update the .csproj |
| `Ambiguous reference` / `CS0436` warnings | These are normal with Il2Cpp interop — the `NoWarn` in the .csproj suppresses them |
| Every single assembly "could not be located" | You haven't set `GameDir` in the .csproj — replace `GAME_FOLDER_PATH` with your actual game path |

### Building on Linux / WSL

If you're developing on Linux or WSL (Windows Subsystem for Linux), there are a few things to keep in mind:

1. **Use forward slashes in the `.csproj`**: All paths in the `.csproj` must use `/` instead of `\`. The example above already uses forward slashes, but if you copy a `.csproj` from a Windows tutorial, you'll need to convert them.

2. **Your game folder is under `/mnt/`**: Since Dale & Dawson is a Windows game, it's installed on your Windows drive. From WSL, access it at:
   ```
   /mnt/c/Program Files (x86)/Steam/steamapps/common/Dale and Dawson
   ```
   Set your `GameDir` accordingly:
   ```xml
   <GameDir>/mnt/c/Program Files (x86)/Steam/steamapps/common/Dale and Dawson</GameDir>
   ```

3. **MelonLoader must have been run on Windows first**: MelonLoader generates the `Il2CppAssemblies/` folder the first time you launch the game with it. Since MelonLoader runs inside the game on Windows, you need to launch the game at least once from Windows (not from WSL) before the reference DLLs will exist.

4. **The compiled DLL is cross-platform**: The output `ArsonMod.dll` is a .NET assembly that works regardless of whether you built it on Linux or Windows. Just copy it to the game's `Mods/` folder on the Windows side:
   ```bash
   cp bin/Release/net6.0/ArsonMod.dll "/mnt/c/Program Files (x86)/Steam/steamapps/common/Dale and Dawson/Mods/"
   ```

---

## Step 5: Test Locally

1. Copy `ArsonMod.dll` from `bin/Release/net6.0/` into your game's `Mods/` folder
2. Launch Dale & Dawson
3. Open the MelonLoader console and look for:
   ```
   [Arson Mode] Arson Mode mod loaded.
   ```
4. Create a lobby and check that the Arson Mode toggle appears in lobby settings
5. Check `MelonLoader/Latest.log` if anything goes wrong — errors will have stack traces pointing to the exact line

### Testing Checklist

- [ ] Mod loads without errors in MelonLoader console
- [ ] "Arson Mode" toggle appears in lobby settings
- [ ] Sliders for fire spread, extinguish time, and rooms appear when toggled on
- [ ] Starting a game with Arson Mode on doesn't crash
- [ ] Fire particles appear when a trash bin catches fire
- [ ] Fire extinguisher interaction works
- [ ] Meeting pause/resume of fire spread works
- [ ] Win conditions trigger correctly (arsonist wins / arsonist caught)

---

## Step 6: Prepare a Release

### Version Bump

Update the version number in [Core/ArsonMod.cs](Core/ArsonMod.cs) line 5:

```csharp
[assembly: MelonInfo(typeof(ArsonMod.Core.ArsonModEntry), "Arson Mode", "1.0.0", "ensui-dev")]
```

Change `"1.0.0"` to your new version. Use semantic versioning:
- **Patch** (1.0.1): bug fixes only
- **Minor** (1.1.0): new features, backward compatible
- **Major** (2.0.0): breaking changes or major rework

### Clean Build

Always do a clean release build:

```bash
dotnet clean -c Release
dotnet build -c Release
```

### Release Package

The only file you need to distribute is:

```
bin\Release\net6.0\ArsonMod.dll
```

Players do **not** need any other DLLs — MelonLoader provides everything at runtime.

### GitHub Release (Recommended)

1. Create a git repository for the project if you haven't already:
   ```bash
   cd ArsonMod
   git init
   git add -A
   git commit -m "Initial release v1.0.0"
   ```

2. Create a GitHub repository and push:
   ```bash
   git remote add origin https://github.com/YOUR_USERNAME/ArsonMod.git
   git push -u origin main
   ```

3. Create a release:
   ```bash
   gh release create v1.0.0 bin\Release\net6.0\ArsonMod.dll \
     --title "Arson Mode v1.0.0" \
     --notes "Initial release of Arson Mode for Dale & Dawson."
   ```

   Or do it through GitHub's web UI: Releases → Draft a new release → attach `ArsonMod.dll`.

4. Share the release link — players download the DLL and follow `INSTALL.md`.

---

## Project Structure Reference

```
ArsonMod/
├── ArsonMod.csproj          ← Build configuration
├── README.md                ← Project overview
├── INSTALL.md               ← Player installation guide
├── BUILD.md                 ← This file
├── Core/
│   ├── ArsonMod.cs          ← Mod entry point (MelonMod)
│   ├── ArsonSettings.cs     ← Configurable settings
│   ├── HarmonyPatches.cs    ← Game method hooks
│   ├── NetworkSync.cs       ← Network state sync
│   ├── PlayerAccess.cs      ← Player system utility
│   └── PlayerInventory.cs   ← Arson item tracking
├── Fire/
│   ├── FireManager.cs       ← Fire spread logic
│   ├── FireEffects.cs       ← Particles, light, audio
│   └── RoomAdjacency.cs     ← Room graph for spread
├── Tasks/
│   ├── ArsonTaskChain.cs    ← Task sequence manager
│   ├── SmokeDetectorTask.cs
│   ├── PrintDocumentsTask.cs
│   ├── StealFluidTask.cs
│   ├── StuffTrashBinTask.cs
│   └── TossCigaretteTask.cs
├── Clues/
│   ├── ProximityTracker.cs
│   ├── SmokeDetectorClue.cs
│   └── PrintLogClue.cs
├── Items/
│   └── FireExtinguisher.cs
└── UI/
    ├── ArsonLobbyUI.cs
    ├── ArsonWinScreen.cs
    └── FireNotifications.cs
```

---

## Updating After a Game Patch

When Dale & Dawson receives an update:

1. Launch the updated game with MelonLoader once — this regenerates the Il2Cpp interop assemblies
2. Rebuild the mod with `dotnet build -c Release`
3. If the build fails, the game's internal class names or method signatures may have changed:
   - Re-decompile the updated `Assembly-CSharp.dll` with ILSpy
   - Compare the changed types against what's used in `HarmonyPatches.cs` and `PlayerAccess.cs`
   - Update the mod code to match the new signatures
4. Test thoroughly before releasing
