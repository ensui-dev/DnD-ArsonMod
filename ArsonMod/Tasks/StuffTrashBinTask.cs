using UnityEngine;

namespace ArsonMod.Tasks
{
    /// <summary>
    /// Task 4: Interact with a trash bin in the break room.
    /// Arsonist version: Stuffs printed documents into the bin (visible change).
    /// Decoy version: Empties/tidies the recycling bin.
    /// The arsonist's version causes a visible model change (papers sticking out)
    /// that serves as a clue for observant players.
    /// </summary>
    public class StuffTrashBinTask : MonoBehaviour
    {
        public string BinId;
        public bool IsStuffed { get; private set; }

        private bool _isBeingUsed;
        private float _useTimer;
        private string _usingPlayerId;
        private const float INTERACTION_DURATION = 3f;

        /// <summary>Reference to the overfill visual (papers sticking out of the bin).</summary>
        private GameObject _overfillVisual;

        private void Awake()
        {
            _overfillVisual = transform.Find("OverfillPapers")?.gameObject;
            if (_overfillVisual != null)
                _overfillVisual.SetActive(false);
        }

        public void StartInteraction(string playerId)
        {
            if (_isBeingUsed) return;

            _isBeingUsed = true;
            _usingPlayerId = playerId;
            _useTimer = 0f;

            // Show loading bar
            var ui = Object.FindObjectOfType<Il2CppUMUI.UIManager>();
            ui?.ShowInstruction("Interacting with trash bin...", "", true);
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
                IsStuffed = true;
                ShowOverfillVisual();

                // Consume the paper_stack item
                Core.PlayerInventory.RemoveItem(_usingPlayerId, "paper_stack");

                taskChain.OnArsonTaskCompleted(_usingPlayerId, 3, BinId);
            }

            _usingPlayerId = null;
        }

        /// <summary>
        /// Shows papers sticking out of the bin — a visible clue.
        /// Players who enter the room and look at the bin can notice this.
        /// </summary>
        private void ShowOverfillVisual()
        {
            if (_overfillVisual != null)
            {
                _overfillVisual.SetActive(true);
            }
            else
            {
                // Fallback: clone the game's document prefab from the Printer
                // and parent copies to the bin with slight rotations for a "stuffed" look
                var printer = Il2CppProps.Printer.Printer.instance;
                if (printer != null && printer.documentPrefab != null)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        var paper = Object.Instantiate(printer.documentPrefab, transform);
                        paper.transform.localPosition = new Vector3(
                            Random.Range(-0.05f, 0.05f),
                            0.15f + i * 0.02f,
                            Random.Range(-0.05f, 0.05f)
                        );
                        paper.transform.localRotation = Quaternion.Euler(
                            Random.Range(-20f, 20f),
                            Random.Range(0f, 360f),
                            Random.Range(-15f, 15f)
                        );
                    }
                    _overfillVisual = transform.GetChild(transform.childCount - 1)?.gameObject;
                }
            }
        }

        /// <summary>Resets the bin to normal state (when arson chain resets).</summary>
        public void ResetBin()
        {
            IsStuffed = false;
            if (_overfillVisual != null)
                _overfillVisual.SetActive(false);
        }
    }
}
