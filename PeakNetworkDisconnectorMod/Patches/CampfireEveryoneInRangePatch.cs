using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using PeakNetworkDisconnectorMod;
using PeakNetworkDisconnectorMod.Managers;
using PeakNetworkDisconnectorMod.Core;

namespace PeakNetworkDisconnectorMod;

[HarmonyPatch(typeof(Campfire), "EveryoneInRange")]
public class CampfireEveryoneInRangePatch
{
	private static bool Prefix(Campfire __instance, ref bool __result, ref string printout)
	{
		if (!PhotonNetwork.IsMasterClient)
		{
			return true;
		}
		bool flag = true;
		printout = "";
		foreach (Character allPlayerCharacter in PlayerHandler.GetAllPlayerCharacters())
		{
			if ((((MonoBehaviourPun)allPlayerCharacter).photonView.Owner == null || (!BanManager.IsBanned(((MonoBehaviourPun)allPlayerCharacter).photonView.Owner.NickName) && !BanManager.IsBannedBySteamID(SteamIntegrationManager.Instance?.GetPlayerSteamID(((MonoBehaviourPun)allPlayerCharacter).photonView.Owner) ?? "Unknown"))) && !allPlayerCharacter.data.dead)
			{
				float num = Vector3.Distance(((Component)__instance).transform.position, allPlayerCharacter.Center);
				if (num > 15f)
				{
					flag = false;
					printout += $"\n{((MonoBehaviourPun)allPlayerCharacter).photonView.Owner.NickName} {Mathf.RoundToInt(num * CharacterStats.unitsToMeters)}m";
				}
			}
		}
		if (!flag)
		{
			printout = "can't light campfire with friends missing!\n" + printout;
		}
		__result = flag;
		return false;
	}
}
