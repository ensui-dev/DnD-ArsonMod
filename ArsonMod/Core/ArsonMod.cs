using MelonLoader;
using HarmonyLib;
using UnityEngine;
using Il2CppGameManagement;
using Il2CppGameManagement.StateMachine;

[assembly: MelonInfo(typeof(ArsonMod.Core.ArsonModEntry), "Arson Mode", "1.0.0", "ensui-dev")]
[assembly: MelonGame("StripedPandaStudios", "DDSS")]

namespace ArsonMod.Core
{
    public class ArsonModEntry : MelonMod
    {
        public static ArsonModEntry Instance { get; private set; }

        public ArsonSettings Settings { get; private set; }
        public Fire.FireManager FireManager { get; private set; }
        public Tasks.ArsonTaskChain TaskChain { get; private set; }
        public Clues.ProximityTracker ProximityTracker { get; private set; }
        public NetworkSync NetworkSync { get; private set; }

        private bool _arsonModeActive;
        private bool _pendingArsonistSelection;
        private float _selectionDelay;
        private float _heartbeatTimer;

        public bool IsArsonModeActive => _arsonModeActive;

        public override void OnInitializeMelon()
        {
            Instance = this;
            Settings = new ArsonSettings();
            NetworkSync = new NetworkSync();
            FileLogger.Initialize();

            LoggerInstance.Msg("Arson Mode mod loaded.");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            FileLogger.Log($"OnSceneWasLoaded: {sceneName} (active={_arsonModeActive})");

            if (!_arsonModeActive)
                return;

            FireManager = new Fire.FireManager(Settings);
            FireManager.OnArsonistWin += OnArsonistWinTriggered;

            TaskChain = new Tasks.ArsonTaskChain();
            ProximityTracker = new Clues.ProximityTracker();

            FileLogger.Log("OnSceneWasLoaded: managers created");
            LoggerInstance.Msg($"Arson Mode initialized for scene: {sceneName}");
        }

        public override void OnUpdate()
        {
            try
            {
                UI.ArsonLobbyUI.PollSettingValues();

                if (!_arsonModeActive)
                    return;

                _heartbeatTimer += Time.deltaTime;
                if (_heartbeatTimer >= 10f)
                {
                    _heartbeatTimer = 0f;
                    FileLogger.Log($"Heartbeat: alive, frame={Time.frameCount}");
                }

                if (_pendingArsonistSelection)
                {
                    _selectionDelay += Time.deltaTime;
                    var players = PlayerAccess.GetAllPlayers();
                    if (players.Count > 0 || _selectionDelay > 5f)
                    {
                        _pendingArsonistSelection = false;
                        RunArsonistSelection(players.Count == 0);
                    }
                }

                Tasks.ArsonTaskInjector.Update(Time.deltaTime);

                FireManager?.Update(Time.deltaTime);
                ProximityTracker?.Update(Time.deltaTime);
            }
            catch (System.Exception ex)
            {
                FileLogger.Error("OnUpdate crashed", ex);
                MelonLogger.Error($"[ArsonMod] OnUpdate exception: {ex}");
            }
        }

        public void BeginArsonistSelection()
        {
            _pendingArsonistSelection = true;
            _selectionDelay = 0f;
        }

        private void RunArsonistSelection(bool timedOut)
        {
            FileLogger.Log($"RunArsonistSelection: timedOut={timedOut}");

            if (timedOut)
                LoggerInstance.Warning("Timed out waiting for player data.");

            var players = PlayerAccess.GetAllPlayers();
            FileLogger.Log($"RunArsonistSelection: {players.Count} players found");
            LoggerInstance.Msg($"[ArsonMod] Running arsonist selection with {players.Count} players.");

            HarmonyPatches.PreArsonistSelection?.Invoke();

            string arsonistId = PlayerAccess.SelectArsonist();

            HarmonyPatches.PostArsonistSelection?.Invoke();

            // Re-check: debug hook may have set arsonist bypassing role requirements
            if (arsonistId == null)
                arsonistId = PlayerAccess.GetArsonistId();

            if (arsonistId == null)
            {
                LoggerInstance.Warning("[ArsonMod] No Slacker players found, arson mode cannot start.");
                return;
            }

            var arsonistIds = PlayerAccess.GetArsonistIds();
            LoggerInstance.Msg($"[ArsonMod] {arsonistIds.Count} arsonist(s) selected.");

            // Initialize all players' arson state
            var allPlayers = PlayerAccess.GetAllPlayers();
            foreach (var p in allPlayers)
                TaskChain.InitializePlayer(p.PlayerId);

            // Notify the local player if they are an arsonist
            var localPlayer = PlayerAccess.GetLocalPlayer();
            if (localPlayer.HasValue && PlayerAccess.IsArsonist(localPlayer.Value.PlayerId))
            {
                var uiManager = HarmonyPatches.CachedUI;
                uiManager?.ShowHint("You are the Arsonist. Complete your tasks to start a fire.", 5f);

                Tasks.ArsonTaskInjector.RequestInjection();
                LoggerInstance.Msg($"[ArsonMod] Arson task injection requested for local arsonist.");
            }
        }

        public void EnableArsonMode(bool enabled)
        {
            _arsonModeActive = enabled;
            LoggerInstance.Msg($"Arson Mode {(enabled ? "enabled" : "disabled")}.");
        }

        public void OnFireIgnited(string roomId)
        {
            FireManager?.IgniteRoom(roomId);
            ProximityTracker?.OnFireStarted(roomId);
            UI.FireNotifications.ShowIgnitionAlert(roomId);

            LoggerInstance.Msg($"Fire ignited in room: {roomId}");
        }

        public void OnMeetingStarted()
        {
            FireManager?.PauseSpread();
        }

        public void OnMeetingEnded(string firedPlayerId)
        {
            FireManager?.ResumeSpread();

            if (firedPlayerId != null && PlayerAccess.IsArsonist(firedPlayerId))
            {
                FireManager?.ExtinguishAll();

                var players = PlayerAccess.GetAllPlayers();
                string arsonistName = firedPlayerId;
                foreach (var p in players)
                {
                    if (p.PlayerId == firedPlayerId)
                    {
                        arsonistName = p.LobbyPlayer.username;
                        break;
                    }
                }
                UI.ArsonWinScreen.ShowArsonistCaught(arsonistName);

                LoggerInstance.Msg("Arsonist was fired! All fires extinguished.");
            }
        }

        private void OnArsonistWinTriggered()
        {
            UI.ArsonWinScreen.ShowArsonistWin();

            // Trigger the game's native end-game state transition.
            // This calls GameFinishedState.Enter() which reveals roles,
            // runs the WaitForWinner coroutine, and shows the end screen.
            var gm = GameManager.instance;
            var currentState = gm?.gameStateMachine?.CurrentState;
            if (currentState != null)
            {
                FileLogger.Log("OnArsonistWinTriggered: transitioning to GameFinished state");
                currentState.ChangeState(GameStates.GameFinished);
            }
            else
            {
                FileLogger.Log("OnArsonistWinTriggered: could not transition — GameManager/StateMachine null");
            }
        }
    }
}
