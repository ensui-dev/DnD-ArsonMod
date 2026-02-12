using System.Collections.Generic;
using System.Linq;
using Il2CppPlayer.Lobby;
using UnityEngine;

namespace ArsonMod.Clues
{
    /// <summary>
    /// Tracks which players were near the fire origin room before ignition.
    /// When fire starts, reveals a proximity clue showing who was recently nearby.
    /// </summary>
    public class ProximityTracker
    {
        /// <summary>How many seconds of history to keep.</summary>
        private const float TRACKING_WINDOW = 30f;

        /// <summary>How often to sample player positions (seconds).</summary>
        private const float SAMPLE_INTERVAL = 2f;

        private float _sampleTimer;

        /// <summary>
        /// Rolling buffer of (playerId, roomId, timestamp) entries.
        /// </summary>
        private readonly List<PositionSample> _samples = new();

        public void Update(float deltaTime)
        {
            _sampleTimer += deltaTime;
            if (_sampleTimer < SAMPLE_INTERVAL) return;
            _sampleTimer = 0f;

            RecordCurrentPositions();
            PruneOldSamples();
        }

        private void RecordCurrentPositions()
        {
            var players = Core.PlayerAccess.GetAllPlayers();
            foreach (var player in players)
            {
                if (player.CurrentRoom != null)
                {
                    _samples.Add(new PositionSample
                    {
                        PlayerId = player.PlayerId,
                        RoomId = player.CurrentRoom,
                        Timestamp = Time.time,
                    });
                }
            }
        }

        private void PruneOldSamples()
        {
            float cutoff = Time.time - TRACKING_WINDOW;
            _samples.RemoveAll(s => s.Timestamp < cutoff);
        }

        /// <summary>
        /// Called when fire starts. Shows a proximity clue to Specialists only,
        /// listing which players were in or adjacent to the origin room recently.
        /// </summary>
        public void OnFireStarted(string originRoomId)
        {
            // Only Specialists can inspect fire origin clues
            var localPlayer = Core.PlayerAccess.GetLocalPlayer();
            if (!localPlayer.HasValue || localPlayer.Value.Role != PlayerRole.Specialist)
                return;

            var adjacentRooms = Fire.RoomAdjacency.GetAdjacentRooms(originRoomId);
            var relevantRooms = new HashSet<string>(adjacentRooms) { originRoomId };

            var nearbyPlayerIds = _samples
                .Where(s => relevantRooms.Contains(s.RoomId))
                .Select(s => s.PlayerId)
                .Distinct()
                .ToList();

            if (nearbyPlayerIds.Count == 0) return;

            // Resolve steam IDs to usernames for the clue display
            var allPlayers = Core.PlayerAccess.GetAllPlayers();
            var playerNames = new List<string>();
            foreach (var id in nearbyPlayerIds)
            {
                string name = id; // fallback to ID
                foreach (var p in allPlayers)
                {
                    if (p.PlayerId == id)
                    {
                        name = p.LobbyPlayer.username;
                        break;
                    }
                }
                playerNames.Add(name);
            }

            UI.FireNotifications.ShowProximityClue(originRoomId, playerNames);
        }

        private struct PositionSample
        {
            public string PlayerId;
            public string RoomId;
            public float Timestamp;
        }
    }
}
