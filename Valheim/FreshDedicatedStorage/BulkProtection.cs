using HarmonyLib;

namespace Mike.Valheim.SmallStorageChest
{
    [HarmonyPatch(typeof(Piece), nameof(Piece.CanBeRemoved))]
    internal static class BulkRemovalProtection
    {
        private static void Postfix(Piece __instance, ref bool __result)
        {
            var box = __instance.GetComponent<BulkBox>();
            if (box && box.ProtectContents) __result = false;
        }
    }

    [HarmonyPatch(typeof(WearNTear), nameof(WearNTear.ApplyDamage))]
    internal static class BulkDamageProtection
    {
        private static bool Prefix(WearNTear __instance, ref bool __result)
        {
            var box = __instance.GetComponent<BulkBox>();
            if (!box || !box.ProtectContents) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(WearNTear), "Destroy")]
    internal static class BulkDestroyProtection
    {
        private static bool Prefix(WearNTear __instance)
        {
            var box = __instance.GetComponent<BulkBox>();
            return !box || !box.ProtectContents;
        }
    }
}
