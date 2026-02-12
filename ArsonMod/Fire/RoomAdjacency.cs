using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Il2CppRoom;
using MelonLoader;

namespace ArsonMod.Fire
{
    /// <summary>
    /// Dynamically discovers room adjacency from the game's RoomManager at runtime.
    /// Supports all maps (current and future) without hardcoded room names.
    /// </summary>
    public static class RoomAdjacency
    {
        private static Dictionary<string, List<string>> _adjacency;

        /// <summary>
        /// Builds the adjacency graph from RoomManager.instance.roomConnections.
        /// No parameters needed — works for any map automatically.
        /// </summary>
        public static void Initialize()
        {
            _adjacency = new Dictionary<string, List<string>>();

            var rm = RoomManager.instance;
            if (rm == null)
            {
                MelonLogger.Warning("[ArsonMod] RoomManager.instance is null, cannot build adjacency graph.");
                return;
            }

            var connections = rm.roomConnections;
            if (connections == null)
            {
                MelonLogger.Warning("[ArsonMod] RoomManager.roomConnections is null.");
                return;
            }

            // roomConnections is Dictionary<ValueTuple<string,string>, List<DoorController>>
            // Each key (roomA, roomB) represents a connection between two rooms
            foreach (var entry in connections)
            {
                var key = entry.Key;
                string roomA = key.Item1;
                string roomB = key.Item2;

                if (string.IsNullOrEmpty(roomA) || string.IsNullOrEmpty(roomB))
                    continue;

                AddEdge(roomA, roomB);
            }

            MelonLogger.Msg($"[ArsonMod] Room adjacency graph built: {_adjacency.Count} rooms discovered.");
            foreach (var kvp in _adjacency)
            {
                MelonLogger.Msg($"[ArsonMod]   {kvp.Key} -> [{string.Join(", ", kvp.Value)}]");
            }
        }

        public static List<string> GetAdjacentRooms(string roomId)
        {
            if (_adjacency != null && _adjacency.TryGetValue(roomId, out var neighbors))
                return neighbors;
            return new List<string>();
        }

        public static List<string> GetAllRooms()
        {
            return _adjacency?.Keys.ToList() ?? new List<string>();
        }

        /// <summary>
        /// Determines which room contains a given world position.
        /// </summary>
        public static string GetRoomAtPosition(Vector3 position)
        {
            return Core.PlayerAccess.GetRoomForPosition(position);
        }

        private static void AddEdge(string a, string b)
        {
            if (!_adjacency.ContainsKey(a)) _adjacency[a] = new List<string>();
            if (!_adjacency.ContainsKey(b)) _adjacency[b] = new List<string>();

            if (!_adjacency[a].Contains(b)) _adjacency[a].Add(b);
            if (!_adjacency[b].Contains(a)) _adjacency[b].Add(a);
        }
    }
}
