using System;
using System.Collections.Generic;

namespace Mike.Valheim.SmallStorageChest
{
    internal sealed class CraftBudget
    {
        internal readonly Dictionary<string, int> Remaining = new Dictionary<string, int>();
        internal void Need(string name, int required, int carried)
        {
            Remaining.TryGetValue(name, out int previous);
            Remaining[name] = checked(previous + Math.Max(0, required - carried));
        }
        internal void Consumed(string name, int amount)
        {
            if (Remaining.TryGetValue(name, out int left)) Remaining[name] = Math.Max(0, left - Math.Max(0, amount));
        }
    }
}
