using UnityEngine;

namespace ArsonMod.Tasks
{
    /// <summary>
    /// Task 3: Interact with the supply/janitor closet.
    /// Arsonist version: Steals lighter fluid, pocketing it.
    /// Decoy version: Organizes the supply closet shelves.
    /// Medium suspicion — the closet is a less common task location.
    /// </summary>
    public class StealFluidTask : MonoBehaviour
    {
        public string ClosetId;

        private bool _isBeingUsed;
        private float _useTimer;
        private string _usingPlayerId;
        private const float INTERACTION_DURATION = 4f;

        public void StartInteraction(string playerId)
        {
            if (_isBeingUsed) return;

            _isBeingUsed = true;
            _usingPlayerId = playerId;
            _useTimer = 0f;

            // Show loading bar
            var ui = Object.FindObjectOfType<Il2CppUMUI.UIManager>();
            ui?.ShowInstruction("Searching supply closet...", "", true);
            ui?.SetInstructionTimer(INTERACTION_DURATION);
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

            var taskChain = Core.ArsonModEntry.Instance.TaskChain;
            bool isArsonist = taskChain.IsArsonist(_usingPlayerId);

            if (isArsonist)
            {
                // Grant lighter_fluid item to the player's inventory
                Core.PlayerInventory.AddItem(_usingPlayerId, "lighter_fluid");

                taskChain.OnArsonTaskCompleted(_usingPlayerId, 2);
            }

            _usingPlayerId = null;
        }
    }
}
