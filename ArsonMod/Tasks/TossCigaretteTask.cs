using UnityEngine;
using MelonLoader;

namespace ArsonMod.Tasks
{
    /// <summary>
    /// Task 5 (FINALE): Drop a lit cigarette near a paper-stuffed trash bin.
    ///
    /// Flow:
    /// 1. Player picks up a CigarettePack (game collectible)
    /// 2. Uses CigarettePack.CmdSmokeCigarette() to get a Cigarette
    /// 3. Uses Cigarette.CmdSmokeCigarette() to light it (tracked by Patch 10)
    /// 4. Walks near a stuffed trash bin (within interaction range)
    /// 5. Mod detects proximity + lit cigarette + stuffed bin → shows prompt
    /// 6. Calls TrashBin.CmdEnableFire() to use the game's native fire animation
    /// </summary>
    public class TossCigaretteTask : MonoBehaviour
    {
        public string BinId;

        private StuffTrashBinTask _linkedBin;
        private Il2CppProps.TrashBin.TrashBin _gameBin;
        private float _interactionRange;
        private bool _promptShown;
        private float _promptCooldown;

        private void Awake()
        {
            _linkedBin = GetComponent<StuffTrashBinTask>();
            _gameBin = GetComponent<Il2CppProps.TrashBin.TrashBin>();

            // Get interaction range from the game's Interactable base class
            var interactable = GetComponent<Il2CppProps.Scripts.Interactable>();
            _interactionRange = interactable != null ? interactable.maxInteractionDistance : 2.5f;
        }

        /// <summary>
        /// Checks if the finale can trigger:
        /// 1. Bin must be stuffed (Task 4 completed)
        /// 2. Player must be the arsonist
        /// 3. Arsonist must have a lit cigarette (tracked by CigaretteSmokePatch)
        /// 4. Player must have completed tasks 0-3
        /// </summary>
        public bool CanTrigger(string playerId)
        {
            if (_linkedBin == null || !_linkedBin.IsStuffed)
                return false;

            if (!Core.PlayerAccess.IsArsonist(playerId))
                return false;

            var taskChain = Core.ArsonModEntry.Instance.TaskChain;
            if (taskChain == null || taskChain.GetProgress(playerId) < 4)
                return false;

            if (!Core.HarmonyPatches.CigaretteSmokePatch.ArsonistHasLitCigarette)
                return false;

            return true;
        }

        private void Update()
        {
            _promptCooldown -= Time.deltaTime;

            var localPlayer = Core.PlayerAccess.GetLocalPlayer();
            if (!localPlayer.HasValue) return;

            string playerId = localPlayer.Value.PlayerId;
            if (!CanTrigger(playerId)) return;

            // Check proximity to this bin
            float distance = Vector3.Distance(
                localPlayer.Value.Position,
                transform.position
            );

            if (distance <= _interactionRange)
            {
                if (!_promptShown && _promptCooldown <= 0f)
                {
                    var ui = Core.ArsonModEntry.Instance.FireManager?.CachedUI;
                    ui?.ShowHint("Press [E] near the trash bin to drop your cigarette...", 3f);
                    _promptShown = true;
                    _promptCooldown = 4f;
                }
            }
            else
            {
                _promptShown = false;
            }
        }

        /// <summary>
        /// Called when the arsonist confirms the cigarette drop near the stuffed bin.
        /// Triggers the game's native fire animation via TrashBin.CmdEnableFire.
        /// </summary>
        public void TriggerFire(string playerId)
        {
            if (!CanTrigger(playerId)) return;
            if (_gameBin == null)
            {
                MelonLogger.Warning("[ArsonMod] No TrashBin component found on this bin.");
                return;
            }

            // Get the local player's NetworkIdentity for the CmdEnableFire call
            var localPlayer = Core.PlayerAccess.GetLocalPlayer();
            if (!localPlayer.HasValue) return;

            var playerNetId = localPlayer.Value.LobbyPlayer.playerController;
            if (playerNetId == null) return;

            // Use the game's native fire system
            _gameBin.CmdEnableFire(playerNetId, true);

            // Mark arson chain as complete
            var taskChain = Core.ArsonModEntry.Instance.TaskChain;
            bool shouldIgnite = taskChain.OnArsonTaskCompleted(playerId, 4, BinId);

            if (shouldIgnite)
            {
                string roomId = Core.PlayerAccess.GetRoomForPosition(transform.position) ?? "Unknown";
                Core.ArsonModEntry.Instance.OnFireIgnited(roomId);
            }

            // Reset the lit cigarette state
            Core.HarmonyPatches.CigaretteSmokePatch.Reset();

            MelonLogger.Msg($"[ArsonMod] Cigarette dropped in bin {BinId}, fire triggered!");
        }
    }
}
