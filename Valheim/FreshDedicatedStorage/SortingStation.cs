using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using UnityEngine;

namespace Mike.Valheim.SmallStorageChest
{
    public sealed class SortingStation : MonoBehaviour, Hoverable, Interactable
    {
        private bool busy;
        public string GetHoverName() => "Hugin's Reliquary";
        public float GetHoverOffset() => 0.25f;
        public string GetHoverText() => Localization.instance.Localize(
            "Hugin's Reliquary\n[<color=yellow>$KEY_Use</color>] Deposit to storage");
        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;
        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || busy || user != Player.m_localPlayer) return false;
            if (!ZNet.IsSinglePlayer) { Say(user, "Sorting currently supports single-player worlds only."); return true; }
            var view = GetComponent<ZNetView>();
            if (!view || !view.IsValid() || !PrivateArea.CheckAccess(transform.position)) return true;
            busy = true;
            BigInteger total = BigInteger.Zero;
            var used = new HashSet<BulkBox>();
            try
            {
                var boxes = BulkBox.Active.Where(box => box && box.isActiveAndEnabled &&
                    (box.transform.position - transform.position).sqrMagnitude <= StorageRouting.Range * StorageRouting.Range)
                    .OrderBy(box => box.GetInstanceID()).ToArray();
                // Snapshot before transfers; each successful removal invalidates inventory enumeration.
                var hotbar = new HashSet<ItemDrop.ItemData>(user.GetInventory().GetHotbar(false));
                foreach (var item in user.GetInventory().GetAllItems().ToArray())
                {
                    if (item.m_equipped || hotbar.Contains(item) || item.m_stack <= 0) continue;
                    var target = StorageRouting.Nearest(boxes, BulkBox.EncodeItem(item), item.m_equipped,
                        box => box.SortingTemplate(), box => (box.transform.position - transform.position).sqrMagnitude);
                    if (!target) continue;
                    int amount = item.m_stack;
                    if (target.DepositAssigned(user, item)) { total += amount; used.Add(target); }
                }
                Say(user, total > 0 ? $"Stored {total:N0} items in {used.Count} boxes." : "No matching items and available assigned boxes within 50 m.");
            }
            catch (Exception error)
            {
                // Stop after a failed transfer: rollback may replace inventory item references.
                Jotunn.Logger.LogError($"Sorting stopped after storing {total} items: {error}");
                Say(user, $"Stored {total:N0} items; sorting stopped on an error. Check the log.");
            }
            finally { busy = false; }
            return true;
        }
        private static void Say(Humanoid user, string message) => user.Message(MessageHud.MessageType.Center, message);
    }
}

