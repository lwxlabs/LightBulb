using System;
using System.Globalization;
using Avalonia.Data.Converters;
using LightBulb.Models;

namespace LightBulb.Converters;

public sealed class TrayLeftClickActionToStringConverter : IValueConverter
{
    public static TrayLeftClickActionToStringConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            TrayLeftClickAction.ShowAppWindow => "Toggle app window",
            TrayLeftClickAction.ShowTrayMenu => "Show tray menu",
            TrayLeftClickAction.ToggleLightBulb => "Enable / disable LightBulb",
            TrayLeftClickAction.CycleProfiles => "Toggle profiles (includes Automatic)",
            _ => "Toggle app window",
        };

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}
