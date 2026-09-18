using System;
using System.Collections.Generic;

namespace Mike.Valheim.SmallStorageChest
{
    internal static class StorageRouting
    {
        internal const float Range = 50f;
        internal static T Nearest<T>(IEnumerable<T> boxes, string template, bool equipped,
            Func<T, string> assignment, Func<T, float> distanceSquared) where T : class
        {
            if (equipped || string.IsNullOrEmpty(template)) return null;
            T nearest = null;
            float best = Range * Range;
            foreach (var box in boxes)
            {
                float distance = distanceSquared(box);
                if (distance > best || float.IsNaN(distance) || assignment(box) != template) continue;
                if (nearest != null && distance == best) continue;
                nearest = box;
                best = distance;
            }
            return nearest;
        }
    }
}
