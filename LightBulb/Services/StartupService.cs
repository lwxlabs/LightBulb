using System;
using Microsoft.Win32;

namespace LightBulb.Services;

/// <summary>
/// Per-user Windows startup registration through HKCU Run. Uses
/// Environment.ProcessPath so the registered command always points at the actual host executable.
/// </summary>
public sealed class StartupService(AppLogService logService)
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "LightBulb";

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(AppName) is string value && !string.IsNullOrWhiteSpace(value);
        }
        catch (Exception ex)
        {
            logService.Warning(nameof(StartupService), $"Could not read Windows startup registration: {ex.Message}");
            return false;
        }
    }

    public void SetEnabled(bool enabled, bool startMinimized)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key is null)
            {
                logService.Warning(nameof(StartupService), "Windows startup registry key could not be opened.");
                return;
            }

            if (!enabled)
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
                logService.Info(nameof(StartupService), "Windows startup registration disabled.");
                return;
            }

            var executable = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executable))
                executable = Program.ExecutableFilePath;

            if (string.IsNullOrWhiteSpace(executable))
            {
                logService.Warning(nameof(StartupService), "Windows startup registration skipped because the executable path is unavailable.");
                return;
            }

            var command = $"\"{executable}\"";
            if (startMinimized)
                command += $" {StartOptions.IsInitiallyHiddenArgument}";

            key.SetValue(AppName, command, RegistryValueKind.String);
            logService.Info(
                nameof(StartupService),
                $"Windows startup registration enabled{(startMinimized ? " (start minimized)" : string.Empty)}."
            );
        }
        catch (Exception ex)
        {
            logService.Error(nameof(StartupService), "Could not update Windows startup registration.", ex);
        }
    }
}
