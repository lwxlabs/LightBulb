using System;
using LightBulb.Localization;
using LightBulb.Models;
using LightBulb.Services;

namespace LightBulb.ViewModels.Components.Settings;

public class HotKeySettingsTabViewModel : SettingsTabViewModelBase
{
    private readonly HotKeyService _hotKeyService;

    public HotKeySettingsTabViewModel(
        SettingsService settingsService,
        LocalizationManager localizationManager,
        HotKeyService hotKeyService
    )
        : base(settingsService, localizationManager, 4)
    {
        _hotKeyService = hotKeyService;
        _hotKeyService.RegistrationStatusChanged += HotKeyService_OnRegistrationStatusChanged;
    }

    public override string DisplayName => LocalizationManager.HotkeysTabName;

    public string InputHint => LocalizationManager.HotKeyInputHint;

    public bool HasRegistrationError =>
        !string.IsNullOrWhiteSpace(_hotKeyService.RegistrationError);

    public string RegistrationErrorText =>
        HasRegistrationError
            ? LocalizationManager.HotKeyRegistrationError
                + Environment.NewLine
                + _hotKeyService.RegistrationError
            : string.Empty;

    public bool HasConflictWarning =>
        !string.IsNullOrWhiteSpace(_hotKeyService.ConflictWarning);

    public string ConflictWarningText =>
        HasConflictWarning
            ? LocalizationManager.HotKeyConflictWarning
                + Environment.NewLine
                + _hotKeyService.ConflictWarning
            : string.Empty;

    public HotKey ToggleHotKey
    {
        get => SettingsService.ToggleHotKey;
        set => SettingsService.ToggleHotKey = value;
    }

    public HotKey ToggleWindowHotKey
    {
        get => SettingsService.ToggleWindowHotKey;
        set => SettingsService.ToggleWindowHotKey = value;
    }

    public HotKey IncreaseTemperatureOffsetHotKey
    {
        get => SettingsService.IncreaseTemperatureOffsetHotKey;
        set => SettingsService.IncreaseTemperatureOffsetHotKey = value;
    }

    public HotKey DecreaseTemperatureOffsetHotKey
    {
        get => SettingsService.DecreaseTemperatureOffsetHotKey;
        set => SettingsService.DecreaseTemperatureOffsetHotKey = value;
    }

    public HotKey IncreaseBrightnessOffsetHotKey
    {
        get => SettingsService.IncreaseBrightnessOffsetHotKey;
        set => SettingsService.IncreaseBrightnessOffsetHotKey = value;
    }

    public HotKey DecreaseBrightnessOffsetHotKey
    {
        get => SettingsService.DecreaseBrightnessOffsetHotKey;
        set => SettingsService.DecreaseBrightnessOffsetHotKey = value;
    }

    public HotKey ResetConfigurationOffsetHotKey
    {
        get => SettingsService.ResetConfigurationOffsetHotKey;
        set => SettingsService.ResetConfigurationOffsetHotKey = value;
    }

    public HotKey CycleProfilesHotKey
    {
        get => SettingsService.CycleProfilesHotKey;
        set => SettingsService.CycleProfilesHotKey = value;
    }

    public HotKey ToggleLastProfileHotKey
    {
        get => SettingsService.ToggleLastProfileHotKey;
        set => SettingsService.ToggleLastProfileHotKey = value;
    }

    private void HotKeyService_OnRegistrationStatusChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(HasRegistrationError));
        OnPropertyChanged(nameof(RegistrationErrorText));
        OnPropertyChanged(nameof(HasConflictWarning));
        OnPropertyChanged(nameof(ConflictWarningText));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _hotKeyService.RegistrationStatusChanged -= HotKeyService_OnRegistrationStatusChanged;

        base.Dispose(disposing);
    }
}
