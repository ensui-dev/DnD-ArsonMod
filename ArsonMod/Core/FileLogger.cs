using System;
using System.IO;
using MelonLoader;

namespace ArsonMod.Core
{
    /// <summary>
    /// Persistent file logger that survives game crashes.
    /// Writes to ArsonMod_log.txt next to the game executable.
    /// Each line is flushed immediately so nothing is lost on crash.
    /// </summary>
    public static class FileLogger
    {
        private static StreamWriter _writer;
        private static readonly object _lock = new object();
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            try
            {
                string logPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "ArsonMod_log.txt"
                );

                // Overwrite each session so the file doesn't grow forever
                _writer = new StreamWriter(logPath, append: false)
                {
                    AutoFlush = true
                };

                _writer.WriteLine($"=== ArsonMod Log Started {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                _initialized = true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ArsonMod] FileLogger failed to initialize: {ex.Message}");
            }
        }

        public static void Log(string message)
        {
            if (!_initialized) return;
            lock (_lock)
            {
                try
                {
                    _writer.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
                }
                catch { /* swallow — don't crash the game over logging */ }
            }
        }

        public static void Error(string message, Exception ex = null)
        {
            if (!_initialized) return;
            lock (_lock)
            {
                try
                {
                    _writer.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ERROR: {message}");
                    if (ex != null)
                        _writer.WriteLine($"  Exception: {ex}");
                }
                catch { }
            }
        }

        public static void Close()
        {
            if (!_initialized) return;
            lock (_lock)
            {
                try
                {
                    _writer.WriteLine($"=== ArsonMod Log Closed {DateTime.Now:HH:mm:ss} ===");
                    _writer.Flush();
                    _writer.Close();
                }
                catch { }
                _initialized = false;
            }
        }
    }
}
