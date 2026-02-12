namespace ArsonMod.Core
{
    public class ArsonSettings
    {
        /// <summary>Seconds between fire spreading to the next adjacent room.</summary>
        public float FireSpreadInterval { get; set; } = 20f;

        /// <summary>Grace period — seconds to extinguish fires once room threshold is met, or arsonist wins.</summary>
        public float ExtinguishTime { get; set; } = 10f;

        /// <summary>Number of simultaneously burning rooms required for arsonist win.</summary>
        public int RoomsToWin { get; set; } = 3;

        /// <summary>Whether fire spread pauses during meetings.</summary>
        public bool PauseFireDuringMeetings { get; set; } = true;

        /// <summary>Number of rooms a single extinguisher can put out before depleting.</summary>
        public int ExtinguisherCharges { get; set; } = 2;

        /// <summary>Seconds before an empty extinguisher respawns.</summary>
        public float ExtinguisherRespawnTime { get; set; } = 30f;

        /// <summary>Minimum seconds between completing consecutive arson tasks (0 = no cooldown).</summary>
        public float ArsonTaskCooldown { get; set; } = 0f;

        /// <summary>Number of arsonists per round.</summary>
        public int ArsonistCount { get; set; } = 1;

        /// <summary>Apply player-count-based scaling to settings.</summary>
        public void ApplyPlayerCountScaling(int playerCount)
        {
            if (playerCount <= 8)
            {
                RoomsToWin = 2;
            }
            else if (playerCount <= 14)
            {
                RoomsToWin = 3;
            }
            else
            {
                RoomsToWin = 5;
                FireSpreadInterval *= 1.2f;
            }
        }
    }
}
