using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LightBulb.Framework;
using LightBulb.Localization;
using LightBulb.Models;
using LightBulb.Services;
using LightBulb.Utils.Extensions;
using LightBulb.ViewModels;
using LightBulb.ViewModels.Components;
using LightBulb.ViewModels.Components.Settings;
using LightBulb.ViewModels.Dialogs;
using LightBulb.Views;
using Microsoft.Extensions.DependencyInjection;

namespace LightBulb;

public partial class App : Application, IDisposable
{
    public static new App? Current => Application.Current as App;

    private readonly ServiceProvider _services;
    private readonly SettingsService _settingsService;
    private readonly MainViewModel _mainViewModel;
    private readonly TrayWheelService _trayWheelService;
    private readonly AppLogService _logService;
    private Window? _quickAdjustWindow;
    private TrayQuickAdjustViewModel? _quickAdjustViewModel;

    private bool _isDisposed;

    public App()
    {
        var services = new ServiceCollection();

        // Framework
        services.AddSingleton<DialogManager>();
        services.AddSingleton<ViewManager>();
        services.AddSingleton<ViewModelManager>();

        // Services
        services.AddSingleton<AppLogService>();
        services.AddSingleton<ExternalApplicationService>();
        services.AddSingleton<LocalizationManager>();
        services.AddSingleton<GammaService>();
        services.AddSingleton<HotKeyService>();
        services.AddSingleton<StartupService>();
        services.AddSingleton<SettingsService>();
        services.AddSingleton<TrayWheelService>();

        // View models
        services.AddTransient<MainViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddTransient<MessageBoxViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<TrayQuickAdjustViewModel>();
        services.AddTransient<SettingsTabViewModelBase, AdvancedSettingsTabViewModel>();
        services.AddTransient<SettingsTabViewModelBase, ApplicationWhitelistSettingsTabViewModel>();
        services.AddTransient<SettingsTabViewModelBase, GeneralSettingsTabViewModel>();
        services.AddTransient<SettingsTabViewModelBase, ProfileSettingsTabViewModel>();
        services.AddTransient<SettingsTabViewModelBase, ToneProfileSettingsTabViewModel>();
        services.AddTransient<SettingsTabViewModelBase, HotKeySettingsTabViewModel>();
        services.AddTransient<SettingsTabViewModelBase, LocationSettingsTabViewModel>();
        services.AddTransient<SettingsTabViewModelBase, LogSettingsTabViewModel>();

        _services = services.BuildServiceProvider(true);
        _settingsService = _services.GetRequiredService<SettingsService>();
        _settingsService.AttachStartupService(_services.GetRequiredService<StartupService>());
        _mainViewModel = _services.GetRequiredService<ViewModelManager>().GetMainViewModel();
        _trayWheelService = _services.GetRequiredService<TrayWheelService>();
        _logService = _services.GetRequiredService<AppLogService>();

        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_OnUnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_OnUnobservedTaskException;

    }

    public override void Initialize()
    {
        base.Initialize();

        AvaloniaXamlLoader.Load(this);

        // TrayIcon bindings resolve through the application's DataContext
        DataContext = _mainViewModel;
    }

    // The fork is intentionally fixed to a dark Material theme in App.axaml. Do not mutate
    // MaterialThemeBase.CurrentTheme at runtime: 0.08 and 0.10 logs proved that the asynchronous
    // Material theme updater can race on its "Default" dictionary key and surface an unobserved
    // KeyNotFoundException from StartUpdatingTheme.

    public override void OnFrameworkInitializationCompleted()
    {
        // Load settings
        _settingsService.Load();
        _logService.Info(nameof(App), $"Starting {Program.Name} {Program.VersionString}.");

        // Add precise wheel gestures to this application's existing notification-area icon.
        var trayIcons = TrayIcon.GetIcons(this);
        if (trayIcons is { Count: > 0 })
            _trayWheelService.Attach(trayIcons[0]);

        // Initialize the lifetime
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Although `App.Dispose()` is invoked from `Program.Main(...)`, on some platforms
            // it may be called too late in the shutdown lifecycle. Attach an exit
            // handler to ensure timely disposal as a safeguard.
            desktop.Exit += (_, _) => Dispose();

            // Startup visibility: the persisted Start minimized setting
            // participates in startup visibility directly, not only through a command-line flag.
            // --start-hidden remains supported for explicit hidden launches (including Windows startup).
            var startHidden =
                StartOptions.Current.IsInitiallyHidden || _settingsService.IsStartMinimizedEnabled;

            if (!startHidden)
            {
                // Show the main window on startup
                ShowMainWindow();
            }
            else
            {
                // When starting hidden, initialize the backend without showing the UI
                _ = _mainViewModel.Dashboard.InitializeAsync();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    internal Window? ShowMainWindow()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return null;

        // Re-use the existing window if already open
        if (desktop.MainWindow is { } existingWindow)
        {
            existingWindow.ShowActivateFocus();
            _mainViewModel.IsOpen = true;
        }
        // Otherwise, create a new window (the previous one was closed to free resources)
        else
        {
            var viewManager = _services.GetRequiredService<ViewManager>();
            var window = viewManager.TryBindWindow(_mainViewModel);

            window?.Closed += (_, _) => desktop.MainWindow = null;

            desktop.MainWindow = window;

            window?.ShowActivateFocus();

        }

        return desktop.MainWindow;
    }

    internal void ToggleMainWindow()
    {
        var existingWindow = ApplicationLifetime?.TryGetMainWindow();

        if (existingWindow is { IsVisible: true })
        {
            existingWindow.Hide();
            _mainViewModel.IsOpen = false;
        }
        else
        {
            ShowMainWindow();
        }
    }

    private void TrayIcon_OnClicked(object? sender, EventArgs args)
    {
        switch (_settingsService.TrayLeftClickAction)
        {
            case TrayLeftClickAction.ShowTrayMenu:
                // Avalonia Win32 exposes right-click menu display only internally. TrayWheelService
                // posts the same documented callback message that Avalonia's TrayIconImpl receives
                // from Shell_NotifyIcon, which opens the exact existing NativeMenu.
                if (!_trayWheelService.ShowTrayMenu())
                    ShowMainWindow();
                break;

            case TrayLeftClickAction.ToggleLightBulb:
                _mainViewModel.Dashboard.SetLightBulbEnabled(!_mainViewModel.Dashboard.IsEnabled);
                _settingsService.Save();
                break;

            case TrayLeftClickAction.CycleProfiles:
                _mainViewModel.Dashboard.CycleProfiles();
                _settingsService.Save();
                break;

            case TrayLeftClickAction.ShowAppWindow:
            default:
                ToggleMainWindow();
                break;
        }
    }

    private void TrayMenu_OnNeedsUpdate(object? sender, EventArgs args) =>
        _mainViewModel.Dashboard.RefreshTrayQuickControls();

    private void TrayToggleWindowMenuItem_OnClick(object? sender, EventArgs args) =>
        ToggleMainWindow();

    private async void TrayShowSettingsMenuItem_OnClick(object? sender, EventArgs args)
    {
        // Settings owns a dedicated window in the fork. It can be opened/focused directly from the
        // tray even while the dashboard is hidden.
        await _mainViewModel.ShowSettingsCommand.ExecuteAsync(null);
    }

    private void TrayCoolerMenuItem_OnClick(object? sender, EventArgs args) =>
        _mainViewModel.Dashboard.AdjustTemperatureOffset(_settingsService.TrayTemperatureWheelStep);

    private void TrayWarmerMenuItem_OnClick(object? sender, EventArgs args) =>
        _mainViewModel.Dashboard.AdjustTemperatureOffset(-_settingsService.TrayTemperatureWheelStep);

    private void TrayBrighterMenuItem_OnClick(object? sender, EventArgs args) =>
        _mainViewModel.Dashboard.AdjustBrightnessOffset(_settingsService.TrayBrightnessWheelStep);

    private void TrayDimmerMenuItem_OnClick(object? sender, EventArgs args) =>
        _mainViewModel.Dashboard.AdjustBrightnessOffset(-_settingsService.TrayBrightnessWheelStep);

    private void TraySaveCurrentAsDayMenuItem_OnClick(object? sender, EventArgs args) =>
        _mainViewModel.Dashboard.SaveCurrentLookAsDayConfiguration();

    private void TraySaveCurrentAsNightMenuItem_OnClick(object? sender, EventArgs args) =>
        _mainViewModel.Dashboard.SaveCurrentLookAsNightConfiguration();

    private void TraySaveCurrentToProfileMenuItem_OnClick(object? sender, EventArgs args) =>
        _mainViewModel.Dashboard.SaveCurrentLookToActiveProfile();

    private void TrayTemperatureControlMenuItem_OnClick(object? sender, EventArgs args) =>
        ShowTrayQuickAdjustWindow();

    private void TrayBrightnessControlMenuItem_OnClick(object? sender, EventArgs args) =>
        ShowTrayQuickAdjustWindow();

    private void ShowTrayQuickAdjustWindow()
    {
        if (_quickAdjustWindow is { } existingWindow)
        {
            if (!existingWindow.IsVisible)
                existingWindow.Show();
            existingWindow.Activate();
            existingWindow.Focus();
            return;
        }

        var viewModel = _services.GetRequiredService<TrayQuickAdjustViewModel>();
        var window = new TrayQuickAdjustWindow(viewModel)
        {
            Title = $"{Program.Name} — {viewModel.LocalizationManager.QuickAdjustTitle}",
        };

        _quickAdjustViewModel = viewModel;
        _quickAdjustWindow = window;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_quickAdjustWindow, window))
            {
                _quickAdjustWindow = null;
                _quickAdjustViewModel?.Dispose();
                _quickAdjustViewModel = null;
            }
        };

        window.Show();
        window.Activate();
    }

    private void TrayToneOffMenuItem_OnClick(object? sender, EventArgs args)
    {
        _mainViewModel.Dashboard.ToggleToneLayer();
        _settingsService.Save();
    }

    private void TrayToneDarkMenuItem_OnClick(object? sender, EventArgs args) =>
        SetTrayToneTarget(LightBulb.Core.ToneBand.DarkTones);

    private void TrayToneMidtonesMenuItem_OnClick(object? sender, EventArgs args) =>
        SetTrayToneTarget(LightBulb.Core.ToneBand.Midtones);

    private void TrayToneHighlightsMenuItem_OnClick(object? sender, EventArgs args) =>
        SetTrayToneTarget(LightBulb.Core.ToneBand.Highlights);

    private void TrayToneAllMenuItem_OnClick(object? sender, EventArgs args) =>
        SetTrayToneTarget(LightBulb.Core.ToneBand.AllToneBands);

    private void SetTrayToneTarget(LightBulb.Core.ToneBand band)
    {
        _mainViewModel.Dashboard.SetSelectedToneBand(band);
        _settingsService.Save();
    }

    private void TrayToneResetMenuItem_OnClick(object? sender, EventArgs args)
    {
        _mainViewModel.Dashboard.ResetToneAdjustments();
        _settingsService.Save();
    }

    private async void TrayToneSaveNewMenuItem_OnClick(object? sender, EventArgs args)
    {
        _mainViewModel.Dashboard.SaveCurrentToneAsNewProfile();
        _settingsService.Save();
        await _mainViewModel.ShowSettingsTabAsync<ToneProfileSettingsTabViewModel>();
    }

    private void TrayResetProfileMenuItem_OnClick(object? sender, EventArgs args) =>
        _mainViewModel.Dashboard.ResetToCurrentProfile();

    private void TrayExitMenuItem_OnClick(object? sender, EventArgs args) => Shutdown();

    private void CurrentDomain_OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        if (args.ExceptionObject is Exception exception)
            _logService.Error(nameof(App), "Unhandled application exception.", exception);
        else
            _logService.Error(nameof(App), $"Unhandled application exception object: {args.ExceptionObject}");
    }

    private void TaskScheduler_OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        _logService.Error(nameof(App), "Unobserved task exception.", args.Exception);
        args.SetObserved();
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_OnUnhandledException;
        TaskScheduler.UnobservedTaskException -= TaskScheduler_OnUnobservedTaskException;

        _logService.Info(nameof(App), "Application shutdown.");

        if (_quickAdjustWindow is not null)
        {
            _quickAdjustWindow.Close();
            _quickAdjustWindow = null;
        }
        _quickAdjustViewModel?.Dispose();
        _quickAdjustViewModel = null;

        _services.Dispose();
    }
}

public partial class App
{
    public static void Shutdown(int exitCode = 0)
    {
        if (Current?.ApplicationLifetime?.TryShutdown(exitCode) != true)
            Environment.Exit(exitCode);
    }
}
