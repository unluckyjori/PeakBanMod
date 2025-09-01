using UnityEngine;
using System;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using PeakNetworkDisconnectorMod;
using PeakNetworkDisconnectorMod.Managers;
using PeakNetworkDisconnectorMod.Core;

namespace PeakNetworkDisconnectorMod.Managers
{
    public class UIManager : MonoBehaviour
    {
        private static UIManager _instance;
        public static UIManager Instance => _instance;

        // UI state
        private bool showUI;
        private Rect windowRect = new Rect(50f, 50f, 400f, 500f);
        private int windowID = 12345;
        private string playerNameInput = "";
        private string playerSteamIDInput = "";
        private string banReasonInput = "";
        private Vector2 scrollPosition;

        // GUI Styles
        private GUIStyle windowStyle;
        private GUIStyle labelStyle;
        private GUIStyle buttonStyle;
        private GUIStyle banButtonStyle;
        private GUIStyle unbanButtonStyle;
        private GUIStyle textFieldStyle;
        private GUIStyle boxStyle;
        private GUIStyle headerStyle;
        private GUIStyle titleStyle;
        private GUIStyle playerNameStyle;

        // Textures
        private Texture2D windowTexture;
        private Texture2D buttonTexture;
        private Texture2D banButtonTexture;
        private Texture2D unbanButtonTexture;
        private Texture2D boxTexture;
        private Texture2D headerTexture;

        // Colors
        private Color windowColor = new Color(0.1f, 0.1f, 0.1f, 0.95f);
        private Color headerColor = new Color(0.8f, 0.2f, 0.2f, 1f);
        private Color buttonColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        private Color banButtonColor = new Color(0.8f, 0.2f, 0.2f, 1f);
        private Color unbanButtonColor = new Color(0.2f, 0.6f, 0.2f, 1f);
        private Color textColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        private Color boxColor = new Color(0.15f, 0.15f, 0.15f, 1f);

        // Last colors for change detection
        private Color _lastWindowColor;
        private Color _lastButtonColor;
        private Color _lastBanButtonColor;
        private Color _lastUnbanButtonColor;
        private Color _lastBoxColor;
        private Color _lastHeaderColor;

        void Awake()
        {
            _instance = this;
        }

        void OnDisable()
        {
            // Cleanup textures when component is disabled
            CleanupUITextures();
        }

        void OnGUI()
        {
            if (!showUI || !PhotonNetwork.IsMasterClient)
            {
                // Cleanup textures when UI is not shown
                if (windowTexture != null || buttonTexture != null || banButtonTexture != null ||
                    unbanButtonTexture != null || boxTexture != null || headerTexture != null)
                {
                    CleanupUITextures();
                }
                return;
            }
            try
            {
                InitializeStyles();
                windowRect = GUILayout.Window(windowID, windowRect, DrawWindow, "", titleStyle, new GUILayoutOption[0]);
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError((object)("Error in OnGUI: " + ex.Message));
            }
        }

        private bool ColorsChanged()
        {
            // Check if any colors have changed since last texture creation
            return windowTexture == null || buttonTexture == null ||
                   banButtonTexture == null || unbanButtonTexture == null ||
                   boxTexture == null || headerTexture == null ||
                   !windowColor.Equals(_lastWindowColor) ||
                   !buttonColor.Equals(_lastButtonColor) ||
                   !banButtonColor.Equals(_lastBanButtonColor) ||
                   !unbanButtonColor.Equals(_lastUnbanButtonColor) ||
                   !boxColor.Equals(_lastBoxColor) ||
                   !headerColor.Equals(_lastHeaderColor);
        }

        private void InitializeStyles()
        {
            if (windowStyle == null)
            {
                try
                {
                    // Store current colors for change detection
                    _lastWindowColor = windowColor;
                    _lastButtonColor = buttonColor;
                    _lastBanButtonColor = banButtonColor;
                    _lastUnbanButtonColor = unbanButtonColor;
                    _lastBoxColor = boxColor;
                    _lastHeaderColor = headerColor;

                    // Destroy existing textures before creating new ones
                    SafeDestroyTexture(ref windowTexture);
                    SafeDestroyTexture(ref buttonTexture);
                    SafeDestroyTexture(ref banButtonTexture);
                    SafeDestroyTexture(ref unbanButtonTexture);
                    SafeDestroyTexture(ref boxTexture);
                    SafeDestroyTexture(ref headerTexture);

                windowTexture = CreateColorTexture(windowColor);
                buttonTexture = CreateColorTexture(buttonColor);
                banButtonTexture = CreateColorTexture(banButtonColor);
                unbanButtonTexture = CreateColorTexture(unbanButtonColor);
                boxTexture = CreateColorTexture(boxColor);
                headerTexture = CreateColorTexture(headerColor);
                windowStyle = new GUIStyle(GUI.skin.window);
                windowStyle.normal.background = windowTexture;
                windowStyle.normal.textColor = textColor;
                windowStyle.padding = new RectOffset(10, 10, 20, 10);
                windowStyle.border = new RectOffset(10, 10, 20, 10);
                titleStyle = new GUIStyle(GUI.skin.window);
                titleStyle.normal.background = windowTexture;
                titleStyle.normal.textColor = textColor;
                titleStyle.padding = new RectOffset(10, 10, 20, 10);
                headerStyle = new GUIStyle(GUI.skin.label);
                headerStyle.fontSize = 16;
                headerStyle.normal.textColor = textColor;
                headerStyle.margin = new RectOffset(5, 5, 10, 5);
                headerStyle.padding = new RectOffset(5, 5, 5, 5);
                labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.fontSize = 12;
                labelStyle.normal.textColor = textColor;
                labelStyle.margin = new RectOffset(5, 5, 2, 2);
                playerNameStyle = new GUIStyle(GUI.skin.label);
                playerNameStyle.fontSize = 14;
                playerNameStyle.normal.textColor = textColor;
                playerNameStyle.margin = new RectOffset(5, 5, 5, 5);
                buttonStyle = new GUIStyle(GUI.skin.button);
                buttonStyle.fontSize = 12;
                buttonStyle.fixedHeight = 30f;
                buttonStyle.normal.background = buttonTexture;
                buttonStyle.normal.textColor = textColor;
                buttonStyle.hover.background = CreateColorTexture(new Color(buttonColor.r * 1.2f, buttonColor.g * 1.2f, buttonColor.b * 1.2f));
                buttonStyle.hover.textColor = Color.white;
                buttonStyle.active.background = CreateColorTexture(new Color(buttonColor.r * 0.8f, buttonColor.g * 0.8f, buttonColor.b * 0.8f));
                buttonStyle.padding = new RectOffset(10, 10, 5, 5);
                banButtonStyle = new GUIStyle(GUI.skin.button);
                banButtonStyle.fontSize = 12;
                banButtonStyle.fixedHeight = 30f;
                banButtonStyle.normal.background = banButtonTexture;
                banButtonStyle.normal.textColor = textColor;
                banButtonStyle.hover.background = CreateColorTexture(new Color(banButtonColor.r * 1.2f, banButtonColor.g * 1.2f, banButtonColor.b * 1.2f));
                banButtonStyle.hover.textColor = Color.white;
                banButtonStyle.active.background = CreateColorTexture(new Color(banButtonColor.r * 0.8f, banButtonColor.g * 0.8f, banButtonColor.b * 0.8f));
                banButtonStyle.padding = new RectOffset(10, 10, 5, 5);
                unbanButtonStyle = new GUIStyle(GUI.skin.button);
                unbanButtonStyle.fontSize = 12;
                unbanButtonStyle.fixedHeight = 30f;
                unbanButtonStyle.normal.background = unbanButtonTexture;
                unbanButtonStyle.normal.textColor = textColor;
                unbanButtonStyle.hover.background = CreateColorTexture(new Color(unbanButtonColor.r * 1.2f, unbanButtonColor.g * 1.2f, unbanButtonColor.b * 1.2f));
                unbanButtonStyle.hover.textColor = Color.white;
                unbanButtonStyle.active.background = CreateColorTexture(new Color(unbanButtonColor.r * 0.8f, unbanButtonColor.g * 0.8f, unbanButtonColor.b * 0.8f));
                unbanButtonStyle.padding = new RectOffset(10, 10, 5, 5);
                textFieldStyle = new GUIStyle(GUI.skin.textField);
                textFieldStyle.fontSize = 14;
                textFieldStyle.fixedHeight = 30f;
                textFieldStyle.normal.textColor = Color.black;
                boxStyle = new GUIStyle(GUI.skin.box);
                boxStyle.normal.background = boxTexture;
                boxStyle.normal.textColor = textColor;
                boxStyle.padding = new RectOffset(10, 10, 10, 10);
                boxStyle.margin = new RectOffset(0, 0, 5, 5);
                }
                catch (System.Exception ex)
                {
                    Plugin.Log.LogError((object)("Error initializing UI styles: " + ex.Message));
                    // Reset all styles to force recreation on next call
                    ResetAllStyles();
                }
            }
        }

        private Texture2D CreateColorTexture(Color color)
        {
            Plugin.Log.LogDebug((object)"[UIManager] Creating color texture");
            Texture2D val = new Texture2D(2, 2);
            Color[] pixels = (Color[])(object)new Color[4] { color, color, color, color };
            val.SetPixels(pixels);
            val.Apply();
            Plugin.Log.LogDebug((object)"[UIManager] Color texture created successfully");
            return val;
        }

        private void SafeDestroyTexture(ref Texture2D texture)
        {
            if (texture != null)
            {
                try
                {
                    UnityEngine.Object.Destroy(texture);
                }
                catch (System.Exception ex)
                {
                    Plugin.Log.LogWarning((object)("Failed to destroy texture: " + ex.Message));
                }
                finally
                {
                    texture = null;
                }
            }
        }

        private void ResetAllStyles()
        {
            windowStyle = null;
            titleStyle = null;
            buttonStyle = null;
            banButtonStyle = null;
            unbanButtonStyle = null;
            textFieldStyle = null;
            boxStyle = null;
            headerStyle = null;
            labelStyle = null;
            playerNameStyle = null;
        }

        private void CleanupUITextures()
        {
            Plugin.Log.LogDebug((object)"[UIManager] Starting texture cleanup");

            SafeDestroyTexture(ref windowTexture);
            if (windowTexture == null) Plugin.Log.LogDebug((object)"[UIManager] Destroyed windowTexture");

            SafeDestroyTexture(ref buttonTexture);
            if (buttonTexture == null) Plugin.Log.LogDebug((object)"[UIManager] Destroyed buttonTexture");

            SafeDestroyTexture(ref banButtonTexture);
            if (banButtonTexture == null) Plugin.Log.LogDebug((object)"[UIManager] Destroyed banButtonTexture");

            SafeDestroyTexture(ref unbanButtonTexture);
            if (unbanButtonTexture == null) Plugin.Log.LogDebug((object)"[UIManager] Destroyed unbanButtonTexture");

            SafeDestroyTexture(ref boxTexture);
            if (boxTexture == null) Plugin.Log.LogDebug((object)"[UIManager] Destroyed boxTexture");

            SafeDestroyTexture(ref headerTexture);
            if (headerTexture == null) Plugin.Log.LogDebug((object)"[UIManager] Destroyed headerTexture");

            // Reset styles to null so they get recreated properly on next open
            windowStyle = null;
            titleStyle = null;
            buttonStyle = null;
            banButtonStyle = null;
            unbanButtonStyle = null;
            textFieldStyle = null;
            boxStyle = null;
            headerStyle = null;
            labelStyle = null;
            playerNameStyle = null;
            Plugin.Log.LogDebug((object)"[UIManager] Texture cleanup completed");
        }

        private void DrawWindow(int id)
        {
            try
            {
                if (!PhotonNetwork.InRoom)
                {
                    GUILayout.Label("Not connected to room", labelStyle, Array.Empty<GUILayoutOption>());
                    GUI.DragWindow();
                    return;
                }
                GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                GUILayout.Label("PEAK BAN SYSTEM", headerStyle, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
                if (GUILayout.Button("✖", buttonStyle, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(30f) }))
                {
                    showUI = false;
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(5f);
                GUILayout.BeginVertical(Array.Empty<GUILayoutOption>());
                GUILayout.BeginVertical(boxStyle, Array.Empty<GUILayoutOption>());
                GUILayout.Label("ONLINE PLAYERS", headerStyle, Array.Empty<GUILayoutOption>());
                Photon.Realtime.Player[] array = PhotonNetwork.PlayerList;
                Photon.Realtime.Player[] array2 = array;
                foreach (Photon.Realtime.Player val in array2)
                {
                    if (val == null)
                    {
                        continue;
                    }
                    GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                    string text = val.NickName ?? "Unknown";
                    if (val.IsMasterClient)
                    {
                        text = "\ud83d\udc51 " + text;
                    }
                    if (val.IsLocal)
                    {
                        text += " (YOU)";
                    }
                     bool flag = EnforcementManager.Instance.TargetPlayers.ContainsKey(val.ActorNumber);
                     bool flag2 = BanManager.IsBanned(val.NickName ?? "");
                     string playerSteamID = SteamIntegrationManager.Instance?.GetPlayerSteamID(val) ?? "Unknown";
                     bool flag3 = BanManager.IsBannedBySteamID(playerSteamID);
                     GUILayout.Label(text, playerNameStyle, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
                     GUILayout.Label(playerSteamID, labelStyle, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(120f) });
                     // Always render exactly 4 controls per row for consistent layout
                     if (flag2 || flag3)
                     {
                         GUILayout.Label("\ud83d\udeab BANNED", playerNameStyle, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(80f) });
                     }
                     else if (flag)
                     {
                         if (GUILayout.Button("STOP", buttonStyle, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(80f) }))
                         {
                             Managers.EnforcementManager.Instance?.StopTargetingPlayer(val);
                         }
                     }
                     else if (!val.IsMasterClient && !val.IsLocal)
                     {
                         if (GUILayout.Button("BAN", banButtonStyle, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(80f) }))
                         {
                             BanManager.BanPlayer(val, playerSteamID);
                             BanManager.StartTargetingPlayer(val);
                             if (EnforcementManager.Instance != null)
                             {
                                 EnforcementManager.Instance.ApplyKickActionsToPlayer(val);
                             }
                         }
                     }
                     else
                     {
                         GUILayout.Label("", playerNameStyle, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(80f) });
                     }
                     // Add consistent 4th control (empty space for alignment)
                     GUILayout.Label("", labelStyle, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(20f) });
                    GUILayout.EndHorizontal();
                    GUILayout.Space(2f);
                }
                GUILayout.EndVertical();
                GUILayout.Space(10f);
                GUILayout.BeginVertical(boxStyle, Array.Empty<GUILayoutOption>());
                GUILayout.Label("BAN BY NAME", headerStyle, Array.Empty<GUILayoutOption>());
                GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                playerNameInput = GUILayout.TextField(playerNameInput ?? "", textFieldStyle, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
                if (GUILayout.Button("BAN", banButtonStyle, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(80f) }) && !string.IsNullOrEmpty(playerNameInput))
                {
                    BanManager.BanPlayerByName(playerNameInput, banReasonInput);
                    playerNameInput = "";
                }
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                GUILayout.Label("Ban Reason:", labelStyle, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(80f) });
                banReasonInput = GUILayout.TextField(banReasonInput ?? "", textFieldStyle, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.Space(10f);
                GUILayout.BeginVertical(boxStyle, Array.Empty<GUILayoutOption>());
                GUILayout.Label("BAN BY STEAM ID", headerStyle, Array.Empty<GUILayoutOption>());
                GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                playerSteamIDInput = GUILayout.TextField(playerSteamIDInput ?? "", textFieldStyle, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.ExpandWidth(true) });
                if (GUILayout.Button("BAN", banButtonStyle, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(80f) }) && !string.IsNullOrEmpty(playerSteamIDInput))
                {
                    BanManager.BanPlayerBySteamID(playerSteamIDInput, banReasonInput);
                    playerSteamIDInput = "";
                }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.Space(10f);
                GUILayout.BeginVertical(boxStyle, Array.Empty<GUILayoutOption>());
                GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
                
                // Check if any anti-cheat is detected to determine UI behavior
                bool anyPeakerDetected = PeakerIntegration.IsPeakerDetected;
                bool anyPeakAntiCheatDetected = PeakAntiCheatIntegration.IsPeakAntiCheatDetected;
                bool isAnyAntiCheatDetected = anyPeakerDetected || anyPeakAntiCheatDetected;
                
                // Always allow auto-detect to be toggled, with info about which system is active
                if (isAnyAntiCheatDetected)
                {
                    GUILayout.Label("Auto-detect and ban cheating users (using external anti-cheats):", labelStyle, Array.Empty<GUILayoutOption>());
                }
                else
                {
                    GUILayout.Label("Auto-detect and ban cheating users (using built-in detection):", labelStyle, Array.Empty<GUILayoutOption>());
                }
                
                // Create custom styles for the toggle button with the requested colors
                GUIStyle enabledStyle = new GUIStyle(banButtonStyle);
                enabledStyle.normal.background = CreateColorTexture(new Color(0.2f, 0.6f, 0.2f)); // Green background (original shade)
                enabledStyle.normal.textColor = Color.white; // White text
                enabledStyle.hover.background = CreateColorTexture(new Color(0.3f, 0.7f, 0.3f));
                enabledStyle.hover.textColor = Color.white;
                enabledStyle.active.background = CreateColorTexture(new Color(0.1f, 0.5f, 0.1f));
                enabledStyle.active.textColor = Color.white;
                
                GUIStyle disabledStyle = new GUIStyle(unbanButtonStyle);
                disabledStyle.normal.background = CreateColorTexture(new Color(0.8f, 0.2f, 0.2f)); // Red background (same as ban buttons)
                disabledStyle.normal.textColor = Color.white; // White text
                disabledStyle.hover.background = CreateColorTexture(new Color(0.9f, 0.3f, 0.3f));
                disabledStyle.hover.textColor = Color.white;
                disabledStyle.active.background = CreateColorTexture(new Color(0.7f, 0.1f, 0.1f));
                disabledStyle.active.textColor = Color.white;
                
                if (GUILayout.Button(ConfigManager.AutoDetectHacks ? "ENABLED" : "DISABLED", ConfigManager.AutoDetectHacks ? enabledStyle : disabledStyle, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(100f) }))
                {
                    ConfigManager.SetAutoDetectHacks(!ConfigManager.AutoDetectHacks);
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(10f);
                
                // Add enforcement mode selection UI
                GUILayout.BeginVertical(boxStyle, Array.Empty<GUILayoutOption>());
                GUILayout.Label("Enforcement Mode", headerStyle, Array.Empty<GUILayoutOption>());
                
                // Create dropdown for enforcement mode with new labels
                string[] enforcementOptions = { "Passive", "Aggressive" };
                int currentModeIndex = ConfigManager.EnforcementMode == "Passive" ? 0 : 1;

                GUIStyle dropdownStyle = new GUIStyle(buttonStyle);
                dropdownStyle.alignment = TextAnchor.MiddleCenter;

                int newModeIndex = GUILayout.SelectionGrid(currentModeIndex, enforcementOptions, 2, dropdownStyle, GUILayout.Height(30f));
                if (newModeIndex != currentModeIndex)
                {
                    ConfigManager.SetEnforcementMode(enforcementOptions[newModeIndex]);
                }
                

                
                GUILayout.EndVertical();
                
                GUILayout.Space(10f);
                var bannedPlayersList = BanManager.GetBannedPlayers();
                GUILayout.Label($"BANNED PLAYERS ({bannedPlayersList.Count})", headerStyle, Array.Empty<GUILayoutOption>());
                scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, true, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(180f) });
                if (bannedPlayersList.Count == 0)
                {
                    GUILayout.Label("No banned players", labelStyle, Array.Empty<GUILayoutOption>());
                }
                else
                {
                    BannedPlayer[] array3 = bannedPlayersList.ToArray();
                    for (int num = array3.Length - 1; num >= 0; num--)
                    {
                        BannedPlayer bannedPlayer = array3[num];
                        if (bannedPlayer != null)
                        {
                            GUILayout.BeginHorizontal(boxStyle, Array.Empty<GUILayoutOption>());
                            GUILayout.BeginVertical(Array.Empty<GUILayoutOption>());
                            GUILayout.Label(bannedPlayer.PlayerName ?? "Unknown", playerNameStyle, Array.Empty<GUILayoutOption>());
                            GUILayout.Label("Steam ID: " + (bannedPlayer.SteamID ?? "Unknown"), labelStyle, Array.Empty<GUILayoutOption>());
                            GUILayout.Label(bannedPlayer.BanDate ?? "Unknown Date", labelStyle, Array.Empty<GUILayoutOption>());
                            if (!string.IsNullOrEmpty(bannedPlayer.Reason))
                            {
                                GUILayout.Label("Reason: " + bannedPlayer.Reason, labelStyle, Array.Empty<GUILayoutOption>());
                            }
                            GUILayout.EndVertical();
                            GUILayout.FlexibleSpace();
                            if (GUILayout.Button("UNBAN", unbanButtonStyle, (GUILayoutOption[])(object)new GUILayoutOption[2]
                            {
                                GUILayout.Width(70f),
                                GUILayout.Height(40f)
                            }))
                            {
                                BanManager.UnbanPlayer(bannedPlayer.PlayerName, bannedPlayer.SteamID);
                                // Warp unbanned player to host
                                if (Managers.UtilityManager.Instance != null)
                                {
                                    foreach (var player in PhotonNetwork.PlayerList)
                                    {
                                        if (player.NickName == bannedPlayer.PlayerName || player.UserId == bannedPlayer.SteamID)
                                        {
                                            Managers.UtilityManager.Instance.WarpPlayerToHost(player);
                                            break;
                                        }
                                    }
                                }
                            }
                            GUILayout.EndHorizontal();
                            GUILayout.Space(5f);
                        }
                    }
                }
                GUILayout.EndScrollView();
                GUILayout.EndVertical();
                
                // Add External Anti-Cheat Integration status display
                GUILayout.Space(10f);
                GUILayout.BeginVertical(boxStyle, Array.Empty<GUILayoutOption>());
                GUILayout.Label("External Anti-Cheat Integration", headerStyle, Array.Empty<GUILayoutOption>());
                
                // Check if either anti-cheat is detected
                bool statusPeakerDetected = PeakerIntegration.IsPeakerDetected;
                bool statusPeakAntiCheatDetected = PeakAntiCheatIntegration.IsPeakAntiCheatDetected;
                
                if (statusPeakerDetected)
                {
                    GUILayout.Label("PEAKER Mod: Detected", labelStyle, Array.Empty<GUILayoutOption>());
                }
                
                if (statusPeakAntiCheatDetected)
                {
                    GUILayout.Label("PeakAntiCheat Mod: Detected", labelStyle, Array.Empty<GUILayoutOption>());
                    
                    // Show current enforcement mode when PeakAntiCheat is detected
                    string selectedMode = ConfigManager.EnforcementMode;
                    GUILayout.Label($"Enforcement Mode: {selectedMode}", labelStyle, Array.Empty<GUILayoutOption>());
                }
                
                if (!statusPeakerDetected && !statusPeakAntiCheatDetected)
                {
                    GUILayout.Label("No external anti-cheats detected. Using built in detection.", labelStyle, Array.Empty<GUILayoutOption>());
                }
                else
                {
                    GUILayout.Label("External Anti-Cheat Integration: Active", labelStyle, Array.Empty<GUILayoutOption>());
                }
                
                GUILayout.EndVertical();
                
                GUILayout.Space(5f);
                GUILayout.Label($"Packets sent: {EnforcementManager.Instance?.PacketsCount ?? 0}", labelStyle, new GUILayoutOption[0]);
                GUILayout.Label($"Press {ConfigManager.ToggleKeybind} to toggle this window", labelStyle, new GUILayoutOption[0]);
                GUILayout.EndVertical();
                GUI.DragWindow();
            }
            catch (System.Exception)
            {
                GUILayout.Label("GUI Error - Check logs", labelStyle, Array.Empty<GUILayoutOption>());
                GUI.DragWindow();
            }
        }

        // Public method to toggle UI
        public void ToggleUI()
        {
            showUI = !showUI;
            if (showUI)
            {
                Plugin.Log.LogInfo((object)"Network Disconnector UI opened (Host only)");
            }
        }

        private void OnDestroy()
        {
            CleanupUITextures();
        }
    }
}