using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using System.IO;
using System.Text.Json;
using Localite;
using CommunityToolkit.Mvvm.ComponentModel;
using LightBulb.Core;
using LightBulb.Framework;
using LightBulb.Localization;
using LightBulb.Models;
using LightBulb.PlatformInterop;
using Microsoft.Win32;

namespace LightBulb.Services;

public partial class SettingsService : ObservableObject, IDisposable
{
    private const string SettingsKey = "settings";
    private StartupService? _startupService;
    private readonly object _saveGate = new();
    private readonly string? _legacySettingsPath;
    private ILocaliteStore? _store;
    private string? _readFailure;
    private bool _disposed;

    // Deserialization creates an in-memory snapshot, not another database connection.
    [JsonConstructor]
    public SettingsService() { }

    public SettingsService(string legacySettingsPath)
    {
        _legacySettingsPath = Path.GetFullPath(legacySettingsPath);
    }

    private string LegacySettingsPath =>
        _legacySettingsPath ?? Path.GetFullPath(StartOptions.Current.SettingsPath);

    private string DatabasePath
    {
        get
        {
            var path = LegacySettingsPath;
            var database = Path.ChangeExtension(path, ".db3");
            return string.Equals(path, database, StringComparison.OrdinalIgnoreCase)
                ? path + ".settings.db3"
                : database;
        }
    }

    private ILocaliteStore Store
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _store ??= LocaliteStore.Create(new LocaliteStoreOptions
            {
                ApplicationId = "LightBulb",
                Directory = Path.GetDirectoryName(DatabasePath),
                DbName = Path.GetFileName(DatabasePath),
                AutoExpire = false,
                CleanupOnStart = false,
                AutoExpireTimeOut = TimeSpan.Zero,
                // Localite reports some SQLite read errors through its logger and returns null.
                // Do not mistake an unsuccessful read for missing settings.
                SqlLogger = message =>
                {
                    if (message.StartsWith("LocaliteStore GetInternal", StringComparison.Ordinal)
                        && message.Contains(" failed:", StringComparison.Ordinal))
                        _readFailure = message;
                },
            });
        }
    }

    private void CopySettingsFrom(SettingsService source)
    {
        // Keep the existing observable instance and all UI bindings intact. The JSON contract
        // excludes registry-backed settings and read-only limits, just like the old serializer.
        foreach (var property in SerializerContext.Default.SettingsService.Properties)
        {
            if (property.Get is { } get && property.Set is { } set)
                set(this, get(source));
        }
    }

    private void SaveSnapshot() =>
        Store.Add(SettingsKey, this, SerializerContext.Default.SettingsService);

    private SettingsService? ReadSnapshot(out bool importedLegacy)
    {
        importedLegacy = false;
        _readFailure = null;
        var snapshot = Store.Get(SettingsKey, SerializerContext.Default.SettingsService);
        if (_readFailure is not null)
            throw new IOException("Unable to read the LightBulb settings database.");
        if (snapshot is not null)
            return snapshot;

        // Import the original JSON only if no SQLite record exists. Retain the original file
        // unchanged for rollback, even after successful migration.
        try
        {
            using var stream = File.OpenRead(LegacySettingsPath);
            snapshot = JsonSerializer.Deserialize(stream, SerializerContext.Default.SettingsService)
                ?? throw new JsonException("The legacy settings file does not contain an object.");
            importedLegacy = true;
            return snapshot;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        lock (_saveGate)
        {
            if (_disposed)
                return;
            _disposed = true;
            _store?.Dispose();
            _store = null;
        }
    }

    private readonly RegistrySwitch<int> _extendedGammaRangeSwitch = new(
        RegistryHive.LocalMachine,
        @"Software\Microsoft\Windows NT\CurrentVersion\ICM",
        "GdiICMGammaRange",
        256
    );



    [ObservableProperty]
    [JsonIgnore]
    public partial bool IsExtendedGammaRangeUnlocked { get; set; }

    public double MinimumTemperature => 500;
    public double MaximumTemperature => 20_000;
    public double MinimumBrightness => 0.1;
    public double MaximumBrightness => 1;
    public double MinimumToneTemperatureOffset => -5000;
    public double MaximumToneTemperatureOffset => 5000;
    public double MinimumToneBrightnessOffset => -0.9;
    public double MaximumToneBrightnessOffset => 0.9;
    public double MinimumToneIntensity => 0;
    public double MaximumToneIntensity => 1;

    [ObservableProperty]
    public partial ColorConfiguration DayConfiguration { get; set; } = new(6600, 1);

    [ObservableProperty]
    public partial ColorConfiguration NightConfiguration { get; set; } = new(3900, 0.85);

    [ObservableProperty]
    public partial TimeSpan ConfigurationTransitionDuration { get; set; } = TimeSpan.FromMinutes(40);

    [ObservableProperty]
    public partial double ConfigurationTransitionOffset { get; set; }

    [ObservableProperty]
    public partial TimeSpan ConfigurationSmoothingMaxDuration { get; set; } = TimeSpan.FromSeconds(5);

    [ObservableProperty]
    public partial bool IsManualSunriseSunsetEnabled { get; set; } = true;

    [ObservableProperty]
    [JsonPropertyName("ManualSunriseTime")]
    public partial TimeOnly ManualSunrise { get; set; } = new(07, 20);

    [ObservableProperty]
    [JsonPropertyName("ManualSunsetTime")]
    public partial TimeOnly ManualSunset { get; set; } = new(16, 30);

    [ObservableProperty]
    public partial GeoLocation? Location { get; set; }

    [ObservableProperty]
    public partial ThemeVariant Theme { get; set; } = ThemeVariant.Dark;

    [ObservableProperty]
    public partial Language Language { get; set; }

    [ObservableProperty]
    [JsonIgnore]
    public partial bool IsAutoStartEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsStartMinimizedEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsDefaultToDayConfigurationEnabled { get; set; }

    // Global schedule switch. When disabled, Automatic uses the configured day look continuously.
    [ObservableProperty]
    public partial bool IsDayNightCycleEnabled { get; set; } = true;

    [ObservableProperty]
    public partial bool IsConfigurationSmoothingEnabled { get; set; } = true;

    [ObservableProperty]
    public partial bool IsPauseWhenFullScreenEnabled { get; set; }

    [ObservableProperty]
    public partial bool IsGammaPollingEnabled { get; set; }

    // Manual tray/hotkey adjustment steps.
    [ObservableProperty]
    public partial double TrayTemperatureWheelStep { get; set; } = 100;

    [ObservableProperty]
    public partial double TrayBrightnessWheelStep { get; set; } = 0.05;

    [ObservableProperty]
    public partial TrayWheelModifier TrayColorWheelModifier { get; set; } = TrayWheelModifier.Shift;

    [ObservableProperty]
    public partial TrayWheelModifier TrayBrightnessWheelModifier { get; set; } = TrayWheelModifier.Control;

    [ObservableProperty]
    public partial TrayLeftClickAction TrayLeftClickAction { get; set; } = LightBulb.Models.TrayLeftClickAction.ShowAppWindow;

    [ObservableProperty]
    public partial bool IsApplicationWhitelistEnabled { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ExternalApplication>? WhitelistedApplications { get; set; }

    [ObservableProperty]
    public partial HotKey ToggleHotKey { get; set; }

    [ObservableProperty]
    [JsonPropertyName("FocusWindowHotKey")]
    public partial HotKey ToggleWindowHotKey { get; set; }

    [ObservableProperty]
    public partial HotKey IncreaseTemperatureOffsetHotKey { get; set; }

    [ObservableProperty]
    public partial HotKey DecreaseTemperatureOffsetHotKey { get; set; }

    [ObservableProperty]
    public partial HotKey IncreaseBrightnessOffsetHotKey { get; set; }

    [ObservableProperty]
    public partial HotKey DecreaseBrightnessOffsetHotKey { get; set; }

    [ObservableProperty]
    public partial HotKey ResetConfigurationOffsetHotKey { get; set; }

    [ObservableProperty]
    public partial HotKey CycleProfilesHotKey { get; set; }

    [ObservableProperty]
    public partial HotKey ToggleLastProfileHotKey { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<DisplayProfile> Profiles { get; set; } = [];

    // One-time migration marker so existing installations also receive the starter profiles.
    [ObservableProperty]
    public partial bool HasSeededDefaultProfiles { get; set; }

    [ObservableProperty]
    public partial Guid? ActiveProfileId { get; set; }

    [ObservableProperty]
    public partial Guid? LastProfileId { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ToneProfile> ToneProfiles { get; set; } = [];

    // One-time migration marker so existing installations receive the starter tone profiles once.
    [ObservableProperty]
    public partial bool HasSeededDefaultToneProfiles { get; set; }

    // 0.20 adds two highlight-only presets without re-adding any older defaults the user removed.
    [ObservableProperty]
    public partial bool HasSeededToneProfilesV020 { get; set; }

    // 0.20 adds editable profile tooltips. This marker populates descriptions once for known
    // built-in profile names while preserving custom/cleared descriptions afterwards.
    [ObservableProperty]
    public partial bool HasMigratedProfileTooltipsV020 { get; set; }

    // One-time migration marker from the original 0.19 tone schema, where band intensity did not
    // exist and deserialization would otherwise yield 0 for the new numeric member.
    [ObservableProperty]
    public partial bool HasMigratedToneIntensity { get; set; }

    [ObservableProperty]
    public partial Guid? ActiveToneProfileId { get; set; }

    // Tone shaping can be disabled without destroying the working tone values. This lets the tray
    // Tone row behave as a real enable/disable switch and restore the exact previous intensity and
    // band when it is enabled again.
    [ObservableProperty]
    public partial bool IsToneLayerEnabled { get; set; }

    [ObservableProperty]
    public partial Guid? LastToneProfileId { get; set; }

    // Migrates 0.19 settings where "Off" only meant "edit the base profile" even though tonal
    // shaping could still be active. 0.20 makes Off a true tone-layer disable state.
    [ObservableProperty]
    public partial bool HasMigratedToneEnableV020 { get; set; }

    // The active tone layer is a working copy. It can diverge from the selected saved tone profile
    // until the user explicitly updates that profile, exactly like base-profile manual offsets.
    [ObservableProperty]
    public partial ToneAdjustmentConfiguration ToneAdjustments { get; set; } =
        ToneAdjustmentConfiguration.Off;

    // Selects which tone band the main Color/Brightness tray wheel rows edit while tones are
    // enabled. The target is retained while tones are disabled so re-enabling restores the same
    // band without changing its saved color/brightness/intensity values.
    [ObservableProperty]
    public partial ToneBand? SelectedToneBand { get; set; }

    // Clicking the main Tone tray row disables/enables the whole tone layer. Keep the most recently
    // selected target so the user's place is restored exactly when tones are enabled again.
    [ObservableProperty]
    public partial ToneBand LastSelectedToneBand { get; set; } = ToneBand.Highlights;

    // Runtime manual adjustments are persisted separately from profile definitions. This lets an
    // unsaved custom look survive an application restart without silently overwriting the profile.
    [ObservableProperty]
    public partial double ManualTemperatureOffset { get; set; }

    [ObservableProperty]
    public partial double ManualBrightnessOffset { get; set; }

    public void AttachStartupService(StartupService startupService)
    {
        _startupService = startupService;
    }

    public void Reset()
    {
        CopySettingsFrom(new SettingsService());
        IsAutoStartEnabled = false;
        IsStartMinimizedEnabled = false;
        Profiles = CreateStarterProfiles();
        HasSeededDefaultProfiles = true;
        ActiveProfileId = null;
        LastProfileId = null;
        ToneProfiles = CreateStarterToneProfiles();
        HasSeededDefaultToneProfiles = true;
        HasSeededToneProfilesV020 = true;
        HasMigratedProfileTooltipsV020 = true;
        HasMigratedToneIntensity = true;
        HasMigratedToneEnableV020 = true;
        ActiveToneProfileId = null;
        LastToneProfileId = null;
        IsToneLayerEnabled = false;
        ToneAdjustments = ToneAdjustmentConfiguration.Off;
        SelectedToneBand = null;
        LastSelectedToneBand = ToneBand.Highlights;
        ManualTemperatureOffset = 0;
        ManualBrightnessOffset = 0;
        OnPropertyChanged(string.Empty);
    }

    public void Save()
    {
#if DEBUG
        IsAutoStartEnabled = false;
#endif
        lock (_saveGate)
            SaveSnapshot();

        try
        {
            _extendedGammaRangeSwitch.IsSet = IsExtendedGammaRangeUnlocked;
            _startupService?.SetEnabled(IsAutoStartEnabled, IsStartMinimizedEnabled);
        }
        catch (Win32Exception)
        {
            // Registry updates are best-effort; failure must not break settings persistence.
        }

        OnPropertyChanged(string.Empty);
    }

    public bool Load()
    {
        lock (_saveGate)
            return LoadCore();
    }

    private bool LoadCore()
    {
        var snapshot = ReadSnapshot(out var importedLegacy);
        var wasLoaded = snapshot is not null;
        if (snapshot is not null)
            CopySettingsFrom(snapshot);

        // This fork is fixed-dark. Preserve the serialized property for backward-compatible
        // settings JSON, but do not let an old Light/System value re-enable runtime theme mutation.
        Theme = ThemeVariant.Dark;

        IsExtendedGammaRangeUnlocked = _extendedGammaRangeSwitch.IsSet;
        IsAutoStartEnabled = _startupService?.IsEnabled() ?? false;

        Profiles ??= [];
        ToneProfiles ??= [];

        var seededDefaultProfiles = false;
        if (!HasSeededDefaultProfiles)
        {
            if (Profiles.Count == 0)
                Profiles = CreateStarterProfiles();

            HasSeededDefaultProfiles = true;
            seededDefaultProfiles = true;
        }

        var seededDefaultToneProfiles = false;
        if (!HasSeededDefaultToneProfiles)
        {
            if (ToneProfiles.Count == 0)
                ToneProfiles = CreateStarterToneProfiles();

            HasSeededDefaultToneProfiles = true;
            seededDefaultToneProfiles = true;
        }

        var seededToneProfilesV020 = false;
        if (!HasSeededToneProfilesV020)
        {
            var additions = CreateV020ToneProfiles()
                .Where(candidate =>
                    !ToneProfiles.Any(existing =>
                        string.Equals(existing.Name, candidate.Name, StringComparison.OrdinalIgnoreCase)
                    )
                )
                .ToArray();

            if (additions.Length > 0)
                ToneProfiles = [.. ToneProfiles, .. additions];

            HasSeededToneProfilesV020 = true;
            seededToneProfilesV020 = true;
        }

        var migratedProfileTooltipsV020 = false;
        if (!HasMigratedProfileTooltipsV020)
        {
            Profiles = Profiles
                .Select(profile =>
                    string.IsNullOrWhiteSpace(profile.Tooltip)
                        ? profile with { Tooltip = GetBuiltInProfileTooltip(profile.Name) }
                        : profile
                )
                .ToArray();

            ToneProfiles = ToneProfiles
                .Select(profile =>
                    string.IsNullOrWhiteSpace(profile.Tooltip)
                        ? profile with { Tooltip = GetBuiltInToneProfileTooltip(profile.Name) }
                        : profile
                )
                .ToArray();

            HasMigratedProfileTooltipsV020 = true;
            migratedProfileTooltipsV020 = true;
        }

        var migratedToneIntensity = false;
        if (!HasMigratedToneIntensity)
        {
            // A fresh install has just created starter profiles with intentional per-band
            // intensities, so do not flatten those to 100%. Only older persisted tone data needs
            // the schema migration from missing intensity values.
            if (!seededDefaultToneProfiles)
            {
                ToneProfiles = ToneProfiles
                    .Where(profile => profile is not null)
                    .Select(profile => profile with
                    {
                        Adjustments = SetToneIntensity(profile.Adjustments, 1),
                    })
                    .ToArray();
                ToneAdjustments = SetToneIntensity(ToneAdjustments, 1);
            }

            HasMigratedToneIntensity = true;
            migratedToneIntensity = true;
        }

        var migratedToneEnableV020 = false;
        if (!HasMigratedToneEnableV020)
        {
            // Preserve the visible 0.19 result on upgrade: a selected tone profile or non-zero
            // working tone curve was visibly active even when the old tray target said Off.
            IsToneLayerEnabled = ActiveToneProfileId is not null || !ToneAdjustments.IsOff;
            LastToneProfileId = ActiveToneProfileId;

            if (IsToneLayerEnabled && SelectedToneBand is null)
                SelectedToneBand = ToneBand.Highlights;

            HasMigratedToneEnableV020 = true;
            migratedToneEnableV020 = true;
        }

        Profiles = Profiles
            .Where(profile => profile is not null)
            .Select((profile, index) =>
                profile with
                {
                    Id = profile.Id == Guid.Empty ? Guid.NewGuid() : profile.Id,
                    Name = string.IsNullOrWhiteSpace(profile.Name) ? $"Profile {index + 1}" : profile.Name.Trim(),
                    Temperature = double.IsFinite(profile.Temperature)
                        ? Math.Clamp(profile.Temperature, MinimumTemperature, MaximumTemperature)
                        : NightConfiguration.Temperature,
                    Brightness = double.IsFinite(profile.Brightness)
                        ? Math.Clamp(profile.Brightness, MinimumBrightness, MaximumBrightness)
                        : NightConfiguration.Brightness,
                    Tooltip = (profile.Tooltip ?? string.Empty).Trim(),
                }
            )
            .GroupBy(profile => profile.Id)
            .Select(group => group.First())
            .ToArray();

        ToneProfiles = ToneProfiles
            .Where(profile => profile is not null)
            .Select((profile, index) =>
                profile with
                {
                    Id = profile.Id == Guid.Empty ? Guid.NewGuid() : profile.Id,
                    Name = string.IsNullOrWhiteSpace(profile.Name)
                        ? $"Tone profile {index + 1}"
                        : profile.Name.Trim(),
                    Adjustments = ClampToneAdjustments(profile.Adjustments),
                    SelectedBand = profile.SelectedBand is { } selectedBand && Enum.IsDefined(selectedBand)
                        ? selectedBand
                        : ToneBand.Highlights,
                    Tooltip = (profile.Tooltip ?? string.Empty).Trim(),
                }
            )
            .GroupBy(profile => profile.Id)
            .Select(group => group.First())
            .ToArray();

        ToneAdjustments = ClampToneAdjustments(ToneAdjustments);
        if (SelectedToneBand is { } selectedToneBand && !Enum.IsDefined(selectedToneBand))
            SelectedToneBand = null;
        if (!Enum.IsDefined(LastSelectedToneBand))
            LastSelectedToneBand = ToneBand.Highlights;
        if (SelectedToneBand is { } currentToneBand)
            LastSelectedToneBand = currentToneBand;

        TrayTemperatureWheelStep = double.IsFinite(TrayTemperatureWheelStep)
            ? Math.Clamp(TrayTemperatureWheelStep, 25, 1000)
            : 100;
        TrayBrightnessWheelStep = double.IsFinite(TrayBrightnessWheelStep)
            ? Math.Clamp(TrayBrightnessWheelStep, 0.01, 0.25)
            : 0.05;

        if (!Enum.IsDefined(TrayColorWheelModifier))
            TrayColorWheelModifier = TrayWheelModifier.Shift;
        if (!Enum.IsDefined(TrayBrightnessWheelModifier))
            TrayBrightnessWheelModifier = TrayWheelModifier.Control;
        if (TrayColorWheelModifier == TrayBrightnessWheelModifier)
            TrayColorWheelModifier = GetAlternativeTrayWheelModifier(TrayBrightnessWheelModifier);

        if (!Enum.IsDefined<LightBulb.Models.TrayLeftClickAction>(TrayLeftClickAction))
            TrayLeftClickAction = LightBulb.Models.TrayLeftClickAction.ShowAppWindow;

        if (ActiveProfileId is { } activeProfileId && !Profiles.Any(p => p.Id == activeProfileId))
            ActiveProfileId = null;
        if (LastProfileId is { } lastProfileId && !Profiles.Any(p => p.Id == lastProfileId))
            LastProfileId = null;
        if (
            ActiveToneProfileId is { } activeToneProfileId
            && !ToneProfiles.Any(p => p.Id == activeToneProfileId)
        )
        {
            ActiveToneProfileId = null;
        }
        if (
            LastToneProfileId is { } lastToneProfileId
            && !ToneProfiles.Any(p => p.Id == lastToneProfileId)
        )
        {
            LastToneProfileId = null;
        }

        if (IsToneLayerEnabled)
        {
            if (SelectedToneBand is null)
                SelectedToneBand = LastSelectedToneBand;
        }
        else
        {
            // Disabled means no tone profile is active. Keep the last profile separately so the
            // user can re-enable the exact working tone without losing custom intensity changes.
            if (ActiveToneProfileId is { } activeToneId)
                LastToneProfileId = activeToneId;
            ActiveToneProfileId = null;
        }

        ManualTemperatureOffset = double.IsFinite(ManualTemperatureOffset)
            ? Math.Clamp(
                ManualTemperatureOffset,
                MinimumTemperature - MaximumTemperature,
                MaximumTemperature - MinimumTemperature
            )
            : 0;
        ManualBrightnessOffset = double.IsFinite(ManualBrightnessOffset)
            ? Math.Clamp(
                ManualBrightnessOffset,
                MinimumBrightness - MaximumBrightness,
                MaximumBrightness - MinimumBrightness
            )
            : 0;

        // Persist the one-time seed marker immediately so starter profiles are not recreated after
        // a user deletes them and exits without opening Settings again. Avoid the override here so
        // registry switches are not needlessly touched during loading.
        if (
            importedLegacy
            || !wasLoaded
            || seededDefaultProfiles
            || seededDefaultToneProfiles
            || seededToneProfilesV020
            || migratedProfileTooltipsV020
            || migratedToneIntensity
            || migratedToneEnableV020
        )
        {
            lock (_saveGate)
                SaveSnapshot();
        }

        OnPropertyChanged(string.Empty);
        return wasLoaded;
    }

    /// <summary>
    /// Persists runtime-only state without touching startup/registry configuration. This is used by
    /// the debounced manual-adjustment checkpoint so tray/touch scrubbing never rewrites the Run key.
    /// </summary>
    public bool TrySaveRuntimeState()
    {
        try
        {
            lock (_saveGate)
                SaveSnapshot();
            return true;
        }
        catch
        {
            // Runtime adjustment persistence is best-effort. Explicit Settings Save retains its
            // existing error behavior and remains authoritative for user-visible settings changes.
            return false;
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

    private static ToneAdjustmentConfiguration SetToneIntensity(
        ToneAdjustmentConfiguration adjustments,
        double intensity
    ) =>
        new(
            adjustments.DarkTones.WithIntensity(intensity),
            adjustments.Midtones.WithIntensity(intensity),
            adjustments.Highlights.WithIntensity(intensity)
        );

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
            double.IsFinite(adjustment.TemperatureOffset)
                ? Math.Clamp(
                    adjustment.TemperatureOffset,
                    MinimumToneTemperatureOffset,
                    MaximumToneTemperatureOffset
                )
                : 0,
            double.IsFinite(adjustment.BrightnessOffset)
                ? Math.Clamp(
                    adjustment.BrightnessOffset,
                    MinimumToneBrightnessOffset,
                    MaximumToneBrightnessOffset
                )
                : 0,
            double.IsFinite(adjustment.Intensity)
                ? Math.Clamp(adjustment.Intensity, MinimumToneIntensity, MaximumToneIntensity)
                : 1
        );

    private static IReadOnlyList<ToneProfile> CreateStarterToneProfiles() =>
        [
            new ToneProfile(
                Guid.NewGuid(),
                "White-page soften",
                new ToneAdjustmentConfiguration(
                    ToneBandAdjustment.None,
                    new ToneBandAdjustment(-200, -0.03, 1),
                    new ToneBandAdjustment(-900, -0.15, 1)
                ),
                ToneBand.Highlights,
                GetBuiltInToneProfileTooltip("White-page soften")
            ),
            new ToneProfile(
                Guid.NewGuid(),
                "Soft highlights",
                new ToneAdjustmentConfiguration(
                    ToneBandAdjustment.None,
                    new ToneBandAdjustment(-100, -0.01, 0.50),
                    new ToneBandAdjustment(-500, -0.08, 0.70)
                ),
                ToneBand.Highlights,
                GetBuiltInToneProfileTooltip("Soft highlights")
            ),
            new ToneProfile(
                Guid.NewGuid(),
                "Warm highlights",
                new ToneAdjustmentConfiguration(
                    ToneBandAdjustment.None,
                    new ToneBandAdjustment(-150, 0, 0.60),
                    new ToneBandAdjustment(-700, -0.03, 0.85)
                ),
                ToneBand.Highlights,
                GetBuiltInToneProfileTooltip("Warm highlights")
            ),
            new ToneProfile(
                Guid.NewGuid(),
                "Shadow warmth",
                new ToneAdjustmentConfiguration(
                    new ToneBandAdjustment(-700, -0.04, 0.85),
                    new ToneBandAdjustment(-250, -0.02, 0.50),
                    ToneBandAdjustment.None
                ),
                ToneBand.DarkTones,
                GetBuiltInToneProfileTooltip("Shadow warmth")
            ),
            new ToneProfile(
                Guid.NewGuid(),
                "Soft contrast",
                new ToneAdjustmentConfiguration(
                    new ToneBandAdjustment(0, 0.04, 0.75),
                    ToneBandAdjustment.None,
                    new ToneBandAdjustment(0, -0.08, 0.75)
                ),
                ToneBand.AllToneBands,
                GetBuiltInToneProfileTooltip("Soft contrast")
            ),
            new ToneProfile(
                Guid.NewGuid(),
                "Dark-room",
                new ToneAdjustmentConfiguration(
                    new ToneBandAdjustment(-400, -0.06, 0.75),
                    new ToneBandAdjustment(-700, -0.12, 0.85),
                    new ToneBandAdjustment(-1200, -0.20, 1)
                ),
                ToneBand.AllToneBands,
                GetBuiltInToneProfileTooltip("Dark-room")
            ),
            .. CreateV020ToneProfiles(),
        ];

    private static IReadOnlyList<ToneProfile> CreateV020ToneProfiles() =>
        [
            new ToneProfile(
                Guid.NewGuid(),
                "White glare dim",
                new ToneAdjustmentConfiguration(
                    ToneBandAdjustment.None,
                    ToneBandAdjustment.None,
                    new ToneBandAdjustment(0, -0.15, 1)
                ),
                ToneBand.Highlights,
                GetBuiltInToneProfileTooltip("White glare dim")
            ),
            new ToneProfile(
                Guid.NewGuid(),
                "White glare dim strong",
                new ToneAdjustmentConfiguration(
                    ToneBandAdjustment.None,
                    ToneBandAdjustment.None,
                    new ToneBandAdjustment(0, -0.25, 1)
                ),
                ToneBand.Highlights,
                GetBuiltInToneProfileTooltip("White glare dim strong")
            ),
        ];

    private static IReadOnlyList<DisplayProfile> CreateStarterProfiles() =>
        [
            new DisplayProfile(
                Guid.NewGuid(),
                "Warm evening",
                3400,
                0.85,
                HotKey.None,
                GetBuiltInProfileTooltip("Warm evening")
            ),
            new DisplayProfile(
                Guid.NewGuid(),
                "Late night",
                2700,
                0.65,
                HotKey.None,
                GetBuiltInProfileTooltip("Late night")
            ),
            new DisplayProfile(
                Guid.NewGuid(),
                "Reading",
                4200,
                0.90,
                HotKey.None,
                GetBuiltInProfileTooltip("Reading")
            ),
            new DisplayProfile(
                Guid.NewGuid(),
                "Neutral",
                5000,
                1.00,
                HotKey.None,
                GetBuiltInProfileTooltip("Neutral")
            ),
        ];

    private static string GetBuiltInProfileTooltip(string? name) =>
        name?.Trim() switch
        {
            "Warm evening" => "Warm, slightly dimmer light for relaxed evening use.",
            "Late night" => "Very warm and dim to reduce glare late at night.",
            "Reading" => "Comfortable warm-white light with mild dimming for reading.",
            "Neutral" => "Neutral color and full brightness for color-sensitive content.",
            _ => string.Empty,
        };

    private static string GetBuiltInToneProfileTooltip(string? name) =>
        name?.Trim() switch
        {
            "White-page soften" => "Warms and dims bright areas to make white pages less harsh.",
            "Soft highlights" => "Gently reduces bright areas while keeping shadows mostly unchanged.",
            "Warm highlights" => "Adds warmth mainly to bright areas without heavily dimming them.",
            "Shadow warmth" => "Warms darker areas more than bright areas.",
            "Soft contrast" => "Lifts darker tones and dims highlights for a softer contrast.",
            "Dark-room" => "Warms and dims the full range, strongest in bright areas.",
            "White glare dim" => "Dims the highlight band for white pages; very bright colors can dim too.",
            "White glare dim strong" => "Stronger highlight-only dimming for harsh white pages; bright colors can dim too.",
            _ => string.Empty,
        };

}

public partial class SettingsService
{
    [JsonSourceGenerationOptions(
        GenerationMode = JsonSourceGenerationMode.Metadata,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip)]
    [JsonSerializable(typeof(SettingsService))]
    private partial class SerializerContext : JsonSerializerContext;
}
