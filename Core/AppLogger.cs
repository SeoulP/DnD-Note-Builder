using Godot;
using System;
using System.IO;

public enum LogLevel { Trace = 0, Debug = 1, Info = 2, Warning = 3, Error = 4 }

public partial class AppLogger : Node
{
    public static AppLogger Instance { get; private set; }

    public event Action<string, LogLevel> ToastRequested;

    private string   _logPath;
    private LogLevel _minLevel = LogLevel.Info;

    public LogLevel MinLevel => _minLevel;

    public override void _Ready()
    {
        Instance = this;
    }

    public void SetLogDirectory(string dir)
    {
        _logPath = Path.Combine(dir, "app.log");
    }

    public void SetMinLevel(LogLevel level)
    {
        _minLevel = level;
    }

    public bool LogExists() => _logPath != null && File.Exists(_logPath);

    public void ClearLog()
    {
        if (_logPath != null && File.Exists(_logPath))
            File.Delete(_logPath);
        Info("AppLogger", "Log cleared by user");
    }

    public void Trace(string source, string message)                          => Write(LogLevel.Trace,   source, message, null);
    public void Debug(string source, string message)                          => Write(LogLevel.Debug,   source, message, null);
    public void Info(string source, string message)                           => Write(LogLevel.Info,    source, message, null);
    public void Warn(string source, string message)                           => Write(LogLevel.Warning, source, message, null);
    public void Error(string source, string message, Exception ex = null)     => Write(LogLevel.Error,   source, message, ex);

    private void Write(LogLevel level, string source, string message, Exception ex)
    {
        if (level < _minLevel) return;

        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ssZ");
        string levelStr  = level switch
        {
            LogLevel.Trace   => "TRACE",
            LogLevel.Debug   => "DEBUG",
            LogLevel.Info    => "INFO",
            LogLevel.Warning => "WARNING",
            LogLevel.Error   => "ERROR",
            _                => level.ToString().ToUpper(),
        };
        string entry = $"[{timestamp}] [{levelStr}] {source}: {message}";
        if (ex != null)
            entry += $"\n  {ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}";

        if (_logPath != null)
        {
            try { File.AppendAllText(_logPath, entry + "\n\n"); }
            catch { /* never crash the app due to logging */ }
        }

        if (level >= LogLevel.Warning)
            ToastRequested?.Invoke(message, level);
    }
}
