using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LightBulb.Framework;
using LightBulb.Localization;
using LightBulb.Services;
using LightBulb.Utils.Extensions;
using LightBulb.ViewModels.Components;
using LightBulb.ViewModels.Components.Settings;
using LightBulb.ViewModels.Dialogs;

namespace LightBulb.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly ViewModelManager _viewModelManager;
    private readonly ViewManager _viewManager;
    private readonly DialogManager _dialogManager;
    private readonly SettingsService _settingsService;
    private readonly LocalizationManager _localizationManager;
    private bool _isInitialized;
    private string _profilesMenuSignature = string.Empty;
    private string _toneProfilesMenuSignature = string.Empty;
    private Window? _settingsWindow;
    private SettingsViewModel? _settingsViewModel;
    private bool _allowSettingsWindowClose;

    public MainViewModel(
        DashboardViewModel dashboard,
        ViewModelManager viewModelManager,
        ViewManager viewManager,
        DialogManager dialogManager,
        SettingsService settingsService,
        LocalizationManager localizationManager
    )
    {
        Dashboard = dashboard;
        _viewModelManager = viewModelManager;
        _viewManager = viewManager;
        _dialogManager = dialogManager;
        _settingsService = settingsService;
        _localizationManager = localizationManager;
        LocalizationManager = localizationManager;

        _settingsService.PropertyChanged += SettingsService_OnPropertyChanged;
        _localizationManager.PropertyChanged += LocalizationManager_OnPropertyChanged;
        _profilesMenuSignature = GetProfilesMenuSignature();
        _toneProfilesMenuSignature = GetToneProfilesMenuSignature();
    }

    public LocalizationManager LocalizationManager { get; }
    public DashboardViewModel Dashboard { get; }
    public NativeMenu ProfilesMenu => BuildProfilesMenu();
    public NativeMenu ToneProfilesMenu => BuildToneProfilesMenu();
    public NativeMenu ToneSaveExistingMenu => BuildToneSaveExistingMenu();

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial bool IsSettingsOpen { get; set; }

    private async Task ShowGammaRangeMessageAsync()
    {
        if (_settingsService.IsExtendedGammaRangeUnlocked)
            return;

        var dialog = _viewModelManager.GetMessageBoxViewModel(
            LocalizationManager.LimitedGammaRangeTitle,
            string.Format(LocalizationManager.LimitedGammaRangeMessage, Program.Name),
            LocalizationManager.FixButton,
            LocalizationManager.CloseButton
        );

        if (await _dialogManager.ShowDialogAsync(dialog) != true)
            return;

        _settingsService.IsExtendedGammaRangeUnlocked = true;
        _settingsService.Save();
    }

    private NativeMenu BuildProfilesMenu()
    {
        var menu = new NativeMenu();

        var automaticItem = new NativeMenuItem
        {
            Header = LocalizationManager.TrayAutomaticProfileMenuItem,
            ToolTip = LocalizationManager.TrayAutomaticProfileTooltip,
            ToggleType = MenuItemToggleType.Radio,
            IsChecked = _settingsService.ActiveProfileId is null,
        };
        automaticItem.Click += (_, _) =>
        {
            Dashboard.ActivateProfile(null);
            _settingsService.Save();
        };
        menu.Add(automaticItem);

        if (_settingsService.Profiles.Count > 0)
            menu.Add(new NativeMenuItemSeparator());

        foreach (var profile in _settingsService.Profiles)
        {
            var profileId = profile.Id;
            var item = new NativeMenuItem
            {
                Header = profile.Name,
                ToolTip = profile.Tooltip,
                ToggleType = MenuItemToggleType.Radio,
                IsChecked = _settingsService.ActiveProfileId == profileId,
            };
            item.Click += (_, _) =>
            {
                Dashboard.ActivateProfile(profileId);
                _settingsService.Save();
            };
            menu.Add(item);
        }

        if (_settingsService.Profiles.Count > 0)
        {
            menu.Add(new NativeMenuItemSeparator());

            var nextProfileItem = new NativeMenuItem
            {
                Header = string.Format(
                    LocalizationManager.TrayCycleProfilesMenuItem,
                    GetNextProfileDisplayName()
                ),
            };
            nextProfileItem.Click += (_, _) =>
            {
                Dashboard.CycleProfiles();
                _settingsService.Save();
            };
            menu.Add(nextProfileItem);

            var toggleLastProfileItem = new NativeMenuItem
            {
                Header = LocalizationManager.TrayToggleLastProfileMenuItem,
            };
            toggleLastProfileItem.Click += (_, _) =>
            {
                Dashboard.ToggleLastProfile();
                _settingsService.Save();
            };
            menu.Add(toggleLastProfileItem);
        }

        menu.Add(new NativeMenuItemSeparator());
        var manageProfilesItem = new NativeMenuItem
        {
            Header = LocalizationManager.ProfileManageMenuItem,
        };
        manageProfilesItem.Click += async (_, _) =>
            await ShowSettingsTabAsync<ProfileSettingsTabViewModel>();
        menu.Add(manageProfilesItem);

        return menu;
    }


    private NativeMenu BuildToneProfilesMenu()
    {
        var menu = new NativeMenu();

        var toggleItem = new NativeMenuItem
        {
            Header = Dashboard.TrayToneEnableDisableMenuText,
            ToolTip = Dashboard.TrayToneEnableDisableTooltip,
            IsEnabled = Dashboard.CanEnableToneLayer,
        };
        toggleItem.Click += (_, _) =>
        {
            Dashboard.ToggleToneLayer();
            _settingsService.Save();
        };
        menu.Add(toggleItem);

        if (_settingsService.ToneProfiles.Count > 0)
            menu.Add(new NativeMenuItemSeparator());

        foreach (var profile in _settingsService.ToneProfiles)
        {
            var profileId = profile.Id;
            var item = new NativeMenuItem
            {
                Header = profile.Name,
                ToolTip = profile.Tooltip,
                ToggleType = MenuItemToggleType.Radio,
                IsChecked = _settingsService.IsToneLayerEnabled
                    && _settingsService.ActiveToneProfileId == profileId,
            };
            item.Click += (_, _) =>
            {
                Dashboard.ActivateToneProfile(profileId, selectHighlights: true);
                _settingsService.Save();
            };
            menu.Add(item);
        }

        menu.Add(new NativeMenuItemSeparator());
        var manageItem = new NativeMenuItem
        {
            Header = LocalizationManager.ToneProfileManageMenuItem,
        };
        manageItem.Click += async (_, _) =>
            await ShowSettingsTabAsync<ToneProfileSettingsTabViewModel>();
        menu.Add(manageItem);

        return menu;
    }

    private NativeMenu BuildToneSaveExistingMenu()
    {
        var menu = new NativeMenu();

        if (_settingsService.ToneProfiles.Count == 0)
        {
            menu.Add(new NativeMenuItem
            {
                Header = LocalizationManager.ToneProfileNoProfilesText,
                IsEnabled = false,
            });
            return menu;
        }

        foreach (var profile in _settingsService.ToneProfiles)
        {
            var profileId = profile.Id;
            var item = new NativeMenuItem { Header = profile.Name };
            item.Click += (_, _) =>
            {
                Dashboard.SaveCurrentToneToProfile(profileId);
                _settingsService.Save();
            };
            menu.Add(item);
        }

        return menu;
    }

    private string GetNextProfileDisplayName()
    {
        if (_settingsService.Profiles.Count == 0)
            return LocalizationManager.TrayAutomaticProfileMenuItem;

        if (_settingsService.ActiveProfileId is not { } activeId)
            return _settingsService.Profiles[0].Name;

        var currentIndex = _settingsService.Profiles
            .ToList()
            .FindIndex(profile => profile.Id == activeId);
        if (currentIndex < 0 || currentIndex >= _settingsService.Profiles.Count - 1)
            return LocalizationManager.TrayAutomaticProfileMenuItem;

        return _settingsService.Profiles[currentIndex + 1].Name;
    }

    private string GetProfilesMenuSignature() =>
        (_settingsService.ActiveProfileId?.ToString("N") ?? "automatic")
        + ":"
        + (_settingsService.LastProfileId?.ToString("N") ?? "none")
        + ":"
        + string.Join(
            "|",
            _settingsService.Profiles.Select(profile => $"{profile.Id:N}:{profile.Name}:{profile.Tooltip}")
        );

    private string GetToneProfilesMenuSignature() =>
        (_settingsService.IsToneLayerEnabled ? "enabled" : "disabled")
        + ":"
        + (Dashboard.CanEnableToneLayer ? "restorable" : "empty")
        + ":"
        + (_settingsService.ActiveToneProfileId?.ToString("N") ?? "off")
        + ":"
        + (_settingsService.LastToneProfileId?.ToString("N") ?? "none")
        + ":"
        + string.Join(
            "|",
            _settingsService.ToneProfiles.Select(profile => $"{profile.Id:N}:{profile.Name}:{profile.Tooltip}")
        );

    private void RefreshProfilesMenuIfNeeded(bool force = false)
    {
        var signature = GetProfilesMenuSignature();
        if (!force && signature == _profilesMenuSignature)
            return;

        _profilesMenuSignature = signature;
        OnPropertyChanged(nameof(ProfilesMenu));
    }

    private void RefreshToneMenusIfNeeded(bool force = false)
    {
        var signature = GetToneProfilesMenuSignature();
        if (!force && signature == _toneProfilesMenuSignature)
            return;

        _toneProfilesMenuSignature = signature;
        OnPropertyChanged(nameof(ToneProfilesMenu));
        OnPropertyChanged(nameof(ToneSaveExistingMenu));
    }

    private void SettingsService_OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(SettingsService.Profiles)
            or nameof(SettingsService.ActiveProfileId)
            or nameof(SettingsService.LastProfileId)
            or "")
        {
            RefreshProfilesMenuIfNeeded();
        }

        if (args.PropertyName is nameof(SettingsService.ToneProfiles)
            or nameof(SettingsService.ActiveToneProfileId)
            or nameof(SettingsService.LastToneProfileId)
            or nameof(SettingsService.IsToneLayerEnabled)
            or nameof(SettingsService.ToneAdjustments)
            or "")
        {
            RefreshToneMenusIfNeeded();
        }
    }

    private void LocalizationManager_OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(LocalizationManager.Language) or "")
        {
            RefreshProfilesMenuIfNeeded(true);
            RefreshToneMenusIfNeeded(true);
        }
    }

    public override async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        _isInitialized = true;
        await ShowGammaRangeMessageAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ShowSettingsAsync() =>
        await ShowSettingsTabAsync<GeneralSettingsTabViewModel>();

    public async Task ShowSettingsTabAsync<T>()
        where T : SettingsTabViewModelBase
    {
        // Settings is intentionally a real non-modal resizable window. The old DialogHost-based
        // settings UI could become orphaned when the main window was hidden and leave Settings
        // permanently disabled until restart. Reusing one dedicated window also lets tray/menu
        // requests focus an already-open Settings instance instead of creating another dialog.
        if (_settingsWindow is { } existingWindow && _settingsViewModel is { } existingViewModel)
        {
            existingViewModel.ActivateTab<T>();
            if (!existingWindow.IsVisible)
                existingWindow.Show();
            existingWindow.Activate();
            existingWindow.Focus();
            return;
        }

        var settingsViewModel = _viewModelManager.GetSettingsViewModel();
        settingsViewModel.ActivateTab<T>();
        var settingsView = _viewManager.TryBindView(settingsViewModel);
        if (settingsView is null)
        {
            settingsViewModel.Dispose();
            return;
        }

        var settingsWindow = new Window
        {
            Title = $"{Program.Name} — {LocalizationManager.SettingsText}",
            Width = 1040,
            Height = 720,
            MinWidth = 680,
            MinHeight = 500,
            CanResize = true,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = settingsView,
        };

        _settingsWindow = settingsWindow;
        _settingsViewModel = settingsViewModel;
        IsSettingsOpen = true;

        settingsWindow.Closing += (_, args) =>
        {
            if (_allowSettingsWindowClose)
                return;

            // Treat the title-bar X exactly like Cancel: restore persisted settings and let the
            // normal SettingsViewModel close path complete before the window is destroyed.
            args.Cancel = true;
            settingsViewModel.CancelCommand.Execute(null);
        };

        settingsWindow.Show();
        settingsWindow.Activate();

        try
        {
            await settingsViewModel.WaitForCloseAsync();
        }
        finally
        {
            if (ReferenceEquals(_settingsWindow, settingsWindow))
            {
                _allowSettingsWindowClose = true;
                try
                {
                    settingsWindow.Close();
                }
                finally
                {
                    _allowSettingsWindowClose = false;
                    _settingsWindow = null;
                    _settingsViewModel = null;
                    IsSettingsOpen = false;
                    settingsViewModel.Dispose();
                }
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _settingsService.PropertyChanged -= SettingsService_OnPropertyChanged;
            _localizationManager.PropertyChanged -= LocalizationManager_OnPropertyChanged;

            if (_settingsViewModel is not null)
                _settingsViewModel.CancelCommand.Execute(null);
            if (_settingsWindow is not null)
            {
                _allowSettingsWindowClose = true;
                _settingsWindow.Close();
                _settingsWindow = null;
            }
        }

        base.Dispose(disposing);
    }
}
