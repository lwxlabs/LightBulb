using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using LightBulb.Core;
using LightBulb.Models;
using LightBulb.ViewModels.Components;
using SystemProcess = System.Diagnostics.Process;

namespace LightBulb.Services;

/// <summary>
/// Wheel interaction for the tray icon and the wheel-adjustable rows in Avalonia's tray popup.
/// Avalonia 12.1.1 renders NativeMenuItem entries as real Avalonia MenuItem controls inside a
/// TrayPopupRoot/MenuFlyoutPresenter. Menu scrolling therefore uses Avalonia routed input directly;
/// the Win32 low-level hook is retained only for scrolling over the notification-area icon itself.
/// </summary>
public sealed class TrayWheelService(
    DashboardViewModel dashboard,
    SettingsService settingsService,
    AppLogService logService
) : IDisposable
{
    public const string TemperatureControlToken = "LightBulb.Tray.Temperature";
    public const string BrightnessControlToken = "LightBulb.Tray.Brightness";
    public const string ToneCurrentControlToken = "LightBulb.Tray.Tone.Current";
    public const string ToneOffControlToken = "LightBulb.Tray.Tone.Off";
    public const string ToneResetControlToken = "LightBulb.Tray.Tone.Reset";
    public const string ToneDarkControlToken = "LightBulb.Tray.Tone.Dark";
    public const string ToneMidtonesControlToken = "LightBulb.Tray.Tone.Midtones";
    public const string ToneHighlightsControlToken = "LightBulb.Tray.Tone.Highlights";
    public const string ToneAllControlToken = "LightBulb.Tray.Tone.All";
    public const double FineTemperatureStep = 25;
    public const double FineBrightnessStep = 0.01;

    private const int WhMouseLl = 14;
    private const int WmMouseWheel = 0x020A;
    private const uint WmTrayMouse = 0x0400 + 1024; // WM_USER + 1024, matches Avalonia TrayIconImpl
    private const int WmRButtonUp = 0x0205;
    private const int VkControl = 0x11;
    private const int VkShift = 0x10;
    private const int VkAlt = 0x12;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;
    private const int WheelDelta = 120;

    private HookProc? _hookProc;
    private IDisposable? _menuWheelSubscription;
    private IDisposable? _menuPointerSubscription;
    private nint _hook;
    private nint _trayWindow;
    private uint _trayIconId;
    private int _wheelRemainder;
    private bool _disposed;
    private bool _loggedPopupWheel;
    private bool _loggedTrayIconWheel;

    public bool Attach(TrayIcon trayIcon)
    {
        _ = trayIcon;

        if (_disposed || !OperatingSystem.IsWindows())
            return false;

        // This is the important popup-menu path. Avalonia's NativeMenuBarPresenter binds the
        // NativeMenuItem.CommandParameter onto the generated MenuItem, so the two tokenized rows in
        // App.axaml can be identified without reflection, HWND guessing, or private Avalonia fields.
        if (_menuWheelSubscription is null)
        {
            _menuWheelSubscription = InputElement.PointerWheelChangedEvent.AddClassHandler<MenuItem>(
                OnMenuItemPointerWheelChanged,
                RoutingStrategies.Tunnel | RoutingStrategies.Direct | RoutingStrategies.Bubble,
                handledEventsToo: true
            );
            logService.Info(
                nameof(TrayWheelService),
                "Avalonia tray-popup MenuItem wheel class handler registered."
            );
        }

        if (_menuPointerSubscription is null)
        {
            _menuPointerSubscription = InputElement.PointerPressedEvent.AddClassHandler<MenuItem>(
                OnMenuItemPointerPressed,
                RoutingStrategies.Tunnel | RoutingStrategies.Bubble,
                handledEventsToo: true
            );
            logService.Info(
                nameof(TrayWheelService),
                "Avalonia tray-popup MenuItem pointer class handler registered."
            );
        }

        if (_hook != 0)
            return true;

        _hookProc = HookCallback;
        using var process = SystemProcess.GetCurrentProcess();
        using var module = process.MainModule;
        var moduleHandle = module?.ModuleName is { Length: > 0 } moduleName
            ? GetModuleHandle(moduleName)
            : 0;

        _hook = SetWindowsHookEx(WhMouseLl, _hookProc, moduleHandle, 0);
        if (_hook != 0)
        {
            logService.Info(
                nameof(TrayWheelService),
                "Tray wheel handlers registered (Avalonia menu rows + Windows tray icon)."
            );
            return true;
        }

        var error = Marshal.GetLastWin32Error();
        Debug.WriteLine($"Tray icon wheel gestures are unavailable: Win32 error {error}.");
        logService.Warning(
            nameof(TrayWheelService),
            $"Tray icon wheel hook could not be registered. Windows error {error}. " +
            "Scrolling the Color/Brightness menu rows remains available."
        );
        _hookProc = null;
        return false;
    }

    /// <summary>
    /// Opens Avalonia's existing tray NativeMenu on Windows. Avalonia's Win32 TrayIconImpl uses
    /// Win32Platform.Instance.Handle plus the tray icon uID and listens for WM_USER + 1024 with
    /// lParam = WM_RBUTTONUP. Posting that same callback keeps one authoritative menu instead of
    /// maintaining a duplicate popup implementation.
    /// </summary>
    public bool ShowTrayMenu()
    {
        if (_disposed || !OperatingSystem.IsWindows())
            return false;

        if (!GetCursorPos(out var point))
            return false;

        if ((_trayWindow == 0 || _trayIconId == 0) && !TryResolveTrayIdentityAtPoint(point))
        {
            logService.Warning(
                nameof(TrayWheelService),
                "Could not resolve the LightBulb tray icon to open its menu from left click."
            );
            return false;
        }

        if (!PostMessage(_trayWindow, WmTrayMouse, (nint)_trayIconId, (nint)WmRButtonUp))
        {
            var error = Marshal.GetLastWin32Error();
            logService.Warning(
                nameof(TrayWheelService),
                $"Could not request the tray menu from Avalonia. Windows error {error}."
            );
            return false;
        }

        return true;
    }

    private void OnMenuItemPointerWheelChanged(MenuItem menuItem, PointerWheelEventArgs args)
    {
        if (_disposed)
            return;

        var token = ResolveMenuToken(menuItem);

        var target = token switch
        {
            TemperatureControlToken => WheelTarget.Temperature,
            ToneCurrentControlToken => !dashboard.IsToneEnabled || dashboard.SelectedToneBand is null
                ? WheelTarget.None
                : WheelTarget.ToneCurrent,
            BrightnessControlToken => WheelTarget.Brightness,
            ToneDarkControlToken => WheelTarget.ToneDark,
            ToneMidtonesControlToken => WheelTarget.ToneMidtones,
            ToneHighlightsControlToken => WheelTarget.ToneHighlights,
            ToneAllControlToken => WheelTarget.ToneAll,
            _ => ResolveTargetFromHeader(menuItem.Header),
        };

        if (target == WheelTarget.None)
            return;

        var direction = Math.Sign(args.Delta.Y);
        if (direction == 0)
            return;

        var fine = args.KeyModifiers.HasFlag(KeyModifiers.Shift);
        ApplyAdjustment(target, direction, fine);

        if (!_loggedPopupWheel)
        {
            _loggedPopupWheel = true;
            logService.Info(
                nameof(TrayWheelService),
                $"Tray popup wheel input received on {target} row."
            );
        }

        // The exact hovered UI element owns the
        // wheel event and marks it handled so the menu presenter cannot consume/route it elsewhere.
        args.Handled = true;
    }


    private void OnMenuItemPointerPressed(MenuItem menuItem, PointerPressedEventArgs args)
    {
        if (_disposed)
            return;

        var token = ResolveMenuToken(menuItem);
        if (
            token is ToneOffControlToken
                or ToneDarkControlToken
                or ToneMidtonesControlToken
                or ToneHighlightsControlToken
                or ToneAllControlToken
                or ToneResetControlToken
        )
        {
            // Keep the Tone submenu open while choosing/resetting a target so several adjustments
            // can be made without reopening it after every click.
            menuItem.StaysOpenOnClick = true;
        }

        if (token != ToneCurrentControlToken)
            return;

        var point = args.GetCurrentPoint(menuItem);
        if (point.Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed)
            return;

        // MenuItem.Click is only raised for items without a submenu, so the parent Tone row uses
        // pointer input for its requested click-to-toggle behavior while still retaining its submenu.
        dashboard.ToggleSelectedToneBand();
        dashboard.RefreshTrayQuickControls();
        settingsService.TrySaveRuntimeState();
    }

    private static string? ResolveMenuToken(MenuItem menuItem)
    {
        var token = menuItem.CommandParameter as string;
        if (token is null && menuItem.DataContext is NativeMenuItem nativeItem)
            token = nativeItem.CommandParameter as string;
        return token;
    }


    private WheelTarget ResolveTargetFromHeader(object? header)
    {
        var text = header?.ToString();
        if (string.IsNullOrWhiteSpace(text))
            return WheelTarget.None;

        // Avalonia's Windows tray implementation converts NativeMenuItem objects into managed
        // MenuItem controls. Header is part of that public bridge and is therefore a safe fallback
        // if CommandParameter propagation changes or is affected by a binding issue.
        if (string.Equals(text, dashboard.TrayTemperatureControlText, StringComparison.Ordinal))
            return WheelTarget.Temperature;
        if (string.Equals(text, dashboard.TrayBrightnessControlText, StringComparison.Ordinal))
            return WheelTarget.Brightness;
        if (string.Equals(text, dashboard.TrayTonesMenuText, StringComparison.Ordinal))
            return !dashboard.IsToneEnabled || dashboard.SelectedToneBand is null
                ? WheelTarget.None
                : WheelTarget.ToneCurrent;
        if (string.Equals(text, dashboard.TrayDarkTonesControlText, StringComparison.Ordinal))
            return WheelTarget.ToneDark;
        if (string.Equals(text, dashboard.TrayMidtonesControlText, StringComparison.Ordinal))
            return WheelTarget.ToneMidtones;
        if (string.Equals(text, dashboard.TrayHighlightsControlText, StringComparison.Ordinal))
            return WheelTarget.ToneHighlights;
        if (string.Equals(text, dashboard.TrayAllToneBandsControlText, StringComparison.Ordinal))
            return WheelTarget.ToneAll;

        return WheelTarget.None;
    }

    private nint HookCallback(int code, nint wParam, nint lParam)
    {
        if (code < 0 || _disposed || wParam != WmMouseWheel)
            return CallNextHookEx(_hook, code, wParam, lParam);

        // The global hook is deliberately scoped to the actual notification-area icon. Popup menu
        // rows are handled above by Avalonia's own PointerWheelChanged routed event.
        if (!IsPointerOverLightBulbTrayIcon())
        {
            _wheelRemainder = 0;
            return CallNextHookEx(_hook, code, wParam, lParam);
        }

        var colorModifierPressed = IsConfiguredModifierPressed(settingsService.TrayColorWheelModifier);
        var brightnessModifierPressed = IsConfiguredModifierPressed(settingsService.TrayBrightnessWheelModifier);

        // The two configured modifiers are kept distinct by SettingsService. If both are held,
        // brightness wins deterministically; with neither held, leave the wheel event alone.
        var target = brightnessModifierPressed
            ? WheelTarget.Brightness
            : colorModifierPressed
                ? WheelTarget.Temperature
                : WheelTarget.None;

        if (target == WheelTarget.None)
        {
            _wheelRemainder = 0;
            return CallNextHookEx(_hook, code, wParam, lParam);
        }

        if (!_loggedTrayIconWheel)
        {
            _loggedTrayIconWheel = true;
            logService.Info(
                nameof(TrayWheelService),
                $"Notification-area tray icon wheel input received ({target})."
            );
        }

        var shiftIsTrayModifier =
            settingsService.TrayColorWheelModifier == TrayWheelModifier.Shift
            || settingsService.TrayBrightnessWheelModifier == TrayWheelModifier.Shift;
        var fine = IsVirtualKeyPressed(VkShift) && !shiftIsTrayModifier;

        var data = Marshal.PtrToStructure<MsllHookStruct>(lParam);
        _wheelRemainder += unchecked((short)((data.MouseData >> 16) & 0xFFFF));

        while (Math.Abs(_wheelRemainder) >= WheelDelta)
        {
            var direction = Math.Sign(_wheelRemainder);
            _wheelRemainder -= direction * WheelDelta;
            var capturedTarget = target;
            var capturedFine = fine;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                ApplyAdjustment(capturedTarget, direction, capturedFine)
            );
        }

        return 1;
    }

    private void ApplyAdjustment(
        WheelTarget target,
        int direction,
        bool fine
    )
    {
        switch (target)
        {
            case WheelTarget.Temperature:
                dashboard.AdjustSelectedToneTemperature(
                    direction * (fine ? FineTemperatureStep : settingsService.TrayTemperatureWheelStep)
                );
                break;
            case WheelTarget.Brightness:
                dashboard.AdjustSelectedToneBrightness(
                    direction * (fine ? FineBrightnessStep : settingsService.TrayBrightnessWheelStep)
                );
                break;
            case WheelTarget.ToneCurrent:
                dashboard.AdjustSelectedToneIntensity(
                    direction * (fine ? FineBrightnessStep : settingsService.TrayBrightnessWheelStep)
                );
                break;
            case WheelTarget.ToneDark:
                ApplyToneIntensity(ToneBand.DarkTones, direction, fine);
                break;
            case WheelTarget.ToneMidtones:
                ApplyToneIntensity(ToneBand.Midtones, direction, fine);
                break;
            case WheelTarget.ToneHighlights:
                ApplyToneIntensity(ToneBand.Highlights, direction, fine);
                break;
            case WheelTarget.ToneAll:
                ApplyToneIntensity(ToneBand.AllToneBands, direction, fine);
                break;
        }

        dashboard.RefreshTrayQuickControls();
    }

    private void ApplyToneIntensity(ToneBand band, int direction, bool fine) =>
        dashboard.AdjustToneIntensity(
            band,
            direction * (fine ? FineBrightnessStep : settingsService.TrayBrightnessWheelStep)
        );

    private static bool IsConfiguredModifierPressed(TrayWheelModifier modifier) =>
        modifier switch
        {
            TrayWheelModifier.Control => IsVirtualKeyPressed(VkControl),
            TrayWheelModifier.Alt => IsVirtualKeyPressed(VkAlt),
            TrayWheelModifier.Shift => IsVirtualKeyPressed(VkShift),
            TrayWheelModifier.Windows => IsVirtualKeyPressed(VkLWin) || IsVirtualKeyPressed(VkRWin),
            _ => false,
        };

    private static bool IsVirtualKeyPressed(int virtualKey) =>
        (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private bool IsPointerOverLightBulbTrayIcon()
    {
        if (!GetCursorPos(out var point))
            return false;

        if (_trayWindow != 0 && _trayIconId != 0)
        {
            var cachedIdentifier = new NotifyIconIdentifier
            {
                CbSize = checked((uint)Marshal.SizeOf<NotifyIconIdentifier>()),
                HWnd = _trayWindow,
                UId = _trayIconId,
            };

            if (Shell_NotifyIconGetRect(ref cachedIdentifier, out var cachedRect) == 0)
                return cachedRect.Contains(point);

            _trayWindow = 0;
            _trayIconId = 0;
        }

        return TryResolveTrayIdentityAtPoint(point);
    }

    private bool TryResolveTrayIdentityAtPoint(WinPoint point)
    {
        var processId = GetCurrentProcessId();
        nint resolvedWindow = 0;
        uint resolvedIconId = 0;

        // Avalonia's Windows TrayIcon registers NOTIFYICONDATA with Win32Platform.Instance.Handle
        // and a monotonically increasing uID. Enumerating this process's windows and asking Windows
        // for each small candidate ID's icon rectangle avoids reflection into Avalonia internals.
        EnumWindows(
            (window, state) =>
            {
                _ = state;
                GetWindowThreadProcessId(window, out var ownerProcessId);
                if (ownerProcessId != processId)
                    return true;

                for (uint iconId = 1; iconId <= 64; iconId++)
                {
                    var identifier = new NotifyIconIdentifier
                    {
                        CbSize = checked((uint)Marshal.SizeOf<NotifyIconIdentifier>()),
                        HWnd = window,
                        UId = iconId,
                    };

                    if (Shell_NotifyIconGetRect(ref identifier, out var rect) == 0 && rect.Contains(point))
                    {
                        resolvedWindow = window;
                        resolvedIconId = iconId;
                        return false;
                    }
                }

                return true;
            },
            0
        );

        if (resolvedWindow == 0 || resolvedIconId == 0)
            return false;

        _trayWindow = resolvedWindow;
        _trayIconId = resolvedIconId;
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _menuWheelSubscription?.Dispose();
        _menuWheelSubscription = null;
        _menuPointerSubscription?.Dispose();
        _menuPointerSubscription = null;

        if (_hook != 0)
        {
            _ = UnhookWindowsHookEx(_hook);
            _hook = 0;
        }

        _hookProc = null;
        _wheelRemainder = 0;
        _trayWindow = 0;
        _trayIconId = 0;
    }

    private enum WheelTarget
    {
        None,
        Temperature,
        Brightness,
        ToneCurrent,
        ToneDark,
        ToneMidtones,
        ToneHighlights,
        ToneAll,
    }

    private delegate nint HookProc(int code, nint wParam, nint lParam);
    private delegate bool EnumWindowsProc(nint window, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct WinPoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly bool Contains(WinPoint point) =>
            point.X >= Left && point.X < Right && point.Y >= Top && point.Y < Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MsllHookStruct
    {
        public WinPoint Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NotifyIconIdentifier
    {
        public uint CbSize;
        public nint HWnd;
        public uint UId;
        public Guid GuidItem;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, HookProc callback, nint module, uint threadId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out WinPoint point);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentProcessId();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);


    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int Shell_NotifyIconGetRect(ref NotifyIconIdentifier identifier, out WinRect iconLocation);
}
