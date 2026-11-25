using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using LightBulb.Models;
using LightBulb.Services;

namespace LightBulb.Views.Controls;

public partial class HotKeyTextBox : UserControl
{
    private const int VkShift = 0x10;
    private const int VkControl = 0x11;
    private const int VkMenu = 0x12;
    private const int VkLWin = 0x5B;
    private const int VkRWin = 0x5C;

    public static readonly StyledProperty<HotKey> HotKeyProperty = AvaloniaProperty.Register<
        HotKeyTextBox,
        HotKey
    >(nameof(HotKey), defaultBindingMode: BindingMode.TwoWay);

    private KeyModifiers _pressedModifiers;
    private bool _isCapturing;
    private bool _hasCandidate;

    static HotKeyTextBox()
    {
        HotKeyProperty.Changed.AddClassHandler<HotKeyTextBox>((control, _) => control.RefreshText());
    }

    public HotKeyTextBox()
    {
        InitializeComponent();

        TextBox.AddHandler(KeyDownEvent, TextBox_OnKeyDown, RoutingStrategies.Tunnel);
        TextBox.AddHandler(KeyUpEvent, TextBox_OnKeyUp, RoutingStrategies.Tunnel);
        TextBox.PointerPressed += (_, _) => BeginCapture();
        TextBox.GotFocus += (_, _) => BeginCapture();
        TextBox.LostFocus += (_, _) => EndCapture();
        DetachedFromVisualTree += (_, _) => EndCapture();

        RefreshText();
    }

    public HotKey HotKey
    {
        get => GetValue(HotKeyProperty);
        set => SetValue(HotKeyProperty, value);
    }

    private void BeginCapture()
    {
        if (!_isCapturing)
        {
            _isCapturing = true;
            HotKeyCaptureState.Enter();
        }

        _pressedModifiers = KeyModifiers.None;
        _hasCandidate = false;
        // Start empty on focus. After a complete combination is captured it is shown, but capture
        // stays active so the user can press another combination to replace it without clicking again.
        TextBox.Text = string.Empty;
        TextBox.Focus();
    }

    private void EndCapture()
    {
        if (_isCapturing)
        {
            _isCapturing = false;
            HotKeyCaptureState.Leave();
        }

        _pressedModifiers = KeyModifiers.None;
        _hasCandidate = false;
        RefreshText();
    }

    private void RefreshText()
    {
        if (_isCapturing)
            return;

        TextBox.Text = HotKey.ToString();
    }

    private void ResetButton_OnClick(object? sender, RoutedEventArgs args)
    {
        HotKey = HotKey.None;
        EndCapture();
        args.Handled = true;
    }

    private void TextBox_OnKeyDown(object? sender, KeyEventArgs args)
    {
        if (!_isCapturing)
            return;

        args.Handled = true;

        var physicalKey = args.PhysicalKey;
        if (physicalKey == PhysicalKey.None)
            return;

        var modifier = GetModifier(physicalKey);
        if (modifier != KeyModifiers.None)
        {
            // A modifier pressed after a completed candidate starts a fresh attempt immediately.
            // Keep keyboard focus/capture instead of forcing the user to click the field again.
            if (_hasCandidate)
            {
                _pressedModifiers = KeyModifiers.None;
                _hasCandidate = false;
                TextBox.Text = string.Empty;
            }

            _pressedModifiers |= modifier;
            return;
        }

        // Combine Avalonia's event modifiers, the modifiers observed by this control, and the native
        // hardware state. This avoids losing Ctrl/Alt due to event
        // ordering or programmable-keyboard/mouse timing.
        var modifiers = args.KeyModifiers | _pressedModifiers | GetNativeModifiers();

        // Delete/Back/Escape with no modifiers clears the shortcut.
        if (
            physicalKey is PhysicalKey.Delete or PhysicalKey.Backspace or PhysicalKey.Escape
            && modifiers == KeyModifiers.None
        )
        {
            HotKey = HotKey.None;
            _hasCandidate = false;
            _pressedModifiers = KeyModifiers.None;
            TextBox.Text = string.Empty;
            return;
        }

        // Enter/Space/Tab must include a modifier so ordinary keyboard navigation remains usable.
        if (
            physicalKey is PhysicalKey.Enter or PhysicalKey.Space or PhysicalKey.Tab
            && modifiers == KeyModifiers.None
        )
        {
            return;
        }

        // Character keys without Ctrl/Alt/Win are intentionally rejected as global shortcuts.
        if (
            physicalKey.ToQwertyKeySymbol() is not null
            && !modifiers.HasFlag(KeyModifiers.Control)
            && !modifiers.HasFlag(KeyModifiers.Alt)
            && !modifiers.HasFlag(KeyModifiers.Meta)
        )
        {
            return;
        }

        // Avalonia documents Key (not PhysicalKey) as the value generally used for shortcuts.
        // Normalize letters/digits/function/navigation keys to the logical Key value before storing
        // them in the legacy PhysicalKey-backed settings model. This preserves JSON compatibility
        // while making the shortcut shown as "L" actually correspond to Windows VK_L on any layout.
        var shortcutKey = NormalizeShortcutKey(args.Key, physicalKey);
        HotKey = new HotKey(shortcutKey, modifiers);
        _hasCandidate = true;
        TextBox.Text = HotKey.ToString();

        // Do not end capture here. As long as the field keeps focus, the next modifier/key
        // combination replaces this candidate. Clicking elsewhere finalizes it.
    }

    private void TextBox_OnKeyUp(object? sender, KeyEventArgs args)
    {
        if (!_isCapturing)
            return;

        var modifier = GetModifier(args.PhysicalKey);
        if (modifier != KeyModifiers.None)
        {
            _pressedModifiers &= ~modifier;
            args.Handled = true;
        }
    }


    private static PhysicalKey NormalizeShortcutKey(Key logicalKey, PhysicalKey fallback) => logicalKey switch
    {
        // Store logical shortcut meaning in the existing PhysicalKey-backed JSON model.
        Key.A => PhysicalKey.A,
        Key.B => PhysicalKey.B,
        Key.C => PhysicalKey.C,
        Key.D => PhysicalKey.D,
        Key.E => PhysicalKey.E,
        Key.F => PhysicalKey.F,
        Key.G => PhysicalKey.G,
        Key.H => PhysicalKey.H,
        Key.I => PhysicalKey.I,
        Key.J => PhysicalKey.J,
        Key.K => PhysicalKey.K,
        Key.L => PhysicalKey.L,
        Key.M => PhysicalKey.M,
        Key.N => PhysicalKey.N,
        Key.O => PhysicalKey.O,
        Key.P => PhysicalKey.P,
        Key.Q => PhysicalKey.Q,
        Key.R => PhysicalKey.R,
        Key.S => PhysicalKey.S,
        Key.T => PhysicalKey.T,
        Key.U => PhysicalKey.U,
        Key.V => PhysicalKey.V,
        Key.W => PhysicalKey.W,
        Key.X => PhysicalKey.X,
        Key.Y => PhysicalKey.Y,
        Key.Z => PhysicalKey.Z,

        Key.D0 => PhysicalKey.Digit0,
        Key.D1 => PhysicalKey.Digit1,
        Key.D2 => PhysicalKey.Digit2,
        Key.D3 => PhysicalKey.Digit3,
        Key.D4 => PhysicalKey.Digit4,
        Key.D5 => PhysicalKey.Digit5,
        Key.D6 => PhysicalKey.Digit6,
        Key.D7 => PhysicalKey.Digit7,
        Key.D8 => PhysicalKey.Digit8,
        Key.D9 => PhysicalKey.Digit9,

        Key.F1 => PhysicalKey.F1,
        Key.F2 => PhysicalKey.F2,
        Key.F3 => PhysicalKey.F3,
        Key.F4 => PhysicalKey.F4,
        Key.F5 => PhysicalKey.F5,
        Key.F6 => PhysicalKey.F6,
        Key.F7 => PhysicalKey.F7,
        Key.F8 => PhysicalKey.F8,
        Key.F9 => PhysicalKey.F9,
        Key.F10 => PhysicalKey.F10,
        Key.F11 => PhysicalKey.F11,
        Key.F12 => PhysicalKey.F12,
        Key.F13 => PhysicalKey.F13,
        Key.F14 => PhysicalKey.F14,
        Key.F15 => PhysicalKey.F15,
        Key.F16 => PhysicalKey.F16,
        Key.F17 => PhysicalKey.F17,
        Key.F18 => PhysicalKey.F18,
        Key.F19 => PhysicalKey.F19,
        Key.F20 => PhysicalKey.F20,
        Key.F21 => PhysicalKey.F21,
        Key.F22 => PhysicalKey.F22,
        Key.F23 => PhysicalKey.F23,
        Key.F24 => PhysicalKey.F24,

        Key.Left => PhysicalKey.ArrowLeft,
        Key.Up => PhysicalKey.ArrowUp,
        Key.Right => PhysicalKey.ArrowRight,
        Key.Down => PhysicalKey.ArrowDown,
        Key.Home => PhysicalKey.Home,
        Key.End => PhysicalKey.End,
        Key.PageUp => PhysicalKey.PageUp,
        Key.PageDown => PhysicalKey.PageDown,
        Key.Insert => PhysicalKey.Insert,
        Key.Delete => PhysicalKey.Delete,
        Key.Back => PhysicalKey.Backspace,
        Key.Tab => PhysicalKey.Tab,
        Key.Enter => PhysicalKey.Enter,
        Key.Escape => PhysicalKey.Escape,
        Key.Space => PhysicalKey.Space,
        _ => fallback,
    };

    private static KeyModifiers GetNativeModifiers()
    {
        var modifiers = KeyModifiers.None;
        if (IsPressed(VkControl))
            modifiers |= KeyModifiers.Control;
        if (IsPressed(VkShift))
            modifiers |= KeyModifiers.Shift;
        if (IsPressed(VkMenu))
            modifiers |= KeyModifiers.Alt;
        if (IsPressed(VkLWin) || IsPressed(VkRWin))
            modifiers |= KeyModifiers.Meta;
        return modifiers;
    }

    private static bool IsPressed(int virtualKey) => (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    private static KeyModifiers GetModifier(PhysicalKey key) =>
        key switch
        {
            PhysicalKey.ControlLeft or PhysicalKey.ControlRight => KeyModifiers.Control,
            PhysicalKey.AltLeft or PhysicalKey.AltRight => KeyModifiers.Alt,
            PhysicalKey.ShiftLeft or PhysicalKey.ShiftRight => KeyModifiers.Shift,
            PhysicalKey.MetaLeft or PhysicalKey.MetaRight => KeyModifiers.Meta,
            _ => KeyModifiers.None,
        };

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
