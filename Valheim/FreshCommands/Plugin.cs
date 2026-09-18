using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace JuStIIFrEsH.Valheim.FreshCommands
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "justiifresh.valheim.freshcommands";
        public const string PluginName = "FreshCommands";
        public const string PluginVersion = "2.4.19";

        private static Plugin Instance;
        private const string LocationStoreKey = "freshcommands.locations.v1";
        private const string LocationStoreHeader = "v1";
        private static readonly Dictionary<string, Vector3> SavedLocations = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
        private static readonly SortedDictionary<string, string> AvailableCommands = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly FieldInfo ChatBufferField = typeof(Chat).GetField("m_chatBuffer", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ChatHideTimerField = typeof(Chat).GetField("m_hideTimer", BindingFlags.Instance | BindingFlags.NonPublic);
        private ZoneSystem loadedZoneSystem;
        private bool welcomeTipShown;
        private float playerJoinTime = -1f;

        private void Awake()
        {
            Instance = this;
            RegisterCommand(
                "corpse",
                "Corpse commands: corpse <recover|tp>",
                CorpseSubcommand,
                isCheat: false,
                isNetwork: true);
            RegisterCommand(
                "set",
                "Saves current position or X/Z: set <name> | set <name> <x> <z>",
                SetLocationCommand,
                isCheat: false,
                isNetwork: true);
            RegisterCommand(
                "delete",
                "Deletes a saved location: delete <name>",
                DeleteLocationCommand,
                isCheat: false,
                isNetwork: true);
            RegisterCommand(
                "tp",
                "Lists locations or teleports: tp [home|corpse|name]",
                TpCommand,
                isCheat: false,
                isNetwork: true);
            RegisterCommand(
                "freshcommands",
                "Lists all FreshCommands commands",
                ListFreshCommands,
                isCheat: false,
                isNetwork: true);

            Logger.LogInfo($"{PluginName} {PluginVersion} loaded. Registered: corpse recover, corpse tp, tp, set, delete");
        }

        private void Update()
        {
            if (ZoneSystem.instance && ZoneSystem.instance != loadedZoneSystem)
            {
                loadedZoneSystem = ZoneSystem.instance;
                LoadSavedLocations();
            }

            if (!Player.m_localPlayer)
            {
                welcomeTipShown = false;
                playerJoinTime = -1f;
                return;
            }

            if (playerJoinTime < 0f)
            {
                playerJoinTime = Time.unscaledTime;
            }

            // Wait until the game's chat UI finishes its world-entry initialization.
            if (!welcomeTipShown && Chat.instance && Time.unscaledTime - playerJoinTime >= 3f)
            {
                AddLocalChatTip();
            }
        }

        private void AddLocalChatTip()
        {
            var chatBuffer = ChatBufferField?.GetValue(Chat.instance) as List<string>;
            if (chatBuffer == null || !Chat.instance.m_output) return;

            const string tip = "<color=orange>FreshCommands:</color> Type /freshcommands for help.";
            chatBuffer.Add(tip);
            Chat.instance.m_chatWindow.gameObject.SetActive(true);
            ChatHideTimerField?.SetValue(Chat.instance, 3f);
            Chat.instance.m_output.text = string.Join("\n", chatBuffer);
            welcomeTipShown = true;
        }

        // Keep all registrations here so /freshcommands stays accurate as the mod grows.
        private static void RegisterCommand(string command, string description, Terminal.ConsoleEvent action, bool isCheat, bool isNetwork)
        {
            new Terminal.ConsoleCommand(command, description, action, isCheat: isCheat, isNetwork: isNetwork);
            AvailableCommands[command] = description;
        }

        private static void ListFreshCommands(Terminal.ConsoleEventArgs args)
        {
            Reply(args, "FreshCommands:");
            Reply(args, "  /freshcommands — Lists all FreshCommands commands");
            Reply(args, "  /corpse recover — recovers items from your most recent corpse.");
            Reply(args, "  /corpse tp — teleports you to your most recent corpse.");
            Reply(args, "  /set <name> — adds current location to saved locations.");
            Reply(args, "  /set <name> <x> <z> — adds the supplied X/Z coordinates to saved locations.");
            Reply(args, "  /delete <name> — Deletes a saved location.");
            Reply(args, "  /tp home — teleports you to your current spawn point.");
            Reply(args, "  /tp corpse — teleports you to your most recent corpse.");
            Reply(args, "  /tp <name> — teleports you to a saved location.");
            Reply(args, "  /tp — lists saved locations.");
            Reply(args, "  Report bugs: https://justiifresh.com/valheim/bug/");
        }

        private static void CorpseSubcommand(Terminal.ConsoleEventArgs args)
        {
            if (args.Length != 2)
            {
                Reply(args, "Usage: corpse <recover|tp>.");
                return;
            }
            switch (args[1].ToLowerInvariant())
            {
                case "tp": CorpseTeleportCommand(args); break;
                case "recover": CorpseRecoverCommand(args); break;
                default: Reply(args, "Usage: corpse <recover|tp>."); break;
            }
        }

        private static void TpCommand(Terminal.ConsoleEventArgs args)
        {
            if (args.Length == 1)
            {
                ListSavedLocationsCommand(args);
                return;
            }
            if (args.Length != 2)
            {
                Reply(args, "Usage: tp [home|corpse|name].");
                return;
            }
            switch (args[1].ToLowerInvariant())
            {
                case "home": HomeCommand(args); break;
                case "corpse": CorpseTeleportCommand(args); break;
                default: TeleportToSavedLocation(args, args[1]); break;
            }
        }

        private static void CorpseTeleportCommand(Terminal.ConsoleEventArgs args)
        {
            if (!Player.m_localPlayer || !Game.instance || ZDOMan.instance == null || !ZNet.instance)
            {
                Reply(args, "Join a world before using corpse tp.");
                return;
            }
            if (!ZNet.instance.IsServer())
            {
                Reply(args, "corpse tp currently requires a single-player world or the hosting player.");
                return;
            }

            LoadSavedLocations();

            PlayerProfile profile = Game.instance.GetPlayerProfile();
            ZDO newest = FindNewestCorpse(profile.GetPlayerID(), profile.GetName(), out long newestDeath);
            if (newest == null)
            {
                if (!profile.HaveDeathPoint())
                {
                    Reply(args, "No corpse or saved death point belonging to this character was found.");
                    return;
                }

                TeleportPlayer(Player.m_localPlayer, profile.GetDeathPoint() + Vector3.up);
                Reply(args, "No tombstone was found, so you were teleported to your saved death point.");
                return;
            }

            Vector3 destination = newest.GetPosition() + Vector3.up;
            TeleportPlayer(Player.m_localPlayer, destination);
            Reply(args, newestDeath > 0 ? "Teleported to your most recent corpse." : "Teleported to your corpse.");
        }

        private static void CorpseRecoverCommand(Terminal.ConsoleEventArgs args)
        {
            if (!Player.m_localPlayer || !Game.instance || ZDOMan.instance == null || !ZNet.instance || !ZNetScene.instance)
            {
                Reply(args, "Join a world before using corpse recover.");
                return;
            }
            if (!ZNet.instance.IsServer())
            {
                Reply(args, "corpse recover currently requires a single-player world or the hosting player.");
                return;
            }

            PlayerProfile profile = Game.instance.GetPlayerProfile();
            ZDO newest = FindNewestCorpse(profile.GetPlayerID(), profile.GetName(), out _);
            if (newest == null)
            {
                Reply(args, "No corpse belonging to this character was found.");
                return;
            }

            RecoverCorpse(newest, args);
        }

        private static void RecoverCorpse(ZDO corpse, Terminal.ConsoleEventArgs args)
        {
            // The tombstone inventory is serialized on its ZDO. Work with that data
            // directly so this command never changes the player's position or the
            // client's world-loading reference point.
            byte[] serializedInventory = corpse.GetByteArray(ZDOVars.s_items, null);
            if (serializedInventory == null || serializedInventory.Length == 0)
            {
                Reply(args, "Your most recent corpse has no items to recover.");
                return;
            }

            var corpseInventory = new Inventory("Tombstone", null, 8, 4);
            corpseInventory.Load(new ZPackage(serializedInventory));
            int itemCount = corpseInventory.NrOfItemsIncludingStacks();
            Player.m_localPlayer.GetInventory().MoveAll(corpseInventory);

            if (corpseInventory.NrOfItems() == 0)
            {
                DestroyRecoveredCorpse(corpse);
                Reply(args, $"Recovered {itemCount} item{(itemCount == 1 ? string.Empty : "s")} from your most recent corpse.");
                return;
            }

            var package = new ZPackage();
            corpseInventory.Save(package);
            corpse.Set(ZDOVars.s_items, package.GetArray());
            int remaining = corpseInventory.NrOfItemsIncludingStacks();
            Reply(args, $"Recovered {itemCount - remaining} item{(itemCount - remaining == 1 ? string.Empty : "s")}. {remaining} remain in your corpse because your inventory is full.");
        }

        private static void DestroyRecoveredCorpse(ZDO corpse)
        {
            // Clear the authoritative saved inventory first. This prevents a loaded
            // tombstone from writing its old contents back before its ZDO is removed.
            corpse.Set(ZDOVars.s_items, (byte[])null);
            ZDOMan.instance.DestroyZDO(corpse);
        }

        private static void HomeCommand(Terminal.ConsoleEventArgs args)
        {
            if (!Player.m_localPlayer || !Game.instance || !ZNet.instance)
            {
                Reply(args, "Join a world before using tp home.");
                return;
            }
            if (!ZNet.instance.IsServer())
            {
                Reply(args, "tp home currently requires a single-player world or the hosting player.");
                return;
            }

            PlayerProfile profile = Game.instance.GetPlayerProfile();
            Vector3 spawnPoint = profile.HaveCustomSpawnPoint()
                ? profile.GetCustomSpawnPoint()
                : profile.GetHomePoint();
            TeleportPlayer(Player.m_localPlayer, spawnPoint + Vector3.up * 1.5f);
            Reply(args, profile.HaveCustomSpawnPoint()
                ? "Teleported to your spawn point."
                : "No custom spawn point is set; teleported to your world home point.");
        }

        private static void SetLocationCommand(Terminal.ConsoleEventArgs args)
        {
            if (!Player.m_localPlayer || !ZoneSystem.instance || !ZNet.instance)
            {
                Reply(args, "Join a world before using set.");
                return;
            }
            if (!ZNet.instance.IsServer())
            {
                Reply(args, "set currently requires a single-player world or the hosting player.");
                return;
            }

            LoadSavedLocations();
            if ((args.Length != 2 && args.Length != 4) || !IsValidLocationName(args[1]))
            {
                Reply(args, "Usage: set <name> (current position) or set <name> <x> <z>. Names may use letters, numbers, _ and -.");
                return;
            }

            string name = args[1];
            if (IsReservedTeleportName(name))
            {
                Reply(args, $"'{name}' is reserved and cannot be used as a location name.");
                return;
            }

            Vector3 position;
            if (args.Length == 4)
            {
                if (!TryParseCoordinate(args[2], out float x) || !TryParseCoordinate(args[3], out float z))
                {
                    Reply(args, "Coordinates must be numbers. Usage: set <name> <x> <z>.");
                    return;
                }
                position = new Vector3(x, ZoneSystem.instance.GetGroundHeight(new Vector3(x, 0f, z)), z);
            }
            else
            {
                position = Player.m_localPlayer.transform.position;
            }
            RemoveSavedLocation(name);
            SavedLocations[name] = position;
            SaveSavedLocations();
            Reply(args, $"Saved '{name}' at X {position.x:0.##}, Z {position.z:0.##}. Use /tp {name} to return here.");
        }

        private static void DeleteLocationCommand(Terminal.ConsoleEventArgs args)
        {
            if (!ZoneSystem.instance || !ZNet.instance)
            {
                Reply(args, "Join a world before using delete.");
                return;
            }
            if (!ZNet.instance.IsServer())
            {
                Reply(args, "delete currently requires a single-player world or the hosting player.");
                return;
            }

            LoadSavedLocations();
            if (args.Length != 2 || !IsValidLocationName(args[1]))
            {
                Reply(args, "Usage: delete <name>.");
                return;
            }

            string name = args[1];
            if (!RemoveSavedLocation(name))
            {
                Reply(args, $"No saved location named '{name}' exists in this world.");
                return;
            }
            SavedLocations.Remove(name);
            SaveSavedLocations();
            Reply(args, $"Deleted saved location '{name}'.");
        }

        private static void ListSavedLocationsCommand(Terminal.ConsoleEventArgs args)
        {
            if (ZoneSystem.instance) LoadSavedLocations();
            if (SavedLocations.Count == 0)
            {
                Reply(args, "No saved locations in this world. Use /set <name> [x z] to create one.");
                return;
            }

            var names = new List<string>(SavedLocations.Keys);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            Reply(args, "Saved locations:");
            foreach (string name in names)
            {
                Vector3 location = SavedLocations[name];
                Reply(args, $"  {name}: X {location.x:0.##}, Z {location.z:0.##}  (/tp {name})");
            }
        }

        private static void LoadSavedLocations()
        {
            SavedLocations.Clear();
            ZDO store = GetLocationStore(create: false);
            if (store == null) return;
            string[] entries = store.GetString(LocationStoreKey, LocationStoreHeader).Split('\n');
            foreach (string entry in entries)
            {
                if (TryParseLocation(entry, out string name, out Vector3 position))
                {
                    SavedLocations[name] = position;
                }
            }
        }

        private static void SaveSavedLocations()
        {
            ZDO store = GetLocationStore(create: true);
            var names = new List<string>(SavedLocations.Keys);
            names.Sort(StringComparer.OrdinalIgnoreCase);
            var entries = new List<string> { LocationStoreHeader };
            foreach (string name in names)
            {
                entries.Add(SerializeLocation(name, SavedLocations[name]));
            }
            store.Set(LocationStoreKey, string.Join("\n", entries));
        }

        private static ZDO GetLocationStore(bool create)
        {
            List<ZDOID> ids = ZDOExtraData.GetAllZDOIDsWithHash(ZDOExtraData.Type.String, LocationStoreKey.GetStableHashCode());
            foreach (ZDOID id in ids)
            {
                ZDO store = ZDOMan.instance.GetZDO(id);
                if (store != null) return store;
            }
            if (!create) return null;

            ZDO newStore = ZDOMan.instance.CreateNewZDO(Vector3.zero, 0);
            newStore.Persistent = true;
            newStore.Set(LocationStoreKey, LocationStoreHeader);
            ZDOMan.instance.AddToSector(newStore, newStore.GetSectorIndex());
            return newStore;
        }

        private static void TeleportToSavedLocation(Terminal.ConsoleEventArgs args, string name)
        {
            if (!Player.m_localPlayer || !ZNet.instance)
            {
                Reply(args, "Join a world before teleporting.");
                return;
            }
            if (!ZNet.instance.IsServer())
            {
                Reply(args, "Saved-location teleports currently require a single-player world or the hosting player.");
                return;
            }
            if (ZoneSystem.instance) LoadSavedLocations();
            if (!SavedLocations.TryGetValue(name, out Vector3 location))
            {
                Reply(args, $"No saved location named '{name}' exists in this world.");
                return;
            }
            TeleportPlayer(Player.m_localPlayer, location + Vector3.up);
            Reply(args, $"Teleported to '{name}'.");
        }

        private static bool RemoveSavedLocation(string name)
        {
            return SavedLocations.Remove(name);
        }

        private static string SerializeLocation(string name, Vector3 position)
        {
            return name + "=" +
                position.x.ToString("R", CultureInfo.InvariantCulture) + "," +
                position.y.ToString("R", CultureInfo.InvariantCulture) + "," +
                position.z.ToString("R", CultureInfo.InvariantCulture);
        }

        private static bool TryParseLocation(string key, out string name, out Vector3 position)
        {
            name = null;
            position = Vector3.zero;
            int separator = key.IndexOf('=');
            if (separator <= 0) return false;
            string[] coordinates = key.Substring(separator + 1).Split(',');
            if (coordinates.Length != 3 ||
                !float.TryParse(coordinates[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(coordinates[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
                !float.TryParse(coordinates[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)) return false;
            name = key.Substring(0, separator);
            position = new Vector3(x, y, z);
            return IsValidLocationName(name);
        }

        private static bool IsValidLocationName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 32) return false;
            foreach (char character in name)
                if (!char.IsLetterOrDigit(character) && character != '_' && character != '-') return false;
            return true;
        }

        private static bool TryParseCoordinate(string text, out float value)
        {
            return float.TryParse(text.Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool IsReservedTeleportName(string name)
        {
            return string.Equals(name, "home", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "corpse", StringComparison.OrdinalIgnoreCase);
        }

        private static ZDO FindNewestCorpse(long playerId, string playerName, out long newestDeath)
        {
            var graves = new List<ZDO>();
            var prefabNames = new HashSet<string> { "TombStone", "Player_tombstone" };
            if (Player.m_localPlayer && Player.m_localPlayer.m_tombstone)
            {
                prefabNames.Add(Player.m_localPlayer.m_tombstone.name);
            }
            foreach (string prefabName in prefabNames)
            {
                int index = 0;
                while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(prefabName, graves, ref index)) { }
            }
            ZDO newest = null;
            newestDeath = long.MinValue;
            foreach (ZDO grave in graves)
            {
                bool isOwner = grave.GetLong(ZDOVars.s_owner, 0L) == playerId;
                bool hasMatchingOwnerName = !string.IsNullOrEmpty(playerName) &&
                    string.Equals(grave.GetString(ZDOVars.s_ownerName, ""), playerName, StringComparison.Ordinal);
                if (!isOwner && !hasMatchingOwnerName) continue;
                long death = grave.GetLong(ZDOVars.s_timeOfDeath, 0L);
                if (newest == null || death > newestDeath) { newest = grave; newestDeath = death; }
            }
            return newest;
        }

        private static void TeleportPlayer(Player player, Vector3 destination)
        {
            // Use Valheim's teleport state machine rather than moving the transform.
            // It loads distant zones before releasing the player and shows the normal
            // teleport transition instead of leaving the camera in an unloaded area.
            player.TeleportTo(destination, player.transform.rotation, distantTeleport: true);
        }

        private static void Reply(Terminal.ConsoleEventArgs args, string message)
        {
            args.Context?.AddString(message);
        }
    }
}
