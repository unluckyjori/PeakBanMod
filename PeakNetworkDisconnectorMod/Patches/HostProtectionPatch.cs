using System;
using System.Collections.Generic;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using PeakNetworkDisconnectorMod;
using PeakNetworkDisconnectorMod.Managers;
using PeakNetworkDisconnectorMod.Core;

namespace PeakNetworkDisconnectorMod;

[HarmonyPatch(typeof(PhotonNetwork), "SetMasterClient")]
public class HostProtectionPatch
{
	[HarmonyPatch(typeof(NetworkConnector), "OnJoinedRoom")]
	public static class OnJoinedRoomPatch
	{
		private static void Postfix()
		{
			try
			{
				originalHostId = -1;
				hostStealAttempts.Clear();
				lastLegitimateHostChangeTime = Time.time;
				if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom.PlayerCount == 1)
				{
					originalHostId = PhotonNetwork.LocalPlayer.ActorNumber;
					UnityEngine.Debug.Log($"Original host set to local player (ID: {originalHostId})");
				}
				else if (PhotonNetwork.MasterClient != null)
				{
					originalHostId = PhotonNetwork.MasterClient.ActorNumber;
					UnityEngine.Debug.Log($"Original host set to current master client (ID: {originalHostId})");
				}
			}
			catch (Exception ex)
			{
				UnityEngine.Debug.LogError("Error in OnJoinedRoomPatch: " + ex.Message);
			}
		}
	}

	internal static int originalHostId = -1;

	private static float lastLegitimateHostChangeTime = 0f;

	private static readonly float HOST_CHANGE_COOLDOWN = 5f;

	internal static HashSet<int> hostStealAttempts = new HashSet<int>();

	private static bool Prefix(Photon.Realtime.Player masterClientPlayer)
	{
		try
		{
			if (!PhotonNetwork.InRoom)
			{
				return true;
			}
			if (originalHostId == -1 && masterClientPlayer != null)
			{
				originalHostId = masterClientPlayer.ActorNumber;
				lastLegitimateHostChangeTime = Time.time;
				UnityEngine.Debug.Log($"Original host initially set to: {originalHostId}");
				return true;
			}
			if (!(Time.time - lastLegitimateHostChangeTime > HOST_CHANGE_COOLDOWN) && originalHostId != masterClientPlayer.ActorNumber && PhotonNetwork.CurrentRoom.GetPlayer(originalHostId, false) != null)
			{
				UnityEngine.Debug.LogWarning($"Possible host steal attempt detected! Player {masterClientPlayer.NickName} (ID: {masterClientPlayer.ActorNumber}) is trying to become host!");
				hostStealAttempts.Add(masterClientPlayer.ActorNumber);
				if (PhotonNetwork.LocalPlayer.ActorNumber == originalHostId)
				{
					Plugin.Log.LogWarning((object)"We are the original host. Reclaiming host status...");
					PhotonNetwork.SetMasterClient(PhotonNetwork.LocalPlayer);
					if (PhotonNetwork.IsMasterClient)
					{
						UnityEngine.Debug.LogWarning("Banning player " + masterClientPlayer.NickName + " for host steal attempt");
						BanManager.BanPlayer(masterClientPlayer, SteamIntegrationManager.Instance?.GetPlayerSteamID(masterClientPlayer) ?? "Unknown", "Auto Ban: Host Steal Attempt");
						UtilityManager.Instance?.SendMessage("<color=red>Player " + masterClientPlayer.NickName + " was banned for attempting to steal host</color>");
					}
				}
				else
				{
					UnityEngine.Debug.Log("We are not the original host. Cannot reclaim host status.");
				}
				return false;
			}
			UnityEngine.Debug.Log($"Legitimate host change to: {masterClientPlayer.NickName} (ID: {masterClientPlayer.ActorNumber})");
			originalHostId = masterClientPlayer.ActorNumber;
			lastLegitimateHostChangeTime = Time.time;
			return true;
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.LogError("Error in HostProtectionPatch: " + ex.Message);
			return true;
		}
	}
}
