using System;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Mike.Valheim.SmallStorageChest
{
    internal static class StoreAndCraftBridge
    {
        private static Type plugin, staging, buildGrab;
        private static CraftBudget current;
        private static object Settings => AccessTools.Property(plugin, "Settings").GetValue(null);
        private static T Setting<T>(string name) => ((ConfigEntry<T>)AccessTools.Property(Settings.GetType(), name).GetValue(Settings)).Value;
        private static bool Enabled(Player player) => player && player == Player.m_localPlayer && ZNet.IsSinglePlayer &&
            (bool)AccessTools.Property(staging, "Active").GetValue(null) &&
            !(bool)AccessTools.Method(buildGrab, "ShouldGrab").Invoke(null, new object[] { player });
        private static BulkBox[] Boxes(Player player) => BulkBox.Active.Where(box => box && box.isActiveAndEnabled &&
            Vector3.Distance(player.transform.position, box.transform.position) <= Setting<float>("CraftRange"))
            .OrderBy(box => (box.transform.position - player.transform.position).sqrMagnitude).ToArray();

        internal static void Install(Harmony harmony)
        {
            if (!Chainloader.PluginInfos.TryGetValue("com.morda.storeandcraft", out var info)) return;
            var assembly = info.Instance.GetType().Assembly;
            plugin = assembly.GetType("StoreAndCraft.Plugin", true);
            staging = assembly.GetType("StoreAndCraft.StagingPull", true);
            buildGrab = assembly.GetType("StoreAndCraft.BuildGrab", true);
            var count = AccessTools.Method(assembly.GetType("StoreAndCraft.RequirementBridge", true), "CountNearby");
            var consume = AccessTools.Method(staging, "ConsumeRequirements");
            var transfer = AccessTools.Method(assembly.GetType("StoreAndCraft.TransferService", true), "Consume");
            if (count == null || consume == null || transfer == null) throw new InvalidOperationException("Unsupported StoreAndCraft API.");
            // Install consumption first: never advertise quantities without a matching debit path.
            harmony.Patch(consume, prefix: Patch(nameof(Begin)), postfix: Patch(nameof(End)), finalizer: Patch(nameof(Cleanup)));
            harmony.Patch(transfer, postfix: Patch(nameof(ChestConsumed)));
            harmony.Patch(count, postfix: Patch(nameof(Count)));
            Jotunn.Logger.LogInfo("Dedicated storage crafting bridge enabled for StoreAndCraft.");
        }
        private static HarmonyMethod Patch(string method) => new HarmonyMethod(typeof(StoreAndCraftBridge), method);
        private static void Count(Player player, string sharedName, int quality, ref int __result)
        {
            if (!Enabled(player)) return;
            long total = Math.Max(0, __result);
            foreach (var box in Boxes(player)) total = Math.Min(int.MaxValue / 2, total + box.CraftAvailable(sharedName, quality, Setting<bool>("LeaveOneItem")));
            __result = (int)total;
        }
        private static void Begin(Player player, Piece.Requirement[] requirements, int qualityLevel, int itemQuality, int multiplier)
        {
            current = null;
            if (!Enabled(player) || requirements == null) return;
            var budget = new CraftBudget();
            var station = player.GetCurrentCraftingStation();
            foreach (var requirement in requirements)
            {
                if (requirement == null || !requirement.m_resItem ||
                    (station && station.m_upgrader != requirement.m_upgraderResource) || (!station && requirement.m_upgraderResource)) continue;
                string name = requirement.m_resItem.m_itemData.m_shared.m_name;
                int carried = player.GetInventory().GetAllItems().Where(item => item.m_shared.m_name == name &&
                    (itemQuality < 0 || item.m_quality == itemQuality) && item.m_worldLevel >= Game.m_worldLevel).Sum(item => item.m_stack);
                budget.Need(name, checked(requirement.GetAmount(qualityLevel) * Math.Max(1, multiplier)), carried);
            }
            current = budget;
        }
        private static void ChestConsumed(string sharedName, int __result) => current?.Consumed(sharedName, __result);
        private static void End(Player player, int itemQuality)
        {
            var budget = current;
            current = null;
            if (budget == null) return;
            var boxes = Boxes(player);
            foreach (var need in budget.Remaining)
            {
                int left = need.Value;
                foreach (var box in boxes)
                {
                    if (left <= 0) break;
                    left -= box.ConsumeForCraft(need.Key, itemQuality, Setting<bool>("LeaveOneItem"), left);
                }
            }
        }
        private static Exception Cleanup(Exception __exception) { current = null; return __exception; }
    }
}

