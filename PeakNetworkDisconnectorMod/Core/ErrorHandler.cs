using System;
using BepInEx.Logging;

namespace PeakNetworkDisconnectorMod;

/// <summary>
/// Centralized error handling for PeakBanMod
/// Provides consistent error logging, user-friendly messages, and error recovery
/// </summary>
public static class ErrorHandler
{
    private static ManualLogSource _logger;

    /// <summary>
    /// Initialize the error handler with a logger
    /// </summary>
    public static void Initialize(ManualLogSource logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Handle an exception with appropriate logging and user messaging
    /// </summary>
    public static void HandleError(Exception ex, string context, bool showUserMessage = false, string userFriendlyMessage = null)
    {
        if (_logger == null)
        {
            Console.WriteLine($"[ErrorHandler] Logger not initialized. Error in {context}: {ex.Message}");
            return;
        }

        // Log the full error details
        _logger.LogError($"Error in {context}: {ex.Message}");
        _logger.LogError($"Stack trace: {ex.StackTrace}");

        // Log recovery suggestion
        string recoverySuggestion = GetRecoverySuggestion(ex);
        _logger.LogInfo($"Recovery suggestion: {recoverySuggestion}");

        // Show user-friendly message if requested
        if (showUserMessage)
        {
            string message = userFriendlyMessage ?? GetUserFriendlyMessage(ex);
            SendUserMessage(message);
        }
    }

    /// <summary>
    /// Handle a warning with appropriate logging
    /// </summary>
    public static void HandleWarning(string message, string context)
    {
        if (_logger == null)
        {
            Console.WriteLine($"[ErrorHandler] Logger not initialized. Warning in {context}: {message}");
            return;
        }

        _logger.LogWarning($"Warning in {context}: {message}");
    }

    /// <summary>
    /// Handle an info message with appropriate logging
    /// </summary>
    public static void HandleInfo(string message, string context)
    {
        if (_logger == null)
        {
            Console.WriteLine($"[ErrorHandler] Logger not initialized. Info in {context}: {message}");
            return;
        }

        _logger.LogInfo($"Info in {context}: {message}");
    }

    /// <summary>
    /// Safely execute an action with error handling
    /// </summary>
    public static bool SafeExecute(Action action, string context, bool showUserMessage = false, string userFriendlyMessage = null)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex)
        {
            HandleError(ex, context, showUserMessage, userFriendlyMessage);
            return false;
        }
    }

    /// <summary>
    /// Safely execute a function with error handling
    /// </summary>
    public static T SafeExecute<T>(Func<T> func, string context, T defaultValue = default, bool showUserMessage = false, string userFriendlyMessage = null)
    {
        try
        {
            return func();
        }
        catch (Exception ex)
        {
            HandleError(ex, context, showUserMessage, userFriendlyMessage);
            return defaultValue;
        }
    }

    /// <summary>
    /// Send a message to the user through the in-game system
    /// </summary>
    private static void SendUserMessage(string message)
    {
        try
        {
            // Try to send message through the game's message system
            var messageService = UnityEngine.Object.FindFirstObjectByType<PlayerConnectionLog>();
            if (messageService != null)
            {
                var method = messageService.GetType().GetMethod("AddMessage", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (method != null)
                {
                    method.Invoke(messageService, new object[] { $"<color=red>PeakBanMod Error: {message}</color>" });
                }
            }
        }
        catch (Exception ex)
        {
            // Fallback to logging if message sending fails
            _logger?.LogWarning($"Failed to send user message: {ex.Message}");
        }
    }

    /// <summary>
    /// Get a user-friendly error message for common error types
    /// </summary>
    public static string GetUserFriendlyMessage(Exception ex)
    {
        if (ex is System.IO.IOException)
        {
            return "File operation failed. Please check file permissions.";
        }
        else if (ex is System.Net.WebException)
        {
            return "Network operation failed. Please check your internet connection.";
        }
        else if (ex is UnauthorizedAccessException)
        {
            return "Access denied. Please check permissions.";
        }
        else if (ex is OutOfMemoryException)
        {
            return "Out of memory. Please restart the application.";
        }
        else
        {
            return "An unexpected error occurred. Please check the logs for details.";
        }
    }

    /// <summary>
    /// Get recovery suggestions for common errors
    /// </summary>
    public static string GetRecoverySuggestion(Exception ex)
    {
        if (ex is System.IO.IOException)
        {
            return "Try running the game as administrator or check if the ban list file is not corrupted. You can also try deleting the ban list file to recreate it.";
        }
        else if (ex is System.Net.WebException)
        {
            return "Check your internet connection and firewall settings. Steam integration may not work properly without a stable connection.";
        }
        else if (ex is UnauthorizedAccessException)
        {
            return "Run the game as administrator or check file/folder permissions for the game's installation directory.";
        }
        else if (ex is OutOfMemoryException)
        {
            return "Close other applications to free up memory, or restart the game. Consider reducing graphics settings if this persists.";
        }
        else if (ex is NullReferenceException)
        {
            return "This appears to be a mod compatibility issue. Try disabling other mods temporarily to identify conflicts.";
        }
        else if (ex.Message.Contains("Steam"))
        {
            return "Steam integration failed. Make sure Steam is running and you're logged in. Restart both Steam and the game if needed.";
        }
        else if (ex.Message.Contains("Photon"))
        {
            return "Network operation failed. Check your internet connection and try rejoining the room.";
        }
        else
        {
            return "Check the mod's log files for more details. Consider updating the mod or reporting this issue to the developer.";
        }
    }
}