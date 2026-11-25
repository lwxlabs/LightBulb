using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightBulb.Core;
using LightBulb.Localization;
using LightBulb.Models;
using LightBulb.Services;
using LightBulb.ViewModels.Components;
using PowerKit.Extensions;

namespace LightBulb.ViewModels.Components.Settings;

public partial class ToneProfileSettingsTabViewModel : SettingsTabViewModelBase
{
    private readonly DashboardViewModel _dashboard;
    private readonly IDisposable _profilesSubscription;
    private readonly IDisposable _activeProfileSubscription;
    private readonly IDisposable _toneEnabledSubscription;

    public ToneProfileSettingsTabViewModel(
        SettingsService settingsService,
        LocalizationManager localizationManager,
        DashboardViewModel dashboard
    ) : base(settingsService, localizationManager, 2)
    {
        _dashboard = dashboard;
        ToneTargetOptions =
        [
            new(ToneBand.DarkTones, localizationManager.ToneDarkTonesLabel),
            new(ToneBand.Midtones, localizationManager.ToneMidtonesLabel),
            new(ToneBand.Highlights, localizationManager.ToneHighlightsLabel),
            new(ToneBand.AllToneBands, localizationManager.ToneAllBandsLabel),
        ];

        _profilesSubscription = settingsService.WatchProperty(
            o => o.ToneProfiles,
            _ => SynchronizeSelection()
        );
        _activeProfileSubscription = settingsService.WatchProperty(
            o => o.ActiveToneProfileId,
            _ => SynchronizeSelection()
        );
        _toneEnabledSubscription = settingsService.WatchProperty(
            o => o.IsToneLayerEnabled,
            _ => OnPropertyChanged(nameof(IsSelectedProfileActive))
        );
        SelectedProfile =
            settingsService.ActiveToneProfileId is { } activeId
                ? settingsService.ToneProfiles.FirstOrDefault(profile => profile.Id == activeId)
                : settingsService.ToneProfiles.FirstOrDefault();
    }

    public override string DisplayName => LocalizationManager.ToneProfilesTabName;

    public IReadOnlyList<ToneProfile> Profiles => SettingsService.ToneProfiles;
    public IReadOnlyList<ToneTargetOption> ToneTargetOptions { get; }
    public bool HasSelectedProfile => SelectedProfile is not null;
    public bool IsSelectedProfileActive =>
        SettingsService.IsToneLayerEnabled
        && SelectedProfile is { } selected
        && SettingsService.ActiveToneProfileId == selected.Id;

    public double MinimumTemperatureOffset => SettingsService.MinimumToneTemperatureOffset;
    public double MaximumTemperatureOffset => SettingsService.MaximumToneTemperatureOffset;
    public double MinimumBrightnessOffset => SettingsService.MinimumToneBrightnessOffset;
    public double MaximumBrightnessOffset => SettingsService.MaximumToneBrightnessOffset;
    public double MinimumIntensity => SettingsService.MinimumToneIntensity;
    public double MaximumIntensity => SettingsService.MaximumToneIntensity;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedProfile))]
    [NotifyPropertyChangedFor(nameof(IsSelectedProfileActive))]
    [NotifyPropertyChangedFor(nameof(ProfileName))]
    [NotifyPropertyChangedFor(nameof(ProfileTooltip))]
    [NotifyPropertyChangedFor(nameof(DefaultToneTarget))]
    [NotifyPropertyChangedFor(nameof(DarkTemperatureOffset))]
    [NotifyPropertyChangedFor(nameof(DarkBrightnessOffset))]
    [NotifyPropertyChangedFor(nameof(DarkIntensity))]
    [NotifyPropertyChangedFor(nameof(MidtoneTemperatureOffset))]
    [NotifyPropertyChangedFor(nameof(MidtoneBrightnessOffset))]
    [NotifyPropertyChangedFor(nameof(MidtoneIntensity))]
    [NotifyPropertyChangedFor(nameof(HighlightTemperatureOffset))]
    [NotifyPropertyChangedFor(nameof(HighlightBrightnessOffset))]
    [NotifyPropertyChangedFor(nameof(HighlightIntensity))]
    [NotifyCanExecuteChangedFor(nameof(RemoveProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(DuplicateProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(CaptureCurrentCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveProfileUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveProfileDownCommand))]
    [NotifyCanExecuteChangedFor(nameof(ActivateSelectedProfileCommand))]
    public partial ToneProfile? SelectedProfile { get; set; }

    public string ProfileName
    {
        get => SelectedProfile?.Name ?? string.Empty;
        set => UpdateSelectedProfile(profile => profile with { Name = NormalizeName(value) });
    }

    public string ProfileTooltip
    {
        get => SelectedProfile?.Tooltip ?? string.Empty;
        set => UpdateSelectedProfile(profile => profile with { Tooltip = NormalizeTooltip(value) });
    }

    public ToneTargetOption? DefaultToneTarget
    {
        get => ToneTargetOptions.FirstOrDefault(option => option.Band == SelectedProfile?.SelectedBand);
        set
        {
            if (value is not null)
                UpdateSelectedProfile(profile => profile with { SelectedBand = value.Band });
        }
    }

    public double DarkTemperatureOffset
    {
        get => SelectedProfile?.Adjustments.DarkTones.TemperatureOffset ?? 0;
        set => UpdateBand(ToneBand.DarkTones, temperatureOffset: value);
    }

    public double DarkBrightnessOffset
    {
        get => SelectedProfile?.Adjustments.DarkTones.BrightnessOffset ?? 0;
        set => UpdateBand(ToneBand.DarkTones, brightnessOffset: value);
    }

    public double DarkIntensity
    {
        get => SelectedProfile?.Adjustments.DarkTones.Intensity ?? 1;
        set => UpdateBand(ToneBand.DarkTones, intensity: value);
    }

    public double MidtoneTemperatureOffset
    {
        get => SelectedProfile?.Adjustments.Midtones.TemperatureOffset ?? 0;
        set => UpdateBand(ToneBand.Midtones, temperatureOffset: value);
    }

    public double MidtoneBrightnessOffset
    {
        get => SelectedProfile?.Adjustments.Midtones.BrightnessOffset ?? 0;
        set => UpdateBand(ToneBand.Midtones, brightnessOffset: value);
    }

    public double MidtoneIntensity
    {
        get => SelectedProfile?.Adjustments.Midtones.Intensity ?? 1;
        set => UpdateBand(ToneBand.Midtones, intensity: value);
    }

    public double HighlightTemperatureOffset
    {
        get => SelectedProfile?.Adjustments.Highlights.TemperatureOffset ?? 0;
        set => UpdateBand(ToneBand.Highlights, temperatureOffset: value);
    }

    public double HighlightBrightnessOffset
    {
        get => SelectedProfile?.Adjustments.Highlights.BrightnessOffset ?? 0;
        set => UpdateBand(ToneBand.Highlights, brightnessOffset: value);
    }

    public double HighlightIntensity
    {
        get => SelectedProfile?.Adjustments.Highlights.Intensity ?? 1;
        set => UpdateBand(ToneBand.Highlights, intensity: value);
    }

    [RelayCommand]
    private void AddProfile()
    {
        var profile = new ToneProfile(
            Guid.NewGuid(),
            $"Tone profile {GetNextProfileNumber()}",
            SettingsService.ToneAdjustments,
            SettingsService.SelectedToneBand
        );
        SettingsService.ToneProfiles = [.. SettingsService.ToneProfiles, profile];
        SelectedProfile = profile;
    }

    [RelayCommand(CanExecute = nameof(HasSelectedProfile))]
    private void RemoveProfile()
    {
        if (SelectedProfile is not { } selected)
            return;

        SettingsService.ToneProfiles = SettingsService.ToneProfiles
            .Where(profile => profile.Id != selected.Id)
            .ToArray();

        if (SettingsService.ActiveToneProfileId == selected.Id)
            _dashboard.ActivateToneProfile(null);
        if (SettingsService.LastToneProfileId == selected.Id)
            SettingsService.LastToneProfileId = null;

        SelectedProfile = SettingsService.ToneProfiles.FirstOrDefault();
    }

    [RelayCommand(CanExecute = nameof(HasSelectedProfile))]
    private void DuplicateProfile()
    {
        if (SelectedProfile is not { } selected)
            return;

        var copy = selected with
        {
            Id = Guid.NewGuid(),
            Name = GetUniqueCopyName(selected.Name),
        };
        var profiles = SettingsService.ToneProfiles.ToList();
        var index = profiles.FindIndex(profile => profile.Id == selected.Id);
        profiles.Insert(index >= 0 ? index + 1 : profiles.Count, copy);
        SettingsService.ToneProfiles = profiles.ToArray();
        SelectedProfile = copy;
    }

    [RelayCommand(CanExecute = nameof(HasSelectedProfile))]
    private void CaptureCurrent()
    {
        if (SelectedProfile is not { } selected)
            return;

        var current = SettingsService.ToneAdjustments;
        var selectedBand = SettingsService.SelectedToneBand;
        SettingsService.ToneProfiles = SettingsService.ToneProfiles
            .Select(profile =>
                profile.Id == selected.Id
                    ? profile with { Adjustments = current, SelectedBand = selectedBand }
                    : profile
            )
            .ToArray();
        SelectedProfile = SettingsService.ToneProfiles.First(profile => profile.Id == selected.Id);

        if (SettingsService.ActiveToneProfileId == selected.Id)
        {
            SettingsService.ToneAdjustments = current;
            SettingsService.SelectedToneBand = selectedBand;
        }
    }

    private bool CanMoveProfileUp() =>
        SelectedProfile is { } selected
        && SettingsService.ToneProfiles.ToList().FindIndex(profile => profile.Id == selected.Id) > 0;

    [RelayCommand(CanExecute = nameof(CanMoveProfileUp))]
    private void MoveProfileUp() => MoveSelectedProfile(-1);

    private bool CanMoveProfileDown()
    {
        if (SelectedProfile is not { } selected)
            return false;

        var index = SettingsService.ToneProfiles.ToList().FindIndex(profile => profile.Id == selected.Id);
        return index >= 0 && index < SettingsService.ToneProfiles.Count - 1;
    }

    [RelayCommand(CanExecute = nameof(CanMoveProfileDown))]
    private void MoveProfileDown() => MoveSelectedProfile(1);

    private void MoveSelectedProfile(int offset)
    {
        if (SelectedProfile is not { } selected)
            return;

        var profiles = SettingsService.ToneProfiles.ToList();
        var oldIndex = profiles.FindIndex(profile => profile.Id == selected.Id);
        var newIndex = oldIndex + offset;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= profiles.Count)
            return;

        profiles.RemoveAt(oldIndex);
        profiles.Insert(newIndex, selected);
        SettingsService.ToneProfiles = profiles.ToArray();
        SelectedProfile = selected;
    }

    [RelayCommand(CanExecute = nameof(HasSelectedProfile))]
    private void ActivateSelectedProfile()
    {
        if (SelectedProfile is { } selected)
            _dashboard.ActivateToneProfile(selected.Id);
    }

    [RelayCommand]
    private void ActivateOff() => _dashboard.ActivateToneProfile(null);

    private void UpdateBand(
        ToneBand band,
        double? temperatureOffset = null,
        double? brightnessOffset = null,
        double? intensity = null
    )
    {
        UpdateSelectedProfile(profile =>
        {
            var existing = profile.Adjustments.GetBand(band);
            var updated = new ToneBandAdjustment(
                (temperatureOffset ?? existing.TemperatureOffset).Clamp(
                    SettingsService.MinimumToneTemperatureOffset,
                    SettingsService.MaximumToneTemperatureOffset
                ),
                (brightnessOffset ?? existing.BrightnessOffset).Clamp(
                    SettingsService.MinimumToneBrightnessOffset,
                    SettingsService.MaximumToneBrightnessOffset
                ),
                (intensity ?? existing.Intensity).Clamp(
                    SettingsService.MinimumToneIntensity,
                    SettingsService.MaximumToneIntensity
                )
            );
            return profile with { Adjustments = profile.Adjustments.WithBand(band, updated) };
        });
    }

    private void UpdateSelectedProfile(Func<ToneProfile, ToneProfile> update)
    {
        if (SelectedProfile is not { } selected)
            return;

        // Follow live edits only while the active tone layer and editing target still match the
        // selected saved profile. A custom tray working copy must never be overwritten implicitly.
        var shouldFollowLiveEdit =
            SettingsService.ActiveToneProfileId == selected.Id
            && SettingsService.ToneAdjustments == selected.Adjustments
            && SettingsService.SelectedToneBand == selected.SelectedBand;

        var updated = update(selected);
        SettingsService.ToneProfiles = SettingsService.ToneProfiles
            .Select(profile => profile.Id == selected.Id ? updated : profile)
            .ToArray();
        SelectedProfile = updated;

        if (shouldFollowLiveEdit)
        {
            SettingsService.ToneAdjustments = updated.Adjustments;
            SettingsService.SelectedToneBand = updated.SelectedBand;
            if (updated.SelectedBand is { } selectedBand)
                SettingsService.LastSelectedToneBand = selectedBand;
        }
    }

    private int GetNextProfileNumber()
    {
        var number = 1;
        while (SettingsService.ToneProfiles.Any(profile => profile.Name == $"Tone profile {number}"))
            number++;
        return number;
    }

    private string GetUniqueCopyName(string sourceName)
    {
        var baseName = string.IsNullOrWhiteSpace(sourceName) ? "Tone profile" : sourceName.Trim();
        var candidate = baseName + " copy";
        var suffix = 2;
        while (
            SettingsService.ToneProfiles.Any(profile =>
                string.Equals(profile.Name, candidate, StringComparison.OrdinalIgnoreCase)
            )
        )
        {
            candidate = $"{baseName} copy {suffix++}";
        }

        return candidate;
    }

    private static string NormalizeName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "Tone profile" : name.Trim();
    private static string NormalizeTooltip(string? tooltip) =>
        string.IsNullOrWhiteSpace(tooltip) ? string.Empty : tooltip.Trim();


    private void SynchronizeSelection()
    {
        if (SelectedProfile is { } selected)
            SelectedProfile = SettingsService.ToneProfiles.FirstOrDefault(profile => profile.Id == selected.Id);

        if (SelectedProfile is null && SettingsService.ToneProfiles.Count > 0)
        {
            SelectedProfile =
                SettingsService.ActiveToneProfileId is { } activeId
                    ? SettingsService.ToneProfiles.FirstOrDefault(profile => profile.Id == activeId)
                    : SettingsService.ToneProfiles.FirstOrDefault();
        }

        OnPropertyChanged(nameof(Profiles));
        OnPropertyChanged(nameof(IsSelectedProfileActive));
        MoveProfileUpCommand.NotifyCanExecuteChanged();
        MoveProfileDownCommand.NotifyCanExecuteChanged();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _profilesSubscription.Dispose();
            _activeProfileSubscription.Dispose();
            _toneEnabledSubscription.Dispose();
        }

        base.Dispose(disposing);
    }
}

public sealed record ToneTargetOption(ToneBand? Band, string Name);
