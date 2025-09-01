using System;
using System.Diagnostics;
using BepInEx.Logging;

namespace PeakNetworkDisconnectorMod;

/// <summary>
/// Centralized logging system for PeakBanMod
/// Provides consistent formatting, performance tracking, and configurable log levels
/// </summary>
public static class Logger
{
    private static ManualLogSource _logger;
    private static LogLevel _minLogLevel = LogLevel.Info;
    private static bool _performanceLoggingEnabled = false;

    // Performance tracking
    private static readonly Stopwatch _stopwatch = new Stopwatch();

    /// <summary>
    /// Initialize the logger
    /// </summary>
    public static void Initialize(ManualLogSource logger, LogLevel minLogLevel = LogLevel.Info, bool enablePerformanceLogging = false)
    {
        _logger = logger;
        _minLogLevel = minLogLevel;
        _performanceLoggingEnabled = enablePerformanceLogging;
        _stopwatch.Start();

        Info("Logger initialized", "Logger");
    }

    /// <summary>
    /// Set the minimum log level
    /// </summary>
    public static void SetMinLogLevel(LogLevel level)
    {
        _minLogLevel = level;
        Info($"Minimum log level set to: {level}", "Logger");
    }

    /// <summary>
    /// Enable or disable performance logging
    /// </summary>
    public static void SetPerformanceLogging(bool enabled)
    {
        _performanceLoggingEnabled = enabled;
        Info($"Performance logging {(enabled ? "enabled" : "disabled")}", "Logger");
    }

    /// <summary>
    /// Log an info message
    /// </summary>
    public static void Info(string message, string context = null, params object[] args)
    {
        if (_logger == null || LogLevel.Info < _minLogLevel) return;

        string formattedMessage = FormatMessage(message, context, "INFO", args);
        _logger.LogInfo((object)formattedMessage);
    }

    /// <summary>
    /// Log a warning message
    /// </summary>
    public static void Warning(string message, string context = null, params object[] args)
    {
        if (_logger == null || LogLevel.Warning < _minLogLevel) return;

        string formattedMessage = FormatMessage(message, context, "WARN", args);
        _logger.LogWarning((object)formattedMessage);
    }

    /// <summary>
    /// Log an error message
    /// </summary>
    public static void Error(string message, string context = null, params object[] args)
    {
        if (_logger == null || LogLevel.Error < _minLogLevel) return;

        string formattedMessage = FormatMessage(message, context, "ERROR", args);
        _logger.LogError((object)formattedMessage);
    }

    /// <summary>
    /// Log a debug message
    /// </summary>
    public static void Debug(string message, string context = null, params object[] args)
    {
        if (_logger == null || LogLevel.Debug < _minLogLevel) return;

        string formattedMessage = FormatMessage(message, context, "DEBUG", args);
        _logger.LogDebug((object)formattedMessage);
    }

    /// <summary>
    /// Log a fatal error message
    /// </summary>
    public static void Fatal(string message, string context = null, params object[] args)
    {
        if (_logger == null) return;

        string formattedMessage = FormatMessage(message, context, "FATAL", args);
        _logger.LogFatal((object)formattedMessage);
    }

    /// <summary>
    /// Log a message with a specific log level
    /// </summary>
    public static void Log(LogLevel level, string message, string context = null, params object[] args)
    {
        switch (level)
        {
            case LogLevel.Debug:
                Debug(message, context, args);
                break;
            case LogLevel.Info:
                Info(message, context, args);
                break;
            case LogLevel.Warning:
                Warning(message, context, args);
                break;
            case LogLevel.Error:
                Error(message, context, args);
                break;
            case LogLevel.Fatal:
                Fatal(message, context, args);
                break;
        }
    }

    /// <summary>
    /// Log performance information
    /// </summary>
    public static void Performance(string operation, long elapsedMilliseconds, string context = null)
    {
        if (!_performanceLoggingEnabled || _logger == null || LogLevel.Info < _minLogLevel) return;

        string message = $"Performance: {operation} took {elapsedMilliseconds}ms";
        string formattedMessage = FormatMessage(message, context, "PERF");
        _logger.LogInfo((object)formattedMessage);
    }

    /// <summary>
    /// Start performance timing for an operation
    /// </summary>
    public static IDisposable TimeOperation(string operationName, string context = null)
    {
        if (!_performanceLoggingEnabled) return new NoOpDisposable();

        return new PerformanceTimer(operationName, context);
    }

    /// <summary>
    /// Format a log message with consistent structure
    /// </summary>
    private static string FormatMessage(string message, string context, string level, params object[] args)
    {
        string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        string contextStr = string.IsNullOrEmpty(context) ? "PeakBanMod" : $"PeakBanMod.{context}";
        string formattedMessage = args.Length > 0 ? string.Format(message, args) : message;

        return $"[{timestamp}] [{level}] [{contextStr}] {formattedMessage}";
    }

    /// <summary>
    /// Log levels for filtering
    /// </summary>
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
        Fatal = 4
    }

    /// <summary>
    /// Performance timer disposable
    /// </summary>
    private class PerformanceTimer : IDisposable
    {
        private readonly string _operationName;
        private readonly string _context;
        private readonly long _startTime;

        public PerformanceTimer(string operationName, string context)
        {
            _operationName = operationName;
            _context = context;
            _startTime = _stopwatch.ElapsedMilliseconds;
        }

        public void Dispose()
        {
            long elapsed = _stopwatch.ElapsedMilliseconds - _startTime;
            Performance(_operationName, elapsed, _context);
        }
    }

    /// <summary>
    /// No-operation disposable for when performance logging is disabled
    /// </summary>
    private class NoOpDisposable : IDisposable
    {
        public void Dispose() { }
    }

    /// <summary>
    /// Get current log statistics
    /// </summary>
    public static string GetLogStats()
    {
        return $"Logger Stats - Min Level: {_minLogLevel}, Performance Logging: {_performanceLoggingEnabled}";
    }
}