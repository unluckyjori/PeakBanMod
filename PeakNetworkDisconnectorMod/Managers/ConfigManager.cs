using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace PeakNetworkDisconnectorMod;

/// <summary>
/// Centralized configuration management for PeakBanMod
/// Provides type-safe access, validation, and migration support for configuration values
/// </summary>
public static class ConfigManager
{
    private static ConfigFile _configFile;
    private static readonly Dictionary<string, ConfigEntryBase> _configEntries = new Dictionary<string, ConfigEntryBase>();

    // Configuration sections
    private const string GENERAL_SECTION = "General";
    private const string PERFORMANCE_SECTION = "Performance";
    private const string ADVANCED_SECTION = "Advanced";

    // Configuration keys
    public const string TOGGLE_KEYBIND_KEY = "ToggleKeybind";
    public const string ENFORCEMENT_MODE_KEY = "EnforcementMode";
    public const string AUTO_DETECT_HACKS_KEY = "AutoDetectHacks";
    public const string BAN_CHECK_INTERVAL_KEY = "BanCheckInterval";
    public const string STEAM_UPDATE_INTERVAL_KEY = "SteamUpdateInterval";
    public const string DISABLE_ITEM_DROP_DETECTION_KEY = "DisableItemDropDetection";

    // Default values
    private const KeyCode DEFAULT_TOGGLE_KEY = KeyCode.F10;
    private const string DEFAULT_ENFORCEMENT_MODE = "Aggressive";
    private const bool DEFAULT_AUTO_DETECT_HACKS = true;
    private const float DEFAULT_BAN_CHECK_INTERVAL = 0.5f;
    private const float DEFAULT_STEAM_UPDATE_INTERVAL = 30f;
    private const bool DEFAULT_DISABLE_ITEM_DROP_DETECTION = false;

    // Configuration entries
    private static ConfigEntry<KeyCode> _toggleKeybind;
    private static ConfigEntry<string> _enforcementMode;
    private static ConfigEntry<bool> _autoDetectHacks;
    private static ConfigEntry<float> _banCheckInterval;
    private static ConfigEntry<float> _steamUpdateInterval;
    private static ConfigEntry<bool> _disableItemDropDetection;

    /// <summary>
    /// Initialize the configuration manager
    /// </summary>
    public static void Initialize(ConfigFile config)
    {
        _configFile = config;
        LoadConfiguration();
    }

    /// <summary>
    /// Load all configuration entries
    /// </summary>
    private static void LoadConfiguration()
    {
        // General settings
        _toggleKeybind = CreateConfigEntry(
            GENERAL_SECTION,
            TOGGLE_KEYBIND_KEY,
            DEFAULT_TOGGLE_KEY,
            "Key to toggle the ban menu"
        );

        _enforcementMode = CreateConfigEntry(
            GENERAL_SECTION,
            ENFORCEMENT_MODE_KEY,
            DEFAULT_ENFORCEMENT_MODE,
            new ConfigDescription(
                "Enforcement mode for handling cheating players. Passive: Players are frozen in place. Aggressive: Actively kick players from the game.",
                new AcceptableValueList<string>("Passive", "Aggressive")
            )
        );

        _autoDetectHacks = CreateConfigEntry(
            GENERAL_SECTION,
            AUTO_DETECT_HACKS_KEY,
            DEFAULT_AUTO_DETECT_HACKS,
            "Automatically detect and ban cheating users"
        );

        // Performance settings
        _banCheckInterval = CreateConfigEntry(
            PERFORMANCE_SECTION,
            BAN_CHECK_INTERVAL_KEY,
            DEFAULT_BAN_CHECK_INTERVAL,
            new ConfigDescription(
                "Interval in seconds between ban checks (lower values = more responsive but higher CPU usage)",
                new AcceptableValueRange<float>(0.1f, 5.0f)
            )
        );

        _steamUpdateInterval = CreateConfigEntry(
            PERFORMANCE_SECTION,
            STEAM_UPDATE_INTERVAL_KEY,
            DEFAULT_STEAM_UPDATE_INTERVAL,
            new ConfigDescription(
                "Interval in seconds between Steam ID updates (lower values = more accurate but higher API usage)",
                new AcceptableValueRange<float>(10f, 300f)
            )
        );

        _disableItemDropDetection = CreateConfigEntry(
            ADVANCED_SECTION,
            DISABLE_ITEM_DROP_DETECTION_KEY,
            DEFAULT_DISABLE_ITEM_DROP_DETECTION,
            "Disable PeakAntiCheat's item dropping detection (prevents false positives on legitimate drops)"
        );

        // Handle configuration migration
        MigrateOldConfiguration();
    }

    /// <summary>
    /// Create a configuration entry with validation
    /// </summary>
    private static ConfigEntry<T> CreateConfigEntry<T>(string section, string key, T defaultValue, string description)
    {
        return CreateConfigEntry(section, key, defaultValue, new ConfigDescription(description));
    }

    /// <summary>
    /// Create a configuration entry with validation
    /// </summary>
    private static ConfigEntry<T> CreateConfigEntry<T>(string section, string key, T defaultValue, ConfigDescription description)
    {
        var entry = _configFile.Bind(section, key, defaultValue, description);
        _configEntries[$"{section}.{key}"] = entry;
        return entry;
    }

    /// <summary>
    /// Handle migration from old configuration values
    /// </summary>
    private static void MigrateOldConfiguration()
    {
        // Check for old enforcement mode configuration
        var oldConfig = _configFile.TryGetEntry<string>(GENERAL_SECTION, "DefaultEnforcementMode", out var oldEntry);
        if (oldConfig && oldEntry != null)
        {
            var oldValue = oldEntry.GetSerializedValue();
            if (oldValue == "PassiveKick")
            {
                _enforcementMode.Value = "Passive";
                ErrorHandler.HandleInfo("Migrated old configuration: DefaultEnforcementMode 'PassiveKick' -> 'Passive'", "ConfigManager");
            }
            else if (oldValue == "AggressiveKick")
            {
                _enforcementMode.Value = "Aggressive";
                ErrorHandler.HandleInfo("Migrated old configuration: DefaultEnforcementMode 'AggressiveKick' -> 'Aggressive'", "ConfigManager");
            }

            // Remove old configuration entry
            var oldEntryToRemove = _configFile.TryGetEntry<string>(GENERAL_SECTION, "DefaultEnforcementMode", out var _);
            if (oldEntryToRemove)
            {
                _configFile.Remove(new ConfigDefinition(GENERAL_SECTION, "DefaultEnforcementMode"));
            }
        }
    }

    /// <summary>
    /// Validate all configuration values
    /// </summary>
    public static bool ValidateConfiguration()
    {
        bool isValid = true;

        // Validate enforcement mode
        if (_enforcementMode.Value != "Passive" && _enforcementMode.Value != "Aggressive")
        {
            ErrorHandler.HandleWarning($"Invalid enforcement mode: {_enforcementMode.Value}. Resetting to default.", "ConfigManager");
            _enforcementMode.Value = DEFAULT_ENFORCEMENT_MODE;
            isValid = false;
        }

        // Validate intervals
        if (_banCheckInterval.Value < 0.1f || _banCheckInterval.Value > 5.0f)
        {
            ErrorHandler.HandleWarning($"Invalid ban check interval: {_banCheckInterval.Value}. Resetting to default.", "ConfigManager");
            _banCheckInterval.Value = DEFAULT_BAN_CHECK_INTERVAL;
            isValid = false;
        }

        if (_steamUpdateInterval.Value < 10f || _steamUpdateInterval.Value > 300f)
        {
            ErrorHandler.HandleWarning($"Invalid Steam update interval: {_steamUpdateInterval.Value}. Resetting to default.", "ConfigManager");
            _steamUpdateInterval.Value = DEFAULT_STEAM_UPDATE_INTERVAL;
            isValid = false;
        }

        if (isValid)
        {
            ErrorHandler.HandleInfo("Configuration validation passed", "ConfigManager");
        }

        return isValid;
    }

    /// <summary>
    /// Save current configuration
    /// </summary>
    public static void SaveConfiguration()
    {
        _configFile.Save();
        ErrorHandler.HandleInfo("Configuration saved", "ConfigManager");
    }

    /// <summary>
    /// Reset configuration to defaults
    /// </summary>
    public static void ResetToDefaults()
    {
        _toggleKeybind.Value = DEFAULT_TOGGLE_KEY;
        _enforcementMode.Value = DEFAULT_ENFORCEMENT_MODE;
        _autoDetectHacks.Value = DEFAULT_AUTO_DETECT_HACKS;
        _banCheckInterval.Value = DEFAULT_BAN_CHECK_INTERVAL;
        _steamUpdateInterval.Value = DEFAULT_STEAM_UPDATE_INTERVAL;
        _disableItemDropDetection.Value = DEFAULT_DISABLE_ITEM_DROP_DETECTION;

        SaveConfiguration();
        ErrorHandler.HandleInfo("Configuration reset to defaults", "ConfigManager");
    }

    // Public accessors for configuration values
    public static KeyCode ToggleKeybind => _toggleKeybind.Value;
    public static string EnforcementMode => _enforcementMode.Value;
    public static bool AutoDetectHacks => _autoDetectHacks.Value;
    public static float BanCheckInterval => _banCheckInterval.Value;
    public static float SteamUpdateInterval => _steamUpdateInterval.Value;
    public static bool DisableItemDropDetection => _disableItemDropDetection.Value;

    // Public setters for configuration values
    public static void SetToggleKeybind(KeyCode value)
    {
        _toggleKeybind.Value = value;
        SaveConfiguration();
    }

    public static void SetEnforcementMode(string value)
    {
        if (value == "Passive" || value == "Aggressive")
        {
            _enforcementMode.Value = value;
            SaveConfiguration();
            ErrorHandler.HandleInfo($"Enforcement mode changed to: {value}", "ConfigManager");
        }
        else
        {
            ErrorHandler.HandleWarning($"Invalid enforcement mode: {value}", "ConfigManager");
        }
    }

    public static void SetAutoDetectHacks(bool value)
    {
        _autoDetectHacks.Value = value;
        SaveConfiguration();
    }

    public static void SetBanCheckInterval(float value)
    {
        if (value >= 0.1f && value <= 5.0f)
        {
            _banCheckInterval.Value = value;
            SaveConfiguration();
        }
        else
        {
            ErrorHandler.HandleWarning($"Invalid ban check interval: {value}", "ConfigManager");
        }
    }

    public static void SetSteamUpdateInterval(float value)
    {
        if (value >= 10f && value <= 300f)
        {
            _steamUpdateInterval.Value = value;
            SaveConfiguration();
        }
        else
        {
            ErrorHandler.HandleWarning($"Invalid Steam update interval: {value}", "ConfigManager");
        }
    }

    public static void SetDisableItemDropDetection(bool value)
    {
        _disableItemDropDetection.Value = value;
        SaveConfiguration();
    }

    /// <summary>
    /// Get all configuration entries for debugging
    /// </summary>
    public static Dictionary<string, string> GetAllConfiguration()
    {
        var config = new Dictionary<string, string>();
        foreach (var entry in _configEntries)
        {
            config[entry.Key] = entry.Value.GetSerializedValue();
        }
        return config;
    }
}