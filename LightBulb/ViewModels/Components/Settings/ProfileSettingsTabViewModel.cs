using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightBulb.Localization;
using LightBulb.Models;
using LightBulb.Services;
using LightBulb.ViewModels.Components;
using PowerKit.Extensions;

namespace LightBulb.ViewModels.Components.Settings;

public partial class ProfileSettingsTabViewModel : SettingsTabViewModelBase
{
    private readonly DashboardViewModel _dashboard;
    private readonly HotKeyService _hotKeyService;
    private readonly IDisposable _eventSubscription;

    public ProfileSettingsTabViewModel(
        SettingsService settingsService,
        LocalizationManager localizationManager,
        DashboardViewModel dashboard,
        HotKeyService hotKeyService
    ) : base(settingsService, localizationManager, 1)
    {
        _dashboard = dashboard;
        _hotKeyService = hotKeyService;
        _hotKeyService.RegistrationStatusChanged += HotKeyService_OnRegistrationStatusChanged;
        _eventSubscription = settingsService.WatchProperty(o => o.Profiles, _ => SynchronizeSelection());
        SelectedProfile = settingsService.Profiles.FirstOrDefault();
    }

    public override string DisplayName => LocalizationManager.ProfilesTabName;
    public System.Collections.Generic.IReadOnlyList<DisplayProfile> Profiles => SettingsService.Profiles;
    public bool HasSelectedProfile => SelectedProfile is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedProfile))]
    [NotifyPropertyChangedFor(nameof(ProfileName))]
    [NotifyPropertyChangedFor(nameof(ProfileTooltip))]
    [NotifyPropertyChangedFor(nameof(ProfileTemperature))]
    [NotifyPropertyChangedFor(nameof(RecommendedMinimumProfileTemperature))]
    [NotifyPropertyChangedFor(nameof(RecommendedMaximumProfileTemperature))]
    [NotifyPropertyChangedFor(nameof(ProfileBrightness))]
    [NotifyPropertyChangedFor(nameof(ProfileHotKey))]
    [NotifyCanExecuteChangedFor(nameof(RemoveProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(DuplicateProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(UpdateSelectedProfileFromCurrentCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveProfileUpCommand))]
    [NotifyCanExecuteChangedFor(nameof(MoveProfileDownCommand))]
    [NotifyCanExecuteChangedFor(nameof(ActivateSelectedProfileCommand))]
    public partial DisplayProfile? SelectedProfile { get; set; }

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

    public double RecommendedMinimumProfileTemperature => Math.Min(2500, ProfileTemperature);
    public double RecommendedMaximumProfileTemperature => Math.Max(6600, ProfileTemperature);

    public double ProfileTemperature
    {
        get => SelectedProfile?.Temperature ?? 3900;
        set
        {
            UpdateSelectedProfile(profile => profile with
            {
                Temperature = value.Clamp(SettingsService.MinimumTemperature, SettingsService.MaximumTemperature),
            });
            OnPropertyChanged(nameof(RecommendedMinimumProfileTemperature));
            OnPropertyChanged(nameof(RecommendedMaximumProfileTemperature));
        }
    }

    public double ProfileBrightness
    {
        get => SelectedProfile?.Brightness ?? 0.85;
        set => UpdateSelectedProfile(profile => profile with
        {
            Brightness = value.Clamp(SettingsService.MinimumBrightness, SettingsService.MaximumBrightness),
        });
    }

    public HotKey ProfileHotKey
    {
        get => SelectedProfile?.HotKey ?? HotKey.None;
        set => UpdateSelectedProfile(profile => profile with { HotKey = value });
    }

    public bool HasRegistrationError => !string.IsNullOrWhiteSpace(_hotKeyService.RegistrationError);
    public string RegistrationErrorText => HasRegistrationError
        ? LocalizationManager.HotKeyRegistrationError + Environment.NewLine + _hotKeyService.RegistrationError
        : string.Empty;

    public bool HasConflictWarning => !string.IsNullOrWhiteSpace(_hotKeyService.ConflictWarning);
    public string ConflictWarningText => HasConflictWarning
        ? LocalizationManager.HotKeyConflictWarning + Environment.NewLine + _hotKeyService.ConflictWarning
        : string.Empty;

    [RelayCommand]
    private void AddProfile()
    {
        var current = _dashboard.TargetConfiguration;
        var profile = new DisplayProfile(Guid.NewGuid(), $"Profile {GetNextProfileNumber()}", current.Temperature, current.Brightness, HotKey.None);
        SettingsService.Profiles = [.. SettingsService.Profiles, profile];
        SelectedProfile = profile;
    }

    [RelayCommand(CanExecute = nameof(HasSelectedProfile))]
    private void RemoveProfile()
    {
        if (SelectedProfile is not { } selected) return;
        SettingsService.Profiles = SettingsService.Profiles.Where(p => p.Id != selected.Id).ToArray();
        if (SettingsService.LastProfileId == selected.Id) SettingsService.LastProfileId = null;
        if (SettingsService.ActiveProfileId == selected.Id) _dashboard.ActivateProfile(null);
        SelectedProfile = SettingsService.Profiles.FirstOrDefault();
    }

    [RelayCommand(CanExecute = nameof(HasSelectedProfile))]
    private void DuplicateProfile()
    {
        if (SelectedProfile is not { } selected) return;
        var copy = selected with { Id = Guid.NewGuid(), Name = GetUniqueCopyName(selected.Name), HotKey = HotKey.None };
        var profiles = SettingsService.Profiles.ToList();
        var index = profiles.FindIndex(p => p.Id == selected.Id);
        profiles.Insert(index >= 0 ? index + 1 : profiles.Count, copy);
        SettingsService.Profiles = profiles.ToArray();
        SelectedProfile = copy;
    }

    [RelayCommand(CanExecute = nameof(HasSelectedProfile))]
    private void UpdateSelectedProfileFromCurrent()
    {
        if (SelectedProfile is not { } selected) return;
        var current = _dashboard.TargetConfiguration;
        UpdateSelectedProfile(profile => profile with { Temperature = current.Temperature, Brightness = current.Brightness });
        if (SettingsService.ActiveProfileId == selected.Id) _dashboard.ResetToCurrentProfile();
    }

    private bool CanMoveProfileUp() => SelectedProfile is { } selected && SettingsService.Profiles.ToList().FindIndex(p => p.Id == selected.Id) > 0;

    [RelayCommand(CanExecute = nameof(CanMoveProfileUp))]
    private void MoveProfileUp() => MoveSelectedProfile(-1);

    private bool CanMoveProfileDown()
    {
        if (SelectedProfile is not { } selected) return false;
        var index = SettingsService.Profiles.ToList().FindIndex(p => p.Id == selected.Id);
        return index >= 0 && index < SettingsService.Profiles.Count - 1;
    }

    [RelayCommand(CanExecute = nameof(CanMoveProfileDown))]
    private void MoveProfileDown() => MoveSelectedProfile(1);

    private void MoveSelectedProfile(int offset)
    {
        if (SelectedProfile is not { } selected) return;
        var profiles = SettingsService.Profiles.ToList();
        var oldIndex = profiles.FindIndex(p => p.Id == selected.Id);
        var newIndex = oldIndex + offset;
        if (oldIndex < 0 || newIndex < 0 || newIndex >= profiles.Count) return;
        profiles.RemoveAt(oldIndex);
        profiles.Insert(newIndex, selected);
        SettingsService.Profiles = profiles.ToArray();
        SelectedProfile = selected;
    }

    [RelayCommand(CanExecute = nameof(HasSelectedProfile))]
    private void ActivateSelectedProfile()
    {
        if (SelectedProfile is { } selected) _dashboard.ActivateProfile(selected.Id);
    }

    [RelayCommand]
    private void ActivateAutomaticProfile() => _dashboard.ActivateProfile(null);

    private int GetNextProfileNumber()
    {
        var number = 1;
        while (SettingsService.Profiles.Any(p => p.Name == $"Profile {number}")) number++;
        return number;
    }

    private string GetUniqueCopyName(string sourceName)
    {
        var baseName = string.IsNullOrWhiteSpace(sourceName) ? "Profile" : sourceName.Trim();
        var candidate = baseName + " copy";
        var suffix = 2;
        while (SettingsService.Profiles.Any(p => string.Equals(p.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            candidate = $"{baseName} copy {suffix++}";
        return candidate;
    }

    private static string NormalizeName(string? name) => string.IsNullOrWhiteSpace(name) ? "Profile" : name.Trim();
    private static string NormalizeTooltip(string? tooltip) => string.IsNullOrWhiteSpace(tooltip) ? string.Empty : tooltip.Trim();

    private void UpdateSelectedProfile(Func<DisplayProfile, DisplayProfile> update)
    {
        if (SelectedProfile is not { } selected) return;
        var updated = update(selected);
        SettingsService.Profiles = SettingsService.Profiles.Select(p => p.Id == selected.Id ? updated : p).ToArray();
        SelectedProfile = updated;
    }

    private void SynchronizeSelection()
    {
        if (SelectedProfile is { } selected)
            SelectedProfile = SettingsService.Profiles.FirstOrDefault(p => p.Id == selected.Id);
        OnPropertyChanged(nameof(Profiles));
        MoveProfileUpCommand.NotifyCanExecuteChanged();
        MoveProfileDownCommand.NotifyCanExecuteChanged();
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
        {
            _hotKeyService.RegistrationStatusChanged -= HotKeyService_OnRegistrationStatusChanged;
            _eventSubscription.Dispose();
        }
        base.Dispose(disposing);
    }
}
