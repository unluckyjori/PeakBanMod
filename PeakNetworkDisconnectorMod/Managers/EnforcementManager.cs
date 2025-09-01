using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Logging;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using PeakNetworkDisconnectorMod.Core;

#nullable enable

namespace PeakNetworkDisconnectorMod.Managers
{
    /// <summary>
    /// Manages player enforcement actions including kick operations, flood packets, and player warping
    /// Handles both aggressive and passive enforcement modes
    /// </summary>
    public class EnforcementManager : MonoBehaviour
    {
        private static EnforcementManager _instance;
        public static EnforcementManager Instance => _instance;

        private ManualLogSource _logger;
        private Dictionary<int, string> _targetPlayers;
        private Dictionary<int, Coroutine> _banActionCoroutines;
        private Vector3 _extremePosition = new Vector3(10000f, 10000f, 10000f);
        private byte[] _dummyData;
        private int _packetsCount;

        // PhotonView caching for performance optimization
        private PhotonView _cachedPhotonView;
        private float _lastPhotonViewCacheTime;
        private const float PHOTON_VIEW_CACHE_DURATION = 10.0f; // Cache PhotonView for 10 seconds

        // Dependencies that need to be injected or accessed
        private Plugin _plugin;

        void Awake()
        {
            _instance = this;
            _targetPlayers = new Dictionary<int, string>();
            _banActionCoroutines = new Dictionary<int, Coroutine>();
            _dummyData = new byte[1024];
            for (int i = 0; i < 1024; i++)
            {
                _dummyData[i] = (byte)UnityEngine.Random.Range(0, 256);
            }
            _cachedPhotonView = null;
            _lastPhotonViewCacheTime = 0f;
        }

        /// <summary>
        /// Get cached PhotonView for RPC calls (performance optimization)
        /// </summary>
        private PhotonView GetCachedPhotonView()
        {
            float currentTime = Time.time;
            if (_cachedPhotonView == null || currentTime - _lastPhotonViewCacheTime >= PHOTON_VIEW_CACHE_DURATION)
            {
                PhotonView[] photonViews = UnityEngine.Object.FindObjectsByType<PhotonView>(UnityEngine.FindObjectsSortMode.None);
                foreach (PhotonView pv in photonViews)
                {
                    if (pv.IsMine)
                    {
                        _cachedPhotonView = pv;
                        _lastPhotonViewCacheTime = currentTime;
                        break;
                    }
                }
            }
            return _cachedPhotonView;
        }

        /// <summary>
        /// Initialize the enforcement manager with required dependencies
        /// </summary>
        public void Initialize(Plugin plugin, ManualLogSource logger)
        {
            _plugin = plugin;
            _logger = logger;
            PeakNetworkDisconnectorMod.Logger.Info("EnforcementManager initialized successfully", "EnforcementManager");
        }

        /// <summary>
        /// Apply aggressive kick actions to targeted players using flood packets and position warping
        /// This method sends a high volume of network packets to disrupt the player's connection
        /// while simultaneously warping their position to prevent gameplay
        /// </summary>
        /// <param name="targetPlayers">Dictionary of player ActorNumbers and their names to target</param>
        /// <example>
        /// <code>
        /// var targets = new Dictionary&lt;int, string&gt; { { 1, "Player1" }, { 2, "Player2" } };
        /// enforcementManager.ApplyAggressiveKickActions(targets);
        /// </code>
        /// </example>
        public void ApplyAggressiveKickActions(Dictionary<int, string> targetPlayers)
        {
            try
            {
                if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
                {
                    return;
                }


                List<int> list = new List<int>();
                foreach (KeyValuePair<int, string> targetPlayer in targetPlayers)
                {
                    int key = targetPlayer.Key;
                    Photon.Realtime.Player value = null;
                    if (!PhotonNetwork.CurrentRoom.Players.TryGetValue(key, out value))
                    {
                        list.Add(key);
                    }
                    else
                    {
                        SendFloodPackets(value);
                    }
                }
                foreach (int item in list)
                {
                    targetPlayers.Remove(item);
                    _logger.LogInfo((object)$"Player with ActorNumber {item} left the room, stopped targeting");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError((object)$"Error in ApplyAggressiveKickActions: {ex.Message}");
            }
        }

        /// <summary>
        /// Apply passive kick actions using RPC filtering to block network communication
        /// This method provides visual feedback by warping the player while RPC filtering
        /// patches handle blocking all network events from the banned player
        /// </summary>
        /// <param name="player">The Photon player to apply passive kick actions to</param>
        /// <example>
        /// <code>
        /// Photon.Realtime.Player bannedPlayer = PhotonNetwork.CurrentRoom.GetPlayer(actorNumber);
        /// enforcementManager.ApplyPassiveKickActions(bannedPlayer);
        /// </code>
        /// </example>
        public void ApplyPassiveKickActions(Photon.Realtime.Player player)
        {
            try
            {
                if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
                {
                    return;
                }

                // For PassiveKick with RPC filtering, we provide visual feedback by warping the player
                // The RPC filtering patch will handle blocking all network events from banned players
                // No continuous position manipulation needed since RPC filtering prevents all actions

                ApplyKickActionsToPlayer(player);

                _logger.LogInfo((object)$"PassiveKick (RPC filtering) applied to player: {player.NickName}");
            }
            catch (Exception ex)
            {
                _logger.LogError((object)$"Error in ApplyPassiveKickActions: {ex.Message}");
            }
        }

        /// <summary>
        /// Send flood packets to a player
        /// </summary>
        private void SendFloodPackets(Photon.Realtime.Player player)
        {
            try
            {
                // Verify player is still in the room before sending packets
                if (player == null || !PhotonNetwork.CurrentRoom.Players.ContainsKey(player.ActorNumber))
                {
                    _logger.LogInfo((object)("Player " + (player?.NickName ?? "Unknown") + " is no longer in the room, skipping packet flood"));
                    return;
                }

                // Use cached PhotonView for better performance
                PhotonView photonView = GetCachedPhotonView();
                if (photonView != null)
                {
                    PeakNetworkDisconnectorMod.Logger.Info($"Sending 200 flood packets to player: {player.NickName}", "EnforcementManager");
                    for (int j = 0; j < 200; j++)
                    {
                        photonView.RPC("FloodPacketRPC", player, new object[1] { _dummyData });
                        _packetsCount++;
                    }
                    PeakNetworkDisconnectorMod.Logger.Info($"Flood packet sequence completed for player: {player.NickName}", "EnforcementManager");
                }
                else
                {
                    PeakNetworkDisconnectorMod.Logger.Warning("Could not find a suitable PhotonView to send RPCs", "EnforcementManager");
                    _logger.LogWarning((object)"Could not find a suitable PhotonView to send RPCs");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError((object)("Error sending flood packets: " + ex.Message));
            }
        }

        /// <summary>
        /// Apply kick actions to a specific player
        /// </summary>
        public void ApplyKickActionsToPlayer(Photon.Realtime.Player player)
        {
            if (BanManager.IsRecentlyUnbanned(player.NickName))
            {
                return;
            }
            try
            {
                Character val = null;
                foreach (Character allCharacter in Character.AllCharacters)
                {
                    if (((object)((MonoBehaviourPun)allCharacter).photonView.Owner).Equals((object?)player))
                    {
                        val = allCharacter;
                        break;
                    }
                }
                if ((UnityEngine.Object)val != (UnityEngine.Object)null)
                {
                    ((MonoBehaviourPun)val).photonView.RPC("WarpPlayerRPC", player, new object[2] { _extremePosition, true });
                    ((MonoBehaviourPun)val).photonView.RPC("RPCA_PassOut", player, Array.Empty<object>());
                    StartCoroutine(ReviveAfterDelay(val, player, 1f));
                    if ((UnityEngine.Object)val.Ghost != (UnityEngine.Object)null)
                    {
                        PhotonNetwork.Destroy(((Component)val.Ghost).gameObject);
                        _logger.LogInfo((object)("Destroyed ghost of banned player: " + player.NickName));
                    }
                    DestroyAllGhostsOwnedByPlayer(player);
                }
                else
                {
                    DestroyAllGhostsOwnedByPlayer(player);
                }
                // Only send flood packets in Aggressive mode, not Passive mode
                if (ConfigManager.EnforcementMode == "Aggressive")
                {
                    SendFloodPackets(player);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError((object)("Error applying kick actions: " + ex.Message));
            }
        }

        /// <summary>
        /// Apply kick actions to all banned players currently in the room
        /// This method iterates through all players and applies the appropriate enforcement
        /// mode (Aggressive or Passive) based on the current configuration
        /// </summary>
        /// <remarks>
        /// Only the room host can execute this method. Players are checked against
        /// the ban list and enforcement actions are applied based on the configured mode.
        /// </remarks>
        public void ApplyKickActions()
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
            {
                return;
            }
            Photon.Realtime.Player[] playerList = PhotonNetwork.PlayerList;
            foreach (Photon.Realtime.Player player in playerList)
            {
                if (!BanManager.IsRecentlyUnbanned(player.NickName) && !player.IsLocal && !player.IsMasterClient && BanManager.IsPlayerBanned(player))
                {
                    ApplyKickActionsToPlayer(player);
                }
            }
        }

        /// <summary>
        /// Ensure continuous ban actions coroutine is running for a banned player
        /// In Aggressive mode, starts a coroutine that repeatedly applies enforcement actions.
        /// In Passive mode, applies one-time RPC filtering setup.
        /// </summary>
        /// <param name="player">The Photon player to ensure has ban actions running</param>
        /// <remarks>
        /// This method prevents duplicate coroutines for the same player and handles
        /// different enforcement modes appropriately.
        /// </remarks>
        public void EnsureBanActionsCoroutine(Photon.Realtime.Player player)
        {
            // Only start continuous ban actions for Aggressive mode
            // Passive mode now uses RPC filtering which doesn't require continuous position manipulation
            if (ConfigManager.EnforcementMode == "Aggressive" &&
                !BanManager.IsRecentlyUnbanned(player.NickName) &&
                !_banActionCoroutines.ContainsKey(player.ActorNumber))
            {
                Coroutine value = StartCoroutine(ContinuousBanActions(player));
                _banActionCoroutines[player.ActorNumber] = value;
                _logger.LogInfo((object)("Started continuous ban actions for player: " + player.NickName));
            }
            else if (ConfigManager.EnforcementMode == "Passive")
            {
                // For Passive mode with RPC filtering, just apply the initial kick actions once
                // RPC filtering will handle blocking all subsequent network events
                ApplyPassiveKickActions(player);
                _logger.LogInfo((object)("Applied passive ban actions (RPC filtering) for player: " + player.NickName));
            }
        }

        /// <summary>
        /// Continuous ban actions coroutine
        /// </summary>
        private IEnumerator ContinuousBanActions(Photon.Realtime.Player player)
        {
            string steamID = SteamIntegrationManager.Instance?.GetPlayerSteamID(player) ?? "Unknown";
            while (PhotonNetwork.InRoom && (BanManager.IsBanned(player.NickName) || BanManager.IsBannedBySteamID(steamID)))
            {
                yield return new WaitForSeconds(0.2f);

                // Check if player is still in room before proceeding
                if (player == null || !PhotonNetwork.CurrentRoom.Players.ContainsKey(player.ActorNumber))
                {
                    _logger.LogInfo((object)("Player " + player.NickName + " has left the room, stopping ban actions"));
                    _banActionCoroutines.Remove(player.ActorNumber);
                    yield break;
                }

                if (BanManager.IsRecentlyUnbanned(player.NickName))
                {
                    continue;
                }

                try
                {
                    ApplyKickActionsToPlayer(player);
                }
                catch (Exception ex)
                {
                    _logger.LogError((object)("Error in continuous ban actions: " + ex.Message));
                }
            }
            _banActionCoroutines.Remove(player.ActorNumber);
        }

        /// <summary>
        /// Revive player after delay
        /// </summary>
        private IEnumerator ReviveAfterDelay(Character targetCharacter, Photon.Realtime.Player player, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (BanManager.IsRecentlyUnbanned(player.NickName))
            {
                yield break;
            }
            try
            {
                if ((UnityEngine.Object)targetCharacter != (UnityEngine.Object)null && (UnityEngine.Object)((MonoBehaviourPun)targetCharacter).photonView != (UnityEngine.Object)null)
                {
                    ((MonoBehaviourPun)targetCharacter).photonView.RPC("RPCA_ReviveAtPosition", RpcTarget.All, new object[2] { _extremePosition, false });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError((object)("Error reviving player: " + ex.Message));
            }
        }

        /// <summary>
        /// Destroy all ghosts owned by a player
        /// </summary>
        private void DestroyAllGhostsOwnedByPlayer(Photon.Realtime.Player player)
        {
            if (BanManager.IsRecentlyUnbanned(player.NickName))
            {
                return;
            }
            try
            {
                PlayerGhost[] array = UnityEngine.Object.FindObjectsByType<PlayerGhost>(UnityEngine.FindObjectsSortMode.None);
                PlayerGhost[] array2 = array;
                foreach (PlayerGhost val in array2)
                {
                    if ((UnityEngine.Object)val.m_view != (UnityEngine.Object)null && ((object)val.m_view.Owner).Equals((object?)player))
                    {
                        PhotonNetwork.Destroy(((Component)val).gameObject);
                        _logger.LogInfo((object)("Destroyed ghost object owned by banned player: " + player.NickName));
                    }
                    if ((UnityEngine.Object)val.m_owner != (UnityEngine.Object)null && ((object)((MonoBehaviourPun)val.m_owner).photonView.Owner).Equals((object?)player))
                    {
                        PhotonNetwork.Destroy(((Component)val).gameObject);
                        _logger.LogInfo((object)("Destroyed ghost with owner character belonging to banned player: " + player.NickName));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError((object)("Error destroying ghosts: " + ex.Message));
            }
        }





        /// <summary>
        /// Start targeting a player
        /// </summary>
        public void StartTargetingPlayer(Photon.Realtime.Player player)
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
                if (_targetPlayers.ContainsKey(player.ActorNumber))
                {
                    _logger.LogInfo((object)("Player " + player.NickName + " is already targeted"));
                    return;
                }
                _targetPlayers[player.ActorNumber] = player.NickName;
                _logger.LogInfo((object)("Started targeting player: " + player.NickName));
            }
            catch (Exception ex)
            {
                _logger.LogError((object)("Error in StartTargetingPlayer: " + ex.Message));
            }
        }

        /// <summary>
        /// Stop targeting a player
        /// </summary>
        public void StopTargetingPlayer(Photon.Realtime.Player player)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient)
                {
                    _logger.LogWarning((object)"Only host can stop targeting players!");
                }
                else if (player == null)
                {
                    _logger.LogWarning((object)"Invalid player");
                }
                else if (_targetPlayers.ContainsKey(player.ActorNumber))
                {
                    _targetPlayers.Remove(player.ActorNumber);
                    _logger.LogInfo((object)("Stopped targeting player: " + player.NickName));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError((object)("Error in StopTargetingPlayer: " + ex.Message));
            }
        }

        /// <summary>
        /// Stop ban actions for a player
        /// </summary>
        public void StopBanActionsForPlayer(Photon.Realtime.Player player)
        {
            if (_banActionCoroutines.ContainsKey(player.ActorNumber))
            {
                StopCoroutine(_banActionCoroutines[player.ActorNumber]);
                _banActionCoroutines.Remove(player.ActorNumber);
                _logger.LogInfo((object)("Stopped ban actions for unbanned player: " + player.NickName));
            }
            if (_targetPlayers.ContainsKey(player.ActorNumber))
            {
                StopTargetingPlayer(player);
            }
        }

        /// <summary>
        /// Handle Photon callback when player leaves room
        /// </summary>
        public void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient)
                {
                    return;
                }

                // Stop targeting the player
                if (_targetPlayers.ContainsKey(otherPlayer.ActorNumber))
                {
                    _targetPlayers.Remove(otherPlayer.ActorNumber);
                    _logger.LogInfo((object)("Stopped targeting disconnected player: " + otherPlayer.NickName));
                }

                // Cleanup enforcement resources for disconnected player
                CleanupDisconnectedPlayer(otherPlayer);

                _logger.LogInfo((object)("Cleaned up enforcement resources for disconnected player: " + otherPlayer.NickName));
            }
            catch (Exception ex)
            {
                _logger.LogError((object)("Error in OnPlayerLeftRoom: " + ex.Message));
            }
        }

        /// <summary>
        /// Clean up resources for disconnected player
        /// </summary>
        public void CleanupDisconnectedPlayer(Photon.Realtime.Player player)
        {
            try
            {
                // Stop targeting the player
                if (_targetPlayers.ContainsKey(player.ActorNumber))
                {
                    _targetPlayers.Remove(player.ActorNumber);
                    _logger.LogInfo((object)("Stopped targeting disconnected player: " + player.NickName));
                }

                // Stop continuous ban actions for disconnected player
                if (_banActionCoroutines.ContainsKey(player.ActorNumber))
                {
                    StopCoroutine(_banActionCoroutines[player.ActorNumber]);
                    _banActionCoroutines.Remove(player.ActorNumber);
                    _logger.LogInfo((object)("Stopped continuous ban actions for disconnected player: " + player.NickName));
                }

                _logger.LogInfo((object)("Cleaned up resources for disconnected player: " + player.NickName));
            }
            catch (Exception ex)
            {
                _logger.LogError((object)("Error in CleanupDisconnectedPlayer: " + ex.Message));
            }
        }

        // Public accessors for compatibility
        public Dictionary<int, string> TargetPlayers => _targetPlayers;
        public int PacketsCount => _packetsCount;
    }
}