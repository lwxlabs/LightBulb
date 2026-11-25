using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia.Input;
using Avalonia.Threading;
using LightBulb.Models;
using SystemProcess = System.Diagnostics.Process;

namespace LightBulb.Services;

/// <summary>
/// Global shortcut service backed by a WH_KEYBOARD_LL hook.
/// Windows virtual-key codes are mapped explicitly from Avalonia PhysicalKey values so ordinary
/// letters such as Ctrl+Alt+L do not depend on Avalonia's Win32 key-conversion fallback.
/// </summary>
public sealed class HotKeyService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;

    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;
    private const int VkLShift = 0xA0;
    private const int VkRShift = 0xA1;
    private const int VkLControl = 0xA2;
    private const int VkRControl = 0xA3;
    private const int VkLMenu = 0xA4;
    private const int VkRMenu = 0xA5;

    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;
    private const int ErrorHotKeyAlreadyRegistered = 1409;

    private readonly AppLogService _logService;
    private readonly object _gate = new();
    private readonly List<Registration> _registrations = [];
    private readonly List<string> _registrationErrors = [];
    private readonly List<string> _conflictWarnings = [];
    private readonly HashSet<int> _keysDown = [];
    private readonly HashSet<string> _loggedShortcutMatches = new(StringComparer.Ordinal);
    private readonly LowLevelKeyboardProc _hookProc;

    private nint _hook;
    private bool _disposed;
    private int _probeHotKeyId = 0x4C42;

    public HotKeyService(AppLogService logService)
    {
        _logService = logService;
        _hookProc = HookCallback;
    }

    public event EventHandler? RegistrationStatusChanged;

    public string? RegistrationError
    {
        get
        {
            lock (_gate)
                return _registrationErrors.Count > 0
                    ? string.Join(Environment.NewLine, _registrationErrors)
                    : null;
        }
    }

    public string? ConflictWarning
    {
        get
        {
            lock (_gate)
                return _conflictWarnings.Count > 0
                    ? string.Join(Environment.NewLine, _conflictWarnings)
                    : null;
        }
    }

    public bool RegisterHotKey(HotKey hotKey, Action callback)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (hotKey == HotKey.None)
            return true;

        var virtualKey = ConvertVirtualKey(hotKey.Key);
        if (virtualKey <= 0)
        {
            AddRegistrationError($"{hotKey}: unsupported physical key '{hotKey.Key}'");
            return false;
        }

        if (!EnsureHook())
            return false;

        lock (_gate)
        {
            if (_registrations.Exists(r => r.VirtualKey == virtualKey && r.Modifiers == hotKey.Modifiers))
            {
                var message = $"{hotKey}: duplicate shortcut";
                _registrationErrors.Add(message);
                _logService.Warning(nameof(HotKeyService), message);
                RegistrationStatusChanged?.Invoke(this, EventArgs.Empty);
                return false;
            }

            _registrations.Add(new Registration(virtualKey, hotKey.Modifiers, callback, hotKey));
        }

        ProbeWindowsHotKeyConflict(hotKey, virtualKey);

        _logService.Info(
            nameof(HotKeyService),
            $"Registered shortcut {hotKey} -> VK 0x{virtualKey:X2}."
        );
        return true;
    }

    public void UnregisterAllHotKeys()
    {
        lock (_gate)
        {
            _registrations.Clear();
            _registrationErrors.Clear();
            _conflictWarnings.Clear();
            _keysDown.Clear();
            _loggedShortcutMatches.Clear();
        }

        // SetWindowsHookEx inserts a hook at the beginning of the hook chain. Releasing it here
        // means the first newly registered LightBulb shortcut installs a fresh hook at the front.
        // This materially improves coexistence with applications that also use WH_KEYBOARD_LL and
        // may otherwise consume combinations such as Ctrl+Alt+L before LightBulb sees them.
        StopHook();
        RegistrationStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool EnsureHook()
    {
        if (_hook != 0)
            return true;

        // Initialize the hook using the executable module handle rather
        // than relying on a null hMod for a low-level hook callback implemented in managed code.
        using var process = SystemProcess.GetCurrentProcess();
        using var module = process.MainModule;
        var moduleHandle = module?.ModuleName is { Length: > 0 } moduleName
            ? GetModuleHandle(moduleName)
            : 0;

        _hook = SetWindowsHookEx(WhKeyboardLl, _hookProc, moduleHandle, 0);
        if (_hook != 0)
        {
            _logService.Info(
                nameof(HotKeyService),
                $"Global keyboard shortcut hook registered (module=0x{moduleHandle:X})."
            );
            return true;
        }

        var error = Marshal.GetLastWin32Error();
        var description = new Win32Exception(error).Message;
        AddRegistrationError($"Global shortcut hook: {description} (Windows error {error})");
        return false;
    }

    private nint HookCallback(int code, nint wParam, nint lParam)
    {
        if (code < 0 || _disposed)
            return CallNextHookEx(_hook, code, wParam, lParam);

        var message = wParam.ToInt32();
        var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
        var key = unchecked((int)data.VkCode);

        // Shortcut recording owns the focused key stream. Do not execute or swallow an existing
        // LightBulb shortcut while the user is trying to replace it.
        if (HotKeyCaptureState.IsCapturing)
        {
            if (message is WmKeyUp or WmSysKeyUp)
            {
                lock (_gate)
                    _keysDown.Remove(key);
            }

            return CallNextHookEx(_hook, code, wParam, lParam);
        }

        if (message is WmKeyUp or WmSysKeyUp)
        {
            lock (_gate)
                _keysDown.Remove(key);
            return CallNextHookEx(_hook, code, wParam, lParam);
        }

        if (message is not (WmKeyDown or WmSysKeyDown))
            return CallNextHookEx(_hook, code, wParam, lParam);

        Registration[] registrations;
        bool isRepeat;
        lock (_gate)
        {
            // The hook's own key state is authoritative. GetAsyncKeyState below is a fallback for
            // hardware/macro timing where Windows' async modifier state briefly lags.
            isRepeat = !_keysDown.Add(key);
            registrations = _registrations.ToArray();
        }

        var matched = false;
        foreach (var registration in registrations)
        {
            if (registration.VirtualKey != key || !ModifiersMatch(registration.Modifiers))
                continue;

            matched = true;
            if (!isRepeat)
            {
                var matchSignature = registration.DisplayHotKey.ToString();
                lock (_gate)
                {
                    if (_loggedShortcutMatches.Add(matchSignature))
                    {
                        _logService.Info(
                            nameof(HotKeyService),
                            $"Global shortcut input matched {registration.DisplayHotKey} (VK 0x{key:X2})."
                        );
                    }
                }

                var callback = registration.Callback;
                Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        callback();
                    }
                    catch (Exception ex)
                    {
                        _logService.Error(nameof(HotKeyService), $"Shortcut {registration.DisplayHotKey} failed.", ex);
                    }
                });
            }
        }

        // Swallow only shortcuts that actually matched LightBulb.
        return matched ? 1 : CallNextHookEx(_hook, code, wParam, lParam);
    }

    private bool ModifiersMatch(KeyModifiers expected)
    {
        var control = IsModifierPressed(VkControl, VkLControl, VkRControl);
        var shift = IsModifierPressed(VkShift, VkLShift, VkRShift);
        var alt = IsModifierPressed(VkMenu, VkLMenu, VkRMenu);
        var meta = IsModifierPressed(VkLWin, VkLWin, VkRWin);

        return control == expected.HasFlag(KeyModifiers.Control)
            && shift == expected.HasFlag(KeyModifiers.Shift)
            && alt == expected.HasFlag(KeyModifiers.Alt)
            && meta == expected.HasFlag(KeyModifiers.Meta);
    }

    private bool IsModifierPressed(int genericKey, int leftKey, int rightKey)
    {
        lock (_gate)
        {
            if (_keysDown.Contains(genericKey) || _keysDown.Contains(leftKey) || _keysDown.Contains(rightKey))
                return true;
        }

        return IsPressed(genericKey) || IsPressed(leftKey) || IsPressed(rightKey);
    }

    private static bool IsPressed(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    internal static int ConvertVirtualKey(PhysicalKey key) => key switch
    {
        // Explicit Windows VK mapping. Avalonia 12 names letter physical keys A..Z (not KeyA..KeyZ).
        // Keep this table explicit instead of relying on enum numeric ordering.
        PhysicalKey.A => 0x41,
        PhysicalKey.B => 0x42,
        PhysicalKey.C => 0x43,
        PhysicalKey.D => 0x44,
        PhysicalKey.E => 0x45,
        PhysicalKey.F => 0x46,
        PhysicalKey.G => 0x47,
        PhysicalKey.H => 0x48,
        PhysicalKey.I => 0x49,
        PhysicalKey.J => 0x4A,
        PhysicalKey.K => 0x4B,
        PhysicalKey.L => 0x4C,
        PhysicalKey.M => 0x4D,
        PhysicalKey.N => 0x4E,
        PhysicalKey.O => 0x4F,
        PhysicalKey.P => 0x50,
        PhysicalKey.Q => 0x51,
        PhysicalKey.R => 0x52,
        PhysicalKey.S => 0x53,
        PhysicalKey.T => 0x54,
        PhysicalKey.U => 0x55,
        PhysicalKey.V => 0x56,
        PhysicalKey.W => 0x57,
        PhysicalKey.X => 0x58,
        PhysicalKey.Y => 0x59,
        PhysicalKey.Z => 0x5A,

        PhysicalKey.Digit0 => 0x30,
        PhysicalKey.Digit1 => 0x31,
        PhysicalKey.Digit2 => 0x32,
        PhysicalKey.Digit3 => 0x33,
        PhysicalKey.Digit4 => 0x34,
        PhysicalKey.Digit5 => 0x35,
        PhysicalKey.Digit6 => 0x36,
        PhysicalKey.Digit7 => 0x37,
        PhysicalKey.Digit8 => 0x38,
        PhysicalKey.Digit9 => 0x39,

        PhysicalKey.F1 => 0x70,
        PhysicalKey.F2 => 0x71,
        PhysicalKey.F3 => 0x72,
        PhysicalKey.F4 => 0x73,
        PhysicalKey.F5 => 0x74,
        PhysicalKey.F6 => 0x75,
        PhysicalKey.F7 => 0x76,
        PhysicalKey.F8 => 0x77,
        PhysicalKey.F9 => 0x78,
        PhysicalKey.F10 => 0x79,
        PhysicalKey.F11 => 0x7A,
        PhysicalKey.F12 => 0x7B,
        PhysicalKey.F13 => 0x7C,
        PhysicalKey.F14 => 0x7D,
        PhysicalKey.F15 => 0x7E,
        PhysicalKey.F16 => 0x7F,
        PhysicalKey.F17 => 0x80,
        PhysicalKey.F18 => 0x81,
        PhysicalKey.F19 => 0x82,
        PhysicalKey.F20 => 0x83,
        PhysicalKey.F21 => 0x84,
        PhysicalKey.F22 => 0x85,
        PhysicalKey.F23 => 0x86,
        PhysicalKey.F24 => 0x87,

        PhysicalKey.NumPad0 => 0x60,
        PhysicalKey.NumPad1 => 0x61,
        PhysicalKey.NumPad2 => 0x62,
        PhysicalKey.NumPad3 => 0x63,
        PhysicalKey.NumPad4 => 0x64,
        PhysicalKey.NumPad5 => 0x65,
        PhysicalKey.NumPad6 => 0x66,
        PhysicalKey.NumPad7 => 0x67,
        PhysicalKey.NumPad8 => 0x68,
        PhysicalKey.NumPad9 => 0x69,
        PhysicalKey.NumPadMultiply => 0x6A,
        PhysicalKey.NumPadAdd => 0x6B,
        PhysicalKey.NumPadSubtract => 0x6D,
        PhysicalKey.NumPadDecimal => 0x6E,
        PhysicalKey.NumPadDivide => 0x6F,

        PhysicalKey.Backspace => 0x08,
        PhysicalKey.Tab => 0x09,
        PhysicalKey.Enter or PhysicalKey.NumPadEnter => 0x0D,
        PhysicalKey.Escape => 0x1B,
        PhysicalKey.Space => 0x20,
        PhysicalKey.PageUp => 0x21,
        PhysicalKey.PageDown => 0x22,
        PhysicalKey.End => 0x23,
        PhysicalKey.Home => 0x24,
        PhysicalKey.ArrowLeft => 0x25,
        PhysicalKey.ArrowUp => 0x26,
        PhysicalKey.ArrowRight => 0x27,
        PhysicalKey.ArrowDown => 0x28,
        PhysicalKey.Insert => 0x2D,
        PhysicalKey.Delete => 0x2E,

        PhysicalKey.Semicolon => 0xBA,
        PhysicalKey.Equal => 0xBB,
        PhysicalKey.Comma => 0xBC,
        PhysicalKey.Minus => 0xBD,
        PhysicalKey.Period => 0xBE,
        PhysicalKey.Slash => 0xBF,
        PhysicalKey.Backquote => 0xC0,
        PhysicalKey.BracketLeft => 0xDB,
        PhysicalKey.Backslash => 0xDC,
        PhysicalKey.BracketRight => 0xDD,
        PhysicalKey.Quote => 0xDE,
        PhysicalKey.IntlBackslash => 0xE2,
        _ => 0,
    };


    private void ProbeWindowsHotKeyConflict(HotKey hotKey, int virtualKey)
    {
        // LightBulb intentionally executes shortcuts through WH_KEYBOARD_LL instead of WM_HOTKEY,
        // so another app owning a RegisterHotKey combination does not automatically make the
        // shortcut unusable. Probe RegisterHotKey only to give the user evidence of a likely clash.
        // The temporary probe is immediately released and never becomes LightBulb's input path.
        var modifiers = ConvertRegisterHotKeyModifiers(hotKey.Modifiers) | ModNoRepeat;
        var id = unchecked(++_probeHotKeyId);

        if (RegisterHotKey(0, id, modifiers, unchecked((uint)virtualKey)))
        {
            UnregisterHotKey(0, id);
            return;
        }

        var error = Marshal.GetLastWin32Error();
        if (error != ErrorHotKeyAlreadyRegistered)
        {
            _logService.Warning(
                nameof(HotKeyService),
                $"Could not probe Windows ownership for {hotKey}. Windows error {error}."
            );
            return;
        }

        var warning = $"{hotKey}: Windows reports this combination is already registered by another application. "
            + "LightBulb will still try its low-level hook, but the other application's hook may win if it consumes the key first.";

        lock (_gate)
        {
            if (!_conflictWarnings.Exists(x => string.Equals(x, warning, StringComparison.Ordinal)))
                _conflictWarnings.Add(warning);
        }

        _logService.Warning(nameof(HotKeyService), warning);
        RegistrationStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private static uint ConvertRegisterHotKeyModifiers(KeyModifiers modifiers)
    {
        var result = 0u;
        if (modifiers.HasFlag(KeyModifiers.Alt)) result |= ModAlt;
        if (modifiers.HasFlag(KeyModifiers.Control)) result |= ModControl;
        if (modifiers.HasFlag(KeyModifiers.Shift)) result |= ModShift;
        if (modifiers.HasFlag(KeyModifiers.Meta)) result |= ModWin;
        return result;
    }

    private void StopHook()
    {
        if (_hook == 0)
            return;

        if (!UnhookWindowsHookEx(_hook))
        {
            var error = Marshal.GetLastWin32Error();
            _logService.Warning(nameof(HotKeyService), $"Could not unregister global keyboard hook. Windows error {error}.");
        }

        _hook = 0;
        lock (_gate)
            _keysDown.Clear();
    }

    private void AddRegistrationError(string message)
    {
        lock (_gate)
            _registrationErrors.Add(message);

        _logService.Warning(nameof(HotKeyService), message);
        RegistrationStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        UnregisterAllHotKeys();

        StopHook();
    }

    private readonly record struct Registration(
        int VirtualKey,
        KeyModifiers Modifiers,
        Action Callback,
        HotKey DisplayHotKey
    );

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    private delegate nint LowLevelKeyboardProc(int code, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(
        int idHook,
        LowLevelKeyboardProc callback,
        nint module,
        uint threadId
    );

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint window, int id);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);
}
