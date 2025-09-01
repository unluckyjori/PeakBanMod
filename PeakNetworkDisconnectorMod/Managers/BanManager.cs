using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Logging;
using Newtonsoft.Json;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using PeakNetworkDisconnectorMod.Core;
using PeakNetworkDisconnectorMod.Managers;

namespace PeakNetworkDisconnectorMod;

/// <summary>
/// Manages player banning and unbanning operations
/// Handles ban list persistence and validation
/// </summary>
public static class BanManager
{
    private static List<BannedPlayer> _bannedPlayers = new List<BannedPlayer>();
    private static readonly object _banListLock = new object();
    private static Dictionary<string, float> _recentlyUnbannedPlayers = new Dictionary<string, float>();
    private static readonly object _recentlyUnbannedLock = new object();
    private static HashSet<string> _bannedNames = new HashSet<string>();
    private static HashSet<string> _bannedSteamIDs = new HashSet<string>();
    private static string _banListPath;
    private static ManualLogSource _logger;

    // Steam ID caching for performance optimization
    private static Dictionary<int, string> _steamIDCache = new Dictionary<int, string>();
    private static float _lastSteamIDCacheUpdate;
    private const float STEAM_ID_CACHE_UPDATE_INTERVAL = 5.0f; // Update cache every 5 seconds

    private const float UNBAN_PROTECTION_TIME = 10f;

    /// <summary>
    /// Initialize the ban manager
    /// </summary>
    public static void Initialize(ManualLogSource logger, string banListPath)
    {
        _logger = logger;
        _banListPath = banListPath;
        LoadBanList();
    }

    /// <summary>
    /// Update Steam ID cache for performance optimization
    /// </summary>
    private static void UpdateSteamIDCache()
    {
        if (!PhotonNetwork.InRoom || Time.time - _lastSteamIDCacheUpdate < STEAM_ID_CACHE_UPDATE_INTERVAL)
        {
            return;
        }

        _steamIDCache.Clear();
        Photon.Realtime.Player[] playerList = PhotonNetwork.PlayerList;
        foreach (Photon.Realtime.Player player in playerList)
        {
            string steamID = SteamIntegrationManager.Instance?.GetPlayerSteamID(player) ?? "Unknown";
            _steamIDCache[player.ActorNumber] = steamID;
        }
        _lastSteamIDCacheUpdate = Time.time;
    }

    /// <summary>
    /// Get cached Steam ID for a player
    /// </summary>
    private static string GetCachedSteamID(Photon.Realtime.Player player)
    {
        if (_steamIDCache.TryGetValue(player.ActorNumber, out string steamID))
        {
            return steamID;
        }
        // Fallback to direct lookup if not in cache
        return SteamIntegrationManager.Instance?.GetPlayerSteamID(player) ?? "Unknown";
    }

    /// <summary>
    /// Update ban indexes for fast lookups
    /// </summary>
    private static void UpdateBanIndexes()
    {
        lock (_banListLock)
        {
            _bannedNames.Clear();
            _bannedSteamIDs.Clear();

            foreach (BannedPlayer player in _bannedPlayers)
            {
                if (!string.IsNullOrEmpty(player.PlayerName))
                {
                    _bannedNames.Add(player.PlayerName.ToLowerInvariant());
                }
                if (!string.IsNullOrEmpty(player.SteamID) && player.SteamID != "Unknown")
                {
                    _bannedSteamIDs.Add(player.SteamID.ToLowerInvariant());
                }
            }

            // Log collection sizes for monitoring
            _logger.LogInfo($"Ban list size: {_bannedPlayers.Count}, Names: {_bannedNames.Count}, SteamIDs: {_bannedSteamIDs.Count}");
        }
    }

    /// <summary>
    /// Load ban list from file
    /// </summary>
    private static void LoadBanList()
    {
        try
        {
            if (File.Exists(_banListPath))
            {
                string text = File.ReadAllText(_banListPath);
                lock (_banListLock)
                {
                    _bannedPlayers = JsonConvert.DeserializeObject<List<BannedPlayer>>(text) ?? new List<BannedPlayer>();
                    foreach (BannedPlayer bannedPlayer in _bannedPlayers)
                    {
                        if (bannedPlayer.SteamID == null)
                        {
                            bannedPlayer.SteamID = "Unknown";
                        }
                    }
                }
                UpdateBanIndexes();
                _logger.LogInfo((object)$"Loaded {_bannedPlayers.Count} banned players");
            }
            else
            {
                lock (_banListLock)
                {
                    _bannedPlayers = new List<BannedPlayer>();
                }
                SaveBanListAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in LoadBanList: {ex.Message}");
            lock (_banListLock)
            {
                _bannedPlayers = new List<BannedPlayer>();
            }
        }
    }

    /// <summary>
    /// Load ban list asynchronously
    /// </summary>
    private static async void LoadBanListAsync()
    {
        try
        {
            if (File.Exists(_banListPath))
            {
                string text = await System.Threading.Tasks.Task.Run(() => File.ReadAllText(_banListPath));
                lock (_banListLock)
                {
                    _bannedPlayers = JsonConvert.DeserializeObject<List<BannedPlayer>>(text) ?? new List<BannedPlayer>();
                    foreach (BannedPlayer bannedPlayer in _bannedPlayers)
                    {
                        if (bannedPlayer.SteamID == null)
                        {
                            bannedPlayer.SteamID = "Unknown";
                        }
                    }
                }
                UpdateBanIndexes();
                _logger.LogInfo((object)$"Loaded {_bannedPlayers.Count} banned players asynchronously");
            }
            else
            {
                lock (_banListLock)
                {
                    _bannedPlayers = new List<BannedPlayer>();
                }
                SaveBanListAsync();
            }
        }
        catch (Exception ex)
        {
            ErrorHandler.HandleError(ex, "LoadBanListAsync", false, "Failed to load ban list");
            lock (_banListLock)
            {
                _bannedPlayers = new List<BannedPlayer>();
            }
        }
    }

    /// <summary>
    /// Save ban list to file
    /// </summary>
    private static void SaveBanList()
    {
        try
        {
            lock (_banListLock)
            {
                string contents = JsonConvert.SerializeObject((object)_bannedPlayers, (Formatting)1);
                File.WriteAllText(_banListPath, contents);
                _logger.LogInfo((object)$"Saved {_bannedPlayers.Count} banned players");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in SaveBanList: {ex.Message}");
        }
    }

    /// <summary>
    /// Save ban list asynchronously
    /// </summary>
    private static async void SaveBanListAsync()
    {
        try
        {
            string contents;
            lock (_banListLock)
            {
                contents = JsonConvert.SerializeObject(_bannedPlayers, Formatting.Indented);
            }

            // Run file I/O on background thread
            await System.Threading.Tasks.Task.Run(() => File.WriteAllText(_banListPath, contents));
            _logger.LogInfo((object)$"Saved {_bannedPlayers.Count} banned players asynchronously");
        }
        catch (Exception ex)
        {
            ErrorHandler.HandleError(ex, "SaveBanListAsync", false, "Failed to save ban list");
        }
    }

    /// <summary>
    /// Check if a player name is banned
    /// </summary>
    public static bool IsBanned(string playerName)
    {
        if (string.IsNullOrEmpty(playerName)) return false;
        return _bannedNames.Contains(playerName.ToLowerInvariant());
    }

    /// <summary>
    /// Check if a Steam ID is banned
    /// </summary>
    public static bool IsBannedBySteamID(string steamID)
    {
        if (string.IsNullOrEmpty(steamID) || steamID == "Unknown") return false;
        return _bannedSteamIDs.Contains(steamID.ToLowerInvariant());
    }

    /// <summary>
    /// Check if a player is banned by checking both their name and Steam ID
    /// </summary>
    /// <param name="player">The Photon player to check</param>
    /// <returns>True if the player is banned by name or Steam ID, false otherwise</returns>
    /// <remarks>
    /// This method performs a comprehensive ban check using both the player's
    /// display name and their Steam ID for maximum accuracy.
    /// </remarks>
    public static bool IsPlayerBanned(Photon.Realtime.Player player)
    {
        if (player == null) return false;

        string playerName = player.NickName;
        // Use cached Steam ID for better performance
        UpdateSteamIDCache();
        string playerSteamID = GetCachedSteamID(player);

        // Check our internal list
        if (IsBanned(playerName) || IsBannedBySteamID(playerSteamID))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Ban a player by Photon Player object with Steam ID and optional reason
    /// Adds the player to the ban list and saves it to disk asynchronously
    /// </summary>
    /// <param name="player">The Photon player to ban</param>
    /// <param name="steamID">The player's Steam ID (can be "Unknown" if not available)</param>
    /// <param name="reason">Optional reason for the ban</param>
    /// <remarks>
    /// This method cannot ban the local player or the room host.
    /// If the player was recently unbanned, they are removed from the protection list.
    /// </remarks>
    /// <example>
    /// <code>
    /// Photon.Realtime.Player badPlayer = PhotonNetwork.CurrentRoom.GetPlayer(actorNumber);
    /// string steamID = SteamIntegrationManager.Instance?.GetPlayerSteamID(badPlayer) ?? "Unknown";
    /// BanManager.BanPlayer(badPlayer, steamID, "Griefing");
    /// </code>
    /// </example>
    public static void BanPlayer(Photon.Realtime.Player player, string steamID, string reason = "")
    {
        if (player == null)
        {
            _logger.LogWarning("Cannot ban null player");
            return;
        }

        if (player.IsLocal)
        {
            _logger.LogWarning("Cannot ban local player");
            return;
        }

        if (player.IsMasterClient)
        {
            _logger.LogWarning("Cannot ban host/master client");
            return;
        }

        string nickName = player.NickName;
        if (string.IsNullOrWhiteSpace(nickName))
        {
            _logger.LogWarning("Player has no valid name");
            return;
        }

        if (nickName.Length > 100) // Reasonable limit
        {
            _logger.LogWarning("Player name too long (max 100 characters)");
            return;
        }
        if (IsBanned(nickName) || IsBannedBySteamID(steamID))
        {
            _logger.LogInfo((object)("Player " + nickName + " is already banned"));
            return;
        }

        lock (_recentlyUnbannedLock)
        {
            if (_recentlyUnbannedPlayers.ContainsKey(nickName))
            {
                _recentlyUnbannedPlayers.Remove(nickName);
                _logger.LogInfo((object)("Removed " + nickName + " from recently unbanned protection list because they're being banned again"));
            }
        }

        lock (_banListLock)
        {
            BannedPlayer item = new BannedPlayer
            {
                PlayerName = nickName,
                SteamID = steamID,
                BanDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Reason = reason
            };
            _bannedPlayers.Add(item);
            UpdateBanIndexes();
            SaveBanListAsync();
        }

        _logger.LogInfo((object)("Host banned player: " + nickName + " with Steam ID: " + steamID + (string.IsNullOrEmpty(reason) ? "" : (", Reason: " + reason))));

        // Note: Targeting and kicking logic will be handled by the calling code
    }

    /// <summary>
    /// Ban a player by name
    /// </summary>
    public static void BanPlayerByName(string playerName, string reason = "")
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            _logger.LogWarning("Player name cannot be null, empty, or whitespace");
            return;
        }

        if (playerName.Length > 100) // Reasonable limit
        {
            _logger.LogWarning("Player name too long (max 100 characters)");
            return;
        }

        if (IsBanned(playerName))
        {
            _logger.LogInfo((object)("Player " + playerName + " is already banned"));
            return;
        }

        lock (_recentlyUnbannedLock)
        {
            if (_recentlyUnbannedPlayers.ContainsKey(playerName))
            {
                _recentlyUnbannedPlayers.Remove(playerName);
                _logger.LogInfo((object)("Removed " + playerName + " from recently unbanned protection list because they're being banned again"));
            }
        }

        string steamID = "Unknown";
        if (PhotonNetwork.InRoom)
        {
            Photon.Realtime.Player[] playerList = PhotonNetwork.PlayerList;
            // Use cached Steam ID lookup for better performance
            UpdateSteamIDCache();
            foreach (Photon.Realtime.Player player in playerList)
            {
                if (player.NickName.Equals(playerName, StringComparison.OrdinalIgnoreCase))
                {
                    steamID = GetCachedSteamID(player);
                    break;
                }
            }
        }

        lock (_banListLock)
        {
            BannedPlayer item = new BannedPlayer
            {
                PlayerName = playerName,
                SteamID = steamID,
                BanDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Reason = reason
            };
            _bannedPlayers.Add(item);
            UpdateBanIndexes();
            SaveBanListAsync();
        }

        _logger.LogInfo((object)("Host banned player by name: " + playerName + " with Steam ID: " + steamID + (string.IsNullOrEmpty(reason) ? "" : (", Reason: " + reason))));

        // Note: Targeting and kicking logic will be handled by the calling code
    }

    /// <summary>
    /// Ban a player by Steam ID
    /// </summary>
    public static void BanPlayerBySteamID(string steamID, string reason = "")
    {
        if (string.IsNullOrWhiteSpace(steamID))
        {
            _logger.LogWarning("Steam ID cannot be null, empty, or whitespace");
            return;
        }

        if (steamID.Length > 20) // Steam IDs are typically 17 digits
        {
            _logger.LogWarning("Steam ID too long (max 20 characters)");
            return;
        }

        // Basic Steam ID format validation (should contain only digits)
        if (!System.Text.RegularExpressions.Regex.IsMatch(steamID, @"^\d+$"))
        {
            _logger.LogWarning("Steam ID must contain only digits");
            return;
        }

        if (IsBannedBySteamID(steamID))
        {
            _logger.LogInfo((object)("Steam ID " + steamID + " is already banned"));
            return;
        }

        string playerName = "Unknown";
        Photon.Realtime.Player player = null;
        if (PhotonNetwork.InRoom)
        {
            Photon.Realtime.Player[] playerList = PhotonNetwork.PlayerList;
            // Use cached Steam ID lookup for better performance
            UpdateSteamIDCache();
            foreach (Photon.Realtime.Player p in playerList)
            {
                string cachedSteamID = GetCachedSteamID(p);
                if (cachedSteamID.Equals(steamID, StringComparison.OrdinalIgnoreCase))
                {
                    playerName = p.NickName;
                    player = p;
                    break;
                }
            }
        }

        lock (_recentlyUnbannedLock)
        {
            if (_recentlyUnbannedPlayers.ContainsKey(playerName))
            {
                _recentlyUnbannedPlayers.Remove(playerName);
                _logger.LogInfo((object)("Removed " + playerName + " from recently unbanned protection list because they're being banned again"));
            }
        }

        lock (_banListLock)
        {
            BannedPlayer item = new BannedPlayer
            {
                PlayerName = playerName,
                SteamID = steamID,
                BanDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Reason = reason
            };
            _bannedPlayers.Add(item);
            UpdateBanIndexes();
            SaveBanListAsync();
        }

        _logger.LogInfo((object)("Host banned player by Steam ID: " + steamID + " with name: " + playerName + (string.IsNullOrEmpty(reason) ? "" : (", Reason: " + reason))));

        // Note: Targeting and kicking logic will be handled by the calling code
    }

    /// <summary>
    /// Unban a player by name or Steam ID
    /// </summary>
    /// <param name="playerName">The name of the player to unban</param>
    /// <param name="steamID">Optional Steam ID of the player to unban</param>
    /// <remarks>
    /// This method removes the player from the ban list and adds them to a
    /// temporary protection list to prevent immediate re-banning.
    /// </remarks>
    public static void UnbanPlayer(string playerName, string steamID = null)
    {
        BannedPlayer bannedPlayer = null;
        lock (_banListLock)
        {
            if (!string.IsNullOrEmpty(playerName) && playerName != "Unknown")
            {
                bannedPlayer = _bannedPlayers.FirstOrDefault((BannedPlayer p) => p.PlayerName.Equals(playerName, StringComparison.OrdinalIgnoreCase));
            }
            if (bannedPlayer == null && !string.IsNullOrEmpty(steamID) && steamID != "Unknown")
            {
                bannedPlayer = _bannedPlayers.FirstOrDefault((BannedPlayer p) => p.SteamID != null && p.SteamID.Equals(steamID, StringComparison.OrdinalIgnoreCase));
            }
            if (bannedPlayer == null)
            {
                return;
            }
            _bannedPlayers.Remove(bannedPlayer);
            UpdateBanIndexes();
            SaveBanListAsync();
        }

        _logger.LogInfo((object)("Host unbanned player: " + bannedPlayer.PlayerName + " with Steam ID: " + bannedPlayer.SteamID));

        lock (_recentlyUnbannedLock)
        {
            _recentlyUnbannedPlayers[bannedPlayer.PlayerName] = Time.time;
            _logger.LogInfo((object)("Added " + bannedPlayer.PlayerName + " to recently unbanned protection list"));
        }

        // Note: Cleanup logic (stopping coroutines, etc.) will be handled by the calling code
    }

    /// <summary>
    /// Get all banned players
    /// </summary>
    public static List<BannedPlayer> GetBannedPlayers()
    {
        lock (_banListLock)
        {
            return new List<BannedPlayer>(_bannedPlayers);
        }
    }

    /// <summary>
    /// Check for banned players in the current room
    /// </summary>
    public static void CheckForBannedPlayers()
    {
        if (!PhotonNetwork.InRoom)
        {
            return;
        }

        // Update Steam ID cache for better performance
        UpdateSteamIDCache();

        Photon.Realtime.Player[] playerList = PhotonNetwork.PlayerList;
        foreach (Photon.Realtime.Player player in playerList)
        {
            if (!player.IsLocal && !player.IsMasterClient)
            {
                // Use cached Steam ID for better performance
                string playerSteamID = GetCachedSteamID(player);
                if (playerSteamID != "Unknown" && IsBannedBySteamID(playerSteamID))
                {
                    _logger.LogInfo((object)("Player " + player.NickName + " has banned Steam ID: " + playerSteamID));
                    // Note: Targeting logic will be handled by the calling code
                }
                else if (IsBanned(player.NickName))
                {
                    // Note: Targeting logic will be handled by the calling code
                }
            }
        }
    }

    /// <summary>
    /// Clean up recently unbanned players
    /// </summary>
    public static void CleanupRecentlyUnbannedPlayers()
    {
        lock (_recentlyUnbannedLock)
        {
            List<string> toRemove = new List<string>();
            foreach (KeyValuePair<string, float> recentlyUnbannedPlayer in _recentlyUnbannedPlayers)
            {
                if (Time.time - recentlyUnbannedPlayer.Value > UNBAN_PROTECTION_TIME)
                {
                    toRemove.Add(recentlyUnbannedPlayer.Key);
                }
            }
            foreach (string item in toRemove)
            {
                _recentlyUnbannedPlayers.Remove(item);
                _logger.LogInfo((object)("Removed " + item + " from recently unbanned protection list"));
            }
        }
    }

    /// <summary>
    /// Check if a player is recently unbanned
    /// </summary>
    public static bool IsRecentlyUnbanned(string playerName)
    {
        lock (_recentlyUnbannedLock)
        {
            return _recentlyUnbannedPlayers.ContainsKey(playerName);
        }
    }

    /// <summary>
    /// Check for players using known hack properties
    /// </summary>
    public static void CheckForHackUsers()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
        {
            return;
        }

        try
        {
            Photon.Realtime.Player[] playerList = PhotonNetwork.PlayerList;
            foreach (Photon.Realtime.Player player in playerList)
            {
                if (player.IsLocal || player.IsMasterClient || IsBanned(player.NickName) ||
                    IsBannedBySteamID(SteamIntegrationManager.Instance?.GetPlayerSteamID(player) ?? "Unknown") ||
                    player.CustomProperties == null)
                {
                    continue;
                }

                foreach (string knownHackProperty in Plugin.KnownHackProperties)
                {
                    if (((Dictionary<object, object>)player.CustomProperties).ContainsKey(knownHackProperty))
                    {
                        string hackName = Plugin.GetHackName(knownHackProperty);
                        string nickName = player.NickName;
                        string playerSteamID = SteamIntegrationManager.Instance?.GetPlayerSteamID(player) ?? "Unknown";
                        _logger.LogWarning((object)("Detected " + hackName + " hack from player: " + nickName + " (SteamID: " + playerSteamID + ")"));
                        BanPlayer(player, playerSteamID, "Auto Ban: " + hackName + " Hack Usage");
                        PeakNetworkDisconnectorMod.Managers.UtilityManager.Instance.SendMessage("<color=red>Player " + nickName + " was banned for using " + hackName + " hack</color>");
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in CheckForHackUsers: {ex.Message}");
        }
    }

    /// <summary>
    /// Start targeting a player for enforcement actions
    /// </summary>
    /// <param name="player">The Photon player to target for enforcement</param>
    /// <remarks>
    /// This method adds the player to the enforcement target list.
    /// Only the host can target players, and local/host players cannot be targeted.
    /// </remarks>
    public static void StartTargetingPlayer(Photon.Realtime.Player player)
    {
        try
        {
            if (!PhotonNetwork.IsMasterClient)
            {
                _logger.LogWarning((object)"Only host can target players!");
                return;
            }
            if (player == null || player.IsLocal || player.IsMasterClient)
            {
                _logger.LogWarning((object)"Cannot target this player");
                return;
            }
            if (EnforcementManager.Instance.TargetPlayers.ContainsKey(player.ActorNumber))
            {
                _logger.LogInfo((object)("Player " + player.NickName + " is already targeted"));
                return;
            }
            EnforcementManager.Instance.TargetPlayers[player.ActorNumber] = player.NickName;
            _logger.LogInfo((object)("Started targeting player: " + player.NickName));
        }
        catch (Exception ex)
        {
            _logger.LogError((object)("Error in StartTargetingPlayer: " + ex.Message));
        }
    }
}