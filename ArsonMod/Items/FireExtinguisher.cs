using UnityEngine;
using Il2CppProps.FireEx;
using Il2CppUMUI;

namespace ArsonMod.Items
{
    /// <summary>
    /// Wall-mounted fire extinguisher that players can pick up and use
    /// to put out fires. Limited charges, respawns after depletion.
    /// Hooks into the game's existing FireExController for particles and audio.
    /// </summary>
    public class FireExtinguisher : MonoBehaviour
    {
        public string ExtinguisherId;

        public int ChargesRemaining;
        public bool IsPickedUp;
        public string HeldByPlayerId;

        private float _respawnTimer;
        private bool _isDepleted;
        private Vector3 _wallPosition;
        private Quaternion _wallRotation;

        private bool _isUsing;
        private float _useTimer;
        private string _targetRoomId;

        private MeshRenderer _renderer;
        private Collider _collider;
        private FireExController _gameExtinguisher;

        private void Awake()
        {
            _wallPosition = transform.position;
            _wallRotation = transform.rotation;
            _renderer = GetComponent<MeshRenderer>();
            _collider = GetComponent<Collider>();
            _gameExtinguisher = GetComponent<FireExController>();

            var settings = Core.ArsonModEntry.Instance.Settings;
            ChargesRemaining = settings.ExtinguisherCharges;
        }

        private void Update()
        {
            if (_isDepleted)
            {
                HandleRespawnTimer();
                return;
            }

            if (_isUsing)
            {
                HandleUsage();
            }
        }

        /// <summary>Player picks up the extinguisher from the wall.</summary>
        public bool TryPickUp(string playerId)
        {
            if (IsPickedUp || _isDepleted || ChargesRemaining <= 0)
                return false;

            IsPickedUp = true;
            HeldByPlayerId = playerId;

            // Hide the wall-mounted model
            if (_renderer != null) _renderer.enabled = false;
            if (_collider != null) _collider.enabled = false;

            // Use the game's collectible system to attach to player's hands
            // The FireExController extends Collectible which handles model attachment
            var localPlayer = Core.PlayerAccess.GetLocalPlayer();
            if (localPlayer.HasValue && localPlayer.Value.PlayerId == playerId)
            {
                var playerController = localPlayer.Value.LobbyPlayer.playerController;
                if (playerController != null)
                {
                    var pc = playerController.GetComponent<Il2CppPlayer.PlayerController>();
                    var netId = _gameExtinguisher?.GetComponent<Il2CppMirror.NetworkIdentity>();
                    if (pc != null && netId != null)
                    {
                        pc.UseCollectible(netId);
                    }
                }
            }

            Core.ArsonModEntry.Instance.NetworkSync.BroadcastExtinguisherPickedUp(ExtinguisherId);
            return true;
        }

        /// <summary>Player drops the extinguisher (returns to wall or drops at feet).</summary>
        public void Drop()
        {
            if (!IsPickedUp) return;

            // Re-enable player's ability to interact with normal tasks
            var localPlayer = Core.PlayerAccess.GetLocalPlayer();
            if (localPlayer.HasValue && localPlayer.Value.PlayerId == HeldByPlayerId)
            {
                var playerController = localPlayer.Value.LobbyPlayer.playerController;
                if (playerController != null)
                {
                    var pc = playerController.GetComponent<Il2CppPlayer.PlayerController>();
                    pc?.StopUseCollectible();
                }
            }

            IsPickedUp = false;
            HeldByPlayerId = null;

            // Return to wall mount
            transform.position = _wallPosition;
            transform.rotation = _wallRotation;
            if (_renderer != null) _renderer.enabled = true;
            if (_collider != null) _collider.enabled = true;
        }

        /// <summary>
        /// Start using the extinguisher on a fire in the current room.
        /// Player must hold for ExtinguishTime seconds to put out the fire.
        /// </summary>
        public bool StartUsing(string roomId)
        {
            if (!IsPickedUp || ChargesRemaining <= 0)
                return false;

            var fireManager = Core.ArsonModEntry.Instance.FireManager;
            if (fireManager.GetRoomState(roomId) != Fire.FireManager.RoomFireState.Burning)
                return false;

            _isUsing = true;
            _useTimer = 0f;
            _targetRoomId = roomId;

            // Play extinguisher spray using the game's built-in particle system and audio
            if (_gameExtinguisher != null)
            {
                var localPlayer = Core.PlayerAccess.GetLocalPlayer();
                if (localPlayer.HasValue)
                {
                    var netId = localPlayer.Value.LobbyPlayer.playerController;
                    if (netId != null)
                    {
                        _gameExtinguisher.PlayStream(netId);
                    }
                }

                // Also directly enable the particle system if available
                if (_gameExtinguisher.fireExPs != null)
                    _gameExtinguisher.fireExPs.Play();
            }

            return true;
        }

        /// <summary>Stop using (player released interact or left the room).</summary>
        public void StopUsing()
        {
            _isUsing = false;
            _useTimer = 0f;
            _targetRoomId = null;

            // Stop spray particles and audio using game's extinguisher
            if (_gameExtinguisher != null && _gameExtinguisher.fireExPs != null)
            {
                _gameExtinguisher.fireExPs.Stop();
            }

            // Clear the instruction/progress UI
            var ui = Object.FindObjectOfType<UIManager>();
            ui?.CloseLoadingScreen();
        }

        private void HandleUsage()
        {
            var settings = Core.ArsonModEntry.Instance.Settings;
            _useTimer += Time.deltaTime;

            // Show progress bar using the game's instruction timer UI
            var ui = Object.FindObjectOfType<UIManager>();
            if (ui != null)
            {
                float progress = _useTimer / settings.ExtinguishTime;
                ui.ShowInstruction("Extinguishing Fire", $"{Mathf.CeilToInt(settings.ExtinguishTime - _useTimer)}s remaining", false);
                ui.SetInstructionTimer(progress);
            }

            if (_useTimer >= settings.ExtinguishTime)
            {
                // Successfully extinguished the room
                var fireManager = Core.ArsonModEntry.Instance.FireManager;
                fireManager.ExtinguishRoom(_targetRoomId);

                ChargesRemaining--;
                _isUsing = false;
                _useTimer = 0f;
                _targetRoomId = null;

                StopUsing();

                if (ChargesRemaining <= 0)
                {
                    Deplete();
                }
            }
        }

        private void Deplete()
        {
            _isDepleted = true;
            _respawnTimer = 0f;

            Drop();

            if (_renderer != null) _renderer.enabled = false;
            if (_collider != null) _collider.enabled = false;

            Core.ArsonModEntry.Instance.NetworkSync.BroadcastExtinguisherDepleted(ExtinguisherId);
        }

        private void HandleRespawnTimer()
        {
            var settings = Core.ArsonModEntry.Instance.Settings;
            _respawnTimer += Time.deltaTime;

            if (_respawnTimer >= settings.ExtinguisherRespawnTime)
            {
                Respawn();
            }
        }

        private void Respawn()
        {
            _isDepleted = false;
            ChargesRemaining = Core.ArsonModEntry.Instance.Settings.ExtinguisherCharges;

            transform.position = _wallPosition;
            transform.rotation = _wallRotation;
            if (_renderer != null) _renderer.enabled = true;
            if (_collider != null) _collider.enabled = true;

            Core.ArsonModEntry.Instance.NetworkSync.BroadcastExtinguisherRespawned(ExtinguisherId);
        }

        public float GetChargeRatio()
        {
            int max = Core.ArsonModEntry.Instance.Settings.ExtinguisherCharges;
            return max > 0 ? (float)ChargesRemaining / max : 0f;
        }

        public float GetUseProgress()
        {
            if (!_isUsing) return 0f;
            return _useTimer / Core.ArsonModEntry.Instance.Settings.ExtinguishTime;
        }
    }
}
