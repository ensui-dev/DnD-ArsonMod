# Asset Bundle Creation Guide

The mod needs a Unity AssetBundle containing fire effects and audio.

## Required Assets

### Prefabs
1. **FireFlames** — Particle system prefab for fire flames
   - Orange/red gradient, upward emission, cone shape
   - If not provided, the mod falls back to a procedurally-generated particle system

2. **FireSmoke** — Particle system prefab for smoke
   - Dark gray, slow rising, expanding over lifetime
   - Falls back to procedural if missing

3. **OverfillPapers** — Simple mesh of crumpled papers sticking out of a trash bin
   - Used as visual clue when arsonist completes Task 4
   - White/off-white paper material

### Audio Clips
4. **FireCrackling** — Looping fire crackle sound (3D spatial)
5. **FireAlarm** — Office fire alarm (short burst, played on ignition)
6. **ExtinguisherSpray** — Hissing spray sound (looping while using extinguisher)

### Optional (polish)
7. **SmokeOverlay** — Screen-space smoke texture for players in burning rooms
8. **CoughSound** — Player coughing audio (played at intervals in burning rooms)

## How to Build the AssetBundle

1. Create a new Unity project matching the game's Unity version
   - Check the game's version: look at `<GameDir>/Dale & Dawson_Data/globalgamemanagers`
   - Or check MelonLoader console output on launch (prints Unity version)

2. Import/create the assets listed above

3. Mark them for bundling:
   - Select each asset in Unity
   - In the Inspector, at the bottom, set AssetBundle name to "arsonmod"

4. Build the bundle using this editor script:

```csharp
// Place in Assets/Editor/BuildBundle.cs
using UnityEditor;

public class BuildBundle
{
    [MenuItem("Assets/Build ArsonMod Bundle")]
    static void Build()
    {
        BuildPipeline.BuildAssetBundles(
            "Assets/Bundles",
            BuildAssetBundleOptions.None,
            BuildTarget.StandaloneWindows64
        );
    }
}
```

5. Copy the output file `Assets/Bundles/arsonmod` to:
   `<GameDir>/Mods/ArsonMod/arsonmod.bundle`

6. Load it in the mod entry point (ArsonMod.cs OnInitializeMelon):

```csharp
var bundlePath = Path.Combine(MelonHandler.ModsDirectory, "ArsonMod", "arsonmod.bundle");
var bundle = AssetBundle.LoadFromFile(bundlePath);
Fire.FireEffects.LoadAssets(bundle);
```
