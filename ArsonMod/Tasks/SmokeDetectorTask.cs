using UnityEngine;
using Il2CppPlayer.Scripts.StateMachineLogic;

namespace ArsonMod.Tasks
{
    /// <summary>
    /// Task 1: Interact with a smoke detector on the ceiling.
    /// Arsonist version: Jams the detector (disables it, leaves a clue for observant players).
    /// Decoy version: Inspects the detector (leaves it functional).
    /// </summary>
    public class SmokeDetectorTask : MonoBehaviour
    {
        public string DetectorId;
        public bool IsJammed { get; private set; }

        private bool _isBeingUsed;
        private float _useTimer;
        private string _usingPlayerId;
        private const float INTERACTION_DURATION = 4f;

        public void StartInteraction(string playerId, bool isRealArson)
        {
            if (_isBeingUsed) return;

            _isBeingUsed = true;
            _usingPlayerId = playerId;
            _useTimer = 0f;

            // Play reaching-up animation using the game's upper body state system
            var localPlayer = Core.PlayerAccess.GetLocalPlayer();
            if (localPlayer.HasValue && localPlayer.Value.PlayerId == playerId)
            {
                var pc = localPlayer.Value.LobbyPlayer.playerController?
                    .GetComponent<Il2CppPlayer.PlayerController>();
                if (pc != null)
                {
                    pc.SetUBState(1); // Upper body interaction state
                }

                // Show loading bar
                var ui = Object.FindObjectOfType<Il2CppUMUI.UIManager>();
                ui?.ShowInstruction(isRealArson ? "Jamming smoke detector..." : "Inspecting smoke detector...", "", true);
                ui?.SetInstructionTimer(INTERACTION_DURATION);
            }
        }

        private void Update()
        {
            if (!_isBeingUsed) return;

            _useTimer += Time.deltaTime;

            if (_useTimer >= INTERACTION_DURATION)
            {
                CompleteInteraction();
            }
        }

        private void CompleteInteraction()
        {
            _isBeingUsed = false;

            // Check if the player doing this is the arsonist
            var taskChain = Core.ArsonModEntry.Instance.TaskChain;
            bool isArsonist = taskChain.IsArsonist(_usingPlayerId);

            if (isArsonist)
            {
                IsJammed = true;
                // Visual: slight angle change on the detector, indicator light off
                UpdateVisuals();
                taskChain.OnArsonTaskCompleted(_usingPlayerId, 0);
            }
            // Decoy: animation plays but detector stays functional

            _usingPlayerId = null;
        }

        private void UpdateVisuals()
        {
            if (IsJammed)
            {
                // Tilt the detector model slightly, turn off the green LED
                // This is subtle enough that players need to actively look for it
                transform.localRotation *= Quaternion.Euler(0, 0, 8f);

                var indicator = transform.Find("IndicatorLight");
                if (indicator != null)
                {
                    var renderer = indicator.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material.color = Color.black;
                        renderer.material.SetColor("_EmissionColor", Color.black);
                    }
                }
            }
        }

        /// <summary>
        /// Called when a Specialist walks near this detector.
        /// If jammed, they get a clue notification.
        /// </summary>
        public void OnPlayerProximity(string playerId)
        {
            if (!IsJammed) return;

            var taskChain = Core.ArsonModEntry.Instance.TaskChain;
            if (taskChain.IsArsonist(playerId)) return;

            UI.FireNotifications.ShowClue(
                "This smoke detector appears to have been tampered with...",
                playerId
            );
        }
    }
}
