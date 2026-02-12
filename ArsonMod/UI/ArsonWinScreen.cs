using System.Linq;
using UnityEngine;
using Il2CppUMUI;

namespace ArsonMod.UI
{
    public static class ArsonWinScreen
    {
        /// <summary>
        /// Shows the arsonist victory screen.
        /// Called when fire reaches enough rooms and isn't extinguished in time.
        /// </summary>
        public static void ShowArsonistWin()
        {
            var ui = Object.FindObjectOfType<UIManager>();
            if (ui == null) return;

            ui.ShowGameEvent(
                "THE ARSONIST WINS",
                "The office has burned down!",
                10f
            );

            // Reveal all arsonists
            var arsonistIds = Core.PlayerAccess.GetArsonistIds();
            if (arsonistIds.Count > 0)
            {
                var players = Core.PlayerAccess.GetAllPlayers();
                var names = players
                    .Where(p => arsonistIds.Contains(p.PlayerId))
                    .Select(p => p.LobbyPlayer.username)
                    .ToList();

                string reveal = names.Count == 1
                    ? $"{names[0]} was the arsonist!"
                    : $"{string.Join(" & ", names)} were the arsonists!";

                ui.ShowNotification("Arsonist Revealed", reveal, 8f);
            }

            MelonLoader.MelonLogger.Msg("[ArsonMod] ARSONIST WINS - The office burned down!");
        }

        /// <summary>
        /// Shows a message when an arsonist is caught and fired.
        /// </summary>
        public static void ShowArsonistCaught(string arsonistName)
        {
            var ui = Object.FindObjectOfType<UIManager>();
            if (ui == null) return;

            ui.ShowGameEvent(
                "ARSONIST CAUGHT",
                $"{arsonistName} was the arsonist! All fires extinguished.",
                8f
            );

            MelonLoader.MelonLogger.Msg($"[ArsonMod] Arsonist {arsonistName} was caught and fired!");
        }
    }
}
