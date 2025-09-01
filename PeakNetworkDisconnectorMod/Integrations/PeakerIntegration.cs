using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using Steamworks;
using UnityEngine;
using PeakNetworkDisconnectorMod.Core;
using PeakNetworkDisconnectorMod.Managers;

namespace PeakNetworkDisconnectorMod
{
    public class PeakerIntegration : MonoBehaviour
    {
        private static bool _peakerDetected = false;
        private static bool _peakerPatchesApplied = false;
        private bool _hasChecked = false;
        
        public static bool IsPeakerDetected => _peakerDetected;
        public static bool IsPeakerPatchesApplied => _peakerPatchesApplied;
        
        internal void Update()
        {
            if (!_hasChecked)
            {
                _hasChecked = true;
                // Check if PEAKER is installed
                _peakerDetected = Chainloader.PluginInfos.ContainsKey("lammas123.PEAKER");

                if (_peakerDetected)
                {
                    Plugin.Log.LogInfo((object)"PEAKER mod detected, applying integration patches");
                    try
                    {
                        ApplyPeakerPatches();
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogWarning((object)$"Error during PEAKER integration: {ex.Message}");
                    }
                }
                else
                {
                    Plugin.Log.LogInfo((object)"PEAKER mod not detected, using default behavior");
                }
            }
        }
        
        // Add new method to patch PEAKER's ban button
        private class PeakerBanButtonPatch
        {
            internal static MethodBase GetTargetMethod()
            {
                try
                {
                    var bannedScoutsType = Type.GetType("PEAKER.BannedScouts, PEAKER");
                    if (bannedScoutsType != null)
                    {
                        // Patch the method that handles the ban button click in DrawSteamWindow
                        return AccessTools.Method(bannedScoutsType, "DrawSteamWindow");
                    }
                    else
                    {
                        Plugin.Log.LogWarning((object)"Could not find PEAKER.BannedScouts type for ban button patch");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning((object)$"Error in PeakerBanButtonPatch.GetTargetMethod(): {ex.Message}");
                }
                return null;
            }
            
            static bool Prefix(int windowID)
            {
                // We'll handle the ban button click in the postfix
                return true;
            }
            
            static void Postfix(object __instance)
            {
                // This will intercept ban button clicks in PEAKER
                try
                {
                    // We're patching the DrawSteamWindow method to intercept ban button actions
                    // The actual interception will happen in the HandlePeakerBanButton method
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError((object)$"Error in PEAKER ban button patch: {ex.Message}");
                }
            }
        }
        
        // Patch the File.AppendAllText method to intercept PEAKER's ban file writes
        [HarmonyPatch(typeof(File), "AppendAllText", new Type[] { typeof(string), typeof(string) })]
        private class PeakerFileWritePatch
        {
            static bool Prefix(string path, string contents)
            {
                try
                {
                    // Check if this is PEAKER trying to write to banned.txt
                    if (path.EndsWith("banned.txt") && contents.Contains(" | ") && Plugin.Instance.IsHost() && Plugin.Instance.AutoDetectHacks)
                    {
                        Plugin.Log.LogInfo((object)$"Intercepting PEAKER file write: {contents}");
                        
                        // Parse the SteamID from the contents
                        // Format: "\n{SteamID} | {PlayerName} | {Reason}"
                        string[] parts = contents.Split(new char[] { '|' }, 3);
                        if (parts.Length >= 1)
                        {
                            string steamIDPart = parts[0].Trim();
                            if (steamIDPart.StartsWith("\n"))
                            {
                                steamIDPart = steamIDPart.Substring(1).Trim();
                            }
                            
                            if (ulong.TryParse(steamIDPart, out ulong steamID))
                            {
                                CSteamID cSteamID = new CSteamID(steamID);
                                Plugin.Log.LogInfo((object)$"Redirecting PEAKER ban for SteamID: {steamID}");
                                
                                // Handle the ban through our system
                                bool result = HandlePeakerBanButton(cSteamID);
                                
                                // If we handled it, prevent the original file write
                                if (!result)
                                {
                                    return false;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError((object)$"Error in PEAKER file write patch: {ex.Message}");
                }
                
                // Let the original method run if we're not intercepting
                return true;
            }
        }
        
        // Add new method to handle PEAKER ban button clicks
        private static bool HandlePeakerBanButton(CSteamID lobbyMemberByIndex)
        {
            try
            {
                // Only redirect if we're the host
                if (!Plugin.Instance.IsHost())
                {
                    return true; // Let the original method run
                }

                // Find the Photon player that matches this Steam ID
                Photon.Realtime.Player targetPlayer = null;
                foreach (var player in PhotonNetwork.PlayerList)
                {
                    var playerSteamID = SteamIntegrationManager.Instance.GetPlayerSteamID(player);
                    if (playerSteamID == lobbyMemberByIndex.m_SteamID.ToString())
                    {
                        targetPlayer = player;
                        break;
                    }
                }

                if (targetPlayer != null)
                {
                    Plugin.Log.LogInfo((object)$"Redirecting PEAKER ban to PeakBanMod: {targetPlayer.NickName}");
                    
                    // Ban through our system
                    BanManager.BanPlayer(targetPlayer, SteamIntegrationManager.Instance.GetPlayerSteamID(targetPlayer), "Banned via PEAKER UI");
                    
                    // Prevent the original file write
                    return false;
                }
                else
                {
                    // If we can't find the player in the current room, we still want to prevent
                    // the file write and handle it through our system
                    Plugin.Log.LogInfo((object)$"Redirecting PEAKER ban to PeakBanMod for SteamID: {lobbyMemberByIndex.m_SteamID} (player not in room)");
                    
                    // Ban by Steam ID through our system
                    BanManager.BanPlayerBySteamID(lobbyMemberByIndex.m_SteamID.ToString(), "Banned via PEAKER UI");
                    
                    // Prevent the original file write
                    return false;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError((object)$"Error in HandlePeakerBanButton: {ex.Message}");
            }
            
            // Let the original method run if there was an error or we're not redirecting
            return true;
        }
        
        
        
        private static void ApplyPeakerPatches()
        {
            if (_peakerPatchesApplied) return;

            try
            {
                // Use reflection to safely get PEAKER types and patch them
                var cheatDetectionsType = Type.GetType("PEAKER.CheatDetections, PEAKER");
                if (cheatDetectionsType != null)
                {
                    // Patch specific methods using reflection
                    var harmony = new Harmony("com.icemods.hostbanmod.peakerintegration");

                    // Apply ban button patch manually
                    var banButtonTarget = PeakerBanButtonPatch.GetTargetMethod();
                    if (banButtonTarget != null)
                    {
                        harmony.Patch(banButtonTarget, postfix: new HarmonyMethod(typeof(PeakerBanButtonPatch), "Postfix"));
                        Plugin.Log.LogInfo((object)"Successfully applied PEAKER ban button patch");
                    }
                    else
                    {
                        Plugin.Log.LogWarning((object)"Failed to apply PEAKER ban button patch - target method not found");
                    }

                    // Apply custom property detection patch manually
                    var customPropTarget = CustomPropertyDetectionPatch.GetTargetMethod();
                    if (customPropTarget != null)
                    {
                        harmony.Patch(customPropTarget, postfix: new HarmonyMethod(typeof(CustomPropertyDetectionPatch), "Postfix"));
                        Plugin.Log.LogInfo((object)"Successfully applied PEAKER custom property detection patch");
                    }
                    else
                    {
                        Plugin.Log.LogWarning((object)"Failed to apply PEAKER custom property detection patch - target method not found");
                    }

                    // Patch various cheat detection methods
                    PatchMethodIfExists(harmony, cheatDetectionsType, "PostBananaPeelRPCA_TriggerBanana");
                    PatchMethodIfExists(harmony, cheatDetectionsType, "PostBeeSwarmDisperseRPC");
                    PatchMethodIfExists(harmony, cheatDetectionsType, "PostBeeSwarmSetBeesAngryRPC");
                    PatchMethodIfExists(harmony, cheatDetectionsType, "PostBugfixAttachBug");
                    PatchMethodIfExists(harmony, cheatDetectionsType, "PostFlareTriggerHelicopter");
                    PatchMethodIfExists(harmony, cheatDetectionsType, "PostCharacterRPCEndGame");
                    PatchMethodIfExists(harmony, cheatDetectionsType, "PostCharacterRPCA_Die");
                    PatchMethodIfExists(harmony, cheatDetectionsType, "PostCharacterRPCA_ReviveAtPosition");
                    PatchMethodIfExists(harmony, cheatDetectionsType, "PostCharacterWarpPlayerRPC");
                    PatchMethodIfExists(harmony, cheatDetectionsType, "PostPlayerGhostRPCA_InitGhost");
                    PatchMethodIfExists(harmony, cheatDetectionsType, "PostPlayerGhostRPCA_SetTarget");

                    _peakerPatchesApplied = true;
                    Plugin.Log.LogInfo((object)"Successfully applied PEAKER integration patches");
                }
                else
                {
                    Plugin.Log.LogWarning((object)"Could not find PEAKER.CheatDetections type for patching");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning((object)$"Failed to apply PEAKER patches: {ex.Message}");
            }
        }
        
        private static void PatchMethodIfExists(Harmony harmony, Type type, string methodName)
        {
            try
            {
                var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                if (method != null)
                {
                    var patchMethod = typeof(PeakerIntegration).GetMethod(
                        "OnPeakerCheatDetected", 
                        BindingFlags.NonPublic | BindingFlags.Static);
                    if (patchMethod != null)
                    {
                        harmony.Patch(method, postfix: new HarmonyMethod(patchMethod));
                        Plugin.Log.LogDebug((object)$"Successfully patched PEAKER method: {methodName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogDebug((object)$"Could not patch {methodName}: {ex.Message}");
            }
        }
        
        private static void OnPeakerCheatDetected(object __0, object __1, object __2, object __3)
        {
            // This will be called whenever PEAKER detects cheating through Harmony patches
            // Parameters vary by method, but we can extract what we need
            try
            {
                // Try to extract player info from the method parameters
                Photon.Realtime.Player player = null;
                string reason = "PEAKER Cheat Detection";
                
                // Try to identify the player from various parameter positions
                if (__0 is Photon.Realtime.Player p0) player = p0;
                else if (__1 is Photon.Realtime.Player p1) player = p1;
                else if (__2 is Photon.Realtime.Player p2) player = p2;
                else if (__3 is Photon.Realtime.Player p3) player = p3;
                
                // Try to get more specific reason from parameters
                if (__1 is string s1 && !string.IsNullOrEmpty(s1)) reason = s1;
                else if (__2 is string s2 && !string.IsNullOrEmpty(s2)) reason = s2;
                
                if (player != null && Plugin.Instance.AutoDetectHacks && Plugin.Instance.IsHost())
                {
                    string playerName = player.NickName;
                    string playerSteamID = SteamIntegrationManager.Instance.GetPlayerSteamID(player);
                    
                    Plugin.Log.LogWarning((object)$"PEAKER detected cheater via patch: {playerName} (SteamID: {playerSteamID}) - {reason}");
                    
                    // Ban the player through PeakBanMod
                    BanManager.BanPlayer(player, SteamIntegrationManager.Instance.GetPlayerSteamID(player), $"Auto Ban: {reason} (PEAKER Detection via Patch)");
                    
                    // Send message to chat
                    string message = $"<color=red>Player {playerName} was banned for using cheats (detected by PEAKER)</color>";
                    UtilityManager.Instance.SendMessage(message);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError((object)$"Error handling PEAKER cheat detection via patch: {ex.Message}");
            }
        }
        
        // Also patch the custom property detection method
        private class CustomPropertyDetectionPatch
        {
            internal static MethodBase GetTargetMethod()
            {
                try
                {
                    var cheatDetectionsType = Type.GetType("PEAKER.CheatDetections, PEAKER");
                    if (cheatDetectionsType != null)
                    {
                        return AccessTools.Method(cheatDetectionsType, "CheckScoutForMods");
                    }
                    else
                    {
                        Plugin.Log.LogWarning((object)"Could not find PEAKER.CheatDetections type for custom property detection patch");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning((object)$"Error in CustomPropertyDetectionPatch.GetTargetMethod(): {ex.Message}");
                }
                return null;
            }
            
            static void Postfix(object __0)
            {
                // This patches CheckScoutForMods method in PEAKER.CheatDetections
                try
                {
                    // __0 should be the Player parameter
                    if (__0 is Photon.Realtime.Player player && Plugin.Instance.AutoDetectHacks && Plugin.Instance.IsHost())
                    {
                        // Check if the player has cheating properties
                        if (player.CustomProperties != null)
                        {
                            string detectedCheat = null;
                            if (((System.Collections.Generic.Dictionary<object, object>)(object)player.CustomProperties).ContainsKey((object)"CherryUser"))
                                detectedCheat = "Cherry Mod User";
                            else if (((System.Collections.Generic.Dictionary<object, object>)(object)player.CustomProperties).ContainsKey((object)"CherryOwner"))
                                detectedCheat = "Cherry Mod Owner";
                            else if (((System.Collections.Generic.Dictionary<object, object>)(object)player.CustomProperties).ContainsKey((object)"AtlUser"))
                                detectedCheat = "Atlas Mod User";
                            else if (((System.Collections.Generic.Dictionary<object, object>)(object)player.CustomProperties).ContainsKey((object)"AtlOwner"))
                                detectedCheat = "Atlas Mod Owner";
                            
                            if (detectedCheat != null)
                            {
                                string playerName = player.NickName;
                                string playerSteamID = SteamIntegrationManager.Instance.GetPlayerSteamID(player);

                                Plugin.Log.LogWarning((object)$"PEAKER detected cheater via custom properties: {playerName} (SteamID: {playerSteamID}) - {detectedCheat}");

                                // Ban the player through PeakBanMod
                                BanManager.BanPlayer(player, playerSteamID, $"Auto Ban: {detectedCheat} (PEAKER Detection via Patch)");

                                // Send message to chat
                                string message = $"<color=red>Player {playerName} was banned for using cheats (detected by PEAKER)</color>";
                                UtilityManager.Instance.SendMessage(message);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError((object)$"Error in custom property detection patch: {ex.Message}");
                }
            }
        }
    }
    
    internal static class AntiCheatDependency
    {
        internal const string PluginGuid = "lammas123.PEAKER";
    
        internal static bool ListenForCheaterDetections(Action<Photon.Realtime.Player, string, CSteamID, string> listener)
        {
            if (!Chainloader.PluginInfos.TryGetValue(PluginGuid, out var pluginInfo))
            {
                return false;
            }
            
            Assembly assembly = pluginInfo.Instance?.GetType()?.Assembly;
            if (assembly == null)
            {
                return false;
            }
            
            Type type = assembly.GetType("PEAKER.AntiCheatDependency");
            if (type == null)
            {
                return false;
            }
            
            MethodInfo method = type.GetMethod("ListenForCheaterDetections", BindingFlags.Static | BindingFlags.Public);
            if (method == null)
            {
                return false;
            }
            
            // Create delegate for the method
            var listenMethod = (Func<Action<Photon.Realtime.Player, string, CSteamID, string>, bool>)Delegate.CreateDelegate(
                typeof(Func<Action<Photon.Realtime.Player, string, CSteamID, string>, bool>), method);
            
            return listenMethod(listener);
        }
    }
}