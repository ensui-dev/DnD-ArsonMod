using System.Collections.Generic;
using UnityEngine;

namespace ArsonMod.Clues
{
    /// <summary>
    /// Monitors smoke detectors that have been jammed by the arsonist (Task 1).
    /// When a non-arsonist player walks near a tampered detector,
    /// they receive a notification clue.
    /// </summary>
    public class SmokeDetectorClue : MonoBehaviour
    {
        private const float DETECTION_RADIUS = 3f;
        private const float CLUE_COOLDOWN = 60f;

        private readonly Dictionary<string, float> _lastNotifiedTime = new();

        private Tasks.SmokeDetectorTask[] _detectors;

        private void Start()
        {
            _detectors = FindObjectsOfType<Tasks.SmokeDetectorTask>();
        }

        private void Update()
        {
            if (_detectors == null || _detectors.Length == 0) return;

            var players = Core.PlayerAccess.GetAllPlayers();

            foreach (var player in players)
            {
                // Don't show clue to the arsonist
                if (Core.PlayerAccess.IsArsonist(player.PlayerId))
                    continue;

                foreach (var detector in _detectors)
                {
                    if (!detector.IsJammed) continue;

                    float dist = Vector3.Distance(player.Position, detector.transform.position);
                    if (dist > DETECTION_RADIUS) continue;

                    string key = $"{player.PlayerId}_{detector.GetInstanceID()}";
                    if (_lastNotifiedTime.TryGetValue(key, out float lastTime) &&
                        Time.time - lastTime < CLUE_COOLDOWN)
                        continue;

                    _lastNotifiedTime[key] = Time.time;

                    // Only show to the local player if they are this player
                    if (player.IsLocal)
                    {
                        UI.FireNotifications.ShowTamperedDetectorClue(player.PlayerId);
                    }
                }
            }
        }
    }
}
