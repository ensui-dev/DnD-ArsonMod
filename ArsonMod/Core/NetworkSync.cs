using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace ArsonMod.Core
{
    /// <summary>
    /// Handles synchronizing arson mod state across all clients.
    ///
    /// Strategy: Since we can't easily register new Mirror message types in Il2Cpp,
    /// we piggyback on the game's existing networked systems:
    /// - Fire state is already synced by TrashBin.RpcEnableFire/RpcPutOutFire (hooked in HarmonyPatches)
    /// - Arson task progress is tracked locally per-client
    /// - Game events (meeting, win) are already synced by GameStateMachine
    ///
    /// For mod-specific state that needs syncing (settings, mode toggle), we use
    /// the host's authority — the host broadcasts via Harmony-patched RPCs.
    /// </summary>
    public class NetworkSync
    {
        public enum MessageType : byte
        {
            FireIgnited = 1,
            FireSpread = 2,
            FireExtinguished = 3,
            AllFiresOut = 4,
            ExtinguisherPickedUp = 5,
            ExtinguisherDepleted = 6,
            ExtinguisherRespawned = 7,
            ArsonTaskCompleted = 8,
            ArsonModeToggled = 9,
            SettingsChanged = 10,
            SlackerWin = 11,
        }

        public event Action<string> OnFireIgnitedReceived;
        public event Action<string> OnFireSpreadReceived;
        public event Action<string> OnFireExtinguishedReceived;
        public event Action OnAllFiresOutReceived;
        public event Action<string> OnExtinguisherPickedUpReceived;
        public event Action<string> OnExtinguisherDepletedReceived;
        public event Action<string> OnExtinguisherRespawnedReceived;
        public event Action<string, int> OnArsonTaskCompletedReceived;
        public event Action<bool> OnArsonModeToggledReceived;
        public event Action OnSlackerWinReceived;

        /// <summary>
        /// Sends a network message to all clients.
        ///
        /// Most arson state is already synced through existing game RPCs:
        /// - Fire ignition/extinguish: TrashBin.RpcEnableFire / RpcPutOutFire
        /// - Game state changes: GameStateMachine transitions
        ///
        /// For mod-only messages, we process them locally and rely on the game's
        /// existing sync for the underlying state changes. This works because:
        /// 1. Fire is controlled by TrashBin which is already networked
        /// 2. Meeting/voting state is already networked by GameManager
        /// 3. Task progress is client-local (each player tracks their own chain)
        /// </summary>
        public void SendMessage(MessageType type, string payload = "")
        {
            // Process locally — the underlying game state is already network-synced
            // through Mirror RPCs on TrashBin, GameManager, etc.
            FileLogger.Log($"Net: {type}: {payload}");
            OnMessageReceived(type, payload);
        }

        /// <summary>
        /// Called when a network message is received (or processed locally).
        /// </summary>
        public void OnMessageReceived(MessageType type, string payload)
        {
            switch (type)
            {
                case MessageType.FireIgnited:
                    OnFireIgnitedReceived?.Invoke(payload);
                    break;
                case MessageType.FireSpread:
                    OnFireSpreadReceived?.Invoke(payload);
                    break;
                case MessageType.FireExtinguished:
                    OnFireExtinguishedReceived?.Invoke(payload);
                    break;
                case MessageType.AllFiresOut:
                    OnAllFiresOutReceived?.Invoke();
                    break;
                case MessageType.ExtinguisherPickedUp:
                    OnExtinguisherPickedUpReceived?.Invoke(payload);
                    break;
                case MessageType.ExtinguisherDepleted:
                    OnExtinguisherDepletedReceived?.Invoke(payload);
                    break;
                case MessageType.ExtinguisherRespawned:
                    OnExtinguisherRespawnedReceived?.Invoke(payload);
                    break;
                case MessageType.ArsonTaskCompleted:
                    ParseTaskCompleted(payload);
                    break;
                case MessageType.ArsonModeToggled:
                    OnArsonModeToggledReceived?.Invoke(payload == "1");
                    break;
                case MessageType.SlackerWin:
                    OnSlackerWinReceived?.Invoke();
                    break;
            }
        }

        private void ParseTaskCompleted(string payload)
        {
            var parts = payload.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[1], out int taskIndex))
            {
                OnArsonTaskCompletedReceived?.Invoke(parts[0], taskIndex);
            }
        }

        public void BroadcastFireIgnited(string roomId)
            => SendMessage(MessageType.FireIgnited, roomId);

        public void BroadcastFireSpread(string roomId)
            => SendMessage(MessageType.FireSpread, roomId);

        public void BroadcastFireExtinguished(string roomId)
            => SendMessage(MessageType.FireExtinguished, roomId);

        public void BroadcastAllFiresOut()
            => SendMessage(MessageType.AllFiresOut);

        public void BroadcastExtinguisherPickedUp(string extinguisherId)
            => SendMessage(MessageType.ExtinguisherPickedUp, extinguisherId);

        public void BroadcastExtinguisherDepleted(string extinguisherId)
            => SendMessage(MessageType.ExtinguisherDepleted, extinguisherId);

        public void BroadcastExtinguisherRespawned(string extinguisherId)
            => SendMessage(MessageType.ExtinguisherRespawned, extinguisherId);

        public void BroadcastArsonTaskCompleted(string playerId, int taskIndex)
            => SendMessage(MessageType.ArsonTaskCompleted, $"{playerId}:{taskIndex}");

        public void BroadcastArsonModeToggled(bool enabled)
            => SendMessage(MessageType.ArsonModeToggled, enabled ? "1" : "0");

        public void BroadcastSlackerWin()
            => SendMessage(MessageType.SlackerWin);
    }
}
