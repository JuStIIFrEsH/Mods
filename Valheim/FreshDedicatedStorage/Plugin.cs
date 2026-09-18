using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace Mike.Valheim.SmallStorageChest
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [BepInDependency("com.morda.storeandcraft", BepInDependency.DependencyFlags.SoftDependency)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mike.valheim.smallstoragechest";
        public const string PluginName = "FreshDedicatedStorage";
        public const string PluginVersion = "0.4.36";
        public const string PrefabName = "Mike_SmallStorageChest";

        private static readonly FieldInfo ChatBufferField = typeof(Chat).GetField("m_chatBuffer", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ChatHideTimerField = typeof(Chat).GetField("m_hideTimer", BindingFlags.Instance | BindingFlags.NonPublic);
        private bool loginTipShown;
        private float playerJoinTime = -1f;

        private void Awake()
        {
            var harmony = new Harmony(PluginGuid);
            harmony.PatchAll(typeof(Plugin).Assembly);
            new Terminal.ConsoleCommand("freshstorage", "Shows the FreshDedicatedStorage tutorial", FreshStorageTutorial, isCheat: false, isNetwork: false);
            try { StoreAndCraftBridge.Install(harmony); }
            catch (Exception error) { Logger.LogError($"StoreAndCraft bridge unavailable: {error}"); }
            TryInstallDedicatedRenameOverride(harmony);
            PrefabManager.OnVanillaPrefabsAvailable += RegisterChest;
            PrefabManager.OnVanillaPrefabsAvailable += RegisterBulkBox;
            PrefabManager.OnVanillaPrefabsAvailable += RegisterSortingStation;
            Logger.LogInfo("FreshDedicatedStorage loaded; waiting for vanilla prefabs.");
        }

        private void Update()
        {
            if (!Player.m_localPlayer)
            {
                loginTipShown = false;
                playerJoinTime = -1f;
                return;
            }
            if (playerJoinTime < 0f) playerJoinTime = Time.unscaledTime;
            if (!loginTipShown && Chat.instance && Time.unscaledTime - playerJoinTime >= 3f)
            {
                var buffer = ChatBufferField?.GetValue(Chat.instance) as List<string>;
                if (buffer == null || !Chat.instance.m_output) return;
                buffer.Add("<color=orange>FreshDedicatedStorage:</color> Type /freshstorage for a quick tutorial.");
                Chat.instance.m_chatWindow.gameObject.SetActive(true);
                ChatHideTimerField?.SetValue(Chat.instance, Mathf.Max(0f, Chat.instance.m_hideDelay - 3f));
                Chat.instance.m_output.text = string.Join("\n", buffer);
                loginTipShown = true;
            }
        }

        private static void FreshStorageTutorial(Terminal.ConsoleEventArgs args)
        {
            Reply(args, "FreshDedicatedStorage:");
            Reply(args, "  Build Dedicated Storage Boxes from Hammer > Furniture (5 Wood).");
            Reply(args, "  E assigns an empty box or deposits matching inventory items.");
            Reply(args, "  Shift+E withdraws; hold Shift and scroll to choose the amount.");
            Reply(args, "  Alt+E uses one stored consumable.");
            Reply(args, "  Hugin's Reliquary stores matching items in assigned boxes within 50 meters.");
        }

        private static void Reply(Terminal.ConsoleEventArgs args, string message) => args.Context?.AddString(message);

        private void RegisterChest()
        {
            // Jotunn retains the registered prefab across world/menu transitions.
            PrefabManager.OnVanillaPrefabsAvailable -= RegisterChest;
            try
            {
                var config = new PieceConfig
                {
                    Name = PluginName,
                    Description = "A compact wooden chest with 6 storage slots.",
                    PieceTable = "Hammer",
                    Category = "Furniture",
                    CraftingStation = "piece_workbench"
                };
                config.AddRequirement("Wood", 5, recover: true);

                var chest = new CustomPiece(PrefabName, "piece_chest_wood", config);
                if (!chest.PiecePrefab)
                {
                    throw new InvalidOperationException("Could not clone piece_chest_wood.");
                }

                var container = chest.PiecePrefab.GetComponent<Container>();
                if (!container)
                {
                    throw new InvalidOperationException("The wooden chest has no Container component.");
                }
                container.m_name = PluginName;
                container.m_width = 3;
                container.m_height = 2;

                // Scale the whole clone so its model, colliders and placement points agree.
                chest.PiecePrefab.transform.localScale *= 0.75f;
                chest.PiecePrefab.GetComponent<ZNetView>().m_syncInitialScale = true;

                if (!PieceManager.Instance.AddPiece(chest))
                {
                    throw new InvalidOperationException("Jotunn rejected the chest registration.");
                }
                Logger.LogInfo("Registered Mike_SmallStorageChest: 3x2 slots, 5 Wood, Hammer/Furniture.");
            }
            catch (Exception error)
            {
                Logger.LogError($"Small Storage Chest registration failed: {error}");
            }
        }

        private void TryInstallDedicatedRenameOverride(Harmony harmony)
        {
            try
            {
                var renameType = AccessTools.TypeByName("ChestRename");
                var tryOpen = renameType == null ? null : AccessTools.Method(renameType, "TryOpen");
                if (tryOpen == null) return;
                harmony.Patch(tryOpen, prefix: new HarmonyMethod(typeof(Plugin), nameof(InterceptDedicatedAltRename)));
            }
            catch (Exception error) { Logger.LogWarning($"Dedicated Storage Box rename override unavailable: {error.Message}"); }
        }

        private static bool InterceptDedicatedAltRename()
        {
            return !BulkBox.TryAltUseHovered();
        }

        private void OnDestroy()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= RegisterChest;
            PrefabManager.OnVanillaPrefabsAvailable -= RegisterBulkBox;
            PrefabManager.OnVanillaPrefabsAvailable -= RegisterSortingStation;
            new Harmony(PluginGuid).UnpatchSelf();
        }

        private void RegisterBulkBox()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= RegisterBulkBox;
            try
            {
                var config = new PieceConfig
                {
                    Name = "Dedicated Storage Box",
                    Description = "Stackable single-item storage. Press E on an empty box to select the item type. Single-player only.",
                    PieceTable = "Hammer",
                    Category = "Furniture",
                    CraftingStation = "piece_workbench"
                };
                config.AddRequirement("Wood", 5, recover: true);
                var box = new CustomPiece("Mike_BulkStorageBox", "piece_chest_wood", config);
                var root = box.PiecePrefab;
                if (!root) throw new InvalidOperationException("Could not clone the base building piece.");
                // Keep Valheim's placement/save/building components, but replace all geometry and storage.
                var sourceRenderer = root.GetComponentInChildren<MeshRenderer>(true);
                if (!sourceRenderer) throw new InvalidOperationException("Base material unavailable.");
                var material = ModelAsset.Material("dedicated_storage_box", sourceRenderer.sharedMaterial);
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                foreach (var component in root.GetComponents<Component>())
                    if (!(component is Transform) && !(component is Piece) &&
                        !(component is ZNetView) && !(component is WearNTear))
                        UnityEngine.Object.DestroyImmediate(component);
                root.transform.localScale = Vector3.one;
                var visual = new GameObject("Quaternius Wood Chest") { layer = root.layer };
                visual.transform.SetParent(root.transform, false);
                visual.AddComponent<MeshFilter>().sharedMesh = ModelAsset.Mesh("dedicated_storage_box", ModelAsset.BoxVisualSize);
                visual.AddComponent<MeshRenderer>().sharedMaterial = material;
                AddFrontPreviewMarker(root, material);
                var collider = root.AddComponent<BoxCollider>();
                collider.center = new Vector3(0, ModelAsset.BoxSize.y / 2, 0);
                collider.size = ModelAsset.BoxSize;
                var wear = root.GetComponent<WearNTear>();
                wear.m_new = visual;
                wear.m_worn = visual;
                wear.m_broken = visual;
                wear.m_wet = null;
                wear.m_snow = null;
                wear.m_snowWorn = null;
                wear.m_snowBroken = null;
                wear.m_fragmentRoots = new[] { visual };
                wear.m_autoCreateFragments = false;
                wear.m_nonSolidRenderers.Clear();
                wear.m_supports = true;
                wear.m_comOffset = collider.center;
                wear.m_health = 100;
                box.Piece.m_groundOnly = false;
                box.Piece.m_groundPiece = false;
                box.Piece.m_notOnWood = false;
                box.Piece.m_noClipping = true;
                box.Piece.m_comfort = 0;
                box.Piece.m_icon = RenderManager.Instance.Render(root, RenderManager.IsometricRotation);
                var size = ModelAsset.BoxSize;
                AddSnap(root, "bottom", Vector3.zero);
                AddSnap(root, "top", new Vector3(0, size.y, 0));
                AddSnap(root, "left", new Vector3(-size.x / 2, size.y / 2, 0));
                AddSnap(root, "right", new Vector3(size.x / 2, size.y / 2, 0));
                AddSnap(root, "front", new Vector3(0, size.y / 2, -size.z / 2));
                AddSnap(root, "back", new Vector3(0, size.y / 2, size.z / 2));
                root.GetComponent<ZNetView>().m_persistent = true;
                root.AddComponent<BulkBox>();
                // A real, saved one-slot inventory handles assignment using the normal chest UI.
                // Keep it off the collider/root so BulkBox remains the world interaction target.
                var assignmentObject = new GameObject("Assignment slot");
                assignmentObject.transform.SetParent(root.transform, false);
                var assignment = assignmentObject.AddComponent<Container>();
                assignment.m_rootObjectOverride = root.GetComponent<ZNetView>();
                assignment.m_name = "Choose item type";
                assignment.m_width = 1;
                assignment.m_height = 1;
                assignment.m_privacy = Container.PrivacySetting.Public;
                assignment.m_checkGuardStone = true;
                if (!PieceManager.Instance.AddPiece(box)) throw new InvalidOperationException("Bulk box registration rejected.");
                Logger.LogInfo("Registered Mike_BulkStorageBox: 0.70m x 0.52m x 0.70m visual on the existing 0.55m x 0.40m x 0.55m snap grid.");
            }
            catch (Exception error) { Logger.LogError($"Dedicated Storage Box registration failed: {error}"); }
        }

        private void RegisterSortingStation()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= RegisterSortingStation;
            try
            {
                var config = new PieceConfig
                {
                    Name = "Hugin's Reliquary",
                    Description = "Stores matching inventory items in assigned dedicated storage within 50 meters. Single-player only.",
                    PieceTable = "Hammer", Category = "Furniture", CraftingStation = "piece_workbench"
                };
                config.AddRequirement("Wood", 10, recover: true);
                var station = new CustomPiece("Mike_SortingTable", "piece_table", config);
                if (!station.PiecePrefab) throw new InvalidOperationException("Could not clone piece_table.");
                var root = station.PiecePrefab;
                var sourceRenderer = root.GetComponentInChildren<MeshRenderer>(true);
                if (!sourceRenderer) throw new InvalidOperationException("Base material unavailable.");
                var baseMaterial = sourceRenderer.sharedMaterial;
                ReplaceGeometry(root, "hugins_reliquary_column", ModelAsset.ColumnSize,
                    ModelAsset.Material("dedicated_storage_box", baseMaterial), out var visual, out var collider);
                AddHuginPerch(root);
                var wear = root.GetComponent<WearNTear>();
                if (wear)
                {
                    wear.m_new = visual; wear.m_worn = visual; wear.m_broken = visual;
                    wear.m_wet = null; wear.m_snow = null; wear.m_snowWorn = null; wear.m_snowBroken = null;
                    wear.m_fragmentRoots = new[] { visual }; wear.m_nonSolidRenderers.Clear();
                    wear.m_autoCreateFragments = false;
                    wear.m_comOffset = collider.center;
                }
                station.Piece.m_comfort = 0;
                station.Piece.m_icon = RenderManager.Instance.Render(root, RenderManager.IsometricRotation);
                root.GetComponent<ZNetView>().m_persistent = true;
                root.AddComponent<SortingStation>();
                if (!PieceManager.Instance.AddPiece(station)) throw new InvalidOperationException("Sorting table registration rejected.");
                Logger.LogInfo("Registered Mike_SortingTable: 50m range, 10 Wood, Hammer/Furniture.");
            }
            catch (Exception error) { Logger.LogError($"Hugin's Reliquary registration failed: {error}"); }
        }

        private static void ReplaceGeometry(GameObject root, string model, Vector3 size, Material material, out GameObject visual, out BoxCollider collider)
        {
            for (int index = root.transform.childCount - 1; index >= 0; index--)
                UnityEngine.Object.DestroyImmediate(root.transform.GetChild(index).gameObject);
            foreach (var component in root.GetComponents<Component>())
                if (!(component is Transform) && !(component is Piece) && !(component is ZNetView) && !(component is WearNTear))
                    UnityEngine.Object.DestroyImmediate(component);
            root.transform.localScale = Vector3.one;
            visual = new GameObject(model + " visual") { layer = root.layer };
            visual.transform.SetParent(root.transform, false);
            visual.AddComponent<MeshFilter>().sharedMesh = ModelAsset.Mesh(model, size);
            visual.AddComponent<MeshRenderer>().sharedMaterial = material;
            collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, size.y / 2, 0);
            collider.size = size;
        }

        private static void AddAssignmentSlot(GameObject root)
        {
            var assignmentObject = new GameObject("Assignment slot");
            assignmentObject.transform.SetParent(root.transform, false);
            var assignment = assignmentObject.AddComponent<Container>();
            assignment.m_rootObjectOverride = root.GetComponent<ZNetView>();
            assignment.m_name = "Choose item type"; assignment.m_width = 1; assignment.m_height = 1;
            assignment.m_privacy = Container.PrivacySetting.Public; assignment.m_checkGuardStone = true;
        }

        private static void AddSnap(GameObject root, string name, Vector3 position)
        {
            var snap = new GameObject("snap_" + name) { tag = "snappoint" };
            snap.transform.SetParent(root.transform, false);
            snap.transform.localPosition = position;
        }

        [HarmonyPatch(typeof(GameCamera), "UpdateCamera")]
        private static class GameCameraWheelPatch
        {
            private static readonly MethodInfo NativeWheel = AccessTools.Method(typeof(ZInput), nameof(ZInput.GetMouseScrollWheel));
            private static readonly MethodInfo FilteredWheel = AccessTools.Method(typeof(BulkBox), nameof(BulkBox.FilterCameraZoomScrollWheel));

            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                foreach (var instruction in instructions)
                {
                    if (instruction.Calls(NativeWheel))
                        yield return new CodeInstruction(OpCodes.Call, FilteredWheel);
                    else
                        yield return instruction;
                }
            }
        }

        // Player.SetupPlacementGhost activates a direct child named _GhostOnly only on the
        // translucent placement preview. The arrow points toward the item-icon face (-Z).
        private static void AddFrontPreviewMarker(GameObject root, Material material)
        {
            var marker = new GameObject("_GhostOnly") { layer = root.layer };
            marker.transform.SetParent(root.transform, false);
            marker.transform.localPosition = new Vector3(0f, ModelAsset.BoxVisualSize.y + .012f, 0f);
            var filter = marker.AddComponent<MeshFilter>();
            filter.sharedMesh = FrontArrowMesh();
            var arrowMaterial = new Material(material) { name = "DedicatedStorageBox_FrontArrow_Bright" };
            arrowMaterial.mainTexture = Texture2D.whiteTexture;
            if (arrowMaterial.HasProperty("_Color")) arrowMaterial.SetColor("_Color", new Color(0.05f, 1f, 1f, 1f));
            if (arrowMaterial.HasProperty("_EmissionColor")) arrowMaterial.SetColor("_EmissionColor", new Color(0f, 1.5f, 1.5f, 1f));
            marker.AddComponent<MeshRenderer>().sharedMaterial = arrowMaterial;
            marker.SetActive(false);
        }

        private static Mesh FrontArrowMesh()
        {
            var mesh = new Mesh { name = "DedicatedStorageBox_FrontArrow" };
            mesh.vertices = new[]
            {
                new Vector3(-.055f, 0f, .105f), new Vector3(.055f, 0f, .105f),
                new Vector3(.055f, 0f, -.015f), new Vector3(.105f, 0f, -.015f),
                new Vector3(0f, 0f, -.165f), new Vector3(-.105f, 0f, -.015f),
                new Vector3(-.055f, 0f, -.015f)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 6, 6, 2, 3, 6, 3, 4, 6, 4, 5 };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddHuginPerch(GameObject root)
        {
            var huginPrefab = PrefabManager.Instance.GetPrefab("Hugin");
            if (!huginPrefab)
            {
                Jotunn.Logger.LogWarning("Hugin prefab was unavailable; Hugin's Reliquary will be built without its raven.");
                return;
            }
            var hugin = UnityEngine.Object.Instantiate(huginPrefab);
            hugin.name = "Perched Hugin";
            hugin.transform.SetParent(root.transform, false);
            hugin.transform.localPosition = Vector3.zero;
            hugin.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            hugin.transform.localScale = Vector3.one * .38f;
            foreach (var component in hugin.GetComponentsInChildren<Component>(true))
            {
                if (component is Transform || component is MeshFilter || component is MeshRenderer ||
                    component is SkinnedMeshRenderer)
                    continue;
                UnityEngine.Object.DestroyImmediate(component);
            }
            // Hugin's renderer bounds are not initialized while Jotunn registers the prefab.
            // This offset was measured against the column top so the talons meet the pedestal.
            hugin.transform.localPosition = new Vector3(0f, .90f, 0f);
        }
    }
}





