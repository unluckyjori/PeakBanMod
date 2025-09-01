using System;
using System.Text;
using HarmonyLib;
using Steamworks;
using Unity.Collections;
using UnityEngine;
using Zorro.Core.Serizalization;
using PeakNetworkDisconnectorMod;

namespace PeakNetworkDisconnectorMod.Patches
{
    [HarmonyPatch]
    public static class SteamLobbyPatch
    {
        // Extracted from Plugin.cs:1602-1653
        [HarmonyPatch(typeof(SteamLobbyHandler), "SendRoomID")]
        [HarmonyPrefix]
        internal static bool PreSteamLobbyHandlerSendRoomID(SteamLobbyHandler __instance)
        {
            // Only run on host
            if (!Photon.Pun.PhotonNetwork.IsMasterClient)
            {
                return true;
            }

            // Check for banned players in the lobby
            CSteamID currentLobby = default(CSteamID);
            if (!__instance.InSteamLobby(out currentLobby))
            {
                return true;
            }

            int numLobbyMembers = SteamMatchmaking.GetNumLobbyMembers(currentLobby);

            for (int i = 0; i < numLobbyMembers; i++)
            {
                CSteamID lobbyMemberByIndex = SteamMatchmaking.GetLobbyMemberByIndex(currentLobby, i);
                string steamID = lobbyMemberByIndex.m_SteamID.ToString();

                // Check if this player is banned in our system
                if (BanManager.IsBannedBySteamID(steamID))
                {
                    Debug.LogWarning((object)$"'{SteamFriends.GetFriendPersonaName(lobbyMemberByIndex)}' ({steamID}) tried to join, but is banned! Not letting them in...");

                    // Send fake room ID to prevent joining (PEAKER approach)
                    string fakeRoomID = GenerateFakeRoomID();
                    BinarySerializer val = new BinarySerializer(256, (Allocator)2);
                    val.WriteByte((byte)2);
                    val.WriteString(fakeRoomID, Encoding.ASCII);
                    byte[] array = val.buffer.ToArray();
                    val.Dispose();

                    if (!SteamMatchmaking.SendLobbyChatMsg(currentLobby, array, array.Length))
                    {
                        Debug.LogError((object)"Failed to send Room ID...");
                        return false;
                    }

                    Debug.Log((object)("Lobby has been requested. Sending " + fakeRoomID + " (fake room)"));
                    return false;
                }
            }

            return true;
        }

        // Extracted from Plugin.cs:1656-1676
        private static string GenerateFakeRoomID()
        {
            string guid = Guid.NewGuid().ToString();
            string[] parts = new string[5]
            {
                guid.Substring(0, 14),
                (new char[15]
                {
                    '0', '1', '2', '3', '5', '6', '7', '8', '9', 'a',
                    'b', 'c', 'd', 'e', 'f'
                })[UnityEngine.Random.Range(0, 15)].ToString(),
                guid.Substring(15, 4),
                (new char[12]
                {
                    '0', '1', '2', '3', '4', '5', '6', '7', 'c', 'd',
                    'e', 'f'
                })[UnityEngine.Random.Range(0, 12)].ToString(),
                guid.Substring(20, guid.Length - 20)
            };
            return string.Concat(parts);
        }
    }
}