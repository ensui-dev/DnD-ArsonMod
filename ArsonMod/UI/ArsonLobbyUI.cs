using Il2Cpp;
using Il2CppSystem.Collections.Generic;
using MelonLoader;
using UnityEngine;

namespace ArsonMod.UI
{
    public class ArsonLobbyUI
    {
        private static bool _injected;

        // Setting keys
        public const string KEY_ARSON_ENABLED = "arsonModeEnabled";
        public const string KEY_FIRE_SPREAD = "arsonFireSpread";
        public const string KEY_EXTINGUISH_TIME = "arsonExtinguishTime";
        public const string KEY_ROOMS_TO_WIN = "arsonRoomsToWin";
        public const string KEY_ARSONIST_COUNT = "arsonArsonistCount";

        // References to our injected Setting objects for polling
        private static Setting _enabledSetting;
        private static Setting _fireSpreadSetting;
        private static Setting _extinguishTimeSetting;
        private static Setting _roomsToWinSetting;
        private static Setting _arsonistCountSetting;

        // Lookup tables for MultiChoice values
        private static readonly float[] FireSpreadValues = { 10f, 15f, 20f, 25f, 30f };
        private static readonly float[] ExtinguishTimeValues = { 5f, 8f, 10f, 12f, 15f };
        private static readonly int[] RoomsToWinValues = { 1, 2, 3, 4, 5 };
        private static readonly int[] ArsonistCountValues = { 1, 2, 3 };

        // Task setting keys for arson tasks (On/Off toggles in Tasks category)
        public const string KEY_TASK_JAM_SMOKE = "arsonTaskJamSmoke";
        public const string KEY_TASK_PRINT_DOCS = "arsonTaskPrintDocs";
        public const string KEY_TASK_STEAL_FLUID = "arsonTaskStealFluid";
        public const string KEY_TASK_STUFF_BIN = "arsonTaskStuffBin";
        public const string KEY_TASK_TOSS_CIG = "arsonTaskTossCig";

        private static readonly string[] ArsonTaskKeys = {
            KEY_TASK_JAM_SMOKE, KEY_TASK_PRINT_DOCS, KEY_TASK_STEAL_FLUID,
            KEY_TASK_STUFF_BIN, KEY_TASK_TOSS_CIG
        };
        private static readonly string[] ArsonTaskLabels = {
            "Jam smoke detector", "Print excessive documents",
            "Steal lighter fluid", "Stuff trash bin", "Toss lit cigarette"
        };

        private static Setting[] _arsonTaskSettings;

        public static void InjectSettings()
        {
            // Don't use _injected as a guard — the game may have destroyed
            // and recreated settings when leaving and re-creating a lobby.
            // Always scan categories to find or create our settings.

            var manager = GameRulesSettingsManager.instance;
            if (manager == null) return;

            var categories = manager.categories;
            if (categories == null) return;

            // One-time category debug logging
            if (!_injected)
            {
                for (int i = 0; i < categories.Count; i++)
                {
                    var cat = categories[i];
                    var settingsList = cat.Settings;
                    int settingCount = settingsList != null ? settingsList.Count : 0;

                    var subCats = new System.Collections.Generic.HashSet<string>();
                    if (settingsList != null)
                    {
                        for (int j = 0; j < settingsList.Count; j++)
                        {
                            var sub = settingsList[j].subCategory;
                            if (!string.IsNullOrEmpty(sub))
                                subCats.Add(sub);
                        }
                    }
                    string subCatStr = subCats.Count > 0 ? string.Join(", ", subCats) : "(none)";
                    Core.FileLogger.Log($"Category[{i}]: '{cat.CategoryName}' ({settingCount} settings, subcats: {subCatStr})");
                }
            }

            // Check if Arson Mode category already exists (handles lobby re-creation)
            for (int i = 0; i < categories.Count; i++)
            {
                if (categories[i].CategoryName == "Arson Mode")
                {
                    CacheSettingReferences(categories[i]);
                    InjectTaskSettings(categories);
                    _injected = true;
                    return;
                }
            }

            var category = new SettingCategory("Arson Mode");

            _enabledSetting = CreateMultiChoice(KEY_ARSON_ENABLED, "Arson Mode",
                "Enable the Arson game mode",
                new[] { "Off", "On" }, 0);
            category.Settings.Add(_enabledSetting);

            _fireSpreadSetting = CreateMultiChoice(KEY_FIRE_SPREAD, "Fire Spread Speed",
                "Time in seconds between fire spreading to adjacent rooms",
                new[] { "10s", "15s", "20s", "25s", "30s" }, 2);
            category.Settings.Add(_fireSpreadSetting);

            _extinguishTimeSetting = CreateMultiChoice(KEY_EXTINGUISH_TIME, "Extinguish Time",
                "Grace period — if fire reaches enough rooms and isn't put out in this time, arsonist wins",
                new[] { "5s", "8s", "10s", "12s", "15s" }, 2);
            category.Settings.Add(_extinguishTimeSetting);

            _roomsToWinSetting = CreateMultiChoice(KEY_ROOMS_TO_WIN, "Rooms to Burn",
                "Number of rooms that must be burning for arsonist to win",
                new[] { "1", "2", "3", "4", "5" }, 2);
            category.Settings.Add(_roomsToWinSetting);

            _arsonistCountSetting = CreateMultiChoice(KEY_ARSONIST_COUNT, "Arsonist Count",
                "Number of arsonists per round",
                new[] { "1", "2", "3" }, 0);
            category.Settings.Add(_arsonistCountSetting);

            categories.Add(category);
            InjectTaskSettings(categories);
            _injected = true;

            MelonLogger.Msg($"[ArsonMod] Injected Arson Mode category with {category.Settings.Count} settings.");
        }

        private static void InjectTaskSettings(List<SettingCategory> categories)
        {
            SettingCategory tasksCategory = null;
            string slackerSubCategory = null;

            for (int i = 0; i < categories.Count; i++)
            {
                var cat = categories[i];
                if (cat.CategoryName == "lobbySettings.tasks.title")
                {
                    tasksCategory = cat;
                    for (int j = 0; j < cat.Settings.Count; j++)
                    {
                        var sub = cat.Settings[j].subCategory;
                        if (!string.IsNullOrEmpty(sub) && sub.ToLower().Contains("slacker"))
                        {
                            slackerSubCategory = sub;
                            break;
                        }
                    }
                    break;
                }
            }

            if (tasksCategory == null) return;

            if (slackerSubCategory == null)
                slackerSubCategory = "Slacker";

            // Check if already injected
            for (int i = 0; i < tasksCategory.Settings.Count; i++)
            {
                if (tasksCategory.Settings[i].Key == KEY_TASK_JAM_SMOKE)
                {
                    CacheTaskSettingReferences(tasksCategory);
                    return;
                }
            }

            _arsonTaskSettings = new Setting[ArsonTaskKeys.Length];
            for (int i = 0; i < ArsonTaskKeys.Length; i++)
            {
                var setting = CreateMultiChoice(
                    ArsonTaskKeys[i], ArsonTaskLabels[i],
                    "Arson Mode task (requires Arson Mode enabled)",
                    new[] { "Off", "On" }, 1);
                setting.subCategory = slackerSubCategory;
                _arsonTaskSettings[i] = setting;
                tasksCategory.Settings.Add(setting);
            }
        }

        private static void CacheTaskSettingReferences(SettingCategory tasksCategory)
        {
            _arsonTaskSettings = new Setting[ArsonTaskKeys.Length];
            for (int i = 0; i < tasksCategory.Settings.Count; i++)
            {
                var s = tasksCategory.Settings[i];
                for (int j = 0; j < ArsonTaskKeys.Length; j++)
                {
                    if (s.Key == ArsonTaskKeys[j])
                    {
                        _arsonTaskSettings[j] = s;
                        break;
                    }
                }
            }
        }

        public static bool IsArsonTaskEnabled(int taskIndex)
        {
            if (_arsonTaskSettings == null || taskIndex < 0 || taskIndex >= _arsonTaskSettings.Length)
                return true;
            var setting = _arsonTaskSettings[taskIndex];
            if (setting == null) return true;
            return (int)setting.Value >= 1;
        }

        private static Setting CreateMultiChoice(string key, string label, string hint, string[] options, int defaultIndex)
        {
            var setting = new Setting();
            setting.Key = key;
            setting.label = label;
            setting.hint = hint;
            setting.subCategory = "Arson Mode";
            setting.type = SettingType.MultiChoice;
            setting.Value = defaultIndex;

            var alts = new List<SettingAlternative>();
            foreach (var opt in options)
                alts.Add(new SettingAlternative(opt.ToLower().Replace(" ", "_"), opt));
            setting.alternatives = alts;

            return setting;
        }

        private static void CacheSettingReferences(SettingCategory category)
        {
            for (int i = 0; i < category.Settings.Count; i++)
            {
                var s = category.Settings[i];
                switch (s.Key)
                {
                    case KEY_ARSON_ENABLED: _enabledSetting = s; break;
                    case KEY_FIRE_SPREAD: _fireSpreadSetting = s; break;
                    case KEY_EXTINGUISH_TIME: _extinguishTimeSetting = s; break;
                    case KEY_ROOMS_TO_WIN: _roomsToWinSetting = s; break;
                    case KEY_ARSONIST_COUNT: _arsonistCountSetting = s; break;
                }
            }
        }

        public static void PollSettingValues()
        {
            if (!_injected) return;

            var settings = Core.ArsonModEntry.Instance?.Settings;
            if (settings == null) return;

            if (_enabledSetting != null)
            {
                bool enabled = (int)_enabledSetting.Value >= 1;
                if (enabled != Core.ArsonModEntry.Instance.IsArsonModeActive)
                {
                    Core.ArsonModEntry.Instance.EnableArsonMode(enabled);
                    Core.ArsonModEntry.Instance.NetworkSync?.BroadcastArsonModeToggled(enabled);
                }
            }

            if (_fireSpreadSetting != null)
            {
                int idx = Mathf.Clamp((int)_fireSpreadSetting.Value, 0, FireSpreadValues.Length - 1);
                settings.FireSpreadInterval = FireSpreadValues[idx];
            }

            if (_extinguishTimeSetting != null)
            {
                int idx = Mathf.Clamp((int)_extinguishTimeSetting.Value, 0, ExtinguishTimeValues.Length - 1);
                settings.ExtinguishTime = ExtinguishTimeValues[idx];
            }

            if (_roomsToWinSetting != null)
            {
                int idx = Mathf.Clamp((int)_roomsToWinSetting.Value, 0, RoomsToWinValues.Length - 1);
                settings.RoomsToWin = RoomsToWinValues[idx];
            }

            if (_arsonistCountSetting != null)
            {
                int idx = Mathf.Clamp((int)_arsonistCountSetting.Value, 0, ArsonistCountValues.Length - 1);
                settings.ArsonistCount = ArsonistCountValues[idx];
            }
        }

        public static void Reset()
        {
            _injected = false;
            _enabledSetting = null;
            _fireSpreadSetting = null;
            _extinguishTimeSetting = null;
            _roomsToWinSetting = null;
            _arsonistCountSetting = null;
            _arsonTaskSettings = null;
        }
    }
}
