using UnityEngine;

namespace ArsonMod.Tasks
{
    /// <summary>
    /// Task 2: Use the office printer.
    /// Arsonist version: Prints an excessive amount, producing a "paper_stack" item.
    /// Decoy version: Prints compliance documents (normal output, no item granted).
    /// Both look identical during the animation — player stands at the printer waiting.
    /// </summary>
    public class PrintDocumentsTask : MonoBehaviour
    {
        public string PrinterId;
        public bool HasExcessiveJob { get; private set; }

        private bool _isBeingUsed;
        private float _useTimer;
        private string _usingPlayerId;
        private const float INTERACTION_DURATION = 5f;

        /// <summary>Timestamp of last excessive print job, used by PrintLogClue.</summary>
        public float LastExcessivePrintTime { get; private set; } = -1f;

        public void StartInteraction(string playerId)
        {
            if (_isBeingUsed) return;

            _isBeingUsed = true;
            _usingPlayerId = playerId;
            _useTimer = 0f;

            // Show loading bar
            var ui = Object.FindObjectOfType<Il2CppUMUI.UIManager>();
            ui?.ShowInstruction("Printing documents...", "", true);
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
                HasExcessiveJob = true;
                LastExcessivePrintTime = Time.time;

                // Grant the paper_stack item to the player's inventory
                Core.PlayerInventory.AddItem(_usingPlayerId, "paper_stack");

                taskChain.OnArsonTaskCompleted(_usingPlayerId, 1);

                UpdatePrintLog();
            }

            _usingPlayerId = null;
        }

        /// <summary>
        /// Updates the printer's "Recent Jobs" display to show the excessive job.
        /// This is a clue — no player name is attached, but the timing can be noted.
        /// </summary>
        private void UpdatePrintLog()
        {
            // Add an entry to the printer's visible log screen:
            // "PRINT JOB - 847 pages - [timestamp]"
            // Normal jobs show 1-15 pages. This stands out if someone checks.
            // Show the excessive print log as a hint to nearby players
            // Use the game's UIManager to display a subtle hint about the print job
            var ui = Object.FindObjectOfType<Il2CppUMUI.UIManager>();
            int pageCount = Random.Range(500, 999);
            ui?.ShowHint($"Printer activity: {pageCount} pages printed", 3f);
        }

        /// <summary>Returns print log data for the clue system.</summary>
        public string GetPrintLogEntry()
        {
            if (!HasExcessiveJob) return null;

            int fakePageCount = Random.Range(500, 999);
            return $"PRINT JOB - {fakePageCount} pages";
        }
    }
}
