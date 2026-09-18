using HarmonyLib;

namespace Mike.Valheim.SmallStorageChest
{
    // Prevent the game's invalid-prefab cleanup from deleting our saved storage
    // if registration ever fails. Normal dismantling remains available when loaded.
    [HarmonyPatch(typeof(ZDOMan), nameof(ZDOMan.DestroyZDO), typeof(ZDO))]
    internal static class MissingPrefabProtection
    {
        private static bool Prefix(ZDO zdo)
        {
            int prefab = zdo.GetPrefab();
            bool ours = prefab == "Mike_BulkStorageBox".GetStableHashCode() ||
                        prefab == "Mike_SmallStorageChest".GetStableHashCode();
            return !ours || (ZNetScene.instance && ZNetScene.instance.GetPrefab(prefab));
        }
    }
}
