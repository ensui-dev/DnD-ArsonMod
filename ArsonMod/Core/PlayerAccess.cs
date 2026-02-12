using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Il2CppPlayer.Lobby;
using Il2CppGameManagement;
using Il2CppRoom;
using MelonLoader;

namespace ArsonMod.Core
{
    /// <summary>
    /// Utility wrapper around the game's player system.
    /// Provides easy access to player positions, roles, rooms, etc.
    /// </summary>
    public static class PlayerAccess
    {
        private static readonly HashSet<string> _arsonistPlayerIds = new();

        public struct PlayerInfo
        {
            public string PlayerId;
            public Vector3 Position;
            public PlayerRole Role;
            public bool IsLocal;
            public string CurrentRoom;
            public LobbyPlayer LobbyPlayer;
        }

        private static bool _loggedDiagnostics;

        /// <summary>
        /// Gets all active (non-fired) players in the game.
        /// Primary: iterates GameManager.workStations.
        /// Fallback: FindObjectsOfType&lt;LobbyPlayer&gt;() for solo lobbies where workstations may be empty.
        /// </summary>
        public static List<PlayerInfo> GetAllPlayers()
        {
            var players = new List<PlayerInfo>();
            var gm = GameManager.instance;

            if (gm != null)
            {
                foreach (var ws in gm.workStations)
                {
                    if (ws == null) continue;
                    var ownerNetId = ws.ownerLobbyPlayer;
                    if (ownerNetId == null) continue;
                    var lobbyPlayer = ownerNetId.GetComponent<LobbyPlayer>();
                    if (lobbyPlayer == null || lobbyPlayer.isFired) continue;

                    var controllerNetId = lobbyPlayer.playerController;
                    if (controllerNetId == null) continue;

                    var playerController = controllerNetId.GetComponent<Il2CppPlayer.PlayerController>();
                    if (playerController == null) continue;

                    players.Add(new PlayerInfo
                    {
                        PlayerId = lobbyPlayer.steamID.ToString(),
                        Position = playerController.transform.position,
                        Role = lobbyPlayer.playerRole,
                        IsLocal = playerController.isLocalPlayer,
                        CurrentRoom = GetRoomForPosition(playerController.transform.position),
                        LobbyPlayer = lobbyPlayer,
                    });
                }
            }

            // Fallback: if workstation-based discovery found nothing, try FindObjectsOfType
            if (players.Count == 0)
            {
                if (!_loggedDiagnostics)
                {
                    LogPlayerDiagnostics(gm);
                    _loggedDiagnostics = true;
                }

                var lobbyPlayers = UnityEngine.Object.FindObjectsOfType<LobbyPlayer>();
                if (lobbyPlayers != null)
                {
                    foreach (var lp in lobbyPlayers)
                    {
                        if (lp == null || lp.isFired) continue;

                        var controllerNetId = lp.playerController;
                        if (controllerNetId == null) continue;

                        var playerController = controllerNetId.GetComponent<Il2CppPlayer.PlayerController>();
                        if (playerController == null) continue;

                        players.Add(new PlayerInfo
                        {
                            PlayerId = lp.steamID.ToString(),
                            Position = playerController.transform.position,
                            Role = lp.playerRole,
                            IsLocal = playerController.isLocalPlayer,
                            CurrentRoom = GetRoomForPosition(playerController.transform.position),
                            LobbyPlayer = lp,
                        });
                    }
                }
            }

            return players;
        }

        private static void LogPlayerDiagnostics(GameManager gm)
        {
            MelonLogger.Msg("[ArsonMod] === Player Discovery Diagnostics ===");
            MelonLogger.Msg($"[ArsonMod] GameManager.instance: {(gm != null ? "exists" : "NULL")}");

            if (gm != null)
            {
                var ws = gm.workStations;
                MelonLogger.Msg($"[ArsonMod] workStations: {(ws != null ? $"{ws.Count} entries" : "NULL")}");

                if (ws != null)
                {
                    for (int i = 0; i < ws.Count; i++)
                    {
                        var station = ws[i];
                        if (station == null) { MelonLogger.Msg($"[ArsonMod]   ws[{i}]: null"); continue; }
                        var owner = station.ownerLobbyPlayer;
                        if (owner == null) { MelonLogger.Msg($"[ArsonMod]   ws[{i}]: ownerLobbyPlayer=null"); continue; }
                        var lp = owner.GetComponent<LobbyPlayer>();
                        if (lp == null) { MelonLogger.Msg($"[ArsonMod]   ws[{i}]: LobbyPlayer component=null"); continue; }
                        MelonLogger.Msg($"[ArsonMod]   ws[{i}]: steamID={lp.steamID}, role={lp.playerRole}, isFired={lp.isFired}, playerController={(lp.playerController != null ? "exists" : "null")}");
                    }
                }
            }

            var allLobbyPlayers = UnityEngine.Object.FindObjectsOfType<LobbyPlayer>();
            MelonLogger.Msg($"[ArsonMod] FindObjectsOfType<LobbyPlayer>: {(allLobbyPlayers != null ? $"{allLobbyPlayers.Length} found" : "null")}");
            if (allLobbyPlayers != null)
            {
                foreach (var lp in allLobbyPlayers)
                {
                    if (lp == null) continue;
                    var ctrl = lp.playerController;
                    var pc = ctrl?.GetComponent<Il2CppPlayer.PlayerController>();
                    MelonLogger.Msg($"[ArsonMod]   LobbyPlayer: steamID={lp.steamID}, role={lp.playerRole}, isFired={lp.isFired}, controller={(ctrl != null ? "exists" : "null")}, isLocal={(pc != null ? pc.isLocalPlayer.ToString() : "N/A")}");
                }
            }

            MelonLogger.Msg("[ArsonMod] === End Player Diagnostics ===");
        }

        public static PlayerInfo? GetLocalPlayer()
        {
            return GetAllPlayers().Cast<PlayerInfo?>().FirstOrDefault(p => p.Value.IsLocal);
        }

        public static string GetRoomForPosition(Vector3 position)
        {
            var triggers = UnityEngine.Object.FindObjectsOfType<RoomTrigger>();
            if (triggers == null) return null;

            foreach (var trigger in triggers)
            {
                if (trigger == null || trigger.currentRoom == null) continue;
                var collider = trigger.GetComponent<Collider>();
                if (collider != null && collider.bounds.Contains(position))
                    return trigger.currentRoom.roomName;
            }

            foreach (var trigger in triggers)
            {
                if (trigger == null || trigger.currentRoom == null) continue;
                var collider = trigger.GetComponent<Collider>();
                if (collider == null) continue;
                var bounds = collider.bounds;
                Vector3 projected = new Vector3(position.x, bounds.center.y, position.z);
                if (bounds.Contains(projected))
                    return trigger.currentRoom.roomName;
            }

            string closestRoom = null;
            float closestDist = float.MaxValue;
            foreach (var trigger in triggers)
            {
                if (trigger == null || trigger.currentRoom == null) continue;
                var collider = trigger.GetComponent<Collider>();
                if (collider == null) continue;
                float dist = Vector2.Distance(
                    new Vector2(position.x, position.z),
                    new Vector2(collider.bounds.center.x, collider.bounds.center.z));
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestRoom = trigger.currentRoom.roomName;
                }
            }

            return (closestRoom != null && closestDist < 10f) ? closestRoom : null;
        }

        public static Vector3? GetPlayerPosition(LobbyPlayer lobbyPlayer)
        {
            if (lobbyPlayer?.playerController == null) return null;
            var pc = lobbyPlayer.playerController.GetComponent<Il2CppPlayer.PlayerController>();
            return pc?.transform.position;
        }

        /// <summary>Adds a player to the arsonist set for this round.</summary>
        public static void SetArsonist(string playerId)
        {
            _arsonistPlayerIds.Add(playerId);
        }

        /// <summary>Checks if a player is a designated arsonist.</summary>
        public static bool IsArsonist(string playerId)
        {
            return _arsonistPlayerIds.Contains(playerId);
        }

        public static bool IsArsonist(LobbyPlayer player)
        {
            return player != null && IsArsonist(player.steamID.ToString());
        }

        /// <summary>Gets any arsonist's player ID (for backward compat), or null.</summary>
        public static string GetArsonistId()
        {
            return _arsonistPlayerIds.Count > 0 ? _arsonistPlayerIds.First() : null;
        }

        /// <summary>Gets all arsonist player IDs.</summary>
        public static HashSet<string> GetArsonistIds() => _arsonistPlayerIds;

        public static void Reset()
        {
            _arsonistPlayerIds.Clear();
            _loggedDiagnostics = false;
        }

        /// <summary>
        /// Selects arsonists from Slackers using a deterministic seed
        /// derived from synced game state. Respects the ArsonistCount setting.
        /// </summary>
        public static string SelectArsonist()
        {
            int count = ArsonModEntry.Instance?.Settings?.ArsonistCount ?? 1;

            var slackers = GetAllPlayers()
                .Where(p => p.Role == PlayerRole.Slacker)
                .OrderBy(p => p.PlayerId)
                .ToList();

            if (slackers.Count == 0) return null;

            int seed = 0;
            foreach (var s in slackers)
                seed = seed * 31 + s.PlayerId.GetHashCode();
            var rng = new System.Random(seed);

            int toSelect = Math.Min(count, slackers.Count);
            for (int i = 0; i < toSelect; i++)
            {
                int idx = rng.Next(slackers.Count);
                SetArsonist(slackers[idx].PlayerId);
                MelonLogger.Msg($"[ArsonMod] Selected arsonist {i + 1}/{toSelect}: {slackers[idx].PlayerId}");
                slackers.RemoveAt(idx);
            }

            return GetArsonistId();
        }
    }
}
