using System;
using System.Collections;
using System.Reflection;
using BepInEx.Logging;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using PeakNetworkDisconnectorMod.Core;

#nullable enable

namespace PeakNetworkDisconnectorMod.Managers
{
    /// <summary>
    /// Manages utility functions for player manipulation and messaging
    /// Handles teleportation, messaging, and other helper operations
    /// </summary>
    public class UtilityManager : MonoBehaviour
    {
        private static UtilityManager _instance;
        public static UtilityManager Instance => _instance;

        private ManualLogSource _logger;

        void Awake()
        {
            _instance = this;
        }

        /// <summary>
        /// Initialize the utility manager with required dependencies
        /// </summary>
        public void Initialize(ManualLogSource logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Send an in-game message to the connection log
        /// </summary>
        public new void SendMessage(string message)
        {
            ErrorHandler.SafeExecute(() =>
            {
                _logger.LogInfo((object)("MESSAGE: " + message));
                PlayerConnectionLog val = UnityEngine.Object.FindFirstObjectByType<PlayerConnectionLog>();
                if ((UnityEngine.Object)(object)val != (UnityEngine.Object)null)
                {
                    MethodInfo method = ((object)val).GetType().GetMethod("AddMessage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (method != null)
                    {
                        method.Invoke(val, new object[1] { message });
                        _logger.LogInfo((object)("Sent message to connection log: " + message));
                    }
                }
            }, "SendMessage", false, "Failed to send in-game message");
        }

        /// <summary>
        /// Warp a player to the host's position
        /// </summary>
        public void WarpPlayerToHost(Photon.Realtime.Player player)
        {
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
                if ((UnityEngine.Object)val != (UnityEngine.Object)null && (UnityEngine.Object)Character.localCharacter != (UnityEngine.Object)null)
                {
                    Vector3 val2 = Character.localCharacter.Head + new Vector3(0f, 4f, 0f);
                    _logger.LogInfo((object)$"Host position: {val2}");
                    _logger.LogInfo((object)$"Player position before teleport: {((Component)val).transform.position}");
                    if (val.data.dead)
                    {
                        ((MonoBehaviourPun)val).photonView.RPC("RPCA_ReviveAtPosition", (RpcTarget)0, new object[2] { val2, true });
                        _logger.LogInfo((object)"Revived player at host position");
                    }
                    ((MonoBehaviourPun)val).photonView.RPC("WarpPlayerRPC", (RpcTarget)0, new object[2] { val2, true });
                    ((Component)val).transform.position = val2;
                    _logger.LogInfo((object)$"Teleported player {player.NickName} to host position: {val2}");
                    ((MonoBehaviourPun)val).photonView.RPC("RPCA_Heal", (RpcTarget)0, new object[1] { 100f });
                    _logger.LogInfo((object)"Healed player to full health");
                    StartCoroutine(DelayedSecondTeleport(val, player));
                }
                else
                {
                    _logger.LogWarning((object)("Could not find character for player " + player.NickName + " to teleport"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError((object)("Error teleporting player to host: " + ex.Message));
            }
        }

        /// <summary>
        /// Delayed second teleport coroutine
        /// </summary>
        private IEnumerator DelayedSecondTeleport(Character targetCharacter, Photon.Realtime.Player player)
        {
            yield return (object)new WaitForSeconds(1f);
            try
            {
                if ((UnityEngine.Object)targetCharacter != (UnityEngine.Object)null && (UnityEngine.Object)Character.localCharacter != (UnityEngine.Object)null)
                {
                    Vector3 val = Character.localCharacter.Head + new Vector3(0f, 4f, 0f);
                    ((MonoBehaviourPun)targetCharacter).photonView.RPC("WarpPlayerRPC", (RpcTarget)0, new object[2] { val, true });
                    ((Component)targetCharacter).transform.position = val;
                    _logger.LogInfo((object)$"Second teleport of player {player.NickName} to host position: {val}");
                }
            }
            catch (Exception ex)
            {
                Exception ex2 = ex;
                _logger.LogError((object)("Error in delayed teleport: " + ex2.Message));
            }
        }
    }
}