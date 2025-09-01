using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using PeakNetworkDisconnectorMod.Core;
using PeakNetworkDisconnectorMod.Managers;

namespace PeakNetworkDisconnectorMod;

[BepInPlugin("com.unluckyjori.peakbanmod", "Peak Ban Mod", "1.0.0")]
[BepInProcess("PEAK.exe")]
public class Plugin : BaseUnityPlugin
{
	internal static ManualLogSource Log;

	// Add PEAKER integration field
	private PeakerIntegration _peakerIntegration;

	// Add PeakAntiCheat integration field
	private PeakAntiCheatIntegration _peakAntiCheatIntegration;

	// Add PeakAntiCheat item drop patch field
	private PeakAntiCheatItemDropPatch _peakAntiCheatItemDropPatch;

	// Add EnforcementManager field
	private Managers.EnforcementManager _enforcementManager;

	// Add SteamIntegrationManager field
	private SteamIntegrationManager _steamIntegrationManager;

	// Add UtilityManager field
	private Managers.UtilityManager _utilityManager;

	// Public property to access autoDetectHacks from config
	public bool AutoDetectHacks => ConfigManager.AutoDetectHacks;

 	// Cached timers for performance optimization
 	private float _lastBanCheckTime;
 	private float _lastSteamUpdateTime;
 	private float _lastStatsLogTime;
 	private const float BAN_CHECK_INTERVAL = 0.5f;
 	private const float STEAM_UPDATE_INTERVAL = 30f;
 	private const float STATS_LOG_INTERVAL = 60f;

  // Additional caching for performance
  private bool _cachedIsHost;
  private float _lastHostCheckTime;
  private string _cachedEnforcementMode;
  private float _lastModeCheckTime;
  private bool _cachedInRoom;
  private float _lastInRoomCheckTime;
  private const float HOST_CHECK_CACHE_TIME = 0.1f; // Cache host status for 100ms
  private const float MODE_CHECK_CACHE_TIME = 1.0f; // Cache enforcement mode for 1 second
  private const float IN_ROOM_CACHE_TIME = 0.1f; // Cache in-room status for 100ms
	public static readonly HashSet<string> KnownHackProperties = new HashSet<string> { "CherryUser", "AtlUser", "AtlOwner", "HackUser", "CheatUser" };

	public static Plugin Instance { get; private set; }

	// Public accessors for UIManager

	/// <summary>
	/// Gets the PEAKER integration instance for accessing PEAKER-specific functionality
	/// </summary>
	public PeakerIntegration PeakerIntegrationInstance => _peakerIntegration;

	/// <summary>
	/// Gets the PeakAntiCheat integration instance for accessing anti-cheat functionality
	/// </summary>
	public PeakAntiCheatIntegration PeakAntiCheatIntegrationInstance => _peakAntiCheatIntegration;

	/// <summary>
	/// Gets the PeakAntiCheat item drop patch instance for bypassing item detection
	/// </summary>
	public PeakAntiCheatItemDropPatch PeakAntiCheatItemDropPatchInstance => _peakAntiCheatItemDropPatch;

	/// <summary>
	/// Gets the enforcement manager instance for handling player enforcement actions
	/// </summary>
	public Managers.EnforcementManager EnforcementManagerInstance => _enforcementManager;

	/// <summary>
	/// Gets the total number of packets sent by the enforcement manager
	/// </summary>
	public int PacketsCount => _enforcementManager?.PacketsCount ?? 0;

	/// <summary>
	/// Gets the human-readable name for a hack property key
	/// </summary>
	/// <param name="propertyKey">The property key to convert (e.g., "CherryUser", "AtlUser")</param>
	/// <returns>The human-readable hack name, or "Unknown" if not recognized</returns>
	public static string GetHackName(string propertyKey)
	{
		switch (propertyKey)
		{
		case "CherryUser":
			return "Cherry";
		case "AtlUser":
		case "AtlOwner":
			return "Atlas";
		case "HackUser":
			return "Hack";
		case "CheatUser":
			return "Cheat";
		default:
			return "Unknown";
		}
	}

	/// <summary>
	/// Main entry point for the PeakNetworkDisconnectorMod
	/// Initializes all managers and sets up the mod functionality
	/// </summary>
	private void Awake()
	{
			Instance = this;
			Log = base.Logger;

			// Initialize centralized error handler
			ErrorHandler.Initialize(Log);

			// Initialize standardized logger
			PeakNetworkDisconnectorMod.Logger.Initialize(Log, PeakNetworkDisconnectorMod.Logger.LogLevel.Info, true);

			try
			{
				// Initialize centralized configuration manager
				PeakNetworkDisconnectorMod.Logger.Info("Initializing configuration manager", "Plugin");
				ConfigManager.Initialize(Config);

				// Validate configuration on startup
				PeakNetworkDisconnectorMod.Logger.Info("Validating configuration", "Plugin");
				ConfigManager.ValidateConfiguration();

			// Initialize ban manager
			PeakNetworkDisconnectorMod.Logger.Info("Initializing ban manager", "Plugin");
			string banListPath = Path.Combine(Paths.ConfigPath, "peak_host_banlist.json");
			BanManager.Initialize(Log, banListPath);

			// Initialize PEAKER integration
			PeakNetworkDisconnectorMod.Logger.Info("Initializing PEAKER integration", "Plugin");
			_peakerIntegration = ((Component)this).gameObject.AddComponent<PeakerIntegration>();

			// Initialize PeakAntiCheat integration
			PeakNetworkDisconnectorMod.Logger.Info("Initializing PeakAntiCheat integration", "Plugin");
			_peakAntiCheatIntegration = ((Component)this).gameObject.AddComponent<PeakAntiCheatIntegration>();

			// Initialize PeakAntiCheat item drop patch
			PeakNetworkDisconnectorMod.Logger.Info("Initializing PeakAntiCheat item drop patch", "Plugin");
			_peakAntiCheatItemDropPatch = ((Component)this).gameObject.AddComponent<PeakAntiCheatItemDropPatch>();

			// Initialize UIManager
			PeakNetworkDisconnectorMod.Logger.Info("Initializing UI manager", "Plugin");
			var uiManager = ((Component)this).gameObject.AddComponent<UIManager>();

			// Initialize EnforcementManager
			PeakNetworkDisconnectorMod.Logger.Info("Initializing enforcement manager", "Plugin");
			_enforcementManager = ((Component)this).gameObject.AddComponent<Managers.EnforcementManager>();
			_enforcementManager.Initialize(this, Log);

			// Initialize SteamIntegrationManager
			PeakNetworkDisconnectorMod.Logger.Info("Initializing Steam integration manager", "Plugin");
			_steamIntegrationManager = ((Component)this).gameObject.AddComponent<SteamIntegrationManager>();

			// Initialize NetworkManager
			PeakNetworkDisconnectorMod.Logger.Info("Initializing network manager", "Plugin");
			var networkManager = ((Component)this).gameObject.AddComponent<Managers.NetworkManager>();
			networkManager.Initialize(Log, new System.Collections.Generic.Dictionary<int, string>());
			PhotonNetwork.AddCallbackTarget(networkManager);

			// Initialize UtilityManager
			PeakNetworkDisconnectorMod.Logger.Info("Initializing utility manager", "Plugin");
			_utilityManager = ((Component)this).gameObject.AddComponent<Managers.UtilityManager>();
			_utilityManager.Initialize(Log);

			// Apply Harmony patches
			PeakNetworkDisconnectorMod.Logger.Info("Applying Harmony patches", "Plugin");
			new Harmony("com.icemods.hostbanmod").PatchAll();

			// Log successful initialization
			PeakNetworkDisconnectorMod.Logger.Info("PEAK BAN SYSTEM Mod loaded successfully! Only host can disconnect players.", "Plugin");
			Log.LogInfo((object)"PEAK BAN SYSTEM Mod loaded! Only host can disconnect players.");

			// Send in-game message about the mod and keybind
			string message = $"<color=green>PeakBanMod loaded! Press {ConfigManager.ToggleKeybind} to open the ban menu.</color>";
			_utilityManager.SendMessage(message);

			// Check Steam initialization
			PeakNetworkDisconnectorMod.Logger.Info("Checking Steam initialization", "Plugin");
			_steamIntegrationManager.CheckSteamInitialization();

			// Log initialization completion with performance metrics
			PeakNetworkDisconnectorMod.Logger.Info("Plugin initialization completed successfully", "Plugin");
		}
		catch (Exception ex)
		{
			ErrorHandler.HandleError(ex, "Awake", false, "Failed to initialize Network Disconnector Mod");
		}
	}

	private void Update()
	{
		try
		{
			// Use cached host check for UI toggle
			if (Input.GetKeyDown(ConfigManager.ToggleKeybind) && IsHostCached())
			{
				UIManager.Instance?.ToggleUI();
			}

			// Replace Time.time % 0.5f check with cached timer
			float currentTime = Time.time;
			bool isHostCached = IsHostCached();

			if (isHostCached && IsInRoomCached() && currentTime - _lastBanCheckTime >= BAN_CHECK_INTERVAL)
			{
				_lastBanCheckTime = currentTime;
				BanManager.CheckForBannedPlayers();
				// Re-enable auto-detection logic - now handled by PEAKER patches when available
				if (ConfigManager.AutoDetectHacks)
				{
					BanManager.CheckForHackUsers();
				}
			}

			if (isHostCached && IsInRoomCached())
			{
				// Apply enforcement based on cached enforcement mode
				string currentMode = GetEnforcementModeCached();
				if (currentMode == "Aggressive")
				{
					_enforcementManager.ApplyAggressiveKickActions(EnforcementManager.Instance.TargetPlayers);
				}
				else if (currentMode == "Passive")
				{
					// Passive is handled through ApplyKickActions() which is called below
				}
			}

			if (isHostCached && IsInRoomCached())
			{
				_enforcementManager.ApplyKickActions();
			}

			// Replace Time.time % 60f check with cached timer
			if (currentTime - _lastStatsLogTime >= STATS_LOG_INTERVAL && currentTime > 10f && _enforcementManager.PacketsCount > 0)
			{
				_lastStatsLogTime = currentTime;
				int packetCount = _enforcementManager.PacketsCount;
				PeakNetworkDisconnectorMod.Logger.Info($"Network Disconnector Stats - Packets sent: {packetCount}", "Plugin");
				Log.LogInfo((object)$"Network Disconnector Stats - Packets sent: {packetCount}");
			}

			// Replace Time.time % 30f check with cached timer - use cached host check
			if (currentTime - _lastSteamUpdateTime >= STEAM_UPDATE_INTERVAL && isHostCached && IsInRoomCached())
			{
				_lastSteamUpdateTime = currentTime;
				_steamIntegrationManager.UpdatePlayerSteamIDs();
			}
		}
		catch (Exception ex)
		{
			ErrorHandler.HandleError(ex, "Update", false, "Error in Update method");
		}
	}

	/// <summary>
	/// Checks if the current player is the room host/master client
	/// </summary>
	/// <returns>True if the current player is the host, false otherwise</returns>
 	public bool IsHost()
 	{
 		return PhotonNetwork.IsMasterClient;
 	}

 	/// <summary>
 	/// Cached version of IsHost() for performance optimization
 	/// </summary>
 	private bool IsHostCached()
 	{
 		float currentTime = Time.time;
 		if (currentTime - _lastHostCheckTime >= HOST_CHECK_CACHE_TIME)
 		{
 			_cachedIsHost = PhotonNetwork.IsMasterClient;
 			_lastHostCheckTime = currentTime;
 		}
 		return _cachedIsHost;
 	}

  /// <summary>
  /// Cached version of ConfigManager.EnforcementMode for performance optimization
  /// </summary>
  private string GetEnforcementModeCached()
  {
  		float currentTime = Time.time;
  		if (currentTime - _lastModeCheckTime >= MODE_CHECK_CACHE_TIME)
  		{
  			_cachedEnforcementMode = ConfigManager.EnforcementMode;
  			_lastModeCheckTime = currentTime;
  		}
  		return _cachedEnforcementMode;
  }

  /// <summary>
  /// Cached version of PhotonNetwork.InRoom for performance optimization
  /// </summary>
  private bool IsInRoomCached()
  {
  		float currentTime = Time.time;
  		if (currentTime - _lastInRoomCheckTime >= IN_ROOM_CACHE_TIME)
  		{
  			_cachedInRoom = PhotonNetwork.InRoom;
  			_lastInRoomCheckTime = currentTime;
  		}
  		return _cachedInRoom;
  }

	// Modify ApplyFloodActions() method to be more explicit about AggressiveKick

	// Add player freezing functionality for PassiveKick mode



	/// <summary>
	/// Gets the current enforcement mode from configuration
	/// </summary>
	/// <returns>The current enforcement mode ("Aggressive" or "Passive")</returns>
	public string GetEnforcementMode()
	{
		return ConfigManager.EnforcementMode;
	}
}
