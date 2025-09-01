using System;
using System.Collections.Generic;
using BepInEx.Logging;
using Photon.Pun;
using Photon.Realtime;
using Steamworks;
using UnityEngine;

namespace PeakNetworkDisconnectorMod.Core
{
    public class SteamIntegrationManager : MonoBehaviour
    {
        private static SteamIntegrationManager _instance;
        public static SteamIntegrationManager Instance => _instance;

        // Steam initialization state
        private bool steamInitialized;
        private float steamInitCheckTimer;
        private const float STEAM_INIT_CHECK_INTERVAL = 2f;
        private int steamInitAttempts;
        private const int MAX_STEAM_INIT_ATTEMPTS = 10;

        // Player Steam ID cache
        private Dictionary<int, string> playerSteamIDs = new Dictionary<int, string>();

        // Reference to logger
        private ManualLogSource Log => Plugin.Log;

        void Awake()
        {
            _instance = this;
        }

        void Update()
        {
            // Handle periodic Steam initialization checks
            if (!steamInitialized)
            {
                steamInitCheckTimer += Time.deltaTime;
                if (steamInitCheckTimer >= STEAM_INIT_CHECK_INTERVAL)
                {
                    steamInitCheckTimer = 0f;
                    CheckSteamInitialization();
                }
            }
        }

        public void CheckSteamInitialization()
        {
            try
            {
                if (SteamManager.Initialized)
                {
                    Log.LogInfo((object)"Steam API initialized!");
                    steamInitialized = true;
                    try
                    {
                        string localSteamID = GetLocalSteamID();
                        Log.LogInfo((object)("Local Steam ID: " + localSteamID));
                        return;
                    }
                    catch (Exception ex)
                    {
                        Log.LogWarning((object)("Could not get local Steam ID: " + ex.Message));
                        return;
                    }
                }
                steamInitAttempts++;
                if (steamInitAttempts <= MAX_STEAM_INIT_ATTEMPTS)
                {
                    Log.LogWarning((object)$"Steam API not initialized yet. Attempt: {steamInitAttempts}/{MAX_STEAM_INIT_ATTEMPTS}");
                }
                else
                {
                    Log.LogWarning((object)"Steam API could not be initialized. Some features may not work properly.");
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.HandleWarning("Error checking Steam initialization: " + ex.Message, "CheckSteamInitialization");
            }
        }

        public string GetLocalSteamID()
        {
            try
            {
                try
                {
                    if (!SteamAPI.IsSteamRunning())
                    {
                        Log.LogWarning((object)"Steam is not running!");
                        return "Steam Not Running";
                    }
                }
                catch (Exception ex)
                {
                    Log.LogWarning((object)("Error checking if Steam is running: " + ex.Message));
                    return "Steam Check Error";
                }
                if (SteamManager.Initialized)
                {
                    try
                    {
                        if (SteamUser.GetSteamID().m_SteamID != 0)
                        {
                            CSteamID steamID = SteamUser.GetSteamID();
                            Log.LogInfo((object)("Local Steam ID: " + ((object)steamID).ToString()));
                            return ((object)steamID).ToString();
                        }
                        Log.LogWarning((object)"Invalid Steam ID (0)");
                    }
                    catch (Exception ex2)
                    {
                        Log.LogWarning((object)("Error getting Steam ID: " + ex2.Message));
                        return "Steam ID Error";
                    }
                }
                else
                {
                    Log.LogWarning((object)"Steam API not initialized");
                }
                try
                {
                    SteamLobbyHandler service = GameHandler.GetService<SteamLobbyHandler>();
                    if (service != null)
                    {
                        Log.LogInfo((object)"Found SteamLobbyHandler, trying to get Steam ID...");
                        CSteamID val = default(CSteamID);
                        if (service.InSteamLobby(out val))
                        {
                            Log.LogInfo((object)("Steam Lobby ID: " + ((object)val).ToString()));
                        }
                    }
                }
                catch (Exception ex3)
                {
                    Log.LogWarning((object)("Error using SteamLobbyHandler: " + ex3.Message));
                }
            }
            catch (Exception ex4)
            {
                Log.LogWarning((object)("Error getting Steam ID: " + ex4.Message));
            }
            return "Unknown";
        }

        public string GetPlayerSteamID(Photon.Realtime.Player photonPlayer)
        {
            try
            {
                if (playerSteamIDs.ContainsKey(photonPlayer.ActorNumber) && !string.IsNullOrEmpty(playerSteamIDs[photonPlayer.ActorNumber]) && playerSteamIDs[photonPlayer.ActorNumber] != "Unknown")
                {
                    return playerSteamIDs[photonPlayer.ActorNumber];
                }
                if (photonPlayer.IsLocal)
                {
                    try
                    {
                        if (SteamManager.Initialized)
                        {
                            CSteamID steamID = SteamUser.GetSteamID();
                            if (steamID.m_SteamID != 0)
                            {
                                Log.LogInfo((object)("Found local player ID from Steam API: " + ((object)steamID).ToString()));
                                playerSteamIDs[photonPlayer.ActorNumber] = ((object)steamID).ToString();
                                return ((object)steamID).ToString();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.LogError((object)("Error getting ID from Steam API: " + ex.Message));
                    }
                }
                try
                {
                    if (SteamManager.Initialized && !photonPlayer.IsLocal)
                    {
                        int friendCount = SteamFriends.GetFriendCount((EFriendFlags)4);
                        Log.LogInfo((object)$"Steam friend count: {friendCount}");
                        for (int i = 0; i < friendCount; i++)
                        {
                            CSteamID friendByIndex = SteamFriends.GetFriendByIndex(i, (EFriendFlags)4);
                            string friendPersonaName = SteamFriends.GetFriendPersonaName(friendByIndex);
                            if (friendPersonaName == photonPlayer.NickName)
                            {
                                Log.LogInfo((object)("Found Steam friend: " + friendPersonaName + " (ID: " + ((object)friendByIndex).ToString() + ")"));
                                playerSteamIDs[photonPlayer.ActorNumber] = ((object)friendByIndex).ToString();
                                return ((object)friendByIndex).ToString();
                            }
                        }
                    }
                }
                catch (Exception ex2)
                {
                    Log.LogError((object)("Error using SteamFriends API: " + ex2.Message));
                }
                try
                {
                    CSteamID val = default(CSteamID);
                    if (SteamManager.Initialized && !photonPlayer.IsLocal && GameHandler.GetService<SteamLobbyHandler>().InSteamLobby(out val))
                    {
                        Log.LogInfo((object)("Steam Lobby ID: " + ((object)val).ToString()));
                        int numLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(val);
                        Log.LogInfo((object)$"Lobby member count: {numLobbyMembers}");
                        for (int j = 0; j < numLobbyMembers; j++)
                        {
                            CSteamID lobbyMemberByIndex = SteamMatchmaking.GetLobbyMemberByIndex(val, j);
                            string friendPersonaName2 = SteamFriends.GetFriendPersonaName(lobbyMemberByIndex);
                            if (friendPersonaName2 == photonPlayer.NickName)
                            {
                                Log.LogInfo((object)("Found Steam lobby member: " + friendPersonaName2 + " (ID: " + ((object)lobbyMemberByIndex).ToString() + ")"));
                                playerSteamIDs[photonPlayer.ActorNumber] = ((object)lobbyMemberByIndex).ToString();
                                return ((object)lobbyMemberByIndex).ToString();
                            }
                        }
                    }
                }
                catch (Exception ex3)
                {
                    Log.LogError((object)("Error using SteamMatchmaking API: " + ex3.Message));
                }
                try
                {
                    Player player = PlayerHandler.GetPlayer(photonPlayer);
                    if ((UnityEngine.Object)player != (UnityEngine.Object)null)
                    {
                        Log.LogInfo((object)("Found Player: " + ((UnityEngine.Object)player).name));
                        Character playerCharacter = PlayerHandler.GetPlayerCharacter(photonPlayer);
                        if ((UnityEngine.Object)playerCharacter != (UnityEngine.Object)null)
                        {
                            Log.LogInfo((object)("Found Character: " + ((UnityEngine.Object)playerCharacter).name));
                        }
                    }
                }
                catch (Exception ex4)
                {
                    Log.LogError((object)("Error using PlayerHandler: " + ex4.Message));
                }
                if (!string.IsNullOrEmpty(photonPlayer.UserId))
                {
                    Log.LogInfo((object)("Found UserId: " + photonPlayer.UserId));
                    playerSteamIDs[photonPlayer.ActorNumber] = photonPlayer.UserId;
                    return photonPlayer.UserId;
                }
                if (photonPlayer.CustomProperties != null && ((Dictionary<object, object>)(object)photonPlayer.CustomProperties).ContainsKey("SteamID"))
                {
                    Log.LogInfo((object)"Found SteamID from CustomProperties");
                    string text = photonPlayer.CustomProperties[(object)"SteamID"].ToString();
                    playerSteamIDs[photonPlayer.ActorNumber] = text;
                    return text;
                }
                if (!string.IsNullOrEmpty(photonPlayer.NickName))
                {
                    Log.LogInfo((object)("Found NickName: " + photonPlayer.NickName));
                }
            }
            catch (Exception ex5)
            {
                Log.LogError((object)("Error getting player Steam ID: " + ex5.Message));
            }
            playerSteamIDs[photonPlayer.ActorNumber] = "Unknown";
            return "Unknown";
        }

        public void UpdatePlayerSteamIDs()
        {
            try
            {
                Photon.Realtime.Player[] playerListOthers = PhotonNetwork.PlayerListOthers;
                Photon.Realtime.Player[] array = playerListOthers;
                foreach (Photon.Realtime.Player val in array)
                {
                    if (playerSteamIDs.ContainsKey(val.ActorNumber) && !(playerSteamIDs[val.ActorNumber] == "Unknown"))
                    {
                        continue;
                    }
                    string playerSteamID = GetPlayerSteamID(val);
                    if (playerSteamID != "Unknown")
                    {
                        playerSteamIDs[val.ActorNumber] = playerSteamID;
                        Log.LogInfo((object)("Updated Steam ID: " + val.NickName + " -> " + playerSteamID));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogError((object)("Error updating Steam IDs: " + ex.Message));
            }
        }

        /// <summary>
        /// Handle player leaving room - clean up Steam ID cache
        /// </summary>
        public void HandlePlayerLeftRoom(Photon.Realtime.Player otherPlayer)
        {
            try
            {
                // Remove player from Steam ID cache when they leave
                if (playerSteamIDs.ContainsKey(otherPlayer.ActorNumber))
                {
                    playerSteamIDs.Remove(otherPlayer.ActorNumber);
                    Log.LogInfo((object)("Cleaned up Steam ID cache for disconnected player: " + otherPlayer.NickName));
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.HandleWarning("Error cleaning up Steam ID cache for disconnected player: " + ex.Message, "HandlePlayerLeftRoom");
            }
        }

        // Public accessor for player Steam IDs (used by UI)
        public Dictionary<int, string> PlayerSteamIDs => playerSteamIDs;
    }
}