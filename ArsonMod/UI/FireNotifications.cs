using System.Collections.Generic;
using UnityEngine;
using Il2CppUMUI;
using Il2CppProps.FireAlarm;

namespace ArsonMod.UI
{
    /// <summary>
    /// Displays fire-related notifications, alerts, and clue popups to players.
    /// Uses the game's built-in UIManager for all display.
    /// </summary>
    public static class FireNotifications
    {
        private const float NOTIFICATION_DURATION = 5f;
        private const float CLUE_DURATION = 8f;

        private static UIManager GetUIManager()
        {
            var fm = Core.ArsonModEntry.Instance?.FireManager;
            if (fm?.CachedUI != null) return fm.CachedUI;
            return Object.FindObjectOfType<UIManager>();
        }

        // ──────────────────────────────────────────────
        // Fire alerts (shown to all players)
        // ──────────────────────────────────────────────

        public static void ShowIgnitionAlert(string roomId)
        {
            var ui = GetUIManager();
            ui?.ShowGameEvent(
                "FIRE REPORTED",
                $"A fire has been reported in {FormatRoomName(roomId)}!",
                NOTIFICATION_DURATION
            );

            // Trigger the game's built-in fire alarm system
            FireAlarmController.alarmEnabled = true;
        }

        public static void ShowSpreadAlert(string roomId)
        {
            var ui = GetUIManager();
            ui?.ShowNotification(
                "Fire Spreading",
                $"The fire has spread to {FormatRoomName(roomId)}!",
                NOTIFICATION_DURATION
            );
        }

        public static void ShowExtinguishedAlert(string roomId)
        {
            var ui = GetUIManager();
            ui?.ShowNotification(
                "Fire Extinguished",
                $"Fire in {FormatRoomName(roomId)} has been extinguished.",
                NOTIFICATION_DURATION
            );
        }

        public static void ShowAllFiresOutAlert()
        {
            var ui = GetUIManager();
            ui?.ShowGameEvent(
                "ALL CLEAR",
                "All fires have been extinguished! Return to work.",
                NOTIFICATION_DURATION
            );

            // Disable fire alarm
            FireAlarmController.alarmEnabled = false;
        }

        // ──────────────────────────────────────────────
        // Clue notifications (shown to specific players)
        // ──────────────────────────────────────────────

        public static void ShowProximityClue(string roomId, List<string> playerIds)
        {
            string playerList = string.Join(", ", playerIds);
            ShowClue(
                $"Players seen near {FormatRoomName(roomId)} before the fire: {playerList}",
                null
            );
        }

        public static void ShowClue(string message, string targetPlayerId)
        {
            // If targetPlayerId is set, only show to that specific player
            if (targetPlayerId != null)
            {
                var localPlayer = Core.PlayerAccess.GetLocalPlayer();
                if (!localPlayer.HasValue || localPlayer.Value.PlayerId != targetPlayerId)
                    return;
            }

            // Use ShowHint for clue popups — they appear smaller and less intrusive
            var ui = GetUIManager();
            ui?.ShowHint($"[Clue] {message}", CLUE_DURATION);
        }

        public static void ShowTamperedDetectorClue(string playerId)
        {
            ShowClue("A smoke detector nearby appears to have been tampered with.", playerId);
        }

        // ──────────────────────────────────────────────
        // Banner display helpers
        // ──────────────────────────────────────────────

        private enum BannerType
        {
            Info,
            Warning,
            Urgent,
            Success,
        }

        private static string FormatRoomName(string roomId)
        {
            var formatted = System.Text.RegularExpressions.Regex.Replace(
                roomId, "([a-z])([A-Z])", "$1 $2"
            );
            return formatted;
        }
    }
}
