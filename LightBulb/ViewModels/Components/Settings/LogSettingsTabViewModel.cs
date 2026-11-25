using System;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using LightBulb.Localization;
using LightBulb.Services;
using LightBulb.Utils.Extensions;

namespace LightBulb.ViewModels.Components.Settings;

public partial class LogSettingsTabViewModel : SettingsTabViewModelBase
{
    private readonly AppLogService _logService;

    public LogSettingsTabViewModel(
        SettingsService settingsService,
        LocalizationManager localizationManager,
        AppLogService logService
    ) : base(settingsService, localizationManager, 7)
    {
        _logService = logService;
        _logService.Changed += LogService_OnChanged;
    }

    public override string DisplayName => LocalizationManager.LogTabName;

    public string LogText => _logService.GetText();

    public string LogPath => AppLogService.LogFilePath;

    [RelayCommand]
    private void ClearLog()
    {
        _logService.Clear();
        OnPropertyChanged(nameof(LogText));
    }

    [RelayCommand]
    private async Task CopyLogAsync()
    {
        var clipboard = App.Current?.ApplicationLifetime?.TryGetMainWindow()?.Clipboard;
        if (clipboard is null)
            return;

        await clipboard.SetTextAsync(LogText);
    }

    [RelayCommand]
    private void RefreshLog() => OnPropertyChanged(nameof(LogText));

    private void LogService_OnChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
            OnPropertyChanged(nameof(LogText));
        else
            Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(LogText)));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _logService.Changed -= LogService_OnChanged;

        base.Dispose(disposing);
    }
}
