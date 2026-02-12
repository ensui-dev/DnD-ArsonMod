using UnityEngine;
using MelonLoader;
using Il2CppPlayer.Tasks;
using Il2Cpp;
using Il2CppRoom;
using Il2CppProps.FireAlarm;
using Il2CppProps.Scripts;
using Il2CppMirror;
using Il2CppInterop.Runtime;

namespace ArsonMod.Tasks
{
    /// <summary>
    /// Central arson task manager: resolves current task, injects native
    /// InteractionAlternatives into game objects for tasks 1-4 (Interactable
    /// targets like Printer, ShelfController, TrashBin), and handles fire
    /// alarm interaction for task 0 (FireAlarmController, which is NOT
    /// an Interactable) directly from OnUpdate.
    ///
    /// For Interactable targets, the game's CameraController handles the entire
    /// interaction flow: proximity detection, "F Options" prompt, loading bar,
    /// and completion callback. We just inject an alternative and handle the
    /// completion logic.
    ///
    /// For FireAlarmController (not Interactable), UpdateFireAlarmInteraction()
    /// runs every frame from OnUpdate, raycasts from the player camera, and
    /// injects UI text + loading bar into CameraController's fields — giving
    /// full visual parity without needing a real Interactable component.
    /// (Harmony Postfix on HandleInteraction doesn't work because it's a
    /// private Il2Cpp method called internally via native code, bypassing
    /// the managed wrapper.)
    /// </summary>
    public static class ArsonTaskInjector
    {
        // --- Task resolution state ---
        private static bool _pendingInjection;
        private static float _injectionDelay;
        private static int _lastInjectedTaskIndex = -1;
        private static bool _loggedSceneObjects;

        // --- Native interaction injection state (Printer, ShelfController, TrashBin) ---
        private static InteractionAlternative _injectedAlternative;
        private static Interactable _injectedInteractable;

        // --- Fire alarm interaction state (non-Interactable targets) ---
        private static CameraController _cachedCameraController;
        private static bool _isFireAlarmInteracting;
        private static float _fireAlarmTimer;
        private static float _fireAlarmDuration;
        private static bool _loggedFirstFrame;

        // --- Location marker state ---
        private static bool _hasActiveMarker;
        private static GameObject _arsonMarkerObj;
        private static RectTransform _arsonMarkerRT;
        private static bool _loggedFirstMarkerFrame;

        // --- Room/target continuity state ---
        // Fire alarm room is stored so trash bin tasks target the same room.
        // Target bin name is stored so task 4 reuses the same bin as task 3.
        private static string _fireAlarmRoomId;
        private static string _targetBinName;

        // --- Random target selection (set once per game) ---
        // Ensures each game picks a different fire alarm (and thus room/bin).
        private static int _selectedAlarmIndex = -1;

        /// <summary>
        /// The current arson task name shown in the HUD task list.
        /// Null when no arson task should be displayed.
        /// Read by the HUDTab.LateUpdate Harmony patch.
        /// </summary>
        public static string CurrentArsonTaskName { get; private set; }

        /// <summary>
        /// The target GameObject for the current arson task.
        /// Used for proximity checks and fire triggering.
        /// </summary>
        public static GameObject CurrentTargetObject { get; private set; }

        /// <summary>Whether the player is currently performing an interaction.</summary>
        public static bool IsInteracting => _isFireAlarmInteracting;

        /// <summary>Whether the current target uses the game's native interaction system.</summary>
        public static bool IsNativeInteraction => _injectedInteractable != null;

        public static void RequestInjection()
        {
            _pendingInjection = true;
            _injectionDelay = 0f;
        }

        public static void Update(float deltaTime)
        {
            // Handle pending injection (deferred to let game state settle)
            if (_pendingInjection)
            {
                _injectionDelay += deltaTime;
                if (_injectionDelay < 0.5f) return;
                _pendingInjection = false;
                ResolveCurrentArsonTask();
            }

            // Fire alarm interaction runs from OnUpdate because CameraController.HandleInteraction
            // is a private Il2Cpp method — internal native calls bypass the managed wrapper, so
            // Harmony Postfix patches on it never fire. We do the same raycast + UI injection
            // logic here instead, using the CameraController's own fields.
            if (CurrentTargetObject != null && !IsNativeInteraction)
            {
                UpdateFireAlarmInteraction(deltaTime);
            }
        }

        // =================================================================
        // FIRE ALARM INTERACTION (for non-Interactable targets)
        // State management + F key detection runs here (OnUpdate).
        // Visual UI injection is in InjectFireAlarmUI(), called from a
        // Harmony Postfix on CameraController.Update — this ensures our
        // text overrides the game's ClearInteractionText, regardless of
        // execution order between MonoBehaviour Updates.
        // =================================================================

        private static void UpdateFireAlarmInteraction(float deltaTime)
        {
            try
            {
                // One-time confirmation log
                if (!_loggedFirstFrame)
                {
                    _loggedFirstFrame = true;
                    MelonLogger.Msg("[ArsonMod] Fire alarm interaction running from OnUpdate (active)");
                    Core.FileLogger.Log("FireAlarmInteraction: First frame reached");
                }

                // During active loading bar, keep updating timer/state
                if (_isFireAlarmInteracting)
                {
                    UpdateActiveFireAlarmInteraction(deltaTime);
                    return;
                }

                var localPlayer = Core.PlayerAccess.GetLocalPlayer();
                if (!localPlayer.HasValue) return;

                string playerId = localPlayer.Value.PlayerId;
                if (!Core.PlayerAccess.IsArsonist(playerId)) return;

                var taskDef = GetCurrentTaskDef(playerId);
                if (taskDef == null) return;

                if (taskDef.RequiresItem != null && !Core.PlayerInventory.HasItem(playerId, taskDef.RequiresItem))
                    return;

                // Get or cache CameraController
                if (_cachedCameraController == null)
                    _cachedCameraController = Object.FindObjectOfType<CameraController>();
                if (_cachedCameraController == null) return;

                var ctrl = _cachedCameraController;

                // Proximity + look direction check (same logic as InjectFireAlarmUI)
                var cam = ctrl.mainCamera;
                if (cam == null) cam = Camera.main;
                if (cam == null) return;

                float playerDist = Vector3.Distance(localPlayer.Value.Position, CurrentTargetObject.transform.position);
                if (playerDist > 4f) return;

                Vector3 dirToTarget = (CurrentTargetObject.transform.position - cam.transform.position).normalized;
                float dot = Vector3.Dot(cam.transform.forward, dirToTarget);
                if (dot < 0.7f) return;

                // Clear game's interactable to prevent competing interactions
                ctrl.lastInteractable = null;

                // Handle F key press to start interaction
                // (UI text is injected by InjectFireAlarmUI in CameraController.Update Postfix)
                if (Input.GetKeyDown(KeyCode.F))
                {
                    StartFireAlarmInteraction(ctrl, taskDef);
                }
            }
            catch (System.Exception ex)
            {
                Core.FileLogger.Error("UpdateFireAlarmInteraction crashed", ex);
            }
        }

        private static void StartFireAlarmInteraction(CameraController ctrl, ArsonTaskDefinition taskDef)
        {
            _isFireAlarmInteracting = true;
            _fireAlarmTimer = 0f;
            _fireAlarmDuration = taskDef.AnimationDuration;

            if (ctrl.loadingBar != null)
            {
                ctrl.loadingBar.gameObject.SetActive(true);
                ctrl.loadingBar.SetProgress(0f);
            }

            Core.FileLogger.Log($"FireAlarm: Interaction started (duration={_fireAlarmDuration}s)");
            MelonLogger.Msg($"[ArsonMod] Started fire alarm interaction: {taskDef.ArsonistTaskName}");
        }

        private static void UpdateActiveFireAlarmInteraction(float deltaTime)
        {
            _fireAlarmTimer += deltaTime;
            float progress = _fireAlarmTimer / _fireAlarmDuration;

            if (_cachedCameraController == null)
            {
                CancelFireAlarmInteraction();
                return;
            }

            // Keep clearing the game's interactable during our interaction
            _cachedCameraController.lastInteractable = null;

            // Cancel if player walks away
            var localPlayer = Core.PlayerAccess.GetLocalPlayer();
            if (localPlayer.HasValue)
            {
                float dist = Vector3.Distance(localPlayer.Value.Position, CurrentTargetObject.transform.position);
                if (dist > 5f)
                {
                    CancelFireAlarmInteraction();
                    return;
                }
            }

            // Complete
            if (progress >= 1f)
            {
                if (_cachedCameraController.loadingBar != null)
                    _cachedCameraController.loadingBar.gameObject.SetActive(false);

                _isFireAlarmInteracting = false;
                _fireAlarmTimer = 0f;

                Core.FileLogger.Log("FireAlarm: Interaction complete");
                OnFireAlarmInteractionComplete();
            }
            // Note: loading bar visuals + text are managed by InjectFireAlarmUI
            // (CameraController.Update Postfix) to ensure they override the game's pipeline
        }

        private static void CancelFireAlarmInteraction()
        {
            _isFireAlarmInteracting = false;
            _fireAlarmTimer = 0f;
            if (_cachedCameraController != null && _cachedCameraController.loadingBar != null)
                _cachedCameraController.loadingBar.gameObject.SetActive(false);
            Core.FileLogger.Log("FireAlarm: Interaction cancelled");
        }

        /// <summary>
        /// Called from Harmony Postfix on CameraController.Update (Patch 12).
        /// Runs AFTER the game's entire HandleInteraction → ClearInteractionText
        /// pipeline, so our UI text overrides the game's clearing.
        /// </summary>
        public static void InjectFireAlarmUI(CameraController ctrl)
        {
            try
            {
                // Only handle non-native (fire alarm) interaction targets
                if (CurrentTargetObject == null || IsNativeInteraction) return;

                // During active loading bar — show progress
                if (_isFireAlarmInteracting)
                {
                    ctrl.lastInteractable = null;
                    float progress = _fireAlarmTimer / _fireAlarmDuration;

                    if (ctrl.loadingBar != null)
                    {
                        if (!ctrl.loadingBar.gameObject.activeSelf)
                            ctrl.loadingBar.gameObject.SetActive(true);
                        ctrl.loadingBar.SetProgress(Mathf.Clamp01(progress));
                    }
                    if (ctrl.interactableName != null)
                    {
                        if (!ctrl.interactableName.gameObject.activeSelf)
                            ctrl.interactableName.gameObject.SetActive(true);
                        ctrl.interactableName.text = "Smoke Detector";
                    }
                    if (ctrl.interactText != null)
                    {
                        if (ctrl.interactText.gameObject.activeSelf)
                            ctrl.interactText.gameObject.SetActive(false);
                    }
                    return;
                }

                // Not interacting — check proximity + look to show "[F] Jam Detector" prompt
                var localPlayer = Core.PlayerAccess.GetLocalPlayer();
                if (!localPlayer.HasValue) return;

                string playerId = localPlayer.Value.PlayerId;
                if (!Core.PlayerAccess.IsArsonist(playerId)) return;

                var taskDef = GetCurrentTaskDef(playerId);
                if (taskDef == null) return;

                if (taskDef.RequiresItem != null && !Core.PlayerInventory.HasItem(playerId, taskDef.RequiresItem))
                    return;

                var cam = ctrl.mainCamera;
                if (cam == null) cam = Camera.main;
                if (cam == null) return;

                float dist = Vector3.Distance(localPlayer.Value.Position, CurrentTargetObject.transform.position);
                if (dist > 4f) return;

                Vector3 dirToTarget = (CurrentTargetObject.transform.position - cam.transform.position).normalized;
                float dot = Vector3.Dot(cam.transform.forward, dirToTarget);
                if (dot < 0.7f) return;

                // In range + looking at target — inject prompt UI
                // Guard SetActive calls with activeSelf checks to avoid triggering
                // OnEnable animations/sounds every frame (game's ClearInteractionText
                // may deactivate these between our Postfix calls).
                ctrl.lastInteractable = null;
                if (ctrl.interactableName != null)
                {
                    if (!ctrl.interactableName.gameObject.activeSelf)
                        ctrl.interactableName.gameObject.SetActive(true);
                    ctrl.interactableName.text = taskDef.ArsonistTaskName;
                }
                if (ctrl.interactText != null)
                {
                    if (!ctrl.interactText.gameObject.activeSelf)
                        ctrl.interactText.gameObject.SetActive(true);
                    ctrl.interactText.text = "[F]";
                }
            }
            catch (System.Exception ex)
            {
                Core.FileLogger.Error("InjectFireAlarmUI crashed", ex);
            }
        }

        private static bool IsTargetObject(GameObject hitGO, GameObject target)
        {
            // Direct match
            if (hitGO.Pointer == target.Pointer) return true;

            // Check if hit object is a child of the target
            var parent = hitGO.transform.parent;
            while (parent != null)
            {
                if (parent.gameObject.Pointer == target.Pointer) return true;
                parent = parent.parent;
            }

            // Check if target is a child of the hit object
            if (hitGO.transform == target.transform.root) return true;

            return false;
        }

        // =================================================================
        // NATIVE INTERACTION (for Interactable targets: Printer, ShelfController, TrashBin)
        // =================================================================

        /// <summary>
        /// Injects an InteractionAlternative into the target Interactable's
        /// alternatives list. The game's CameraController will show this option
        /// when the player is near the object, with a native loading bar.
        /// </summary>
        private static void InjectAlternativeIntoTarget(Interactable interactable, ArsonTaskDefinition taskDef)
        {
            RemoveInjectedAlternative();

            try
            {
                // Create Il2Cpp delegate for the action callback
                var actionDelegate = DelegateSupport.ConvertDelegate<
                    UnityEngine.Events.UnityAction<NetworkIdentity, NetworkConnectionToClient>>(
                    new System.Action<NetworkIdentity, NetworkConnectionToClient>((player, conn) =>
                    {
                        OnNativeInteractionComplete();
                    })
                );

                // Create Il2Cpp delegate for the show condition
                var showDelegate = DelegateSupport.ConvertDelegate<Il2CppSystem.Func<bool>>(
                    new System.Func<bool>(() =>
                    {
                        return ShouldShowArsonAlternative();
                    })
                );

                _injectedAlternative = new InteractionAlternative(
                    taskDef.ArsonistTaskName,           // text
                    InteractionPermission.All,           // interactionPermission
                    actionDelegate,                      // action
                    false,                               // duringUse
                    showDelegate,                        // showAlternative
                    taskDef.AnimationDuration,            // duration
                    null,                                // timerCallback
                    null,                                // onStartInteraction
                    true,                                // canAlwaysInteract
                    true                                 // isSabotage
                );

                interactable.alternatives.Add(_injectedAlternative);
                _injectedInteractable = interactable;

                Core.FileLogger.Log($"InjectAlternative: Added '{taskDef.ArsonistTaskName}' to {interactable.gameObject.name} (duration={taskDef.AnimationDuration}s)");
                MelonLogger.Msg($"[ArsonMod] Injected arson alternative into {interactable.gameObject.name}");
            }
            catch (System.Exception ex)
            {
                Core.FileLogger.Error("InjectAlternative: Failed to inject", ex);
                MelonLogger.Error($"[ArsonMod] Failed to inject alternative: {ex.Message}");
                _injectedAlternative = null;
                _injectedInteractable = null;
            }
        }

        /// <summary>
        /// Removes the previously injected alternative from the Interactable.
        /// </summary>
        private static void RemoveInjectedAlternative()
        {
            if (_injectedAlternative != null && _injectedInteractable != null)
            {
                try
                {
                    var alts = _injectedInteractable.alternatives;
                    if (alts != null)
                    {
                        for (int i = alts.Count - 1; i >= 0; i--)
                        {
                            if (alts[i].Pointer == _injectedAlternative.Pointer)
                            {
                                alts.RemoveAt(i);
                                Core.FileLogger.Log("RemoveInjectedAlternative: Removed from alternatives list");
                                break;
                            }
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Core.FileLogger.Error("RemoveInjectedAlternative: Failed", ex);
                }
            }
            _injectedAlternative = null;
            _injectedInteractable = null;
        }

        /// <summary>
        /// Condition function for showAlternative — controls when the arson
        /// option appears in the interaction menu.
        /// </summary>
        private static bool ShouldShowArsonAlternative()
        {
            try
            {
                if (!Core.ArsonModEntry.Instance.IsArsonModeActive) return false;

                var localPlayer = Core.PlayerAccess.GetLocalPlayer();
                if (!localPlayer.HasValue) return false;

                string playerId = localPlayer.Value.PlayerId;
                if (!Core.PlayerAccess.IsArsonist(playerId)) return false;

                var taskDef = GetCurrentTaskDef(playerId);
                if (taskDef == null) return false;

                // Check item prerequisites
                if (taskDef.RequiresItem != null && !Core.PlayerInventory.HasItem(playerId, taskDef.RequiresItem))
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Called by the InteractionAlternative action callback when the game's
        /// native interaction system completes the loading bar.
        /// </summary>
        private static void OnNativeInteractionComplete()
        {
            try
            {
                var localPlayer = Core.PlayerAccess.GetLocalPlayer();
                if (!localPlayer.HasValue) return;

                string playerId = localPlayer.Value.PlayerId;
                var taskDef = GetCurrentTaskDef(playerId);
                if (taskDef == null) return;

                CompleteTask(playerId, taskDef);
            }
            catch (System.Exception ex)
            {
                Core.FileLogger.Error("OnNativeInteractionComplete: Failed", ex);
            }
        }

        /// <summary>
        /// Called when the fire alarm loading bar interaction completes.
        /// </summary>
        public static void OnFireAlarmInteractionComplete()
        {
            try
            {
                var localPlayer = Core.PlayerAccess.GetLocalPlayer();
                if (!localPlayer.HasValue) return;

                string playerId = localPlayer.Value.PlayerId;
                var taskDef = GetCurrentTaskDef(playerId);
                if (taskDef == null) return;

                CompleteTask(playerId, taskDef);
            }
            catch (System.Exception ex)
            {
                Core.FileLogger.Error("OnFireAlarmInteractionComplete: Failed", ex);
            }
        }

        // =================================================================
        // SHARED TASK COMPLETION
        // =================================================================

        private static void CompleteTask(string playerId, ArsonTaskDefinition taskDef)
        {
            var taskChain = Core.ArsonModEntry.Instance.TaskChain;
            if (taskChain == null) return;

            int taskIdx = taskChain.GetProgress(playerId);
            if (taskIdx < 0 || taskIdx >= taskChain.TaskDefinitions.Count) return;

            // Grant item if this task gives one
            if (taskDef.GrantsItem != null)
            {
                Core.PlayerInventory.AddItem(playerId, taskDef.GrantsItem);
                string itemName = FormatItemName(taskDef.GrantsItem);
                Core.FileLogger.Log($"Item acquired: {itemName}");
            }

            // Consume required item
            if (taskDef.RequiresItem != null)
                Core.PlayerInventory.RemoveItem(playerId, taskDef.RequiresItem);

            // Track which bin was used for trash bin tasks.
            // Store the name so task 4 resolves to the SAME bin as task 3.
            string targetBinId = null;
            if (taskDef.LocationType == TaskLocationType.TrashBin && CurrentTargetObject != null)
            {
                targetBinId = CurrentTargetObject.name;
                _targetBinName = targetBinId;
            }

            // Advance the chain
            bool shouldIgnite = taskChain.OnArsonTaskCompleted(playerId, taskIdx, targetBinId);

            Core.FileLogger.Log($"Task {taskIdx} completed: '{taskDef.ArsonistTaskName}', ignite={shouldIgnite}");
            MelonLogger.Msg($"[ArsonMod] Completed task {taskIdx}: {taskDef.ArsonistTaskName}");

            // Task completion is reflected by the HUD task list clearing + next task appearing

            // Handle finale — trigger fire via the game's native system
            if (shouldIgnite)
            {
                TriggerFire(playerId);
            }

            // Advance to next task
            OnCurrentTaskCompleted();
        }

        /// <summary>
        /// Triggers fire on the target TrashBin. First tries the normal
        /// CmdEnableFire path. If CanPutOnFire() returns false (bin not
        /// physically stuffed from the game's perspective — our "stuff bin"
        /// task is conceptual), falls back to calling RpcEnableFire directly
        /// which bypasses the server-side validation and triggers fire
        /// effects + networking on all clients.
        /// </summary>
        private static void TriggerFire(string playerId)
        {
            if (CurrentTargetObject == null) return;

            var trashBin = CurrentTargetObject.GetComponent<Il2CppProps.TrashBin.TrashBin>();
            if (trashBin == null)
            {
                Core.FileLogger.Log("TriggerFire: No TrashBin component on target");
                MelonLogger.Warning("[ArsonMod] Finale target has no TrashBin component!");
                return;
            }

            var localPlayer = Core.PlayerAccess.GetLocalPlayer();
            if (!localPlayer.HasValue) return;

            var playerNetId = localPlayer.Value.LobbyPlayer.playerController;
            if (playerNetId == null)
            {
                Core.FileLogger.Log("TriggerFire: No playerController NetworkIdentity");
                return;
            }

            string roomId = Core.PlayerAccess.GetRoomForPosition(
                CurrentTargetObject.transform.position) ?? "Unknown";

            bool canFire = trashBin.CanPutOnFire();
            Core.FileLogger.Log($"TriggerFire: CanPutOnFire={canFire}, isOnFire={trashBin.isOnFire}, room={roomId}");

            if (canFire)
            {
                // Normal path — CmdEnableFire goes through server validation + broadcasts RPC
                trashBin.CmdEnableFire(playerNetId, true);
                Core.FileLogger.Log($"TriggerFire: Used CmdEnableFire in room: {roomId}");
            }
            else
            {
                // Bypass — bin isn't physically stuffed from the game's perspective.
                // Our arson task chain is conceptual; the actual CollectibleHolder state
                // wasn't modified. Call RpcEnableFire directly to trigger fire visuals
                // and broadcast to all clients (works because we're the host/server).
                trashBin.RpcEnableFire(playerNetId, true);
                Core.FileLogger.Log($"TriggerFire: Bypassed via RpcEnableFire in room: {roomId}");
            }

            MelonLogger.Msg($"[ArsonMod] FIRE TRIGGERED in {roomId}!");

            Core.HarmonyPatches.CigaretteSmokePatch.Reset();
        }

        // =================================================================
        // TASK RESOLUTION
        // =================================================================

        private static void ResolveCurrentArsonTask()
        {
            Core.FileLogger.Log("Resolve: start");

            var localPlayer = Core.PlayerAccess.GetLocalPlayer();
            if (!localPlayer.HasValue) return;

            string playerId = localPlayer.Value.PlayerId;
            if (!Core.PlayerAccess.IsArsonist(playerId)) return;

            var taskChain = Core.ArsonModEntry.Instance.TaskChain;
            if (taskChain == null) return;

            int nextIdx = taskChain.GetProgress(playerId);
            Core.FileLogger.Log($"Resolve: progress={nextIdx}, lastResolved={_lastInjectedTaskIndex}");

            if (nextIdx < 0 || nextIdx >= taskChain.TaskDefinitions.Count)
            {
                // All tasks complete — clear the UI
                CurrentArsonTaskName = null;
                CurrentTargetObject = null;
                RemoveInjectedAlternative();
                Core.FileLogger.Log("Resolve: all tasks complete");
                return;
            }

            if (nextIdx == _lastInjectedTaskIndex) return;

            var taskDef = taskChain.TaskDefinitions[nextIdx];
            if (!UI.ArsonLobbyUI.IsArsonTaskEnabled(nextIdx)) return;

            // Log scene objects once for debugging
            if (!_loggedSceneObjects)
            {
                LogSceneObjectDiscovery();
                _loggedSceneObjects = true;
            }

            // Find the target game object
            GameObject targetObj = FindTargetObject(taskDef.LocationType);
            Core.FileLogger.Log($"Resolve: target={(targetObj != null ? targetObj.name : "null")}");

            // Set managed state
            CurrentArsonTaskName = taskDef.ArsonistTaskName;
            CurrentTargetObject = targetObj;
            _lastInjectedTaskIndex = nextIdx;

            // Store the fire alarm's room so trash bin tasks target the same room
            if (taskDef.LocationType == TaskLocationType.SmokeDetector && targetObj != null)
            {
                _fireAlarmRoomId = Core.PlayerAccess.GetRoomForPosition(targetObj.transform.position);
                Core.FileLogger.Log($"Resolve: Fire alarm at {targetObj.transform.position}, room stored: {_fireAlarmRoomId ?? "null"}");
            }

            // Inject interaction depending on whether the target has an Interactable
            if (targetObj != null)
            {
                var interactable = targetObj.GetComponent<Interactable>();

                if (interactable != null)
                {
                    // Native path: Printer, ShelfController, TrashBin
                    InjectAlternativeIntoTarget(interactable, taskDef);
                    Core.FileLogger.Log($"Resolve: Native interaction set up for '{taskDef.ArsonistTaskName}' on {targetObj.name}");
                }
                else
                {
                    // Non-Interactable path: FireAlarmController
                    // UpdateFireAlarmInteraction() runs every frame from OnUpdate and handles
                    // raycast detection, UI injection, and loading bar
                    Core.FileLogger.Log($"Resolve: Fire alarm path — OnUpdate will handle interaction for '{taskDef.ArsonistTaskName}' on {targetObj.name}");
                    MelonLogger.Msg($"[ArsonMod] Fire alarm task: OnUpdate interaction handler active");
                }
            }

            // Show location marker on the target object
            if (targetObj != null)
                ShowTaskMarker(targetObj, taskDef.ArsonistTaskName);

            // Task is shown persistently in the HUD list via Patch 11 — no popup needed

            Core.FileLogger.Log($"Resolve: task '{CurrentArsonTaskName}' now active, target={targetObj?.name ?? "null"}");
            MelonLogger.Msg($"[ArsonMod] Arson task activated: {CurrentArsonTaskName}");
        }

        // =================================================================
        // LOCATION MARKERS
        // =================================================================

        /// <summary>
        /// Creates a UI marker under the game's Canvas, styled to match the
        /// game's native task markers. Copies font/size from the game's
        /// staticHighlightObjectPrefab for visual parity.
        ///
        /// Positioned each frame by UpdateMarkerPosition() via WorldToScreenPoint.
        /// </summary>
        private static void ShowTaskMarker(GameObject target, string label)
        {
            RemoveTaskMarker();

            try
            {
                var highlighter = TaskHighlighter.instance;
                if (highlighter == null || highlighter.parent == null)
                {
                    MelonLogger.Warning("[ArsonMod] ShowTaskMarker: No TaskHighlighter, cannot create marker");
                    return;
                }

                var canvas = highlighter.parent.GetComponentInParent<Canvas>();
                if (canvas == null)
                {
                    MelonLogger.Warning("[ArsonMod] ShowTaskMarker: No Canvas found");
                    return;
                }

                // Create marker GO with RectTransform + TMP text
                _arsonMarkerObj = new GameObject("ArsonTaskMarker");
                _arsonMarkerObj.transform.SetParent(canvas.transform, false);

                _arsonMarkerRT = _arsonMarkerObj.AddComponent<RectTransform>();
                _arsonMarkerRT.sizeDelta = new Vector2(300, 40);

                var tmp = _arsonMarkerObj.AddComponent<Il2CppTMPro.TextMeshProUGUI>();
                tmp.text = $"▼ {label}";
                tmp.alignment = Il2CppTMPro.TextAlignmentOptions.Center;
                tmp.color = new Color(1f, 0.42f, 0.21f); // Arson orange
                tmp.enableWordWrapping = false;
                tmp.overflowMode = Il2CppTMPro.TextOverflowModes.Overflow;
                tmp.raycastTarget = false;

                // Copy font from the game's highlight prefab for native styling
                var prefab = highlighter.staticHighlightObjectPrefab;
                if (prefab?.objectName != null)
                {
                    tmp.font = prefab.objectName.font;
                    tmp.fontSize = prefab.objectName.fontSize;
                    Core.FileLogger.Log($"ShowTaskMarker: Copied font '{tmp.font?.name}' size={tmp.fontSize} from game prefab");
                }
                else
                {
                    tmp.fontSize = 14;
                    Core.FileLogger.Log("ShowTaskMarker: No game prefab font found, using fallback size=14");
                }

                _arsonMarkerObj.SetActive(true);
                _hasActiveMarker = true;
                _loggedFirstMarkerFrame = false;

                Core.FileLogger.Log($"ShowTaskMarker: Created marker '{label}' on {target.name}");
                MelonLogger.Msg($"[ArsonMod] Location marker shown: {label}");
            }
            catch (System.Exception ex)
            {
                Core.FileLogger.Error("ShowTaskMarker: Failed", ex);
                MelonLogger.Error($"[ArsonMod] ShowTaskMarker failed: {ex.Message}");
            }
        }

        private static void RemoveTaskMarker()
        {
            if (!_hasActiveMarker && _arsonMarkerObj == null) return;

            try
            {
                if (_arsonMarkerObj != null)
                {
                    _arsonMarkerObj.SetActive(false);
                    Object.Destroy(_arsonMarkerObj);
                }
            }
            catch (System.Exception ex)
            {
                Core.FileLogger.Error("RemoveTaskMarker: Failed", ex);
            }

            _arsonMarkerObj = null;
            _arsonMarkerRT = null;
            _hasActiveMarker = false;
        }

        /// <summary>
        /// Called every frame from Patch 12 (TaskHighlighter.Update Postfix).
        /// Positions our custom marker on screen using WorldToScreenPoint.
        /// </summary>
        public static void UpdateMarkerPosition()
        {
            if (!_hasActiveMarker) return;

            if (_arsonMarkerObj == null)
            {
                MelonLogger.Warning("[ArsonMod] Marker was destroyed by game — clearing state");
                _hasActiveMarker = false;
                _arsonMarkerObj = null;
                _arsonMarkerRT = null;
                return;
            }

            try
            {
                var target = CurrentTargetObject;

                if (!_loggedFirstMarkerFrame)
                {
                    _loggedFirstMarkerFrame = true;
                    MelonLogger.Msg($"[ArsonMod] Marker positioning active: target={(target != null ? target.name : "null")}");
                }

                if (target == null) return;

                var cam = Camera.main;
                if (cam == null) return;

                Vector3 worldPos = target.transform.position + Vector3.up * 0.5f;
                Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

                if (screenPos.z <= 0)
                {
                    if (_arsonMarkerObj.activeSelf)
                        _arsonMarkerObj.SetActive(false);
                    return;
                }

                if (!_arsonMarkerObj.activeSelf)
                    _arsonMarkerObj.SetActive(true);

                if (_arsonMarkerRT != null)
                    _arsonMarkerRT.position = new Vector3(screenPos.x, screenPos.y, 0);
            }
            catch (System.Exception ex)
            {
                Core.FileLogger.Error("UpdateMarkerPosition: Failed", ex);
            }
        }

        // =================================================================
        // HELPERS
        // =================================================================

        /// <summary>
        /// Gets the current task definition for a player based on their chain progress.
        /// Public so Patch 12 can read the task definition for fire alarm interactions.
        /// </summary>
        public static ArsonTaskDefinition GetCurrentTaskDef(string playerId)
        {
            var taskChain = Core.ArsonModEntry.Instance.TaskChain;
            if (taskChain == null) return null;
            int idx = taskChain.GetProgress(playerId);
            if (idx < 0 || idx >= taskChain.TaskDefinitions.Count) return null;
            return taskChain.TaskDefinitions[idx];
        }

        private static string FormatItemName(string itemId)
        {
            return itemId switch
            {
                "paper_stack" => "printed documents",
                "lighter_fluid" => "lighter fluid",
                _ => itemId,
            };
        }

        public static void OnCurrentTaskCompleted()
        {
            _lastInjectedTaskIndex = -1;
            RemoveTaskMarker();
            RemoveInjectedAlternative();
            ResetFireAlarmInteraction();
            CurrentArsonTaskName = null;
            CurrentTargetObject = null;
            // Don't call RequestInjection() here — the next arson task will appear
            // when the game's ServerGiveNewTasks fires (Patch 8), matching the
            // original task flow where new tasks appear with the next task batch.
        }

        private static GameObject FindTargetObject(TaskLocationType locType)
        {
            switch (locType)
            {
                case TaskLocationType.Printer:
                    var printer = Il2CppProps.Printer.Printer.instance;
                    if (printer != null) return printer.gameObject;
                    break;

                case TaskLocationType.TrashBin:
                    return FindTrashBinTarget();

                case TaskLocationType.SmokeDetector:
                    var alarms = Object.FindObjectsOfType<FireAlarmController>();
                    if (alarms != null && alarms.Count > 0)
                    {
                        // Log all available alarms for diagnostics
                        for (int i = 0; i < alarms.Count; i++)
                        {
                            string alarmRoom = Core.PlayerAccess.GetRoomForPosition(alarms[i].transform.position);
                            MelonLogger.Msg($"[ArsonMod] FireAlarm[{i}]: '{alarms[i].gameObject.name}' in room '{alarmRoom}' at {alarms[i].transform.position}");
                        }

                        if (_selectedAlarmIndex < 0 || _selectedAlarmIndex >= alarms.Count)
                        {
                            // Use GUID hash directly — Il2Cpp's System.Random.Next()
                            // is broken for small ranges (produces 0 regardless of seed).
                            // Guid.NewGuid() is crypto-random, so its hash has good distribution.
                            int hash = System.Guid.NewGuid().GetHashCode();
                            _selectedAlarmIndex = ((hash % alarms.Count) + alarms.Count) % alarms.Count;
                            string selectedRoom = Core.PlayerAccess.GetRoomForPosition(alarms[_selectedAlarmIndex].transform.position);
                            MelonLogger.Msg($"[ArsonMod] Selected fire alarm {_selectedAlarmIndex}/{alarms.Count} (hash={hash}): '{alarms[_selectedAlarmIndex].gameObject.name}' in room '{selectedRoom}'");
                            Core.FileLogger.Log($"FindTarget: Selected alarm index {_selectedAlarmIndex} of {alarms.Count} (hash={hash}) in room '{selectedRoom}'");
                        }
                        return alarms[_selectedAlarmIndex].gameObject;
                    }
                    break;

                case TaskLocationType.SupplyCloset:
                    var shelves = Object.FindObjectsOfType<Il2CppProps.Shelf.ShelfController>();
                    if (shelves != null && shelves.Count > 0) return shelves[0].gameObject;
                    break;
            }
            return null;
        }

        /// <summary>
        /// Finds the trash bin for tasks 3 and 4.
        /// - Task 4 reuses the exact same bin as task 3 (by stored name).
        /// - Task 3 picks a bin in the same room as the fire alarm (task 0).
        /// - Fallback: first bin found.
        /// </summary>
        private static GameObject FindTrashBinTarget()
        {
            var bins = Object.FindObjectsOfType<Il2CppProps.TrashBin.TrashBin>();
            if (bins == null || bins.Count == 0) return null;

            // Task 4: reuse the exact same bin as task 3
            if (_targetBinName != null)
            {
                foreach (var bin in bins)
                {
                    if (bin.gameObject.name == _targetBinName)
                    {
                        Core.FileLogger.Log($"FindTrashBin: Matched task 3 bin '{_targetBinName}'");
                        return bin.gameObject;
                    }
                }
                Core.FileLogger.Log($"FindTrashBin: WARNING — task 3 bin '{_targetBinName}' not found, falling through");
            }

            // Task 3: find a bin in the same room as the fire alarm
            if (_fireAlarmRoomId != null)
            {
                foreach (var bin in bins)
                {
                    string binRoom = Core.PlayerAccess.GetRoomForPosition(bin.transform.position);
                    if (binRoom == _fireAlarmRoomId)
                    {
                        Core.FileLogger.Log($"FindTrashBin: Matched bin '{bin.gameObject.name}' in fire alarm room '{_fireAlarmRoomId}'");
                        return bin.gameObject;
                    }
                }
                Core.FileLogger.Log($"FindTrashBin: No bin in fire alarm room '{_fireAlarmRoomId}', falling back");
            }

            // Fallback
            Core.FileLogger.Log($"FindTrashBin: Using fallback bin '{bins[0].gameObject.name}'");
            return bins[0].gameObject;
        }

        private static void LogSceneObjectDiscovery()
        {
            MelonLogger.Msg("[ArsonMod] === Scene Object Discovery ===");

            var rooms = Object.FindObjectsOfType<RoomTrigger>();
            if (rooms != null)
            {
                MelonLogger.Msg($"[ArsonMod] RoomTriggers: {rooms.Count}");
                foreach (var room in rooms)
                    if (room?.currentRoom != null)
                        MelonLogger.Msg($"[ArsonMod]   Room: '{room.currentRoom.roomName}' at {room.transform.position}");
            }

            var shelves = Object.FindObjectsOfType<Il2CppProps.Shelf.ShelfController>();
            if (shelves != null && shelves.Count > 0)
            {
                MelonLogger.Msg($"[ArsonMod] ShelfControllers: {shelves.Count}");
                foreach (var shelf in shelves)
                    MelonLogger.Msg($"[ArsonMod]   Shelf: '{shelf.gameObject.name}' at {shelf.transform.position}");
            }

            var printer = Il2CppProps.Printer.Printer.instance;
            MelonLogger.Msg($"[ArsonMod] Printer.instance: {(printer != null ? printer.gameObject.name : "null")}");

            var bins = Object.FindObjectsOfType<Il2CppProps.TrashBin.TrashBin>();
            if (bins != null)
            {
                MelonLogger.Msg($"[ArsonMod] TrashBins: {bins.Count}");
                foreach (var bin in bins)
                    MelonLogger.Msg($"[ArsonMod]   TrashBin: '{bin.gameObject.name}' at {bin.transform.position}");
            }

            var alarms = Object.FindObjectsOfType<FireAlarmController>();
            if (alarms != null && alarms.Count > 0)
            {
                MelonLogger.Msg($"[ArsonMod] FireAlarmControllers: {alarms.Count}");
                foreach (var alarm in alarms)
                    MelonLogger.Msg($"[ArsonMod]   FireAlarm: '{alarm.gameObject.name}' at {alarm.transform.position}");
            }

            MelonLogger.Msg("[ArsonMod] === End Scene Object Discovery ===");
        }

        private static void ResetFireAlarmInteraction()
        {
            _isFireAlarmInteracting = false;
            _fireAlarmTimer = 0f;
            _fireAlarmDuration = 0f;
            _cachedCameraController = null;
            _loggedFirstFrame = false;
        }

        public static void Reset()
        {
            _pendingInjection = false;
            _injectionDelay = 0f;
            _lastInjectedTaskIndex = -1;
            _loggedSceneObjects = false;
            _fireAlarmRoomId = null;
            _targetBinName = null;
            _selectedAlarmIndex = -1;
            _loggedFirstMarkerFrame = false;
            RemoveTaskMarker();
            RemoveInjectedAlternative();
            ResetFireAlarmInteraction();
            CurrentArsonTaskName = null;
            CurrentTargetObject = null;
        }
    }
}
