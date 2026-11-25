using System;
using System.Collections.Generic;
using System.ComponentModel;
using LightBulb.Localization;
using LightBulb.Models;
using LightBulb.Services;
using LightBulb.ViewModels.Components;

namespace LightBulb.ViewModels.Components.Settings;

public class AdvancedSettingsTabViewModel : SettingsTabViewModelBase
{
    private readonly DashboardViewModel _dashboard;

    public AdvancedSettingsTabViewModel(
        SettingsService settingsService,
        LocalizationManager localizationManager,
        DashboardViewModel dashboard
    ) : base(settingsService, localizationManager, 6)
    {
        _dashboard = dashboard;
        _dashboard.PropertyChanged += Dashboard_OnPropertyChanged;
    }

    public override string DisplayName => LocalizationManager.AdvancedTabName;

    public IReadOnlyList<Language> AvailableLanguages { get; } = Enum.GetValues<Language>();

    public IReadOnlyList<TrayWheelModifier> AvailableTrayWheelModifiers { get; } =
        Enum.GetValues<TrayWheelModifier>();

    public IReadOnlyList<TrayLeftClickAction> AvailableTrayLeftClickActions { get; } =
        Enum.GetValues<TrayLeftClickAction>();

    public TrayLeftClickAction TrayLeftClickAction
    {
        get => SettingsService.TrayLeftClickAction;
        set => SettingsService.TrayLeftClickAction = value;
    }

    public Language Language
    {
        get => SettingsService.Language;
        set => SettingsService.Language = value;
    }

    /// <summary>
    /// Runtime master switch. This is deliberately the exact Dashboard state used by the tray item
    /// and Toggle LightBulb shortcut, not a second persisted flag that could drift out of sync.
    /// </summary>
    public bool IsLightBulbEnabled
    {
        get => _dashboard.IsEnabled;
        set
        {
            if (_dashboard.IsEnabled != value)
                _dashboard.SetLightBulbEnabled(value);
        }
    }

    public bool IsAutoStartEnabled
    {
        get => SettingsService.IsAutoStartEnabled;
        set => SettingsService.IsAutoStartEnabled = value;
    }

    public bool IsStartMinimizedEnabled
    {
        get => SettingsService.IsStartMinimizedEnabled;
        set => SettingsService.IsStartMinimizedEnabled = value;
    }

    public bool IsDefaultToDayConfigurationEnabled
    {
        get => SettingsService.IsDefaultToDayConfigurationEnabled;
        set => SettingsService.IsDefaultToDayConfigurationEnabled = value;
    }

    public bool IsDayNightCycleEnabled
    {
        get => SettingsService.IsDayNightCycleEnabled;
        set => SettingsService.IsDayNightCycleEnabled = value;
    }

    public bool IsConfigurationSmoothingEnabled
    {
        get => SettingsService.IsConfigurationSmoothingEnabled;
        set => SettingsService.IsConfigurationSmoothingEnabled = value;
    }

    public bool IsPauseWhenFullScreenEnabled
    {
        get => SettingsService.IsPauseWhenFullScreenEnabled;
        set => SettingsService.IsPauseWhenFullScreenEnabled = value;
    }

    public bool IsGammaPollingEnabled
    {
        get => SettingsService.IsGammaPollingEnabled;
        set => SettingsService.IsGammaPollingEnabled = value;
    }

    public double TrayTemperatureWheelStep
    {
        get => SettingsService.TrayTemperatureWheelStep;
        set => SettingsService.TrayTemperatureWheelStep = Math.Clamp(value, 25, 1000);
    }

    public double TrayBrightnessWheelStep
    {
        get => SettingsService.TrayBrightnessWheelStep;
        set => SettingsService.TrayBrightnessWheelStep = Math.Clamp(value, 0.01, 0.25);
    }

    public TrayWheelModifier TrayColorWheelModifier
    {
        get => SettingsService.TrayColorWheelModifier;
        set
        {
            if (SettingsService.TrayBrightnessWheelModifier == value)
                SettingsService.TrayBrightnessWheelModifier = GetAlternativeTrayWheelModifier(value);

            SettingsService.TrayColorWheelModifier = value;
        }
    }

    public TrayWheelModifier TrayBrightnessWheelModifier
    {
        get => SettingsService.TrayBrightnessWheelModifier;
        set
        {
            if (SettingsService.TrayColorWheelModifier == value)
                SettingsService.TrayColorWheelModifier = GetAlternativeTrayWheelModifier(value);

            SettingsService.TrayBrightnessWheelModifier = value;
        }
    }

    private static TrayWheelModifier GetAlternativeTrayWheelModifier(TrayWheelModifier excluded)
    {
        foreach (var candidate in new[]
        {
            TrayWheelModifier.Control,
            TrayWheelModifier.Shift,
            TrayWheelModifier.Alt,
            TrayWheelModifier.Windows,
        })
        {
            if (candidate != excluded)
                return candidate;
        }

        return TrayWheelModifier.Control;
    }

    private void Dashboard_OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(DashboardViewModel.IsEnabled) or "")
            OnPropertyChanged(nameof(IsLightBulbEnabled));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _dashboard.PropertyChanged -= Dashboard_OnPropertyChanged;

        base.Dispose(disposing);
    }
}
