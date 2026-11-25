using System.Collections.Generic;

namespace LightBulb.Localization;

public partial class LocalizationManager
{
    private static readonly IReadOnlyDictionary<string, string> EnglishLocalization =
        new Dictionary<string, string>
        {
            // Dashboard
            [nameof(SunsetLabel)] = "Sunset",
            [nameof(SunriseLabel)] = "Sunrise",
            [nameof(SunsetTransitionTooltip)] =
                "Sunset transition starts at **{0}** and ends at **{1}**",
            [nameof(SunriseTransitionTooltip)] =
                "Sunrise transition starts at **{0}** and ends at **{1}**",
            [nameof(OffsetTooltipHeader)] =
                "Current temperature and brightness values are adjusted by an offset:",
            [nameof(TemperatureOffsetLabel)] = "Temperature offset:",
            [nameof(BrightnessOffsetLabel)] = "Brightness offset:",
            [nameof(ClickToResetLabel)] = "Click to reset",
            [nameof(OffsetLabel)] = "offset",
            // Main window
            [nameof(ToggleLightBulbTooltip)] = "Toggle LightBulb on/off",
            [nameof(HideToTrayTooltip)] = "Hide LightBulb to the system tray",
            [nameof(PreviewText)] = "PREVIEW",
            [nameof(StopPreviewTooltip)] = "Stop preview",
            [nameof(StartPreviewTooltip)] = "Preview 24-hour cycle",
            [nameof(SettingsText)] = "SETTINGS",
            [nameof(OpenSettingsTooltip)] = "Open settings",
            // Settings dialog
            [nameof(ResetButton)] = "RESET",
            [nameof(ResetTooltip)] = "Reset all settings to their defaults",
            [nameof(CancelButton)] = "CANCEL",
            [nameof(SaveButton)] = "SAVE",
            // Settings tabs
            [nameof(GeneralTabName)] = "General",
            [nameof(ProfilesTabName)] = "Profiles",
            [nameof(ToneProfilesTabName)] = "Tone profiles",
            [nameof(LocationTabName)] = "Location",
            [nameof(AdvancedTabName)] = "Advanced",
            [nameof(AppWhitelistTabName)] = "Application whitelist",
            [nameof(HotkeysTabName)] = "Hotkeys",
            [nameof(LogTabName)] = "Log",
            // Advanced settings tab
            [nameof(ThemeLabel)] = "Theme",
            [nameof(ThemeTooltip)] = "Preferred user interface theme",
            [nameof(LanguageLabel)] = "Language",
            [nameof(LanguageTooltip)] = "Preferred user interface language",
            [nameof(StartWithWindowsLabel)] = "Start with Windows",
            [nameof(StartWithWindowsTooltip)] = "Launch LightBulb at Windows startup",
            [nameof(StartMinimizedLabel)] = "Start minimized",
            [nameof(StartMinimizedTooltip)] = "Keep the main window hidden in the system tray when LightBulb starts.",
            [nameof(TrayLeftClickActionLabel)] = "Tray icon left click:",
            [nameof(TrayLeftClickActionTooltip)] = "Choose what a left click on the Windows tray icon does. Right click always opens the tray menu. Profile cycling includes Automatic as part of the cycle.",
            [nameof(LightBulbEnabledLabel)] = "LightBulb enabled",
            [nameof(LightBulbEnabledTooltip)] =
                "Master on/off switch. This is the same state controlled by the tray Enable/Disable item and the Toggle LightBulb shortcut.",
            [nameof(DefaultToDayConfigLabel)] = "Default to day-time configuration",
            [nameof(DefaultToDayConfigTooltip)] =
                "When LightBulb is disabled or paused, restore the configured day-time temperature and brightness instead of the default monitor gamma",
            [nameof(DayNightCycleLabel)] = "Day/night schedule",
            [nameof(DayNightCycleTooltip)] =
                "Automatically switch between the configured day and night looks using the selected sunrise/sunset method. When disabled, Automatic stays on the Day configuration continuously; profiles are unaffected.",
            [nameof(PauseWhenFullscreenLabel)] = "Pause when fullscreen",
            [nameof(PauseWhenFullscreenTooltip)] =
                "Pause LightBulb when any fullscreen window is in the foreground",
            [nameof(GammaSmoothingLabel)] = "Gamma smoothing",
            [nameof(GammaSmoothingTooltip)] =
                "Transition slowly when enabling or disabling LightBulb to give time for eyes to adjust",
            [nameof(GammaPollingLabel)] = "Gamma polling",
            [nameof(GammaPollingTooltip)] =
                "Force-refresh monitor gamma at regular intervals to prevent other programs from overriding it",
            [nameof(TrayTemperatureStepLabel)] = "Manual color step (K):",
            [nameof(TrayTemperatureStepTooltip)] =
                "Temperature change for one normal tray wheel notch, Color quick-control scroll, or temperature hotkey press (25-1000 K)",
            [nameof(TrayBrightnessStepLabel)] = "Manual brightness step:",
            [nameof(TrayBrightnessStepTooltip)] =
                "Gamma-brightness change for one normal tray wheel notch, Brightness quick-control scroll, or brightness hotkey press (1-25%)",
            [nameof(TrayColorWheelModifierLabel)] = "Tray wheel color modifier:",
            [nameof(TrayColorWheelModifierTooltip)] =
                "Modifier held while scrolling directly over the tray icon to change color temperature. If the same modifier is selected for brightness, brightness automatically moves to another available modifier.",
            [nameof(TrayWheelModifierLabel)] = "Tray wheel brightness modifier:",
            [nameof(TrayWheelModifierTooltip)] =
                "Modifier held while scrolling directly over the tray icon to change brightness. If the same modifier is selected for color, color automatically moves to another available modifier.",
            [nameof(TrayFineWheelHint)] =
                "Tray icon: hold the color modifier to change color, or the brightness modifier to change brightness. Over the Color/Brightness quick-control rows, the row under the pointer is adjusted directly. Hold Shift additionally for fine control when Shift is not assigned to either tray action (25 K / 1%).",
            // General settings tab
            [nameof(DayTemperatureLabel)] = "Day-time color temperature:",
            [nameof(DayTemperatureTooltip)] = "Color temperature during the day",
            [nameof(NightTemperatureLabel)] = "Night-time color temperature:",
            [nameof(NightTemperatureTooltip)] = "Color temperature during the night",
            [nameof(DayBrightnessLabel)] = "Day-time brightness:",
            [nameof(DayBrightnessTooltip)] = """
                Brightness during the day

                Note that this brightness setting applies to the color gamma, not to the actual brightness of the monitor.
                If your computer is already capable of auto-adjusting screen brightness based on lighting conditions (common for laptops), then it's recommended to disable LightBulb's brightness control by keeping both brightness settings at 100%.
                """,
            [nameof(NightBrightnessLabel)] = "Night-time brightness:",
            [nameof(NightBrightnessTooltip)] = """
                Brightness during the night

                Note that this brightness setting applies to the color gamma, not to the actual brightness of the monitor.
                If your computer is already capable of auto-adjusting screen brightness based on lighting conditions (common for laptops), then it's recommended to disable LightBulb's brightness control by keeping both brightness settings at 100%.
                """,
            [nameof(TransitionDurationLabel)] = "Transition duration:",
            [nameof(TransitionDurationTooltip)] =
                "Duration of time it takes to switch between day-time and night-time configurations",
            [nameof(TransitionOffsetLabel)] = "Transition offset:",
            [nameof(TransitionOffsetTooltip)] =
                "Offset that specifies how early or late the transition starts, relative to the sunrise and sunset",
            // Profile settings tab
            [nameof(ProfileEditorHeader)] = "Profile",
            [nameof(ProfileNameLabel)] = "Name:",
            [nameof(ProfileTooltipLabel)] = "Tooltip:",
            [nameof(ProfileTooltipWatermark)] = "Short description shown in the tray menu",
            [nameof(ProfileTemperatureLabel)] = "Color temperature:",
            [nameof(ProfileBrightnessLabel)] = "Brightness:",
            [nameof(ProfileShortcutLabel)] = "Shortcut:",
            [nameof(ProfileAddButton)] = "ADD",
            [nameof(ProfileDuplicateButton)] = "COPY",
            [nameof(ProfileRemoveButton)] = "REMOVE",
            [nameof(ProfileMoveUpButton)] = "MOVE UP",
            [nameof(ProfileMoveDownButton)] = "MOVE DOWN",
            [nameof(ProfileUpdateFromCurrentButton)] = "CAPTURE CURRENT",
            [nameof(ProfileActivateButton)] = "ACTIVATE",
            [nameof(ProfileAutomaticButton)] = "AUTOMATIC",
            [nameof(ProfileNoProfilesText)] = "Create a profile to store a fixed color temperature, brightness, and optional shortcut.",
            [nameof(ProfileHintText)] = "Automatic follows the Day/night schedule when it is enabled; otherwise it stays on Day. ADD stores the current on-screen look. CAPTURE CURRENT updates the selected profile from the current look, including temporary tray adjustments. Profile order is also the cycling order.",
            [nameof(ProfileManageMenuItem)] = "Manage profiles...",
            // Tone profile settings tab
            [nameof(ToneProfileEditorHeader)] = "Tone profile",
            [nameof(ToneProfileNameLabel)] = "Name:",
            [nameof(ToneProfileTooltipWatermark)] = "Short description of what this tone profile changes",
            [nameof(ToneProfileAddButton)] = "ADD",
            [nameof(ToneProfileDuplicateButton)] = "COPY",
            [nameof(ToneProfileRemoveButton)] = "REMOVE",
            [nameof(ToneProfileMoveUpButton)] = "MOVE UP",
            [nameof(ToneProfileMoveDownButton)] = "MOVE DOWN",
            [nameof(ToneProfileActivateButton)] = "ACTIVATE",
            [nameof(ToneProfileOffButton)] = "Disable tones",
            [nameof(ToneProfileCaptureCurrentButton)] = "CAPTURE CURRENT",
            [nameof(ToneProfileNoProfilesText)] = "Create a tone profile to shape dark tones, midtones, and highlights independently on top of any normal profile.",
            [nameof(ToneProfileHintText)] = "Tone profiles store color/brightness deltas plus an independent 0–100% intensity for each band. The saved default target is used outside the tray profile selector; choosing a profile from the tray starts on Highlights. Tone-band rows change intensity only. Highlight-only presets target bright values, not semantic white pixels, so very bright colors can also be affected.",
            [nameof(ToneProfileManageMenuItem)] = "Manage tone profiles...",
            [nameof(ToneDarkTonesLabel)] = "Dark tones",
            [nameof(ToneMidtonesLabel)] = "Midtones",
            [nameof(ToneHighlightsLabel)] = "Highlights",
            [nameof(ToneAllBandsLabel)] = "All tone bands",
            [nameof(ToneTemperatureOffsetLabel)] = "Color offset:",
            [nameof(ToneBrightnessOffsetLabel)] = "Brightness offset:",
            [nameof(ToneIntensityLabel)] = "Intensity:",
            [nameof(ToneDefaultTargetLabel)] = "Default tray target:",
            [nameof(ToneDefaultTargetTooltip)] = "Chooses which tone band the tray Color/Brightness rows edit when this profile is activated.",
            [nameof(ToneDarkTonesHelpTooltip)] = "Adjusts shadows and darker brightness values.",
            [nameof(ToneMidtonesHelpTooltip)] = "Adjusts middle brightness values.",
            [nameof(ToneHighlightsHelpTooltip)] = "Adjusts bright areas such as white and light backgrounds.",
            [nameof(ToneIntensityTooltip)] = "Scales how strongly this band's color and brightness offsets are applied.",
            // Location settings tab
            [nameof(SolarConfigLabel)] = "Solar configuration:",
            [nameof(ManualLabel)] = "Manual",
            [nameof(ManualTooltip)] = "Configure sunrise and sunset manually",
            [nameof(LocationBasedLabel)] = "Location-based",
            [nameof(LocationBasedTooltip)] =
                "Configure your location and use it to automatically calculate the sunrise and sunset times",
            [nameof(SunriseTimeLabel)] = "Sunrise:",
            [nameof(SunsetTimeLabel)] = "Sunset:",
            [nameof(YourLocationLabel)] = "Your location:",
            [nameof(AutoDetectLocationTooltip)] =
                "Try to detect the location automatically based on your IP address",
            [nameof(LocationQueryTooltip)] = """
                Specify your location using geographic coordinates or a search query

                Examples of valid inputs:
                **41.25, -120.9762**
                **41.25°N, 120.9762°W**
                **New York, USA**
                **Germany**
                """,
            [nameof(SetLocationTooltip)] = "Set location",
            [nameof(LocationErrorText)] = "Error resolving location, try again",
            // Hot key settings tab
            [nameof(ToggleLightBulbHotkeyLabel)] = "Toggle LightBulb",
            [nameof(ToggleLightBulbHotkeyTooltip)] = "Global hotkey to toggle LightBulb on/off",
            [nameof(ToggleWindowLabel)] = "Toggle window",
            [nameof(ToggleWindowHotkeyTooltip)] =
                "Global hotkey to show/hide LightBulb's main window",
            [nameof(IncreaseTemperatureOffsetLabel)] = "Temperature offset ↑",
            [nameof(IncreaseTemperatureOffsetTooltip)] =
                "Global hotkey to increase the current temperature offset",
            [nameof(DecreaseTemperatureOffsetLabel)] = "Temperature offset ↓",
            [nameof(DecreaseTemperatureOffsetTooltip)] =
                "Global hotkey to decrease the current temperature offset",
            [nameof(IncreaseBrightnessOffsetLabel)] = "Brightness offset ↑",
            [nameof(IncreaseBrightnessOffsetTooltip)] =
                "Global hotkey to increase the current brightness offset",
            [nameof(DecreaseBrightnessOffsetLabel)] = "Brightness offset ↓",
            [nameof(DecreaseBrightnessOffsetTooltip)] =
                "Global hotkey to decrease the current brightness offset",
            [nameof(ResetOffsetLabel)] = "Reset offset",
            [nameof(ResetOffsetHotkeyTooltip)] =
                "Global hotkey to reset the current temperature and brightness offsets",
            [nameof(HotKeyInputHint)] =
                "Click a shortcut field: it starts blank. After a combination is captured it stays focused, so you can immediately press another combination to replace it. Click elsewhere to finish. Use × (or Delete/Backspace/Escape) to clear it.",
            [nameof(HotKeyRegistrationError)] =
                "One or more shortcuts could not be activated:",
            [nameof(HotKeyConflictWarning)] =
                "Possible shortcut conflict detected:",
            [nameof(CycleProfilesLabel)] = "Cycle profiles",
            [nameof(CycleProfilesTooltip)] = "Global hotkey to switch Automatic → saved profiles → Automatic",
            [nameof(ToggleLastProfileLabel)] = "Toggle Automatic / last profile",
            [nameof(ToggleLastProfileTooltip)] =
                "Global hotkey to jump between the automatic schedule and the last profile you used",
            // Application whitelist settings tab
            [nameof(AppWhitelistLabel)] = "Application whitelist",
            [nameof(RefreshAppsTooltip)] = "Refresh running applications",
            [nameof(PauseForWhitelistedTooltip)] =
                "Pause LightBulb when one of the selected applications is in the foreground",
            // Tray icon context menu
            [nameof(QuickAdjustTitle)] = "Quick adjustment",
            [nameof(QuickAdjustProfileLabel)] = "Current profile:",
            [nameof(QuickAdjustTemperatureLabel)] = "Color temperature",
            [nameof(QuickAdjustBrightnessLabel)] = "Brightness",
            [nameof(QuickAdjustResetButton)] = "Reset to current profile",
            [nameof(TrayShowMenuItem)] = "Show",
            [nameof(TrayHideMenuItem)] = "Hide",
            [nameof(TraySettingsMenuItem)] = "Settings",
            [nameof(TrayProfilesMenuItem)] = "Profiles",
            [nameof(TrayAutomaticProfileMenuItem)] = "Automatic",
            [nameof(TrayToneProfilesMenuItem)] = "Tone profile",
            [nameof(TrayTonesMenuItem)] = "Tone",
            [nameof(TrayToneOffMenuItem)] = "Off",
            [nameof(TrayToneDisableMenuItem)] = "Disable tones",
            [nameof(TrayToneEnableMenuItem)] = "Enable tones",
            [nameof(TrayToneCustomMenuItem)] = "Custom",
            [nameof(TrayToneMixedValue)] = "mixed",
            [nameof(TrayToneStatusPrefix)] = "Tone",
            [nameof(TrayToneResetMenuItem)] = "Reset tone adjustments",
            [nameof(TrayToneSaveExistingMenuItem)] = "Save to existing tone profile",
            [nameof(TrayToneSaveNewMenuItem)] = "Save as new tone profile...",
            [nameof(TrayToneWheelHintMenuItem)] = "Tone rows: scroll to change intensity · Shift+scroll for 1% steps",
            [nameof(TrayToneTargetTooltip)] = "Selected tone target. Scroll to change its intensity; click to disable or restore all tone shaping; hover for tone controls.",
            [nameof(TrayToneOffTargetTooltip)] = "Disables all tone shaping while remembering the current tone settings.",
            [nameof(TrayToneDisableTooltip)] = "Disables all tone shaping and remembers the current profile, target, and intensity.",
            [nameof(TrayToneEnableTooltip)] = "Restores the last tone profile, target, and intensity.",
            [nameof(TrayDarkTonesTooltip)] = "Targets shadows and darker values. Scroll to change this band's intensity.",
            [nameof(TrayMidtonesTooltip)] = "Targets middle brightness levels. Scroll to change this band's intensity.",
            [nameof(TrayHighlightsTooltip)] = "Targets bright areas such as light backgrounds. Scroll to change this band's intensity.",
            [nameof(TrayAllToneBandsTooltip)] = "Changes the intensity of dark tones, midtones, and highlights together.",
            [nameof(TrayToneResetTooltip)] = "Restores the active tone profile's saved tone values without changing the current target.",
            [nameof(TrayToneProfileOffTooltip)] = "Tone shaping is disabled; the normal profile is unchanged.",
            [nameof(TrayCustomToneProfileTooltip)] = "Unsaved custom tone adjustments layered over the normal profile.",
            [nameof(TrayAutomaticProfileTooltip)] = "Follows the configured day/night schedule.",
            [nameof(TraySelectedToneTemperatureControlFormat)] = "Color ↕: {0} K",
            [nameof(TraySelectedToneTemperatureMixedControlFormat)] = "Color ↕: {0}",
            [nameof(TraySelectedToneBrightnessControlFormat)] = "Brightness ↕: {0}%",
            [nameof(TraySelectedToneBrightnessMixedControlFormat)] = "Brightness ↕: {0}",
            [nameof(TrayToneTemperatureControlFormat)] = "Color ↕ [{0}]: {1} K",
            [nameof(TrayToneTemperatureMixedControlFormat)] = "Color ↕ [{0}]: {1}",
            [nameof(TrayToneBrightnessControlFormat)] = "Brightness ↕ [{0}]: {1}%",
            [nameof(TrayToneBrightnessMixedControlFormat)] = "Brightness ↕ [{0}]: {1}",
            [nameof(TrayCycleProfilesMenuItem)] = "Next profile: {0}",
            [nameof(TrayToggleLastProfileMenuItem)] = "Toggle Automatic / last profile",
            [nameof(TrayAdjustMenuItem)] = "Color / brightness",
            [nameof(TrayCoolerMenuItem)] = "Cooler",
            [nameof(TrayWarmerMenuItem)] = "Warmer",
            [nameof(TrayBrighterMenuItem)] = "Brighter",
            [nameof(TrayDimmerMenuItem)] = "Dimmer",
            [nameof(TrayResetProfileMenuItem)] = "Reset manual adjustments",
            [nameof(TrayWheelHintMenuItem)] = "Tray icon: {0}: color · {1}: brightness",
            [nameof(TrayTemperatureControlFormat)] = "Color ↕: {0} K ({1} K / {2}%)",
            [nameof(TrayTemperatureControlTooltip)] = "Hover and scroll to adjust color temperature. If Tones targets a band, scrolling edits that band instead of the base profile. Hold Shift for 25 K steps. Click to open the base-profile touch-friendly quick adjustment panel.",
            [nameof(TrayBrightnessControlFormat)] = "Brightness ↕: {0}% ({1}%)",
            [nameof(TrayBrightnessControlTooltip)] = "Hover and scroll to adjust brightness. If Tones targets a band, scrolling edits that band instead of the base profile. Hold Shift for 1% steps. Click to open the base-profile touch-friendly quick adjustment panel.",
            [nameof(TraySaveCurrentMenuItem)] = "Save current look...",
            [nameof(TraySaveCurrentAsDayMenuItem)] = "Use as Day configuration",
            [nameof(TraySaveCurrentAsNightMenuItem)] = "Use as Night configuration",
            [nameof(TraySaveCurrentToProfileMenuItem)] = "Update active profile",
            [nameof(TrayEnableMenuItem)] = "Enable",
            [nameof(TrayDisableMenuItem)] = "Disable",
            [nameof(TrayDisableTemporarilyMenuItem)] = "Disable...",
            [nameof(TrayDisableUntilSunriseMenuItem)] = "Until sunrise",
            [nameof(TrayDisableFor1DayMenuItem)] = "For 1 day",
            [nameof(TrayDisableFor12HoursMenuItem)] = "For 12 hours",
            [nameof(TrayDisableFor6HoursMenuItem)] = "For 6 hours",
            [nameof(TrayDisableFor3HoursMenuItem)] = "For 3 hours",
            [nameof(TrayDisableFor1HourMenuItem)] = "For 1 hour",
            [nameof(TrayDisableFor30MinutesMenuItem)] = "For 30 minutes",
            [nameof(TrayDisableFor15MinutesMenuItem)] = "For 15 minutes",
            [nameof(TrayDisableFor5MinutesMenuItem)] = "For 5 minutes",
            [nameof(TrayDisableFor1MinuteMenuItem)] = "For 1 minute",
            [nameof(TrayExitMenuItem)] = "Exit",
            [nameof(TrayTooltipDisabled)] = "Disabled",
            // Log settings tab
            [nameof(LogHeader)] = "Warnings and errors",
            [nameof(LogHint)] = "Only warnings and errors are stored. Times use your local time zone; the log is persisted under LocalAppData and includes shortcut/tray failures and fatal application errors.",
            [nameof(LogRefreshButton)] = "REFRESH",
            [nameof(LogCopyButton)] = "COPY",
            [nameof(LogClearButton)] = "CLEAR",
            // Dialog messages
            [nameof(CloseButton)] = "CLOSE",
            [nameof(LimitedGammaRangeTitle)] = "Limited gamma range",
            [nameof(LimitedGammaRangeMessage)] = """
                {0} has detected that extended gamma range controls are not enabled on this system.
                This may cause some color configurations to not work correctly.

                Press FIX to unlock the gamma range. Administrator privileges may be required.
                """,
            [nameof(FixButton)] = "FIX",
        };
}
