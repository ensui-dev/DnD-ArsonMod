using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using MelonLoader;

namespace ArsonMod.Fire
{
    /// <summary>
    /// Central fire state machine. Tracks which rooms are burning,
    /// spreads fire to nearby TrashBins at the configured interval,
    /// and manages the arsonist win timer.
    ///
    /// Fire spread uses the game's native TrashBin fire system —
    /// RpcEnableFire ignites the bin's built-in particles/audio,
    /// and Patch 5 (TrashBinFirePatch) picks up the event to
    /// update room burning state here.
    /// </summary>
    public class FireManager
    {
        public enum RoomFireState
        {
            Safe,
            Burning,
            Extinguished,
        }

        public class RoomState
        {
            public string RoomId;
            public RoomFireState State;
            public float BurnStartTime;
        }

        private readonly Core.ArsonSettings _settings;
        private readonly Dictionary<string, RoomState> _rooms = new();
        private float _spreadTimer;
        private bool _isPaused;
        private bool _fireActive;
        private string _originRoomId;

        // Win condition: once BurningRoomCount >= RoomsToWin, start a
        // countdown. If the countdown reaches ExtinguishTime without
        // fires being put out below threshold, arsonist wins.
        private float _winTimer;
        private bool _winTimerActive;
        private bool _winTriggered;

        public event Action<string> OnRoomIgnited;
        public event Action<string> OnRoomExtinguished;
        public event Action OnAllFiresOut;
        public event Action OnArsonistWin;

        public bool IsFireActive => _fireActive;
        public int BurningRoomCount => _rooms.Values.Count(r => r.State == RoomFireState.Burning);

        private Il2CppUMUI.UIManager _cachedUi;
        public Il2CppUMUI.UIManager CachedUI => _cachedUi;

        public FireManager(Core.ArsonSettings settings)
        {
            _settings = settings;
        }

        public void Initialize()
        {
            RoomAdjacency.Initialize();

            _cachedUi = UnityEngine.Object.FindObjectOfType<Il2CppUMUI.UIManager>();

            _rooms.Clear();
            foreach (var roomId in RoomAdjacency.GetAllRooms())
            {
                _rooms[roomId] = new RoomState
                {
                    RoomId = roomId,
                    State = RoomFireState.Safe,
                };
            }

            _fireActive = false;
            _isPaused = false;
            _spreadTimer = 0f;
            _winTimer = 0f;
            _winTimerActive = false;
            _winTriggered = false;
        }

        public void Update(float deltaTime)
        {
            if (!_fireActive || _isPaused || _winTriggered)
                return;

            // Fire spread timer
            _spreadTimer += deltaTime;
            if (_spreadTimer >= _settings.FireSpreadInterval)
            {
                _spreadTimer = 0f;
                SpreadFire();
            }

            // Win condition timer
            if (BurningRoomCount >= _settings.RoomsToWin)
            {
                if (!_winTimerActive)
                {
                    _winTimerActive = true;
                    _winTimer = 0f;
                    MelonLogger.Msg($"[ArsonMod] Fire reached {BurningRoomCount} rooms! " +
                        $"Arsonist wins in {_settings.ExtinguishTime}s unless fires are extinguished.");
                    Core.FileLogger.Log($"Win timer started: {_settings.ExtinguishTime}s countdown");
                }

                _winTimer += deltaTime;
                if (_winTimer >= _settings.ExtinguishTime)
                {
                    _winTriggered = true;
                    _winTimerActive = false;
                    MelonLogger.Msg("[ArsonMod] ARSONIST WINS — fire not extinguished in time!");
                    Core.FileLogger.Log("Win condition met: arsonist wins");
                    OnArsonistWin?.Invoke();
                }
            }
            else
            {
                // Fires went below threshold — reset win timer
                if (_winTimerActive)
                {
                    _winTimerActive = false;
                    _winTimer = 0f;
                    MelonLogger.Msg("[ArsonMod] Fire count dropped below threshold, win timer reset.");
                    Core.FileLogger.Log("Win timer reset: fires extinguished below threshold");
                }
            }

            ApplyBurningRoomEffects(deltaTime);
        }

        /// <summary>Ignites a room, starting the fire event. Idempotent.</summary>
        public void IgniteRoom(string roomId)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
                return;

            if (room.State == RoomFireState.Burning)
                return; // Already burning

            room.State = RoomFireState.Burning;
            room.BurnStartTime = Time.time;

            if (!_fireActive)
            {
                _fireActive = true;
                _originRoomId = roomId;
            }
            _spreadTimer = 0f;

            OnRoomIgnited?.Invoke(roomId);

            Core.FileLogger.Log($"IgniteRoom: {roomId} now burning ({BurningRoomCount} total)");
        }

        /// <summary>
        /// Spreads fire to a TrashBin in an adjacent room.
        /// Uses the game's native fire system via RpcEnableFire.
        /// </summary>
        private void SpreadFire()
        {
            var candidates = new List<string>();

            foreach (var room in _rooms.Values)
            {
                if (room.State != RoomFireState.Burning) continue;

                foreach (var neighborId in RoomAdjacency.GetAdjacentRooms(room.RoomId))
                {
                    if (_rooms.TryGetValue(neighborId, out var neighbor) &&
                        neighbor.State == RoomFireState.Safe &&
                        !candidates.Contains(neighborId))
                    {
                        candidates.Add(neighborId);
                    }
                }
            }

            if (candidates.Count == 0) return;

            // Sort for deterministic selection across clients
            candidates.Sort();
            string targetRoom = candidates[0];

            // Find a trash bin in the target room and ignite it
            var bins = UnityEngine.Object.FindObjectsOfType<Il2CppProps.TrashBin.TrashBin>();
            Il2CppProps.TrashBin.TrashBin targetBin = null;

            if (bins != null)
            {
                foreach (var bin in bins)
                {
                    if (bin == null || bin.isOnFire) continue;
                    string binRoom = Core.PlayerAccess.GetRoomForPosition(bin.transform.position);
                    if (binRoom == targetRoom)
                    {
                        targetBin = bin;
                        break;
                    }
                }
            }

            if (targetBin != null)
            {
                // Ignite via the game's native system — triggers Patch 5
                // which calls OnFireIgnited → IgniteRoom
                var localPlayer = Core.PlayerAccess.GetLocalPlayer();
                if (localPlayer.HasValue)
                {
                    var playerNetId = localPlayer.Value.LobbyPlayer.playerController;
                    if (playerNetId != null)
                    {
                        targetBin.RpcEnableFire(playerNetId, true);
                        Core.FileLogger.Log($"SpreadFire: Ignited TrashBin '{targetBin.gameObject.name}' in {targetRoom} via RpcEnableFire");
                    }
                }
            }
            else
            {
                // No trash bin in room — track state directly (no fire visuals)
                IgniteRoom(targetRoom);
                Core.FileLogger.Log($"SpreadFire: No TrashBin in {targetRoom}, tracked state only");
            }

            UI.FireNotifications.ShowSpreadAlert(targetRoom);
        }

        /// <summary>Extinguishes a specific room.</summary>
        public void ExtinguishRoom(string roomId)
        {
            if (!_rooms.TryGetValue(roomId, out var room) || room.State != RoomFireState.Burning)
                return;

            room.State = RoomFireState.Extinguished;

            OnRoomExtinguished?.Invoke(roomId);

            if (BurningRoomCount == 0)
            {
                _fireActive = false;
                _winTimerActive = false;
                _winTimer = 0f;
                OnAllFiresOut?.Invoke();

                // Reset all rooms to Safe for potential re-attempt
                foreach (var r in _rooms.Values)
                    r.State = RoomFireState.Safe;
            }

            Core.FileLogger.Log($"ExtinguishRoom: {roomId} extinguished ({BurningRoomCount} remaining)");
        }

        /// <summary>Immediately extinguishes all fires (arsonist was voted out).</summary>
        public void ExtinguishAll()
        {
            foreach (var room in _rooms.Values)
            {
                if (room.State == RoomFireState.Burning || room.State == RoomFireState.Extinguished)
                    room.State = RoomFireState.Safe;
            }

            _fireActive = false;
            _winTimerActive = false;
            _winTimer = 0f;
            OnAllFiresOut?.Invoke();
        }

        private float _smokeHintCooldown;

        private void ApplyBurningRoomEffects(float deltaTime)
        {
            var localPlayer = Core.PlayerAccess.GetLocalPlayer();
            if (!localPlayer.HasValue) return;

            string playerRoom = localPlayer.Value.CurrentRoom;
            if (playerRoom == null) return;

            bool inBurningRoom = _rooms.TryGetValue(playerRoom, out var room)
                && room.State == RoomFireState.Burning;

            if (inBurningRoom)
            {
                _smokeHintCooldown -= deltaTime;
                if (_smokeHintCooldown <= 0f)
                {
                    _cachedUi?.ShowHint("You are in a burning room! Find an extinguisher!", 3f);
                    _smokeHintCooldown = 5f;
                }
            }
            else
            {
                _smokeHintCooldown = 0f;
            }
        }

        public void PauseSpread() { _isPaused = true; }
        public void ResumeSpread() { _isPaused = false; }

        public RoomFireState GetRoomState(string roomId)
        {
            return _rooms.TryGetValue(roomId, out var room) ? room.State : RoomFireState.Safe;
        }

        public string GetOriginRoomId() => _originRoomId;

        public List<string> GetBurningRooms()
        {
            return _rooms.Values
                .Where(r => r.State == RoomFireState.Burning)
                .Select(r => r.RoomId)
                .ToList();
        }
    }
}
