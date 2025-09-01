using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using PeakNetworkDisconnectorMod.Core;

namespace PeakNetworkDisconnectorMod.Managers
{
    /// <summary>
    /// Manages Photon network callbacks and player connection events
    /// Handles room events, player joins/leaves, and network state changes
    /// </summary>
    public class NetworkManager : MonoBehaviour, IInRoomCallbacks
    {
        private static NetworkManager _instance;
        public static NetworkManager Instance => _instance;

        private ManualLogSource _logger;
        private Dictionary<int, string> _playerSteamIDs;

        void Awake()
        {
            _instance = this;
            _playerSteamIDs = new Dictionary<int, string>();
        }

        /// <summary>
        /// Initialize the network manager with required dependencies
        /// </summary>
        public void Initialize(ManualLogSource logger, Dictionary<int, string> playerSteamIDs)
        {
            _logger = logger;
            _playerSteamIDs = playerSteamIDs;
        }

        /// <summary>
        /// Called when a player enters the room
        /// </summary>
        public void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
        {
            // Player join handling - currently not implemented
            // Could be used for logging, Steam ID caching, etc.
        }

        /// <summary>
        /// Called when a player leaves the room
        /// </summary>
        public void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient)
                {
                    return;
                }

                // Use EnforcementManager to cleanup disconnected player
                if (EnforcementManager.Instance != null)
                {
                    EnforcementManager.Instance.CleanupDisconnectedPlayer(otherPlayer);
                }

                // Remove from recently unbanned players if present
                // Note: This cleanup is now handled by BanManager

                // Remove from player Steam IDs cache
                if (_playerSteamIDs.ContainsKey(otherPlayer.ActorNumber))
                {
                    _playerSteamIDs.Remove(otherPlayer.ActorNumber);
                    _logger?.LogInfo((object)("Removed disconnected player " + otherPlayer.NickName + " from Steam ID cache"));
                }

                _logger?.LogInfo((object)("Cleaned up resources for disconnected player: " + otherPlayer.NickName));
            }
            catch (Exception ex)
            {
                _logger?.LogError((object)("Error in OnPlayerLeftRoom: " + ex.Message));
            }
        }

        /// <summary>
        /// Called when room properties are updated
        /// </summary>
        public void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
        {
            // Not implemented
        }

        /// <summary>
        /// Called when a player's properties are updated
        /// </summary>
        public void OnPlayerPropertiesUpdate(Photon.Realtime.Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
        {
            // Not implemented
        }

        /// <summary>
        /// Called when the master client switches
        /// </summary>
        public void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
        {
            // Not implemented
        }

        /// <summary>
        /// Called when the room list is updated
        /// </summary>
        public void OnRoomListUpdate(List<RoomInfo> roomList)
        {
            // Not implemented
        }

        /// <summary>
        /// Called when the local player joins a room
        /// </summary>
        public void OnJoinedRoom()
        {
            // Not implemented
        }

        /// <summary>
        /// Called when the local player leaves a room
        /// </summary>
        public void OnLeftRoom()
        {
            // Not implemented
        }

        /// <summary>
        /// Called when the client gets disconnected
        /// </summary>
        public void OnDisconnected(DisconnectCause cause)
        {
            // Not implemented
        }

        /// <summary>
        /// Called when the client connects to the Photon master server
        /// </summary>
        public void OnConnected()
        {
            // Not implemented
        }

        /// <summary>
        /// Called when custom authentication response is received
        /// </summary>
        public void OnCustomAuthenticationResponse(Dictionary<string, object> data)
        {
            // Not implemented
        }

        /// <summary>
        /// Called when custom authentication fails
        /// </summary>
        public void OnCustomAuthenticationFailed(string debugMessage)
        {
            // Not implemented
        }
    }
}