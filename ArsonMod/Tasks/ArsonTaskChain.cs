using System;
using System.Collections.Generic;

namespace ArsonMod.Tasks
{
    /// <summary>
    /// Manages the sequential 5-task arson chain for the Slacker arsonist,
    /// and the harmless decoy versions assigned to Specialists.
    /// </summary>
    public class ArsonTaskChain
    {
        public enum TaskRole
        {
            /// <summary>Real arson task — advances the chain toward ignition.</summary>
            Arsonist,
            /// <summary>Harmless decoy — same location/animation, no effect.</summary>
            Decoy,
        }

        public enum ChainState
        {
            NotStarted,
            InProgress,
            Completed,
            Reset,
        }

        private readonly List<ArsonTaskDefinition> _taskDefinitions;
        private readonly Dictionary<string, PlayerArsonState> _playerStates = new();

        public ChainState CurrentState { get; private set; } = ChainState.NotStarted;

        public ArsonTaskChain()
        {
            _taskDefinitions = new List<ArsonTaskDefinition>
            {
                new ArsonTaskDefinition
                {
                    Index = 0,
                    ArsonistTaskName = "Jam the smoke detector",
                    DecoyTaskName = "Inspect the smoke detector",
                    LocationType = TaskLocationType.SmokeDetector,
                    AnimationDuration = 4f,
                    SuspicionLevel = SuspicionLevel.Low,
                },
                new ArsonTaskDefinition
                {
                    Index = 1,
                    ArsonistTaskName = "Print excessive documents",
                    DecoyTaskName = "Print compliance documents",
                    LocationType = TaskLocationType.Printer,
                    AnimationDuration = 5f,
                    SuspicionLevel = SuspicionLevel.Low,
                    GrantsItem = "paper_stack",
                },
                new ArsonTaskDefinition
                {
                    Index = 2,
                    ArsonistTaskName = "Steal lighter fluid from supply closet",
                    DecoyTaskName = "Organize the supply closet",
                    LocationType = TaskLocationType.SupplyCloset,
                    AnimationDuration = 4f,
                    SuspicionLevel = SuspicionLevel.Medium,
                    GrantsItem = "lighter_fluid",
                },
                new ArsonTaskDefinition
                {
                    Index = 3,
                    ArsonistTaskName = "Stuff trash bin with printed documents",
                    DecoyTaskName = "Empty the recycling bin",
                    LocationType = TaskLocationType.TrashBin,
                    AnimationDuration = 3f,
                    SuspicionLevel = SuspicionLevel.Medium,
                    RequiresItem = "paper_stack",
                    CausesVisualChange = true,
                },
                new ArsonTaskDefinition
                {
                    Index = 4,
                    ArsonistTaskName = "Toss lit cigarette into the trash bin",
                    DecoyTaskName = null, // Slacker-only — never assigned to Specialists
                    LocationType = TaskLocationType.TrashBin,
                    AnimationDuration = 3f,
                    SuspicionLevel = SuspicionLevel.High,
                    RequiresItem = "lighter_fluid",
                    IsFinale = true,
                    MustTargetSameBinAsTask3 = true,
                },
            };
        }

        public List<ArsonTaskDefinition> TaskDefinitions => _taskDefinitions;

        /// <summary>
        /// Registers a player's arson state after arsonist selection.
        /// Must be called before GetProgress() or OnArsonTaskCompleted().
        /// </summary>
        public void InitializePlayer(string playerId)
        {
            bool isArsonist = Core.PlayerAccess.IsArsonist(playerId);
            _playerStates[playerId] = new PlayerArsonState
            {
                PlayerId = playerId,
                IsArsonist = isArsonist
            };
        }

        /// <summary>
        /// Generates the mixed task list for a player, injecting arson-themed tasks
        /// into their normal task rotation.
        /// </summary>
        public List<PlayerTask> GenerateTaskList(string playerId, bool isSlacker, List<PlayerTask> normalTasks)
        {
            var mixed = new List<PlayerTask>();
            bool isArsonist = Core.PlayerAccess.IsArsonist(playerId);
            var state = new PlayerArsonState { PlayerId = playerId, IsArsonist = isArsonist };
            _playerStates[playerId] = state;

            int normalIndex = 0;
            int arsonIndex = 0;
            int tasksBetweenArson = 2; // inject an arson task every 2 normal tasks

            while (normalIndex < normalTasks.Count || arsonIndex < _taskDefinitions.Count)
            {
                // Add a batch of normal tasks
                for (int i = 0; i < tasksBetweenArson && normalIndex < normalTasks.Count; i++)
                {
                    mixed.Add(normalTasks[normalIndex]);
                    normalIndex++;
                }

                // Inject the next arson task
                if (arsonIndex < _taskDefinitions.Count)
                {
                    var def = _taskDefinitions[arsonIndex];

                    // Task 5 (finale) is Slacker-only
                    if (def.DecoyTaskName == null && !isSlacker)
                    {
                        arsonIndex++;
                        continue;
                    }

                    var arsonTask = new PlayerTask
                    {
                        TaskId = $"arson_{arsonIndex}",
                        DisplayName = isSlacker ? def.ArsonistTaskName : def.DecoyTaskName,
                        LocationType = def.LocationType,
                        AnimationDuration = def.AnimationDuration,
                        IsArsonTask = true,
                        ArsonTaskIndex = arsonIndex,
                        IsRealArson = isSlacker,
                        IsMandatoryGate = !isSlacker, // Specialists must complete to unlock next tasks
                        RequiresItem = isSlacker ? def.RequiresItem : null,
                        GrantsItem = isSlacker ? def.GrantsItem : null,
                    };

                    mixed.Add(arsonTask);
                    arsonIndex++;
                }
            }

            return mixed;
        }

        /// <summary>
        /// Called when a player completes an arson task.
        /// Returns true if this was the finale task (fire should ignite).
        /// </summary>
        public bool OnArsonTaskCompleted(string playerId, int taskIndex, string targetBinId = null)
        {
            if (!_playerStates.TryGetValue(playerId, out var state))
                return false;

            if (!state.IsArsonist)
            {
                // Decoy task — just mark complete, unlock next normal tasks
                return false;
            }

            // Verify sequential order
            if (taskIndex != state.NextArsonTaskIndex)
                return false;

            // Check cooldown
            float now = UnityEngine.Time.time;
            float cooldown = Core.ArsonModEntry.Instance.Settings.ArsonTaskCooldown;
            if (state.LastArsonTaskTime > 0 && (now - state.LastArsonTaskTime) < cooldown)
                return false;

            state.NextArsonTaskIndex++;
            state.LastArsonTaskTime = now;

            // Track which bin was used for Task 4 (stuff bin)
            if (taskIndex == 3 && targetBinId != null)
            {
                state.TargetBinId = targetBinId;
            }

            // Task 5 (finale) — verify same bin as Task 4
            if (taskIndex == 4)
            {
                var def = _taskDefinitions[4];
                if (def.MustTargetSameBinAsTask3 && targetBinId != state.TargetBinId)
                    return false;

                CurrentState = ChainState.Completed;
                Core.ArsonModEntry.Instance.NetworkSync.BroadcastArsonTaskCompleted(playerId, taskIndex);
                return true; // Fire should ignite!
            }

            Core.ArsonModEntry.Instance.NetworkSync.BroadcastArsonTaskCompleted(playerId, taskIndex);
            CurrentState = ChainState.InProgress;
            return false;
        }

        /// <summary>Resets the arson chain (called when fire is fully extinguished).</summary>
        public void ResetChain(string playerId)
        {
            if (_playerStates.TryGetValue(playerId, out var state))
            {
                state.NextArsonTaskIndex = 0;
                state.LastArsonTaskTime = 0;
                state.TargetBinId = null;
                CurrentState = ChainState.Reset;
            }
        }

        /// <summary>Checks if a player is the arsonist. Delegates to PlayerAccess as single source of truth.</summary>
        public bool IsArsonist(string playerId)
        {
            return Core.PlayerAccess.IsArsonist(playerId);
        }

        /// <summary>Gets the arsonist's current progress (0-4).</summary>
        public int GetProgress(string playerId)
        {
            if (_playerStates.TryGetValue(playerId, out var state) && state.IsArsonist)
                return state.NextArsonTaskIndex;
            return -1;
        }
    }

    public class PlayerArsonState
    {
        public string PlayerId;
        public bool IsArsonist;
        public int NextArsonTaskIndex;
        public float LastArsonTaskTime;
        public string TargetBinId;
    }

    public class ArsonTaskDefinition
    {
        public int Index;
        public string ArsonistTaskName;
        public string DecoyTaskName; // null = Slacker-only task
        public TaskLocationType LocationType;
        public float AnimationDuration;
        public SuspicionLevel SuspicionLevel;
        public string RequiresItem;
        public string GrantsItem;
        public bool CausesVisualChange;
        public bool IsFinale;
        public bool MustTargetSameBinAsTask3;
    }

    public class PlayerTask
    {
        public string TaskId;
        public string DisplayName;
        public TaskLocationType LocationType;
        public float AnimationDuration;
        public bool IsArsonTask;
        public int ArsonTaskIndex;
        public bool IsRealArson;
        public bool IsMandatoryGate;
        public bool IsCompleted;
        public string RequiresItem;
        public string GrantsItem;
    }

    public enum TaskLocationType
    {
        SmokeDetector,
        Printer,
        SupplyCloset,
        TrashBin,
    }

    public enum SuspicionLevel
    {
        Low,
        Medium,
        High,
    }
}
