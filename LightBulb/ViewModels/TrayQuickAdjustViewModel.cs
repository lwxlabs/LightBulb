using System;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightBulb.Framework;
using LightBulb.Localization;
using LightBulb.ViewModels.Components;

namespace LightBulb.ViewModels;

public partial class TrayQuickAdjustViewModel : ViewModelBase
{
    private readonly DashboardViewModel _dashboard;

    public TrayQuickAdjustViewModel(
        DashboardViewModel dashboard,
        LocalizationManager localizationManager
    )
    {
        _dashboard = dashboard;
        LocalizationManager = localizationManager;
        _dashboard.PropertyChanged += Dashboard_OnPropertyChanged;
    }

    public LocalizationManager LocalizationManager { get; }

    public string ActiveProfileName => _dashboard.ActiveProfileName;

    public double MinimumTemperature => Math.Min(2500, Temperature);
    public double MaximumTemperature => Math.Max(6600, Temperature);

    public double Temperature
    {
        get => _dashboard.TargetConfiguration.Temperature;
        set
        {
            _dashboard.SetEffectiveTemperature(value);
            NotifyValuesChanged();
        }
    }

    public double Brightness
    {
        get => _dashboard.TargetConfiguration.Brightness;
        set
        {
            _dashboard.SetEffectiveBrightness(value);
            NotifyValuesChanged();
        }
    }

    public string TemperatureText => $"{Temperature:0} K";
    public string BrightnessText => Brightness.ToString("P0");

    [RelayCommand]
    private void Reset()
    {
        _dashboard.ResetToCurrentProfile();
        _dashboard.RefreshTrayQuickControls();
        NotifyValuesChanged();
    }

    private void Dashboard_OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(DashboardViewModel.TemperatureOffset)
            or nameof(DashboardViewModel.BrightnessOffset)
            or nameof(DashboardViewModel.TargetConfiguration)
            or nameof(DashboardViewModel.Instant)
            or nameof(DashboardViewModel.IsEnabled)
            or "")
        {
            NotifyValuesChanged();
        }

        if (args.PropertyName is nameof(DashboardViewModel.ActiveProfileName)
            or nameof(DashboardViewModel.ActiveProfile)
            or "")
        {
            OnPropertyChanged(nameof(ActiveProfileName));
        }
    }

    private void NotifyValuesChanged()
    {
        OnPropertyChanged(nameof(Temperature));
        OnPropertyChanged(nameof(Brightness));
        OnPropertyChanged(nameof(TemperatureText));
        OnPropertyChanged(nameof(BrightnessText));
        OnPropertyChanged(nameof(MinimumTemperature));
        OnPropertyChanged(nameof(MaximumTemperature));
        OnPropertyChanged(nameof(ActiveProfileName));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _dashboard.PropertyChanged -= Dashboard_OnPropertyChanged;

        base.Dispose(disposing);
    }
}
