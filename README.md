# Arson Mode

A [MelonLoader](https://melonwiki.xyz/) mod for **Dale & Dawson Stationery Supplies** that adds an arson/firefighting social deduction game mode. One (or more) players are secretly arsonists working through a 5-task chain to ignite fires, while other players must detect clues, extinguish flames, and vote out the culprit.

**Arson Mode v1.0.0** by ensui-dev

## How It Works

### Roles

- **Slacker (Arsonist):** Secretly complete a 5-task chain to start fires. Win by burning enough rooms simultaneously before time runs out.
- **Specialist (Analyst):** Investigate fire origin clues, spot tampered smoke detectors, and gather evidence to identify and vote out the arsonist.
- **All Players:** Extinguish fires using wall-mounted fire extinguishers when fire breaks out.

### Arson Task Chain

The arsonist receives these tasks interleaved with normal work tasks, appearing one at a time in the HUD task list with location markers:

| # | Task | Location | Duration | Effect |
|---|------|----------|----------|--------|
| 1 | Jam the smoke detector | Random fire alarm (varies per game) | 4s | Disables a smoke detector |
| 2 | Print excessive documents | Printer | 5s | Grants paper stack |
| 3 | Steal lighter fluid | Supply closet | 4s | Grants lighter fluid |
| 4 | Stuff trash bin with paper | Trash bin (same room as task 1) | 3s | Requires paper stack |
| 5 | Toss lit cigarette into bin | Same trash bin as task 4 | 3s | **Fire ignites!** |

- Tasks 1-4 have decoy versions assigned to Specialists (e.g. "Inspect the smoke detector") that look identical but don't advance the chain.
- Task 5 is arsonist-only and requires lighter fluid.
- Each game randomly selects a different fire alarm location, and the subsequent trash bin tasks target the same room.
- Individual tasks can be toggled on/off in the lobby settings under the Tasks > Slacker category.

### Fire Mechanics

- Fire uses the game's native `TrashBin` fire system (particles, audio, networking all built-in)
- Fire spreads to adjacent rooms at a configurable interval (default: 20s)
- Room adjacency is automatically discovered from `RoomManager.roomConnections` — works on all maps including future ones
- Fire spread pauses during emergency meetings
- Wall-mounted fire extinguishers have 2 charges each
- When enough rooms are burning simultaneously, a countdown begins — if fires aren't extinguished in time, the arsonist wins and the game ends

### Clue System (Specialist Only)

| Clue | How It Works |
|------|-------------|
| **Proximity** | On ignition, Specialists see which players were near the fire origin room in the last 30 seconds |
| **Smoke Detector** | Players near a jammed detector get a "tampered" notification |
| **Print Log** | Printer job history shows a suspicious entry |
| **Visual** | Stuffed trash bin is visible to observant players |

### Win Conditions

| Outcome | Condition |
|---------|-----------|
| **Arsonist wins** | N rooms burning simultaneously + grace period expires without extinguishing |
| **Crew wins** | Arsonist voted out in emergency meeting |

When the arsonist wins, the game transitions to the standard end-game screen with role reveals.

## Lobby Settings

All settings appear in the lobby under the **Arson Mode** category:

| Setting | Options | Default | Description |
|---------|---------|---------|-------------|
| Arson Mode | Off / On | Off | Enable/disable the game mode |
| Fire Spread Speed | 10s / 15s / 20s / 25s / 30s | 20s | Seconds between fire spreading to adjacent rooms |
| Extinguish Time | 5s / 8s / 10s / 12s / 15s | 10s | Grace period before arsonist wins once room threshold is met |
| Rooms to Burn | 1 / 2 / 3 / 4 / 5 | 3 | Burning rooms required for arsonist win condition |
| Arsonist Count | 1 / 2 / 3 | 1 | Number of arsonists per round |

Rooms to Burn auto-scales with player count (2 for <=8 players, 3 for <=14, 5 for 15+).

## Installation

1. Install [MelonLoader](https://melonwiki.xyz/) v0.6+ for Dale & Dawson Stationery Supplies
2. Download the latest release from the [Releases](https://github.com/ensui-dev/DnD-ArsonMod/releases) page
3. Place `ArsonMod.dll` in the game's `Mods/` folder
4. Launch the game — "Arson Mode v1.0.0 by ensui-dev" will appear in the MelonLoader console

## Project Structure

```
ArsonMod/
  Core/
    ArsonMod.cs            # Mod entry point, lifecycle, OnUpdate loop
    ArsonSettings.cs       # Configurable parameters + player count scaling
    HarmonyPatches.cs      # 12 Harmony patches hooking game systems
    PlayerAccess.cs        # Player data, room detection, arsonist selection
    PlayerInventory.cs     # Lightweight item tracking (paper, fluid)
    NetworkSync.cs         # Network event broadcasting
    FileLogger.cs          # Debug file logging
  Fire/
    FireManager.cs         # Fire state machine, spread logic, win conditions
    FireEffects.cs         # Particle/audio effects (procedural fallback)
    RoomAdjacency.cs       # Dynamic room graph from RoomManager.roomConnections
  Tasks/
    ArsonTaskChain.cs      # 5-task progression + decoy system
    ArsonTaskInjector.cs   # InteractionAlternative injection + fire alarm handler
    SmokeDetectorTask.cs   # Task 1: Jam detector
    PrintDocumentsTask.cs  # Task 2: Print documents
    StealFluidTask.cs      # Task 3: Steal lighter fluid
    StuffTrashBinTask.cs   # Task 4: Stuff bin
    TossCigaretteTask.cs   # Task 5: Ignite (finale)
  Clues/
    ProximityTracker.cs    # Player position sampling near fire origins
    SmokeDetectorClue.cs   # Tamper detection alerts
    PrintLogClue.cs        # Printer job log evidence
  UI/
    ArsonLobbyUI.cs        # Lobby settings injection into game UI
    ArsonWinScreen.cs      # Victory/defeat screen overlay
    FireNotifications.cs   # Alerts, banners, clue popups
  Items/
    FireExtinguisher.cs    # Extinguisher pickup/usage

ArsonMod.csproj            # Build project
```

## Building from Source

### Prerequisites

- .NET 6.0 SDK
- Dale & Dawson Stationery Supplies with MelonLoader v0.6+ installed

### Setup

1. Update the `GameDir` path in `ArsonMod.csproj` to point to your game installation:
   ```xml
   <GameDir>C:\SteamLibrary\steamapps\common\Dale&amp;Dawson</GameDir>
   ```
2. Build:
   ```bash
   dotnet build ArsonMod.csproj -c Release
   ```
3. Copy `bin/Release/net6.0/ArsonMod.dll` to the game's `Mods/` folder

## Technical Details

The mod uses 12 Harmony patches to hook into the game's systems without modifying game files:

- **Patch 1:** `InGameState.Enter` — Initialize arson round, select arsonists
- **Patch 2-3:** `MeetingState.Enter/Exit` — Pause/resume fire during meetings
- **Patch 5:** `TrashBin.RpcEnableFire` — Detect fire events on all clients
- **Patch 6:** `LobbySettingsTab.ShowCategories` — Inject lobby settings UI
- **Patch 8:** `TaskController.ServerGiveNewTasks` — Trigger next arson task
- **Patch 11:** `HUDTab.LateUpdate` — Display arson task in HUD task list
- **Patch 12:** `CameraController.Update` + `TaskHighlighter.Update` — Fire alarm interaction UI + location markers

All fire mechanics use the game's native `TrashBin.CmdEnableFire()` / `RpcEnableFire()` Mirror networking — no custom network messages needed.

## Upcoming Features

- **Interactive task flow:** Print documents task will require the player to physically use the printer and carry documents to the trash bin. The finale task will have the player actually smoke a cigarette from a cigarette pack and toss it into the stuffed bin to trigger the fire — full use of the game's built-in `CigarettePack` and `Cigarette` systems.
- **Enhanced clue system:** More investigative tools for Specialists to identify the arsonist.
- **Fire extinguisher improvements:** Proper charge tracking and respawn mechanics.

## License

This project is a fan-made mod and is not affiliated with Striped Panda Studios.
