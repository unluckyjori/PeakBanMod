using System;
using HarmonyLib;
using UnityEngine;
using Photon.Pun;

namespace PeakNetworkDisconnectorMod;

[HarmonyPatch(typeof(NetworkConnector), "OnJoinedRoom")]
public class OnJoinedRoomPatch
{
	private static void Postfix()
	{
		try
		{
			if (PhotonNetwork.IsMasterClient)
			{
				UnityEngine.Debug.Log("Host joined room - network disconnector active");
			}
		}
		catch (Exception ex)
		{
			UnityEngine.Debug.LogError("Error in OnJoinedRoom patch: " + ex.Message);
		}
	}
}
