using System;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using PeakNetworkDisconnectorMod.Core;
using PeakNetworkDisconnectorMod.Managers;

namespace PeakNetworkDisconnectorMod
{
    public class PeakAntiCheatItemDropPatch : MonoBehaviour
    {
        private static bool _peakAntiCheatDetected = false;
        private static bool _itemDropPatchesApplied = false;
        private static Harmony _harmony;
        private bool _hasChecked = false;

        public static bool IsPeakAntiCheatDetected => _peakAntiCheatDetected;
        public static bool IsItemDropPatchesApplied => _itemDropPatchesApplied;

        internal void Update()
        {
            if (!_hasChecked)
            {
                _hasChecked = true;
                // Check if PeakAntiCheat is installed
                _peakAntiCheatDetected = Chainloader.PluginInfos.ContainsKey("com.hiccup444.anticheat");

                if (_peakAntiCheatDetected)
                {
                    Plugin.Log.LogInfo((object)"PeakAntiCheat detected, applying item drop patches");
                    try
                    {
                        ApplyItemDropPatches();

                        // Apply Harmony patches for item drop detection bypass
                        if (_harmony == null)
                        {
                            _harmony = new Harmony("com.icemods.peakanticheat.itemdrop.patch");
                            ApplyHarmonyPatches();
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogWarning((object)$"Error during PeakAntiCheat item drop patch application: {ex.Message}");
                    }
                }
                else
                {
                    Plugin.Log.LogInfo((object)"PeakAntiCheat not detected, skipping item drop patches");
                }
            }
        }

        private static void ApplyItemDropPatches()
        {
            if (_itemDropPatchesApplied) return;

            try
            {
                // The patches are applied via Harmony, so we just mark as applied
                _itemDropPatchesApplied = true;
                Plugin.Log.LogInfo((object)"Successfully initialized item drop patch system");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning((object)$"Failed to apply item drop patches: {ex.Message}");
            }
        }

        private static void ApplyHarmonyPatches()
        {
            try
            {
                // Find PeakAntiCheat's assembly
                if (!Chainloader.PluginInfos.TryGetValue("com.hiccup444.anticheat", out var pluginInfo))
                {
                    Plugin.Log.LogWarning((object)"PeakAntiCheat plugin not found for item drop configuration - skipping");
                    return;
                }

                Assembly assembly = pluginInfo.Instance?.GetType()?.Assembly;
                if (assembly == null)
                {
                    Plugin.Log.LogWarning((object)"Could not get PeakAntiCheat assembly for item drop configuration - skipping");
                    return;
                }

                Plugin.Log.LogInfo((object)$"Successfully loaded PeakAntiCheat assembly: {assembly.FullName}");

                // Use DetectionManager API to disable item detection (same as UI sliders)
                DisableItemDetectionViaDetectionManager(assembly);

                // Keep the PhotonView patch as it works fine
                ApplyPhotonViewPatch();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError((object)$"Failed to configure PeakAntiCheat item detection: {ex.Message}");
                Plugin.Log.LogError((object)$"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Plugin.Log.LogError((object)$"Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        private static void DisableItemDetectionViaDetectionManager(Assembly assembly)
        {
            try
            {
                // Get the DetectionManager type
                var detectionManagerType = assembly.GetType("AntiCheatMod.DetectionManager");
                if (detectionManagerType == null)
                {
                    Plugin.Log.LogWarning((object)"Could not find DetectionManager type in PeakAntiCheat");
                    return;
                }

                // Get the SetDetectionSettings method
                var setSettingsMethod = detectionManagerType.GetMethod("SetDetectionSettings",
                    BindingFlags.Public | BindingFlags.Static);
                if (setSettingsMethod == null)
                {
                    Plugin.Log.LogWarning((object)"Could not find SetDetectionSettings method in DetectionManager");
                    return;
                }

                // Get the DetectionSettings type and constructor
                var settingsType = assembly.GetType("AntiCheatMod.DetectionSettings");
                if (settingsType == null)
                {
                    Plugin.Log.LogWarning((object)"Could not find DetectionSettings type in PeakAntiCheat");
                    return;
                }

                var settingsConstructor = settingsType.GetConstructor(new Type[] { typeof(bool), typeof(bool), typeof(bool), typeof(bool) });
                if (settingsConstructor == null)
                {
                    Plugin.Log.LogWarning((object)"Could not find DetectionSettings constructor");
                    return;
                }

                // Get DetectionType enum
                var detectionTypeEnum = assembly.GetType("AntiCheatMod.DetectionType");
                if (detectionTypeEnum == null)
                {
                    Plugin.Log.LogWarning((object)"Could not find DetectionType enum in PeakAntiCheat");
                    return;
                }

                // Create disabled settings (same as UI slider = 0 "Off")
                var disabledSettings = settingsConstructor.Invoke(new object[] { false, false, false, false });

                // Disable UnauthorizedItemDrop detection
                try
                {
                    var itemDropType = Enum.Parse(detectionTypeEnum, "UnauthorizedItemDrop");
                    setSettingsMethod.Invoke(null, new object[] { itemDropType, disabledSettings });
                    Plugin.Log.LogInfo((object)"Successfully disabled UnauthorizedItemDrop detection");
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning((object)$"Failed to disable UnauthorizedItemDrop detection: {ex.Message}");
                }

                // Disable ItemSpawning detection
                try
                {
                    var itemSpawningType = Enum.Parse(detectionTypeEnum, "ItemSpawning");
                    setSettingsMethod.Invoke(null, new object[] { itemSpawningType, disabledSettings });
                    Plugin.Log.LogInfo((object)"Successfully disabled ItemSpawning detection");
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning((object)$"Failed to disable ItemSpawning detection: {ex.Message}");
                }

                // Try to broadcast the settings update (same as UI does)
                try
                {
                    var broadcastMethod = assembly.GetType("AntiCheatMod.AntiCheatPlugin")?
                        .GetMethod("BroadcastDetectionSettingsUpdate", BindingFlags.Public | BindingFlags.Static);
                    broadcastMethod?.Invoke(null, null);
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning((object)$"Failed to broadcast detection settings update: {ex.Message}");
                }

                Plugin.Log.LogInfo((object)"Successfully configured PeakAntiCheat to disable item detection");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError((object)$"Error in DisableItemDetectionViaDetectionManager: {ex.Message}");
                throw;
            }
        }

        private static void ApplyPhotonViewPatch()
        {
            try
            {
                // Patch PhotonView.OwnerActorNr setter directly (same target as PeakAntiCheat's PreOwnerActorNrSetter)
                var photonViewType = typeof(PhotonView);
                var ownerActorNrProperty = photonViewType.GetProperty("OwnerActorNr");
                var ownerActorNrSetter = ownerActorNrProperty?.GetSetMethod();
                if (ownerActorNrSetter == null)
                {
                    Plugin.Log.LogError((object)"Could not find PhotonView.OwnerActorNr setter");
                    return;
                }

                MethodInfo prefixMethod = typeof(PeakAntiCheatItemDropPatch).GetMethod("PreOwnerActorNrSetterPrefix", BindingFlags.NonPublic | BindingFlags.Static);
                if (prefixMethod == null)
                {
                    Plugin.Log.LogError((object)"Could not find PreOwnerActorNrSetterPrefix method in PeakAntiCheatItemDropPatch");
                    return;
                }

                // Use lower priority so our patch runs AFTER PeakAntiCheat's patch
                _harmony.Patch(ownerActorNrSetter, new HarmonyMethod(prefixMethod, priority: Priority.Low));
                Plugin.Log.LogInfo((object)"Successfully patched PhotonView.OwnerActorNr setter for item drop bypass");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning((object)$"Failed to apply PhotonView patch: {ex.Message}");
            }
        }

        // Harmony prefix for PreOwnerActorNrSetter - bypass item ownership checks when config is disabled
        [HarmonyPriority(Priority.Low)] // Run after PeakAntiCheat's patch
        private static bool PreOwnerActorNrSetterPrefix(PhotonView __instance, int value)
        {
            try
            {
                // If item drop detection is disabled and this is an item ownership change
                if (ConfigManager.DisableItemDropDetection && __instance != null)
                {
                    // Check if this is an item (not a character) by checking for Item component
                    // We use reflection to get the Item type from PeakAntiCheat's assembly
                    try
                    {
                        var peakAntiCheatAssembly = Chainloader.PluginInfos["com.hiccup444.anticheat"].Instance.GetType().Assembly;
                        var itemType = peakAntiCheatAssembly.GetType("Item");
                        if (itemType != null)
                        {
                            var item = __instance.GetComponent(itemType);
                            if (item != null)
                            {
                                // Skip the item ownership checking logic
                                Plugin.Log.LogInfo((object)$"Bypassing item ownership check for ViewID {__instance.ViewID} (item drop detection disabled)");
                                return true; // Allow the ownership change without checks
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogWarning((object)$"Error checking for Item component: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning((object)$"Error in PreOwnerActorNrSetterPrefix: {ex.Message}");
            }

            // Continue with original method
            return true;
        }

        // Harmony prefix for PlayerShouldHaveItem - return true when config is disabled
        private static bool PlayerShouldHaveItemPrefix(int actorNumber, string itemName, ref bool __result)
        {
            try
            {
                // If item drop detection is disabled, always return true (player "has" the item)
                if (ConfigManager.DisableItemDropDetection)
                {
                    __result = true;
                    Plugin.Log.LogInfo((object)$"Bypassing PlayerShouldHaveItem check for actor {actorNumber}, item {itemName}");
                    return false; // Skip original method
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning((object)$"Error in PlayerShouldHaveItemPrefix: {ex.Message}");
            }

            // Continue with original method
            return true;
        }

        // Harmony prefix for ProcessPendingItemChecks - skip processing when config is disabled
        private static bool ProcessPendingItemChecksPrefix()
        {
            try
            {
                // If item drop detection is disabled, skip all pending item checks
                if (ConfigManager.DisableItemDropDetection)
                {
                    Plugin.Log.LogInfo((object)"Skipping ProcessPendingItemChecks (item drop detection disabled)");
                    return false; // Skip original method
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning((object)$"Error in ProcessPendingItemChecksPrefix: {ex.Message}");
            }

            // Continue with original method
            return true;
        }
    }
}