using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightBulb.Core;
using LightBulb.Core.Utils.Extensions;
using LightBulb.Framework;
using LightBulb.Localization;
using LightBulb.Models;
using LightBulb.PlatformInterop;
using LightBulb.Services;
using LightBulb.Utils.Extensions;
using PowerKit;
using PowerKit.Extensions;

namespace LightBulb.ViewModels.Components;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly GammaService _gammaService;
    private readonly HotKeyService _hotKeyService;
    private readonly ExternalApplicationService _externalApplicationService;

    private readonly IDisposable _eventSubscription;

    private readonly Timer _updateInstantTimer;
    private readonly Timer _updateConfigurationTimer;
    private readonly Timer _updateIsPausedTimer;

    private IDisposable? _enableAfterDelayRegistration;
    private IDisposable? _persistManualAdjustmentsRegistration;
    private IDisposable? _persistToneAdjustmentsRegistration;
    private bool _isRestoringManualAdjustments;
    private ColorConfiguration? _configurationSmoothingSource;
    private ColorConfiguration? _configurationSmoothingTarget;
    private string _registeredProfileHotKeySignature = string.Empty;

    public DashboardViewModel(
        SettingsService settingsService,
        LocalizationManager localizationManager,
        GammaService gammaService,
        HotKeyService hotKeyService,
        ExternalApplicationService externalApplicationService
    )
    {
        _settingsService = settingsService;
        LocalizationManager = localizationManager;
        _gammaService = gammaService;
        _hotKeyService = hotKeyService;
        _externalApplicationService = externalApplicationService;

        _eventSubscription = Disposable.Merge(
            this.WatchProperty(
                o => o.IsEnabled,
                v =>
                {
                    if (v)
                    {
                        // Cancel any activate 'disable temporarily' timers
                        _enableAfterDelayRegistration?.Dispose();

                        // Invalidate device contexts
                        _gammaService.InvalidateDeviceContexts();
                    }
                }
            ),
            // Refresh transition tooltips when the language changes
            localizationManager.WatchProperty(
                o => o.Language,
                _ =>
                {
                    OnPropertyChanged(nameof(SunsetTransitionTooltip));
                    OnPropertyChanged(nameof(SunriseTransitionTooltip));
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(TrayProfilesMenuText));
                    OnPropertyChanged(nameof(TrayToneProfilesMenuText));
                    OnPropertyChanged(nameof(ActiveProfileTooltip));
                    OnPropertyChanged(nameof(ActiveToneProfileTooltip));
                    OnPropertyChanged(nameof(TrayTonesMenuText));
                    OnPropertyChanged(nameof(TrayWheelHintText));
                    RefreshTrayQuickControls();
                }
            ),
            // Re-register hotkeys when they get updated
            settingsService.WatchProperties(
                [
                    o => o.ToggleHotKey,
                    o => o.ToggleWindowHotKey,
                    o => o.IncreaseTemperatureOffsetHotKey,
                    o => o.DecreaseTemperatureOffsetHotKey,
                    o => o.IncreaseBrightnessOffsetHotKey,
                    o => o.DecreaseBrightnessOffsetHotKey,
                    o => o.ResetConfigurationOffsetHotKey,
                    o => o.CycleProfilesHotKey,
                    o => o.ToggleLastProfileHotKey,
                ],
                RegisterHotKeys
            ),
            settingsService.WatchProperty(
                o => o.Profiles,
                _ =>
                {
                    RefreshProfileState();
                    RegisterHotKeysIfProfileBindingsChanged();
                }
            ),
            settingsService.WatchProperty(o => o.ActiveProfileId, _ => RefreshProfileState()),
            settingsService.WatchProperty(o => o.LastProfileId, _ => RefreshProfileState()),
            settingsService.WatchProperty(o => o.ToneProfiles, _ => RefreshToneState()),
            settingsService.WatchProperty(o => o.ActiveToneProfileId, _ => RefreshToneState()),
            settingsService.WatchProperty(o => o.LastToneProfileId, _ => RefreshToneState()),
            settingsService.WatchProperty(o => o.IsToneLayerEnabled, _ => RefreshToneState()),
            settingsService.WatchProperty(o => o.ToneAdjustments, _ => RefreshToneState()),
            settingsService.WatchProperty(o => o.SelectedToneBand, _ => RefreshToneTargetState()),
            settingsService.WatchProperty(
                o => o.TrayColorWheelModifier,
                _ => OnPropertyChanged(nameof(TrayWheelHintText))
            ),
            settingsService.WatchProperty(
                o => o.TrayBrightnessWheelModifier,
                _ => OnPropertyChanged(nameof(TrayWheelHintText))
            ),
            settingsService.WatchProperty(
                o => o.IsDayNightCycleEnabled,
                _ =>
                {
                    OnPropertyChanged(nameof(TargetConfiguration));
                    OnPropertyChanged(nameof(CycleState));
                    OnPropertyChanged(nameof(StatusText));
                    RefreshTrayQuickControls();
                }
            )
        );

        _updateConfigurationTimer = new Timer(
            TimeSpan.FromMilliseconds(50),
            () => Dispatcher.UIThread.Post(UpdateConfiguration)
        );
        _updateInstantTimer = new Timer(
            TimeSpan.FromMilliseconds(50),
            () => Dispatcher.UIThread.Post(UpdateInstant)
        );
        _updateIsPausedTimer = new Timer(
            TimeSpan.FromSeconds(1),
            () => Dispatcher.UIThread.Post(UpdateIsPaused)
        );
    }

    public LocalizationManager LocalizationManager { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial bool IsEnabled { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial bool IsPaused { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial bool IsCyclePreviewEnabled { get; set; }

    public bool IsActive => IsEnabled && !IsPaused || IsCyclePreviewEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SolarTimes))]
    [NotifyPropertyChangedFor(nameof(SunriseStart))]
    [NotifyPropertyChangedFor(nameof(SunriseEnd))]
    [NotifyPropertyChangedFor(nameof(SunsetStart))]
    [NotifyPropertyChangedFor(nameof(SunsetEnd))]
    [NotifyPropertyChangedFor(nameof(SunriseTransitionTooltip))]
    [NotifyPropertyChangedFor(nameof(SunsetTransitionTooltip))]
    [NotifyPropertyChangedFor(nameof(TargetConfiguration))]
    [NotifyPropertyChangedFor(nameof(CycleState))]
    public partial DateTimeOffset Instant { get; set; } = DateTimeOffset.Now;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOffsetEnabled))]
    [NotifyPropertyChangedFor(nameof(TargetConfiguration))]
    [NotifyPropertyChangedFor(nameof(AdjustedDayConfiguration))]
    [NotifyPropertyChangedFor(nameof(AdjustedNightConfiguration))]
    [NotifyPropertyChangedFor(nameof(CycleState))]
    [NotifyPropertyChangedFor(nameof(IsTemperatureOffsetEnabled))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(TrayTemperatureControlText))]
    [NotifyPropertyChangedFor(nameof(TrayResetControlText))]
    public partial double TemperatureOffset { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOffsetEnabled))]
    [NotifyPropertyChangedFor(nameof(TargetConfiguration))]
    [NotifyPropertyChangedFor(nameof(AdjustedDayConfiguration))]
    [NotifyPropertyChangedFor(nameof(AdjustedNightConfiguration))]
    [NotifyPropertyChangedFor(nameof(CycleState))]
    [NotifyPropertyChangedFor(nameof(IsBrightnessOffsetEnabled))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(TrayBrightnessControlText))]
    [NotifyPropertyChangedFor(nameof(TrayResetControlText))]
    public partial double BrightnessOffset { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial ColorConfiguration CurrentConfiguration { get; set; } =
        ColorConfiguration.Default;

    public IReadOnlyList<DisplayProfile> Profiles => _settingsService.Profiles;

    public bool HasProfiles => Profiles.Count > 0;

    public DisplayProfile? ActiveProfile =>
        _settingsService.ActiveProfileId is { } activeProfileId
            ? Profiles.FirstOrDefault(p => p.Id == activeProfileId)
            : null;

    public string ActiveProfileName =>
        ActiveProfile?.Name ?? LocalizationManager.TrayAutomaticProfileMenuItem;

    public string ActiveProfileTooltip =>
        ActiveProfile is { } profile
            ? profile.Tooltip
            : LocalizationManager.TrayAutomaticProfileTooltip;

    public bool HasActiveProfile => ActiveProfile is not null;

    public DisplayProfile? LastProfile =>
        _settingsService.LastProfileId is { } lastProfileId
            ? Profiles.FirstOrDefault(p => p.Id == lastProfileId)
            : null;

    public bool HasLastProfile => LastProfile is not null;

    public string TrayProfilesMenuText =>
        $"{LocalizationManager.TrayProfilesMenuItem}: {ActiveProfileName}";

    public IReadOnlyList<ToneProfile> ToneProfiles => _settingsService.ToneProfiles;

    public bool HasToneProfiles => ToneProfiles.Count > 0;

    public ToneProfile? ActiveToneProfile =>
        IsToneEnabled && _settingsService.ActiveToneProfileId is { } activeToneProfileId
            ? ToneProfiles.FirstOrDefault(p => p.Id == activeToneProfileId)
            : null;

    public bool HasActiveToneProfile => ActiveToneProfile is not null;

    public bool IsToneEnabled => _settingsService.IsToneLayerEnabled;

    public bool CanEnableToneLayer => IsToneEnabled
        || HasStoredToneAdjustments
        || (_settingsService.LastToneProfileId is { } lastId && ToneProfiles.Any(p => p.Id == lastId));

    public ToneAdjustmentConfiguration ToneAdjustments => _settingsService.ToneAdjustments;

    public bool HasStoredToneAdjustments => ToneAdjustments != ToneAdjustmentConfiguration.Off;

    public bool HasToneAdjustments => IsToneEnabled && HasStoredToneAdjustments;

    public bool IsToneLayerOff => !IsToneEnabled;

    public bool IsToneProfileCustom =>
        ActiveToneProfile is { } profile && profile.Adjustments != ToneAdjustments;

    public string ActiveToneProfileName =>
        !IsToneEnabled
            ? LocalizationManager.TrayToneOffMenuItem
            : ActiveToneProfile?.Name
                ?? (HasStoredToneAdjustments
                    ? LocalizationManager.TrayToneCustomMenuItem
                    : LocalizationManager.TrayToneOffMenuItem);

    public string ActiveToneProfileTooltip =>
        !IsToneEnabled
            ? LocalizationManager.TrayToneProfileOffTooltip
            : ActiveToneProfile is { } profile
            ? profile.Tooltip
            : HasStoredToneAdjustments
                ? LocalizationManager.TrayCustomToneProfileTooltip
                : LocalizationManager.TrayToneProfileOffTooltip;

    public string ToneLayerDisplayName =>
        !IsToneEnabled
            ? LocalizationManager.TrayToneOffMenuItem
            : ActiveToneProfile is { } profile
            ? IsToneProfileCustom
                ? profile.Name + " (custom)"
                : profile.Name
            : HasStoredToneAdjustments
                ? LocalizationManager.TrayToneCustomMenuItem
                : LocalizationManager.TrayToneOffMenuItem;

    public string TrayToneProfilesMenuText =>
        $"{LocalizationManager.TrayToneProfilesMenuItem}: {ActiveToneProfileName}";

    public string TrayToneEnableDisableMenuText => IsToneEnabled
        ? LocalizationManager.TrayToneDisableMenuItem
        : LocalizationManager.TrayToneEnableMenuItem;

    public string TrayToneEnableDisableTooltip => IsToneEnabled
        ? LocalizationManager.TrayToneDisableTooltip
        : LocalizationManager.TrayToneEnableTooltip;

    public ToneBand? SelectedToneBand => _settingsService.SelectedToneBand;

    public bool IsToneTargetOff => !IsToneEnabled;
    public bool IsDarkTonesTarget => IsToneEnabled && SelectedToneBand == ToneBand.DarkTones;
    public bool IsMidtonesTarget => IsToneEnabled && SelectedToneBand == ToneBand.Midtones;
    public bool IsHighlightsTarget => IsToneEnabled && SelectedToneBand == ToneBand.Highlights;
    public bool IsAllToneBandsTarget => IsToneEnabled && SelectedToneBand == ToneBand.AllToneBands;

    public string ToneTargetDisplayName => SelectedToneBand switch
    {
        ToneBand.DarkTones => LocalizationManager.ToneDarkTonesLabel,
        ToneBand.Midtones => LocalizationManager.ToneMidtonesLabel,
        ToneBand.Highlights => LocalizationManager.ToneHighlightsLabel,
        ToneBand.AllToneBands => LocalizationManager.ToneAllBandsLabel,
        _ => LocalizationManager.TrayToneOffMenuItem,
    };

    public string TrayTonesMenuText
    {
        get
        {
            if (!IsToneEnabled || SelectedToneBand is null)
                return $"{LocalizationManager.TrayTonesMenuItem}: {LocalizationManager.TrayToneOffMenuItem}";

            return $"{LocalizationManager.TrayTonesMenuItem}: {ToneTargetDisplayName} {SelectedToneIntensityDisplay}";
        }
    }

    private string SelectedToneIntensityDisplay
    {
        get
        {
            if (!IsToneEnabled || SelectedToneBand is not { } band)
                return string.Empty;

            if (band != ToneBand.AllToneBands)
                return Math.Round(ToneAdjustments.GetBand(band).Intensity * 100)
                    .ToString("0", CultureInfo.CurrentCulture) + "%";

            var dark = ToneAdjustments.DarkTones.Intensity;
            var mid = ToneAdjustments.Midtones.Intensity;
            var highlights = ToneAdjustments.Highlights.Intensity;
            return Math.Abs(dark - mid) < 0.0001 && Math.Abs(mid - highlights) < 0.0001
                ? Math.Round(dark * 100).ToString("0", CultureInfo.CurrentCulture) + "%"
                : LocalizationManager.TrayToneMixedValue;
        }
    }

    public string TrayDarkTonesControlText => FormatToneIntensityControlText(
        LocalizationManager.ToneDarkTonesLabel,
        ToneAdjustments.DarkTones.Intensity
    );

    public string TrayMidtonesControlText => FormatToneIntensityControlText(
        LocalizationManager.ToneMidtonesLabel,
        ToneAdjustments.Midtones.Intensity
    );

    public string TrayHighlightsControlText => FormatToneIntensityControlText(
        LocalizationManager.ToneHighlightsLabel,
        ToneAdjustments.Highlights.Intensity
    );

    public string TrayAllToneBandsControlText
    {
        get
        {
            var dark = ToneAdjustments.DarkTones.Intensity;
            var mid = ToneAdjustments.Midtones.Intensity;
            var highlights = ToneAdjustments.Highlights.Intensity;

            return Math.Abs(dark - mid) < 0.0001 && Math.Abs(mid - highlights) < 0.0001
                ? FormatToneIntensityControlText(LocalizationManager.ToneAllBandsLabel, dark)
                : LocalizationManager.ToneAllBandsLabel
                    + " ↕: "
                    + LocalizationManager.TrayToneMixedValue;
        }
    }

    public string TrayToneWheelHintText => LocalizationManager.TrayToneWheelHintMenuItem;

    public string TrayWheelHintText =>
        string.Format(
            CultureInfo.CurrentCulture,
            LocalizationManager.TrayWheelHintMenuItem,
            _settingsService.TrayColorWheelModifier,
            _settingsService.TrayBrightnessWheelModifier
        );

    private ColorConfiguration AutomaticConfiguration =>
        !_settingsService.IsDayNightCycleEnabled
            ? _settingsService.DayConfiguration
            : Cycle.InterpolateConfiguration(
                SolarTimes,
                _settingsService.DayConfiguration,
                _settingsService.NightConfiguration,
                _settingsService.ConfigurationTransitionDuration,
                _settingsService.ConfigurationTransitionOffset,
                Instant
            );

    private ColorConfiguration BaseTargetConfiguration =>
        ActiveProfile is { } profile && !IsCyclePreviewEnabled
            ? new ColorConfiguration(profile.Temperature, profile.Brightness)
            : AutomaticConfiguration;

    public SolarTimes SolarTimes =>
        _settingsService is { IsManualSunriseSunsetEnabled: false, Location: { } location }
            ? SolarTimes.Calculate(location, Instant)
            : new SolarTimes(_settingsService.ManualSunrise, _settingsService.ManualSunset);

    public TimeOnly SunriseStart =>
        Cycle.GetSunriseStart(
            SolarTimes.Sunrise,
            _settingsService.ConfigurationTransitionDuration,
            _settingsService.ConfigurationTransitionOffset
        );

    public TimeOnly SunriseEnd =>
        Cycle.GetSunriseEnd(
            SolarTimes.Sunrise,
            _settingsService.ConfigurationTransitionDuration,
            _settingsService.ConfigurationTransitionOffset
        );

    public TimeOnly SunsetStart =>
        Cycle.GetSunsetStart(
            SolarTimes.Sunset,
            _settingsService.ConfigurationTransitionDuration,
            _settingsService.ConfigurationTransitionOffset
        );

    public TimeOnly SunsetEnd =>
        Cycle.GetSunsetEnd(
            SolarTimes.Sunset,
            _settingsService.ConfigurationTransitionDuration,
            _settingsService.ConfigurationTransitionOffset
        );

    public string SunsetTransitionTooltip =>
        string.Format(
            LocalizationManager.SunsetTransitionTooltip,
            SunsetStart.ToString(CultureInfo.CurrentCulture),
            SunsetEnd.ToString(CultureInfo.CurrentCulture)
        );

    public string SunriseTransitionTooltip =>
        string.Format(
            LocalizationManager.SunriseTransitionTooltip,
            SunriseStart.ToString(CultureInfo.CurrentCulture),
            SunriseEnd.ToString(CultureInfo.CurrentCulture)
        );

    public bool IsOffsetEnabled => Math.Abs(TemperatureOffset) + Math.Abs(BrightnessOffset) >= 0.01;

    public bool IsTemperatureOffsetEnabled => Math.Abs(TemperatureOffset) >= 0.01;

    public bool IsBrightnessOffsetEnabled => Math.Abs(BrightnessOffset) >= 0.001;

    public string TrayTemperatureControlText
    {
        get
        {
            if (IsToneEnabled && SelectedToneBand is { } toneBand)
            {
                if (toneBand == ToneBand.AllToneBands)
                {
                    var dark = ToneAdjustments.DarkTones.TemperatureOffset;
                    var mid = ToneAdjustments.Midtones.TemperatureOffset;
                    var highlights = ToneAdjustments.Highlights.TemperatureOffset;
                    if (Math.Abs(dark - mid) >= 0.01 || Math.Abs(mid - highlights) >= 0.01)
                    {
                        return string.Format(
                            CultureInfo.CurrentCulture,
                            LocalizationManager.TraySelectedToneTemperatureMixedControlFormat,
                            LocalizationManager.TrayToneMixedValue
                        );
                    }
                }

                var adjustment = toneBand == ToneBand.AllToneBands
                    ? ToneAdjustments.DarkTones
                    : ToneAdjustments.GetBand(toneBand);
                return string.Format(
                    CultureInfo.CurrentCulture,
                    LocalizationManager.TraySelectedToneTemperatureControlFormat,
                    FormatTraySigned(adjustment.TemperatureOffset, 6)
                );
            }

            var baseTemperature = BaseTargetConfiguration.Temperature;
            var effectiveTemperature = TargetConfiguration.Temperature;
            var delta = effectiveTemperature - baseTemperature;
            var percent = baseTemperature > 0 ? delta / baseTemperature * 100 : 0;

            return string.Format(
                CultureInfo.CurrentCulture,
                LocalizationManager.TrayTemperatureControlFormat,
                FormatTrayUnsigned(effectiveTemperature, 5),
                FormatTraySigned(delta, 6),
                FormatTraySigned(percent, 6)
            );
        }
    }

    public string TrayBrightnessControlText
    {
        get
        {
            if (IsToneEnabled && SelectedToneBand is { } toneBand)
            {
                if (toneBand == ToneBand.AllToneBands)
                {
                    var dark = ToneAdjustments.DarkTones.BrightnessOffset;
                    var mid = ToneAdjustments.Midtones.BrightnessOffset;
                    var highlights = ToneAdjustments.Highlights.BrightnessOffset;
                    if (Math.Abs(dark - mid) >= 0.0001 || Math.Abs(mid - highlights) >= 0.0001)
                    {
                        return string.Format(
                            CultureInfo.CurrentCulture,
                            LocalizationManager.TraySelectedToneBrightnessMixedControlFormat,
                            LocalizationManager.TrayToneMixedValue
                        );
                    }
                }

                var adjustment = toneBand == ToneBand.AllToneBands
                    ? ToneAdjustments.DarkTones
                    : ToneAdjustments.GetBand(toneBand);
                return string.Format(
                    CultureInfo.CurrentCulture,
                    LocalizationManager.TraySelectedToneBrightnessControlFormat,
                    FormatTraySigned(adjustment.BrightnessOffset * 100, 4)
                );
            }

            var baseBrightness = BaseTargetConfiguration.Brightness;
            var effectiveBrightness = TargetConfiguration.Brightness;
            var deltaPercent = (effectiveBrightness - baseBrightness) * 100;

            return string.Format(
                CultureInfo.CurrentCulture,
                LocalizationManager.TrayBrightnessControlFormat,
                FormatTrayUnsigned(effectiveBrightness * 100, 3),
                FormatTraySigned(deltaPercent, 4)
            );
        }
    }

    public string TrayResetControlText =>
        LocalizationManager.TrayResetProfileMenuItem
        + " ("
        + FormatTraySigned(TemperatureOffset, 6)
        + " K, "
        + FormatTraySigned(BrightnessOffset * 100, 4)
        + "%)";

    private static string FormatTrayUnsigned(double value, int width) =>
        PadTrayNumericColumn(Math.Round(value).ToString("0", CultureInfo.CurrentCulture), width);

    private static string FormatTraySigned(double value, int width) =>
        PadTrayNumericColumn(Math.Round(value).ToString("+0;-0;0", CultureInfo.CurrentCulture), width);

    private static string PadTrayNumericColumn(string value, int width) =>
        value.Length >= width ? value : new string('\u2007', width - value.Length) + value;

    private static string FormatToneIntensityControlText(string label, double intensity) =>
        label
        + " ↕: "
        + FormatTrayUnsigned(intensity * 100, 3)
        + "%";

    public ColorConfiguration TargetConfiguration =>
        IsActive
            ? BaseTargetConfiguration
                .WithOffset(TemperatureOffset, BrightnessOffset)
                .Clamp(
                    _settingsService.MinimumTemperature,
                    _settingsService.MaximumTemperature,
                    _settingsService.MinimumBrightness,
                    _settingsService.MaximumBrightness
                )
        : _settingsService.IsDefaultToDayConfigurationEnabled ? _settingsService.DayConfiguration
        : ColorConfiguration.Default;

    public ColorConfiguration AdjustedDayConfiguration =>
        _settingsService.DayConfiguration.WithOffset(TemperatureOffset, BrightnessOffset);

    public ColorConfiguration AdjustedNightConfiguration =>
        _settingsService.NightConfiguration.WithOffset(TemperatureOffset, BrightnessOffset);

    public CycleState CycleState =>
        this switch
        {
            _ when CurrentConfiguration != TargetConfiguration => CycleState.Transition,
            _ when !IsEnabled => CycleState.Disabled,
            _ when IsPaused => CycleState.Paused,
            _ when CurrentConfiguration == AdjustedDayConfiguration => CycleState.Day,
            _ when CurrentConfiguration == AdjustedNightConfiguration => CycleState.Night,
            _ => CycleState.Transition,
        };

    public string StatusText =>
        Program.Name
        + " — "
        + (HasActiveProfile && (IsTemperatureOffsetEnabled || IsBrightnessOffsetEnabled)
            ? ActiveProfileName + " (custom)"
            : ActiveProfileName)
        + Environment.NewLine
        + (
            IsActive
                ? CurrentConfiguration.Temperature.ToString("F0")
                    + " / "
                    + CurrentConfiguration.Brightness.ToString("P0")
                : LocalizationManager.TrayTooltipDisabled
        )
        + (
            IsToneEnabled
                ? Environment.NewLine
                    + LocalizationManager.TrayToneStatusPrefix
                    + ": "
                    + ToneLayerDisplayName
                : string.Empty
        );

    private string GetProfileHotKeySignature() =>
        string.Join(
            "|",
            Profiles.Select(profile => $"{profile.Id:N}:{profile.HotKey}")
        );

    private void RegisterHotKeysIfProfileBindingsChanged()
    {
        var signature = GetProfileHotKeySignature();
        if (signature != _registeredProfileHotKeySignature)
            RegisterHotKeys();
    }

    private void RefreshProfileState()
    {
        OnPropertyChanged(nameof(Profiles));
        OnPropertyChanged(nameof(HasProfiles));
        OnPropertyChanged(nameof(ActiveProfile));
        OnPropertyChanged(nameof(ActiveProfileName));
        OnPropertyChanged(nameof(ActiveProfileTooltip));
        OnPropertyChanged(nameof(HasActiveProfile));
        OnPropertyChanged(nameof(LastProfile));
        OnPropertyChanged(nameof(HasLastProfile));
        OnPropertyChanged(nameof(TrayProfilesMenuText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(TargetConfiguration));
        OnPropertyChanged(nameof(CycleState));
        RefreshTrayQuickControls();
    }

    private void RefreshToneState()
    {
        OnPropertyChanged(nameof(ToneProfiles));
        OnPropertyChanged(nameof(HasToneProfiles));
        OnPropertyChanged(nameof(ActiveToneProfile));
        OnPropertyChanged(nameof(HasActiveToneProfile));
        OnPropertyChanged(nameof(IsToneEnabled));
        OnPropertyChanged(nameof(CanEnableToneLayer));
        OnPropertyChanged(nameof(ToneAdjustments));
        OnPropertyChanged(nameof(HasStoredToneAdjustments));
        OnPropertyChanged(nameof(HasToneAdjustments));
        OnPropertyChanged(nameof(IsToneLayerOff));
        OnPropertyChanged(nameof(IsToneProfileCustom));
        OnPropertyChanged(nameof(ActiveToneProfileName));
        OnPropertyChanged(nameof(ActiveToneProfileTooltip));
        OnPropertyChanged(nameof(ToneLayerDisplayName));
        OnPropertyChanged(nameof(TrayToneProfilesMenuText));
        OnPropertyChanged(nameof(TrayToneEnableDisableMenuText));
        OnPropertyChanged(nameof(TrayToneEnableDisableTooltip));
        OnPropertyChanged(nameof(StatusText));
        RefreshTrayQuickControls();
        _gammaService.InvalidateGamma();
    }

    private void RefreshToneTargetState()
    {
        OnPropertyChanged(nameof(SelectedToneBand));
        OnPropertyChanged(nameof(IsToneTargetOff));
        OnPropertyChanged(nameof(IsDarkTonesTarget));
        OnPropertyChanged(nameof(IsMidtonesTarget));
        OnPropertyChanged(nameof(IsHighlightsTarget));
        OnPropertyChanged(nameof(IsAllToneBandsTarget));
        OnPropertyChanged(nameof(ToneTargetDisplayName));
        RefreshTrayQuickControls();
    }

    private void RegisterHotKeys()
    {
        _hotKeyService.UnregisterAllHotKeys();

        if (_settingsService.ToggleHotKey != HotKey.None)
            _hotKeyService.RegisterHotKey(_settingsService.ToggleHotKey, ToggleFromUser);

        if (_settingsService.ToggleWindowHotKey != HotKey.None)
            _hotKeyService.RegisterHotKey(_settingsService.ToggleWindowHotKey, () => App.Current?.ToggleMainWindow());

        if (_settingsService.IncreaseTemperatureOffsetHotKey != HotKey.None)
            _hotKeyService.RegisterHotKey(_settingsService.IncreaseTemperatureOffsetHotKey, () => AdjustTemperatureOffset(_settingsService.TrayTemperatureWheelStep));

        if (_settingsService.DecreaseTemperatureOffsetHotKey != HotKey.None)
            _hotKeyService.RegisterHotKey(_settingsService.DecreaseTemperatureOffsetHotKey, () => AdjustTemperatureOffset(-_settingsService.TrayTemperatureWheelStep));

        if (_settingsService.IncreaseBrightnessOffsetHotKey != HotKey.None)
            _hotKeyService.RegisterHotKey(_settingsService.IncreaseBrightnessOffsetHotKey, () => AdjustBrightnessOffset(_settingsService.TrayBrightnessWheelStep));

        if (_settingsService.DecreaseBrightnessOffsetHotKey != HotKey.None)
            _hotKeyService.RegisterHotKey(_settingsService.DecreaseBrightnessOffsetHotKey, () => AdjustBrightnessOffset(-_settingsService.TrayBrightnessWheelStep));

        if (_settingsService.ResetConfigurationOffsetHotKey != HotKey.None)
            _hotKeyService.RegisterHotKey(_settingsService.ResetConfigurationOffsetHotKey, ResetToCurrentProfile);

        if (_settingsService.CycleProfilesHotKey != HotKey.None && HasProfiles)
        {
            _hotKeyService.RegisterHotKey(
                _settingsService.CycleProfilesHotKey,
                () =>
                {
                    CycleProfiles();
                    _settingsService.Save();
                }
            );
        }

        if (_settingsService.ToggleLastProfileHotKey != HotKey.None && HasProfiles)
        {
            _hotKeyService.RegisterHotKey(
                _settingsService.ToggleLastProfileHotKey,
                () =>
                {
                    ToggleLastProfile();
                    _settingsService.Save();
                }
            );
        }

        foreach (var profile in Profiles)
        {
            if (profile.HotKey == HotKey.None)
                continue;

            var profileId = profile.Id;
            _hotKeyService.RegisterHotKey(
                profile.HotKey,
                () =>
                {
                    ActivateProfile(profileId);
                    _settingsService.Save();
                }
            );
        }

        _registeredProfileHotKeySignature = GetProfileHotKeySignature();
    }

    private void UpdateInstant()
    {
        // If in cycle preview mode, advance quickly until the full cycle has been reached
        if (IsCyclePreviewEnabled)
        {
            // Cycle is supposed to end 1 full day past the current real time
            var targetInstant = DateTimeOffset.Now + TimeSpan.FromDays(1);

            Instant = Instant.StepTo(targetInstant, TimeSpan.FromMinutes(5));
            if (Instant >= targetInstant)
                IsCyclePreviewEnabled = false;
        }
        // Otherwise, synchronize the instant with the system clock
        else
        {
            Instant = DateTimeOffset.Now;
        }
    }

    private void UpdateConfiguration()
    {
        var isSmooth =
            !IsCyclePreviewEnabled
            && CurrentConfiguration != TargetConfiguration
            && _settingsService.IsConfigurationSmoothingEnabled
            && _settingsService.ConfigurationSmoothingMaxDuration.TotalSeconds >= 0.1;

        if (isSmooth)
        {
            // Check if the target configuration has changed since the last transition started
            if (
                _configurationSmoothingTarget != TargetConfiguration
                || _configurationSmoothingSource is null
            )
            {
                _configurationSmoothingSource = CurrentConfiguration;
                _configurationSmoothingTarget = TargetConfiguration;
            }

            var brightnessDelta = Math.Abs(
                _configurationSmoothingTarget.Value.Brightness
                    - _configurationSmoothingSource.Value.Brightness
            );

            var brightnessStep = Math.Max(
                brightnessDelta
                    / _settingsService.ConfigurationSmoothingMaxDuration.TotalSeconds
                    * _updateConfigurationTimer.Interval.TotalSeconds,
                0.08
            );

            var temperatureDelta = Math.Abs(
                _configurationSmoothingTarget.Value.Temperature
                    - _configurationSmoothingSource.Value.Temperature
            );

            var temperatureStep = Math.Max(
                temperatureDelta
                    / _settingsService.ConfigurationSmoothingMaxDuration.TotalSeconds
                    * _updateConfigurationTimer.Interval.TotalSeconds,
                30
            );

            CurrentConfiguration = CurrentConfiguration.StepTo(
                TargetConfiguration,
                temperatureStep,
                brightnessStep
            );
        }
        else
        {
            CurrentConfiguration = TargetConfiguration;
            _configurationSmoothingSource = null;
            _configurationSmoothingTarget = null;
        }

        _gammaService.SetGamma(
            CurrentConfiguration,
            IsActive && IsToneEnabled ? ToneAdjustments : ToneAdjustmentConfiguration.Off
        );
    }

    private void UpdateIsPaused()
    {
        bool IsPausedByFullScreen() =>
            _settingsService.IsPauseWhenFullScreenEnabled
            && _externalApplicationService.IsForegroundApplicationFullScreen();

        bool IsPausedByWhitelistedApplication() =>
            _settingsService.IsApplicationWhitelistEnabled
            && _settingsService.WhitelistedApplications is not null
            && _settingsService.WhitelistedApplications.Contains(
                _externalApplicationService.TryGetForegroundApplication()
            );

        IsPaused = IsPausedByFullScreen() || IsPausedByWhitelistedApplication();
    }

    public override Task InitializeAsync()
    {
        RestorePersistedManualAdjustments();

        // Always register persisted hotkeys when the backend initializes.
        // This also covers startup paths where loading settings did not raise a change event.
        RegisterHotKeys();

        _updateInstantTimer.Start();
        _updateConfigurationTimer.Start();
        _updateIsPausedTimer.Start();

        // Hack: feign property changes to refresh the tray icon
        OnAllPropertiesChanged();

        return Task.CompletedTask;
    }

    [RelayCommand]
    private void DisableTemporarily(TimeSpan duration)
    {
        IsEnabled = false;
        _enableAfterDelayRegistration?.Dispose();
        _enableAfterDelayRegistration = Timer.QueueDelayedAction(
            duration,
            () => Dispatcher.UIThread.Post(() => IsEnabled = true)
        );
    }

    [RelayCommand]
    private void DisableUntilSunrise()
    {
        var now = DateTimeOffset.Now;
        var timeUntilSunrise = SolarTimes.Sunrise.NextAfter(now) - now;
        DisableTemporarily(timeUntilSunrise);
    }

    /// <summary>
    /// Applies the master enabled state from an explicit user action. Enabling always clears manual
    /// offsets first so the screen returns to the currently selected profile (or Automatic), as the
    /// Settings toggle, tray Toggle command, and global Toggle LightBulb shortcut must stay identical.
    /// </summary>
    public void SetLightBulbEnabled(bool enabled)
    {
        if (enabled && !IsEnabled)
            ResetToCurrentProfile();

        IsEnabled = enabled;
        RefreshTrayQuickControls();
    }

    private void ToggleFromUser() => SetLightBulbEnabled(!IsEnabled);

    [RelayCommand]
    private void Toggle() => ToggleFromUser();

    partial void OnTemperatureOffsetChanged(double value) => QueueManualAdjustmentPersistence();

    partial void OnBrightnessOffsetChanged(double value) => QueueManualAdjustmentPersistence();

    private void RestorePersistedManualAdjustments()
    {
        var baseConfiguration = BaseTargetConfiguration;

        _isRestoringManualAdjustments = true;
        try
        {
            TemperatureOffset = _settingsService.ManualTemperatureOffset.Clamp(
                _settingsService.MinimumTemperature - baseConfiguration.Temperature,
                _settingsService.MaximumTemperature - baseConfiguration.Temperature
            );
            BrightnessOffset = _settingsService.ManualBrightnessOffset.Clamp(
                _settingsService.MinimumBrightness - baseConfiguration.Brightness,
                _settingsService.MaximumBrightness - baseConfiguration.Brightness
            );
        }
        finally
        {
            _isRestoringManualAdjustments = false;
        }

        // Keep any clamped migration value as the new authoritative runtime checkpoint.
        _settingsService.ManualTemperatureOffset = TemperatureOffset;
        _settingsService.ManualBrightnessOffset = BrightnessOffset;
    }

    private void QueueManualAdjustmentPersistence()
    {
        if (_isRestoringManualAdjustments)
            return;

        // Update the in-memory checkpoint immediately, but debounce disk I/O so wheel or touch
        // scrubbing cannot serialize settings once per pointer/wheel event.
        _settingsService.ManualTemperatureOffset = TemperatureOffset;
        _settingsService.ManualBrightnessOffset = BrightnessOffset;

        _persistManualAdjustmentsRegistration?.Dispose();
        _persistManualAdjustmentsRegistration = Timer.QueueDelayedAction(
            TimeSpan.FromMilliseconds(350),
            () => _settingsService.TrySaveRuntimeState()
        );
    }

    private void FlushManualAdjustmentPersistence()
    {
        _persistManualAdjustmentsRegistration?.Dispose();
        _persistManualAdjustmentsRegistration = null;

        _settingsService.ManualTemperatureOffset = TemperatureOffset;
        _settingsService.ManualBrightnessOffset = BrightnessOffset;
        _settingsService.TrySaveRuntimeState();
    }

    public void AdjustTemperatureOffset(double amount)
    {
        var baseTemperature = BaseTargetConfiguration.Temperature;
        TemperatureOffset = (TemperatureOffset + amount).Clamp(
            _settingsService.MinimumTemperature - baseTemperature,
            _settingsService.MaximumTemperature - baseTemperature
        );
    }

    public void AdjustBrightnessOffset(double amount)
    {
        var baseBrightness = BaseTargetConfiguration.Brightness;
        BrightnessOffset = (BrightnessOffset + amount).Clamp(
            _settingsService.MinimumBrightness - baseBrightness,
            _settingsService.MaximumBrightness - baseBrightness
        );
    }

    public void SetEffectiveTemperature(double temperature)
    {
        var baseTemperature = BaseTargetConfiguration.Temperature;
        TemperatureOffset = (temperature - baseTemperature).Clamp(
            _settingsService.MinimumTemperature - baseTemperature,
            _settingsService.MaximumTemperature - baseTemperature
        );
        RefreshTrayQuickControls();
    }

    public void SetEffectiveBrightness(double brightness)
    {
        var baseBrightness = BaseTargetConfiguration.Brightness;
        BrightnessOffset = (brightness - baseBrightness).Clamp(
            _settingsService.MinimumBrightness - baseBrightness,
            _settingsService.MaximumBrightness - baseBrightness
        );
        RefreshTrayQuickControls();
    }

    public void ActivateProfile(Guid? profileId)
    {
        if (profileId is { } id && !Profiles.Any(p => p.Id == id))
            profileId = null;

        if (profileId is { } validProfileId)
            _settingsService.LastProfileId = validProfileId;

        _settingsService.ActiveProfileId = profileId;
        ResetToCurrentProfile();
    }

    public void ToggleLastProfile()
    {
        if (Profiles.Count == 0)
        {
            ActivateProfile(null);
            return;
        }

        if (_settingsService.ActiveProfileId is not null)
        {
            ActivateProfile(null);
            return;
        }

        ActivateProfile(LastProfile?.Id ?? Profiles[0].Id);
    }

    public void CycleProfiles()
    {
        if (Profiles.Count == 0)
        {
            ActivateProfile(null);
            return;
        }

        if (_settingsService.ActiveProfileId is not { } activeId)
        {
            ActivateProfile(Profiles[0].Id);
            return;
        }

        var currentIndex = Profiles.ToList().FindIndex(p => p.Id == activeId);
        if (currentIndex < 0 || currentIndex >= Profiles.Count - 1)
            ActivateProfile(null);
        else
            ActivateProfile(Profiles[currentIndex + 1].Id);
    }


    public void ActivateToneProfile(Guid? profileId, bool selectHighlights = false)
    {
        if (profileId is { } id && !ToneProfiles.Any(p => p.Id == id))
            profileId = null;

        if (profileId is { } validId)
        {
            var profile = ToneProfiles.First(p => p.Id == validId);
            _settingsService.IsToneLayerEnabled = true;
            _settingsService.ActiveToneProfileId = validId;
            _settingsService.LastToneProfileId = validId;
            _settingsService.ToneAdjustments = profile.Adjustments;
            // The tray profile submenu explicitly enters Highlights for fast adjustment. Other
            // activation paths can still honor the profile's saved default tray target.
            var selectedBand = selectHighlights
                ? ToneBand.Highlights
                : profile.SelectedBand ?? ToneBand.Highlights;
            _settingsService.SelectedToneBand = selectedBand;
            _settingsService.LastSelectedToneBand = selectedBand;
        }
        else
        {
            DisableToneLayer();
        }

        QueueToneAdjustmentPersistence();
    }

    public void SetSelectedToneBand(ToneBand? band)
    {
        if (band is { } value && !Enum.IsDefined(value))
            band = null;

        if (band is null)
        {
            DisableToneLayer();
            return;
        }

        EnableToneLayer();
        _settingsService.SelectedToneBand = band;
        if (band is { } selectedBand)
            _settingsService.LastSelectedToneBand = selectedBand;
        QueueToneAdjustmentPersistence();
    }

    public void ToggleSelectedToneBand()
    {
        ToggleToneLayer();
    }

    public void ToggleToneLayer()
    {
        if (IsToneEnabled)
            DisableToneLayer();
        else
            EnableToneLayer();
    }

    public void DisableToneLayer()
    {
        if (SelectedToneBand is { } currentBand)
            _settingsService.LastSelectedToneBand = currentBand;
        if (_settingsService.ActiveToneProfileId is { } currentProfileId)
            _settingsService.LastToneProfileId = currentProfileId;

        _settingsService.IsToneLayerEnabled = false;
        _settingsService.ActiveToneProfileId = null;
        QueueToneAdjustmentPersistence();
    }

    public void EnableToneLayer()
    {
        var restoreBand = _settingsService.LastSelectedToneBand;
        if (!Enum.IsDefined(restoreBand))
            restoreBand = ToneBand.Highlights;

        // Nothing has ever been selected/edited, so there is no previous tone state to restore.
        // Selecting a tone profile is the authoritative way to establish the first tone state.
        var hasRestorableTone = HasStoredToneAdjustments
            || (_settingsService.LastToneProfileId is { } lastId && ToneProfiles.Any(p => p.Id == lastId));
        if (!hasRestorableTone)
            return;

        _settingsService.IsToneLayerEnabled = true;
        _settingsService.SelectedToneBand = restoreBand;

        if (
            _settingsService.LastToneProfileId is { } lastProfileId
            && ToneProfiles.Any(p => p.Id == lastProfileId)
        )
        {
            _settingsService.ActiveToneProfileId = lastProfileId;
        }

        QueueToneAdjustmentPersistence();
    }

    public void AdjustSelectedToneIntensity(double amount)
    {
        if (IsToneEnabled && SelectedToneBand is { } band)
            AdjustToneIntensity(band, amount);
    }

    public void AdjustSelectedToneTemperature(double amount)
    {
        if (IsToneEnabled && SelectedToneBand is { } band)
            AdjustToneBand(band, amount, 0);
        else
            AdjustTemperatureOffset(amount);
    }

    public void AdjustSelectedToneBrightness(double amount)
    {
        if (IsToneEnabled && SelectedToneBand is { } band)
            AdjustToneBand(band, 0, amount);
        else
            AdjustBrightnessOffset(amount);
    }

    public void AdjustToneBand(
        ToneBand band,
        double temperatureOffset,
        double brightnessOffset
    )
    {
        var adjusted = ToneAdjustments.AddToBand(band, temperatureOffset, brightnessOffset);
        _settingsService.ToneAdjustments = ClampToneAdjustments(adjusted);
        QueueToneAdjustmentPersistence();
    }

    public void AdjustToneIntensity(ToneBand band, double amount)
    {
        if (!IsToneEnabled)
            return;

        var adjusted = ToneAdjustments.AddIntensity(band, amount);
        _settingsService.ToneAdjustments = ClampToneAdjustments(adjusted);
        QueueToneAdjustmentPersistence();
    }

    public void ResetToneAdjustments()
    {
        if (ActiveToneProfile is { } profile)
        {
            _settingsService.ToneAdjustments = profile.Adjustments;
        }
        else
        {
            _settingsService.ToneAdjustments = ToneAdjustmentConfiguration.Off;
        }
        QueueToneAdjustmentPersistence();
    }

    public void SaveCurrentToneToProfile(Guid profileId)
    {
        var target = ToneProfiles.FirstOrDefault(profile => profile.Id == profileId);
        if (target is null)
            return;

        var current = ToneAdjustments;
        var selectedBand = SelectedToneBand;
        _settingsService.ToneProfiles = ToneProfiles
            .Select(profile =>
                profile.Id == profileId
                    ? profile with { Adjustments = current, SelectedBand = selectedBand }
                    : profile
            )
            .ToArray();
        _settingsService.IsToneLayerEnabled = true;
        _settingsService.ActiveToneProfileId = profileId;
        _settingsService.LastToneProfileId = profileId;
        _settingsService.ToneAdjustments = current;
        _settingsService.SelectedToneBand = selectedBand ?? ToneBand.Highlights;
        _settingsService.LastSelectedToneBand = _settingsService.SelectedToneBand.Value;
    }

    public ToneProfile SaveCurrentToneAsNewProfile()
    {
        var profile = new ToneProfile(
            Guid.NewGuid(),
            $"Tone profile {GetNextToneProfileNumber()}",
            ToneAdjustments,
            SelectedToneBand
        );
        _settingsService.ToneProfiles = ToneProfiles.Append(profile).ToArray();
        _settingsService.IsToneLayerEnabled = true;
        _settingsService.ActiveToneProfileId = profile.Id;
        _settingsService.LastToneProfileId = profile.Id;
        _settingsService.ToneAdjustments = profile.Adjustments;
        _settingsService.SelectedToneBand = SelectedToneBand ?? ToneBand.Highlights;
        _settingsService.LastSelectedToneBand = _settingsService.SelectedToneBand.Value;
        return profile;
    }

    private int GetNextToneProfileNumber()
    {
        var number = 1;
        while (ToneProfiles.Any(profile => profile.Name == $"Tone profile {number}"))
            number++;
        return number;
    }

    private ToneAdjustmentConfiguration ClampToneAdjustments(
        ToneAdjustmentConfiguration adjustments
    ) =>
        new(
            ClampToneBand(adjustments.DarkTones),
            ClampToneBand(adjustments.Midtones),
            ClampToneBand(adjustments.Highlights)
        );

    private ToneBandAdjustment ClampToneBand(ToneBandAdjustment adjustment) =>
        new(
            adjustment.TemperatureOffset.Clamp(
                _settingsService.MinimumToneTemperatureOffset,
                _settingsService.MaximumToneTemperatureOffset
            ),
            adjustment.BrightnessOffset.Clamp(
                _settingsService.MinimumToneBrightnessOffset,
                _settingsService.MaximumToneBrightnessOffset
            ),
            adjustment.Intensity.Clamp(
                _settingsService.MinimumToneIntensity,
                _settingsService.MaximumToneIntensity
            )
        );

    private void QueueToneAdjustmentPersistence()
    {
        _persistToneAdjustmentsRegistration?.Dispose();
        _persistToneAdjustmentsRegistration = Timer.QueueDelayedAction(
            TimeSpan.FromMilliseconds(350),
            () => _settingsService.TrySaveRuntimeState()
        );
    }

    private void FlushToneAdjustmentPersistence()
    {
        _persistToneAdjustmentsRegistration?.Dispose();
        _persistToneAdjustmentsRegistration = null;
        _settingsService.TrySaveRuntimeState();
    }

    private void PreserveEffectiveLook(ColorConfiguration configuration)
    {
        var baseConfiguration = BaseTargetConfiguration;
        TemperatureOffset = (configuration.Temperature - baseConfiguration.Temperature).Clamp(
            _settingsService.MinimumTemperature - baseConfiguration.Temperature,
            _settingsService.MaximumTemperature - baseConfiguration.Temperature
        );
        BrightnessOffset = (configuration.Brightness - baseConfiguration.Brightness).Clamp(
            _settingsService.MinimumBrightness - baseConfiguration.Brightness,
            _settingsService.MaximumBrightness - baseConfiguration.Brightness
        );
    }

    public void SaveCurrentLookAsDayConfiguration()
    {
        var current = TargetConfiguration;
        var night = _settingsService.NightConfiguration;

        _settingsService.DayConfiguration = current;
        _settingsService.NightConfiguration = new ColorConfiguration(
            Math.Min(night.Temperature, current.Temperature),
            Math.Min(night.Brightness, current.Brightness)
        );

        PreserveEffectiveLook(current);
        _settingsService.Save();
    }

    public void SaveCurrentLookAsNightConfiguration()
    {
        var current = TargetConfiguration;
        var day = _settingsService.DayConfiguration;

        _settingsService.NightConfiguration = current;
        _settingsService.DayConfiguration = new ColorConfiguration(
            Math.Max(day.Temperature, current.Temperature),
            Math.Max(day.Brightness, current.Brightness)
        );

        PreserveEffectiveLook(current);
        _settingsService.Save();
    }

    public void SaveCurrentLookToActiveProfile()
    {
        if (ActiveProfile is not { } activeProfile)
            return;

        var current = TargetConfiguration;
        _settingsService.Profiles = Profiles
            .Select(profile =>
                profile.Id == activeProfile.Id
                    ? profile with
                    {
                        Temperature = current.Temperature,
                        Brightness = current.Brightness,
                    }
                    : profile
            )
            .ToArray();
        PreserveEffectiveLook(current);
        _settingsService.Save();
    }

    public void ResetTemperatureOffset() => TemperatureOffset = 0;

    public void ResetBrightnessOffset() => BrightnessOffset = 0;

    public void ResetToCurrentProfile()
    {
        TemperatureOffset = 0;
        BrightnessOffset = 0;
    }

    public void RefreshTrayQuickControls()
    {
        OnPropertyChanged(nameof(TrayTemperatureControlText));
        OnPropertyChanged(nameof(TrayBrightnessControlText));
        OnPropertyChanged(nameof(TrayResetControlText));
        OnPropertyChanged(nameof(TrayDarkTonesControlText));
        OnPropertyChanged(nameof(TrayMidtonesControlText));
        OnPropertyChanged(nameof(TrayHighlightsControlText));
        OnPropertyChanged(nameof(TrayAllToneBandsControlText));
        OnPropertyChanged(nameof(TrayToneWheelHintText));
        OnPropertyChanged(nameof(TrayTonesMenuText));
        OnPropertyChanged(nameof(ToneTargetDisplayName));
        OnPropertyChanged(nameof(IsToneTargetOff));
        OnPropertyChanged(nameof(IsDarkTonesTarget));
        OnPropertyChanged(nameof(IsMidtonesTarget));
        OnPropertyChanged(nameof(IsHighlightsTarget));
        OnPropertyChanged(nameof(IsAllToneBandsTarget));
        OnPropertyChanged(nameof(IsTemperatureOffsetEnabled));
        OnPropertyChanged(nameof(IsBrightnessOffsetEnabled));
        OnPropertyChanged(nameof(IsOffsetEnabled));
    }

    [RelayCommand]
    private void ResetConfigurationOffset() => ResetToCurrentProfile();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _eventSubscription.Dispose();

            _updateInstantTimer.Dispose();
            _updateConfigurationTimer.Dispose();
            _updateIsPausedTimer.Dispose();

            _enableAfterDelayRegistration?.Dispose();
            FlushManualAdjustmentPersistence();
            FlushToneAdjustmentPersistence();
        }

        base.Dispose(disposing);
    }
}
