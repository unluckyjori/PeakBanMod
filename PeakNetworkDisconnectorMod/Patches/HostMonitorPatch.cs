using System;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using PeakNetworkDisconnectorMod;
using PeakNetworkDisconnectorMod.Managers;
using PeakNetworkDisconnectorMod.Core;

namespace PeakNetworkDisconnectorMod;

[HarmonyPatch(typeof(Plugin), "Update")]
public class HostMonitorPatch
{
	private static int lastKnownMasterClientId = -1;

	private static void Postfix(Plugin __instance)
	{
		try
		{
			if (!PhotonNetwork.InRoom)
			{
				return;
			}
			if (lastKnownMasterClientId == -1)
			{
				lastKnownMasterClientId = PhotonNetwork.MasterClient.ActorNumber;
			}
			else
			{
				if (PhotonNetwork.MasterClient.ActorNumber == lastKnownMasterClientId)
				{
					return;
				}
				UnityEngine.Debug.Log($"Master client changed from {lastKnownMasterClientId} to {PhotonNetwork.MasterClient.ActorNumber}");
				if (HostProtectionPatch.originalHostId != -1 && PhotonNetwork.LocalPlayer.ActorNumber == HostProtectionPatch.originalHostId && PhotonNetwork.MasterClient.ActorNumber != HostProtectionPatch.originalHostId)
				{
					UnityEngine.Debug.LogWarning("Detected host change outside of SetMasterClient! Reclaiming host status...");
					PhotonNetwork.SetMasterClient(PhotonNetwork.LocalPlayer);
					if (PhotonNetwork.IsMasterClient)
					{
						Photon.Realtime.Player player = PhotonNetwork.CurrentRoom.GetPlayer(PhotonNetwork.MasterClient.ActorNumber, false);
						if (player != null)
						{
							UnityEngine.Debug.LogWarning("Banning player " + player.NickName + " for host steal attempt");
							BanManager.BanPlayer(player, SteamIntegrationManager.Instance?.GetPlayerSteamID(player) ?? "Unknown", "Auto Ban: Host Steal Attempt (Outside SetMasterClient)");
							UtilityManager.Instance?.SendMessage("<color=red>Player " + player.NickName + " was banned for attempting to steal host</color>");
						}
					}
				}
				else
				{
					HostProtectionPatch.originalHostId = PhotonNetwork.MasterClient.ActorNumber;
				}
				lastKnownMasterClientId = PhotonNetwork.MasterClient.ActorNumber;
			}
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.LogError("Error in HostMonitorPatch: " + ex.Message);
		}
	}
}
