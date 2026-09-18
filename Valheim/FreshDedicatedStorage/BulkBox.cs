using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using UnityEngine;
using HarmonyLib;

namespace Mike.Valheim.SmallStorageChest
{
    public sealed class BulkBox : MonoBehaviour, Hoverable, Interactable
    {
        private sealed class WithdrawalOption
        {
            internal string Key;
            internal string Label;
            internal BigInteger Amount;
        }

        private sealed class WithdrawalTarget
        {
            internal Vector2i Position;
            internal int Amount;
        }

        private const string StateKey = "mike_bulkbox_state_v1";
        internal static readonly HashSet<BulkBox> Active = new HashSet<BulkBox>();
        internal static bool IsWithdrawalWheelActive()
        {
            if (!ZNet.IsSinglePlayer || !Player.m_localPlayer || !ZInput.GetButton("AltPlace")) return false;
            var hovered = Player.m_localPlayer.GetHoverObject();
            var box = hovered ? hovered.GetComponentInParent<BulkBox>() : null;
            return box && box.View && box.View.IsValid() && box.Read().Count > 0;
        }

        internal static float FilterCameraZoomScrollWheel() => IsWithdrawalWheelActive() ? 0f : ZInput.GetMouseScrollWheel();

        internal static bool TryAltUseHovered()
        {
            if (!IsRenameModifierHeld() || !Player.m_localPlayer) return false;
            var hovered = Player.m_localPlayer.GetHoverObject();
            var box = hovered ? hovered.GetComponentInParent<BulkBox>() : null;
            return box && box.UseOneForAltUse(Player.m_localPlayer);
        }

        private static bool IsRenameModifierHeld() => ZInput.GetKey(KeyCode.LeftAlt) || ZInput.GetKey(KeyCode.RightAlt);

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        internal string SortingTemplate()
        {
            if (busy || !View || !View.IsValid() || !PrivateArea.CheckAccess(transform.position, 0f, flash: false)) return null;
            if (Assignment && (Assignment.IsInUse() || Assignment.GetInventory()?.NrOfItems() > 0)) return null;
            return Read().Template;
        }

        internal bool DepositAssigned(Humanoid user, ItemDrop.ItemData item)
        {
            if (user != Player.m_localPlayer || !ZNet.IsSinglePlayer || item.m_equipped) return false;
            string template = SortingTemplate();
            if (string.IsNullOrEmpty(template) || template != EncodeItem(item)) return false;
            View.ClaimOwnership();
            if (!View.IsOwner()) return false;
            busy = true;
            try { Deposit(user, item); return true; }
            finally { busy = false; }
        }
        private ZNetView view;
        private string cachedRaw;
        private BulkState cachedState;
        private ItemDrop.ItemData cachedItem;
        private bool busy;
        private Container assignment;
        private bool assignmentFailed;
        private SpriteRenderer[] itemIcons;
        private string iconTemplate;
        private string selectedWithdrawal = "stack";
        private int altWithdrawalFrame = -1;
        private Container Assignment => assignment ? assignment : (assignment = GetComponentInChildren<Container>());

        private ZNetView View => view ? view : (view = GetComponent<ZNetView>());
        private BulkState Read()
        {
            string raw = View && View.IsValid() ? View.GetZDO().GetString(StateKey, "") : "";
            if (cachedState == null || raw != cachedRaw)
            {
                cachedState = BulkState.Decode(raw);
                cachedRaw = raw;
                cachedItem = null;
            }
            return cachedState;
        }

        // Fail closed: unreadable or missing-item data must never allow dismantling.
        public bool ProtectContents
        {
            get { try { return Read().Count > 0 || (Assignment && Assignment.GetInventory()?.NrOfItems() > 0); } catch { return true; } }
        }

        public string GetHoverName() => "Dedicated Storage Box";
        public float GetHoverOffset() => 0.25f;
        public string GetHoverText()
        {
            try
            {
                var state = Read();
                string name = string.IsNullOrEmpty(state.Template) ? "Unassigned" : Item(state).m_shared.m_name;
                string text = "Dedicated Storage Box\n" + name + ": <color=yellow>" +
                    state.Count.ToString("N0", CultureInfo.InvariantCulture) + "</color>";
                if (!ZNet.IsSinglePlayer) return text + "\nSingle-player storage only";
                if (!PrivateArea.CheckAccess(transform.position, 0f, flash: false)) return text + "\n$piece_noaccess";
                text += string.IsNullOrEmpty(state.Template)
                    ? "\n[<color=yellow>$KEY_Use</color>] Choose item type"
                    : "\n[<color=yellow>$KEY_Use</color>] Deposit all matching items";
                if (state.Count > 0)
                {
                    var option = SelectedWithdrawal(state);
                    text += "\n[<color=yellow>Alt + $KEY_Use</color>] Use 1";
                    text += "\n[<color=yellow>$KEY_AltPlace + Mouse wheel</color>] Amount: <color=yellow>" + option.Label + "</color>";
                    text += "\n[<color=yellow>$KEY_AltPlace + $KEY_Use</color>] Withdraw " + option.Label;
                }
                else
                {
                    text += "\n[<color=yellow>$KEY_AltPlace + $KEY_Use</color>] Clear item assignment";
                }
                return Localization.instance.Localize(text);
            }
            catch { return "Dedicated Storage Box\nStored data unavailable; contents preserved. Check the log."; }
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold) return false;
            if (user == Player.m_localPlayer && IsRenameModifierHeld()) return UseOneForAltUse(user);
            if (altWithdrawalFrame == Time.frameCount) return true;
            return Run(user, () =>
            {
                var state = Read();
                if (Assignment && Assignment.GetInventory()?.NrOfItems() > 0)
                {
                    OpenAssignment();
                    return;
                }
                if (alt)
                {
                    if (state.Count == 0)
                    {
                        View.GetZDO().Set(StateKey, BulkState.Empty.Encode());
                        Say(user, "Item assignment cleared.");
                    }
                    else Withdraw(user, state, SelectedWithdrawal(state).Amount);
                }
                else if (string.IsNullOrEmpty(state.Template))
                    OpenAssignment();
                else
                {
                    BigInteger deposited = BigInteger.Zero;
                    foreach (var item in user.GetInventory().GetAllItems().ToArray())
                    {
                        if (!item.m_equipped && item.m_stack > 0 && EncodeItem(item) == Read().Template)
                        {
                            int amount = item.m_stack;
                            Deposit(user, item);
                            deposited += amount;
                        }
                    }
                    Say(user, deposited > 0 ? $"Deposited {deposited:N0}." : "No matching items in your inventory.");
                }
            });
        }

        private bool UseOneForAltUse(Humanoid user)
        {
            if (altWithdrawalFrame == Time.frameCount) return true;
            altWithdrawalFrame = Time.frameCount;
            return Run(user, () =>
            {
                var state = Read();
                if (state.Count <= 0) { Say(user, "No stored items to use."); return; }

                // Use a one-item staging inventory so Valheim runs its normal consumable path
                // (food/status effect, animation, visual and cooldown checks) without first
                // placing the item in the player's inventory.
                var item = Item(state).Clone();
                item.m_stack = 1;
                if (!user.CanConsumeItem(item, checkWorldLevel: true))
                {
                    Say(user, "This stored item cannot be used right now.");
                    return;
                }
                var staging = new Inventory("Dedicated storage use", null, 1, 1);
                staging.GetAllItems().Add(item);
                user.UseItem(staging, item, fromInventoryGui: true);
                if (staging.ContainsItem(item)) return;
                View.GetZDO().Set(StateKey, state.Withdraw(BigInteger.One).Encode());
            });
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

        private void OpenAssignment()
        {
            if (!Assignment || Assignment.GetInventory() == null || !InventoryGui.instance)
                throw new InvalidOperationException("Assignment inventory is not ready; try again.");
            assignmentFailed = false;
            Assignment.SetInUse(true);
            InventoryGui.instance.Show(Assignment);
        }

        private void Awake()
        {
            itemIcons = new[]
            {
                CreateItemIcon("Assigned item icon front", -1f)
            };
        }

        private void LateUpdate()
        {
            RefreshItemIcon();
            UpdateWithdrawalSelection();
            // Inventory callbacks run during drag/transfer operations. Wait until LateUpdate so
            // both sides of the player's transfer are finished before moving the slot into bulk.
            if (busy || assignmentFailed || !ZNet.IsSinglePlayer || !View || !View.IsValid() || !View.IsOwner()) return;
            if (!Assignment || Assignment.GetInventory() == null) return;
            var inventory = Assignment.GetInventory();
            var items = inventory.GetAllItems();
            if (items.Count == 0) return;
            busy = true;
            try
            {
                if (items.Count != 1) throw new InvalidOperationException("Assignment slot has unexpected contents; retrieve them first.");
                var item = items[0];
                if (!item.m_dropPrefab || !ObjectDB.instance.GetItemPrefab(item.m_dropPrefab.name))
                    throw new InvalidOperationException("This item cannot be stored safely; take it back from the slot.");
                var next = Read().AssignFromSlot(EncodeItem(item), item.m_stack);
                ChangeInventory(inventory, next, () =>
                {
                    if (!inventory.RemoveItem(item)) throw new InvalidOperationException("Assignment transfer failed.");
                });
                // Keep the UI open: closing during a drop lets the same click reach attack controls.
                if (Player.m_localPlayer)
                    Say(Player.m_localPlayer, Localization.instance.Localize("Assigned " + item.m_shared.m_name + $"; stored {item.m_stack:N0}."));
            }
            catch (Exception error)
            {
                assignmentFailed = true;
                Jotunn.Logger.LogError($"Bulk box assignment failed; inspect the saved assignment slot: {error}");
                if (Player.m_localPlayer) Say(Player.m_localPlayer, "Could not assign that item. Press E to retrieve it from the slot.");
            }
            finally { busy = false; }
        }

        private void UpdateWithdrawalSelection()
        {
            if (!ZNet.IsSinglePlayer || !Player.m_localPlayer || !ZInput.GetButton("AltPlace")) return;
            var hovered = Player.m_localPlayer.GetHoverObject();
            if (!hovered || hovered.GetComponentInParent<BulkBox>() != this) return;
            float wheel = ZInput.GetMouseScrollWheel();
            if (Mathf.Abs(wheel) < .01f) return;
            var options = WithdrawalOptions(Read());
            if (options.Count == 0) return;
            int selected = options.FindIndex(option => option.Key == selectedWithdrawal);
            if (selected < 0) selected = DefaultWithdrawalIndex(options);
            selected = (selected + (wheel > 0f ? 1 : -1) + options.Count) % options.Count;
            selectedWithdrawal = options[selected].Key;
        }

        private WithdrawalOption SelectedWithdrawal(BulkState state)
        {
            var options = WithdrawalOptions(state);
            if (options.Count == 0) throw new InvalidOperationException("This box has no items to withdraw.");
            var selected = options.FirstOrDefault(option => option.Key == selectedWithdrawal);
            if (selected != null) return selected;
            selected = options[DefaultWithdrawalIndex(options)];
            selectedWithdrawal = selected.Key;
            return selected;
        }

        private List<WithdrawalOption> WithdrawalOptions(BulkState state)
        {
            var options = new List<WithdrawalOption>();
            if (state.Count <= 0) return options;
            int maxStack = Math.Max(1, Math.Min(ushort.MaxValue, Item(state).m_shared.m_maxStackSize));
            foreach (int amount in new[] { 1, 5, 10, 20, 50, 100 })
            {
                if (amount < maxStack && state.Count > amount)
                    options.Add(new WithdrawalOption { Key = amount.ToString(CultureInfo.InvariantCulture), Label = amount.ToString(CultureInfo.InvariantCulture), Amount = amount });
            }
            if (state.Count > maxStack)
                options.Add(new WithdrawalOption { Key = "stack", Label = "stack", Amount = maxStack });
            options.Add(new WithdrawalOption { Key = "all", Label = "all", Amount = state.Count });
            return options;
        }

        private static int DefaultWithdrawalIndex(List<WithdrawalOption> options)
        {
            int stack = options.FindIndex(option => option.Key == "stack");
            return stack >= 0 ? stack : options.Count - 1;
        }

        private void RefreshItemIcon()
        {
            if (itemIcons == null || itemIcons.Length == 0 || !View || !View.IsValid() || ObjectDB.instance == null) return;
            try
            {
                var state = Read();
                if (state.Template == iconTemplate) return;
                iconTemplate = state.Template;
                if (string.IsNullOrEmpty(iconTemplate))
                {
                    SetItemIconVisible(false);
                    return;
                }
                var sprite = Item(state).GetIcon();
                if (!sprite)
                {
                    SetItemIconVisible(false);
                    return;
                }
                float largestDimension = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
                var scale = largestDimension > 0f
                    ? UnityEngine.Vector3.one * (.27f / largestDimension)
                    : UnityEngine.Vector3.one;
                foreach (var icon in itemIcons)
                {
                    icon.sprite = sprite;
                    icon.transform.localScale = scale;
                    icon.enabled = true;
                }
            }
            catch
            {
                // The stored item can refer to a mod that is temporarily unavailable. Keep its
                // saved data intact and hide the visual until its prefab can resolve again.
                SetItemIconVisible(false);
            }
        }

        private SpriteRenderer CreateItemIcon(string name, float side)
        {
            var iconObject = new GameObject(name) { layer = gameObject.layer };
            iconObject.transform.SetParent(transform, false);
            iconObject.transform.localPosition = new UnityEngine.Vector3(0f, ModelAsset.BoxVisualSize.y * .48f,
                side * (ModelAsset.BoxVisualSize.z * .5f + .008f));
            // SpriteRenderer faces +Z by default. Flip the -Z face outward.
            iconObject.transform.localRotation = side < 0f
                ? UnityEngine.Quaternion.Euler(0f, 180f, 0f)
                : UnityEngine.Quaternion.identity;
            var renderer = iconObject.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 10;
            renderer.enabled = false;
            return renderer;
        }

        private void SetItemIconVisible(bool visible)
        {
            if (itemIcons == null) return;
            foreach (var icon in itemIcons)
                if (icon) icon.enabled = visible;
        }

        private bool Run(Humanoid user, Action operation)
        {
            if (busy || user != Player.m_localPlayer) return true;
            if (!ZNet.IsSinglePlayer) { Say(user, "Bulk boxes currently support single-player worlds only."); return true; }
            if (!View || !View.IsValid()) return true;
            if (!PrivateArea.CheckAccess(transform.position)) return true;
            View.ClaimOwnership();
            if (!View.IsOwner()) { Say(user, "Box not ready; try again."); return true; }
            busy = true;
            try { operation(); }
            catch (InvalidOperationException error) { Say(user, error.Message); }
            catch (Exception error)
            {
                Jotunn.Logger.LogError($"Bulk box operation failed; stored state retained: {error}");
                Say(user, "Storage operation failed. Check the log before using this box again.");
            }
            finally { busy = false; }
            return true;
        }

        internal int CraftAvailable(string name, int quality, bool leaveOne)
        {
            if (!ZNet.IsSinglePlayer || string.IsNullOrEmpty(SortingTemplate())) return 0;
            var state = Read();
            var item = Item(state);
            if (item.m_shared.m_name != name || (quality >= 0 && item.m_quality != quality) ||
                item.m_worldLevel < Game.m_worldLevel) return 0;
            return (int)BigInteger.Min(int.MaxValue, BigInteger.Max(0, state.Count - (leaveOne ? 1 : 0)));
        }

        internal int ConsumeForCraft(string name, int quality, bool leaveOne, int requested)
        {
            int amount = Math.Min(requested, CraftAvailable(name, quality, leaveOne));
            if (amount <= 0) return 0;
            View.ClaimOwnership();
            if (!View.IsOwner()) return 0;
            View.GetZDO().Set(StateKey, Read().Withdraw(amount).Encode());
            return amount;
        }

        private void Deposit(Humanoid user, ItemDrop.ItemData item)
        {
            var inventory = user.GetInventory();
            if (!inventory.ContainsItem(item) || item.m_stack <= 0)
                throw new InvalidOperationException("That item is no longer in your inventory.");
            if (!item.m_dropPrefab || !ObjectDB.instance.GetItemPrefab(item.m_dropPrefab.name))
                throw new InvalidOperationException("This item has no registered prefab and cannot be stored safely.");
            var next = Read().Deposit(EncodeItem(item), item.m_stack);
            ChangeInventory(inventory, next, () =>
            {
                if (!inventory.RemoveItem(item)) throw new InvalidOperationException("Could not remove the deposited stack.");
            });
        }

        private void Withdraw(Humanoid user, BulkState state, BigInteger requested)
        {
            var inventory = user.GetInventory();
            var template = Item(state);
            int maxStack = Math.Max(1, Math.Min(ushort.MaxValue, template.m_shared.m_maxStackSize));
            BigInteger remaining = BigInteger.Min(state.Count, requested);
            var targets = new List<WithdrawalTarget>();
            // Only merge with an exact metadata match. Valheim's general AddItem can merge variants.
            foreach (var stack in inventory.GetAllItems())
            {
                if (remaining > 0 && stack.m_stack < maxStack && EncodeItem(stack) == state.Template)
                {
                    int amount = (int)BigInteger.Min(remaining, maxStack - stack.m_stack);
                    targets.Add(new WithdrawalTarget { Position = stack.m_gridPos, Amount = amount });
                    remaining -= amount;
                }
            }
            for (int y = 0; remaining > 0 && y < inventory.GetHeight(); y++)
                for (int x = 0; remaining > 0 && x < inventory.GetWidth(); x++)
                    if (inventory.GetItemAt(x, y) == null)
                    {
                        int amount = (int)BigInteger.Min(remaining, maxStack);
                        targets.Add(new WithdrawalTarget { Position = new Vector2i(x, y), Amount = amount });
                        remaining -= amount;
                    }
            BigInteger withdrawn = BigInteger.Min(state.Count, requested) - remaining;
            if (withdrawn <= 0) { Say(user, "Inventory full; nothing withdrawn."); return; }
            ChangeInventory(inventory, state.Withdraw(withdrawn), () =>
            {
                foreach (var target in targets)
                {
                    var outgoing = template.Clone();
                    outgoing.m_stack = target.Amount;
                    var transfer = new Inventory("Bulk transfer", null, 1, 1);
                    transfer.GetAllItems().Add(outgoing);
                    if (!inventory.MoveItemToThis(transfer, outgoing, target.Amount, target.Position.x, target.Position.y))
                        throw new InvalidOperationException("Not enough inventory space; nothing withdrawn.");
                }
            });
            Say(user, remaining > 0 ? $"Withdrew {withdrawn:N0}; inventory is full." : $"Withdrew {withdrawn:N0}.");
        }

        private void ChangeInventory(Inventory inventory, BulkState next, Action transfer)
        {
            string encoded = next.Encode();
            string previous = View.GetZDO().GetString(StateKey, "");
            var snapshot = new ZPackage();
            inventory.Save(snapshot);
            try
            {
                transfer();
                View.GetZDO().Set(StateKey, encoded);
            }
            catch
            {
                View.GetZDO().Set(StateKey, previous);
                inventory.Load(new ZPackage(snapshot.GetArray()));
                throw;
            }
        }

        internal static string EncodeItem(ItemDrop.ItemData item)
        {
            var copy = item.Clone();
            copy.m_stack = 1;
            copy.m_gridPos = new Vector2i(0, 0);
            copy.m_equipped = false;
            copy.m_pickedUp = true;
            copy.m_customData = new Dictionary<string, string>();
            foreach (var entry in item.m_customData.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                copy.m_customData.Add(entry.Key, entry.Value);
            var oneItem = new Inventory("Bulk template", null, 1, 1);
            oneItem.GetAllItems().Add(copy);
            var package = new ZPackage();
            oneItem.Save(package);
            return Convert.ToBase64String(package.GetArray());
        }

        private ItemDrop.ItemData Item(BulkState state)
        {
            if (cachedItem != null) return cachedItem;
            var package = new ZPackage(Convert.FromBase64String(state.Template));
            var version = (global::Version.Item)package.ReadInt();
            if (version < global::Version.Item.Smaller || package.ReadUShort() != 1)
                throw new FormatException("Invalid item template version/count.");
            var loaded = ItemDrop.ItemData.Load(package, version);
            var prefab = ObjectDB.instance.GetItemPrefab(loaded.prefabHash);
            if (!prefab) throw new InvalidOperationException("Stored item's mod is missing. Restore it before withdrawing.");
            cachedItem = loaded.itemData;
            cachedItem.m_shared = prefab.GetComponent<ItemDrop>().m_itemData.m_shared;
            cachedItem.m_dropPrefab = prefab;
            return cachedItem;
        }

        private static void Say(Humanoid user, string text) => user.Message(MessageHud.MessageType.Center, text);
    }
}

