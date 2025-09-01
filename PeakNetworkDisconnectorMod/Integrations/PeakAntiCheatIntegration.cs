using System;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using Photon.Realtime;
using Steamworks;
using UnityEngine;
using PeakNetworkDisconnectorMod.Core;
using PeakNetworkDisconnectorMod.Managers;

namespace PeakNetworkDisconnectorMod
{
	public class PeakAntiCheatIntegration : MonoBehaviour
	{
		private static bool _peakAntiCheatDetected = false;
		private static bool _peakAntiCheatPatchesApplied = false;
		private static Harmony _harmony;
		private bool _hasChecked = false;

		public static bool IsPeakAntiCheatDetected => _peakAntiCheatDetected;
		public static bool IsPeakAntiCheatPatchesApplied => _peakAntiCheatPatchesApplied;
		
		internal void Update()
		{
			if (!_hasChecked)
			{
				_hasChecked = true;
				// Check if PeakAntiCheat is installed
				_peakAntiCheatDetected = Chainloader.PluginInfos.ContainsKey("com.hiccup444.anticheat");

				if (_peakAntiCheatDetected)
				{
					Plugin.Log.LogInfo((object)"PeakAntiCheat mod detected, applying integration");
					try
					{
						ApplyPeakAntiCheatIntegration();

						// Apply Harmony patches for PeakAntiCheat integration
						if (_harmony == null)
						{
							_harmony = new Harmony("com.unluckyjori.peakanticheat.integration");
							ApplyPeakAntiCheatPatches();
						}
					}
					catch (Exception ex)
					{
						Plugin.Log.LogWarning((object)$"Error during PeakAntiCheat integration: {ex.Message}");
					}
				}
				else
				{
					Plugin.Log.LogInfo((object)"PeakAntiCheat mod not detected - integration features will be unavailable");
				}
			}
		}
		
		private static void ApplyPeakAntiCheatIntegration()
		{
			if (_peakAntiCheatPatchesApplied) return;

			try
			{
				// Listen for cheat detection events from PeakAntiCheat
				if (ListenForPeakAntiCheatDetections(OnPeakAntiCheatDetected))
				{
					_peakAntiCheatPatchesApplied = true;
					Plugin.Log.LogInfo((object)"Successfully applied PeakAntiCheat integration");
				}
				else
				{
					Plugin.Log.LogWarning((object)"Failed to apply PeakAntiCheat integration - some features may not work");
					// Don't set _peakAntiCheatPatchesApplied to true so we can retry later
				}
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning((object)$"Failed to apply PeakAntiCheat integration: {ex.Message}");
				// Don't set _peakAntiCheatPatchesApplied to true so we can retry later
			}
		}
		
		private static bool ListenForPeakAntiCheatDetections(Action<Photon.Realtime.Player, string, CSteamID, string> listener)
		{
			const string pluginGuid = "com.hiccup444.anticheat";

			if (!Chainloader.PluginInfos.TryGetValue(pluginGuid, out var pluginInfo))
			{
				Plugin.Log.LogWarning((object)"PeakAntiCheat plugin not found in Chainloader");
				return false;
			}

			Assembly assembly = pluginInfo.Instance?.GetType()?.Assembly;
			if (assembly == null)
			{
				Plugin.Log.LogWarning((object)"Could not get PeakAntiCheat assembly");
				return false;
			}

			// Try multiple possible type names
			Type type = null;
			string[] possibleTypeNames = {
				"AntiCheatMod.AntiCheatEvents",
				"AntiCheatEvents",
				"PEAKAntiCheat.AntiCheatEvents",
				"AntiCheatMod.Events",
				"Events",
				"PEAKAntiCheat.Events"
			};

			foreach (string typeName in possibleTypeNames)
			{
				type = assembly.GetType(typeName);
				if (type != null)
				{
					Plugin.Log.LogInfo((object)$"Found AntiCheatEvents type: {typeName}");
					break;
				}
			}

			if (type == null)
			{
				Plugin.Log.LogWarning((object)"Could not find AntiCheatEvents type in PeakAntiCheat assembly");
				// Log all available types for debugging
				try
				{
					var allTypes = assembly.GetTypes();
					var eventTypeNames = new System.Collections.Generic.List<string>();
					foreach (var t in allTypes)
					{
						if (t.Name.Contains("Event"))
						{
							eventTypeNames.Add(t.FullName);
						}
					}
					if (eventTypeNames.Count > 0)
					{
						Plugin.Log.LogInfo((object)$"Available event-related types: {string.Join(", ", eventTypeNames)}");
					}
				}
				catch (Exception ex)
				{
					Plugin.Log.LogWarning((object)$"Error enumerating types: {ex.Message}");
				}
				return false;
			}

			// Try multiple possible field names for the cheater detected event
			FieldInfo eventField = null;
			string[] possibleFieldNames = {
				"OnCheaterDetected",
				"CheaterDetected",
				"OnCheaterDetectedEvent",
				"CheaterDetectedEvent"
			};

			foreach (string fieldName in possibleFieldNames)
			{
				eventField = type.GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
				if (eventField != null)
				{
					Plugin.Log.LogInfo((object)$"Found cheater detected event field: {fieldName}");
					break;
				}
			}

			if (eventField == null)
			{
				Plugin.Log.LogWarning((object)$"Could not find any cheater detected event field in type {type.FullName}");

				// Log all available fields for debugging
				try
				{
					var allFields = type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
					if (allFields.Length > 0)
					{
						var fieldNames = new System.Collections.Generic.List<string>();
						foreach (var f in allFields)
						{
							if (f.Name.ToLower().Contains("cheat") || f.Name.ToLower().Contains("detect") || f.Name.ToLower().Contains("event"))
							{
								fieldNames.Add($"{f.Name} ({f.FieldType.Name})");
							}
						}
						if (fieldNames.Count > 0)
						{
							Plugin.Log.LogInfo((object)$"Available cheat/detection/event fields: {string.Join(", ", fieldNames)}");
						}
						else
						{
							// If no relevant fields found, list all fields
							fieldNames.Clear();
							foreach (var f in allFields)
							{
								fieldNames.Add($"{f.Name} ({f.FieldType.Name})");
							}
							Plugin.Log.LogInfo((object)$"All available static fields: {string.Join(", ", fieldNames)}");
						}
					}
					else
					{
						Plugin.Log.LogWarning((object)"No static fields found in the type");
					}
				}
				catch (Exception ex)
				{
					Plugin.Log.LogWarning((object)$"Error enumerating fields: {ex.Message}");
				}
				return false;
			}

			try
			{
				// Subscribe to the event
				var currentEvent = (MulticastDelegate)eventField.GetValue(null);
				var handler = new Action<Photon.Realtime.Player, string, CSteamID, string>(listener);
				eventField.SetValue(null, Delegate.Combine(currentEvent, handler));
				Plugin.Log.LogInfo((object)"Successfully subscribed to PeakAntiCheat OnCheaterDetected event");
				return true;
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError((object)$"Error subscribing to OnCheaterDetected event: {ex.Message}");
				Plugin.Log.LogError((object)$"Stack trace: {ex.StackTrace}");
				if (ex.InnerException != null)
				{
					Plugin.Log.LogError((object)$"Inner exception: {ex.InnerException.Message}");
				}
				return false;
			}
		}
		
		private static void OnPeakAntiCheatDetected(Photon.Realtime.Player player, string reason, CSteamID steamID, string timestamp)
		{
			try
			{
				if (player != null && Plugin.Instance.AutoDetectHacks && Plugin.Instance.IsHost())
				{
					string playerName = player.NickName;
					string playerSteamID = SteamIntegrationManager.Instance.GetPlayerSteamID(player);

					Plugin.Log.LogWarning((object)$"PeakAntiCheat detected cheater: {playerName} (SteamID: {playerSteamID}) - {reason}");

					// Ban the player through PeakBanMod
					BanManager.BanPlayer(player, playerSteamID, $"Auto Ban: {reason} (PeakAntiCheat Detection)");

					// Apply selected enforcement method based on current setting
					string currentEnforcementMode = Plugin.Instance.GetEnforcementMode();

					if (currentEnforcementMode == "Passive")
					{
						Plugin.Instance.GetType().GetMethod("ApplyPassiveKickActions", BindingFlags.NonPublic | BindingFlags.Instance)
							?.Invoke(Plugin.Instance, new object[] { player });
					}
					else
					{
						// Default to Aggressive (existing behavior)
						Plugin.Instance.EnforcementManagerInstance?.StartTargetingPlayer(player);
					}

					// Send message to chat
					string message = $"<color=red>Player {playerName} was banned for using cheats (detected by PeakAntiCheat)</color>";
					UtilityManager.Instance.SendMessage(message);
				}
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError((object)$"Error handling PeakAntiCheat detection: {ex.Message}");
			}
		}
		
		private static void ApplyPeakAntiCheatPatches()
		{
			try
			{
				// Find PeakAntiCheat's assembly
				if (!Chainloader.PluginInfos.TryGetValue("com.hiccup444.anticheat", out var pluginInfo))
				{
					Plugin.Log.LogWarning((object)"PeakAntiCheat plugin not found for patching - skipping");
					return;
				}

				Assembly assembly = pluginInfo.Instance?.GetType()?.Assembly;
				if (assembly == null)
				{
					Plugin.Log.LogWarning((object)"Could not get PeakAntiCheat assembly for patching - skipping");
					return;
				}

				Plugin.Log.LogInfo((object)$"Successfully loaded PeakAntiCheat assembly for patching: {assembly.FullName}");

				// Patch BlockingManager.BlockPlayer method
				Type blockingManagerType = assembly.GetType("AntiCheatMod.BlockingManager");
				if (blockingManagerType == null)
				{
					Plugin.Log.LogError((object)"Could not find BlockingManager type in PeakAntiCheat");
					return;
				}

				MethodInfo blockPlayerMethod = blockingManagerType.GetMethod("BlockPlayer", BindingFlags.Public | BindingFlags.Static);
				if (blockPlayerMethod == null)
				{
					Plugin.Log.LogError((object)"Could not find BlockPlayer method in PeakAntiCheat");
					return;
				}

				MethodInfo prefixMethod = typeof(PeakAntiCheatIntegration).GetMethod("BlockPlayerPrefix", BindingFlags.NonPublic | BindingFlags.Static);
				if (prefixMethod == null)
				{
					Plugin.Log.LogError((object)"Could not find BlockPlayerPrefix method in PeakAntiCheatIntegration");
					return;
				}

				_harmony.Patch(blockPlayerMethod, new HarmonyMethod(prefixMethod));
				Plugin.Log.LogInfo((object)"Successfully patched PeakAntiCheat's BlockPlayer method");
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError((object)$"Failed to patch PeakAntiCheat methods: {ex.Message}");
			}
		}

		// Add new method at the end of the class
		private static bool BlockPlayerPrefix(Photon.Realtime.Player player, string reason, object blockReason, CSteamID steamID, object detectionType)
		{
			try
			{
				// Only redirect if we're the host
				if (!Plugin.Instance.IsHost())
				{
					return true; // Let the original method run
				}

				// Check if this is a UI-initiated block (not an automated detection)
				bool isUIBlock = blockReason?.ToString() == "Manual";

				// Don't redirect automated detections if auto-detect is disabled
				// Manual bans (UI-initiated) should still work even when auto-detect is off
				if (!Plugin.Instance.AutoDetectHacks && !isUIBlock)
				{
					return true; // Let the original method run
				}

				// If using our enforcement or this is a UI block, redirect to our ban system
				// Note: Auto-detect setting doesn't affect manual bans
				string currentEnforcementMode = Plugin.Instance.GetEnforcementMode();
				if (currentEnforcementMode == "Passive" || currentEnforcementMode == "Aggressive" || isUIBlock)
				{
					Plugin.Log.LogInfo((object)$"Redirecting PeakAntiCheat block to PeakBanMod: {player.NickName} - {reason}");
					
					// Ban through our system
					BanManager.BanPlayer(player, SteamIntegrationManager.Instance.GetPlayerSteamID(player), $"PeakAntiCheat Block: {reason}");
					
					// Apply enforcement based on current setting
					if (currentEnforcementMode == "Passive")
					{
						Plugin.Instance.GetType().GetMethod("ApplyPassiveKickActions", BindingFlags.NonPublic | BindingFlags.Instance)
							?.Invoke(Plugin.Instance, new object[] { player });
					}
					else
					{
						Plugin.Instance.EnforcementManagerInstance?.StartTargetingPlayer(player);
					}
					
					// Prevent the original BlockPlayer method from running
					return false;
				}
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError((object)$"Error in BlockPlayerPrefix: {ex.Message}");
			}
			
			// Let the original method run if there was an error or we're not redirecting
			return true;
		}
	}
}
