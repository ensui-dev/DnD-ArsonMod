# Arson Mode - Installation Guide

This guide will walk you through installing Arson Mode for Dale & Dawson, step by step. No modding experience needed!

**Important:** Every player in the lobby needs this mod installed for it to work. Share this guide with your friends so everyone can get set up.

---

## What You Need

- A PC copy of **Dale & Dawson** (Steam)
- The **ArsonMod.dll** file (you should have received this from whoever shared the mod)
- An internet connection (for downloading MelonLoader)

---

## Step 1: Install MelonLoader

MelonLoader is a free, open-source mod loader that lets Dale & Dawson load mods like Arson Mode. You only need to do this once.

### Option A: Automatic Installer (Easiest)

1. Go to the MelonLoader releases page:
   **https://github.com/LavaGang/MelonLoader/releases/latest**

2. Download **MelonLoader.Installer.exe**

3. Run the installer. If Windows shows a security warning, click **"More info"** then **"Run anyway"** — this is normal for unsigned programs.

4. In the installer:
   - Click the **SELECT** button next to the file path box
   - Navigate to your Dale & Dawson game folder and select **DaleAndDawson.exe**
     - Not sure where your game is? See the section below: *"Finding Your Game Folder"*
   - Make sure the version is set to the latest
   - Make sure **"Il2Cpp"** is selected as the game type (it should auto-detect this)
   - Click **INSTALL**

5. Wait for it to finish. You should see a success message.

### Option B: Manual Install

If the installer doesn't work for you:

1. Download the latest **MelonLoader.x64.zip** from the releases page above
2. Extract the contents directly into your Dale & Dawson game folder
3. You should now see a `MelonLoader` folder and `version.dll` sitting next to `DaleAndDawson.exe`

### First Launch (Important!)

After installing MelonLoader, **launch Dale & Dawson once and wait for it to fully load to the main menu.** This first launch takes longer than usual because MelonLoader is setting up. You'll see a console window pop up with green text — that's normal!

Once you reach the main menu, you can close the game. This step creates the folders MelonLoader needs.

---

## Step 2: Install Arson Mode

This is the easy part!

1. Open your **Dale & Dawson game folder**

2. You should now see a folder called **Mods** (it was created during the first launch in Step 1). If you don't see it, create a new folder and name it exactly `Mods`

3. **Drag and drop ArsonMod.dll** into the `Mods` folder

4. That's it! The mod is installed.

Your game folder should now look something like this:

```
Dale & Dawson/
├── DaleAndDawson.exe
├── version.dll               (from MelonLoader)
├── MelonLoader/               (from MelonLoader)
│   ├── Il2CppAssemblies/
│   └── ...
├── Mods/
│   └── ArsonMod.dll           <-- your mod goes here
└── ...
```

---

## Step 3: Verify It's Working

1. Launch Dale & Dawson
2. A console window will appear alongside the game — look for a line that says:

   ```
   [Arson Mode] Arson Mode mod loaded.
   ```

3. If you see that message, the mod is installed and ready to go!

---

## How to Play Arson Mode

1. The **host** creates a lobby as normal
2. In the lobby settings, a new **"Arson Mode"** toggle will appear at the bottom
3. The host enables it and can adjust settings like fire spread speed, extinguish time, and how many rooms need to burn for the arsonist to win
4. Start the game — one Slacker will secretly be chosen as the arsonist
5. The arsonist completes a chain of tasks to start a fire. Everyone else tries to figure out who it is and vote them out before the fire spreads too far!

---

## Finding Your Game Folder

If you're not sure where Dale & Dawson is installed:

### Steam

1. Open **Steam**
2. Go to your **Library**
3. Right-click **Dale & Dawson**
4. Click **Manage** then **Browse local files**
5. This opens the game folder — that's where you need to put things!

---

## Uninstalling the Mod

Want to remove Arson Mode?

- **Just the mod:** Delete `ArsonMod.dll` from the `Mods` folder
- **MelonLoader entirely:** Delete the `MelonLoader` folder and `version.dll` from your game folder. The game will go back to normal.

---

## Troubleshooting

### The game won't launch after installing MelonLoader
- Make sure you downloaded the correct version (Il2Cpp, x64)
- Try running the game as administrator
- Verify your game files through Steam (right-click the game, Properties, Local Files, "Verify integrity of game files") and reinstall MelonLoader

### I don't see the Arson Mode toggle in lobby settings
- Make sure everyone in the lobby has the mod installed
- Check the MelonLoader console for error messages
- Make sure the `ArsonMod.dll` is in the `Mods` folder (not in a subfolder inside it)

### The console shows errors in red text
- Make sure your version of the game matches the version the mod was built for
- If the game recently updated, the mod may need to be updated too — check where you got it for a newer version

### The game crashes on startup
- Remove `ArsonMod.dll` from the `Mods` folder and try again to confirm it's the mod causing issues
- Check the log file at `MelonLoader/Latest.log` for details on what went wrong

---

## Need Help?

If you run into issues, check the `MelonLoader/Latest.log` file in your game folder. This log contains detailed information about what happened and is helpful for debugging problems. Share it with whoever gave you the mod so they can help figure out what went wrong.
