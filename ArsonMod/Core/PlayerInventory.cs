using System.Collections.Generic;

namespace ArsonMod.Core
{
    /// <summary>
    /// Simple dictionary-based inventory for tracking arson-specific items
    /// (paper_stack, lighter_fluid). The base game doesn't have a generic
    /// item inventory, so we maintain our own.
    /// </summary>
    public static class PlayerInventory
    {
        private static readonly Dictionary<string, HashSet<string>> _inventories = new();

        public static void AddItem(string playerId, string itemName)
        {
            if (!_inventories.ContainsKey(playerId))
                _inventories[playerId] = new HashSet<string>();

            _inventories[playerId].Add(itemName);
        }

        public static bool HasItem(string playerId, string itemName)
        {
            return _inventories.ContainsKey(playerId) &&
                   _inventories[playerId].Contains(itemName);
        }

        public static bool RemoveItem(string playerId, string itemName)
        {
            if (!_inventories.ContainsKey(playerId))
                return false;

            return _inventories[playerId].Remove(itemName);
        }

        /// <summary>
        /// Clears all inventories. Call at round start.
        /// </summary>
        public static void Reset()
        {
            _inventories.Clear();
        }
    }
}
