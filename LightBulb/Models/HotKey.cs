using System.Text;
using Avalonia.Input;

namespace LightBulb.Models;

public readonly record struct HotKey(PhysicalKey Key, KeyModifiers Modifiers = KeyModifiers.None)
{
    public static HotKey None { get; } = new();

    public override string ToString()
    {
        if (Key == PhysicalKey.None && Modifiers == KeyModifiers.None)
            return "< None >";

        var buffer = new StringBuilder();

        if (Modifiers.HasFlag(KeyModifiers.Control))
            buffer.Append("Ctrl + ");
        if (Modifiers.HasFlag(KeyModifiers.Shift))
            buffer.Append("Shift + ");
        if (Modifiers.HasFlag(KeyModifiers.Alt))
            buffer.Append("Alt + ");
        if (Modifiers.HasFlag(KeyModifiers.Meta))
            buffer.Append("Win + ");

        buffer.Append(GetDisplayKey(Key));

        return buffer.ToString();
    }

    private static string GetDisplayKey(PhysicalKey key)
    {
        var value = (int)key;
        if (value >= (int)PhysicalKey.A && value <= (int)PhysicalKey.Z)
            return ((char)('A' + value - (int)PhysicalKey.A)).ToString();
        if (value >= (int)PhysicalKey.Digit0 && value <= (int)PhysicalKey.Digit9)
            return ((char)('0' + value - (int)PhysicalKey.Digit0)).ToString();

        return key switch
        {
            PhysicalKey.ArrowLeft => "Left",
            PhysicalKey.ArrowRight => "Right",
            PhysicalKey.ArrowUp => "Up",
            PhysicalKey.ArrowDown => "Down",
            _ => key.ToString(),
        };
    }
}
