using System;
using HarmonyLib;
using Photon.Pun;

namespace PeakNetworkDisconnectorMod;

[HarmonyPatch(typeof(PhotonView))]
[HarmonyPatch("RPC", new Type[]
{
	typeof(string),
	typeof(RpcTarget),
	typeof(object[])
})]
public class PhotonViewRPCPatch
{
	private static bool Prefix(PhotonView __instance, string methodName, RpcTarget target, params object[] parameters)
	{
		try
		{
			if (methodName == "FloodPacketRPC")
			{
				Plugin.Log.LogDebug((object)"Sending flood packet");
				return true;
			}
			return true;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError((object)("Error in RPC patch: " + ex.Message));
			return true;
		}
	}
}
