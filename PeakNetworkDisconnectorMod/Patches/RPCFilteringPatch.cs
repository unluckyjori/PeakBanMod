using System;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using PeakNetworkDisconnectorMod;
using PeakNetworkDisconnectorMod.Managers;
using PeakNetworkDisconnectorMod.Core;

namespace PeakNetworkDisconnectorMod
{
    [HarmonyPatch]
    public static class RPCFilteringPatch
    {
        [HarmonyPatch(typeof(PhotonNetwork), "OnEvent")]
        [HarmonyPrefix]
        public static bool PreOnEvent(EventData photonEvent)
        {
            try
            {
                int sender = photonEvent.Sender;

                // Block events from banned players, but allow specific "leaving" events
                if (sender > 0 && IsPlayerBanned(sender))
                {
                    // Allow specific events that indicate the player is leaving or cleaning up
                    // These are typically Photon's internal events for player leaving
                    if (photonEvent.Code == 1 || // Player left room event
                        photonEvent.Code == 2 || // Player properties changed (for leaving)
                        photonEvent.Code == 3)   // Room properties changed (for leaving)
                    {
                        return true; // Allow these events to pass through
                    }

                    // Block all other events silently without any logging
                    return false;
                }

                return true; // Allow all legitimate events
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("Error in RPC filtering: " + ex.Message);
                return true; // Allow event on error to prevent breaking functionality
            }
        }

        private static bool IsPlayerBanned(int actorNumber)
        {
            try
            {
                // Get the player from the actor number
                Photon.Realtime.Player player = PhotonNetwork.CurrentRoom?.GetPlayer(actorNumber);
                if (player == null)
                {
                    return false;
                }

                // Check if player is banned by name
                if (BanManager.IsBanned(player.NickName))
                {
                    return true;
                }

                // Check if player is banned by SteamID
                string steamID = SteamIntegrationManager.Instance?.GetPlayerSteamID(player) ?? "Unknown";
                if (!string.IsNullOrEmpty(steamID) && steamID != "Unknown" && BanManager.IsBannedBySteamID(steamID))
                {
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("Error checking if player is banned: " + ex.Message);
                return false; // Don't block on error
            }
        }
    }
}