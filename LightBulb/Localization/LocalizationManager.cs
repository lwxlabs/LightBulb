using System;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using LightBulb.Services;
using PowerKit;
using PowerKit.Extensions;

namespace LightBulb.Localization;

public partial class LocalizationManager : ObservableObject, IDisposable
{
    private readonly IDisposable _eventSubscription;

    public LocalizationManager(SettingsService settingsService)
    {
        _eventSubscription = Disposable.Merge(
            settingsService.WatchProperty(o => o.Language, v => Language = v, true),
            this.WatchProperty(
                o => o.Language,
                _ =>
                {
                    foreach (var propertyName in EnglishLocalization.Keys)
                        OnPropertyChanged(propertyName);
                }
            )
        );
    }

    [ObservableProperty]
    public partial Language Language { get; set; } = Language.System;

    private string Get([CallerMemberName] string? key = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        var localization = Language switch
        {
            Language.System =>
                CultureInfo.CurrentUICulture.ThreeLetterISOLanguageName.ToLowerInvariant() switch
                {
                    "ukr" => UkrainianLocalization,
                    "deu" => GermanLocalization,
                    "fra" => FrenchLocalization,
                    "spa" => SpanishLocalization,
                    "zho"
                        when CultureInfo
                            .CurrentUICulture.GetSelfAndParents()
                            .Any(c =>
                                string.Equals(c.Name, "zh-Hans", StringComparison.OrdinalIgnoreCase)
                            ) => ChineseSimplifiedLocalization,
                    "zho"
                        when CultureInfo
                            .CurrentUICulture.GetSelfAndParents()
                            .Any(c =>
                                string.Equals(c.Name, "zh-Hant", StringComparison.OrdinalIgnoreCase)
                            ) => ChineseTraditionalLocalization,
                    _ => EnglishLocalization,
                },
            Language.Ukrainian => UkrainianLocalization,
            Language.German => GermanLocalization,
            Language.French => FrenchLocalization,
            Language.Spanish => SpanishLocalization,
            Language.ChineseSimplified => ChineseSimplifiedLocalization,
            Language.ChineseTraditional => ChineseTraditionalLocalization,
            _ => EnglishLocalization,
        };

        if (
            localization.TryGetValue(key, out var value)
            || EnglishLocalization.TryGetValue(key, out value)
        )
        {
            return value;
        }

        return $"Missing localization for '{key}'";
    }

    public void Dispose() => _eventSubscription.Dispose();
}

public partial class LocalizationManager
{
    // ---- Dashboard ----

    public string SunsetLabel => Get();
    public string SunriseLabel => Get();
    public string SunsetTransitionTooltip => Get();
    public string SunriseTransitionTooltip => Get();
    public string OffsetTooltipHeader => Get();
    public string TemperatureOffsetLabel => Get();
    public string BrightnessOffsetLabel => Get();
    public string ClickToResetLabel => Get();
    public string OffsetLabel => Get();

    // ---- Main window ----

    public string ToggleLightBulbTooltip => Get();
    public string HideToTrayTooltip => Get();
    public string PreviewText => Get();
    public string StopPreviewTooltip => Get();
    public string StartPreviewTooltip => Get();
    public string SettingsText => Get();
    public string OpenSettingsTooltip => Get();

    // ---- Settings dialog ----

    public string ResetButton => Get();
    public string ResetTooltip => Get();
    public string CancelButton => Get();
    public string SaveButton => Get();

    // ---- Settings tabs ----

    public string GeneralTabName => Get();
    public string ProfilesTabName => Get();
    public string ToneProfilesTabName => Get();
    public string LocationTabName => Get();
    public string AdvancedTabName => Get();
    public string AppWhitelistTabName => Get();
    public string HotkeysTabName => Get();
    public string LogTabName => Get();

    // ---- Advanced settings tab ----

    public string ThemeLabel => Get();
    public string ThemeTooltip => Get();
    public string LanguageLabel => Get();
    public string LanguageTooltip => Get();
    public string StartWithWindowsLabel => Get();
    public string StartWithWindowsTooltip => Get();
    public string StartMinimizedLabel => Get();
    public string StartMinimizedTooltip => Get();
    public string TrayLeftClickActionLabel => Get();
    public string TrayLeftClickActionTooltip => Get();
    public string LightBulbEnabledLabel => Get();
    public string LightBulbEnabledTooltip => Get();
    public string DefaultToDayConfigLabel => Get();
    public string DefaultToDayConfigTooltip => Get();
    public string DayNightCycleLabel => Get();
    public string DayNightCycleTooltip => Get();
    public string PauseWhenFullscreenLabel => Get();
    public string PauseWhenFullscreenTooltip => Get();
    public string GammaSmoothingLabel => Get();
    public string GammaSmoothingTooltip => Get();
    public string GammaPollingLabel => Get();
    public string GammaPollingTooltip => Get();
    public string TrayTemperatureStepLabel => Get();
    public string TrayTemperatureStepTooltip => Get();
    public string TrayBrightnessStepLabel => Get();
    public string TrayBrightnessStepTooltip => Get();
    public string TrayFineWheelHint => Get();
    public string TrayColorWheelModifierLabel => Get();
    public string TrayColorWheelModifierTooltip => Get();
    public string TrayWheelModifierLabel => Get();
    public string TrayWheelModifierTooltip => Get();

    // ---- General settings tab ----

    public string DayTemperatureLabel => Get();
    public string DayTemperatureTooltip => Get();
    public string NightTemperatureLabel => Get();
    public string NightTemperatureTooltip => Get();
    public string DayBrightnessLabel => Get();
    public string DayBrightnessTooltip => Get();
    public string NightBrightnessLabel => Get();
    public string NightBrightnessTooltip => Get();
    public string TransitionDurationLabel => Get();
    public string TransitionDurationTooltip => Get();
    public string TransitionOffsetLabel => Get();
    public string TransitionOffsetTooltip => Get();

    // ---- Profile settings tab ----

    public string ProfileEditorHeader => Get();
    public string ProfileNameLabel => Get();
    public string ProfileTooltipLabel => Get();
    public string ProfileTooltipWatermark => Get();
    public string ProfileTemperatureLabel => Get();
    public string ProfileBrightnessLabel => Get();
    public string ProfileShortcutLabel => Get();
    public string ProfileAddButton => Get();
    public string ProfileDuplicateButton => Get();
    public string ProfileRemoveButton => Get();
    public string ProfileMoveUpButton => Get();
    public string ProfileMoveDownButton => Get();
    public string ProfileUpdateFromCurrentButton => Get();
    public string ProfileActivateButton => Get();
    public string ProfileAutomaticButton => Get();
    public string ProfileNoProfilesText => Get();
    public string ProfileHintText => Get();
    public string ProfileManageMenuItem => Get();

    // ---- Tone profile settings tab ----

    public string ToneProfileEditorHeader => Get();
    public string ToneProfileNameLabel => Get();
    public string ToneProfileTooltipWatermark => Get();
    public string ToneProfileAddButton => Get();
    public string ToneProfileDuplicateButton => Get();
    public string ToneProfileRemoveButton => Get();
    public string ToneProfileMoveUpButton => Get();
    public string ToneProfileMoveDownButton => Get();
    public string ToneProfileActivateButton => Get();
    public string ToneProfileOffButton => Get();
    public string ToneProfileCaptureCurrentButton => Get();
    public string ToneProfileNoProfilesText => Get();
    public string ToneProfileHintText => Get();
    public string ToneProfileManageMenuItem => Get();
    public string ToneDarkTonesLabel => Get();
    public string ToneMidtonesLabel => Get();
    public string ToneHighlightsLabel => Get();
    public string ToneAllBandsLabel => Get();
    public string ToneTemperatureOffsetLabel => Get();
    public string ToneBrightnessOffsetLabel => Get();
    public string ToneIntensityLabel => Get();
    public string ToneDefaultTargetLabel => Get();
    public string ToneDefaultTargetTooltip => Get();
    public string ToneDarkTonesHelpTooltip => Get();
    public string ToneMidtonesHelpTooltip => Get();
    public string ToneHighlightsHelpTooltip => Get();
    public string ToneIntensityTooltip => Get();

    // ---- Location settings tab ----

    public string SolarConfigLabel => Get();
    public string ManualLabel => Get();
    public string ManualTooltip => Get();
    public string LocationBasedLabel => Get();
    public string LocationBasedTooltip => Get();
    public string SunriseTimeLabel => Get();
    public string SunsetTimeLabel => Get();
    public string YourLocationLabel => Get();
    public string AutoDetectLocationTooltip => Get();
    public string LocationQueryTooltip => Get();
    public string SetLocationTooltip => Get();
    public string LocationErrorText => Get();

    // ---- Hot key settings tab ----

    public string ToggleLightBulbHotkeyLabel => Get();
    public string ToggleLightBulbHotkeyTooltip => Get();
    public string ToggleWindowLabel => Get();
    public string ToggleWindowHotkeyTooltip => Get();
    public string IncreaseTemperatureOffsetLabel => Get();
    public string IncreaseTemperatureOffsetTooltip => Get();
    public string DecreaseTemperatureOffsetLabel => Get();
    public string DecreaseTemperatureOffsetTooltip => Get();
    public string IncreaseBrightnessOffsetLabel => Get();
    public string IncreaseBrightnessOffsetTooltip => Get();
    public string DecreaseBrightnessOffsetLabel => Get();
    public string DecreaseBrightnessOffsetTooltip => Get();
    public string ResetOffsetLabel => Get();
    public string ResetOffsetHotkeyTooltip => Get();
    public string HotKeyInputHint => Get();
    public string HotKeyRegistrationError => Get();
    public string HotKeyConflictWarning => Get();
    public string CycleProfilesLabel => Get();
    public string CycleProfilesTooltip => Get();
    public string ToggleLastProfileLabel => Get();
    public string ToggleLastProfileTooltip => Get();

    // ---- Application whitelist settings tab ----

    public string AppWhitelistLabel => Get();
    public string RefreshAppsTooltip => Get();
    public string PauseForWhitelistedTooltip => Get();

    // ---- Tray icon context menu ----

    public string QuickAdjustTitle => Get();
    public string QuickAdjustProfileLabel => Get();
    public string QuickAdjustTemperatureLabel => Get();
    public string QuickAdjustBrightnessLabel => Get();
    public string QuickAdjustResetButton => Get();

    public string TrayShowMenuItem => Get();
    public string TrayHideMenuItem => Get();
    public string TraySettingsMenuItem => Get();
    public string TrayProfilesMenuItem => Get();
    public string TrayAutomaticProfileMenuItem => Get();
    public string TrayToneProfilesMenuItem => Get();
    public string TrayTonesMenuItem => Get();
    public string TrayToneOffMenuItem => Get();
    public string TrayToneDisableMenuItem => Get();
    public string TrayToneEnableMenuItem => Get();
    public string TrayToneCustomMenuItem => Get();
    public string TrayToneMixedValue => Get();
    public string TrayToneStatusPrefix => Get();
    public string TrayToneResetMenuItem => Get();
    public string TrayToneSaveExistingMenuItem => Get();
    public string TrayToneSaveNewMenuItem => Get();
    public string TrayToneWheelHintMenuItem => Get();
    public string TrayToneTargetTooltip => Get();
    public string TrayToneOffTargetTooltip => Get();
    public string TrayToneDisableTooltip => Get();
    public string TrayToneEnableTooltip => Get();
    public string TrayDarkTonesTooltip => Get();
    public string TrayMidtonesTooltip => Get();
    public string TrayHighlightsTooltip => Get();
    public string TrayAllToneBandsTooltip => Get();
    public string TrayToneResetTooltip => Get();
    public string TrayToneProfileOffTooltip => Get();
    public string TrayCustomToneProfileTooltip => Get();
    public string TrayAutomaticProfileTooltip => Get();
    public string TraySelectedToneTemperatureControlFormat => Get();
    public string TraySelectedToneTemperatureMixedControlFormat => Get();
    public string TraySelectedToneBrightnessControlFormat => Get();
    public string TraySelectedToneBrightnessMixedControlFormat => Get();
    public string TrayToneTemperatureControlFormat => Get();
    public string TrayToneTemperatureMixedControlFormat => Get();
    public string TrayToneBrightnessControlFormat => Get();
    public string TrayToneBrightnessMixedControlFormat => Get();
    public string TrayCycleProfilesMenuItem => Get();
    public string TrayToggleLastProfileMenuItem => Get();
    public string TrayAdjustMenuItem => Get();
    public string TrayCoolerMenuItem => Get();
    public string TrayWarmerMenuItem => Get();
    public string TrayBrighterMenuItem => Get();
    public string TrayDimmerMenuItem => Get();
    public string TrayResetProfileMenuItem => Get();
    public string TrayWheelHintMenuItem => Get();
    public string TrayTemperatureControlFormat => Get();
    public string TrayTemperatureControlTooltip => Get();
    public string TrayBrightnessControlFormat => Get();
    public string TrayBrightnessControlTooltip => Get();
    public string TraySaveCurrentMenuItem => Get();
    public string TraySaveCurrentAsDayMenuItem => Get();
    public string TraySaveCurrentAsNightMenuItem => Get();
    public string TraySaveCurrentToProfileMenuItem => Get();
    public string TrayEnableMenuItem => Get();
    public string TrayDisableMenuItem => Get();
    public string TrayDisableTemporarilyMenuItem => Get();
    public string TrayDisableUntilSunriseMenuItem => Get();
    public string TrayDisableFor1DayMenuItem => Get();
    public string TrayDisableFor12HoursMenuItem => Get();
    public string TrayDisableFor6HoursMenuItem => Get();
    public string TrayDisableFor3HoursMenuItem => Get();
    public string TrayDisableFor1HourMenuItem => Get();
    public string TrayDisableFor30MinutesMenuItem => Get();
    public string TrayDisableFor15MinutesMenuItem => Get();
    public string TrayDisableFor5MinutesMenuItem => Get();
    public string TrayDisableFor1MinuteMenuItem => Get();
    public string TrayExitMenuItem => Get();

    public string TrayTooltipDisabled => Get();

    // ---- Log settings tab ----

    public string LogHeader => Get();
    public string LogHint => Get();
    public string LogRefreshButton => Get();
    public string LogCopyButton => Get();
    public string LogClearButton => Get();

    // ---- Dialog messages ----

    public string CloseButton => Get();
    public string LimitedGammaRangeTitle => Get();
    public string LimitedGammaRangeMessage => Get();
    public string FixButton => Get();
}
