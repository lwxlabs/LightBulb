using System;
using System.Globalization;
using Avalonia.Data.Converters;
using LightBulb.ViewModels.Components.Settings;
using Material.Icons;

namespace LightBulb.Converters;

public class SettingsTabToMaterialIconKindConverter : IValueConverter
{
    public static SettingsTabToMaterialIconKindConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            // Use distinct icons already proven by the existing Material.Icons package in this app.
            GeneralSettingsTabViewModel => MaterialIconKind.WhiteBalanceSunny,
            ProfileSettingsTabViewModel => MaterialIconKind.Apps,
            ToneProfileSettingsTabViewModel => MaterialIconKind.WhiteBalanceSunny,
            LocationSettingsTabViewModel => MaterialIconKind.Globe,
            HotKeySettingsTabViewModel => MaterialIconKind.Keyboard,
            ApplicationWhitelistSettingsTabViewModel => MaterialIconKind.CheckboxesMarked,
            AdvancedSettingsTabViewModel => MaterialIconKind.Settings,
            LogSettingsTabViewModel => MaterialIconKind.Cancel,
            _ => MaterialIconKind.QuestionMark,
        };

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    ) => throw new NotSupportedException();
}
