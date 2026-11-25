using System;
using System.IO;
using System.Reflection;
using System.Threading;
using Avalonia;
using LightBulb.PlatformInterop;

namespace LightBulb;

public static class Program
{
    private static Assembly Assembly { get; } = Assembly.GetExecutingAssembly();

    public static string Name { get; } = Assembly.GetName().Name ?? "LightBulb";

    private static string ExitRequestEventName => $@"Local\{Name}_ExitRequest";

    public static Version Version { get; } = Assembly.GetName().Version ?? new Version(0, 0, 0);

    public static string VersionString { get; } =
        Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Version.ToString(3);

    public static string ExecutableDirPath { get; } = AppContext.BaseDirectory;

    public static string ExecutableFilePath { get; } =
        Environment.ProcessPath
        ?? Path.ChangeExtension(Assembly.Location, "exe");

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .With(
                new Win32PlatformOptions
                {
                    // Use redirection surface composition to avoid Avalonia's WinUI Composition
                    // renderer (WinUiCompositorConnection) from ticking endlessly via dcomp.dll
                    // when the application is idle, which would otherwise wake up dwm.exe
                    // continuously even when monitors are powered off.
                    CompositionMode = [Win32CompositionMode.RedirectionSurface],
                    RenderingMode = [Win32RenderingMode.AngleEgl, Win32RenderingMode.Software],
                }
            )
            .LogToTrace();

    [STAThread]
    public static int Main(string[] args)
    {
        // Installer/control path: signal the already-running instance before the single-instance
        // mutex check. This gives upgrades a graceful exit path even though the desktop lifetime
        // intentionally uses ShutdownMode.OnExplicitShutdown.
        if (
            Array.Exists(
                args,
                argument => string.Equals(
                    argument,
                    StartOptions.ExitExistingArgument,
                    StringComparison.OrdinalIgnoreCase
                )
            )
        )
        {
            SignalExistingInstanceToExit();
            return 0;
        }

        // Ensure only one instance of the app is running at a time.
        using var identityMutex = new Mutex(
            true,
            $"{Name}_Identity",
            out var isOnlyRunningInstance
        );

        if (!isOnlyRunningInstance)
            return 1;

        EventWaitHandle? exitRequestEvent = null;
        RegisteredWaitHandle? exitRequestRegistration = null;
        if (OperatingSystem.IsWindows())
        {
            exitRequestEvent = new EventWaitHandle(
                false,
                EventResetMode.AutoReset,
                ExitRequestEventName
            );
            exitRequestRegistration = ThreadPool.RegisterWaitForSingleObject(
                exitRequestEvent,
                (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => App.Shutdown()),
                null,
                Timeout.Infinite,
                executeOnlyOnce: false
            );
        }

        // Build and run the app.
        var builder = BuildAvaloniaApp();

        try
        {
            return builder.StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Services.AppLogService.WriteEmergency(nameof(Program), "Fatal application error.", ex);
            MessageBox.ShowError("Fatal Error", ex.ToString());
            throw;
        }
        finally
        {
            exitRequestRegistration?.Unregister(null);
            exitRequestEvent?.Dispose();

            // Clean up after application shutdown.
            if (builder.Instance is IDisposable disposableApp)
                disposableApp.Dispose();
        }
    }

    private static void SignalExistingInstanceToExit()
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            using var exitRequestEvent = EventWaitHandle.OpenExisting(ExitRequestEventName);
            exitRequestEvent.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // No running 0.20+ instance is listening. The installer has a process-kill fallback for
            // older 0.19 instances and crashed/stuck processes.
        }
        catch (UnauthorizedAccessException)
        {
            // Elevated/non-elevated object access can differ. The installer fallback handles this.
        }
    }
}
