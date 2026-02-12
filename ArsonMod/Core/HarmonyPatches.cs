using HarmonyLib;
using UnityEngine;
using Il2Cpp;
using Il2CppUMUI;
using Il2CppGameManagement;
using Il2CppGameManagement.StateMachine;
using Il2CppPlayer.Lobby;
using Il2CppPlayer.Tasks;
using Il2CppProps.TrashBin;
using Il2CppProps.FireEx;
using Il2CppRoom;
using MelonLoader;
using Il2CppMirror;


namespace ArsonMod.Core
{
    /// <summary>
    /// All Harmony patches that hook into Dale & Dawson's game systems.
    /// Each patch is a nested class so PatchAll() discovers them.
    /// </summary>
    public static class HarmonyPatches
    {
        private static Il2CppUMUI.UIManager _cachedUi;

        /// <summary>
        /// Hook for debug DLL: called BEFORE arsonist selection so roles can be overridden.
        /// </summary>
        public static System.Action PreArsonistSelection;

        /// <summary>
        /// Hook for debug DLL: called AFTER arsonist selection so the arsonist can be overridden.
        /// </summary>
        public static System.Action PostArsonistSelection;

        /// <summary>Lazily cached UIManager reference shared by all patches.</summary>
        public static Il2CppUMUI.UIManager CachedUI
        {
            get
            {
                if (_cachedUi == null)
                    _cachedUi = Object.FindObjectOfType<Il2CppUMUI.UIManager>();
                return _cachedUi;
            }
        }

        public static void ClearCache() { _cachedUi = null; }

        // =================================================================
        // PATCH 1: GAME START - Initialize arson mode when round begins
        // =================================================================
        [HarmonyPatch(typeof(InGameState), "Enter")]
        public static class InGameStartPatch
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                try
                {
                    if (!ArsonModEntry.Instance.IsArsonModeActive) return;

                    FileLogger.Log("Patch1: InGameState.Enter");
                    MelonLogger.Msg("[ArsonMod] InGame state entered, initializing arson round.");

                    ArsonModEntry.Instance.FireManager?.Initialize();

                    var s = ArsonModEntry.Instance.Settings;
                    MelonLogger.Msg($"[ArsonMod] Settings: FireSpread={s.FireSpreadInterval}s, Extinguish={s.ExtinguishTime}s, RoomsToWin={s.RoomsToWin}, ArsonistCount={s.ArsonistCount}");

                    PlayerAccess.Reset();
                    PlayerInventory.Reset();
                    UI.ArsonLobbyUI.Reset();
                    CigaretteSmokePatch.Reset();
                    Tasks.ArsonTaskInjector.Reset();

                    ArsonModEntry.Instance.BeginArsonistSelection();
                }
                catch (System.Exception ex)
                {
                    FileLogger.Error("Patch1: InGameState.Enter crashed", ex);
                }
            }
        }

        // =================================================================
        // PATCH 2: MEETING START - Pause fire spread during meetings
        // =================================================================
        [HarmonyPatch(typeof(MeetingState), "Enter")]
        public static class MeetingStartPatch
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                try
                {
                    if (!ArsonModEntry.Instance.IsArsonModeActive) return;
                    FileLogger.Log("Patch2: MeetingState.Enter");
                    MelonLogger.Msg("[ArsonMod] Meeting started, pausing fire spread.");
                    ArsonModEntry.Instance.OnMeetingStarted();
                }
                catch (System.Exception ex) { FileLogger.Error("Patch2: MeetingState.Enter crashed", ex); }
            }
        }

        // =================================================================
        // PATCH 3: MEETING END - Resume fire, check if arsonist voted out
        // =================================================================
        [HarmonyPatch(typeof(MeetingState), "Exit")]
        public static class MeetingEndPatch
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                try
                {
                    if (!ArsonModEntry.Instance.IsArsonModeActive) return;
                    FileLogger.Log("Patch3: MeetingState.Exit");
                    MelonLogger.Msg("[ArsonMod] Meeting ended, resuming fire spread.");

                    string firedPlayerId = null;
                    var firedPlayer = GameManager.instance?.latestFiredPlayer;
                    if (firedPlayer != null)
                    {
                        firedPlayerId = firedPlayer.steamID.ToString();
                        MelonLogger.Msg($"[ArsonMod] Player fired: {firedPlayerId}, isArsonist: {PlayerAccess.IsArsonist(firedPlayerId)}");
                    }

                    ArsonModEntry.Instance.OnMeetingEnded(firedPlayerId);
                }
                catch (System.Exception ex) { FileLogger.Error("Patch3: MeetingState.Exit crashed", ex); }
            }
        }

        // =================================================================
        // PATCH 4: GAME FINISHED - Check arson win condition
        // =================================================================
        [HarmonyPatch(typeof(GameFinishedState), "Enter")]
        public static class GameFinishedPatch
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                try
                {
                    if (!ArsonModEntry.Instance.IsArsonModeActive) return;
                    FileLogger.Log("Patch4: GameFinishedState.Enter");

                    // Win condition is handled by FireManager's timer.
                    // This patch just logs the game-end event for diagnostics.
                    var fireManager = ArsonModEntry.Instance.FireManager;
                    if (fireManager != null)
                    {
                        MelonLogger.Msg($"[ArsonMod] Game finished. Burning rooms: {fireManager.BurningRoomCount}");
                    }
                }
                catch (System.Exception ex) { FileLogger.Error("Patch4: GameFinishedState.Enter crashed", ex); }
            }
        }

        // =================================================================
        // PATCH 5: TRASH BIN FIRE - Track when fire starts on a trash bin
        // =================================================================
        [HarmonyPatch(typeof(TrashBin), "RpcEnableFire")]
        public static class TrashBinFirePatch
        {
            [HarmonyPostfix]
            public static void Postfix(TrashBin __instance, NetworkIdentity player, bool b)
            {
                try
                {
                    if (!ArsonModEntry.Instance.IsArsonModeActive) return;

                    string roomId = PlayerAccess.GetRoomForPosition(__instance.transform.position);
                    if (roomId == null) roomId = "Unknown";

                    FileLogger.Log($"Patch5: TrashBin.RpcEnableFire fire={b} room={roomId}");

                    if (b)
                    {
                        MelonLogger.Msg($"[ArsonMod] Fire detected on TrashBin in room: {roomId}");
                        ArsonModEntry.Instance.OnFireIgnited(roomId);
                    }
                    else
                    {
                        MelonLogger.Msg($"[ArsonMod] Fire extinguished on TrashBin in room: {roomId}");

                        var fireManager = ArsonModEntry.Instance.FireManager;
                        fireManager?.ExtinguishRoom(roomId);

                        UI.FireNotifications.ShowExtinguishedAlert(roomId);

                        if (fireManager != null && fireManager.BurningRoomCount == 0)
                        {
                            UI.FireNotifications.ShowAllFiresOutAlert();
                        }
                    }
                }
                catch (System.Exception ex) { FileLogger.Error("Patch5: TrashBin.RpcEnableFire crashed", ex); }
            }
        }

        // =================================================================
        // PATCH 6: FIRE EXTINGUISHER USE - Track extinguisher spraying
        // =================================================================
        [HarmonyPatch(typeof(FireExController), "PlayStream")]
        public static class ExtinguisherUsePatch
        {
            [HarmonyPostfix]
            public static void Postfix(FireExController __instance, NetworkIdentity player)
            {
                try
                {
                    if (!ArsonModEntry.Instance.IsArsonModeActive) return;

                    var position = __instance.transform.position;
                    string roomId = PlayerAccess.GetRoomForPosition(position);

                    var fireManager = ArsonModEntry.Instance.FireManager;
                    if (fireManager != null && roomId != null && fireManager.GetRoomState(roomId) == Fire.FireManager.RoomFireState.Burning)
                    {
                        FileLogger.Log($"Patch6: Extinguisher used in burning room: {roomId}");
                    }
                }
                catch (System.Exception ex) { FileLogger.Error("Patch6: ExtinguisherUse crashed", ex); }
            }
        }

        // =================================================================
        // PATCH 7: LOBBY SETTINGS - Inject arson mode settings into game rules
        // =================================================================
        [HarmonyPatch(typeof(Il2Cpp.LobbySettingsTab), "ShowCategories")]
        public static class LobbySettingsPatch
        {
            [HarmonyPrefix]
            public static void Prefix()
            {
                try
                {
                    UI.ArsonLobbyUI.InjectSettings();
                }
                catch (System.Exception ex) { FileLogger.Error("Patch7: LobbySettings.ShowCategories crashed", ex); }
            }
        }

        // =================================================================
        // PATCH 8: TASK ASSIGNMENT - Show arson objectives after tasks given
        // =================================================================
        [HarmonyPatch(typeof(TaskController), "ServerGiveNewTasks")]
        public static class TaskAssignmentPatch
        {
            [HarmonyPostfix]
            public static void Postfix(TaskController __instance)
            {
                try
                {
                    if (!ArsonModEntry.Instance.IsArsonModeActive) return;

                    var localPlayer = PlayerAccess.GetLocalPlayer();
                    if (!localPlayer.HasValue) return;

                    string playerId = localPlayer.Value.PlayerId;
                    if (!PlayerAccess.IsArsonist(playerId)) return;

                    var taskChain = ArsonModEntry.Instance.TaskChain;
                    if (taskChain == null) return;

                    int nextTaskIdx = taskChain.GetProgress(playerId);
                    FileLogger.Log($"Patch8: ServerGiveNewTasks, arson task idx={nextTaskIdx}");

                    if (nextTaskIdx >= 0 && nextTaskIdx < taskChain.TaskDefinitions.Count)
                    {
                        if (UI.ArsonLobbyUI.IsArsonTaskEnabled(nextTaskIdx))
                        {
                            // Notification is shown by ArsonTaskInjector when it resolves
                            FileLogger.Log($"Patch8: Task assignment detected, requesting injection idx={nextTaskIdx}");
                            Tasks.ArsonTaskInjector.RequestInjection();
                        }
                    }
                }
                catch (System.Exception ex) { FileLogger.Error("Patch8: TaskAssignment crashed", ex); }
            }
        }

        // =================================================================
        // PATCH 9: SUPPRESS StartFireTask - Remove built-in fire task during arson mode
        // =================================================================
        [HarmonyPatch(typeof(TaskController), "ServerInitTaskQueue")]
        public static class SuppressStartFireTaskPatch
        {
            [HarmonyPrefix]
            public static void Prefix(TaskController __instance)
            {
                try
                {
                    if (!ArsonModEntry.Instance.IsArsonModeActive) return;

                    var taskChain = ArsonModEntry.Instance.TaskChain;
                    if (taskChain != null && taskChain.CurrentState == Tasks.ArsonTaskChain.ChainState.Completed)
                        return;

                    var fullList = __instance.fullTaskList;
                    if (fullList == null) return;

                    for (int i = fullList.Count - 1; i >= 0; i--)
                    {
                        var entry = fullList[i];
                        if (entry.Item1 != null && entry.Item1.Name == "StartFireTask")
                        {
                            fullList.RemoveAt(i);
                            FileLogger.Log("Patch9: Suppressed StartFireTask");
                            MelonLogger.Msg("[ArsonMod] Suppressed built-in StartFireTask from task pool.");
                        }
                    }
                }
                catch (System.Exception ex) { FileLogger.Error("Patch9: SuppressStartFireTask crashed", ex); }
            }
        }

        // =================================================================
        // PATCH 10: CIGARETTE TRACKING - Track when arsonist smokes a cigarette
        // =================================================================
        [HarmonyPatch(typeof(Il2CppProps.Smoking.Cigarette), "RPCSmokeCigarette")]
        public static class CigaretteSmokePatch
        {
            public static bool ArsonistHasLitCigarette { get; set; }

            [HarmonyPostfix]
            public static void Postfix(Il2CppProps.Smoking.Cigarette __instance, NetworkIdentity arg0)
            {
                try
                {
                    if (!ArsonModEntry.Instance.IsArsonModeActive) return;

                    var localPlayer = PlayerAccess.GetLocalPlayer();
                    if (!localPlayer.HasValue) return;

                    var lobbyPlayer = arg0?.GetComponent<Il2CppPlayer.Lobby.LobbyPlayer>();
                    if (lobbyPlayer != null && PlayerAccess.IsArsonist(lobbyPlayer))
                    {
                        ArsonistHasLitCigarette = true;
                        FileLogger.Log("Patch10: Arsonist lit cigarette");
                    }
                }
                catch (System.Exception ex) { FileLogger.Error("Patch10: CigaretteSmoke crashed", ex); }
            }

            public static void Reset() { ArsonistHasLitCigarette = false; }
        }

        // =================================================================
        // PATCH 11: ARSON TASK UI - Persistent arson task in task list
        // Runs every frame via LateUpdate so the entry appears as soon as
        // CurrentArsonTaskName is set and survives UpdateTasks rebuilds.
        //
        // Uses the game's taskPrefab for proper visual styling but prevents
        // sound spam by deactivating the prefab before Instantiate — this
        // stops Awake/OnEnable from firing on the clone. AudioSources and
        // Animators are disabled while the clone is still inactive, then
        // the clone is activated. The prefab's active state is immediately
        // restored so the game's own task creation isn't affected.
        // =================================================================
        [HarmonyPatch(typeof(Il2Cpp.HUDTab), "LateUpdate")]
        public static class ArsonTaskUIPatch
        {
            private const string ArsonEntryName = "ArsonMod_TaskEntry";
            private static GameObject _cachedEntry;
            private static Il2CppTMPro.TextMeshProUGUI _cachedText;

            [HarmonyPostfix]
            public static void Postfix(Il2Cpp.HUDTab __instance)
            {
                try
                {
                    if (!ArsonModEntry.Instance.IsArsonModeActive) return;

                    string arsonTaskName = Tasks.ArsonTaskInjector.CurrentArsonTaskName;
                    var grid = __instance.taskGrid;
                    if (grid == null) return;

                    // No arson task active — destroy stale entry if present
                    if (arsonTaskName == null)
                    {
                        if (_cachedEntry != null)
                        {
                            Object.Destroy(_cachedEntry);
                            _cachedEntry = null;
                            _cachedText = null;
                        }
                        return;
                    }

                    // Check if our cached entry still exists
                    // (game's UpdateTasks may destroy all grid children)
                    if (_cachedEntry == null)
                        _cachedText = null;

                    // Entry exists — just update the text
                    if (_cachedEntry != null && _cachedText != null)
                    {
                        _cachedText.text = $"<color=#FF6B35>[Arson]</color> {arsonTaskName}";
                        return;
                    }

                    var prefab = __instance.taskPrefab;
                    if (prefab == null) return;

                    // Instantiate while prefab is inactive to prevent
                    // Awake/OnEnable from firing (which plays sounds)
                    bool wasActive = prefab.activeSelf;
                    prefab.SetActive(false);
                    var go = Object.Instantiate(prefab, grid);
                    prefab.SetActive(wasActive);

                    go.name = ArsonEntryName;

                    // Disable audio components while clone is still inactive
                    var audioSources = go.GetComponentsInChildren<AudioSource>(true);
                    if (audioSources != null)
                        foreach (var audio in audioSources)
                            audio.enabled = false;

                    // Now activate — OnEnable fires but audio/animation are disabled
                    go.SetActive(true);

                    var taskUI = go.GetComponent<Il2CppUI.Tabs.HUDTab.TaskUI>();
                    if (taskUI != null)
                    {
                        _cachedText = taskUI.taskText;
                        if (taskUI.taskText != null)
                            taskUI.taskText.text = $"<color=#FF6B35>[Arson]</color> {arsonTaskName}";

                        if (taskUI.checkMark != null)
                            taskUI.checkMark.SetActive(false);

                        if (taskUI.subtaskPage != null)
                            taskUI.subtaskPage.gameObject.SetActive(false);

                        if (taskUI.taskRoleImage != null)
                            taskUI.taskRoleImage.gameObject.SetActive(false);
                    }

                    _cachedEntry = go;
                }
                catch (System.Exception ex)
                {
                    FileLogger.Error("Patch11: ArsonTaskUI crashed", ex);
                }
            }
        }

        // =================================================================
        // PATCH 12: LOCATION MARKERS - Manual marker positioning
        // The native HighlightObject component is disabled on our marker
        // (enabled=false) because its LateUpdate/PositionUIElementInFront
        // has internal checks that fail for non-Interactable targets (fire
        // alarms). This Postfix runs our own WorldToScreenPoint positioning
        // every frame, giving uniform behavior for all arson target types.
        // =================================================================
        [HarmonyPatch(typeof(Il2Cpp.TaskHighlighter), "Update")]
        public static class TaskHighlighterUpdatePatch
        {
            [HarmonyPostfix]
            public static void Postfix()
            {
                try
                {
                    if (!ArsonModEntry.Instance.IsArsonModeActive) return;
                    Tasks.ArsonTaskInjector.UpdateMarkerPosition();
                }
                catch (System.Exception ex)
                {
                    FileLogger.Error("Patch12: TaskHighlighter.Update crashed", ex);
                }
            }
        }

        // =================================================================
        // PATCH 13: FIRE ALARM UI - Override text AFTER game clears it
        // CameraController.Update() calls HandleInteraction internally
        // (native code), which sets/clears interaction text. Our OnUpdate
        // text injection gets overridden because execution order is
        // non-deterministic. This Postfix runs AFTER the entire pipeline
        // so our text sticks until rendering.
        // =================================================================
        [HarmonyPatch(typeof(Il2Cpp.CameraController), "Update")]
        public static class CameraControllerUpdatePatch
        {
            [HarmonyPostfix]
            public static void Postfix(Il2Cpp.CameraController __instance)
            {
                try
                {
                    if (!ArsonModEntry.Instance.IsArsonModeActive) return;
                    Tasks.ArsonTaskInjector.InjectFireAlarmUI(__instance);
                }
                catch (System.Exception ex)
                {
                    FileLogger.Error("Patch13: CameraController.Update crashed", ex);
                }
            }
        }

    }
}
