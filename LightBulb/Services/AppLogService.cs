using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LightBulb.Services;

public sealed class AppLogService
{
    private const long MaximumLogFileBytes = 2 * 1024 * 1024;
    private readonly object _lock = new();

    public static string LogDirectoryPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "LightBulb",
        "Logs"
    );

    public static string LogFilePath { get; } = Path.Combine(LogDirectoryPath, "LightBulb.log");

    public event EventHandler? Changed;

    public AppLogService()
    {
        try
        {
            Directory.CreateDirectory(LogDirectoryPath);
            TrimIfNeeded();
        }
        catch
        {
            // Logging itself must never stop the application from starting.
        }
    }

    // The user-facing log is intentionally warnings/errors only. Informational diagnostics stay
    // out of the persistent file so the Log page remains actionable rather than noisy.
    public void Info(string source, string message) { }

    public void Warning(string source, string message) => Write("WARN", source, message);

    public void Error(string source, string message, Exception? exception = null) =>
        Write("ERROR", source, exception is null ? message : message + Environment.NewLine + exception);

    public string GetText()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(LogFilePath))
                    return string.Empty;

                // Filter legacy INFO entries too, so upgrading immediately gives a warnings/errors-only
                // view without requiring the user to clear the old log first. Stack-trace/detail lines
                // belonging to a WARN/ERROR entry are retained until the next timestamped entry.
                var lines = File.ReadAllLines(LogFilePath);
                var builder = new StringBuilder();
                var includeEntry = false;

                foreach (var line in lines)
                {
                    if (line.StartsWith("[", StringComparison.Ordinal))
                    {
                        includeEntry =
                            line.Contains("] [WARN] [", StringComparison.Ordinal)
                            || line.Contains("] [ERROR] [", StringComparison.Ordinal);
                    }

                    if (includeEntry)
                        builder.AppendLine(line);
                }

                return builder.ToString();
            }
            catch (Exception ex)
            {
                return $"Unable to read log: {ex.Message}";
            }
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            try
            {
                Directory.CreateDirectory(LogDirectoryPath);
                File.WriteAllText(LogFilePath, string.Empty);
            }
            catch
            {
                // Best effort. The UI still remains usable if the file is locked.
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public static void WriteEmergency(string source, string message, Exception? exception = null)
    {
        try
        {
            Directory.CreateDirectory(LogDirectoryPath);
            var line = FormatLine("ERROR", source, exception is null ? message : message + Environment.NewLine + exception);
            File.AppendAllText(LogFilePath, line, Encoding.UTF8);
        }
        catch
        {
            // A fatal-error logger must not throw another exception.
        }
    }

    private void Write(string level, string source, string message)
    {
        lock (_lock)
        {
            try
            {
                Directory.CreateDirectory(LogDirectoryPath);
                TrimIfNeeded();
                File.AppendAllText(LogFilePath, FormatLine(level, source, message), Encoding.UTF8);
            }
            catch
            {
                // Best effort only.
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void TrimIfNeeded()
    {
        if (!File.Exists(LogFilePath))
            return;

        var fileInfo = new FileInfo(LogFilePath);
        if (fileInfo.Length <= MaximumLogFileBytes)
            return;

        // Keep the newest half of the existing lines to prevent unbounded growth.
        var lines = File.ReadAllLines(LogFilePath);
        var keepCount = Math.Max(1, lines.Length / 2);
        File.WriteAllLines(LogFilePath, lines.Skip(lines.Length - keepCount));
    }

    private static string FormatLine(string level, string source, string message)
    {
        var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
        var normalized = (message ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var builder = new StringBuilder();
        builder.Append('[').Append(timestamp).Append("] [").Append(level).Append("] [").Append(source).Append("] ");
        builder.AppendLine(lines.FirstOrDefault() ?? string.Empty);
        foreach (var line in lines.Skip(1))
            builder.Append("    ").AppendLine(line);
        return builder.ToString();
    }
}
